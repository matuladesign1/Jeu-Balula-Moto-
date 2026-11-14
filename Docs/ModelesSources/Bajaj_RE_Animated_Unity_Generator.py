# Bajaj RE - animated Unity FBX generator
# Run in Blender 3.6+ / 4.x: Scripting > Open > Run Script.
# Builds an approximate, editable reference-based model and exports FBX.
# IMPORTANT: dimensions and unseen areas are approximations because only front/rear photos were supplied.

import bpy, math
from mathutils import Vector
from math import radians

# ---------- RESET ----------
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials, bpy.data.cameras, bpy.data.lights):
    pass

# ---------- MATERIALS ----------
def mat(name, color, metallic=0.0, rough=0.45):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    bs = m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value = (*color, 1)
    bs.inputs["Metallic"].default_value = metallic
    bs.inputs["Roughness"].default_value = rough
    return m

BLUE = mat("Body_Blue_Reference", (0.015, 0.22, 0.62), 0.28, 0.27)
DARK = mat("Bumper_Black_Grey", (0.035, 0.04, 0.05), 0.12, 0.38)
CANVAS = mat("Canopy_Off_White_Beige", (0.76, 0.73, 0.66), 0.0, 0.8)
GLASS = mat("Window_Glass_Tinted", (0.12, 0.19, 0.22), 0.25, 0.16)
RUBBER = mat("Tyre_Rubber", (0.012, 0.014, 0.016), 0.0, 0.92)
METAL = mat("Metal_Silver", (0.42, 0.45, 0.48), 0.78, 0.25)
RED = mat("Tail_Lamp_Red", (0.55, 0.012, 0.018), 0.05, 0.22)
AMBER = mat("Indicator_Amber", (1.0, 0.24, 0.015), 0.05, 0.25)
WHITE = mat("Headlamp_White", (0.82, 0.85, 0.82), 0.1, 0.18)
YELLOW = mat("Plate_Yellow", (1.0, 0.65, 0.015), 0.0, 0.4)
SEAT = mat("Seat_Light_Grey", (0.58, 0.57, 0.53), 0.0, 0.8)

# ---------- HELPERS ----------
def cube(name, loc, scale, material, bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o=bpy.context.object; o.name=name; o.dimensions=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material: o.data.materials.append(material)
    if bevel:
        mod=o.modifiers.new("Soft_edges","BEVEL"); mod.width=bevel; mod.segments=3
        o.modifiers.new("Weighted_normals","WEIGHTED_NORMAL")
    return o

def uv(name, loc, scale, material):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=16, location=loc)
    o=bpy.context.object; o.name=name; o.scale=scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if material: o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o

def cyl(name, loc, radius, depth, material, rot=(0,0,0), vertices=48):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth, location=loc, rotation=rot)
    o=bpy.context.object; o.name=name
    if material: o.data.materials.append(material)
    bevel=o.modifiers.new("Edge_softening","BEVEL"); bevel.width=0.018; bevel.segments=2
    o.modifiers.new("Weighted_normals","WEIGHTED_NORMAL")
    return o

def parent_keep(obj, parent):
    obj.parent=parent
    return obj

def rod(name, a, b, radius, material):
    mid=(Vector(a)+Vector(b))/2
    direction=Vector(b)-Vector(a)
    o=cyl(name, mid, radius, direction.length, material)
    o.rotation_mode='QUATERNION'
    o.rotation_quaternion=direction.to_track_quat('Z','Y')
    return o

# ---------- MAIN CHASSIS ----------
chassis=cube("Chassis_Main", (0,0,0.82), (1.18,2.15,0.30), DARK, 0.12)
floor=cube("Passenger_Floor", (0,0.10,1.02), (1.20,1.48,0.10), DARK, 0.06)

