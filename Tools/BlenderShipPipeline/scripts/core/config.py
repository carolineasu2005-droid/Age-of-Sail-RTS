"""Versioned ship configuration loading, provenance, and validation."""

from __future__ import annotations

import copy
import hashlib
import json
import math
import re
from collections.abc import Iterable, Mapping
from pathlib import Path
from typing import Any

from core.paths import ships_config_dir


FORMAL_SCHEMA_VERSION = 2
PROVENANCE_CATEGORIES = (
    "user_locked_historical_baseline",
    "modeling_heuristic",
    "stylization",
)
PROVENANCE_PARAMETER_ROOTS = (
    "identity",
    "coordinate_system",
    "dimensions",
    "hull",
    "keel",
    "stem",
    "stern",
    "rudder",
    "decks",
    "bulwarks",
    "wales",
    "gunports",
    "generation",
    "stylization",
    "materials",
)

_SAFE_ID = re.compile(r"^[a-z0-9][a-z0-9_]*$")
_REQUIRED_RENDER_VIEWS = {
    "port",
    "starboard",
    "top",
    "bow",
    "stern",
    "perspective",
    "rts_perspective",
}
_GELDERLAND_LOCKED_VALUES = {
    "stage": "formal_base_model",
    "identity.period_label": "c.1634",
    "identity.ship_type": "medium_warship",
    "coordinate_system.longitudinal_axis": "X",
    "coordinate_system.bow_direction": "-X",
    "coordinate_system.stern_direction": "+X",
    "coordinate_system.transverse_axis": "Y",
    "coordinate_system.port_direction": "-Y",
    "coordinate_system.starboard_direction": "+Y",
    "coordinate_system.up_direction": "+Z",
    "coordinate_system.meters_per_blender_unit": 1.0,
    "dimensions.length_m": 36.4,
    "dimensions.beam_m": 8.6,
    "hull.tumblehome_style": "moderate",
    "stern.form": "mirror_transom",
    "gunports.deck_count": 1,
    "gunports.count_per_side": 14,
}


def ship_config_path(filename: str = "gelderland_1634.json") -> Path:
    """Return a validated ship-config path without depending on process CWD."""
    if Path(filename).name != filename:
        raise ValueError("Ship configuration must be a filename from config/ships")
    return ships_config_dir() / filename


def load_ship_config(
    filename: str = "gelderland_1634.json",
    *,
    enforce_locked: bool = True,
) -> dict[str, Any]:
    """Load and validate a ship config.

    ``enforce_locked=False`` is intentionally available only for isolated
    parameter-response variants. A production build should keep the default.
    """
    config_path = ship_config_path(filename)
    if not config_path.is_file():
        raise FileNotFoundError(f"Ship configuration was not found: {config_path}")
    with config_path.open("r", encoding="utf-8") as source:
        config = json.load(source)
    validate_ship_config(config, config_path, enforce_locked=enforce_locked)
    print(f"Loaded ship configuration: {config_path}")
    print(f"Ship configuration SHA-256: {config_file_sha256(config_path)}")
    return config


def config_file_sha256(path: Path | str) -> str:
    """Hash the exact source bytes used by a build."""
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def canonical_config_json(config: Mapping[str, Any]) -> str:
    """Return stable JSON suitable for embedding in a .blend Text block."""
    return json.dumps(config, ensure_ascii=False, sort_keys=True, separators=(",", ":"))


def config_manifest(config: Mapping[str, Any], source: Path | str | None = None) -> dict[str, Any]:
    """Create build-trace metadata without mutating the source configuration."""
    canonical = canonical_config_json(config)
    manifest: dict[str, Any] = {
        "ship_id": config.get("id"),
        "schema_version": config.get("schema_version", 1),
        "stage": config.get("stage", "proxy"),
        "canonical_sha256": hashlib.sha256(canonical.encode("utf-8")).hexdigest(),
        "canonical_config": canonical,
    }
    if source is not None:
        source_path = Path(source)
        manifest["source_path"] = str(source_path)
        if source_path.is_file():
            manifest["source_file_sha256"] = config_file_sha256(source_path)
    return manifest


