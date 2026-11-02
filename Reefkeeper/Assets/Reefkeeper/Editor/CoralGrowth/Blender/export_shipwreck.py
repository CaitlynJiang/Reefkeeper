"""Export the shipwreck (built by shipwreck.py) for Unity.

    blender.exe -b --factory-startup --python export_shipwreck.py -- [BLEND]

- Hull, frames, mast, rigging, barrels and planks -> one mesh, decimated, procedural moss/wear
  baked into one 2048 texture.
- Sail strips -> a separate mesh with UV2 = (sway weight, phase) for the 'Lit Sway' shader.
  The Blender Geometry Nodes sway is removed first so the strips export at rest.
- Planting sockets -> empties (Socket_Table_xx).
"""
import os
import sys

sys.dont_write_bytecode = True

import bpy  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import export_rack_bases as erb  # noqa: E402  (reuse merge/UV/bake helpers)

REEF_ROOT = erb.REEF_ROOT
MESH_DIR = os.path.join(REEF_ROOT, "Meshes", "CoralGrowth", "Shipwreck")
TEX_DIR = os.path.join(REEF_ROOT, "Textures", "CoralGrowth", "Shipwreck")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BLEND = argv[0] if argv else r"D:\blender-5.2.3\projects\coral_growth\shipwreck\shipwreck.blend"
HULL_BUDGET = 45000      # triangles for the whole wreck
TEX_HULL, TEX_SAIL = 2048, 512


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.convert(target="MESH")        # applies bevel / subsurf / skin results
    if len(objs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return ob


def unwrap(ob, shrink_clumps):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.002)
    if shrink_clumps:
        erb.shrink_clump_uvs(ob, 0.08)          # clumps are near-solid moss: tiny texture share
        bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.pack_islands(rotate=True, margin=0.0008)
    bpy.ops.object.mode_set(mode="OBJECT")


def bake(ob, name, size):
    scene = bpy.context.scene
    img = bpy.data.images.new(name, size, size, alpha=False)
    # tiny islands (moss clumps) blur with their surroundings in the mips: make those moss, not black
    img.generated_color = (0.1, 0.15, 0.06, 1.0)
    for m in ob.data.materials:
        if m and m.use_nodes:
            n = m.node_tree.nodes.new("ShaderNodeTexImage")
            n.image = img
            m.node_tree.nodes.active = n
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    b = scene.render.bake
    b.use_pass_direct = b.use_pass_indirect = False
    b.use_pass_color = True
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.bake(type="DIFFUSE", margin=8, use_clear=False)
    os.makedirs(TEX_DIR, exist_ok=True)
    img.filepath_raw = os.path.join(TEX_DIR, name + ".png")
    img.file_format = "PNG"
    img.save()
    final = bpy.data.materials.new(name)
    final.use_nodes = True
    t = final.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = img
    final.node_tree.links.new(t.outputs["Color"], final.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    ob.data.materials.clear()
    ob.data.materials.append(final)


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def main():
    bpy.ops.wm.open_mainfile(filepath=BLEND)
    scene = bpy.context.scene
    sockets = [(o.name, o.matrix_world.translation.copy()) for o in scene.objects
               if o.type == "EMPTY" and o.name.startswith("Socket_")]

    sail_src = bpy.data.objects["Wreck_Sail"]
    for m in list(sail_src.modifiers):           # the Blender-side sway: export at rest
        if m.type == "NODES":
            sail_src.modifiers.remove(m)
    hull_parts = [o for o in scene.objects if o.type == "MESH" and o is not sail_src
                  and not o.name.startswith("Circle") and not o.name.startswith("SandDrift")]

    hull = join(hull_parts, "Shipwreck")
    if tris(hull) > HULL_BUDGET:
        dec = hull.modifiers.new("Decimate", "DECIMATE")
        dec.ratio = HULL_BUDGET / tris(hull)
        bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.shade_smooth()
    unwrap(hull, shrink_clumps=True)
    bake(hull, "Shipwreck", TEX_HULL)

    sail = join([sail_src], "Shipwreck_Sail")
    unwrap(sail, shrink_clumps=False)
    bake(sail, "Shipwreck_Sail", TEX_SAIL)
    me = sail.data                                   # UV2 for the sway shader
    h, p = me.attributes["sway_h"].data, me.attributes["sway_p"].data
    uv = me.uv_layers.new(name="Sway")
    for loop in me.loops:
        uv.data[loop.index].uv = (h[loop.vertex_index].value, p[loop.vertex_index].value)
    me.uv_layers.active_index = 0

    for o in [o for o in scene.objects if o.type == "EMPTY"]:
        bpy.data.objects.remove(o)
    empties = []
    for name, pos in sockets:
        e = bpy.data.objects.new(name, None)
        e.location = pos
        scene.collection.objects.link(e)
        empties.append(e)

    os.makedirs(MESH_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in [hull, sail] + empties:
        o.select_set(True)
    bpy.context.view_layer.objects.active = hull
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MESH_DIR, "Shipwreck.fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", colors_type="NONE")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND.replace(".blend", "_export.blend"))
    print(f"[wreck] hull {tris(hull)} tris / {len(hull.data.vertices)} verts, sail {tris(sail)} tris, sockets {len(sockets)}")
    print("[wreck] done")


if __name__ == "__main__":
    main()
