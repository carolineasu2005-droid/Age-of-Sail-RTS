"""Path helpers derived from this package's location, never the process CWD."""

from pathlib import Path


def pipeline_root() -> Path:
    return Path(__file__).resolve().parents[2]


def config_dir() -> Path:
    return pipeline_root() / "config"


def ships_config_dir() -> Path:
    return config_dir() / "ships"


def output_dir() -> Path:
    return pipeline_root() / "output"


def blend_output_dir() -> Path:
    return output_dir() / "blend"


def renders_output_dir() -> Path:
    return output_dir() / "renders"


def reference_dir() -> Path:
    return pipeline_root() / "reference"
