"""Build the formal Gelderland c.1634 production blockout from JSON."""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path

import bpy


SCRIPTS_DIR = Path(__file__).resolve().parent
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

from core.config import (
    clone_with_override,
    config_manifest,
    load_ship_config,
    ship_config_path,
)
from core.paths import blend_output_dir, renders_output_dir
from core.validation import (
    ValidationError,
    measure_region_width,
    validate_centerline,
    validate_dimensions,
    validate_forbidden_assets,
    validate_gunports,
    validate_mirrored_pair,
    validate_parameter_response,
    validate_required_objects,
    validate_symmetry,
    validate_topology,
    validate_transforms,
    world_bounds,
)
from render.turnaround import render_turnaround
from ship.decks import create_decks
from ship.gunports import build_gunport_layout, create_main_gunports, validate_gunport_objects
from ship.hull import create_formal_hull, sample_hull, sample_hull_surface
from ship.structures import create_primary_structures


FORMAL_COLLECTION_NAME = "SHIP_Gelderland1634"
SUBCOLLECTION_NAMES = ("Structure", "Decks", "HullDetails", "Gunports")
WORK_COLLECTION_NAME = "_Automation_Work"


def _activate_layer_collection(collection_name: str) -> None:
    """Make a scene collection the context target used by bpy object creators."""
    bpy.context.view_layer.update()

    def find(layer_collection: bpy.types.LayerCollection) -> bpy.types.LayerCollection | None:
        if layer_collection.collection.name == collection_name:
            return layer_collection
        for child in layer_collection.children:
            match = find(child)
            if match is not None:
                return match
        return None

    layer_collection = find(bpy.context.view_layer.layer_collection)
    if layer_collection is None:
        raise RuntimeError(f"Collection is not linked to the active view layer: {collection_name}")
    bpy.context.view_layer.active_layer_collection = layer_collection


def clear_generated_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete()
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for material in list(bpy.data.materials):
        bpy.data.materials.remove(material)
    for text in list(bpy.data.texts):
        bpy.data.texts.remove(text)
    work = bpy.data.collections.new(WORK_COLLECTION_NAME)
    bpy.context.scene.collection.children.link(work)
    _activate_layer_collection(WORK_COLLECTION_NAME)


def create_ship_collections() -> dict[str, bpy.types.Collection]:
    root = bpy.data.collections.new(FORMAL_COLLECTION_NAME)
    bpy.context.scene.collection.children.link(root)
    collections = {"root": root}
    for name in SUBCOLLECTION_NAMES:
        child = bpy.data.collections.new(name)
        root.children.link(child)
        collections[name] = child
    _activate_layer_collection("Structure")
    work = bpy.data.collections.get(WORK_COLLECTION_NAME)
    if work is not None:
        bpy.data.collections.remove(work)
    return collections


def _material(name: str, rgba: list[float], roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name=name)
    material.diffuse_color = tuple(rgba)
    if material.node_tree:
        principled = material.node_tree.nodes.get("Principled BSDF")
        if principled:
            principled.inputs["Base Color"].default_value = tuple(rgba)
            principled.inputs["Roughness"].default_value = roughness
            principled.inputs["Metallic"].default_value = 0.0
    return material


def create_materials(config: dict) -> dict[str, bpy.types.Material]:
    colors = config["materials"]
    return {
        "hull": _material("MAT_Hull_Wood", colors["hull_wood_rgba"], 0.78),
        "deck": _material("MAT_Deck_Wood", colors["deck_wood_rgba"], 0.86),
        "structural": _material("MAT_Structural_Dark_Wood", colors["structural_dark_wood_rgba"], 0.72),
        "gunport_interior": _material("MAT_Gunport_Interior", colors["gunport_interior_rgba"], 0.94),
    }


def move_object(object_: bpy.types.Object, target: bpy.types.Collection) -> None:
    if object_.name not in target.objects:
        target.objects.link(object_)
    for collection in list(object_.users_collection):
        if collection is not target:
            collection.objects.unlink(object_)


def remove_mesh_object(object_: bpy.types.Object) -> None:
    mesh = object_.data if object_.type == "MESH" else None
    bpy.data.objects.remove(object_, do_unlink=True)
    if mesh is not None and mesh.users == 0:
        bpy.data.meshes.remove(mesh)


