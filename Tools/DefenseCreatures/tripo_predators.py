"""Prepare the five downloaded Tripo meshes in Blender and export the Unity bake input."""

import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


# Width, length, height, longitudinal center, and reference points along the nose.
# The neutral swim pose is +Y in Blender and +Z in Unity.
PROFILES = {
    "Rammer": ("y", 1.84, 2.72, 1.16, -.20, .82, .14, .71, -.15,
               "39484E 896945 6E7773 B29B70 3F2D2A FFB34F"),
    "Remora": ("x", 1.28, 2.49, 1.04, -.22, .80, .13, .78, -.08,
               "33334E 634567 766983 634777 4D2F52 FF89AF"),
    "Thief": ("y", 1.18, 2.57, .98, -.13, .77, .16, .73, -.11,
               "234747 527D6B 879A79 A0A36F 41523A B6FFD0"),
    "Abductor": ("squid", 2.72, 3.89, 1.70, -.09, .45, .145, .57, -.12,
                 "462C48 8A515F 654053 AB7583 2B1A35 FFC06F"),
    "Stalker": ("x", 1.48, 3.03, 1.32, -.24, .82, .19, .66, -.13,
                "303445 55576C 877B80 585E75 392638 6EEDFF"),
}


def pigment(value):
    return tuple((int(value[i:i + 2], 16) / 255) ** 2.2 for i in (0, 2, 4))


def mix(a, b, t):
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def ease(start, end, value):
    t = min(1, max(0, (value - start) / (end - start)))
    return t * t * (3 - 2 * t)


def material(name, glow=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    shade = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    colors = nodes.new("ShaderNodeVertexColor")
    colors.layer_name = "Pigment"
    mat.node_tree.links.new(colors.outputs["Color"], shade.inputs["Base Color"])
    shade.inputs["Roughness"].default_value = .75
    if glow:
        mat.node_tree.links.new(colors.outputs["Color"], shade.inputs["Emission Color"])
        shade.inputs["Emission Strength"].default_value = 1.15
    return mat


def import_geometry(name, source, profile, originals):
    axis, width, length, height, center, *_ = profile
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.import_scene.gltf(filepath=str(source))
    raw, = (obj for obj in bpy.context.selected_objects if obj.type == "MESH")
    raw.name = name + " — Tripo original"
    raw.data.name = raw.name
    for collection in list(raw.users_collection):
        collection.objects.unlink(raw)
    originals.objects.link(raw)
    bpy.context.view_layer.update()
    obj = bpy.data.objects.new(name + " · skin", raw.data.copy())
    coords = [(raw.matrix_world @ v.co) for v in raw.data.vertices]
    if axis == "squid":
        # The generated squid is upright. Rotate its pointed mantle back into a neutral hunting pose.
        forward = Vector((.48, .04, -.875)).normalized()
        right = forward.cross(Vector((1, 0, 0))).normalized()
        up = right.cross(forward)
        adjusted = [Vector((v.dot(right),v.dot(forward),v.dot(up))) for v in coords]
    else:
        adjusted = [Vector((-y,x,z) if axis=="x" else (x,y,z)) for x,y,z in coords]
    low = Vector(tuple(min(p[i] for p in adjusted) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in adjusted) for i in range(3)))
    size = high - low
    middle = (high + low) * .5
    for vert, point in zip(obj.data.vertices, adjusted):
        x, y, z = (point - middle)
        x *= width / size.x
        y = y * length / size.y + center
        z *= height / size.z
        u = (y - center) / length + .5
        if name in {"Stalker", "Thief"}:
            z -= (.10 if name=="Thief" else .17)*(1-ease(.02,.42,u))
        if name == "Remora" and z < -.22:
            z = -.22 + (z + .22) * .55
        if name == "Abductor":
            x += .14
            z -= .29
        vert.co = (x, y, z)
    obj.data.update()
    return obj, len(raw.data.polygons)