def resolve_config_value(config: Mapping[str, Any], dotted_path: str) -> Any:
    """Resolve a provenance/override path such as ``hull.bow_v_shape``."""
    if not dotted_path or dotted_path.startswith(".") or dotted_path.endswith("."):
        raise KeyError(f"Invalid configuration path: {dotted_path!r}")
    current: Any = config
    for part in dotted_path.split("."):
        if not isinstance(current, Mapping) or part not in current:
            raise KeyError(f"Configuration path does not exist: {dotted_path}")
        current = current[part]
    return current


def clone_with_override(
    config: Mapping[str, Any],
    dotted_path: str,
    value: Any,
    *,
    validate: bool = True,
) -> dict[str, Any]:
    """Deep-copy a config and change one existing leaf for a response test.

    Locked-production checks are deliberately disabled for the temporary copy;
    schema/range/provenance checks remain enabled. The source mapping is never
    modified.
    """
    result = copy.deepcopy(dict(config))
    parts = dotted_path.split(".")
    if not parts:
        raise KeyError("An override path is required")
    current: Any = result
    for part in parts[:-1]:
        if not isinstance(current, dict) or part not in current:
            raise KeyError(f"Configuration path does not exist: {dotted_path}")
        current = current[part]
    leaf = parts[-1]
    if not isinstance(current, dict) or leaf not in current:
        raise KeyError(f"Configuration path does not exist: {dotted_path}")
    current[leaf] = value
    if validate:
        validate_ship_config(result, enforce_locked=False)
    return result


def validate_ship_config(
    config: Mapping[str, Any],
    source: Path | None = None,
    *,
    enforce_locked: bool = True,
) -> None:
    """Validate either the retained Phase-3A schema or formal schema v2."""
    if not isinstance(config, Mapping):
        raise ValueError("Ship configuration root must be an object")
    schema_version = config.get("schema_version", 1)
    if isinstance(schema_version, bool) or not isinstance(schema_version, int):
        raise ValueError("Configuration field 'schema_version' must be an integer")
    if schema_version == 1:
        _validate_legacy_ship_config(config)
    elif schema_version == FORMAL_SCHEMA_VERSION:
        _validate_formal_ship_config(config)
        if enforce_locked:
            _validate_locked_baseline(config)
    else:
        raise ValueError(f"Unsupported ship configuration schema_version: {schema_version}")
    if source is not None and source.suffix.lower() != ".json":
        raise ValueError(f"Ship configuration must be JSON: {source}")


