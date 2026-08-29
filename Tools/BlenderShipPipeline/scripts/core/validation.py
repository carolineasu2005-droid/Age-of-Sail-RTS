"""Strict, reusable acceptance checks for generated ship blockouts.

The formal coordinate convention used here is X longitudinal, Y transverse,
and Z up, with bow at -X and port at -Y. Helpers raise ``ValidationError`` on
failure so Blender's ``--python-exit-code`` can propagate an honest non-zero
build result.
"""

from __future__ import annotations

import math
import re
from collections import Counter
from collections.abc import Iterable, Mapping, Sequence
from contextlib import contextmanager
from dataclasses import dataclass
from typing import Any, Iterator

import bpy
from mathutils import Vector


class ValidationError(RuntimeError):
    """A blocking production acceptance failure."""


@dataclass(frozen=True)
class Bounds:
    min_x: float
    max_x: float
    min_y: float
    max_y: float
    min_z: float
    max_z: float

    @property
    def length(self) -> float:
        return self.max_x - self.min_x

    @property
    def beam(self) -> float:
        return self.max_y - self.min_y

    @property
    def height(self) -> float:
        return self.max_z - self.min_z

    @property
    def center(self) -> Vector:
        return Vector(
            (
                (self.min_x + self.max_x) * 0.5,
                (self.min_y + self.max_y) * 0.5,
                (self.min_z + self.max_z) * 0.5,
            )
        )


@dataclass(frozen=True)
class GunportRecord:
    side: str
    index: int
    center: tuple[float, float, float]


REQUIRED_PRODUCTION_OBJECTS = {
    "Hull_Main",
    "Keel",
    "Stem",
    "Sternpost",
    "Rudder",
    "Deck_Main",
    "Deck_Forecastle",
    "Deck_Quarter",
    "Transom_Main",
    "Stern_Upperworks",
    "Bulwark_Port",
    "Bulwark_Starboard",
    "Wales_Main_Port",
    "Wales_Main_Starboard",
}

_FORBIDDEN_TOKENS = {
    "mast",
    "masts",
    "yard",
    "yards",
    "sail",
    "sails",
    "rigging",
    "ratline",
    "ratlines",
    "shroud",
    "shrouds",
    "anchor",
    "anchors",
    "boat",
    "boats",
    "launch",
    "launches",
    "crew",
    "cannon",
    "cannons",
    "gun",
    "guns",
    "carriage",
    "carriages",
    "barrel",
    "barrels",
    "crate",
    "crates",
    "rope",
    "ropes",
    "coil",
    "coils",
    "lantern",
    "lanterns",
    "flag",
    "flags",
    "figurehead",
    "sculpture",
    "sculptures",
}


def _as_objects(objects: Any) -> list[bpy.types.Object]:
    if objects is None:
        return []
    if isinstance(objects, bpy.types.Object):
        return [objects]
    return list(objects)


@contextmanager
def _mesh_view(
    object_: bpy.types.Object,
    *,
    evaluated: bool,
) -> Iterator[tuple[bpy.types.Mesh, Any]]:
    if object_.type != "MESH":
        raise ValidationError(f"Expected a mesh object, got {object_.name} ({object_.type})")
    if not evaluated:
        yield object_.data, object_.matrix_world
        return
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_object = object_.evaluated_get(depsgraph)
    mesh = evaluated_object.to_mesh()
    if mesh is None:
        raise ValidationError(f"Could not evaluate mesh object: {object_.name}")
    try:
        yield mesh, evaluated_object.matrix_world
    finally:
        evaluated_object.to_mesh_clear()


def _world_vertex_coordinates(object_: bpy.types.Object, *, evaluated: bool = True) -> list[Vector]:
    with _mesh_view(object_, evaluated=evaluated) as (mesh, matrix_world):
        return [matrix_world @ vertex.co for vertex in mesh.vertices]


def world_bounds(objects: Any, *, evaluated: bool = True) -> Bounds:
    """Return combined world bounds with X=length and Y=beam semantics."""
    coordinates: list[Vector] = []
    for object_ in _as_objects(objects):
        if object_.type == "MESH":
            coordinates.extend(_world_vertex_coordinates(object_, evaluated=evaluated))
    if not coordinates:
        raise ValidationError("Cannot calculate bounds: no mesh vertices were supplied")
    return Bounds(
        min(point.x for point in coordinates),
        max(point.x for point in coordinates),
        min(point.y for point in coordinates),
        max(point.y for point in coordinates),
        min(point.z for point in coordinates),
        max(point.z for point in coordinates),
    )


