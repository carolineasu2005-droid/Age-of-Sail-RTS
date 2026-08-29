"""Generate the formal ship's large structural objects.

The module deliberately depends on a very small hull-sampling contract instead
of importing implementation details from :mod:`ship.hull`.  A sampler passed to
``create_primary_structures`` should accept ``(config, x_m)`` (or just ``x_m``)
and return either a mapping or an object exposing these values:

``keel_z``
    Bottom of the hull at the requested longitudinal position.
``sheer_z`` (``deck_z`` is accepted as an alias)
    Main-deck / rail-base height.
``max_half_width``
    Maximum local half breadth.
``rail_half_width``
    Half breadth at the rail/deck edge.
``half_width_at(z_ratio)``
    Optional callable returning local half breadth between keel (0) and sheer
    (1).  A conservative U-section/tumblehome approximation is used when this
    callable is absent.

All generated vertices natively follow the production coordinate convention:
X longitudinal (-X bow, +X stern), Y transverse (-Y port, +Y starboard), Z up.
Objects use identity transforms and remain independent, editable meshes.
"""

from __future__ import annotations

from dataclasses import dataclass
import inspect
import math
from typing import Any, Callable, Mapping, Sequence

import bpy


Sampler = Callable[..., Any]
MaterialMap = Mapping[str, bpy.types.Material | str]
CollectionMap = Mapping[str, bpy.types.Collection]


@dataclass(frozen=True)
class StationProfile:
    """Normalized view of the small interface consumed by structure modules."""

    x: float
    keel_z: float
    sheer_z: float
    max_half_width: float
    rail_half_width: float
    width_sampler: Callable[[float], float] | None = None

    def half_width_at(self, z_ratio: float) -> float:
        ratio = max(0.0, min(1.0, float(z_ratio)))
        if self.width_sampler is not None:
            width = float(self.width_sampler(ratio))
            if math.isfinite(width) and width >= 0.0:
                return width

        # Broad lower body, full bilge and moderate upper-side tumblehome.
        if ratio <= 0.68:
            lower = math.sin((ratio / 0.68) * math.pi * 0.5) ** 0.72
            return self.max_half_width * lower
        blend = (ratio - 0.68) / 0.32
        return self.max_half_width + (self.rail_half_width - self.max_half_width) * blend ** 1.25


def config_value(config: Mapping[str, Any], *path: str, default: Any = None) -> Any:
    """Read a nested config value without coupling modules to one schema draft."""

    value: Any = config
    for key in path:
        if not isinstance(value, Mapping) or key not in value:
            return default
        value = value[key]
    return value


def ship_dimensions(config: Mapping[str, Any]) -> tuple[float, float, float]:
    dimensions = config_value(config, "dimensions", default={})
    length = float(dimensions.get("length_m", 36.4))
    beam = float(dimensions.get("beam_m", 8.6))
    depth = float(dimensions.get("hull_depth_m", dimensions.get("depth_m", 4.2)))
    if length <= 0.0 or beam <= 0.0 or depth <= 0.0:
        raise ValueError("Ship length, beam, and hull depth must be positive")
    return length, beam, depth


def ratio_to_x(config: Mapping[str, Any], ratio: float) -> float:
    """Convert a bow-to-stern ratio (0..1) to production-space X."""

    length, _, _ = ship_dimensions(config)
    return -0.5 * length + max(0.0, min(1.0, float(ratio))) * length


def x_to_ratio(config: Mapping[str, Any], x: float) -> float:
    length, _, _ = ship_dimensions(config)
    return max(0.0, min(1.0, (float(x) + 0.5 * length) / length))


def station_xs(config: Mapping[str, Any], start_ratio: float, end_ratio: float, count: int) -> list[float]:
    count = max(2, int(count))
    start = ratio_to_x(config, start_ratio)
    end = ratio_to_x(config, end_ratio)
    return [start + (end - start) * index / (count - 1) for index in range(count)]


def _mapping_or_attribute(raw: Any, names: Sequence[str], default: Any = None) -> Any:
    for name in names:
        if isinstance(raw, Mapping) and name in raw:
            return raw[name]
        if hasattr(raw, name):
            return getattr(raw, name)
    return default


