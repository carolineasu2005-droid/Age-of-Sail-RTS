"""Longitudinal control profiles for the formal procedural ship hull.

This module deliberately has no :mod:`bpy` dependency. It turns the ship
configuration into smoothly interpolated station data in the production
coordinate system: X is longitudinal, bow is -X, stern is +X.
"""

from __future__ import annotations

from dataclasses import dataclass
import math
from typing import Sequence


PROFILE_NAMES = ("bow", "forward_mid", "midship", "aft_mid", "stern_run")


def clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(maximum, value))


def smoothstep(value: float) -> float:
    value = clamp(value, 0.0, 1.0)
    return value * value * (3.0 - 2.0 * value)


def _smooth_power(value: float, exponent: float) -> float:
    return smoothstep(value) ** max(exponent, 0.05)


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


@dataclass(frozen=True)
class SectionCharacter:
    """Dimensionless transverse character at one longitudinal profile key."""

    width_scale: float
    floor_width: float
    lower_bilge_width: float
    full_bilge_width: float
    upper_side_width: float
    tumblehome_width: float
    rail_width: float
    max_breadth_z_ratio: float

    def interpolate(self, other: "SectionCharacter", amount: float) -> "SectionCharacter":
        amount = smoothstep(amount)

        def mix(a: float, b: float) -> float:
            return a + (b - a) * amount

        return SectionCharacter(
            width_scale=mix(self.width_scale, other.width_scale),
            floor_width=mix(self.floor_width, other.floor_width),
            lower_bilge_width=mix(self.lower_bilge_width, other.lower_bilge_width),
            full_bilge_width=mix(self.full_bilge_width, other.full_bilge_width),
            upper_side_width=mix(self.upper_side_width, other.upper_side_width),
            tumblehome_width=mix(self.tumblehome_width, other.tumblehome_width),
            rail_width=mix(self.rail_width, other.rail_width),
            max_breadth_z_ratio=mix(self.max_breadth_z_ratio, other.max_breadth_z_ratio),
        )


@dataclass(frozen=True)
class LongitudinalSample:
    """All longitudinal values needed to construct one hull station."""

    normalized_x: float
    x: float
    keel_z: float
    rail_z: float
    character: SectionCharacter

    @property
    def section_height(self) -> float:
        return self.rail_z - self.keel_z


def profile_knots(config: dict) -> tuple[float, float, float, float, float]:
    """Return ordered characteristic locations from bow (0) to stern (1)."""
    hull = _mapping(config, "hull", "hull_shape")
    max_beam = _number(hull, "max_beam_x_ratio", _number(hull, "max_beam_position", 0.54))
    max_beam = clamp(max_beam, 0.40, 0.64)
    bow_region_end = _number(hull, "bow_region_end_ratio", 0.24)
    forward_mid_end = _number(hull, "forward_mid_end_ratio", 0.42)
    aft_mid_start = _number(hull, "aft_mid_start_ratio", 0.68)
    stern_run_start = _number(hull, "stern_run_start_ratio", 0.80)
    forward_default = 0.5 * (bow_region_end + forward_mid_end)
    aft_default = 0.5 * (aft_mid_start + stern_run_start)
    forward_mid = clamp(
        _number(hull, "forward_mid_profile_x_ratio", forward_default),
        0.12,
        max_beam - 0.08,
    )
    aft_mid = clamp(
        _number(hull, "aft_mid_profile_x_ratio", aft_default),
        max_beam + 0.08,
        0.90,
    )
    return (0.0, forward_mid, max_beam, aft_mid, 1.0)