def validate_dimensions(
    hull: bpy.types.Object,
    config: Mapping[str, Any],
    *,
    full_ship_objects: Any = None,
) -> dict[str, float]:
    """Validate locked Hull_Main dimensions and separately report full bounds."""
    expected = config["dimensions"]
    settings = config.get("validation", {})
    dimension_tolerance = float(settings.get("dimension_abs_tolerance_m", 0.03))
    ratio_tolerance = float(settings.get("ratio_abs_tolerance", 0.02))
    hull_bounds = world_bounds(hull)
    expected_length = float(expected["length_m"])
    expected_beam = float(expected["beam_m"])
    if not math.isclose(hull_bounds.length, expected_length, rel_tol=0.0, abs_tol=dimension_tolerance):
        raise ValidationError(
            f"Hull_Main length mismatch: expected {expected_length:.3f} m, "
            f"got {hull_bounds.length:.3f} m"
        )
    if not math.isclose(hull_bounds.beam, expected_beam, rel_tol=0.0, abs_tol=dimension_tolerance):
        raise ValidationError(
            f"Hull_Main beam mismatch: expected {expected_beam:.3f} m, got {hull_bounds.beam:.3f} m"
        )
    if hull_bounds.height <= 0.0:
        raise ValidationError("Hull_Main has non-positive height")
    actual_ratio = hull_bounds.length / hull_bounds.beam
    expected_ratio = expected_length / expected_beam
    if not math.isclose(actual_ratio, expected_ratio, rel_tol=0.0, abs_tol=ratio_tolerance):
        raise ValidationError(
            f"Hull_Main L/B mismatch: expected {expected_ratio:.3f}, got {actual_ratio:.3f}"
        )
    full_bounds = world_bounds(full_ship_objects if full_ship_objects is not None else hull)
    return {
        "hull_length_m": hull_bounds.length,
        "hull_beam_m": hull_bounds.beam,
        "hull_height_m": hull_bounds.height,
        "hull_length_beam_ratio": actual_ratio,
        "full_length_m": full_bounds.length,
        "full_beam_m": full_bounds.beam,
        "full_height_m": full_bounds.height,
    }


def _quantized_coordinate(coordinate: Sequence[float], tolerance: float) -> tuple[int, int, int]:
    return tuple(int(round(float(value) / tolerance)) for value in coordinate)  # type: ignore[return-value]