def simplify(obj, ratio):
    bpy.context.view_layer.objects.active = obj
    mod = obj.modifiers.new("Silhouette-preserving collapse", "DECIMATE")
    mod.ratio = ratio
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00012)
    boundary=[edge for edge in bm.edges if edge.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(bm,edges=boundary,sides=7)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    stats = (sum(not edge.is_manifold for edge in bm.edges), len(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    return stats


def paint(obj, profile, name):
    _, width, length, height, center, _, _, hinge, mouth_z, values = profile
    skin, armor, belly, fin, lip, _ = (pigment(value) for value in values.split())
    colors = obj.data.color_attributes.get("Pigment")
    if colors is None:
        colors = obj.data.color_attributes.new(name="Pigment", type="FLOAT_COLOR", domain="CORNER")
    for face in obj.data.polygons:
        x, y, z = face.center
        along = (y - center) / length + .5
        ventral = 1 - ease(-.35, .06, z / height)
        accent = ease(.12, .49, z / height) * (.72 + .28 * face.normal.z)
        coating = mix(skin, armor, accent)
        coating = mix(coating, belly, ventral * .79)
        fin_mask = max(ease(width * .24, width * .44, abs(x)),
                       (1 - ease(.08, .24, along)) * ease(.09, .28, abs(z) / height))
        coating = mix(coating, fin, fin_mask)
        if along > hinge and abs(z - mouth_z) < height * .075 and face.normal.z < -.12:
            coating = mix(coating, lip, .67)
        if along > .83 and name in {"Stalker", "Thief"} and z < .04 and abs(face.normal.z) > .68:
            coating = mix(coating, belly, .62)
        finish = .94 + .13 * max(0, face.normal.z) + .065 * math.sin(face.index * 4.15)
        color = (*[component * finish for component in coating], 1.)
        face.use_smooth = False
        for loop in face.loop_indices:
            colors.data[loop].color = color


def add_eyes(skin, profile, body_mat, glow_mat, parent):
    _, width, length, height, center, fraction, elevation, *_ = profile
    shine = pigment(profile[-1].split()[-1])
    result = []
    for side in (-1, 1):
        along = center + (fraction - .5) * length
        candidates=[]
        for height_offset in (0,-.04,.04,-.10,.10,-.18,.18):
            for travel in (0,.06,-.06,.14,-.14,.23,-.23):
                place=Vector((side*width,along+travel,elevation+height_offset))
                hit,point,normal,_=skin.ray_cast(place,Vector((-side,0,0)))
                if hit:
                    score=abs(travel)*1.5+abs(height_offset)-max(-.1,normal.x*side)*.095
                    candidates.append((score,point,normal))
        if not candidates:
            raise ValueError(skin.name + " eye alignment missed the mesh")
        _,point,normal=min(candidates,key=lambda value:value[0])
        normal = Vector((side * .91, max(.12, normal.y) * .60, max(.09, normal.z) * .35)).normalized()
        lens = .123 if profile[0]=="squid" else .067 if width>1.35 else .051
        for is_eye in (False, True):
            bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=1)
            obj = bpy.context.object
            obj.name = ("Eye lens" if is_eye else "Eye socket") + (" L" if side < 0 else " R")
            scale = .81 if is_eye else 1.24
            basis = normal.to_track_quat("X", "Z")
            origin = point + normal * (lens * (.33 if is_eye else -.10))
            for v in obj.data.vertices:
                x, y, z = v.co
                v.co = origin + basis @ Vector((x * lens * .51, y * lens * scale * 1.30, z * lens * scale * .81))
            obj.location = (0, 0, 0)
            obj.data.materials.append(glow_mat if is_eye else body_mat)
            colors = obj.data.color_attributes.new(name="Pigment", type="FLOAT_COLOR", domain="CORNER")
            for entry in colors.data:
                entry.color = (*(shine if is_eye else (.017, .016, .025)), 1)
            obj.data.update()
            obj.parent = parent
            result.append((obj, is_eye))
    return result


def pivots(profile):
    axis, width, length, height, center, _, _, hinge, mouth_z, _ = profile
    point = lambda fraction: center + (fraction - .5) * length
    return [Vector(vector) for vector in (
        (0, 0, 0), (0, point(.36), -.018), (0, point(.12), -.03),
        (-width * .12, point(.55), -height * .07), (width * .12, point(.55), -height * .07),
        (0, point(hinge), mouth_z), (0, .54 if axis=="squid" else point(.95), mouth_z),
    )]


BONES = ("Body", "Tail", "TailTip", "FinLeft", "FinRight", "Jaw", "MouthSocket")


def squid_arms(profile):
    if profile[0]!="squid":
        return []
    return [Vector((side*.24,.30-.04*i,.10-.13*i)) for side in (-1,1) for i in range(4)]


def influences(position, profile):
    axis, width, length, height, center, _, _, hinge, mouth_z, _ = profile
    x, y, z = position
    u = (y - center) / length + .5
    tail = 1 - ease(.23, .55, u)
    tip = 1 - ease(.043, .23, u)
    fin = ease(width * .17, width * .37, abs(x)) * ease(.23, .39, u) * (1 - ease(.65, .79, u))
    jaw = ease(hinge - .12, hinge + .13, u) * (1 - ease(mouth_z + .030, mouth_z + .105, z))
    regular=(max(0, 1-tail-fin-jaw), tail*(1-tip), tail*tip,fin if x<0 else 0,fin if x>=0 else 0,jaw)
    if axis!="squid":
        return regular
    opened=ease(.42,.73,u)*max(ease(.19,.72,abs(x)),ease(.61,.86,u))
    layer=max(0,min(3,(.25-z)/.24))
    left=1-ease(-.16,.16,x)
    arm=[max(0,1-abs(i-layer))*opened*side for side in (left,1-left) for i in range(4)]
    return (*[weight*(1-opened) for weight in regular],*arm)


def rig(name, parent, skin, profile):
    armature = bpy.data.armatures.new(name + " joints")
    obj = bpy.data.objects.new(name + " · swim rig", armature)
    parent.users_collection[0].objects.link(obj)
    obj.parent = parent
    obj.show_in_front = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    bindings=list(zip(BONES[:-1],pivots(profile)[:-1]))
    bindings.extend((f"Tentacle {i+1}",p) for i,p in enumerate(squid_arms(profile)))
    for index, (label, pivot) in enumerate(bindings):
        bone = armature.edit_bones.new(label)
        bone.head = pivot
        bone.tail = pivot + Vector((0, -.31 if index in (1, 2) else .26, 0))
        if index == 2:
            bone.parent = armature.edit_bones["Tail"]
        if index>=6:
            bone.parent=armature.edit_bones["FinLeft" if index<10 else "FinRight"]
    bpy.ops.object.mode_set(mode="OBJECT")
    groups = [skin.vertex_groups.new(name=label) for label,_ in bindings]
    for v in skin.data.vertices:
        weights = influences(v.co, profile)
        for group, weight in zip(groups, weights):
            if weight > .0001:
                group.add([v.index], weight / sum(weights), "REPLACE")
    deform = skin.modifiers.new("Procedural swim rig", "ARMATURE")
    deform.object = obj


def unity(vector):
    x, y, z = vector
    return [round(x, 5), round(z, 5), round(y, 5)]


def geometry(parts, profile):
    packets = []
    for obj, eye in parts:
        obj.data.calc_loop_triangles()
        colors = obj.data.color_attributes["Pigment"].data
        for tri in obj.data.loop_triangles:
            normal = unity(tri.normal)
            points, pigment_values, bone_indices, bone_weights = [], [], [], []
            for loop in (tri.loops[0], tri.loops[2], tri.loops[1]):
                pos = obj.data.vertices[obj.data.loops[loop].vertex_index].co
                points.extend(unity(pos))
                pigment_values.extend(round(channel, 5) for channel in colors[loop].color[:3])
                resolved = sorted(enumerate(influences(pos, profile) if obj == parts[0][0] else (1, 0, 0, 0, 0, 0)),
                                  key=lambda pair: pair[1], reverse=True)[:4]
                total = sum(weight for _, weight in resolved)
                bone_indices.extend(index for index, _ in resolved)
                bone_weights.extend(round(weight / total, 6) for _, weight in resolved)
            packets.append((eye, points, normal * 3, pigment_values, bone_indices, bone_weights))
    result = {key: [] for key in ("points", "normals", "colors", "indices", "weights")}
    for _, *entries in sorted(packets, key=lambda packet: packet[0]):
        for key, values in zip(result, entries):
            result[key].extend(values)
    result["bodyCount"] = sum(not face[0] for face in packets) * 3
    return result


def prepare(input_dir, source_blend):
    input_dir = Path(input_dir)
    # This runs in an isolated review file. Restrict cleanup to this script's own collections.
    for name in ("Raw Tripo geometry", "Underwater Fishing predators"):
        old = bpy.data.collections.get(name)
        if old:
            for obj in list(old.objects):
                bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.collections.remove(old)
    for obj in list(bpy.data.objects):
        if obj.name.endswith(" raw"):
            bpy.data.objects.remove(obj, do_unlink=True)
    originals = bpy.data.collections.new("Raw Tripo geometry")
    edited = bpy.data.collections.new("Underwater Fishing predators")
    for collection in (originals, edited):
        bpy.context.scene.collection.children.link(collection)
    skin_mat, glow_mat = material("Flat underwater pigment"), material("Abyssal eye light", True)
    for index, (name, profile) in enumerate(PROFILES.items()):
        root = bpy.data.objects.new(name, None)
        edited.objects.link(root)
        skin, raw_faces = import_geometry(name, input_dir / (name + "-decoded.glb"), profile, originals)
        skin.parent = root
        edited.objects.link(skin)
        defects, faces = simplify(skin, .64 if name in {"Remora", "Thief"} else .73)
        paint(skin, profile, name)
        skin.data.materials.append(skin_mat)
        eyes = add_eyes(skin, profile, skin_mat, glow_mat, root)
        full = geometry([(skin, False), *eyes], profile)
        lod = bpy.data.objects.new(name + " · shoal geometry", skin.data.copy())
        edited.objects.link(lod)
        simplify(lod, .135)
        paint(lod, profile, name)
        distant = geometry([(lod, False), *eyes], profile)
        bpy.data.objects.remove(lod, do_unlink=True)
        rig(name, root, skin, profile)
        root.location.x = ((index % 3) - 1) * 3.7
        root.location.y = (index // 3) * 3.8
        for eye, _ in eyes:
            for collection in list(eye.users_collection):
                collection.objects.unlink(eye)
            edited.objects.link(eye)
        (input_dir / (name + "-unity.json")).write_text(json.dumps({
            "species": name, "pivots": [value for p in pivots(profile) for value in unity(p)],
            "armPivots": [value for p in squid_arms(profile) for value in unity(p)],
            "main": full, "shoal": distant,
        }, separators=(",", ":")), encoding="utf-8")
        print(f"{name}: {raw_faces} original triangles, {faces} body, "
              f"{len(distant['points']) // 9} distant triangles, {defects} non-manifold body edges, "
              f"{len(skin.vertex_groups)} animated body zones")
    originals.hide_viewport = originals.hide_render = True
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.save_as_mainfile(filepath=str(source_blend))


if __name__ == "__main__":
    input_dir=Path(sys.argv[sys.argv.index("--")+1])
    bpy.ops.wm.read_factory_settings(use_empty=True)
    prepare(input_dir,input_dir/"TripoPredators.blend")
