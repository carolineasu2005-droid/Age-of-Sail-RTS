"""Formal station-based hull generation for the Blender ship pipeline.

The control mesh is created natively in the production coordinate system:
X longitudinal (-X bow, +X stern), Y transverse (-Y port, +Y starboard),
and +Z up. A port half is generated first and mirrored by index while sharing
every centerline vertex; no object rotation or duplicate seam is used.
"""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Iterable

import bmesh
import bpy
from mathutils import Vector

from ship.longitudinal import (
    LongitudinalSample,
    clamp,
    distributed_positions,
    profile_knots,
    sample_longitudinal,
    station_positions,
)


FORMAL_HULL_OBJECT_NAME = "Hull_Main"
HULL_OBJECT_NAME = "SHIP_Gelderland1634_Hull_Proxy"
CENTERLINE_EPSILON = 1.0e-8


@dataclass(frozen=True)
class HullSurfaceSample:
    normalized_x: float
    normalized_z: float
    x: float
    y: float
    z: float

    def as_tuple(self) -> tuple[float, float, float]:
        return self.x, self.y, self.z


def _mapping(config: dict, primary: str, fallback: str | None = None) -> dict:
    value = config.get(primary)
    if isinstance(value, dict):
        return value
    if fallback:
        value = config.get(fallback)
        if isinstance(value, dict):
            return value
    return {}


def _number(values: dict, key: str, default: float) -> float:
    value = values.get(key, default)
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return float(default)
    value = float(value)
    return value if math.isfinite(value) else float(default)


def _section_anchor_data(config: dict, station: LongitudinalSample) -> tuple[list[float], list[float]]:
    """Return semantic Z anchors and half-width fractions for one station."""
    hull = _mapping(config, "hull", "hull_shape")
    character = station.character
    tumble_start = clamp(_number(hull, "tumblehome_start_z_ratio", 0.67), 0.60, 0.82)
    max_breadth_z = clamp(character.max_breadth_z_ratio, 0.42, tumble_start - 0.045)
    full_bilge_z = max(0.34, max_breadth_z - 0.12)
    upper_side_z = min(tumble_start - 0.025, max_breadth_z + 0.075)
    late_tumble_z = min(0.88, tumble_start + 0.14)
    anchors = sorted({
        0.0,
        0.12,
        0.28,
        round(full_bilge_z, 9),
        round(max_breadth_z, 9),
        round(upper_side_z, 9),
        round(tumble_start, 9),
        round(late_tumble_z, 9),
        1.0,
    })
    semantic = {
        0.0: 0.0,
        0.12: character.floor_width,
        0.28: character.lower_bilge_width,
        round(full_bilge_z, 9): character.full_bilge_width,
        round(max_breadth_z, 9): 1.0,
        round(upper_side_z, 9): character.upper_side_width,
        round(tumble_start, 9): character.tumblehome_width,
        round(late_tumble_z, 9): character.tumblehome_width * 0.42 + character.rail_width * 0.58,
        1.0: character.rail_width,
    }
    return anchors, [semantic[value] for value in anchors]


def _interpolate_piecewise(position: float, anchors: list[float], values: list[float]) -> float:
    position = clamp(position, 0.0, 1.0)
    for index in range(len(anchors) - 1):
        left, right = anchors[index], anchors[index + 1]
        if position <= right or index == len(anchors) - 2:
            amount = (position - left) / max(right - left, 1.0e-9)
            amount = amount * amount * (3.0 - 2.0 * amount)
            return values[index] + (values[index + 1] - values[index]) * amount
    return values[-1]


def _stem_adjusted_x(config: dict, station: LongitudinalSample, normalized_z: float) -> float:
    if station.normalized_x > CENTERLINE_EPSILON:
        return station.x
    stem = _mapping(config, "stem")
    rake_m = max(0.0, _number(stem, "rake_m", _number(stem, "forward_extension_m", 0.68)))
    curvature = clamp(_number(stem, "curvature", _number(stem, "curvature_ratio", 0.22)), 0.0, 1.0)
    # Rail is the forward-most point; lower stem points move aft.
    return station.x + rake_m * (1.0 - normalized_z) ** (1.15 + curvature)