def validate_topology(
    object_: bpy.types.Object,
    *,
    closed: bool = True,
    duplicate_tolerance: float = 1e-6,
    zero_area_tolerance: float = 1e-10,
    evaluated: bool = True,
) -> dict[str, int | float]:
    """Check finite geometry, duplicates, face area, incidence, and density.

    General BVH self-intersection is intentionally not treated as blocking here:
    coplanar adjacent strips create false positives. A hull generator should add
    deterministic station/profile intersection checks for its own control data.
    """
    if duplicate_tolerance <= 0.0 or zero_area_tolerance <= 0.0:
        raise ValueError("Topology tolerances must be positive")
    with _mesh_view(object_, evaluated=evaluated) as (mesh, _matrix_world):
        if not mesh.vertices or not mesh.polygons:
            raise ValidationError(f"{object_.name} has no usable mesh geometry")
        coordinates = [tuple(float(value) for value in vertex.co) for vertex in mesh.vertices]
        if any(not math.isfinite(value) for coordinate in coordinates for value in coordinate):
            raise ValidationError(f"{object_.name} contains NaN or infinite coordinates")

        quantized = [_quantized_coordinate(coordinate, duplicate_tolerance) for coordinate in coordinates]
        coordinate_counts = Counter(quantized)
        duplicate_vertices = sum(count - 1 for count in coordinate_counts.values() if count > 1)
        if duplicate_vertices:
            raise ValidationError(f"{object_.name} contains {duplicate_vertices} duplicate vertices")

        zero_area_faces = [polygon.index for polygon in mesh.polygons if polygon.area <= zero_area_tolerance]
        if zero_area_faces:
            raise ValidationError(
                f"{object_.name} contains {len(zero_area_faces)} zero-area polygons; "
                f"first indices: {zero_area_faces[:8]}"
            )
        invalid_normal_faces = [
            polygon.index
            for polygon in mesh.polygons
            if not all(math.isfinite(value) for value in polygon.normal) or polygon.normal.length <= 1e-12
        ]
        if invalid_normal_faces:
            raise ValidationError(f"{object_.name} contains invalid polygon normals")

        face_keys = [tuple(sorted(int(index) for index in polygon.vertices)) for polygon in mesh.polygons]
        duplicate_faces = sum(count - 1 for count in Counter(face_keys).values() if count > 1)
        if duplicate_faces:
            raise ValidationError(f"{object_.name} contains {duplicate_faces} duplicate faces")

        edge_use = Counter()
        for polygon in mesh.polygons:
            indices = list(polygon.vertices)
            for index, vertex_a in enumerate(indices):
                vertex_b = indices[(index + 1) % len(indices)]
                edge_use[tuple(sorted((int(vertex_a), int(vertex_b))))] += 1
        zero_length_edges = 0
        for vertex_a, vertex_b in edge_use:
            distance = (Vector(coordinates[vertex_a]) - Vector(coordinates[vertex_b])).length
            if distance <= duplicate_tolerance:
                zero_length_edges += 1
        if zero_length_edges:
            raise ValidationError(f"{object_.name} contains {zero_length_edges} zero-length edges")

        boundary_edges = sum(1 for count in edge_use.values() if count == 1)
        overused_edges = sum(1 for count in edge_use.values() if count > 2)
        if overused_edges or (closed and boundary_edges):
            raise ValidationError(
                f"{object_.name} is non-manifold: boundary_edges={boundary_edges}, "
                f"edges_used_more_than_twice={overused_edges}"
            )

        mesh.calc_loop_triangles()
        signed_volume = 0.0
        for triangle in mesh.loop_triangles:
            vertex_a, vertex_b, vertex_c = (Vector(coordinates[index]) for index in triangle.vertices)
            signed_volume += vertex_a.dot(vertex_b.cross(vertex_c)) / 6.0
        if closed and abs(signed_volume) <= zero_area_tolerance:
            raise ValidationError(f"{object_.name} has zero enclosed volume")

        triangle_count = sum(1 for polygon in mesh.polygons if len(polygon.vertices) == 3)
        quad_count = sum(1 for polygon in mesh.polygons if len(polygon.vertices) == 4)
        ngon_count = len(mesh.polygons) - triangle_count - quad_count
        return {
            "vertices": len(mesh.vertices),
            "edges": len(edge_use),
            "faces": len(mesh.polygons),
            "triangles": triangle_count,
            "quads": quad_count,
            "ngons": ngon_count,
            "boundary_edges": boundary_edges,
            "signed_volume_m3": signed_volume,
        }


def validate_centerline(
    hull: bpy.types.Object,
    *,
    tolerance: float = 1e-6,
    evaluated: bool = True,
) -> dict[str, int]:
    """Reject duplicate Y=0 seam vertices and internal mirror-plane walls."""
    if tolerance <= 0.0:
        raise ValueError("Centerline tolerance must be positive")
    with _mesh_view(hull, evaluated=evaluated) as (mesh, matrix_world):
        world_coordinates = [matrix_world @ vertex.co for vertex in mesh.vertices]
        center_indices = {index for index, point in enumerate(world_coordinates) if abs(point.y) <= tolerance}
        if not center_indices:
            raise ValidationError(f"{hull.name} has no Y=0 centerline vertices")
        seam_keys = Counter(
            (int(round(world_coordinates[index].x / tolerance)), int(round(world_coordinates[index].z / tolerance)))
            for index in center_indices
        )
        duplicate_center_vertices = sum(count - 1 for count in seam_keys.values() if count > 1)
        if duplicate_center_vertices:
            raise ValidationError(
                f"{hull.name} has {duplicate_center_vertices} duplicate centerline vertices"
            )
        internal_center_faces = [
            polygon.index
            for polygon in mesh.polygons
            if polygon.vertices and all(int(index) in center_indices for index in polygon.vertices)
        ]
        if internal_center_faces:
            raise ValidationError(
                f"{hull.name} contains {len(internal_center_faces)} all-centerline faces "
                "(an internal mirror wall or zero-width strip)"
            )
        seam_edges = [
            edge
            for edge in mesh.edges
            if int(edge.vertices[0]) in center_indices and int(edge.vertices[1]) in center_indices
        ]
        seam_edge_keys = Counter(
            tuple(
                sorted(
                    (
                        (
                            int(round(world_coordinates[int(vertex_index)].x / tolerance)),
                            int(round(world_coordinates[int(vertex_index)].z / tolerance)),
                        )
                        for vertex_index in edge.vertices
                    )
                )
            )
            for edge in seam_edges
        )
        duplicate_seam_edges = sum(count - 1 for count in seam_edge_keys.values() if count > 1)
        if duplicate_seam_edges:
            raise ValidationError(f"{hull.name} has {duplicate_seam_edges} overlapping centerline edges")
        return {
            "centerline_vertices": len(center_indices),
            "centerline_edges": len(seam_edges),
            "centerline_faces": 0,
        }


