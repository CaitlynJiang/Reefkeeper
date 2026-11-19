"""Original first-person seated diver blockout. Run with Blender --background --python.

No external mesh or texture dependency. Blender source stays outside Unity Assets.
Style reference: nolanfa's Diver (see README); geometry is authored here from rings.
"""
import bpy
import bmesh
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Reefkeeper/Assets/Reefkeeper/Art/OpeningDiver'
SOURCE = ROOT / 'ArtSource/OpeningDiver'
REVIEW = ROOT / 'artifacts/opening-diver'
for path in (OUT, SOURCE, REVIEW):
    path.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)

# A single tiny palette, all surfaces opaque. Values are display-space RGB.
COLORS = ['173951', '245778', '347896', '59afbd', '93cdd1', 'f5cd67', '102c3a', 'e4e6cc']
palette = bpy.data.images.new('DiverPalette', width=32, height=4)
pixels = []
for row in range(4):
    for x in range(32):
        h = COLORS[x // 4]
        pixels.extend([int(h[i:i+2], 16)/255 for i in (0, 2, 4)] + [1])
palette.pixels = pixels
palette.filepath_raw = str(OUT / 'DiverPalette.png')
palette.file_format = 'PNG'
palette.save()
mat = bpy.data.materials.new('OpeningDiver_Palette')
mat.use_nodes = True
bsdf = mat.node_tree.nodes.get('Principled BSDF')
bsdf.inputs['Roughness'].default_value = .78
tex = mat.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = palette
tex.interpolation = 'Closest'
mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])

verts, faces, colors, weights = [], [], [], []

def vertex(co, weight):
    verts.append(co)
    weights.append(weight)
    return len(verts)-1

def face(ids, color):
    faces.append(ids)
    colors.append(color)

def loft(rings, count, color, axis='Z', cap=True):
    """Rings: (center, radii in the two perpendicular axes, vertex bone weights)."""
    ids = []
    for center, rx, ry, weight in rings:
        ring = []
        for i in range(count):
            a = 2*math.pi*i/count
            c = list(center)
            if axis == 'Z':
                c[0] += math.cos(a)*rx
                c[1] += math.sin(a)*ry
            else:
                c[0] += math.cos(a)*rx
                c[2] += math.sin(a)*ry
            ring.append(vertex(c, weight))
        ids.append(ring)
    for k in range(len(ids)-1):
        for j in range(count):
            shade = color(k, j) if callable(color) else color
            face([ids[k][j], ids[k][(j+1)%count], ids[k+1][(j+1)%count], ids[k+1][j]], shade)
    if cap:
        face(ids[0][::-1], color(0, 0) if callable(color) else color)
        face(ids[-1], color(len(ids)-2, 0) if callable(color) else color)

# Rest pose is standing, facing -Y. Hips at .925m; pelvis origin is preserved in FBX.
hip, knee, ankle = .925, .515, .115
loft([((0, 0, z), rx, ry, {'Hips': 1}) for z, rx, ry in [
    (.85, .17, .115), (.91, .225, .135), (1.02, .195, .12),
    (1.12, .185, .115)]], 12, lambda k,j: 1 if j >= 6 else 0)
# Narrow waist band. No metallic buckle or zipper teeth.
loft([((0, 0, z), .198, .123, {'Hips': 1}) for z in (1.015, 1.045)], 12, 6)
loft([((0, -.126, z), .024, .006, {'Hips': 1}) for z in (1.048, 1.115)], 4, 5)