def sample_hull_surface(
    config: dict,
    normalized_x: float,
    normalized_z: float,
    side: str = "port",
) -> HullSurfaceSample:
    """Sample the external hull at bow-to-stern X and keel-to-rail Z ratios."""
    normalized_x = clamp(float(normalized_x), 0.0, 1.0)
    normalized_z = clamp(float(normalized_z), 0.0, 1.0)
    side_key = side.lower()
    if side_key not in {"port", "p", "starboard", "s"}:
        raise ValueError("Hull sample side must be 'port' or 'starboard'")
    dimensions = _mapping(config, "dimensions")
    beam = _number(dimensions, "beam_m", 8.6)
    station = sample_longitudinal(config, normalized_x)
    anchors, widths = _section_anchor_data(config, station)
    fraction = _interpolate_piecewise(normalized_z, anchors, widths)
    half_width = 0.5 * beam * station.character.width_scale * fraction
    y = -half_width if side_key in {"port", "p"} else half_width
    z = station.keel_z + station.section_height * normalized_z
    return HullSurfaceSample(
        normalized_x,
        normalized_z,
        _stem_adjusted_x(config, station, normalized_z),
        y,
        z,
    )


def sample_station(config: dict, x_m: float) -> LongitudinalSample:
    """Sample longitudinal values at an X coordinate expressed in meters."""
    dimensions = _mapping(config, "dimensions")
    length = _number(dimensions, "length_m", 36.4)
    normalized_x = clamp((float(x_m) + 0.5 * length) / length, 0.0, 1.0)
    return sample_longitudinal(config, normalized_x)


def sample_hull(config: dict, x_m: float) -> dict:
    """Expose the small sampler contract consumed by structure modules."""
    dimensions = _mapping(config, "dimensions")
    length = _number(dimensions, "length_m", 36.4)
    beam = _number(dimensions, "beam_m", 8.6)
    normalized_x = clamp((float(x_m) + 0.5 * length) / length, 0.0, 1.0)
    station = sample_longitudinal(config, normalized_x)

    def half_width_at(normalized_z: float) -> float:
        return abs(sample_hull_surface(config, normalized_x, normalized_z, "starboard").y)

    maximum = 0.5 * beam * station.character.width_scale
    return {
        "x": station.x,
        "keel_z": station.keel_z,
        "sheer_z": station.rail_z,
        "deck_z": station.rail_z,
        "max_half_width": maximum,
        "rail_half_width": maximum * station.character.rail_width,
        "half_width_at": half_width_at,
    }


def sample_section(
    config: dict,
    x_m: float,
    side: str = "port",
    resolution: int | None = None,
) -> list[tuple[float, float, float]]:
    """Return a semantic keel-to-rail section for dependent structures."""
    dimensions = _mapping(config, "dimensions")
    generation = _mapping(config, "generation")
    length = _number(dimensions, "length_m", 36.4)
    normalized_x = clamp((float(x_m) + 0.5 * length) / length, 0.0, 1.0)
    station = sample_longitudinal(config, normalized_x)
    anchors, _ = _section_anchor_data(config, station)
    if resolution is None:
        resolution = int(
            _number(generation, "cross_section_resolution", _number(generation, "vertical_sections", 13))
        )
    positions = distributed_positions(max(int(resolution), len(anchors)), anchors)
    return [sample_hull_surface(config, normalized_x, value, side).as_tuple() for value in positions]


def _coordinate_key(coordinate: tuple[float, float, float], digits: int = 9) -> tuple[float, float, float]:
    return tuple(round(component, digits) for component in coordinate)


