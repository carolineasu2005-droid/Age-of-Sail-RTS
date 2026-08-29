"""Procedural deck architecture for the formal Gelderland base model.

Decks are closed, low-profile slabs whose vertices follow the production hull
sampler.  They are intentionally large editable structures rather than flat
primitive cubes or collections of individual planks.
"""

from __future__ import annotations

from typing import Any, Mapping

import bpy

from ship.structures import (
    CollectionMap,
    MaterialMap,
    Sampler,
    config_value,
    create_mesh_object,
    resolve_collection,
    resolve_material,
    sample_station,
    station_xs,
)


def _number(section: Mapping[str, Any], names: tuple[str, ...], default: float) -> float:
    for name in names:
        if name in section:
            return float(section[name])
    return float(default)


def _build_deck_slab(
    name: str,
    config: Mapping[str, Any],
    section: Mapping[str, Any],
    *,
    sampler: Sampler | None,
    default_start: float,
    default_end: float,
    default_elevation: float,
    default_width_ratio: float,
    collection: bpy.types.Collection | None,
    material: bpy.types.Material | None,
) -> bpy.types.Object:
    start = _number(section, ("start_x_ratio", "start_ratio"), default_start)
    end = _number(section, ("end_x_ratio", "end_ratio"), default_end)
    if not 0.0 <= start < end <= 1.0:
        raise ValueError(f"{name} longitudinal ratios must satisfy 0 <= start < end <= 1")

    generation = config_value(config, "generation", default={})
    base_sections = int(generation.get("longitudinal_sections", generation.get("station_count", 41)))
    span_fraction = end - start
    station_count = max(5, round(max(17, base_sections) * span_fraction) + 1)
    elevation = _number(section, ("elevation_m", "deck_elevation_m", "vertical_offset_m"), default_elevation)
    thickness = _number(section, ("thickness_m",), 0.16)
    inset = _number(section, ("edge_inset_m", "inset_m"), 0.12)
    width_ratio = _number(section, ("width_ratio",), default_width_ratio)
    dimensions = config_value(config, "dimensions", default={})
    hull_reference_z = float(dimensions.get("hull_depth_m", 4.2))
    baseline_value = section.get("baseline_z_m")
    baseline_z = float(baseline_value) if baseline_value is not None else None
    follow_sheer = bool(section.get("follow_sheer", True))
    if thickness <= 0.0 or not 0.25 <= width_ratio <= 1.1:
        raise ValueError(f"{name} thickness/width ratio is invalid")

    vertices: list[tuple[float, float, float]] = []
    rings: list[list[tuple[float, float, float]]] = []
    for x in station_xs(config, start, end, station_count):
        station = sample_station(config, x, sampler)
        if baseline_z is None:
            top = station.sheer_z + elevation
        else:
            sheer_offset = station.sheer_z - hull_reference_z if follow_sheer else 0.0
            top = baseline_z + sheer_offset + elevation
        bottom = top - thickness
        half_width = max(0.08, station.rail_half_width * width_ratio - inset)
        rings.append([
            (x, -half_width, bottom),
            (x, half_width, bottom),
            (x, half_width, top),
            (x, -half_width, top),
        ])
        vertices.extend(rings[-1])

    faces: list[list[int]] = []
    ring_size = 4
    for ring_index in range(len(rings) - 1):
        current = ring_index * ring_size
        following = current + ring_size
        # Bottom, starboard edge, top, port edge.
        for edge in range(ring_size):
            next_edge = (edge + 1) % ring_size
            faces.append([current + edge, following + edge, following + next_edge, current + next_edge])
    faces.append([3, 2, 1, 0])
    last = (len(rings) - 1) * ring_size
    faces.append([last, last + 1, last + 2, last + 3])

    object_ = create_mesh_object(name, vertices, faces, collection=collection, material=material)
    object_["deck_start_x_ratio"] = start
    object_["deck_end_x_ratio"] = end
    object_["deck_elevation_m"] = elevation
    if baseline_z is not None:
        object_["deck_baseline_z_m"] = baseline_z
    object_["deck_follows_sheer"] = follow_sheer
    object_["production_structure"] = True
    return object_


def create_decks(
    config: Mapping[str, Any],
    *,
    sampler: Sampler | None = None,
    collections: bpy.types.Collection | CollectionMap | None = None,
    materials: MaterialMap | None = None,
) -> dict[str, bpy.types.Object]:
    """Create the continuous main deck and restrained fore/aft raised decks."""

    decks = config_value(config, "decks", default={})
    main = decks.get("main", {})
    forecastle = decks.get("forecastle", {})
    quarter = decks.get("quarterdeck", decks.get("quarter", {}))
    if not all(isinstance(section, Mapping) for section in (main, forecastle, quarter)):
        raise ValueError("decks.main, decks.forecastle and decks.quarterdeck must be objects")

    collection = resolve_collection(collections, "Decks")
    material = resolve_material(materials, "deck")
    objects = {
        "Deck_Main": _build_deck_slab(
            "Deck_Main",
            config,
            main,
            sampler=sampler,
            default_start=0.035,
            default_end=0.955,
            default_elevation=0.0,
            default_width_ratio=0.98,
            collection=collection,
            material=material,
        ),
        "Deck_Forecastle": _build_deck_slab(
            "Deck_Forecastle",
            config,
            forecastle,
            sampler=sampler,
            default_start=0.055,
            default_end=0.255,
            default_elevation=0.62,
            default_width_ratio=0.92,
            collection=collection,
            material=material,
        ),
        "Deck_Quarter": _build_deck_slab(
            "Deck_Quarter",
            config,
            quarter,
            sampler=sampler,
            default_start=0.69,
            default_end=0.94,
            default_elevation=0.78,
            default_width_ratio=0.92,
            collection=collection,
            material=material,
        ),
    }
    return objects


__all__ = ["create_decks"]