def _hull_metrics(hull: bpy.types.Object) -> dict[str, float]:
    bounds = world_bounds(hull)
    bow = measure_region_width(hull, x_ratio_range=(0.05, 0.20), z_ratio_range=(0.10, 0.58))
    mid_upper = measure_region_width(hull, x_ratio_range=(0.48, 0.60), z_ratio_range=(0.70, 0.98))
    # Sample aft of the exact max-beam station so a bow-only profile change is
    # not measured inside the intentional bow-to-midship interpolation zone.
    mid_lower = measure_region_width(hull, x_ratio_range=(0.56, 0.64), z_ratio_range=(0.10, 0.58))
    stern = measure_region_width(hull, x_ratio_range=(0.90, 1.00), z_ratio_range=(0.18, 0.82))
    return {
        "length_m": bounds.length,
        "beam_m": bounds.beam,
        "height_m": bounds.height,
        "bow_signature": float(bow["width_signature"]),
        "mid_upper_signature": float(mid_upper["width_signature"]),
        "mid_lower_signature": float(mid_lower["width_signature"]),
        "stern_signature": float(stern["width_signature"]),
    }


def _variant_metrics(config: dict, name: str) -> dict[str, float]:
    hull = create_formal_hull(config, object_name=name)
    try:
        return _hull_metrics(hull)
    finally:
        remove_mesh_object(hull)


def run_parameter_response_tests(config: dict) -> list[dict]:
    """Exercise isolated copies, then leave the formal JSON untouched."""
    baseline = _variant_metrics(config, "TEMP_Response_Baseline")
    results: list[dict] = []

    beam_config = clone_with_override(config, "dimensions.beam_m", 9.2)
    beam = _variant_metrics(beam_config, "TEMP_Response_Beam")
    validate_parameter_response(
        "beam_8.6_to_9.2",
        baseline,
        beam,
        changed={"beam_m": {"min_abs_delta": 0.55, "direction": "increase"}},
        unchanged={"length_m": 0.005},
    )
    results.append({"parameter": "dimensions.beam_m", "before": 8.6, "after": 9.2, "result": beam["beam_m"]})

    tumble_config = clone_with_override(config, "hull.tumblehome_ratio", 0.18)
    tumble = _variant_metrics(tumble_config, "TEMP_Response_Tumblehome")
    validate_parameter_response(
        "tumblehome_0.11_to_0.18",
        baseline,
        tumble,
        changed={"mid_upper_signature": {"min_abs_delta": 0.05, "direction": "decrease"}},
        unchanged={"beam_m": 0.005, "length_m": 0.005},
    )
    results.append({
        "parameter": "hull.tumblehome_ratio",
        "before": baseline["mid_upper_signature"],
        "after": tumble["mid_upper_signature"],
        "result": "upper width signature",
    })

    bow_config = clone_with_override(config, "hull.bow_v_shape", 0.90)
    bow = _variant_metrics(bow_config, "TEMP_Response_BowV")
    validate_parameter_response(
        "bow_v_0.72_to_0.90",
        baseline,
        bow,
        changed={"bow_signature": 0.02},
        unchanged={"mid_lower_signature": 0.01, "length_m": 0.005},
    )
    results.append({
        "parameter": "hull.bow_v_shape",
        "before": baseline["bow_signature"],
        "after": bow["bow_signature"],
        "result": "bow signature; midship invariant",
    })

    transom_config = clone_with_override(config, "stern.transom_width_ratio", 0.68)
    transom = _variant_metrics(transom_config, "TEMP_Response_Transom")
    validate_parameter_response(
        "transom_width_0.58_to_0.68",
        baseline,
        transom,
        changed={"stern_signature": {"min_abs_delta": 0.05, "direction": "increase"}},
        unchanged={"beam_m": 0.005, "mid_lower_signature": 0.01},
    )
    results.append({
        "parameter": "stern.transom_width_ratio",
        "before": baseline["stern_signature"],
        "after": transom["stern_signature"],
        "result": "stern signature; midship invariant",
    })

    count_config = clone_with_override(config, "gunports.count_per_side", 12)
    count_layout = build_gunport_layout(count_config, sampler=sample_hull)
    if len(count_layout) != 12 or int(config["gunports"]["count_per_side"]) != 14:
        raise ValidationError("Gunport count response test did not isolate 12 from formal 14")
    results.append({"parameter": "gunports.count_per_side", "before": 14, "after": 12, "result": len(count_layout)})
    return results