def _reflected_key(key: tuple[int, int, int], axis_index: int) -> tuple[int, int, int]:
    values = list(key)
    values[axis_index] = -values[axis_index]
    return tuple(values)  # type: ignore[return-value]


def validate_symmetry(
    object_: bpy.types.Object,
    *,
    tolerance: float = 1e-4,
    axis: str = "Y",
    require_face_symmetry: bool = True,
    evaluated: bool = True,
) -> dict[str, int]:
    """Validate coordinate and optional topology symmetry across Y=0."""
    axis_index = {"X": 0, "Y": 1, "Z": 2}.get(axis.upper())
    if axis_index is None:
        raise ValueError(f"Unsupported symmetry axis: {axis}")
    with _mesh_view(object_, evaluated=evaluated) as (mesh, matrix_world):
        keys = [_quantized_coordinate(matrix_world @ vertex.co, tolerance) for vertex in mesh.vertices]
        vertex_counts = Counter(keys)
        missing_vertices = sum(
            max(0, count - vertex_counts.get(_reflected_key(key, axis_index), 0))
            for key, count in vertex_counts.items()
        )
        if missing_vertices:
            raise ValidationError(
                f"{object_.name} is not {axis.upper()}-symmetric: {missing_vertices} unmatched vertices"
            )
        unmatched_faces = 0
        if require_face_symmetry:
            face_counts = Counter(
                tuple(sorted(keys[int(index)] for index in polygon.vertices))
                for polygon in mesh.polygons
            )
            unmatched_faces = sum(
                max(
                    0,
                    count
                    - face_counts.get(
                        tuple(sorted(_reflected_key(vertex_key, axis_index) for vertex_key in face_key)),
                        0,
                    ),
                )
                for face_key, count in face_counts.items()
            )
            if unmatched_faces:
                raise ValidationError(
                    f"{object_.name} is not topology-symmetric: {unmatched_faces} unmatched faces"
                )
        return {"vertices": len(keys), "unmatched_vertices": 0, "unmatched_faces": unmatched_faces}


def validate_mirrored_pair(
    port_object: bpy.types.Object,
    starboard_object: bpy.types.Object,
    *,
    tolerance: float = 1e-4,
    evaluated: bool = True,
) -> dict[str, int]:
    """Compare separate Port/Starboard structures by reflecting world Y."""
    port_keys = Counter(
        _quantized_coordinate(point, tolerance)
        for point in _world_vertex_coordinates(port_object, evaluated=evaluated)
    )
    starboard_keys = Counter(
        _quantized_coordinate(point, tolerance)
        for point in _world_vertex_coordinates(starboard_object, evaluated=evaluated)
    )
    reflected_port = Counter({_reflected_key(key, 1): count for key, count in port_keys.items()})
    if reflected_port != starboard_keys:
        unmatched = sum((reflected_port - starboard_keys).values()) + sum((starboard_keys - reflected_port).values())
        raise ValidationError(
            f"{port_object.name} and {starboard_object.name} are not mirrored across Y=0 "
            f"({unmatched} unmatched vertices)"
        )
    return {"port_vertices": sum(port_keys.values()), "starboard_vertices": sum(starboard_keys.values())}