def _validate_formal_ship_config(config: Mapping[str, Any]) -> None:
    identifier = _require_string(config, "id")
    if not _SAFE_ID.fullmatch(identifier):
        raise ValueError("Configuration field 'id' must use lowercase letters, digits, and underscores")
    _require_string(config, "display_name")
    _require_choice(config, "stage", {"formal_base_model"})

    identity = _require_mapping(config, "identity")
    for key in ("period_label", "cultural_origin", "ship_type", "family", "design_intent"):
        _require_string(identity, key, prefix="identity")

    coordinates = _require_mapping(config, "coordinate_system")
    for key in (
        "longitudinal_axis",
        "bow_direction",
        "stern_direction",
        "transverse_axis",
        "port_direction",
        "starboard_direction",
        "up_direction",
        "normalized_longitudinal_ratio",
    ):
        _require_string(coordinates, key, prefix="coordinate_system")
    _require_number(coordinates, "meters_per_blender_unit", positive=True, prefix="coordinate_system")

    dimensions = _require_mapping(config, "dimensions")
    for key in ("length_m", "beam_m", "hull_depth_m"):
        _require_number(dimensions, key, positive=True, prefix="dimensions")
    if float(dimensions["length_m"]) <= float(dimensions["beam_m"]):
        raise ValueError("Hull length must exceed beam")

    hull = _require_mapping(config, "hull")
    ratio_fields = (
        "max_beam_x_ratio",
        "max_beam_z_ratio",
        "midship_fullness",
        "floor_width_ratio",
        "bilge_roundness",
        "tumblehome_ratio",
        "tumblehome_start_z_ratio",
        "upper_side_inset_ratio",
        "bow_region_end_ratio",
        "forward_mid_end_ratio",
        "aft_mid_start_ratio",
        "stern_run_start_ratio",
        "bow_fineness",
        "bow_v_shape",
        "stern_fineness",
        "stern_run_fullness",
    )
    for key in ratio_fields:
        _require_ratio(hull, key, prefix="hull", allow_zero=False)
    _require_choice(hull, "tumblehome_style", {"moderate"}, prefix="hull")
    for key in ("bow_sheer_rise_m", "stern_sheer_rise_m", "keel_curve_m"):
        _require_number(hull, key, nonnegative=True, prefix="hull")
    _require_number(hull, "sheer_curve_exponent", positive=True, prefix="hull")
    region_order = [
        float(hull["bow_region_end_ratio"]),
        float(hull["forward_mid_end_ratio"]),
        float(hull["max_beam_x_ratio"]),
        float(hull["aft_mid_start_ratio"]),
        float(hull["stern_run_start_ratio"]),
    ]
    if any(b <= a for a, b in zip(region_order, region_order[1:])):
        raise ValueError("Hull longitudinal region ratios must increase from bow to stern")

    keel = _require_mapping(config, "keel")
    for key in ("width_m", "depth_m"):
        _require_number(keel, key, positive=True, prefix="keel")
    for key in ("bow_inset_ratio", "stern_inset_ratio"):
        _require_ratio(keel, key, prefix="keel")

    stem = _require_mapping(config, "stem")
    _require_angle(stem, "rake_deg", prefix="stem")
    _require_ratio(stem, "curvature_ratio", prefix="stem")
    for key in ("width_m", "depth_m", "forward_extension_m"):
        _require_number(stem, key, positive=True, prefix="stem")

    stern = _require_mapping(config, "stern")
    _require_choice(stern, "form", {"mirror_transom"}, prefix="stern")
    for key in ("sternpost_rake_deg", "transom_rake_deg"):
        _require_angle(stern, key, prefix="stern")
    for key in (
        "sternpost_width_m",
        "sternpost_depth_m",
        "transom_height_m",
        "upperworks_height_m",
    ):
        _require_number(stern, key, positive=True, prefix="stern")
    for key in ("transom_width_ratio", "transom_bottom_z_ratio", "upperworks_length_ratio"):
        _require_ratio(stern, key, prefix="stern", allow_zero=False)

    rudder = _require_mapping(config, "rudder")
    for key in ("height_m", "length_m", "thickness_m", "aft_offset_m"):
        _require_number(rudder, key, positive=True, prefix="rudder")

    decks = _require_mapping(config, "decks")
    main_deck = _require_mapping(decks, "main", prefix="decks")
    _require_number(main_deck, "baseline_z_m", nonnegative=True, prefix="decks.main")
    for key in ("thickness_m", "edge_inset_m"):
        _require_number(main_deck, key, positive=True, prefix="decks.main")
    _require_bool(main_deck, "follow_sheer", prefix="decks.main")
    for deck_name in ("forecastle", "quarterdeck"):
        deck = _require_mapping(decks, deck_name, prefix="decks")
        _validate_ratio_interval(deck, "start_x_ratio", "end_x_ratio", prefix=f"decks.{deck_name}")
        for key in ("elevation_m", "thickness_m", "side_height_m"):
            _require_number(deck, key, positive=True, prefix=f"decks.{deck_name}")

    bulwarks = _require_mapping(config, "bulwarks")
    _validate_ratio_interval(bulwarks, "start_x_ratio", "end_x_ratio", prefix="bulwarks")
    for key in ("height_m", "thickness_m", "top_rail_height_m", "top_rail_outboard_m"):
        _require_number(bulwarks, key, positive=True, prefix="bulwarks")

    wales = _require_mapping(config, "wales")
    wale_count = _require_integer(wales, "count_per_side", minimum=1, prefix="wales")
    wale_levels = wales.get("vertical_z_ratios")
    if not isinstance(wale_levels, list) or len(wale_levels) != wale_count:
        raise ValueError("Configuration field 'wales.vertical_z_ratios' must match count_per_side")
    _validate_ratio_sequence(wale_levels, "wales.vertical_z_ratios")
    for key in ("height_m", "outboard_projection_m"):
        _require_number(wales, key, positive=True, prefix="wales")
    _validate_ratio_interval(wales, "start_x_ratio", "end_x_ratio", prefix="wales")

    gunports = _require_mapping(config, "gunports")
    _require_integer(gunports, "deck_count", minimum=1, prefix="gunports")
    _require_integer(gunports, "count_per_side", minimum=1, prefix="gunports")
    for key in ("width_m", "height_m", "recess_depth_m", "frame_thickness_m"):
        _require_number(gunports, key, positive=True, prefix="gunports")
    _validate_ratio_interval(gunports, "forward_limit_ratio", "aft_limit_ratio", prefix="gunports")
    _require_choice(gunports, "spacing_mode", {"even_between_limits"}, prefix="gunports")
    _require_number(gunports, "vertical_offset_m", nonnegative=True, prefix="gunports")
    _require_bool(gunports, "follow_sheer", prefix="gunports")

    generation = _require_mapping(config, "generation")
    _require_integer(generation, "longitudinal_sections", minimum=9, prefix="generation")
    _require_integer(generation, "cross_section_resolution", minimum=5, prefix="generation")
    _require_choice(
        generation,
        "mirror_strategy",
        {"procedural_y_symmetry", "blender_y_mirror_applied"},
        prefix="generation",
    )
    _require_number(
        generation,
        "centerline_merge_tolerance_m",
        positive=True,
        prefix="generation",
    )

    stylization = _require_mapping(config, "stylization")
    for key in (
        "structural_exaggeration",
        "minimum_feature_size_m",
        "wale_projection_scale",
        "gunport_frame_scale",
    ):
        _require_number(stylization, key, positive=True, prefix="stylization")

    materials = _require_mapping(config, "materials")
    for key in (
        "hull_wood_rgba",
        "deck_wood_rgba",
        "structural_dark_wood_rgba",
        "gunport_interior_rgba",
    ):
        _require_rgba(materials, key, prefix="materials")

    validation = _require_mapping(config, "validation")
    for key in (
        "dimension_abs_tolerance_m",
        "ratio_abs_tolerance",
        "symmetry_tolerance_m",
        "centerline_tolerance_m",
        "duplicate_vertex_tolerance_m",
        "zero_area_tolerance_m2",
        "spacing_relative_tolerance",
        "transform_tolerance",
    ):
        _require_number(validation, key, positive=True, prefix="validation")
    render_margin = _require_ratio(validation, "render_safe_margin_ratio", prefix="validation")
    if render_margin >= 0.25:
        raise ValueError("Configuration field 'validation.render_safe_margin_ratio' must be below 0.25")

    output = _require_mapping(config, "output")
    blend_name = _require_string(output, "blend_name", prefix="output")
    if Path(blend_name).name != blend_name or not blend_name.lower().endswith(".blend"):
        raise ValueError("Configuration field 'output.blend_name' must be a .blend filename")
    render_prefix = _require_string(output, "render_prefix", prefix="output")
    render_path = Path(render_prefix)
    if render_path.is_absolute() or ".." in render_path.parts:
        raise ValueError("Configuration field 'output.render_prefix' must be a safe relative path")
    required_views = output.get("required_views")
    if not isinstance(required_views, list) or not all(isinstance(view, str) for view in required_views):
        raise ValueError("Configuration field 'output.required_views' must be a string array")
    if len(required_views) != len(set(required_views)):
        raise ValueError("Configuration field 'output.required_views' contains duplicates")
    missing_views = _REQUIRED_RENDER_VIEWS.difference(required_views)
    if missing_views:
        raise ValueError(f"Configuration is missing required render views: {sorted(missing_views)}")

    _validate_provenance(config)


