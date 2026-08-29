"""Bounding-box-aware diagnostic rendering for formal ship builds."""

from pathlib import Path

import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector


RENDER_SIZE = 1024


def _as_objects(targets) -> list[bpy.types.Object]:
    if isinstance(targets, bpy.types.Object):
        return [targets]
    objects = [obj for obj in targets if isinstance(obj, bpy.types.Object)]
    if not objects:
        raise RuntimeError("Turnaround renderer received no Blender objects")
    return objects


def _object_corners(objects: list[bpy.types.Object]) -> list[Vector]:
    return [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]


def _bounds(objects: list[bpy.types.Object]) -> tuple[Vector, Vector, Vector, list[Vector]]:
    corners = _object_corners(objects)
    minimum = Vector((
        min(point.x for point in corners),
        min(point.y for point in corners),
        min(point.z for point in corners),
    ))
    maximum = Vector((
        max(point.x for point in corners),
        max(point.y for point in corners),
        max(point.z for point in corners),
    ))
    return minimum, maximum, (minimum + maximum) * 0.5, corners


def _point_at(object_: bpy.types.Object, target: Vector) -> None:
    object_.rotation_euler = (target - object_.location).to_track_quat("-Z", "Y").to_euler()


def _configure_scene(scene: bpy.types.Scene) -> None:
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = RENDER_SIZE
    scene.render.resolution_y = RENDER_SIZE
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    world = scene.world or bpy.data.worlds.new("Ship_Turnaround_World")
    scene.world = world
    world.color = (0.026, 0.035, 0.052)
    if world.node_tree:
        background = world.node_tree.nodes.get("Background")
        if background:
            background.inputs["Color"].default_value = (0.026, 0.035, 0.052, 1.0)
            background.inputs["Strength"].default_value = 0.32


def _create_lighting(center: Vector, diagonal: float) -> list[bpy.types.Object]:
    lights = []
    for name, direction, energy in (
        ("Diagnostic_Key", Vector((-1.0, -1.1, 1.5)), 3.2),
        ("Diagnostic_Fill", Vector((0.7, 1.0, 0.8)), 1.45),
        ("Diagnostic_Rim", Vector((1.0, -0.3, 1.2)), 1.7),
    ):
        bpy.ops.object.light_add(type="SUN", location=center + direction.normalized() * diagonal)
        light = bpy.context.active_object
        light.name = name
        light.data.energy = energy
        _point_at(light, center)
        lights.append(light)
    return lights


def _create_camera() -> bpy.types.Object:
    bpy.ops.object.camera_add()
    camera = bpy.context.active_object
    camera.name = "Diagnostic_Camera"
    camera.data.clip_start = 0.05
    camera.data.clip_end = 1000.0
    return camera


def _camera_contains(scene: bpy.types.Scene, camera: bpy.types.Object, corners: list[Vector], margin: float = 0.025) -> bool:
    bpy.context.view_layer.update()
    projected = [world_to_camera_view(scene, camera, point) for point in corners]
    return all(
        point.z > 0.0
        and margin <= point.x <= 1.0 - margin
        and margin <= point.y <= 1.0 - margin
        for point in projected
    )


def _fit_perspective(scene: bpy.types.Scene, camera: bpy.types.Object, center: Vector, direction: Vector, diagonal: float, corners: list[Vector]) -> None:
    camera.data.type = "PERSP"
    camera.data.lens = 52.0
    for factor in (1.15, 1.3, 1.5, 1.75, 2.0, 2.35, 2.7):
        camera.location = center + direction.normalized() * diagonal * factor
        _point_at(camera, center)
        if _camera_contains(scene, camera, corners):
            return
    raise RuntimeError("Unable to frame the full ship in perspective camera")


def _render_png(scene: bpy.types.Scene, output_path: Path) -> None:
    scene.render.filepath = str(output_path)
    bpy.ops.render.render(write_still=True)
    if not output_path.is_file() or output_path.stat().st_size == 0:
        raise RuntimeError(f"Diagnostic render was not created: {output_path}")

    image = bpy.data.images.load(str(output_path), check_existing=False)
    try:
        if image.size[0] != RENDER_SIZE or image.size[1] != RENDER_SIZE:
            raise RuntimeError(f"Diagnostic image has unexpected dimensions: {image.size[:]}")
        pixels = image.pixels
        luminances = []
        for index in range(0, len(pixels), 512):
            red, green, blue = pixels[index:index + 3]
            luminances.append(0.2126 * red + 0.7152 * green + 0.0722 * blue)
        if not luminances or sum(luminances) / len(luminances) <= 0.01:
            raise RuntimeError(f"Diagnostic render appears black: {output_path}")
        if max(luminances) - min(luminances) <= 0.015:
            raise RuntimeError(f"Diagnostic render lacks visible foreground contrast: {output_path}")
    finally:
        bpy.data.images.remove(image)


def render_turnaround(targets, render_dir: Path, include_rts: bool = True) -> dict[str, Path]:
    """Render formal-coordinate engineering views and elevated 3/4 diagnostics.

    Formal axes: X longitudinal (-X bow, +X stern), Y transverse
    (-Y port, +Y starboard), and +Z up.
    """
    objects = _as_objects(targets)
    render_dir.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    _configure_scene(scene)
    minimum, maximum, center, corners = _bounds(objects)
    spans = maximum - minimum
    diagonal = max(spans.length, 1.0)
    camera = _create_camera()
    scene.camera = camera
    _create_lighting(center, diagonal)

    orthographic_views = (
        ("port", Vector((0.0, -1.0, 0.0)), max(spans.x, spans.z)),
        ("starboard", Vector((0.0, 1.0, 0.0)), max(spans.x, spans.z)),
        ("top", Vector((0.0, 0.0, 1.0)), max(spans.x, spans.y)),
        ("bow", Vector((-1.0, 0.0, 0.0)), max(spans.y, spans.z)),
        ("stern", Vector((1.0, 0.0, 0.0)), max(spans.y, spans.z)),
    )
    outputs: dict[str, Path] = {}
    camera.data.type = "ORTHO"
    for name, direction, framing_span in orthographic_views:
        camera.location = center + direction * diagonal * 1.5
        _point_at(camera, center)
        camera.data.ortho_scale = max(framing_span * 1.22, 1.0)
        if not _camera_contains(scene, camera, corners):
            raise RuntimeError(f"{name} camera crops the generated ship")
        output_path = render_dir / f"{name}.png"
        _render_png(scene, output_path)
        outputs[name] = output_path

    _fit_perspective(scene, camera, center, Vector((-1.0, 1.0, 0.72)), diagonal, corners)
    perspective_path = render_dir / "perspective.png"
    _render_png(scene, perspective_path)
    outputs["perspective"] = perspective_path

    if include_rts:
        _fit_perspective(scene, camera, center, Vector((-1.0, 1.0, 1.35)), diagonal, corners)
        rts_path = render_dir / "rts_perspective.png"
        _render_png(scene, rts_path)
        outputs["rts_perspective"] = rts_path

    return outputs