def _mirror_port_half(
    port_vertices: list[tuple[float, float, float]],
    port_faces: list[list[int]],
    port_face_kinds: list[str],
) -> tuple[list[tuple[float, float, float]], list[list[int]], list[str]]:
    """Mirror a port half while reusing, rather than duplicating, seam indices."""
    vertices = list(port_vertices)
    mirror_index: dict[int, int] = {}
    for index, (x, y, z) in enumerate(port_vertices):
        if abs(y) <= CENTERLINE_EPSILON:
            mirror_index[index] = index
        else:
            mirror_index[index] = len(vertices)
            vertices.append((x, -y, z))
    faces = list(port_faces)
    face_kinds = list(port_face_kinds)
    known_faces = {tuple(sorted(face)) for face in faces}
    for face, kind in zip(port_faces, port_face_kinds):
        mirrored = [mirror_index[index] for index in reversed(face)]
        if len(set(mirrored)) < 3:
            continue
        signature = tuple(sorted(mirrored))
        if signature not in known_faces:
            faces.append(mirrored)
            face_kinds.append(kind)
            known_faces.add(signature)
    return vertices, faces, face_kinds


def _build_control_mesh(config: dict) -> tuple[
    list[tuple[float, float, float]],
    list[list[int]],
    list[str],
    int,
    int,
]:
    generation = _mapping(config, "generation")
    longitudinal_count = max(17, int(_number(generation, "longitudinal_sections", 41)))
    requested_sections = max(
        9,
        int(_number(generation, "cross_section_resolution", _number(generation, "vertical_sections", 13))),
    )
    normalized_stations = station_positions(config, longitudinal_count)
    # Keep one shared, low-density strip lattice. The maximum-beam section's
    # semantic rows include every important inflection (floor, bilge,
    # breadth, tumblehome); station-specific character is sampled on it.
    semantic_anchors, _ = _section_anchor_data(
        config,
        sample_longitudinal(config, profile_knots(config)[2]),
    )
    section_count = max(requested_sections, len(semantic_anchors))
    normalized_sections = distributed_positions(section_count, semantic_anchors)

    port_vertices: list[tuple[float, float, float]] = []
    port_rows: list[list[int]] = []
    center_top_indices: list[int] = []
    for normalized_x in normalized_stations:
        row: list[int] = []
        for normalized_z in normalized_sections:
            sample = sample_hull_surface(config, normalized_x, normalized_z, "port")
            row.append(len(port_vertices))
            port_vertices.append(sample.as_tuple())
        port_rows.append(row)
        rail_index = row[-1]
        rail = port_vertices[rail_index]
        if abs(rail[1]) <= CENTERLINE_EPSILON:
            center_top_indices.append(rail_index)
        else:
            center_top_indices.append(len(port_vertices))
            port_vertices.append((rail[0], 0.0, rail[2]))

    port_faces: list[list[int]] = []
    port_face_kinds: list[str] = []
    for station_index in range(len(port_rows) - 1):
        current, following = port_rows[station_index], port_rows[station_index + 1]
        for section_index in range(section_count - 1):
            port_faces.append([
                current[section_index],
                following[section_index],
                following[section_index + 1],
                current[section_index + 1],
            ])
            port_face_kinds.append("side")
        top_face = [
            current[-1],
            following[-1],
            center_top_indices[station_index + 1],
            center_top_indices[station_index],
        ]
        top_face = list(dict.fromkeys(top_face))
        if len(top_face) >= 3:
            port_faces.append(top_face)
            port_face_kinds.append("top_closure")

    stern_face = list(dict.fromkeys([center_top_indices[-1], *reversed(port_rows[-1])]))
    port_faces.append(stern_face)
    port_face_kinds.append("stern_closure")
    vertices, faces, kinds = _mirror_port_half(port_vertices, port_faces, port_face_kinds)
    return vertices, faces, kinds, len(normalized_stations), section_count


def _recalculate_normals(mesh: bpy.types.Mesh) -> None:
    bm = bmesh.new()
    try:
        bm.from_mesh(mesh)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(mesh)
    finally:
        bm.free()
    mesh.update(calc_edges=True)


def _edge_usage(polygons: Iterable[bpy.types.MeshPolygon]) -> dict[tuple[int, int], int]:
    usage: dict[tuple[int, int], int] = {}
    for polygon in polygons:
        indices = list(polygon.vertices)
        for index, vertex_a in enumerate(indices):
            vertex_b = indices[(index + 1) % len(indices)]
            edge = tuple(sorted((vertex_a, vertex_b)))
            usage[edge] = usage.get(edge, 0) + 1
    return usage