def _normalise_side(value: Any) -> str:
    side = str(value).strip().lower()
    aliases = {"port": "port", "-y": "port", "left": "port", "starboard": "starboard", "+y": "starboard", "right": "starboard"}
    if side not in aliases:
        raise ValidationError(f"Invalid gunport side: {value!r}")
    return aliases[side]


def _coerce_gunport_record(value: Any, fallback_index: int) -> GunportRecord:
    if isinstance(value, GunportRecord):
        return value
    if isinstance(value, Mapping):
        side = _normalise_side(value.get("side"))
        index = int(value.get("index", fallback_index))
        center_value = value.get("center", value.get("position"))
    elif isinstance(value, bpy.types.Object):
        side = _normalise_side(value.get("gunport_side", value.get("side", "")))
        index = int(value.get("gunport_index", fallback_index))
        center_value = tuple(value.matrix_world.translation)
    else:
        raise ValidationError(f"Unsupported gunport record: {value!r}")
    if not isinstance(center_value, Sequence) or len(center_value) != 3:
        raise ValidationError("Gunport records require a three-component center")
    center = tuple(float(component) for component in center_value)
    if any(not math.isfinite(component) for component in center):
        raise ValidationError("Gunport record contains a non-finite center")
    return GunportRecord(side=side, index=index, center=center)  # type: ignore[arg-type]


def validate_gunports(
    records: Iterable[Any],
    config: Mapping[str, Any],
    *,
    validate_limits: bool = True,
) -> dict[str, int | float]:
    """Validate dynamic count, X spacing, and Port/Starboard Y reflection."""
    parsed = [_coerce_gunport_record(value, index) for index, value in enumerate(records)]
    expected_count = int(config["gunports"]["count_per_side"])
    port = sorted((record for record in parsed if record.side == "port"), key=lambda record: record.center[0])
    starboard = sorted(
        (record for record in parsed if record.side == "starboard"),
        key=lambda record: record.center[0],
    )
    if len(port) != expected_count or len(starboard) != expected_count:
        raise ValidationError(
            f"Gunport count mismatch: expected {expected_count}/{expected_count}, "
            f"got Port={len(port)}, Starboard={len(starboard)}"
        )
    tolerance = float(config.get("validation", {}).get("symmetry_tolerance_m", 1e-4))
    for index, (port_record, starboard_record) in enumerate(zip(port, starboard)):
        px, py, pz = port_record.center
        sx, sy, sz = starboard_record.center
        if py >= -tolerance or sy <= tolerance:
            raise ValidationError(f"Gunport pair {index} is on the wrong side of Y=0")
        if not (
            math.isclose(px, sx, abs_tol=tolerance, rel_tol=0.0)
            and math.isclose(pz, sz, abs_tol=tolerance, rel_tol=0.0)
            and math.isclose(py, -sy, abs_tol=tolerance, rel_tol=0.0)
        ):
            raise ValidationError(
                f"Gunport pair {index} is not mirrored: Port={port_record.center}, "
                f"Starboard={starboard_record.center}"
            )

    spacing_tolerance = float(config.get("validation", {}).get("spacing_relative_tolerance", 0.03))
    port_spacing = [b.center[0] - a.center[0] for a, b in zip(port, port[1:])]
    starboard_spacing = [b.center[0] - a.center[0] for a, b in zip(starboard, starboard[1:])]
    all_spacing = port_spacing + starboard_spacing
    mean_spacing = sum(all_spacing) / len(all_spacing) if all_spacing else 0.0
    max_relative_deviation = (
        max(abs(spacing - mean_spacing) for spacing in all_spacing) / mean_spacing
        if mean_spacing > 0.0
        else 0.0
    )
    if max_relative_deviation > spacing_tolerance:
        raise ValidationError(
            f"Gunport spacing is inconsistent: max relative deviation "
            f"{max_relative_deviation:.4f} > {spacing_tolerance:.4f}"
        )

    if validate_limits and parsed:
        length = float(config["dimensions"]["length_m"])
        bow_x = -length * 0.5
        expected_forward = bow_x + float(config["gunports"]["forward_limit_ratio"]) * length
        expected_aft = bow_x + float(config["gunports"]["aft_limit_ratio"]) * length
        limit_tolerance = float(config.get("validation", {}).get("dimension_abs_tolerance_m", 0.03))
        if not math.isclose(port[0].center[0], expected_forward, abs_tol=limit_tolerance, rel_tol=0.0):
            raise ValidationError(
                f"Forward gunport limit mismatch: expected X={expected_forward:.3f}, "
                f"got X={port[0].center[0]:.3f}"
            )
        if not math.isclose(port[-1].center[0], expected_aft, abs_tol=limit_tolerance, rel_tol=0.0):
            raise ValidationError(
                f"Aft gunport limit mismatch: expected X={expected_aft:.3f}, "
                f"got X={port[-1].center[0]:.3f}"
            )

    return {
        "port_count": len(port),
        "starboard_count": len(starboard),
        "total_count": len(parsed),
        "mean_spacing_m": mean_spacing,
        "max_spacing_relative_deviation": max_relative_deviation,
    }