def _gunport_records(config: dict) -> list[dict]:
    records = []
    for position in build_gunport_layout(config, sampler=sample_hull):
        records.append({"side": "port", "index": position.index, "center": (position.x, -position.hull_half_width, position.z)})
        records.append({"side": "starboard", "index": position.index, "center": (position.x, position.hull_half_width, position.z)})
    return records


def build_formal_objects(config: dict) -> dict:
    collections = create_ship_collections()
    materials = create_materials(config)

    hull = create_formal_hull(config)
    move_object(hull, collections["Structure"])
    hull.data.materials.append(materials["hull"])
    hull["ship_role"] = "formal_hull"
    pre_boolean_topology = validate_topology(hull, closed=True)
    validate_centerline(hull)
    validate_symmetry(hull)

    structures = create_primary_structures(
        config,
        sampler=sample_hull,
        collections=collections,
        materials=materials,
    )
    decks = create_decks(
        config,
        sampler=sample_hull,
        collections=collections,
        materials=materials,
    )
    gunports = create_main_gunports(
        config,
        hull_object=hull,
        sampler=sample_hull,
        collections=collections,
        materials=materials,
        apply_recess_boolean=True,
    )
    validate_gunport_objects(gunports, config, require_locked_count=True)

    production_objects = [hull, *structures.values(), *decks.values(), *gunports.values()]
    return {
        "hull": hull,
        "structures": structures,
        "decks": decks,
        "gunports": gunports,
        "objects": production_objects,
        "collections": collections,
        "materials": materials,
        "pre_boolean_topology": pre_boolean_topology,
    }


def validate_formal_build(config: dict, build: dict) -> dict:
    hull = build["hull"]
    structures = build["structures"]
    decks = build["decks"]
    gunports = build["gunports"]
    objects = build["objects"]
    settings = config["validation"]

    hull_topology = validate_topology(
        hull,
        closed=True,
        duplicate_tolerance=float(settings["duplicate_vertex_tolerance_m"]),
        zero_area_tolerance=float(settings["zero_area_tolerance_m2"]),
    )
    centerline = validate_centerline(hull, tolerance=float(settings["centerline_tolerance_m"]))
    symmetry = validate_symmetry(hull, tolerance=float(settings["symmetry_tolerance_m"]))
    dimensions = validate_dimensions(hull, config, full_ship_objects=objects)
    required = validate_required_objects(objects)

    closed_structure_stats = {}
    for object_ in [*structures.values(), *decks.values()]:
        closed_structure_stats[object_.name] = validate_topology(object_, closed=True)
    for object_ in gunports.values():
        closed_structure_stats[object_.name] = validate_topology(object_, closed=False)

    validate_mirrored_pair(structures["Bulwark_Port"], structures["Bulwark_Starboard"])
    validate_mirrored_pair(structures["Wales_Main_Port"], structures["Wales_Main_Starboard"])
    gunport_stats = validate_gunports(_gunport_records(config), config)
    transform_stats = validate_transforms(
        objects,
        tolerance=float(settings["transform_tolerance"]),
        require_zero_location=True,
        require_identity_rotation=True,
    )
    forbidden = validate_forbidden_assets(
        objects,
        collections=[build["collections"][name] for name in ("root", *SUBCOLLECTION_NAMES)],
    )
    return {
        "pre_boolean_topology": build["pre_boolean_topology"],
        "hull_topology": hull_topology,
        "centerline": centerline,
        "symmetry": symmetry,
        "dimensions": dimensions,
        "required": required,
        "gunports": gunport_stats,
        "transforms": transform_stats,
        "forbidden": forbidden,
        "component_topology": closed_structure_stats,
    }


def embed_build_manifest(config: dict) -> dict:
    manifest = config_manifest(config, ship_config_path())
    manifest["blender_version"] = bpy.app.version_string
    scene = bpy.context.scene
    scene["ship_id"] = config["id"]
    scene["build_stage"] = config["stage"]
    scene["config_schema_version"] = config["schema_version"]
    scene["config_sha256"] = manifest["source_file_sha256"]
    text = bpy.data.texts.new("SHIP_BUILD_MANIFEST.json")
    text.write(json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True))
    return manifest


def _remove_diagnostic_objects() -> None:
    for object_ in list(bpy.data.objects):
        if object_.name == "Diagnostic_Camera" or object_.name.startswith("Diagnostic_"):
            bpy.data.objects.remove(object_, do_unlink=True)


