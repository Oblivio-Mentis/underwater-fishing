"""Project-authored bow parasite. Blender headless; Unity world metres, +Z away from bow.
The open throat matches the interactive feeding mouth at (0, 3.5, 15.6).
No downloaded geometry, camera, lights, collider or gameplay component is exported.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/_Game/Nereid/Art/Creature'
ART=ROOT/'art/nereid-creature'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):bpy.data.materials.remove(m)
PALETTE={
    'Nereid_CreatureSkin':(.103,.145,.19,1),
    'Nereid_CreatureDark':(.029,.043,.066,1),
    'Nereid_CreatureIvory':(.48,.46,.35,1),
    'Nereid_CreatureEye':(.9,.27,.055,1),
    'Nereid_CreatureGill':(.235,.12,.17,1),
}
MATS={}
for name,color in PALETTE.items():
    m=bpy.data.materials.new(name);m.use_nodes=True;m.diffuse_color=color
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=color
    p.inputs['Roughness'].default_value=.36 if name.endswith('Skin') else .43
    p.inputs['Metallic'].default_value=.025
    if name.endswith('Eye'):
        p.inputs['Emission Color'].default_value=color;p.inputs['Emission Strength'].default_value=1.7
        p.inputs['Roughness'].default_value=.17
    if name.endswith('Skin'):
        n=m.node_tree.nodes.new('ShaderNodeTexNoise');n.inputs['Scale'].default_value=8;n.inputs['Detail'].default_value=2
        b=m.node_tree.nodes.new('ShaderNodeBump');b.inputs['Strength'].default_value=.14;b.inputs['Distance'].default_value=.075
        m.node_tree.links.new(n.outputs['Fac'],b.inputs['Height']);m.node_tree.links.new(b.outputs['Normal'],p.inputs['Normal'])
    MATS[name]=m

PARTS={}
def add(mat,verts,faces):
    v,f=PARTS.setdefault(mat,([],[]));start=len(v);v.extend(tuple(p)for p in verts);f.extend(tuple(start+i for i in face)for face in faces)

def catmull(points,steps=8):
    ps=[Vector(p)for p in points];result=[]
    for i in range(len(ps)-1):
        a,b,c,d=ps[max(0,i-1)],ps[i],ps[i+1],ps[min(len(ps)-1,i+2)]
        for k in range(steps):
            t=k/steps;result.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    result.append(ps[-1]);return result

def sweep(path,radii,mat,sides=16,flatten=.9,seed=0,details=False):
    path=[Vector(p)for p in path];verts=[];faces=[];frames=[]
    previous=None
    for i,p in enumerate(path):
        t=(path[min(len(path)-1,i+1)]-path[max(0,i-1)]).normalized()
        if previous is None:
            a=t.cross(Vector((0,1,0))if abs(t.y)<.9 else Vector((0,0,1))).normalized()
        else:
            a=(previous-t*previous.dot(t)).normalized()
        b=t.cross(a).normalized();previous=a;frames.append((a,b,t))
        fraction=i/(len(path)-1);radius=radii[0]*(1-fraction)**radii[1]+radii[2]
        for j in range(sides):
            theta=j*math.tau/sides
            muscle=1+.065*math.sin(i*.72+theta*3+seed)+.032*math.sin(theta*7-i*.31)
            verts.append(p+(a*math.cos(theta)+b*math.sin(theta)*flatten)*radius*muscle)
    for i in range(len(path)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;c=a+sides;d=b+sides
            faces.append((a,b,d,c))
    faces.append(tuple(reversed(tuple(range(sides)))));faces.append(tuple((len(path)-1)*sides+j for j in range(sides)))
    add(mat,verts,faces)
    if details:
        # Ventral, cupped suckers are actual recessed geometry; no torus or repeated sphere bodies.
        for i in range(5,len(path)-5,4):
            a,b,t=frames[i];fraction=i/(len(path)-1);radius=radii[0]*(1-fraction)**radii[1]+radii[2]
            inward=Vector((-path[i].x,-path[i].y,10-path[i].z))
            normal=(b if b.dot(inward)>0 else -b).normalized()
            across=t.cross(normal).normalized()
            for side in (-1,1):
                centre=path[i]+normal*radius*.82+across*radius*.37*side
                cup(centre,normal,across,t,max(.055,radius*.29),mat='Nereid_CreatureGill')
    return frames

def cup(centre,normal,a,b,radius,mat='Nereid_CreatureGill'):
    # A flattened muscular cup with a thick quiet lip, not a luminous disc.
    verts=[];faces=[];count=10
    profile=[(.62,-.025),(.95,.015),(1,.1),(.79,.14),(.55,.045),(.18,-.025),(0,-.035)]
    for r,z in profile:
        for j in range(count):
            theta=j*math.tau/count;verts.append(centre+normal*(z*radius)+a*(math.cos(theta)*r*radius)+b*(math.sin(theta)*r*radius*.82))
    for i in range(len(profile)-1):
        for j in range(count):faces.append((i*count+j,i*count+(j+1)%count,(i+1)*count+(j+1)%count,(i+1)*count+j))
    add(mat,verts,faces)

def body_point(z,rx,ry,cy,theta):
    shape=1+.035*math.sin(theta*5+z*.35)+.018*math.sin(theta*9-z*.6)
    x=math.cos(theta)*rx*shape
    y=cy+math.sin(theta)*ry*shape
    # Broad, asymmetric ridged forehead and compressed underside, with no primitive seam.
    y+=max(0,math.sin(theta))**6*(.3+.14*math.sin(z*.6))
    return Vector((x,y,z))

# Heavy cuttlefish-like mantle: open front around the true feeding cavity, broad shoulders, tapering tail.
sections=[(15.85,5.13,3.65,3.55),(17,5.6,4.1,4.1),(19,6.5,5.0,4.75),
          (22,7.2,5.6,5.2),(25,7.35,5.55,5.55),(28,6.7,5.1,5.9),
          (31,5.5,4.5,5.95),(34,4.05,3.7,5.8),(37,2.3,2.75,5.3),(39.5,.11,.16,4.9)]
profiles=catmull(sections,4)

def mantle_loft(profiles,mat,inside=False):
    sides=72;verts=[];faces=[]
    for z,rx,ry,cy in profiles:
        for j in range(sides):verts.append(body_point(z,rx,ry,cy,j*math.tau/sides))
    for i in range(len(profiles)-1):
        for j in range(sides):
            a=i*sides+j;b=i*sides+(j+1)%sides;c=a+sides;d=b+sides
            faces.append((a,c,d,b)if inside else(a,b,d,c))
    add(mat,verts,faces)

mantle_loft(profiles,'Nereid_CreatureSkin')
inner=[(15.58,4.66,3.18,3.5),(17.4,4.75,3.3,3.55),(19.15,4.75,3.3,3.55),
       (20.8,4.0,2.8,3.9),(22.5,2.85,2.0,4.4),(24.4,1.1,.9,4.9),(25,.02,.02,5.0)]
mantle_loft(catmull(inner,3),'Nereid_CreatureDark',True)
mantle_loft([(15.85,5.13,3.65,3.55),(15.58,4.66,3.18,3.5)],'Nereid_CreatureGill')

# Unequal radial throat folds; all begin beyond the 19m clear corridor.
for k in range(8):
    a=(k+.2)*math.tau/8
    points=[]
    for z,rx,ry,cy in inner[2:-1]:points.append((math.cos(a)*rx*.98,cy+math.sin(a)*ry*.98,z))
    sweep(catmull(points,4),(.14,1.1,.025),'Nereid_CreatureGill',10,.6,k)

# Interlocking dorsal and side armour, as shaped organic plates instead of a smooth balloon.
def section_at(z):
    for i in range(len(profiles)-1):
        if profiles[i][0]<=z<=profiles[i+1][0]:
            t=(z-profiles[i][0])/(profiles[i+1][0]-profiles[i][0]);return tuple(profiles[i][k]*(1-t)+profiles[i+1][k]*t for k in range(1,4))
    return tuple(profiles[-1][k]for k in range(1,4))

for row,theta in enumerate((.36,.81,1.22,1.69,2.13,2.59,2.95)):
    for k,z in enumerate((19.3,22.1,25.0,28.0,31.1,34.1)):
        z+=.32*math.sin(row*2.1+k);length=2.3 if k<4 else 1.8;span=.22 if row%2 else .25
        # Tapered six-sided carapace leaves, with a curved raised middle and quiet skin seams.
        # Their rounded shoulders and pointed ends avoid a mechanical checkerboard of flat rectangles.
        outline=[(-.72,0),(-.31,-1),(.31,-.93),(.68,.06),(.25,1.02),(-.35,.88)]
        verts=[];faces=[]
        for scale,push in ((1,.20),(.66,.43)):
            for dz,da in outline:
                zi=z+dz*length*scale;ai=theta+da*span*scale
                rx,ry,cy=section_at(zi);p=body_point(zi,rx,ry,cy,ai)
                verts.append(p+Vector((math.cos(ai),math.sin(ai),0))*push)
        rx,ry,cy=section_at(z)
        centre=body_point(z,rx,ry,cy,theta)+Vector((math.cos(theta),math.sin(theta),0))*(.57+.06*math.sin(k+row))
        verts.append(centre)
        for i in range(6):
            j=(i+1)%6;faces.extend(((i,j,6+j,6+i),(6+i,6+j,12)))
        add('Nereid_CreatureDark',verts,faces)
        sweep([verts[0],centre,verts[3]],(.09,.5,.025),'Nereid_CreatureSkin',8,.55,k+row)

# Dorsal keel: broad serrated membrane with a thick organic root, readable in silhouette.
for side in(-1,1):
    verts=[];faces=[]
    for k,z in enumerate((23,25.5,28,30.5,33,35.6,37.7)):
        rx,ry,cy=section_at(z);theta=math.pi/2+side*.11
        base=body_point(z,rx,ry,cy,theta)
        height=(1.9 if k in(1,2,3)else 1.0)*(.95+.12*math.sin(k*2.3+side))
        verts.extend((base+Vector((side*.22,0,0)),base+Vector((side*.5,height,-.45)),base+Vector((side*.05,.05,1.2))))
    for k in range(6):
        i=k*3;faces.extend(((i,i+1,i+4,i+3),(i+1,i+2,i+5,i+4)))
    add('Nereid_CreatureSkin',verts,faces)

# Gills lie outside the body shoulder, separated enough to read from pressure windows.
for side in(-1,1):
    for k in range(5):
        z=25.2+k*1.13;rx,ry,cy=section_at(z)
        theta=.2 if side>0 else math.pi-.2
        p=body_point(z,rx,ry,cy,theta)
        points=[p+Vector((side*.1,-.5,-.4)),p+Vector((side*.3,.2,0)),p+Vector((side*.12,1.1,.25))]
        sweep(catmull(points,5),(.13,.1,.025),'Nereid_CreatureGill',10,.55,k)

# Eight posed grasping arms. Paths wrap the pressure hull rather than ending at arbitrary rings.
ARM_PATHS=[]
for side in(-1,1):
    ARM_PATHS.append(([(side*5.8,8.7,22),(side*10.3,11.5,17.5),(side*15.2,11.6,10),
                       (side*17,8.0,3),(side*15.2,4.0,-1.5),(side*13.7,3.5,-4),(side*13.6,5,-3)],1.38))
    ARM_PATHS.append(([(side*6.5,4.7,23),(side*11.3,4.0,20),(side*16.2,.5,14.5),
                       (side*16,-3.5,8),(side*12.7,-6,3.5),(side*10.9,-5.2,2.5),(side*11.7,-3.5,5)],1.55))
    ARM_PATHS.append(([(side*4.8,.55,21),(side*8,-2.8,18),(side*11,-7.2,13),
                       (side*9,-10,7.3),(side*5.5,-10.2,4.8),(side*3.8,-9.1,6),(side*5.1,-8.6,8)],1.22))
    # The short pair reaches around the containment bulkhead and curls along the wet outer fairing.
    ARM_PATHS.append(([(side*5.45,1,19.5),(side*8.9,1.35,20.6),(side*12.35,2.85,19.5),
                       (side*15.3,4.9,17.3),(side*16.05,5.45,15.1),(side*15.65,4.29,14.11),(side*15.30,3.55,15.15)],1.07))

short_arm_vertices={}
for k,(points,radius)in enumerate(ARM_PATHS):
    start={mat:len(parts[0]) for mat,parts in PARTS.items()}
    path=catmull(points,7)
    sweep(path,(radius,1.15,.055),'Nereid_CreatureSkin',18,.86,k,True)
    # Raised lateral nerve folds wind smoothly with the arm, visibly modelled rather than a texture stripe.
    for offset in(-.5,.5):
        sidepath=[]
        for i,p in enumerate(path):
            frac=i/(len(path)-1);r=radius*(1-frac)**1.15+.055
            sidepath.append(p+Vector((offset*r,.23*r,0)))
        sweep(sidepath,(.075,.6,.012),'Nereid_CreatureDark',7,.55,k)

    if k in (3,7):
        for mat,(positions,_) in PARTS.items():
            short_arm_vertices.setdefault(mat,set()).update(range(start.get(mat,0),len(positions)))

# Raised orbital brows and truly convex amber lenses; two side eyes remain identifiable from outside.
def lens(centre,normal,rx,ry,mat,bulge=.2):
    normal=Vector(normal).normalized();b=Vector((0,1,0));b=(b-normal*b.dot(normal)).normalized();a=b.cross(normal).normalized()
    verts=[];faces=[];sides=40
    profile=[(1,0),(.83,bulge*.6),(.5,bulge*.94),(.05,bulge)]
    for r,z in profile:
        for j in range(sides):
            t=j*math.tau/sides;verts.append(Vector(centre)+a*(math.cos(t)*rx*r)+b*(math.sin(t)*ry*r)+normal*z)
    for i in range(len(profile)-1):
        for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    add(mat,verts,faces);return a,b,normal

for side in(-1,1):
    centre=Vector((side*6.86,7.1+(side+1)*.11,22.25))
    normal=Vector((side,.08,-.16)).normalized()
    a,b,n=lens(centre,normal,1.34,.83,'Nereid_CreatureDark',.21)
    lens(centre+n*.1,normal,.83,.49,'Nereid_CreatureEye',.28)
    lens(centre+n*.39,normal,.12,.41,'Nereid_CreatureDark',.035)
    # Hooded brow, heavy lids and infraorbital fold.
    points=[centre+a*1.34+b*.18,centre+a*.6+b*.94,centre-a*.45+b*1.0,centre-a*1.3+b*.16]
    sweep(catmull(points,7),(.29,.05,.08),'Nereid_CreatureSkin',14,.72,side)
    sweep(catmull([centre+a*1.25-b*.2,centre-b*.73,centre-a*1.22-b*.24],6),(.12,.05,.055),'Nereid_CreatureGill',10,.7,side)

# Four irregular ivory cheek hooks frame the opening without closing its clear 9x6m throat.
for side in(-1,1):
    sweep(catmull([(side*4.9,6.45,17),(side*5.3,6.1,15.9),(side*5.15,5.0,15.1),(side*4.8,4.5,15.25)],6),(.34,1.4,.012),'Nereid_CreatureIvory',12,.7,side)
    sweep(catmull([(side*4.75,.1,17.1),(side*5.1,-.1,16.0),(side*4.9,.8,15.05),(side*4.65,1.5,15.2)],6),(.28,1.5,.01),'Nereid_CreatureIvory',12,.7,side)

# Convert Unity-space vertices to Blender, with one batch per material and smooth organic normals.
objects=[]
for mat,(verts,faces)in PARTS.items():
    mesh=bpy.data.meshes.new(mat);mesh.from_pydata([(p[0],-p[2],p[1])for p in verts],[],faces);mesh.update()
    obj=bpy.data.objects.new(mat,mesh);bpy.context.collection.objects.link(obj);mesh.materials.append(MATS[mat]);objects.append(obj)
    for poly in mesh.polygons:poly.use_smooth=True
    uv=mesh.uv_layers.new(name='CreatureSurface')
    # UV2 tags the free-feeling pair for Unity's Reach and Curl shapes. Adjacent mantle folds
    # and all six older arms keep their original articulation even where trajectories cross.
    rig=mesh.uv_layers.new(name='Feelers')
    short_pair=short_arm_vertices.get(mat,set())
    for loop in mesh.loops:
        rig.data[loop.index].uv=(1.0 if loop.vertex_index in short_pair else 0.0,0.0)
    for poly in mesh.polygons:
        axis=max(range(3),key=lambda i:abs(poly.normal[i]));dims=[i for i in range(3)if i!=axis]
        for li in poly.loop_indices:
            p=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(p[dims[0]]*.25,p[dims[1]]*.25)

scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
fbx=OUT/'Nereid_BowCreature.fbx'
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='STRIP')
allverts=[];tris=0
for obj in objects:
    obj.data.calc_loop_triangles();tris+=len(obj.data.loop_triangles)
    allverts.extend((v.co.x,v.co.z,-v.co.y)for v in obj.data.vertices)
assert tris<100000,tris
metadata={'coordinates':'New Unity WORLD metres; +Y up, +Z away from bow; root origin (0,0,0), unit scale',
          'file':fbx.name,'materials':list(PALETTE),'mesh_count':len(objects),'triangles':tris,
          'unity_bounds_min':[min(p[k]for p in allverts)for k in range(3)],'unity_bounds_max':[max(p[k]for p in allverts)for k in range(3)],
          'feeding_opening':{'centre':[0,3.5,15.6],'clear_width':9,'clear_height':6,'clear_depth_end':19.0},
          'design':'Hollow-front mantle, deep throat, eight posed gripping arms with modelled cupped suckers, overlapping dorsal armour, two convex side eyes; no gameplay or collision data.'}
(OUT/'Nereid_BowCreature.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'Nereid_BowCreature.blend'))

# Preview cameras/lights are generated only after export and are not part of the FBX/source asset.
scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=1280;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world.color=(.035,.055,.07)
def aim(o,p):o.rotation_euler=(Vector((p[0],-p[2],p[1]))-o.location).to_track_quat('-Z','Y').to_euler()
def area(name,pos,power,col,size,target):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.color=col;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);bpy.context.collection.objects.link(o);o.location=(pos[0],-pos[2],pos[1]);aim(o,target)
area('Cold ocean key',(15,24,9),13000,(.4,.7,1),16,(0,4,20))
area('Cold rim',(-18,10,29),15000,(.28,.62,1),14,(0,5,21))
area('Ship warm reflected',(-8,7,6),9000,(1,.62,.34),10,(0,4,21))
area('Soft front',(0,8,-7),7500,(.6,.72,1),12,(0,4,20))
camera=bpy.data.objects.new('Preview camera',bpy.data.cameras.new('Preview camera'));bpy.context.collection.objects.link(camera);scene.camera=camera
camera.data.lens=39;camera.location=(55,-66,23);aim(camera,(0,2,16));scene.render.filepath=str(ART/'preview_side.png');bpy.ops.render.render(write_still=True)
camera.data.lens=35;camera.location=(40,35,20);aim(camera,(0,3.5,18));scene.render.filepath=str(ART/'preview_front.png');bpy.ops.render.render(write_still=True)
print('NEREID_CREATURE_EXPORT',json.dumps(metadata))
