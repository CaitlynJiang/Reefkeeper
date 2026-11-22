"""Export D:/blender-5.2.3/projects/photo_board/photo_board.blend to Unity (read-only on the .blend).
blender.exe -b D:/blender-5.2.3/projects/photo_board/photo_board.blend --python export_photo_board.py
Writes Reefkeeper/Assets/Reefkeeper/Meshes/PhotoBoard/PhotoBoard.fbx: everything under Board_Rig,
modifiers applied (the Solidify outline shells keep their "Outline" material slot), metres.
Z-up as usual; the Unity importer bakes the axis conversion (bakeAxisConversion), which also
handles the nested photo quads (baking it in the exporter skipped children of Pb_Photos).
Unity side: Reefkeeper > Photos > Build photo board (PhotoBoardBuilder.cs).
"""
import os
import bpy

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Reefkeeper", "Assets", "Reefkeeper",
                   "Meshes", "PhotoBoard", "PhotoBoard.fbx")
OUT = os.path.normpath(OUT)

rig = bpy.data.objects["Board_Rig"]
keep = {rig} | set(rig.children_recursive)
for o in bpy.context.scene.objects:
    o.hide_set(False)
    o.select_set(o in keep)
bpy.context.view_layer.objects.active = rig
bpy.ops.export_scene.fbx(
    filepath=OUT, use_selection=True, object_types={"EMPTY", "MESH"},
    use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", global_scale=1.0,
    axis_forward="-Z", axis_up="Y", bake_space_transform=False,
    add_leaf_bones=False, bake_anim=False, path_mode="STRIP", embed_textures=False)
print("EXPORTED", OUT)