def validate_transforms(
    objects: Any,
    *,
    tolerance: float = 1e-6,
    require_zero_location: bool = False,
    require_identity_rotation: bool = True,
) -> dict[str, int]:
    """Require unit scale and prevent a rotated-object coordinate disguise."""
    failures = []
    inspected = _as_objects(objects)
    for object_ in inspected:
        scale = tuple(float(value) for value in object_.scale)
        rotation = tuple(float(value) for value in object_.rotation_euler)
        location = tuple(float(value) for value in object_.location)
        if any(not math.isfinite(value) for value in (*scale, *rotation, *location)):
            failures.append(f"{object_.name}: non-finite transform")
            continue
        if any(not math.isclose(value, 1.0, abs_tol=tolerance, rel_tol=0.0) for value in scale):
            failures.append(f"{object_.name}: scale={scale}")
        if require_identity_rotation and any(abs(value) > tolerance for value in rotation):
            failures.append(f"{object_.name}: rotation={rotation}")
        if require_zero_location and any(abs(value) > tolerance for value in location):
            failures.append(f"{object_.name}: location={location}")
    if failures:
        raise ValidationError("Invalid production transforms: " + "; ".join(failures))
    return {"objects_checked": len(inspected), "invalid_transforms": 0}


def _name_tokens(name: str) -> set[str]:
    # Gunport is a legal structural term and must not be misread as forbidden Gun.
    without_gunports = re.sub(r"gun[\s_.-]*ports?", "", name, flags=re.IGNORECASE)
    split_camel = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", without_gunports)
    return set(re.findall(r"[a-z]+", split_camel.lower()))


def validate_forbidden_assets(
    objects: Any,
    *,
    collections: Iterable[bpy.types.Collection] = (),
) -> dict[str, int]:
    """Scan structured names/roles without producing a Gunport false positive."""
    inspected_objects = _as_objects(objects)
    failures = []
    for object_ in inspected_objects:
        role = str(object_.get("ship_role", ""))
        forbidden = (_name_tokens(object_.name) | _name_tokens(role)).intersection(_FORBIDDEN_TOKENS)
        if forbidden:
            failures.append(f"object {object_.name!r}: {sorted(forbidden)}")
    inspected_collections = list(collections)
    for collection in inspected_collections:
        forbidden = _name_tokens(collection.name).intersection(_FORBIDDEN_TOKENS)
        if forbidden:
            failures.append(f"collection {collection.name!r}: {sorted(forbidden)}")
    if failures:
        raise ValidationError("Forbidden asset content found: " + "; ".join(failures))
    return {
        "objects_checked": len(inspected_objects),
        "collections_checked": len(inspected_collections),
        "forbidden_matches": 0,
    }


def validate_required_objects(
    objects: Any,
    *,
    required_names: Iterable[str] = REQUIRED_PRODUCTION_OBJECTS,
) -> dict[str, int]:
    names = {object_.name for object_ in _as_objects(objects)}
    required = set(required_names)
    missing = required - names
    if missing:
        raise ValidationError(f"Required production objects are missing: {sorted(missing)}")
    return {"required_objects": len(required), "missing_objects": 0}


