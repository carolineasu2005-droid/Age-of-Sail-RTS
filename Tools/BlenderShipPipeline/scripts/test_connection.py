"""Minimal Blender CLI + bpy connection test for BlenderShipPipeline."""

from pathlib import Path

import bpy


def pipeline_root() -> Path:
    """Locate BlenderShipPipeline independently of Blender's working directory."""
    return Path(__file__).resolve().parents[1]


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def main() -> None:
    root = pipeline_root()
    blend_dir = root / "output" / "blend"
    blend_dir.mkdir(parents=True, exist_ok=True)
    blend_path = blend_dir / "codex_blender_test.blend"

    clear_scene()

    bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, 0.0))
    cube = bpy.context.active_object
    cube.name = "Codex_Test_Cube"
    cube.data.name = "Codex_Test_Cube_Mesh"

    bpy.ops.object.camera_add(location=(7.0, -7.0, 5.0))
    camera = bpy.context.active_object
    camera.name = "Codex_Test_Camera"
    bpy.context.scene.camera = camera

    bpy.ops.object.light_add(type="POINT", location=(4.0, -4.0, 6.0))
    light = bpy.context.active_object
    light.name = "Codex_Test_Light"
    light.data.energy = 1000.0

    # Remove only this prior generated test file to prevent Blender from
    # emitting a .blend1 backup during repeatable automation runs.
    if blend_path.exists():
        blend_path.unlink()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    if not blend_path.is_file() or blend_path.stat().st_size == 0:
        raise RuntimeError(f"Invalid .blend output: {blend_path}")

    print(f"PIPELINE_TEST_CONNECTION_SUCCESS: {blend_path}")


if __name__ == "__main__":
    main()