def _validate_locked_baseline(config: Mapping[str, Any]) -> None:
    """Protect the explicit Gelderland brief while allowing isolated variants."""
    if config.get("id") != "gelderland_1634":
        return
    for dotted_path, expected in _GELDERLAND_LOCKED_VALUES.items():
        actual = resolve_config_value(config, dotted_path)
        if isinstance(expected, float):
            if isinstance(actual, bool) or not isinstance(actual, (int, float)):
                raise ValueError(f"Locked field '{dotted_path}' must be numeric")
            matches = math.isclose(float(actual), expected, rel_tol=0.0, abs_tol=1e-9)
        else:
            matches = actual == expected
        if not matches:
            raise ValueError(
                f"Locked Gelderland baseline changed at '{dotted_path}': "
                f"expected {expected!r}, got {actual!r}"
            )


def _validate_provenance(config: Mapping[str, Any]) -> None:
    provenance = _require_mapping(config, "provenance")
    assigned: dict[str, str] = {}
    for category in PROVENANCE_CATEGORIES:
        category_data = _require_mapping(provenance, category, prefix="provenance")
        _require_string(category_data, "description", prefix=f"provenance.{category}")
        parameters = category_data.get("parameters")
        if not isinstance(parameters, list) or not parameters:
            raise ValueError(f"Configuration field 'provenance.{category}.parameters' must be a non-empty array")
        for dotted_path in parameters:
            if not isinstance(dotted_path, str) or not dotted_path:
                raise ValueError(f"Invalid provenance path in category '{category}'")
            resolve_config_value(config, dotted_path)
            if dotted_path in assigned:
                raise ValueError(
                    f"Configuration path '{dotted_path}' is assigned to both "
                    f"'{assigned[dotted_path]}' and '{category}'"
                )
            assigned[dotted_path] = category

    expected_paths: set[str] = set()
    for root in PROVENANCE_PARAMETER_ROOTS:
        root_value = resolve_config_value(config, root)
        expected_paths.update(_iter_leaf_paths(root_value, root))
    assigned_paths = set(assigned)
    missing = expected_paths - assigned_paths
    extra = assigned_paths - expected_paths
    if missing or extra:
        details = []
        if missing:
            details.append(f"unclassified={sorted(missing)}")
        if extra:
            details.append(f"not-model-parameters={sorted(extra)}")
        raise ValueError("Configuration provenance is incomplete: " + "; ".join(details))


