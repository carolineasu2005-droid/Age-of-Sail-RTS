"""Generate the single continuous main-gunport tier without any cannons.

Gunports are data-driven and grouped into one final mesh per side.  When a
closed ``Hull_Main`` is supplied, all mirrored openings are cut in one Exact
Boolean operation using one temporary aggregate cutter.  The cutter is removed
immediately; a five-sided, open-front pocket lining remains for each recess,
combined into ``Gunports_Main_Port`` / ``Gunports_Main_Starboard``.  This avoids
28 unmanaged objects, decals, floating black squares, and dummy barrels.
"""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Any, Mapping, Sequence

import bpy

from ship.structures import (
    CollectionMap,
    MaterialMap,
    Sampler,
    config_value,
    create_mesh_object,
    ratio_to_x,
    resolve_collection,
    resolve_material,
    sample_station,
)


@dataclass(frozen=True)
class GunportPosition:
    """Center and local hull surface of one mirrored gunport pair."""

    index: int
    x: float
    z: float
    hull_half_width: float


def _value(section: Mapping[str, Any], names: Sequence[str], default: Any) -> Any:
    for name in names:
        if name in section:
            return section[name]
    return default


def _gunport_config(config: Mapping[str, Any]) -> Mapping[str, Any]:
    section = config_value(config, "gunports", default={})
    if not isinstance(section, Mapping):
        raise ValueError("Configuration field 'gunports' must be an object")
    return section


def build_gunport_layout(
    config: Mapping[str, Any],
    *,
    sampler: Sampler | None = None,
) -> list[GunportPosition]:
    """Calculate one side's equal-spacing layout in the usable hull region."""

    section = _gunport_config(config)
    count = int(_value(section, ("count_per_side", "count"), 14))
    forward = float(_value(section, ("forward_limit_ratio", "forward_limit"), 0.16))
    aft = float(_value(section, ("aft_limit_ratio", "aft_limit"), 0.82))
    width = float(_value(section, ("width_m", "port_width_m", "port_width"), 0.82))
    height = float(_value(section, ("height_m", "port_height_m", "port_height"), 0.70))
    follow_sheer = bool(section.get("follow_sheer", True))
    vertical_offset = float(_value(section, ("vertical_offset_m", "vertical_offset"), 1.20))
    z_ratio = float(_value(section, ("z_ratio", "height_ratio"), 0.69))

    if count < 1:
        raise ValueError("gunports.count_per_side must be at least 1")
    if not 0.0 < forward < aft < 1.0:
        raise ValueError("Gunport limits must satisfy 0 < forward < aft < 1")
    if width <= 0.0 or height <= 0.0:
        raise ValueError("Gunport width and height must be positive")

    first_x = ratio_to_x(config, forward)
    last_x = ratio_to_x(config, aft)
    spacing = (last_x - first_x) / (count - 1) if count > 1 else 0.0
    if count > 1 and spacing <= width * 1.08:
        raise ValueError(
            f"Gunport spacing {spacing:.3f}m is too small for {width:.3f}m openings"
        )

    layout: list[GunportPosition] = []
    for index in range(count):
        x = first_x + spacing * index if count > 1 else 0.5 * (first_x + last_x)
        station = sample_station(config, x, sampler)
        depth = station.sheer_z - station.keel_z
        z = station.sheer_z - vertical_offset if follow_sheer else station.keel_z + depth * z_ratio
        local_ratio = (z - station.keel_z) / depth
        if not 0.08 < local_ratio < 0.94:
            raise ValueError(
                f"Gunport {index + 1} lies outside the usable hull side at x={x:.3f}, z ratio={local_ratio:.3f}"
            )
        half_width = station.half_width_at(local_ratio)
        if half_width <= 0.05:
            raise ValueError(f"Gunport {index + 1} has no usable hull breadth at x={x:.3f}")
        layout.append(GunportPosition(index=index, x=x, z=z, hull_half_width=half_width))
    return layout


def _append_box(
    vertices: list[tuple[float, float, float]],
    faces: list[list[int]],
    minimum: tuple[float, float, float],
    maximum: tuple[float, float, float],
) -> None:
    x0, y0, z0 = minimum
    x1, y1, z1 = maximum
    offset = len(vertices)
    vertices.extend([
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1),
    ])
    faces.extend([
        [offset + 0, offset + 3, offset + 2, offset + 1],
        [offset + 4, offset + 5, offset + 6, offset + 7],
        [offset + 0, offset + 1, offset + 5, offset + 4],
        [offset + 1, offset + 2, offset + 6, offset + 5],
        [offset + 2, offset + 3, offset + 7, offset + 6],
        [offset + 3, offset + 0, offset + 4, offset + 7],
    ])