def _call_sampler(sampler: Sampler, config: Mapping[str, Any], x: float) -> Any:
    """Call either the preferred ``(config, x)`` or compact ``(x)`` contract."""

    try:
        signature = inspect.signature(sampler)
        positional = [
            parameter
            for parameter in signature.parameters.values()
            if parameter.kind in (parameter.POSITIONAL_ONLY, parameter.POSITIONAL_OR_KEYWORD)
        ]
        if len(positional) >= 2:
            return sampler(config, x)
    except (TypeError, ValueError):
        # Some Blender/C callables do not expose an inspectable signature.
        pass
    return sampler(x)


def _resolve_default_sampler() -> Sampler | None:
    """Discover a public hull sampler only; never reach into hull internals."""

    try:
        from ship import hull as hull_module
    except ImportError:
        return None
    for name in ("sample_hull", "sample_station", "station_profile"):
        candidate = getattr(hull_module, name, None)
        if callable(candidate):
            return candidate
    return None


def _fallback_station(config: Mapping[str, Any], x: float) -> StationProfile:
    """Backward-compatible sample for development before the formal hull API exists."""

    length, beam, depth = ship_dimensions(config)
    ratio = x_to_ratio(config, x)
    hull = config_value(config, "hull", default={})
    legacy = config_value(config, "hull_shape", default={})
    max_beam_ratio = float(hull.get("max_beam_x_ratio", legacy.get("max_beam_position", 0.54)))
    max_beam_ratio = max(0.2, min(0.8, max_beam_ratio))
    bow_fineness = float(hull.get("bow_fineness", legacy.get("bow_fineness", 0.72)))
    stern_fineness = float(hull.get("stern_fineness", legacy.get("stern_fineness", 0.82)))

    if ratio <= max_beam_ratio:
        progress = ratio / max_beam_ratio
        width_factor = 0.12 + 0.88 * math.sin(progress * math.pi * 0.5) ** (1.2 + bow_fineness)
    else:
        progress = (ratio - max_beam_ratio) / (1.0 - max_beam_ratio)
        width_factor = 1.0 - 0.62 * progress ** (1.35 + 0.35 * stern_fineness)

    bow_rise = float(hull.get("bow_sheer_rise_m", legacy.get("bow_rise_m", 1.6)))
    stern_rise = float(hull.get("stern_sheer_rise_m", legacy.get("stern_rise_m", 2.2)))
    sheer_exponent = float(hull.get("sheer_curve_exponent", 1.75))
    bow_progress = max(0.0, (max_beam_ratio - ratio) / max_beam_ratio)
    stern_progress = max(0.0, (ratio - max_beam_ratio) / (1.0 - max_beam_ratio))
    sheer_z = depth + bow_rise * bow_progress ** sheer_exponent + stern_rise * stern_progress ** sheer_exponent
    keel_curve = float(hull.get("keel_curve_m", legacy.get("keel_curve", 0.15)))
    keel_z = keel_curve * math.sin(math.pi * ratio)
    maximum = 0.5 * beam * max(0.03, width_factor)
    tumblehome = float(hull.get("tumblehome_ratio", 0.10))
    rail = maximum * max(0.65, 1.0 - tumblehome)
    return StationProfile(x=x, keel_z=keel_z, sheer_z=sheer_z, max_half_width=maximum, rail_half_width=rail)


def sample_station(config: Mapping[str, Any], x: float, sampler: Sampler | None = None) -> StationProfile:
    """Return a normalized station sample for any of the formal structure modules."""

    resolved = sampler or _resolve_default_sampler()
    if resolved is None:
        return _fallback_station(config, x)

    raw = _call_sampler(resolved, config, float(x))
    if isinstance(raw, StationProfile):
        return raw

    fallback = _fallback_station(config, x)
    keel_z = float(_mapping_or_attribute(raw, ("keel_z", "bottom_z"), fallback.keel_z))
    sheer_z = float(_mapping_or_attribute(raw, ("sheer_z", "deck_z", "rail_z", "top_z"), fallback.sheer_z))
    maximum = float(
        _mapping_or_attribute(raw, ("max_half_width", "half_beam", "maximum_half_width"), fallback.max_half_width)
    )
    rail = float(
        _mapping_or_attribute(raw, ("rail_half_width", "deck_half_width", "gunwale_half_width"), fallback.rail_half_width)
    )
    width_sampler = _mapping_or_attribute(raw, ("half_width_at", "width_at_z_ratio"), None)
    if width_sampler is not None and not callable(width_sampler):
        width_sampler = None
    values = (keel_z, sheer_z, maximum, rail)
    if any(not math.isfinite(value) for value in values) or sheer_z <= keel_z or maximum < 0.0 or rail < 0.0:
        raise RuntimeError(f"Hull sampler returned an invalid station at x={x:.3f}: {values}")
    return StationProfile(float(x), keel_z, sheer_z, maximum, rail, width_sampler)


