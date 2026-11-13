# Blender 3.6+ / 4.x
# Procedural Police Officer character inspired by the supplied RDC police uniform references.
# Run: Blender > Scripting > Open this file > Run Script.
# The script creates a posed, segmented character, a basic armature, and exports Police_Officer_RDC.fbx
# NOTE: This is a clean base mesh / game-character blockout, not a scanned or sculpted photoreal human.

import bpy
import math
from mathutils import Vector

# ---------- scene ----------
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.armatures):
    pass

def mat(name, color, metallic=0.0, rough=0.72):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (*color, 1)
    bs.inputs["Metallic"].default_value = metallic
    bs.inputs["Roughness"].default_value = rough
    return m

navy = mat("Uniform | deep police navy", (0.025, 0.055, 0.14))
navy2 = mat("Uniform | panel blue", (0.035, 0.075, 0.19))
black = mat("Boots and belt | black leather", (0.012, 0.015, 0.022), 0.05)
beretmat = mat("Beret | black wool", (0.012, 0.014, 0.02))
skin = mat("Skin | deep brown", (0.19, 0.075, 0.038))
skin_hi = mat("Skin highlights", (0.27, 0.12, 0.065))
metal = mat("Hardware | dark metal", (0.24, 0.27, 0.31), 0.72, 0.3)
badge = mat("Insignia | muted brass", (0.48, 0.34, 0.12), 0.55, 0.35)
white = mat("Insignia lettering", (0.78, 0.8, 0.78), 0.15, 0.45)
eye = mat("Eyes", (0.025, 0.018, 0.012))
gunmat = mat("Service weapon | matte black", (0.018, 0.022, 0.028), 0.28, 0.4)

parts = []

def uv(name, loc, scale, material, seg=20, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=loc)
    o = bpy.context.object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material: o.data.materials.append(material)
    parts.append(o)
    return o

def cube(name, loc, scale, material, bevel=0.045):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.name = name
    o.dimensions = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material: o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new("Soft uniform edges","BEVEL"); mod.width=bevel; mod.segments=2
        o.modifiers.new("Weighted corner normals","WEIGHTED_NORMAL")
    parts.append(o)
    return o

def cyl_between(name, a, b, radius, material, vertices=16, radius2=None):
    a,b=Vector(a),Vector(b)
    d=b-a
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=radius, radius2=(radius if radius2 is None else radius2),
                                   depth=d.length, location=(a+b)/2)
    o=bpy.context.object; o.name=name
    o.rotation_mode='QUATERNION'; o.rotation_quaternion=d.to_track_quat('Z','Y')
    if material: o.data.materials.append(material)
    parts.append(o)
    return o

def text_obj(name, body, loc, size, material, rotation=(math.pi/2,0,0)):
    cu=bpy.data.curves.new(name,'FONT'); cu.body=body; cu.size=size; cu.extrude=0.0008
    ob=bpy.data.objects.new(name,cu); bpy.context.collection.objects.link(ob)
    ob.location=loc; ob.rotation_euler=rotation
    if material: ob.data.materials.append(material)
    parts.append(ob)
    return ob

# Character faces forward toward -Y. Height approx 1.82m.
# Boots and legs
for side, x in [('L',-0.115),('R',0.115)]:
    cube(f'{side} boot sole',(x,-0.035,0.075),(0.19,0.36,0.075),black,0.025)
    uv(f'{side} boot toe',(x,-0.105,0.16),(0.095,0.17,0.09),black)
    uv(f'{side} boot shaft',(x,0.015,0.29),(0.085,0.105,0.18),black)
    cyl_between(f'{side} lower trouser',(x,0.015,0.43),(x,0.015,0.86),0.085,navy,20,0.115)
    uv(f'{side} knee',(x,0.0,0.88),(0.105,0.105,0.12),navy2)
    cyl_between(f'{side} upper trouser',(x,0.015,0.91),(x*0.92,0.0,1.18),0.115,navy,20,0.15)
    # cargo pocket on outer thigh
    cube(f'{side} cargo pocket',(x*1.75,-0.105,0.79),(0.105,0.055,0.17),navy2,0.018)
    cube(f'{side} cargo flap',(x*1.75,-0.14,0.87),(0.11,0.025,0.045),navy,0.01)

