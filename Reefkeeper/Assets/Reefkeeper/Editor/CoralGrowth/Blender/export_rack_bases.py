"""Export the three rack bases (built by bases.py) for Unity.

    blender.exe -b --factory-startup --python export_rack_bases.py -- [BLEND_DIR]

Per base: merge every part into one mesh, decimate to a PICO-friendly budget, UV unwrap,
bake the procedural moss/wear material into one diffuse texture, and export an FBX with
empty objects marking the planting sockets (Socket_Table_xx on cradles / stone tops,
Socket_Hang_xx on the hanging hooks).
"""
import os
import sys

sys.dont_write_bytecode = True

import bpy  # noqa: E402
from mathutils import Vector  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
REEF_ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
MESH_DIR = os.path.join(REEF_ROOT, "Meshes", "CoralGrowth", "RackBases")
TEX_DIR = os.path.join(REEF_ROOT, "Textures", "CoralGrowth", "RackBases")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
BLEND_DIR = argv[0] if argv else r"D:\blender-5.2.3\projects\coral_growth\bases"

# blend name, export name, triangle budget
BASES = [
    ("base_B_tree", "RackBase_Tree", 24000),
    ("base_D_cairn", "RackBase_Cairn", 18000),
    ("base_E_hanging", "RackBase_Hanging", 24000),
]
TEX_SIZE = 2048


def world_verts(ob):
    mw = ob.matrix_world
    return [mw @ v.co for v in ob.data.vertices]


def find_sockets(meshes, blend):
    sockets = []
    for ob in meshes:
        mats = [m.name for m in ob.data.materials if m]
        if ob.name.startswith("Cradle"):
            vs = world_verts(ob)
            top = max(v.z for v in vs)
            c = sum(vs, Vector()) / len(vs)
            sockets.append(("Table", Vector((c.x, c.y, top - 0.006))))
        elif any(m.startswith("Clip") for m in mats):
            sockets.append(("Hang", ob.matrix_world.translation.copy()))
    if blend == "base_D_cairn":  # the highest stone's crown is the table socket
        stones = [o for o in meshes if any(m and m.name.startswith("RelicStone") for m in o.data.materials)]
        top = max(stones, key=lambda o: max(v.z for v in world_verts(o)))
        vs = world_verts(top)
        z = max(v.z for v in vs)
        c = sum(vs, Vector()) / len(vs)
        sockets.append(("Table", Vector((c.x, c.y, z - 0.01))))
    return sockets


def shrink_clump_uvs(ob, factor):
    """Moss clumps are near-solid colour; give them little texture space so stone and wood get
    the resolution. Each clump (a connected piece) is scaled toward its own UV centre."""
    import bmesh
    bm = bmesh.from_edit_mesh(ob.data)
    uv = bm.loops.layers.uv.active
    clump = {i for i, m in enumerate(ob.data.materials) if m and m.name.startswith("MossClump")}
    seen = set()
    for f in bm.faces:
        if f.index in seen or f.material_index not in clump:
            continue
        group, stack = [], [f]
        seen.add(f.index)
        while stack:
            g = stack.pop()
            group.append(g)
            for e in g.edges:
                for n in e.link_faces:
                    if n.index not in seen and n.material_index in clump:
                        seen.add(n.index)
                        stack.append(n)
        loops = [l for g in group for l in g.loops]
        c = sum((l[uv].uv for l in loops), Vector((0, 0))) / len(loops)
        for l in loops:
            l[uv].uv = c + (l[uv].uv - c) * factor
    bmesh.update_edit_mesh(ob.data)


def export(blend, name, budget):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(BLEND_DIR, blend + ".blend"))
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH" and not o.name.startswith("Circle")]
    sockets = find_sockets(meshes, blend)

    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.convert(target="MESH")          # applies subsurf etc.
    bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = ob.data.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    if tris > budget:
        dec = ob.modifiers.new("Decimate", "DECIMATE")
        dec.ratio = budget / tris
        bpy.ops.object.modifier_apply(modifier=dec.name)
    bpy.ops.object.shade_smooth()

    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.002)
    shrink_clump_uvs(ob, 0.2)
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.pack_islands(rotate=True, margin=0.002)
    bpy.ops.object.mode_set(mode="OBJECT")

    img = bpy.data.images.new(name, TEX_SIZE, TEX_SIZE, alpha=False)
    for m in ob.data.materials:
        if not m or not m.use_nodes:
            continue
        n = m.node_tree.nodes.new("ShaderNodeTexImage")
        n.image = img
        m.node_tree.nodes.active = n
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 16
    bake = scene.render.bake
    bake.use_pass_direct = False
    bake.use_pass_indirect = False
    bake.use_pass_color = True
    bpy.ops.object.bake(type="DIFFUSE", margin=8, use_clear=True)
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

    empties = []
    for i, (kind, p) in enumerate(sockets):
        e = bpy.data.objects.new(f"Socket_{kind}_{i:02d}", None)
        e.empty_display_size = 0.04
        e.location = p
        scene.collection.objects.link(e)
        empties.append(e)

    os.makedirs(MESH_DIR, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    for e in empties:
        e.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(MESH_DIR, name + ".fbx"), use_selection=True, object_types={"MESH", "EMPTY"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP", colors_type="NONE")
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, name + "_export.blend"))
    tris = sum(len(p.vertices) - 2 for p in ob.data.polygons)
    print(f"[rackbase] {name}: {tris} tris, {len(ob.data.vertices)} verts, sockets {len(sockets)}")


if __name__ == "__main__":
    for blend, name, budget in BASES:
        export(blend, name, budget)
    print("[rackbase] done")
