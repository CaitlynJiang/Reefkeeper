"""Export the cat merchant shop (built by cat_shop.py) for Unity.

    blender.exe -b --factory-startup --python export_cat_shop.py -- [BLEND]

- Shop: every stall piece -> one mesh, vertex paint baked into one 2048 texture (CatShop.fbx),
  with empties for the basket, the push cart and where the cat stands.
- Cat: the parts stay separate objects (body, head, ears, arms, tail and one Face_* mesh per
  expression) with their pivots and parenting, sharing one 1024 baked atlas (CatMerchant.fbx).
"""
import os
import sys

sys.dont_write_bytecode = True

import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import export_rack_bases as erb  # noqa: E402

REEF_ROOT = erb.REEF_ROOT
MESH_DIR = os.path.join(REEF_ROOT, "Meshes", "CoralGrowth", "CatShop")
TEX_DIR = os.path.join(REEF_ROOT, "Textures", "CoralGrowth", "CatShop")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BLEND = argv[0] if argv else r"D:\blender-5.2.3\projects\coral_growth\cat_shop\cat_shop.blend"
SHOP_BUDGET = 60000


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def select(objs, active=None):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.hide_set(False)
        o.hide_viewport = False
        o.hide_render = False
        o.select_set(True)
    bpy.context.view_layer.objects.active = active or objs[0]


def bake_material(name, size):
    """Material that shows the 'Col' paint, with an image node as the bake target."""
    img = bpy.data.images.new(name, size, size, alpha=False)
    m = bpy.data.materials.new(name + "_bake")
    m.use_nodes = True
    nt = m.node_tree
    a = nt.nodes.new("ShaderNodeAttribute")
    a.attribute_name = "Col"
    nt.links.new(a.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = img
    nt.nodes.active = t
    return m, img


def unwrap_and_bake(objs, name, size, margin=0.003):
    mat, img = bake_material(name, size)
    img.generated_color = (0.5, 0.5, 0.5, 1.0)
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(mat)
    select(objs)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=margin)
    bpy.ops.uv.pack_islands(rotate=True, margin=margin)
    bpy.ops.object.mode_set(mode="OBJECT")
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 4
    b = scene.render.bake
    b.use_pass_direct = b.use_pass_indirect = False
    b.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE", margin=6, use_clear=True)
    os.makedirs(TEX_DIR, exist_ok=True)
    img.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()
    final = bpy.data.materials.new(name)
    final.use_nodes = True
    t = final.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = img
    final.node_tree.links.new(t.outputs["Color"], final.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    for o in objs:
        o.data.materials.clear()
        o.data.materials.append(final)


def export(objs, path):
    select(objs)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", colors_type="NONE")


def main():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    scene = bpy.context.scene
    root = bpy.data.objects["CatMerchant"]
    cat = [o for o in root.children_recursive if o.type == "MESH"]
    shop = [o for o in scene.objects if o.type == "MESH" and o not in cat
            and "Col" in o.data.color_attributes and not o.name.startswith("Circle")]
    empties = [o for o in scene.objects if o.type == "EMPTY" and o.name in ("Socket_Basket", "Socket_Cart", "Cat_Anchor")]

    # --- shop: one mesh
    select(shop)
    bpy.ops.object.convert(target="MESH")
    bpy.ops.object.join()
    shop_ob = bpy.context.view_layer.objects.active
    shop_ob.name = shop_ob.data.name = "CatShop"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if tris(shop_ob) > SHOP_BUDGET:
        dec = shop_ob.modifiers.new("Decimate", "DECIMATE")
        dec.ratio = SHOP_BUDGET / tris(shop_ob)
        bpy.ops.object.modifier_apply(modifier=dec.name)
    unwrap_and_bake([shop_ob], "CatShop", 2048, 0.002)

    # --- cat: parts stay separate, one shared atlas; stand it at the origin
    root.matrix_world = Matrix.Identity(4)
    bpy.context.view_layer.update()
    unwrap_and_bake(cat, "CatMerchant", 1024, 0.004)

    os.makedirs(MESH_DIR, exist_ok=True)
    export([shop_ob] + empties, os.path.join(MESH_DIR, "CatShop.fbx"))
    export([root] + list(root.children_recursive), os.path.join(MESH_DIR, "CatMerchant.fbx"))
    bpy.ops.wm.save_as_mainfile(filepath=BLEND.replace(".blend", "_export.blend"))
    print(f"[catshop] shop {tris(shop_ob)} tris, cat {sum(tris(o) for o in cat)} tris in {len(cat)} parts")
    print("[catshop] export done")


if __name__ == "__main__":
    main()