def _profile_characters(config: dict) -> tuple[SectionCharacter, ...]:
    hull = _mapping(config, "hull", "hull_shape")
    stern = _mapping(config, "stern")
    bow_fineness = clamp(_number(hull, "bow_fineness", 0.72), 0.0, 1.5)
    bow_v_shape = clamp(_number(hull, "bow_v_shape", 0.70), 0.0, 1.0)
    midship_fullness = clamp(_number(hull, "midship_fullness", 0.90), 0.55, 1.0)
    stern_fineness = clamp(_number(hull, "stern_fineness", 0.82), 0.0, 1.5)
    stern_fullness = clamp(_number(hull, "stern_run_fullness", 0.72), 0.0, 1.0)
    floor_width = clamp(_number(hull, "floor_width_ratio", 0.44), 0.25, 0.62)
    bilge_roundness = clamp(_number(hull, "bilge_roundness", 0.76), 0.35, 1.0)
    max_breadth_z = clamp(_number(hull, "max_beam_z_ratio", 0.55), 0.43, 0.62)
    tumblehome = clamp(_number(hull, "tumblehome_ratio", 0.12), 0.03, 0.24)
    upper_inset = clamp(_number(hull, "upper_side_inset_ratio", 0.02), 0.0, 0.10)
    rail_width = clamp(1.0 - tumblehome - upper_inset, 0.68, 0.95)
    transom_width = clamp(
        _number(stern, "transom_width_ratio", _number(hull, "transom_width_ratio", 0.58)),
        0.40,
        0.76,
    )

    bow = SectionCharacter(
        width_scale=0.0,
        floor_width=clamp(floor_width * (1.0 - 0.86 * bow_v_shape), 0.035, 0.20),
        lower_bilge_width=clamp(0.58 - 0.38 * bow_v_shape, 0.20, 0.52),
        full_bilge_width=clamp(0.84 - 0.25 * bow_v_shape, 0.54, 0.80),
        upper_side_width=0.90,
        tumblehome_width=clamp(rail_width + 0.08, rail_width, 0.94),
        rail_width=clamp(rail_width * 0.88, 0.62, 0.86),
        max_breadth_z_ratio=clamp(max_breadth_z + 0.075, 0.50, 0.67),
    )
    midship = SectionCharacter(
        width_scale=1.0,
        floor_width=floor_width,
        lower_bilge_width=clamp(0.62 + 0.14 * bilge_roundness + 0.06 * midship_fullness, 0.70, 0.84),
        full_bilge_width=clamp(0.88 + 0.06 * bilge_roundness + 0.05 * midship_fullness, 0.93, 0.985),
        upper_side_width=0.985,
        tumblehome_width=clamp(1.0 - tumblehome * 0.28, rail_width, 0.99),
        rail_width=rail_width,
        max_breadth_z_ratio=max_breadth_z,
    )
    forward_mid = SectionCharacter(
        width_scale=clamp(0.71 - 0.19 * bow_fineness, 0.48, 0.70),
        floor_width=bow.floor_width * 0.40 + midship.floor_width * 0.60,
        lower_bilge_width=bow.lower_bilge_width * 0.36 + midship.lower_bilge_width * 0.64,
        full_bilge_width=bow.full_bilge_width * 0.30 + midship.full_bilge_width * 0.70,
        upper_side_width=0.96,
        tumblehome_width=midship.tumblehome_width * 0.96,
        rail_width=rail_width * 0.95,
        max_breadth_z_ratio=clamp(max_breadth_z + 0.035, 0.47, 0.65),
    )
    aft_mid = SectionCharacter(
        width_scale=clamp(0.95 + 0.04 * stern_fullness - 0.05 * stern_fineness, 0.88, 0.97),
        floor_width=floor_width * (0.78 + 0.10 * stern_fullness),
        lower_bilge_width=midship.lower_bilge_width * (0.91 + 0.05 * stern_fullness),
        full_bilge_width=midship.full_bilge_width * 0.985,
        upper_side_width=0.98,
        tumblehome_width=midship.tumblehome_width,
        rail_width=rail_width,
        max_breadth_z_ratio=clamp(max_breadth_z + 0.01, 0.45, 0.64),
    )
    stern_run = SectionCharacter(
        width_scale=transom_width,
        floor_width=floor_width * (0.62 + 0.16 * stern_fullness - 0.10 * stern_fineness),
        lower_bilge_width=clamp(0.61 + 0.17 * stern_fullness - 0.08 * stern_fineness, 0.53, 0.74),
        full_bilge_width=clamp(0.86 + 0.10 * stern_fullness, 0.86, 0.96),
        upper_side_width=0.97,
        tumblehome_width=midship.tumblehome_width,
        rail_width=rail_width,
        max_breadth_z_ratio=clamp(max_breadth_z + 0.04, 0.49, 0.66),
    )
    return bow, forward_mid, midship, aft_mid, stern_run