# Pelvis / jacket torso
uv("Pelvis",(0,0.015,1.19),(0.205,0.135,0.17),navy)
uv("Jacket torso",(0,0.015,1.48),(0.255,0.145,0.34),navy)
# front jacket placket and collar
cube("Central jacket placket",(0,-0.135,1.49),(0.045,0.025,0.34),navy2,0.008)
for z in [1.35,1.43,1.51,1.59,1.67]:
    uv("Jacket button",(0,-0.153,z),(0.012,0.008,0.012),metal,12,8)
# shoulders
for x in [-0.265,0.265]:
    uv("Shoulder",(x,0.0,1.69),(0.12,0.145,0.12),navy)
    cube("Shoulder epaulette",(x,-0.005,1.775),(0.17,0.095,0.035),navy2,0.012)
    uv("Epaulette button",(x,-0.06,1.78),(0.012,0.008,0.012),metal,12,8)
# arms: right arm bent to hold weapon in front; left relaxed
# upper arms and forearms as tapered segments
cyl_between("Left upper sleeve",(-0.29,0.0,1.68),(-0.36,-0.005,1.39),0.095,navy,20,0.075)
uv("Left elbow",(-0.36,-0.005,1.39),(0.075,0.08,0.08),navy)
cyl_between("Left forearm sleeve",(-0.36,-0.005,1.39),(-0.39,-0.025,1.13),0.073,navy,20,0.055)
uv("Left hand",(-0.39,-0.045,1.08),(0.052,0.045,0.085),skin)
cyl_between("Right upper sleeve",(0.29,0.0,1.68),(0.39,-0.015,1.48),0.095,navy,20,0.075)
uv("Right elbow",(0.39,-0.015,1.48),(0.078,0.08,0.08),navy)
cyl_between("Right forearm sleeve",(0.39,-0.015,1.48),(0.30,-0.22,1.36),0.073,navy,20,0.055)
uv("Right hand gripping",(0.27,-0.255,1.34),(0.06,0.052,0.075),skin)

# Neck and head
cyl_between("Neck",(0,0.015,1.76),(0,0.015,1.88),0.075,skin,20,0.075)
uv("Head",(0,-0.005,1.99),(0.135,0.125,0.17),skin,32,20)
uv("Jaw",(0,-0.025,1.91),(0.112,0.11,0.09),skin)
uv("Nose",(0,-0.132,1.985),(0.027,0.035,0.05),skin_hi)
for x in [-0.052,0.052]:
    uv("Ear",(x,-0.005,1.99),(0.027,0.025,0.045),skin)
    uv("Eye",(x,-0.121,2.025),(0.018,0.009,0.012),eye,12,8)
    uv("Eyebrow",(x,-0.125,2.047),(0.03,0.012,0.009),skin_hi,12,8)
uv("Upper lip",(0,-0.13,1.94),(0.035,0.012,0.009),skin_hi)
uv("Chin",(0,-0.105,1.895),(0.045,0.02,0.018),skin_hi)

# Beret: flattened wool dome with tilted crown and badge
uv("Black beret crown",(0,0.0,2.155),(0.16,0.145,0.065),beretmat,32,16)
uv("Beret band",(0,-0.005,2.125),(0.15,0.14,0.025),black,32,12)
uv("Beret police badge",(0,-0.143,2.145),(0.026,0.008,0.032),badge,16,10)
uv("Badge center",(0,-0.152,2.145),(0.009,0.005,0.012),white,12,8)

# Shirt pockets, flaps, buttons, chest labels and shoulder patches
for x in [-0.13,0.13]:
    cube("Chest pocket",(x,-0.145,1.53),(0.105,0.035,0.105),navy2,0.012)
    cube("Chest pocket flap",(x,-0.17,1.59),(0.112,0.022,0.035),navy,0.008)
    uv("Pocket button",(x,-0.185,1.585),(0.009,0.006,0.009),metal,10,8)