# Coordinate convention: X = width, Y = length; front is negative Y.
# Blue lower body shell / front nose
front=cube("Front_Body_Blue", (0,-0.82,1.00), (1.22,0.74,0.90), BLUE, 0.22)
rear=cube("Rear_Body_Blue", (0,0.67,1.02), (1.32,0.94,0.88), BLUE, 0.16)
nose=uv("Front_Nose_Rounded", (0,-1.03,0.83), (0.60,0.40,0.50), BLUE)

# Rear service panel and grille
panel=cube("Rear_Engine_Panel", (0,1.155,0.99), (0.92,0.045,0.72), BLUE, 0.09)
grille=cube("Rear_Grille", (0,1.184,1.08), (0.66,0.025,0.34), DARK, 0.06)
for z in [0.93,1.00,1.07,1.14,1.21]:
    cube("Grille_Slat", (0,1.202,z), (0.60,0.012,0.012), METAL, 0.004)

# Front fascia and bumper
fascia=cube("Front_Fascia", (0,-1.245,0.79), (1.03,0.12,0.43), DARK, 0.13)
bumper=cube("Front_Bumper", (0,-1.32,0.59), (1.10,0.12,0.13), DARK, 0.05)
rear_bumper=cube("Rear_Bumper", (0,1.24,0.55), (1.18,0.13,0.13), BLUE, 0.05)

# Front lights (discs facing forward)
for x in [-0.36,0.36]:
    cyl("Headlamp_Rim", (x,-1.315,0.79), 0.145, 0.045, DARK, (radians(90),0,0))
    cyl("Headlamp_Lens", (x,-1.343,0.79), 0.112, 0.025, WHITE, (radians(90),0,0))
for x in [-0.46,0.46]:
    cube("Front_Indicator", (x,-1.31,1.12), (0.19,0.055,0.10), AMBER, 0.035)

# Rear tail lamps
for x in [-0.53,0.53]:
    cube("Rear_Tail_Lamp", (x,1.19,0.94), (0.16,0.07,0.35), RED, 0.055)
    cube("Rear_Amber_Section", (x,1.23,0.78), (0.15,0.025,0.08), AMBER, 0.018)

# Yellow registration plate
cube("Rear_Number_Plate", (0,1.245,0.69), (0.43,0.025,0.20), YELLOW, 0.018)
cube("Front_Number_Plate", (0,-1.345,1.00), (0.44,0.025,0.19), YELLOW, 0.018)

# ---------- WHEELS ----------
wheel_rig=[]
def wheel(name, x, y, z, radius, width, steer=False):
    pivot=bpy.data.objects.new(name+"_SteerPivot", None); bpy.context.collection.objects.link(pivot)
    pivot.location=(x,y,z)
    tire=cyl(name+"_Tyre", (x,y,z), radius, width, RUBBER, (radians(90),0,0), 64)
    tire.parent=pivot; tire.location=(0,0,0)
    # Wheel axis is X; cylinder default axis Z, rotate 90deg about Y.
    tire.rotation_euler=(0,radians(90),0)
    hub=cyl(name+"_Hub", (x,y,z), radius*0.47, width*1.04, METAL, (0,radians(90),0), 48)
    hub.parent=pivot; hub.location=(0,0,0); hub.rotation_euler=(0,radians(90),0)
    cap=cyl(name+"_HubCap", (x + (width*0.54 if x>=0 else -width*0.54),y,z), radius*0.18, 0.025, DARK, (0,radians(90),0))
    cap.parent=pivot; cap.location=(width*0.54 if x>=0 else -width*0.54,0,0); cap.rotation_euler=(0,radians(90),0)
    wheel_rig.append((pivot, steer))
    return pivot

# Front wheel and rear pair
front_pivot=wheel("Front_Wheel",0,-1.18,0.43,0.36,0.17,True)
rear_left=wheel("Rear_Left_Wheel",-0.61,0.73,0.43,0.39,0.18)
rear_right=wheel("Rear_Right_Wheel",0.61,0.73,0.43,0.39,0.18)

