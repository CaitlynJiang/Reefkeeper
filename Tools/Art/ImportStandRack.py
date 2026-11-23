"""Extract the exact MC_5_StandRack GLB without its three placeholder sleeves.
Run with Blender --background --python Tools/Art/ImportStandRack.py.
The original GLB and an editable Blend with non-destructive simplification stay in ArtSource.
"""
import bpy, json, shutil
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[2]
source=Path('C:/Users/anlan/Downloads/music_corner/glb/MC_5_StandRack.glb')
art=root/'ArtSource/MusicCorner';art.mkdir(parents=True,exist_ok=True)
copy=art/source.name
if not copy.exists():shutil.copy2(source,copy)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(copy))
rack=bpy.data.objects['MC_5_StandRack'];before=after=0
for i,label in enumerate(['Bottom','Middle','Top'],1):
    sleeve=bpy.data.objects[f'Sleeve_Rack{i}']
    center=sum((Vector(v) for v in sleeve.bound_box),Vector())/8
    slot=bpy.data.objects.new('Slot_'+label,None);bpy.context.collection.objects.link(slot)
    slot.parent=rack;slot.matrix_world=sleeve.matrix_world.copy();slot.location+=sleeve.matrix_local.to_3x3()@center
    bpy.data.objects.remove(sleeve,do_unlink=True)
for obj in list(rack.children_recursive):
    if obj.type!='MESH':continue
    obj.data.calc_loop_triangles();n=len(obj.data.loop_triangles);before+=n
    if n>1000:
        modifier=obj.modifiers.new('Mobile detail reduction (editable)','DECIMATE')
        modifier.ratio=max(300/n,.10);modifier.use_collapse_triangulate=True
    mesh=obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh();mesh.calc_loop_triangles();after+=len(mesh.loop_triangles)
    obj.evaluated_get(bpy.context.evaluated_depsgraph_get()).to_mesh_clear()
textures=root/'Reefkeeper/Assets/Reefkeeper/Textures/MusicCorner/StandRack';textures.mkdir(parents=True,exist_ok=True)
material_info={}
used={m for o in rack.children_recursive if o.type=='MESH' for m in o.data.materials if m}
for material in used:
    bsdf=next((n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
    image=next((n.image for n in material.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None)
    image_path=None
    if image:
        image_path=textures/(material.name+'.png');image.filepath_raw=str(image_path);image.file_format='PNG';image.save()
    material_info[material.name]={'color':list(bsdf.inputs['Base Color'].default_value) if bsdf else list(material.diffuse_color),
        'texture':str(image_path.relative_to(root/'Reefkeeper')).replace('\\','/') if image_path else None}
(art/'stand-rack-materials.json').write_text(json.dumps(material_info,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(art/'MC_5_StandRack_Editable.blend'))
for o in bpy.context.scene.objects:o.select_set(o==rack or o in rack.children_recursive)
bpy.context.view_layer.objects.active=rack
out=root/'Reefkeeper/Assets/Reefkeeper/Meshes/MusicCorner/MC_5_StandRack.fbx'
bpy.ops.export_scene.fbx(filepath=str(out),use_selection=True,object_types={'EMPTY','MESH'},use_mesh_modifiers=True,
    mesh_smooth_type='FACE',use_tspace=False,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',global_scale=1,
    axis_forward='-Z',axis_up='Y',bake_space_transform=False,add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
print('STAND_RACK_EXPORT',json.dumps(dict(triangles_before=before,triangles_after=after,materials=material_info,fbx=str(out))))