cube("POLICE chest patch",(0.13,-0.17,1.665),(0.105,0.018,0.035),black,0.005)
text_obj("POLICE lettering","POLICE",(0.083,-0.181,1.657),0.018,white)
# sleeve patches
for x in [-0.365,0.365]:
    cube("Sleeve insignia",(x,-0.082,1.52),(0.055,0.018,0.075),badge,0.01)

# Belt, buckle, holster, radio
cube("Duty belt",(0,0.0,1.255),(0.46,0.31,0.065),black,0.025)
cube("Belt buckle",(0,-0.165,1.255),(0.075,0.025,0.045),metal,0.006)
cube("Radio pouch",(-0.19,-0.17,1.27),(0.075,0.065,0.11),black,0.012)
cube("Holster",(0.22,-0.14,1.22),(0.09,0.075,0.15),black,0.012)

# Service firearm prop held in the right hand, angled slightly upward.
# Non-functional visual model only.
cube("Pistol slide",(0.25,-0.36,1.405),(0.19,0.045,0.045),gunmat,0.012)
cube("Pistol frame",(0.25,-0.355,1.37),(0.15,0.04,0.035),gunmat,0.01)
cyl_between("Pistol grip",(0.29,-0.35,1.37),(0.31,-0.30,1.27),0.026,gunmat,12,0.035)
cube("Pistol muzzle",(0.145,-0.36,1.405),(0.025,0.035,0.035),black,0.004)
uv("Trigger guard",(0.255,-0.385,1.355),(0.035,0.008,0.022),metal,16,8)

# Armature with a basic humanoid skeleton. Segmented objects are parented to root for stable FBX import.
arm_data=bpy.data.armatures.new("PoliceOfficer_Rig")
rig=bpy.data.objects.new("PoliceOfficer_Rig",arm_data)
bpy.context.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig
rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
bones=[
("root",(0,0,0),(0,0,1.0),None),
("pelvis",(0,0,1.0),(0,0,1.25),"root"),
("spine",(0,0,1.25),(0,0,1.62),"pelvis"),
("neck",(0,0,1.62),(0,0,1.86),"spine"),
("head",(0,0,1.86),(0,0,2.12),"neck"),
("upper_arm.L",(-0.25,0,1.68),(-0.36,0,1.39),"spine"),
("forearm.L",(-0.36,0,1.39),(-0.39,0,1.13),"upper_arm.L"),
("upper_arm.R",(0.25,0,1.68),(0.39,-0.015,1.48),"spine"),
("forearm.R",(0.39,-0.015,1.48),(0.27,-0.25,1.34),"upper_arm.R"),
("thigh.L",(-0.11,0,1.2),(-0.11,0,0.88),"pelvis"),
("shin.L",(-0.11,0,0.88),(-0.11,0,0.2),"thigh.L"),
("foot.L",(-0.11,0,0.2),(-0.11,-0.16,0.08),"shin.L"),
("thigh.R",(0.11,0,1.2),(0.11,0,0.88),"pelvis"),
("shin.R",(0.11,0,0.88),(0.11,0,0.2),"thigh.R"),
("foot.R",(0.11,0,0.2),(0.11,-0.16,0.08),"shin.R")]
edit={}
for name,head,tail,parent in bones:
    b=arm_data.edit_bones.new(name); b.head=head; b.tail=tail; edit[name]=b
    if parent: b.parent=edit[parent]
bpy.ops.object.mode_set(mode='OBJECT')

# Parent all visible parts to the rig's root bone (rigid segmented-character setup).
for ob in parts:
    ob.parent=rig
    ob.parent_type='BONE'
    ob.parent_bone='root'

# Set origin / scene and export selection
bpy.ops.object.select_all(action='DESELECT')
for ob in parts: ob.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active=rig

# FBX export beside this .blend / script file; change path if desired.
output_path = bpy.path.abspath("//Police_Officer_RDC.fbx")
bpy.ops.export_scene.fbx(
    filepath=output_path,
    use_selection=True,
    object_types={'MESH','ARMATURE','EMPTY'},
    apply_scale_options='FBX_SCALE_ALL',
    add_leaf_bones=False,
    bake_anim=False,
    path_mode='AUTO'
)
print("FBX exported to:", output_path)