# Front mudguard and rear wheel arches
uv("Front_Mudguard",(0,-1.16,0.69),(0.43,0.40,0.12),BLUE)
for x in [-0.62,0.62]:
    arch=uv("Rear_Wheel_Arch",(x,0.72,0.68),(0.30,0.62,0.19),BLUE)

# ---------- CABIN / CANOPY ----------
# Side pillars and roof supports
for x in [-0.59,0.59]:
    rod("Cabin_Front_Pillar",(x,-0.72,1.30),(x,-0.68,2.28),0.045,CANVAS)
    rod("Cabin_Rear_Pillar",(x,0.98,1.30),(x,0.90,2.25),0.045,CANVAS)
    rod("Cabin_Mid_Frame",(x,0.10,1.30),(x,0.10,2.26),0.035,CANVAS)

# Canopy roof, slightly rounded by bevel
roof=cube("Canopy_Roof_OffWhite",(0,0.10,2.30),(1.48,2.08,0.27),CANVAS,0.20)
# Rear canvas wall and side canvas panels; windows are dark translucent-looking panels.
rear_canvas=cube("Rear_Canvas_Wall",(0,1.02,1.83),(1.23,0.12,0.90),CANVAS,0.09)
rear_window=cube("Rear_Window",(0,1.088,1.93),(0.72,0.025,0.40),GLASS,0.09)
for x in [-0.635,0.635]:
    side_canvas=cube("Side_Canvas_Panel",(x,0.12,1.82),(0.10,1.55,0.87),CANVAS,0.07)
    side_window=cube("Side_Window",(x*1.055,0.14,1.90),(0.025,0.73,0.37),GLASS,0.08)

# Front windscreen frame and glass (front faces -Y)
wind=cube("Front_Windshield_Glass",(0,-0.70,1.82),(1.12,0.035,0.79),GLASS,0.08)
for x in [-0.60,0.60]:
    rod("Windscreen_Frame_Side",(x,-0.73,1.37),(x,-0.73,2.24),0.035,METAL)
rod("Windscreen_Frame_Top",(-0.60,-0.73,2.23),(0.60,-0.73,2.23),0.035,CANVAS)
rod("Windscreen_Frame_Bottom",(-0.60,-0.73,1.39),(0.60,-0.73,1.39),0.035,DARK)

# Windscreen wiper with animated arm pivot
wiper_pivot=bpy.data.objects.new("Wiper_Animated_Pivot",None); bpy.context.collection.objects.link(wiper_pivot)
wiper_pivot.location=(0,-0.765,1.48)
wiper=rod("Windscreen_Wiper_Blade",(-0.02,-0.77,1.48),(0.46,-0.77,2.12),0.018,METAL)
wiper.parent=wiper_pivot
wiper.location=(0.24,0,0.32)
wiper.rotation_euler=(0,0,0)

# Mirrors
for x in [-1,1]:
    rod("Mirror_Stem",(x*0.58,-0.68,1.62),(x*0.82,-0.70,1.78),0.025,METAL)
    mirror=uv("Side_Mirror",(x*0.86,-0.70,1.82),(0.12,0.07,0.19),DARK)

# Seats visible through cabin
seat=cube("Front_Bench_Seat",(0,-0.05,1.38),(1.02,0.42,0.18),SEAT,0.09)
seatback=cube("Front_Bench_Back",(0,0.24,1.65),(1.02,0.16,0.50),SEAT,0.08)

# ---------- ANIMATION / RIG ----------
scene=bpy.context.scene
scene.frame_start=1
scene.frame_end=121
scene.render.fps=30

# wheel spin: local rotation around axle (X)
for pivot, is_steer in wheel_rig:
    pivot.rotation_mode='XYZ'
    # spin is applied to wheel child objects via their local Y rotation
    for child in pivot.children:
        if "Tyre" in child.name or "Hub" in child.name:
            child.rotation_mode='XYZ'
            child.rotation_euler[1]=radians(90)
            child.keyframe_insert(data_path="rotation_euler",frame=1)
            child.rotation_euler[1]=radians(90)+2*math.pi
            child.keyframe_insert(data_path="rotation_euler",frame=121)

