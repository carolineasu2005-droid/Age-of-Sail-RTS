"""Render a visibly framed test object to validate Blender's headless renderer."""

from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


def pipeline_root() -> Path:
    """Locate BlenderShipPipeline independently of Blender's working directory."""
    return Path(__file__).resolve().parents[1]


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()


def point_camera_at(camera: bpy.types.Object, target: Vector) -> None:
    direction = target - camera.location
    camera.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def make_material() -> bpy.types.Material:
    material = bpy.data.materials.new(name="Codex_Render_Test_Material")
    material.diffuse_color = (0.08, 0.45, 0.95, 1.0)
    if material.node_tree:
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled:
            principled.inputs["Base Color"].default_value = (0.08, 0.45, 0.95, 1.0)
            principled.inputs["Roughness"].default_value = 0.32
            principled.inputs["Metallic"].default_value = 0.08
    return material


def configure_render(scene: bpy.types.Scene, output_path: Path) -> None:
    # Blender 5.2 exposes the EEVEE renderer as BLENDER_EEVEE (rather than
    # the older BLENDER_EEVEE_NEXT enum name).
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.filepath = str(output_path)

    world = scene.world or bpy.data.worlds.new("Codex_Render_Test_World")
    scene.world = world
    world.color = (0.025, 0.04, 0.08)
    if world.node_tree:
        background = world.node_tree.nodes.get("Background")
        if background:
            background.inputs["Color"].default_value = (0.025, 0.04, 0.08, 1.0)
            background.inputs["Strength"].default_value = 0.35


def verify_camera_composition(scene: bpy.types.Scene, camera: bpy.types.Object) -> None:
    bpy.context.view_layer.update()
    projected_origin = world_to_camera_view(scene, camera, Vector((0.0, 0.0, 0.0)))
    if not (0.2 < projected_origin.x < 0.8 and 0.2 < projected_origin.y < 0.8 and projected_origin.z > 0):
        raise RuntimeError(f"Test cube is not correctly framed by the camera: {projected_origin}")


def verify_render(image_path: Path) -> None:
    if not image_path.is_file() or image_path.stat().st_size == 0:
        raise RuntimeError(f"Invalid PNG output: {image_path}")

    # Load the written PNG instead of relying on the transient Render Result
    # data block, whose dimensions are unavailable in this Blender 5.2
    # background configuration.
    image = bpy.data.images.load(str(image_path), check_existing=False)
    if image.size[0] <= 0 or image.size[1] <= 0:
        raise RuntimeError("Written PNG has invalid dimensions")

    # Sample the render result to guard against an all-black headless render.
    pixels = image.pixels
    sample_count = 0
    luminance_total = 0.0
    for index in range(0, len(pixels), 256):
        red, green, blue = pixels[index:index + 3]
        luminance_total += 0.2126 * red + 0.7152 * green + 0.0722 * blue
        sample_count += 1
    average_luminance = luminance_total / sample_count
    if average_luminance <= 0.01:
        raise RuntimeError(f"Render appears black (average luminance={average_luminance:.5f})")

    print(
        f"PIPELINE_RENDER_VALID: {image_path} "
        f"({image.size[0]}x{image.size[1]}, average_luminance={average_luminance:.4f})"
    )


def main() -> None:
    root = pipeline_root()
    render_dir = root / "output" / "renders"
    render_dir.mkdir(parents=True, exist_ok=True)
    output_path = render_dir / "test_render.png"

    clear_scene()
    scene = bpy.context.scene
    configure_render(scene, output_path)

    bpy.ops.mesh.primitive_cube_add(size=2.4, location=(0.0, 0.0, 0.0))
    cube = bpy.context.active_object
    cube.name = "Codex_Render_Test_Cube"
    cube.data.materials.append(make_material())
    bevel = cube.modifiers.new(name="Soft_Edges", type="BEVEL")
    bevel.width = 0.12
    bevel.segments = 3

    bpy.ops.object.camera_add(location=(7.2, -7.2, 5.4))
    camera = bpy.context.active_object
    camera.name = "Codex_Render_Test_Camera"
    camera.data.lens = 52
    point_camera_at(camera, Vector((0.0, 0.0, 0.0)))
    scene.camera = camera
    verify_camera_composition(scene, camera)

    bpy.ops.object.light_add(type="AREA", location=(4.5, -4.5, 6.5))
    key_light = bpy.context.active_object
    key_light.name = "Codex_Render_Key_Light"
    key_light.data.energy = 900.0
    key_light.data.shape = "DISK"
    key_light.data.size = 5.0
    point_camera_at(key_light, Vector((0.0, 0.0, 0.0)))

    bpy.ops.object.light_add(type="AREA", location=(-3.5, -1.5, 3.0))
    fill_light = bpy.context.active_object
    fill_light.name = "Codex_Render_Fill_Light"
    fill_light.data.energy = 350.0
    fill_light.data.size = 4.0
    point_camera_at(fill_light, Vector((0.0, 0.0, 0.0)))

    bpy.ops.render.render(write_still=True)
    verify_render(output_path)
    print(f"PIPELINE_RENDER_TEST_SUCCESS: {output_path}")


if __name__ == "__main__":
    main()