def _interpolate_character(
    normalized_x: float,
    knots: Sequence[float],
    profiles: Sequence[SectionCharacter],
) -> SectionCharacter:
    normalized_x = clamp(normalized_x, 0.0, 1.0)
    for index in range(len(knots) - 1):
        left, right = knots[index], knots[index + 1]
        if normalized_x <= right or index == len(knots) - 2:
            amount = (normalized_x - left) / max(right - left, 1.0e-9)
            return profiles[index].interpolate(profiles[index + 1], amount)
    return profiles[-1]


def sample_longitudinal(config: dict, normalized_x: float) -> LongitudinalSample:
    """Sample the smooth hull profiles at bow-to-stern coordinate ``0..1``."""
    dimensions = _mapping(config, "dimensions")
    hull = _mapping(config, "hull", "hull_shape")
    length = _number(dimensions, "length_m", 36.4)
    depth = _number(dimensions, "hull_depth_m", 4.2)
    normalized_x = clamp(normalized_x, 0.0, 1.0)
    x = -0.5 * length + normalized_x * length

    bow_rise = _number(hull, "bow_sheer_rise_m", _number(hull, "bow_rise_m", 1.10))
    stern_rise = _number(hull, "stern_sheer_rise_m", _number(hull, "stern_rise_m", 1.65))
    exponent = _number(hull, "sheer_curve_exponent", 1.35)
    bow_end = clamp(_number(hull, "bow_sheer_end_x_ratio", 0.38), 0.20, 0.48)
    stern_start = clamp(_number(hull, "stern_sheer_start_x_ratio", 0.60), 0.52, 0.78)
    bow_amount = _smooth_power((bow_end - normalized_x) / bow_end, exponent)
    stern_amount = _smooth_power((normalized_x - stern_start) / (1.0 - stern_start), exponent)
    rail_z = _number(hull, "deck_baseline_m", depth) + bow_rise * bow_amount + stern_rise * stern_amount

    keel_curve = max(0.0, _number(hull, "keel_curve_m", _number(hull, "keel_curve", 0.12)))
    bow_keel_rise = _number(hull, "bow_keel_rise_m", max(0.28, bow_rise * 0.30))
    stern_keel_rise = _number(hull, "stern_keel_rise_m", max(0.18, stern_rise * 0.14))
    keel_z = (
        keel_curve * (1.0 - math.sin(math.pi * normalized_x) ** 2)
        + bow_keel_rise * bow_amount
        + stern_keel_rise * stern_amount
    )
    if rail_z <= keel_z + 0.5:
        raise ValueError("Hull longitudinal profile produced a non-positive section height")
    character = _interpolate_character(normalized_x, profile_knots(config), _profile_characters(config))
    return LongitudinalSample(normalized_x, x, keel_z, rail_z, character)


def distributed_positions(count: int, knots: Sequence[float]) -> list[float]:
    """Distribute exactly ``count`` values while retaining all supplied knots."""
    clean_knots = sorted({clamp(float(value), 0.0, 1.0) for value in knots} | {0.0, 1.0})
    if count < len(clean_knots):
        raise ValueError(f"At least {len(clean_knots)} samples are required for the control knots")
    segment_count = count - 1
    lengths = [right - left for left, right in zip(clean_knots, clean_knots[1:])]
    allocations = [max(1, int(round(length * segment_count))) for length in lengths]
    while sum(allocations) < segment_count:
        index = max(range(len(lengths)), key=lambda i: lengths[i] / allocations[i])
        allocations[index] += 1
    while sum(allocations) > segment_count:
        candidates = [i for i, allocation in enumerate(allocations) if allocation > 1]
        if not candidates:
            raise RuntimeError("Unable to distribute hull samples across profile regions")
        index = min(candidates, key=lambda i: lengths[i] / allocations[i])
        allocations[index] -= 1
    positions = [clean_knots[0]]
    for left, right, allocation in zip(clean_knots, clean_knots[1:], allocations):
        positions.extend(left + (right - left) * step / allocation for step in range(1, allocation + 1))
    if len(positions) != count or any(b <= a for a, b in zip(positions, positions[1:])):
        raise RuntimeError("Hull sample distribution produced duplicate or unordered positions")
    return positions


def station_positions(config: dict, count: int) -> list[float]:
    """Return formal hull stations including every regional profile key."""
    return distributed_positions(count, profile_knots(config))