def _append_open_pocket(
    vertices: list[tuple[float, float, float]],
    faces: list[list[int]],
    *,
    side: int,
    position: GunportPosition,
    width: float,
    height: float,
    depth: float,
    lip_inset: float,
) -> None:
    """Append a back and four walls; the outward-facing rectangle stays open."""

    x0 = position.x - 0.5 * width
    x1 = position.x + 0.5 * width
    z0 = position.z - 0.5 * height
    z1 = position.z + 0.5 * height
    front_y = side * (position.hull_half_width - lip_inset)
    back_y = front_y - side * depth
    offset = len(vertices)
    # Front ring first, back ring second; there is intentionally no front face.
    vertices.extend([
        (x0, front_y, z0), (x1, front_y, z0), (x1, front_y, z1), (x0, front_y, z1),
        (x0, back_y, z0), (x1, back_y, z0), (x1, back_y, z1), (x0, back_y, z1),
    ])
    faces.extend([
        [offset + 4, offset + 7, offset + 6, offset + 5],
        [offset + 0, offset + 1, offset + 5, offset + 4],
        [offset + 1, offset + 2, offset + 6, offset + 5],
        [offset + 2, offset + 3, offset + 7, offset + 6],
        [offset + 3, offset + 0, offset + 4, offset + 7],
    ])


def _create_aggregate_cutter(
    name: str,
    layout: Sequence[GunportPosition],
    *,
    sides: Sequence[int],
    width: float,
    height: float,
    depth: float,
    outside_extension: float,
    collection: bpy.types.Collection | None,
) -> bpy.types.Object:
    vertices: list[tuple[float, float, float]] = []
    faces: list[list[int]] = []
    for side in sides:
        if side not in {-1, 1}:
            raise ValueError(f"Cutter side must be -1 or 1, got {side}")
        for position in layout:
            outer_y = side * (position.hull_half_width + outside_extension)
            inner_y = side * (position.hull_half_width - depth)
            _append_box(
                vertices,
                faces,
                (
                    position.x - 0.5 * width,
                    min(outer_y, inner_y),
                    position.z - 0.5 * height,
                ),
                (
                    position.x + 0.5 * width,
                    max(outer_y, inner_y),
                    position.z + 0.5 * height,
                ),
            )
    cutter = create_mesh_object(name, vertices, faces, collection=collection)
    cutter.display_type = "WIRE"
    cutter.hide_render = True
    return cutter


def _apply_boolean_recess(hull: bpy.types.Object, cutter: bpy.types.Object, side_name: str) -> None:
    if hull.type != "MESH":
        raise TypeError("Gunport recesses require a mesh Hull_Main object")
    bpy.ops.object.select_all(action="DESELECT")
    hull.hide_set(False)
    hull.select_set(True)
    bpy.context.view_layer.objects.active = hull
    modifier = hull.modifiers.new(name=f"Gunport_Recesses_{side_name}", type="BOOLEAN")
    modifier.operation = "DIFFERENCE"
    modifier.solver = "EXACT"
    modifier.object = cutter
    result = bpy.ops.object.modifier_apply(modifier=modifier.name)
    if "FINISHED" not in result:
        raise RuntimeError(f"Failed to apply aggregate {side_name} gunport recess boolean: {result}")


def _enforce_y_mirror(hull: bpy.types.Object) -> None:
    """Resolve Boolean tessellation to a genuinely mirrored production mesh."""
    bpy.ops.object.select_all(action="DESELECT")
    hull.select_set(True)
    bpy.context.view_layer.objects.active = hull
    modifier = hull.modifiers.new(name="Gunport_PostBoolean_Y_Symmetry", type="MIRROR")
    modifier.use_axis[0] = False
    modifier.use_axis[1] = True
    modifier.use_axis[2] = False
    modifier.use_bisect_axis[1] = True
    modifier.use_clip = True
    modifier.use_mirror_merge = True
    modifier.merge_threshold = 1.0e-6
    result = bpy.ops.object.modifier_apply(modifier=modifier.name)
    if "FINISHED" not in result:
        raise RuntimeError(f"Failed to apply post-Boolean Y symmetry: {result}")


def _remove_object_and_mesh(object_: bpy.types.Object) -> None:
    mesh = object_.data
    bpy.data.objects.remove(object_, do_unlink=True)
    if mesh.users == 0:
        bpy.data.meshes.remove(mesh)