def resolve_collection(
    collections: bpy.types.Collection | CollectionMap | None,
    group: str,
) -> bpy.types.Collection | None:
    if isinstance(collections, Mapping):
        return collections.get(group) or collections.get("root")
    return collections


def resolve_material(materials: MaterialMap | None, key: str) -> bpy.types.Material | None:
    if not materials or key not in materials:
        return None
    material = materials[key]
    if isinstance(material, str):
        resolved = bpy.data.materials.get(material)
        if resolved is None:
            raise KeyError(f"Material is not available: {material}")
        return resolved
    return material


def create_mesh_object(
    name: str,
    vertices: Sequence[Sequence[float]],
    faces: Sequence[Sequence[int]],
    *,
    collection: bpy.types.Collection | None = None,
    material: bpy.types.Material | None = None,
) -> bpy.types.Object:
    if not vertices or not faces:
        raise ValueError(f"Cannot create empty structural mesh: {name}")
    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update(calc_edges=True)
    corrected = mesh.validate(verbose=False, clean_customdata=False)
    if corrected:
        raise RuntimeError(f"Blender had to correct invalid generated mesh data: {name}")
    object_ = bpy.data.objects.new(name, mesh)
    (collection or bpy.context.collection).objects.link(object_)
    object_.location = (0.0, 0.0, 0.0)
    object_.rotation_euler = (0.0, 0.0, 0.0)
    object_.scale = (1.0, 1.0, 1.0)
    if material is not None:
        mesh.materials.append(material)
    return object_


def _append_closed_rings(
    vertices: list[tuple[float, float, float]],
    faces: list[list[int]],
    rings: Sequence[Sequence[tuple[float, float, float]]],
) -> None:
    if len(rings) < 2 or len(rings[0]) < 3:
        raise ValueError("A closed ring strip needs at least two rings of three vertices")
    ring_size = len(rings[0])
    if any(len(ring) != ring_size for ring in rings):
        raise ValueError("All structural rings must have the same vertex count")
    offset = len(vertices)
    for ring in rings:
        vertices.extend(ring)
    for ring_index in range(len(rings) - 1):
        current = offset + ring_index * ring_size
        following = current + ring_size
        for index in range(ring_size):
            next_index = (index + 1) % ring_size
            faces.append([current + index, following + index, following + next_index, current + next_index])
    faces.append([offset + index for index in reversed(range(ring_size))])
    last = offset + (len(rings) - 1) * ring_size
    faces.append([last + index for index in range(ring_size)])


def _append_extruded_polygon_y(
    vertices: list[tuple[float, float, float]],
    faces: list[list[int]],
    polygon_xz: Sequence[tuple[float, float]],
    half_width_y: float,
) -> None:
    if len(polygon_xz) < 3:
        raise ValueError("An extruded structural polygon needs at least three points")
    offset = len(vertices)
    vertices.extend((x, -half_width_y, z) for x, z in polygon_xz)
    vertices.extend((x, half_width_y, z) for x, z in polygon_xz)
    count = len(polygon_xz)
    faces.append([offset + index for index in reversed(range(count))])
    faces.append([offset + count + index for index in range(count)])
    for index in range(count):
        following = (index + 1) % count
        faces.append([offset + index, offset + following, offset + count + following, offset + count + index])


def _ribbon_polygon(points: Sequence[tuple[float, float]], thickness_x: float) -> list[tuple[float, float]]:
    half = 0.5 * thickness_x
    return [(x - half, z) for x, z in points] + [(x + half, z) for x, z in reversed(points)]