# Front steering pivot left/right/center
front_pivot.rotation_euler[2]=radians(-18); front_pivot.keyframe_insert(data_path="rotation_euler",frame=1)
front_pivot.rotation_euler[2]=radians(18); front_pivot.keyframe_insert(data_path="rotation_euler",frame=61)
front_pivot.rotation_euler[2]=0; front_pivot.keyframe_insert(data_path="rotation_euler",frame=121)

# Wiper sweep
wiper_pivot.rotation_euler[1]=radians(-22); wiper_pivot.keyframe_insert(data_path="rotation_euler",frame=1)
wiper_pivot.rotation_euler[1]=radians(22); wiper_pivot.keyframe_insert(data_path="rotation_euler",frame=31)
wiper_pivot.rotation_euler[1]=radians(-22); wiper_pivot.keyframe_insert(data_path="rotation_euler",frame=61)
wiper_pivot.rotation_euler[1]=radians(22); wiper_pivot.keyframe_insert(data_path="rotation_euler",frame=91)
wiper_pivot.rotation_euler[1]=radians(-22); wiper_pivot.keyframe_insert(data_path="rotation_euler",frame=121)

# Gentle chassis suspension bounce (subtle, no body redesign)
for f,z in [(1,0.82),(31,0.79),(61,0.84),(91,0.79),(121,0.82)]:
    chassis.location.z=z
    chassis.keyframe_insert(data_path="location",frame=f)

# Linear interpolation for wheel spin; smooth for other motion
for obj in bpy.data.objects:
    if obj.animation_data and obj.animation_data.action:
        for fc in obj.animation_data.action.fcurves:
            for kp in fc.keyframe_points:
                kp.interpolation='LINEAR' if ("Wheel" in obj.name and "SteerPivot" not in obj.name) else 'BEZIER'

# ---------- UNITY-FRIENDLY ORGANIZATION ----------
root=bpy.data.objects.new("BAJAJ_RE_ROOT",None); bpy.context.collection.objects.link(root)
root.empty_display_type='PLAIN_AXES'
# Parent all scene mesh objects and rigs under root while preserving transforms
for obj in list(bpy.context.scene.objects):
    if obj != root and obj.parent is None:
        obj.parent=root

# Set origin/root at ground center, apply scale
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1.0

# Camera and lighting for preview only (not required for FBX)
bpy.ops.object.camera_add(location=(4.8,-5.6,3.3))
cam=bpy.context.object; cam.name="Preview_Camera"
target=Vector((0,0,1.1)); cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
scene.camera=cam
bpy.ops.object.light_add(type='AREA', location=(1,-3,5))
light=bpy.context.object; light.name="Preview_Area_Light"; light.data.energy=850; light.data.shape='DISK'; light.data.size=5
# Hide preview helpers from FBX selection
cam.hide_set(True); light.hide_set(True)

# Timeline labels
scene.timeline_markers.new("Driving_Animation_Start", frame=1)
scene.timeline_markers.new("Steering_Midpoint", frame=61)
scene.timeline_markers.new("Animation_End", frame=121)

# Select root hierarchy
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
bpy.context.view_layer.objects.active=root

# FBX export beside the .blend file (or to a known path)
output_path=bpy.path.abspath("//Bajaj_RE_Animated_Unity.fbx")
bpy.ops.export_scene.fbx(
    filepath=output_path,
    use_selection=False,
    apply_scale_options='FBX_SCALE_ALL',
    axis_forward='-Z',
    axis_up='Y',
    bake_anim=True,
    bake_anim_use_all_bones=True,
    bake_anim_use_nla_strips=False,
    bake_anim_use_all_actions=True,
    add_leaf_bones=False,
    object_types={'MESH','EMPTY'},
    path_mode='AUTO'
)
print("FBX exported to:", output_path)
