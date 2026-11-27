"""Prepare the five downloaded Tripo meshes in Blender and export the Unity bake input."""

import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


# Width, length, height, longitudinal center, and reference points along the nose.
# The neutral swim pose is +Y in Blender and +Z in Unity.
PROFILES = {
    "Rammer": (True, 1.78, 2.62, 1.07, -.20, .86, .11, .71, -.13,
               "43565B 9A784A 7D8B80 B8C1A5 353837 FFB64F"),
    "Remora": (False, .91, 2.47, .73, -.24, .88, .042, .77, -.11,
               "372B55 71517D 917B92 AE83A8 261E37 ED8FD2"),
    "Thief": (False, 1.35, 2.54, .79, -.09, .87, .063, .73, -.065,
               "204746 528B77 9DBF97 677C5D 203436 A9FFBD"),
    "Abductor": (True, 2.24, 2.52, .93, -.02, .84, .083, .73, .023,
                 "33444E 507987 92A8A3 946B62 2B3139 75E6E3"),
    "Stalker": (False, 1.40, 2.86, .98, -.21, .89, .076, .69, -.063,
                "292837 58455D A397A6 84475A 2D2031 FF8D98"),
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
    along_x, width, length, height, center, *_ = profile
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
    adjusted = [Vector((-y, x, z) if along_x else (-x, -y, z)) for x, y, z in coords]
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
            # Maintain the fork while returning the peduncle to the swim axis.
            z -= (.090 if name == "Thief" else .045) * (1 - ease(.02, .31, u))
        if name == "Abductor":
            z -= (u - .5) * .43
        if name == "Remora" and z < -.22:
            z = -.22 + (z + .22) * .55
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
        place = Vector((side * width, along, elevation))
        hit, point, normal, _ = skin.ray_cast(place, Vector((-side, 0, 0)))
        if not hit:
            raise ValueError(skin.name + " eye alignment missed the mesh")
        normal = Vector((side * .91, max(.12, normal.y) * .60, max(.09, normal.z) * .35)).normalized()
        lens = (.064 if width > 1 else .045)
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
    _, width, length, height, center, _, _, hinge, mouth_z, _ = profile
    point = lambda fraction: center + (fraction - .5) * length
    return [Vector(vector) for vector in (
        (0, 0, 0), (0, point(.36), -.018), (0, point(.12), -.03),
        (-width * .12, point(.55), -height * .07), (width * .12, point(.55), -height * .07),
        (0, point(hinge), mouth_z), (0, point(.95), mouth_z),
    )]


BONES = ("Body", "Tail", "TailTip", "FinLeft", "FinRight", "Jaw", "MouthSocket")


def influences(position, profile):
    _, width, length, height, center, _, _, hinge, mouth_z, _ = profile
    x, y, z = position
    u = (y - center) / length + .5
    tail = 1 - ease(.23, .55, u)
    tip = 1 - ease(.043, .23, u)
    fin = ease(width * .17, width * .37, abs(x)) * ease(.23, .39, u) * (1 - ease(.65, .79, u))
    jaw = ease(hinge - .12, hinge + .13, u) * (1 - ease(mouth_z + .030, mouth_z + .105, z))
    return (max(0, 1-tail-fin-jaw), tail*(1-tip), tail*tip,
            fin if x < 0 else 0, fin if x >= 0 else 0, jaw)


def rig(name, parent, skin, profile):
    armature = bpy.data.armatures.new(name + " joints")
    obj = bpy.data.objects.new(name + " · swim rig", armature)
    parent.users_collection[0].objects.link(obj)
    obj.parent = parent
    obj.show_in_front = True
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT")
    for index, (label, pivot) in enumerate(zip(BONES, pivots(profile))):
        bone = armature.edit_bones.new(label)
        bone.head = pivot
        bone.tail = pivot + Vector((0, -.31 if index in (1, 2) else .26, 0))
        if index == 2:
            bone.parent = armature.edit_bones["Tail"]
    bpy.ops.object.mode_set(mode="OBJECT")
    groups = [skin.vertex_groups.new(name=label) for label in BONES[:-1]]
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
        defects, faces = simplify(skin, .34 if name in {"Remora", "Thief"} else .42)
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
            "main": full, "shoal": distant,
        }, separators=(",", ":")), encoding="utf-8")
        print(f"{name}: {raw_faces} original triangles, {faces} body, "
              f"{len(distant['points']) // 9} distant triangles, {defects} non-manifold body edges, "
              f"{len(skin.vertex_groups)} animated body zones")
    originals.hide_viewport = originals.hide_render = True
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.save_as_mainfile(filepath=str(source_blend))