def _iter_leaf_paths(value: Any, prefix: str) -> Iterable[str]:
    if isinstance(value, Mapping):
        for key, child in value.items():
            yield from _iter_leaf_paths(child, f"{prefix}.{key}")
    else:
        # Arrays (colors/profile controls) are classified as one atomic value.
        yield prefix


def _validate_legacy_ship_config(config: Mapping[str, Any]) -> None:
    """Retain validation support for archived Phase-3A proxy configurations."""
    _require_string(config, "id")
    _require_string(config, "display_name")
    dimensions = _require_mapping(config, "dimensions")
    for key in ("length_m", "beam_m", "hull_depth_m"):
        _require_number(dimensions, key, positive=True, prefix="dimensions")
    shape = _require_mapping(config, "hull_shape")
    _require_ratio(shape, "max_beam_position", prefix="hull_shape", allow_zero=False)
    for key in ("bow_fineness", "stern_fineness"):
        _require_number(shape, key, positive=True, prefix="hull_shape")
    for key in ("bow_rise_m", "stern_rise_m", "keel_curve"):
        _require_number(shape, key, nonnegative=True, prefix="hull_shape")
    generation = _require_mapping(config, "generation")
    _require_integer(generation, "longitudinal_sections", minimum=5, prefix="generation")
    _require_integer(generation, "vertical_sections", minimum=3, prefix="generation")
    output = _require_mapping(config, "output")
    _require_string(output, "blend_name", prefix="output")
    _require_string(output, "render_prefix", prefix="output")


def _field_path(prefix: str, key: str) -> str:
    return f"{prefix}.{key}" if prefix else key


def _require_mapping(values: Mapping[str, Any], key: str, *, prefix: str = "") -> Mapping[str, Any]:
    value = values.get(key)
    if not isinstance(value, Mapping):
        raise ValueError(f"Configuration field '{_field_path(prefix, key)}' must be an object")
    return value


def _require_string(values: Mapping[str, Any], key: str, *, prefix: str = "") -> str:
    value = values.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"Configuration field '{_field_path(prefix, key)}' must be a non-empty string")
    return value


def _require_bool(values: Mapping[str, Any], key: str, *, prefix: str = "") -> bool:
    value = values.get(key)
    if not isinstance(value, bool):
        raise ValueError(f"Configuration field '{_field_path(prefix, key)}' must be boolean")
    return value