def _profile_side_band(
    config: Mapping[str, Any],
    xs: Sequence[float],
    sampler: Sampler | None,
    side: int,
    bottom_ratio: float,
    top_ratio: float,
    outward_offset: float,
    thickness_y: float,
) -> tuple[list[tuple[float, float, float]], list[list[int]]]:
    vertices: list[tuple[float, float, float]] = []
    faces: list[list[int]] = []
    rings: list[list[tuple[float, float, float]]] = []
    for x in xs:
        station = sample_station(config, x, sampler)
        depth = station.sheer_z - station.keel_z
        z0 = station.keel_z + depth * bottom_ratio
        z1 = station.keel_z + depth * top_ratio
        width0 = station.half_width_at(bottom_ratio) + outward_offset
        width1 = station.half_width_at(top_ratio) + outward_offset
        outer0 = side * (width0 + 0.5 * thickness_y)
        inner0 = side * (width0 - 0.5 * thickness_y)
        outer1 = side * (width1 + 0.5 * thickness_y)
        inner1 = side * (width1 - 0.5 * thickness_y)
        rings.append([(x, inner0, z0), (x, outer0, z0), (x, outer1, z1), (x, inner1, z1)])
    _append_closed_rings(vertices, faces, rings)
    return vertices, faces


def _rake_distance(config_section: Mapping[str, Any], vertical_span: float, default_m: float) -> float:
    if "rake_m" in config_section:
        return float(config_section["rake_m"])
    if "rake_deg" in config_section:
        return math.tan(math.radians(float(config_section["rake_deg"]))) * vertical_span
    return float(config_section.get("rake", default_m))