def _validate_mesh(hull: bpy.types.Object) -> None:
    mesh = hull.data
    if not mesh.vertices or not mesh.polygons:
        raise RuntimeError("Generated formal hull has no vertices or faces")
    if any(not math.isfinite(component) for vertex in mesh.vertices for component in vertex.co):
        raise RuntimeError("Generated formal hull contains NaN or infinite coordinates")
    coordinate_indices: dict[tuple[float, float, float], list[int]] = {}
    for vertex in mesh.vertices:
        coordinate_indices.setdefault(_coordinate_key(tuple(vertex.co)), []).append(vertex.index)
    duplicates = [indices for indices in coordinate_indices.values() if len(indices) > 1]
    if duplicates:
        raise RuntimeError(f"Generated formal hull contains {len(duplicates)} duplicate vertex coordinates")

    face_signatures: set[tuple[int, ...]] = set()
    for polygon in mesh.polygons:
        if polygon.area <= 1.0e-10 or polygon.normal.length <= 1.0e-8:
            raise RuntimeError(f"Generated formal hull contains zero-area face {polygon.index}")
        signature = tuple(sorted(polygon.vertices))
        if signature in face_signatures:
            raise RuntimeError("Generated formal hull contains a duplicate face")
        face_signatures.add(signature)
        if all(abs(mesh.vertices[index].co.y) <= CENTERLINE_EPSILON for index in polygon.vertices):
            raise RuntimeError(f"Generated formal hull contains an internal centerline face {polygon.index}")
    bad_edges = [edge for edge, count in _edge_usage(mesh.polygons).items() if count != 2]
    if bad_edges:
        raise RuntimeError(f"Generated formal hull is not watertight ({len(bad_edges)} unexpected edges)")
    coordinates = set(coordinate_indices)
    for x, y, z in coordinates:
        if (x, round(-y, 9), z) not in coordinates:
            raise RuntimeError("Generated formal hull is not exactly symmetric across Y=0")


def create_formal_hull(config: dict, object_name: str = FORMAL_HULL_OBJECT_NAME) -> bpy.types.Object:
    """Build a clean, watertight formal hull control mesh from configuration."""
    vertices, faces, face_kinds, station_count, section_count = _build_control_mesh(config)
    mesh = bpy.data.meshes.new(f"{object_name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    _recalculate_normals(mesh)
    for polygon, kind in zip(mesh.polygons, face_kinds):
        polygon.use_smooth = kind == "side"

    hull = bpy.data.objects.new(object_name, mesh)
    bpy.context.collection.objects.link(hull)
    hull.location = (0.0, 0.0, 0.0)
    hull.rotation_euler = (0.0, 0.0, 0.0)
    hull.scale = (1.0, 1.0, 1.0)
    hull["construction"] = "port_half_programmatic_y_mirror_shared_centerline"
    hull["coordinate_system"] = "X longitudinal; -X bow; +X stern; -Y port; +Y starboard; +Z up"
    hull["longitudinal_station_count"] = station_count
    hull["cross_section_vertex_count"] = section_count
    _validate_mesh(hull)
    return hull


def create_proxy_hull(config: dict, object_name: str = HULL_OBJECT_NAME) -> bpy.types.Object:
    """Compatibility wrapper retained for the Phase 3A build entry point."""
    return create_formal_hull(config, object_name=object_name)


def world_bounds(hull: bpy.types.Object) -> tuple[float, float, float, float, float, float]:
    points = [hull.matrix_world @ Vector(corner) for corner in hull.bound_box]
    return (
        min(point.x for point in points),
        max(point.x for point in points),
        min(point.y for point in points),
        max(point.y for point in points),
        min(point.z for point in points),
        max(point.z for point in points),
    )


def dimensions_from_bounds(hull: bpy.types.Object) -> tuple[float, float, float]:
    """Return formal (longitudinal length, transverse beam, vertical height)."""
    min_x, max_x, min_y, max_y, min_z, max_z = world_bounds(hull)
    return max_x - min_x, max_y - min_y, max_z - min_z