def _require_number(
    values: Mapping[str, Any],
    key: str,
    *,
    positive: bool = False,
    nonnegative: bool = False,
    prefix: str = "",
) -> float:
    value = values.get(key)
    path = _field_path(prefix, key)
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ValueError(f"Configuration field '{path}' must be a number")
    result = float(value)
    if not math.isfinite(result):
        raise ValueError(f"Configuration field '{path}' must be finite")
    if positive and result <= 0.0:
        raise ValueError(f"Configuration field '{path}' must be positive")
    if nonnegative and result < 0.0:
        raise ValueError(f"Configuration field '{path}' must be non-negative")
    return result


def _require_integer(
    values: Mapping[str, Any],
    key: str,
    *,
    minimum: int | None = None,
    prefix: str = "",
) -> int:
    value = values.get(key)
    path = _field_path(prefix, key)
    if isinstance(value, bool) or not isinstance(value, int):
        raise ValueError(f"Configuration field '{path}' must be an integer")
    if minimum is not None and value < minimum:
        raise ValueError(f"Configuration field '{path}' must be at least {minimum}")
    return value


def _require_ratio(
    values: Mapping[str, Any],
    key: str,
    *,
    prefix: str = "",
    allow_zero: bool = True,
) -> float:
    result = _require_number(values, key, prefix=prefix)
    lower_ok = result >= 0.0 if allow_zero else result > 0.0
    if not lower_ok or result > 1.0:
        boundary = "[0, 1]" if allow_zero else "(0, 1]"
        raise ValueError(f"Configuration field '{_field_path(prefix, key)}' must be in {boundary}")
    return result


def _require_angle(values: Mapping[str, Any], key: str, *, prefix: str = "") -> float:
    result = _require_number(values, key, nonnegative=True, prefix=prefix)
    if result >= 45.0:
        raise ValueError(f"Configuration field '{_field_path(prefix, key)}' must be below 45 degrees")
    return result


def _require_choice(
    values: Mapping[str, Any],
    key: str,
    choices: set[str],
    *,
    prefix: str = "",
) -> str:
    result = _require_string(values, key, prefix=prefix)
    if result not in choices:
        raise ValueError(
            f"Configuration field '{_field_path(prefix, key)}' must be one of {sorted(choices)}"
        )
    return result


def _validate_ratio_interval(
    values: Mapping[str, Any],
    start_key: str,
    end_key: str,
    *,
    prefix: str,
) -> None:
    start = _require_ratio(values, start_key, prefix=prefix)
    end = _require_ratio(values, end_key, prefix=prefix)
    if end <= start:
        raise ValueError(
            f"Configuration interval '{_field_path(prefix, start_key)}' -> "
            f"'{_field_path(prefix, end_key)}' must increase"
        )


def _validate_ratio_sequence(values: list[Any], path: str) -> None:
    parsed = []
    for index, value in enumerate(values):
        if isinstance(value, bool) or not isinstance(value, (int, float)):
            raise ValueError(f"Configuration field '{path}[{index}]' must be numeric")
        number = float(value)
        if not math.isfinite(number) or not 0.0 <= number <= 1.0:
            raise ValueError(f"Configuration field '{path}[{index}]' must be a finite [0, 1] ratio")
        parsed.append(number)
    if any(b <= a for a, b in zip(parsed, parsed[1:])):
        raise ValueError(f"Configuration field '{path}' must be strictly increasing")


def _require_rgba(values: Mapping[str, Any], key: str, *, prefix: str = "") -> tuple[float, ...]:
    value = values.get(key)
    path = _field_path(prefix, key)
    if not isinstance(value, list) or len(value) != 4:
        raise ValueError(f"Configuration field '{path}' must be an RGBA array of four values")
    result = []
    for index, component in enumerate(value):
        if isinstance(component, bool) or not isinstance(component, (int, float)):
            raise ValueError(f"Configuration field '{path}[{index}]' must be numeric")
        number = float(component)
        if not math.isfinite(number) or not 0.0 <= number <= 1.0:
            raise ValueError(f"Configuration field '{path}[{index}]' must be in [0, 1]")
        result.append(number)
    return tuple(result)