for side, x in [('L', .14), ('R', -.14)]:
    thigh, shin, foot, fin = [n + '.' + side for n in ('Thigh', 'Shin', 'Foot', 'Fin')]
    rings = []
    for z, rx, ry, blend in [
        (.95,.108,.110,0), (.86,.115,.112,0), (.73,.105,.105,0),
        (.59,.091,.092,.1), (.515,.090,.090,.5), (.455,.086,.088,.9),
        (.34,.088,.085,1), (.20,.066,.063,1), (.115,.055,.060,1)]:
        w = {thigh: 1-blend, shin: blend}
        w = {key: value for key, value in w.items() if value > 0}
        rings.append(((x, 0, z), rx, ry, w))
    loft(rings, 10, lambda k,j: 2 if j in (6,7) else (1 if j in (5,8) else 0))
    # The knee panel is colour only: no floating armour / extra layered geometry.
    # Boot / foot pocket, chunky but tapered. Closed geometry has a real sole.
    loft([((x,y,z),w,h,{foot:1}) for y,z,w,h in [
        (.055,.09,.047,.045),(-.015,.085,.065,.065),
        (-.13,.065,.071,.047),(-.20,.055,.058,.025)]],8,6,axis='Y')
    # Broad swept blades, with rounded low-poly tips and contrasting printed tips.
    loft([((x,y,z),w,h,{foot:1-t,fin:t}) for y,z,w,h,t in [
        (-.14,.035,.058,.016,0),(-.23,.034,.086,.018,.35),
        (-.39,.029,.115,.015,1),(-.56,.022,.122,.012,1),
        (-.635,.018,.095,.009,1),(-.665,.017,.052,.006,1)]],
        8,lambda k,j: (4 if k>=4 else (3 if j<4 else 2)),axis='Y')
    # Two small raised ribs per blade, low-poly tubes sharing the same material.
    for sign in (-1,1):
        loft([((x+sign*w,y,z),.007,.005,{fin:1}) for y,z,w in [
            (-.25,.052,.055),(-.40,.045,.080),(-.56,.036,.087)]],4,2,axis='Y')
    # Short yellow ankle tab breaks up the blue without an extra material.
    loft([((x, y, .145), .029, .006, {foot:1}) for y in (-.047,-.088)],4,5,axis='Y')

mesh = bpy.data.meshes.new('OpeningDiver_LowPoly')
mesh.from_pydata(verts, [], faces)
mesh.update()
uv = mesh.uv_layers.new(name='PaletteUV')
for poly, color in zip(mesh.polygons, colors):
    for loop in poly.loop_indices:
        uv.data[loop].uv = ((color+.5)/8, .5)
bm = bmesh.new()
bm.from_mesh(mesh)
bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
bm.to_mesh(mesh)
bm.free()
body = bpy.data.objects.new('OpeningDiver_Body', mesh)
bpy.context.collection.objects.link(body)
body.data.materials.append(mat)
body['purpose'] = 'First person seated hips, legs and flippers. No head, arms or hands.'
for poly in mesh.polygons:
    poly.use_smooth = False

arm = bpy.data.armatures.new('OpeningDiver_Skeleton')
rig = bpy.data.objects.new('OpeningDiver_Rig', arm)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active = rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name, head, tail, parent=None):
    b = arm.edit_bones.new(name)
    b.head, b.tail = head, tail
    if parent: b.parent = arm.edit_bones[parent]
    return b
bone('Hips',(0,0,hip),(0,0,1.1))
for side,x in [('L',.14),('R',-.14)]:
    bone('Thigh.'+side,(x,0,hip),(x,0,knee),'Hips')
    bone('Shin.'+side,(x,0,knee),(x,0,ankle),'Thigh.'+side)
    bone('Foot.'+side,(x,0,ankle),(x,-.20,.055),'Shin.'+side)
    bone('Fin.'+side,(x,-.20,.055),(x,-.60,.022),'Foot.'+side)
bpy.ops.object.mode_set(mode='OBJECT')
for b in arm.bones:
    group = body.vertex_groups.new(name=b.name)
    for i,w in enumerate(weights):
        if w.get(b.name,0)>0:
            group.add([i],w[b.name],'REPLACE')
body.parent = rig
modifier=body.modifiers.new('Nine bone deformation','ARMATURE')
modifier.object=rig

# Pose in world coordinates, then convert to the bone's local rest axes.
# Preserve camera tracking: animation is confined to the legs / feet.
from mathutils import Matrix
def set_pose(name, angle):
    p = rig.pose.bones[name]
    p.rotation_mode = 'QUATERNION'
    basis = p.bone.matrix_local.to_3x3()
    p.rotation_quaternion = (basis.inverted() @ Matrix.Rotation(math.radians(angle),3,'X') @ basis).to_quaternion()

scene=bpy.context.scene
scene.render.fps=30
scene.frame_start, scene.frame_end=1,121
for frame in range(1,122,5):
    phase=(frame-1)/120*math.tau
    for side,offset in [('L',0),('R',math.pi*.8)]:
        set_pose('Thigh.'+side,-80)
        set_pose('Shin.'+side,80+math.sin(phase+offset)*3)
        set_pose('Foot.'+side,math.sin(phase+offset+.5)*1.8)
        set_pose('Fin.'+side,math.sin(phase+offset+1)*1.2)
        for part in ('Thigh','Shin','Foot','Fin'):
            rig.pose.bones[part+'.'+side].keyframe_insert('rotation_quaternion',frame=frame,group=part+'.'+side)