def measure_region_width(
    hull: bpy.types.Object,
    *,
    x_ratio_range: tuple[float, float] = (0.0, 1.0),
    z_ratio_range: tuple[float, float] = (0.0, 1.0),
    evaluated: bool = True,
) -> dict[str, int | float]:
    """Measure a local hull-width signature for parameter-response tests.

    X ratios are always measured bow-to-stern: 0 at minimum X, 1 at maximum X.
    """
    x_start, x_end = x_ratio_range
    z_start, z_end = z_ratio_range
    if not (0.0 <= x_start < x_end <= 1.0 and 0.0 <= z_start < z_end <= 1.0):
        raise ValueError("Region ranges must be increasing ratios within [0, 1]")
    bounds = world_bounds(hull, evaluated=evaluated)
    x_min = bounds.min_x + bounds.length * x_start
    x_max = bounds.min_x + bounds.length * x_end
    z_min = bounds.min_z + bounds.height * z_start
    z_max = bounds.min_z + bounds.height * z_end
    points = [
        point
        for point in _world_vertex_coordinates(hull, evaluated=evaluated)
        if x_min - 1e-9 <= point.x <= x_max + 1e-9 and z_min - 1e-9 <= point.z <= z_max + 1e-9
    ]
    if not points:
        raise ValidationError(
            f"No hull vertices fall in response-test region X={x_ratio_range}, Z={z_ratio_range}"
        )
    half_widths = [abs(point.y) for point in points]
    return {
        "vertex_count": len(points),
        "mean_half_width_m": sum(half_widths) / len(half_widths),
        "max_full_width_m": 2.0 * max(half_widths),
        "width_signature": sum(half_widths),
    }


def validate_parameter_response(
    test_name: str,
    baseline_metrics: Mapping[str, float],
    variant_metrics: Mapping[str, float],
    *,
    changed: Mapping[str, float | Mapping[str, Any]],
    unchanged: Mapping[str, float] = {},
) -> dict[str, float]:
    """Validate changed metrics and protected invariants for a temporary build.

    A changed spec may be a minimum absolute delta or a mapping containing
    ``min_abs_delta`` and optional ``direction`` (``increase``/``decrease``).
    Unchanged values map metric names to their maximum absolute delta.
    """
    deltas: dict[str, float] = {}
    for metric, specification in changed.items():
        baseline, variant = _metric_pair(test_name, metric, baseline_metrics, variant_metrics)
        delta = variant - baseline
        deltas[metric] = delta
        if isinstance(specification, Mapping):
            minimum = float(specification.get("min_abs_delta", 0.0))
            direction = specification.get("direction")
        else:
            minimum = float(specification)
            direction = None
        if minimum < 0.0 or abs(delta) < minimum:
            raise ValidationError(
                f"Parameter response '{test_name}' did not change {metric} enough: "
                f"delta={delta:.6g}, required={minimum:.6g}"
            )
        if direction == "increase" and delta <= 0.0:
            raise ValidationError(f"Parameter response '{test_name}' expected {metric} to increase")
        if direction == "decrease" and delta >= 0.0:
            raise ValidationError(f"Parameter response '{test_name}' expected {metric} to decrease")
        if direction not in (None, "increase", "decrease"):
            raise ValueError(f"Unsupported response direction for {metric}: {direction!r}")
    for metric, maximum in unchanged.items():
        baseline, variant = _metric_pair(test_name, metric, baseline_metrics, variant_metrics)
        delta = variant - baseline
        deltas[metric] = delta
        if float(maximum) < 0.0 or abs(delta) > float(maximum):
            raise ValidationError(
                f"Parameter response '{test_name}' changed invariant {metric}: "
                f"delta={delta:.6g}, allowed={float(maximum):.6g}"
            )
    return deltas


def _metric_pair(
    test_name: str,
    metric: str,
    baseline_metrics: Mapping[str, float],
    variant_metrics: Mapping[str, float],
) -> tuple[float, float]:
    if metric not in baseline_metrics or metric not in variant_metrics:
        raise KeyError(f"Parameter response '{test_name}' is missing metric: {metric}")
    baseline = float(baseline_metrics[metric])
    variant = float(variant_metrics[metric])
    if not math.isfinite(baseline) or not math.isfinite(variant):
        raise ValidationError(f"Parameter response '{test_name}' metric {metric} is non-finite")
    return baseline, variant