def create_primary_structures(
    config: Mapping[str, Any],
    *,
    sampler: Sampler | None = None,
    collections: bpy.types.Collection | CollectionMap | None = None,
    materials: MaterialMap | None = None,
) -> dict[str, bpy.types.Object]:
    """Create keel/end structures, bulwarks and main wales as modular meshes."""

    length, beam, depth = ship_dimensions(config)
    generation = config_value(config, "generation", default={})
    generation_count = int(generation.get("longitudinal_sections", generation.get("station_count", 41)))
    generation_count = max(17, generation_count)
    objects: dict[str, bpy.types.Object] = {}
    structure_collection = resolve_collection(collections, "Structure")
    detail_collection = resolve_collection(collections, "HullDetails")
    structural_material = resolve_material(materials, "structural")
    hull_material = resolve_material(materials, "hull")

    # Keel: a modest, closed longitudinal member directly beneath the hull.
    keel_config = config_value(config, "keel", default={})
    keel_start = float(keel_config.get("start_x_ratio", keel_config.get("bow_inset_ratio", 0.025)))
    keel_end = float(keel_config.get("end_x_ratio", 1.0 - float(keel_config.get("stern_inset_ratio", 0.025))))
    keel_width = float(keel_config.get("width_m", 0.28))
    keel_depth = float(keel_config.get("depth_m", 0.38))
    keel_rings: list[list[tuple[float, float, float]]] = []
    for x in station_xs(config, keel_start, keel_end, generation_count):
        station = sample_station(config, x, sampler)
        top = station.keel_z + float(keel_config.get("top_offset_m", 0.04))
        bottom = top - keel_depth
        keel_rings.append([
            (x, -0.5 * keel_width, bottom),
            (x, 0.5 * keel_width, bottom),
            (x, 0.5 * keel_width, top),
            (x, -0.5 * keel_width, top),
        ])
    vertices: list[tuple[float, float, float]] = []
    faces: list[list[int]] = []
    _append_closed_rings(vertices, faces, keel_rings)
    objects["Keel"] = create_mesh_object(
        "Keel", vertices, faces, collection=structure_collection, material=structural_material
    )

    bow_x = -0.5 * length
    stern_x = 0.5 * length
    bow_sample = sample_station(config, bow_x + 0.015 * length, sampler)
    stern_sample = sample_station(config, stern_x - 0.015 * length, sampler)

    # Stem: curved, forward-raked (-X) ribbon, independent from Hull_Main.
    stem_config = config_value(config, "stem", default={})
    stem_bottom = bow_sample.keel_z - 0.08
    stem_top = bow_sample.sheer_z + float(stem_config.get("height_above_sheer_m", 0.55))
    stem_span = max(stem_top - stem_bottom, 0.5)
    stem_rake = _rake_distance(stem_config, stem_span, 0.82)
    stem_curve = float(stem_config.get("curvature", stem_config.get("curvature_ratio", 0.18)))
    stem_points: list[tuple[float, float]] = []
    for index in range(7):
        t = index / 6.0
        forward = stem_rake * (t ** (1.12 + 0.35 * stem_curve))
        stem_points.append((bow_x - forward, stem_bottom + stem_span * t))
    vertices, faces = [], []
    _append_extruded_polygon_y(
        vertices,
        faces,
        _ribbon_polygon(stem_points, float(stem_config.get("thickness_x_m", 0.26))),
        0.5 * float(stem_config.get("width_m", 0.32)),
    )
    objects["Stem"] = create_mesh_object(
        "Stem", vertices, faces, collection=structure_collection, material=structural_material
    )

    # Sternpost: restrained +X rake, kept separate for later rudder detailing.
    stern_config = config_value(config, "stern", default={})
    post_bottom = stern_sample.keel_z - 0.05
    post_top = stern_sample.sheer_z - float(stern_config.get("sternpost_top_inset_m", 0.35))
    post_span = max(post_top - post_bottom, 0.5)
    post_rake_config = dict(stern_config)
    if "sternpost_rake_deg" in stern_config:
        post_rake_config["rake_deg"] = stern_config["sternpost_rake_deg"]
    post_rake = _rake_distance(post_rake_config, post_span, 0.38)
    post_points = [
        (stern_x + post_rake * (index / 5.0) ** 1.08, post_bottom + post_span * index / 5.0)
        for index in range(6)
    ]
    vertices, faces = [], []
    _append_extruded_polygon_y(
        vertices,
        faces,
        _ribbon_polygon(post_points, float(stern_config.get("sternpost_thickness_x_m", 0.28))),
        0.5 * float(stern_config.get("sternpost_width_m", 0.34)),
    )
    objects["Sternpost"] = create_mesh_object(
        "Sternpost", vertices, faces, collection=structure_collection, material=structural_material
    )

    # Rudder: readable trapezoidal plate aft (+X) of the sternpost, no hardware.
    rudder_config = config_value(config, "rudder", default={})
    rudder_height = float(rudder_config.get("height_m", min(3.2, depth * 0.76)))
    rudder_length = float(rudder_config.get("length_m", 1.25))
    rudder_bottom = post_bottom + float(rudder_config.get("bottom_clearance_m", 0.18))
    rudder_top = min(post_top - 0.18, rudder_bottom + rudder_height)
    rudder_front_bottom = stern_x + 0.10
    rudder_front_top = stern_x + post_rake * max(0.0, min(1.0, (rudder_top - post_bottom) / post_span)) + 0.10
    rudder_polygon = [
        (rudder_front_bottom, rudder_bottom),
        (rudder_front_bottom + rudder_length * 0.72, rudder_bottom + 0.10),
        (rudder_front_top + rudder_length, rudder_top - 0.12),
        (rudder_front_top, rudder_top),
    ]
    vertices, faces = [], []
    _append_extruded_polygon_y(
        vertices, faces, rudder_polygon, 0.5 * float(rudder_config.get("thickness_m", 0.24))
    )
    objects["Rudder"] = create_mesh_object(
        "Rudder", vertices, faces, collection=structure_collection, material=structural_material
    )

    # Medium mirror/transom stern, a restrained tapered closed prism.
    transom_width_ratio = float(stern_config.get("transom_width_ratio", 0.58))
    transom_height = float(stern_config.get("transom_height_m", 2.15))
    transom_thickness = float(stern_config.get("transom_thickness_m", 0.30))
    transom_center_x = stern_x - float(stern_config.get("transom_inset_m", 0.25))
    transom_base = stern_sample.sheer_z - float(stern_config.get("transom_base_below_sheer_m", 1.15))
    transom_top = transom_base + transom_height
    lower_half_width = 0.5 * beam * transom_width_ratio * 0.78
    upper_half_width = 0.5 * beam * transom_width_ratio
    rings = [
        [
            (x, -lower_half_width, transom_base),
            (x, lower_half_width, transom_base),
            (x, upper_half_width, transom_top),
            (x, -upper_half_width, transom_top),
        ]
        for x in (transom_center_x - 0.5 * transom_thickness, transom_center_x + 0.5 * transom_thickness)
    ]
    vertices, faces = [], []
    _append_closed_rings(vertices, faces, rings)
    objects["Transom_Main"] = create_mesh_object(
        "Transom_Main", vertices, faces, collection=structure_collection, material=structural_material
    )

    # Compact aft upperworks; generated as one closed volume following sheer.
    upper_start = float(stern_config.get("upperworks_start_x_ratio", 0.79))
    upper_end = float(stern_config.get("upperworks_end_x_ratio", 0.965))
    upper_height = float(stern_config.get("upperworks_height_m", 1.15))
    upper_width_ratio = float(stern_config.get("upperworks_width_ratio", 0.86))
    upper_rings: list[list[tuple[float, float, float]]] = []
    for x in station_xs(config, upper_start, upper_end, max(5, generation_count // 5)):
        station = sample_station(config, x, sampler)
        base = station.sheer_z + float(stern_config.get("upperworks_base_offset_m", 0.12))
        half_width = station.rail_half_width * upper_width_ratio
        upper_rings.append([
            (x, -half_width, base),
            (x, half_width, base),
            (x, half_width * 0.93, base + upper_height),
            (x, -half_width * 0.93, base + upper_height),
        ])
    vertices, faces = [], []
    _append_closed_rings(vertices, faces, upper_rings)
    objects["Stern_Upperworks"] = create_mesh_object(
        "Stern_Upperworks", vertices, faces, collection=structure_collection, material=hull_material
    )

    # Bulwarks are one clean longitudinal object per side, not fragmented posts.
    bulwark_config = config_value(config, "bulwarks", default={})
    bulwark_start = float(bulwark_config.get("start_x_ratio", 0.09))
    bulwark_end = float(bulwark_config.get("end_x_ratio", 0.94))
    bulwark_height = float(bulwark_config.get("height_m", 0.82))
    bulwark_thickness = float(bulwark_config.get("thickness_m", 0.18))
    bulwark_xs = station_xs(config, bulwark_start, bulwark_end, generation_count)
    for side, name in ((-1, "Bulwark_Port"), (1, "Bulwark_Starboard")):
        rings = []
        for x in bulwark_xs:
            station = sample_station(config, x, sampler)
            base = station.sheer_z + float(bulwark_config.get("base_offset_m", 0.02))
            width = station.rail_half_width
            outer = side * (width + 0.5 * bulwark_thickness)
            inner = side * (width - 0.5 * bulwark_thickness)
            rings.append([
                (x, inner, base),
                (x, outer, base),
                (x, outer, base + bulwark_height),
                (x, inner, base + bulwark_height),
            ])
        vertices, faces = [], []
        _append_closed_rings(vertices, faces, rings)
        objects[name] = create_mesh_object(
            name, vertices, faces, collection=detail_collection, material=hull_material
        )

    # Main wales are aggregated by side, preserving a compact object hierarchy.
    wales_config = config_value(config, "wales", default={})
    wale_start = float(wales_config.get("start_x_ratio", 0.075))
    wale_end = float(wales_config.get("end_x_ratio", 0.94))
    wale_levels = [float(value) for value in wales_config.get(
        "vertical_z_ratios",
        [wales_config.get("height_ratio", 0.62)],
    )]
    wale_height_ratio = float(wales_config.get("height_m", 0.34)) / depth
    wale_projection = float(wales_config.get(
        "outboard_projection_m",
        wales_config.get("outward_offset_m", 0.13),
    ))
    wale_thickness = float(wales_config.get("thickness_m", max(0.10, wale_projection * 0.72)))
    wale_xs = station_xs(config, wale_start, wale_end, generation_count)
    for side, name in ((-1, "Wales_Main_Port"), (1, "Wales_Main_Starboard")):
        vertices: list[tuple[float, float, float]] = []
        faces: list[list[int]] = []
        for wale_center_ratio in wale_levels:
            band_vertices, band_faces = _profile_side_band(
                config,
                wale_xs,
                sampler,
                side,
                max(0.05, wale_center_ratio - 0.5 * wale_height_ratio),
                min(0.95, wale_center_ratio + 0.5 * wale_height_ratio),
                wale_projection,
                wale_thickness,
            )
            offset = len(vertices)
            vertices.extend(band_vertices)
            faces.extend([[index + offset for index in face] for face in band_faces])
        objects[name] = create_mesh_object(
            name, vertices, faces, collection=detail_collection, material=structural_material
        )
        objects[name]["wale_band_count"] = len(wale_levels)

    for object_ in objects.values():
        object_["production_structure"] = True
    return objects


__all__ = [
    "StationProfile",
    "config_value",
    "create_mesh_object",
    "create_primary_structures",
    "ratio_to_x",
    "resolve_collection",
    "resolve_material",
    "sample_station",
    "ship_dimensions",
    "station_xs",
    "x_to_ratio",
]
