"""Export a minimal mesh with Blender 5.2's bundled official FBX exporter."""

import importlib
from pathlib import Path

import bpy


def pipeline_root() -> Path:
    """Locate BlenderShipPipeline independently of Blender's working directory."""
    return Path(__file__).resolve().parents[1]


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def get_fbx_export_operator():
    """Register Blender 5.2's bundled FBX exporter for this process if needed."""
    if "fbx" not in dir(bpy.ops.export_scene):
        try:
            bundled_exporter = importlib.import_module("io_scene_fbx")
            bundled_exporter.register()
        except Exception as error:
            raise RuntimeError(
                "Blender's bundled official FBX exporter could not be registered "
                "for this background process"
            ) from error

    if "fbx" not in dir(bpy.ops.export_scene):
        raise RuntimeError("Blender 5.2 FBX export operator is unavailable after registration")
    return bpy.ops.export_scene.fbx


def main() -> None:
    root = pipeline_root()
    blend_dir = root / "output" / "blend"
    export_dir = root / "output" / "exports"
    blend_dir.mkdir(parents=True, exist_ok=True)
    export_dir.mkdir(parents=True, exist_ok=True)
    blend_path = blend_dir / "export_test.blend"
    fbx_path = export_dir / "export_test.fbx"

    clear_scene()
    bpy.ops.mesh.primitive_cube_add(location=(0.0, 0.0, 0.0))
    cube = bpy.context.active_object
    cube.name = "Codex_Export_Test"
    cube.data.name = "Codex_Export_Test_Mesh"

    # Remove only this prior generated test file to prevent Blender from
    # emitting a .blend1 backup during repeatable automation runs.
    if blend_path.exists():
        blend_path.unlink()
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    if not blend_path.is_file() or blend_path.stat().st_size == 0:
        raise RuntimeError(f"Invalid .blend output: {blend_path}")

    bpy.ops.object.select_all(action="DESELECT")
    cube.select_set(True)
    bpy.context.view_layer.objects.active = cube

    export_fbx = get_fbx_export_operator()
    result = export_fbx(
        filepath=str(fbx_path),
        use_selection=True,
        object_types={"MESH"},
        use_mesh_modifiers=True,
        bake_anim=False,
        add_leaf_bones=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    if "FINISHED" not in result:
        raise RuntimeError(f"FBX export did not finish successfully: {result}")
    if not fbx_path.is_file() or fbx_path.stat().st_size == 0:
        raise RuntimeError(f"Invalid FBX output: {fbx_path}")

    print(f"PIPELINE_EXPORT_TEST_SUCCESS: {fbx_path}")


if __name__ == "__main__":
    main()