rig.animation_data.action.name='SeatedIdle'
scene.frame_set(1)
bpy.context.view_layer.update()
mesh.calc_loop_triangles()
tris=len(mesh.loop_triangles)
assert tris<=2500, tris
assert len(body.data.materials)==1
assert len(arm.bones)==9
assert all(abs(sum(w.values())-1)<.0001 for w in weights)
# Bind-pose geometry and authored four-second animation are both retained.
bpy.ops.object.select_all(action='DESELECT')
body.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(OUT/'OpeningDiver.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',
    add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,
    bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
    path_mode='RELATIVE',use_mesh_modifiers=True)
palette.filepath='//../../Reefkeeper/Assets/Reefkeeper/Art/OpeningDiver/DiverPalette.png'
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'OpeningDiver.blend'))

# Review scene is not exported. Approximate the existing platform dimensions.
def solid(name,color,roughness=.8):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    n=m.node_tree.nodes.get('Principled BSDF')
    n.inputs['Base Color'].default_value=(*color,1)
    n.inputs['Roughness'].default_value=roughness
    return m
wood=solid('Review wood',(.42,.25,.12))
lightwood=solid('Review seat',(.62,.40,.20))
sea=solid('Review sea',(.065,.27,.32))
cream=solid('Review manual',(.85,.85,.66))
def cube(name,loc,scale,material):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=bpy.context.object
    o.name=name
    o.scale=scale
    o.data.materials.append(material)
    return o
review=[]
for i in range(9):
    review.append(cube('Review plank',((i-4)*.39,1.07,.56),(.375,2.7,.16),wood))
review.append(cube('Review edge',(0,-.30,.55),(3.64,.16,.25),wood))
review.append(cube('Review seat',(0,0,.76),(2.6,.42,.15),lightwood))
review.append(cube('Review manual',(-.62,0,.855),(.32,.42,.02),cream))
review.append(cube('Review water',(0,0,-.04),(200,200,.05),sea))
world=bpy.data.worlds.new('Soft studio sky')
world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.42,.60,.67,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.7
scene.world=world
def area(name,loc,power,size):
    d=bpy.data.lights.new(name,'AREA')
    d.energy=power
    d.shape='DISK'
    d.size=size
    o=bpy.data.objects.new(name,d)
    scene.collection.objects.link(o)
    o.location=loc
    o.rotation_euler=(Vector((0,0,.7))-o.location).to_track_quat('-Z','Y').to_euler()
area('Review soft sun',(-3,-2,5),450,4)
area('Review fill',(3,1,3),180,3)
camdata=bpy.data.cameras.new('ReviewCamera')
camera=bpy.data.objects.new('ReviewCamera',camdata)
scene.collection.objects.link(camera)
scene.camera=camera
scene.render.engine='CYCLES'
scene.cycles.samples=24
scene.cycles.use_denoising=True
scene.render.resolution_x=1280
scene.render.resolution_y=900
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
def render(name,pos,target,lens=40):
    camera.location=pos
    camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.lens=lens
    scene.render.filepath=str(REVIEW/(name+'.png'))
    bpy.ops.render.render(write_still=True)
render('first-person',(0,-.13,1.57),(0,-.75,.48),28)
render('seated-three-quarter',(1.8,-2.4,2.2),(0,-.26,.72),52)
for obj in review: obj.hide_render=True
scene.render.film_transparent=True
render('body-transparent',(1.8,-2.4,2.2),(0,-.26,.72),60)
report={'triangles':tris,'vertices':len(verts),'bones':len(arm.bones),'mesh_renderers':1,
    'materials':1,'palette':'32 x 4 RGBA, opaque material','animation':'SeatedIdle: 4 s at 30 fps',
    'scope':'waist / hips / legs / boots / flippers; no head, hands or arms',
    'not_yet_validated':'Unity import, headset fit, Unity material, real-device performance'}
(REVIEW/'metrics.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('OPENING_DIVER_RESULT '+json.dumps(report))
