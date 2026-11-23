"""Build the first-person sleeves from the crew's FBX. Run with Blender in background mode."""
from pathlib import Path
import bmesh
import bpy
from mathutils import Vector

assets = Path(__file__).resolve().parents[2] / "Assets/_Game/Factory/Diver"
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(assets / "ExpeditionDiver.fbx"))
rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
body = next(obj for obj in bpy.context.scene.objects if obj.type == "MESH")


def mesh_islands(mesh):
    parent = list(range(len(mesh.vertices)))
    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i
    seam = {}
    for v in mesh.vertices:
        key = tuple(round(c, 5) for c in body.matrix_world @ v.co)
        parent[root(v.index)] = root(seam.setdefault(key, v.index))
    for edge in mesh.edges:
        a, b = edge.vertices
        parent[root(a)] = root(b)
    labels = {}
    return [labels.setdefault(root(i), len(labels)) for i in range(len(parent))]


islands = mesh_islands(body.data)


def make_sleeve(side):
    sleeve = body.copy()
    sleeve.data = body.data.copy()
    sleeve.name = side + "Sleeve"
    bpy.context.collection.objects.link(sleeve)
    bm = bmesh.new()
    bm.from_mesh(sleeve.data)
    weights = bm.verts.layers.deform.active
    uv = bm.loops.layers.uv.active
    arm = {g.index for g in sleeve.vertex_groups
           if g.name.split(":")[-1].startswith((side + "Arm", side + "ForeArm", side + "Hand"))}
    # The source atlas splits the suit, glove trim, thigh tools and air hoses into mesh islands.
    # Keep the same glove accessories as the original first-person equipment.
    accessories = {18, 57, 58, 60, 61} if side == "Left" else {54, 55, 59, 62}
    glove = next(g.index for g in sleeve.vertex_groups if g.name.endswith(side + "Hand"))
    loose = []
    for v in bm.verts:
        influences = v[weights]
        total = sum(influences[g] for g in influences.keys() if g in arm)
        if islands[v.index] not in accessories and (islands[v.index] != 6 or total < .54):
            loose.append(v)
            continue
        if total == 0:
            influences[glove] = total = 1
        for group in list(influences.keys()):
            if group in arm:
                influences[group] /= total
            else:
                del influences[group]
    bmesh.ops.delete(bm, geom=loose, context="VERTS")

    # Close the actual armhole with a torso-side gusset. The crew's elbow, forearm,
    # wrist, finger bones, UVs and all visible sleeve surface vertices are preserved.
    shoulder = next(b for b in rig.data.bones if b.name.endswith(side + "Shoulder"))
    upper = next(b for b in rig.data.bones if b.name.endswith(side + "Arm"))
    bone_to_mesh = sleeve.matrix_world.inverted() @ rig.matrix_world
    socket = bone_to_mesh @ shoulder.head_local
    start = bone_to_mesh @ upper.head_local
    upper_axis = (bone_to_mesh @ upper.tail_local) - start

    def proximal(v):
        return (v.co - start).dot(upper_axis) < .80 * upper_axis.length_squared

    rim = [e for e in bm.edges if e.is_boundary and all(proximal(v) for v in e.verts)]
    assert 20 <= len(rim) <= 55, (side, len(rim))
    border = {v for edge in rim for v in edge.verts}
    centre = sum((v.co for v in border), Vector()) / len(border)
    shoulder_group = sleeve.vertex_groups[shoulder.name].index
    back = {}
    for v in border:
        end = bm.verts.new(socket + (v.co - centre) * .79)
        end[weights][shoulder_group] = 1.0
        back[v] = end
    plug = bm.verts.new(socket)
    plug[weights][shoulder_group] = 1.0
    # Sample the adjacent suit texel at each end of the seam. The extension keeps
    # the existing suit material and its stitching without adding a material slot.
    for edge in rim:
        lead = edge.link_loops[0]
        a, b = lead.vert, lead.link_loop_next.vert
        ta = lead[uv].uv.copy()
        tb = lead.link_loop_next[uv].uv.copy()
        sidewall = bm.faces.new((b, a, back[a], back[b]))
        sidewall.material_index = lead.face.material_index
        for loop, at in zip(sidewall.loops, (tb, ta, ta, tb)):
            loop[uv].uv = at
        cap = bm.faces.new((back[b], back[a], plug))
        cap.material_index = lead.face.material_index
        for loop in cap.loops:
            loop[uv].uv = (ta + tb) * .5
    assert all(not e.is_boundary for e in rim)
    bm.normal_update()
    bm.to_mesh(sleeve.data)
    bm.free()


for side in ("Left", "Right"):
    make_sleeve(side)
bpy.data.objects.remove(body, do_unlink=True)
bpy.ops.export_scene.fbx(filepath=str(assets / "ExpeditionSleeves.fbx"),
                         object_types={"ARMATURE", "MESH"}, add_leaf_bones=False, bake_anim=False,
                         apply_unit_scale=True, axis_forward="-Z", axis_up="Y", path_mode="STRIP")