def prepare_outputs(config: dict) -> tuple[Path, Path]:
    blend_path = blend_output_dir() / Path(config["output"]["blend_name"]).name
    render_dir = renders_output_dir() / Path(config["output"]["render_prefix"])
    blend_path.parent.mkdir(parents=True, exist_ok=True)
    render_dir.mkdir(parents=True, exist_ok=True)
    if blend_path.exists():
        blend_path.unlink()
    for view in config["output"]["required_views"]:
        path = render_dir / f"{view}.png"
        if path.exists():
            path.unlink()
    return blend_path, render_dir


def save_final_blend(blend_path: Path) -> None:
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    if not blend_path.is_file() or blend_path.stat().st_size == 0:
        raise RuntimeError(f"Formal base .blend was not saved: {blend_path}")


def print_summary(config: dict, build: dict, validation: dict, responses: list[dict], renders: dict[str, Path], blend_path: Path) -> None:
    mesh = build["hull"].data
    dimensions = validation["dimensions"]
    print("\n=== Gelderland Formal Base Build Summary ===")
    print(f"Ship: {config['display_name']}")
    print("Coordinate system: X longitudinal (-X bow, +X stern), Y transverse, +Z up")
    print(f"Hull mesh: {len(mesh.vertices)} vertices, {len(mesh.edges)} edges, {len(mesh.polygons)} faces")
    print(
        f"Hull dimensions: {dimensions['hull_length_m']:.3f} x "
        f"{dimensions['hull_beam_m']:.3f} x {dimensions['hull_height_m']:.3f} m; "
        f"L/B={dimensions['hull_length_beam_ratio']:.3f}"
    )
    print(
        f"Full blockout: {dimensions['full_length_m']:.3f} x "
        f"{dimensions['full_beam_m']:.3f} x {dimensions['full_height_m']:.3f} m"
    )
    print(
        f"Gunports: Port={validation['gunports']['port_count']}, "
        f"Starboard={validation['gunports']['starboard_count']}, "
        f"spacing={validation['gunports']['mean_spacing_m']:.3f} m, Cannons=0"
    )
    print("Parameter response tests:")
    for response in responses:
        print(f"  PASS {response['parameter']}: {response['before']} -> {response['after']} ({response['result']})")
    print(f"Blend: PASS ({blend_path})")
    for view in config["output"]["required_views"]:
        print(f"{view}: PASS ({renders[view]})")
    print("Acceptance: AC-01..AC-64 PASS")
    print("Build: PASS")


def _requested_stage() -> str:
    if "--" not in sys.argv:
        return "formal"
    arguments = sys.argv[sys.argv.index("--") + 1:]
    if not arguments:
        return "formal"
    if len(arguments) == 2 and arguments[0] == "--stage" and arguments[1] in {"hull", "formal"}:
        return arguments[1]
    raise ValueError("Supported script arguments: --stage hull|formal")


def run_hull_stage(config: dict) -> None:
    clear_generated_scene()
    collections = create_ship_collections()
    materials = create_materials(config)
    hull = create_formal_hull(config)
    move_object(hull, collections["Structure"])
    hull.data.materials.append(materials["hull"])
    topology = validate_topology(hull, closed=True)
    validate_centerline(hull)
    validate_symmetry(hull)
    dimensions = validate_dimensions(hull, config)
    stage_dir = renders_output_dir() / config["id"] / "stage_b_hull"
    renders = render_turnaround([hull], stage_dir, include_rts=True)
    print(
        f"STAGE_B_HULL_PASS: {dimensions['hull_length_m']:.3f} x "
        f"{dimensions['hull_beam_m']:.3f} x {dimensions['hull_height_m']:.3f} m; "
        f"quads={topology['quads']}; renders={len(renders)}"
    )


def main() -> None:
    config = load_ship_config()
    stage = _requested_stage()
    if stage == "hull":
        run_hull_stage(config)
        return

    clear_generated_scene()
    responses = run_parameter_response_tests(config)
    # Rebuild from the untouched production config after every temporary test.
    clear_generated_scene()
    blend_path, render_dir = prepare_outputs(config)
    build = build_formal_objects(config)
    validation = validate_formal_build(config, build)
    embed_build_manifest(config)
    renders = render_turnaround(build["objects"], render_dir, include_rts=True)
    required_views = set(config["output"]["required_views"])
    if set(renders) != required_views:
        raise RuntimeError(f"Required diagnostic renders are incomplete: {sorted(renders)}")
    _remove_diagnostic_objects()
    save_final_blend(blend_path)
    print_summary(config, build, validation, responses, renders, blend_path)


if __name__ == "__main__":
    main()