def validate_gunport_objects(
    objects: Mapping[str, bpy.types.Object],
    config: Mapping[str, Any],
    *,
    require_locked_count: bool = False,
) -> None:
    """Validate counts and spacing without mistaking legal Gunports for guns."""

    section = _gunport_config(config)
    expected = int(_value(section, ("count_per_side", "count"), 14))
    for name in ("Gunports_Main_Port", "Gunports_Main_Starboard"):
        object_ = objects.get(name)
        if object_ is None:
            raise RuntimeError(f"Required gunport object is missing: {name}")
        if int(object_.get("gunport_count", -1)) != expected:
            raise RuntimeError(f"{name} does not contain {expected} gunports")
        positions = [float(value) for value in object_.get("gunport_x_positions", [])]
        if len(positions) != expected:
            raise RuntimeError(f"{name} has incomplete gunport position metadata")
        spacings = [b - a for a, b in zip(positions, positions[1:])]
        if spacings and max(spacings) - min(spacings) > 1.0e-5:
            raise RuntimeError(f"{name} gunports are not equally spaced")
    if require_locked_count and expected != 14:
        raise RuntimeError(f"Formal Gelderland build requires exactly 14 gunports per side, got {expected}")

    port_x = list(objects["Gunports_Main_Port"]["gunport_x_positions"])
    starboard_x = list(objects["Gunports_Main_Starboard"]["gunport_x_positions"])
    if len(port_x) != len(starboard_x) or any(abs(a - b) > 1.0e-6 for a, b in zip(port_x, starboard_x)):
        raise RuntimeError("Port and starboard gunport longitudinal positions do not correspond")


def create_main_gunports(
    config: Mapping[str, Any],
    *,
    hull_object: bpy.types.Object | None,
    sampler: Sampler | None = None,
    collections: bpy.types.Collection | CollectionMap | None = None,
    materials: MaterialMap | None = None,
    apply_recess_boolean: bool = True,
) -> dict[str, bpy.types.Object]:
    """Create mirrored main-tier openings and two aggregated pocket objects.

    ``apply_recess_boolean`` is intentionally explicit.  Formal builds should
    leave it enabled and pass ``Hull_Main``; callers may disable it only for
    isolated layout/parameter-response tests.
    """

    if apply_recess_boolean and hull_object is None:
        raise ValueError("A Hull_Main object is required when applying gunport recess booleans")

    section = _gunport_config(config)
    layout = build_gunport_layout(config, sampler=sampler)
    width = float(_value(section, ("width_m", "port_width_m", "port_width"), 0.82))
    height = float(_value(section, ("height_m", "port_height_m", "port_height"), 0.70))
    recess_depth = float(_value(section, ("recess_depth_m", "depth_m"), 0.34))
    outside_extension = float(section.get("boolean_outside_extension_m", 0.18))
    lip_inset = float(section.get("lining_lip_inset_m", 0.035))
    if recess_depth <= 0.05 or outside_extension <= 0.01:
        raise ValueError("Gunport recess depth / outside extension is too small")

    collection = resolve_collection(collections, "Gunports")
    interior_material = resolve_material(materials, "gunport_interior")
    objects: dict[str, bpy.types.Object] = {}

    # One symmetric Boolean avoids the subtly different triangulation that can
    # result when port and starboard are solved in two sequential operations.
    if apply_recess_boolean:
        cutter = _create_aggregate_cutter(
            "TEMP_Gunport_Cutter_Mirrored",
            layout,
            sides=(-1, 1),
            width=width,
            height=height,
            depth=recess_depth,
            outside_extension=outside_extension,
            collection=collection,
        )
        try:
            _apply_boolean_recess(hull_object, cutter, "Mirrored")
            _enforce_y_mirror(hull_object)
        finally:
            _remove_object_and_mesh(cutter)

    for side, side_name, object_name in (
        (-1, "Port", "Gunports_Main_Port"),
        (1, "Starboard", "Gunports_Main_Starboard"),
    ):
        vertices: list[tuple[float, float, float]] = []
        faces: list[list[int]] = []
        for position in layout:
            _append_open_pocket(
                vertices,
                faces,
                side=side,
                position=position,
                width=width * 0.96,
                height=height * 0.96,
                depth=recess_depth * 0.92,
                lip_inset=lip_inset,
            )
        object_ = create_mesh_object(
            object_name,
            vertices,
            faces,
            collection=collection,
            material=interior_material,
        )
        object_["gunport_side"] = side_name
        object_["gunport_count"] = len(layout)
        object_["gunport_x_positions"] = [position.x for position in layout]
        object_["gunport_z_positions"] = [position.z for position in layout]
        object_["gunport_width_m"] = width
        object_["gunport_height_m"] = height
        object_["recess_depth_m"] = recess_depth
        object_["recess_boolean_applied"] = apply_recess_boolean
        object_["contains_cannons"] = False
        object_["production_structure"] = True
        objects[object_name] = object_

    validate_gunport_objects(objects, config)
    return objects


__all__ = [
    "GunportPosition",
    "build_gunport_layout",
    "create_main_gunports",
    "validate_gunport_objects",
]
