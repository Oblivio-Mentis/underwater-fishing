"""Extract the supplied FBX's geometry for the editable Stalker rig.

Run Blender -b -P Tools/DefenseCreatures/import_stalker.py -- /path/to/monster_fish_3d_model.fbx.
The output includes geometry, pivots and palette assignments. UVs and images are omitted.
"""

from collections import defaultdict
from itertools import pairwise
from pathlib import Path
import json
from math import radians
import sys

import bmesh
import bpy
from mathutils import Vector


PIVOTS = {
    "Body": (0, 0, 0),
    "Tail": (.133, -.017, .408),
    "TailTip": (.375, .033, .670),
    "FinLeft": (-.185, .121, .347),
    "FinRight": (-.186, -.167, .355),
    "Jaw": (-.290, -.051, .266),
}
TARGET_LENGTH = 2.57


def rounded(sequence):
    return [round(float(value), 5) for value in sequence]


def place(point):
    """Turn the supplied Blender -X facing geometry into Unity +Z."""
    x, y, z = point
    return Vector((-(y+.052)*TARGET_LENGTH, (z-.413)*TARGET_LENGTH, -x*TARGET_LENGTH))


def rest_pivot(name):
    return (0, 0, 0) if name == "Body" else rounded(place(PIVOTS[name]))


def weld_source(obj):
    table = {}
    index_map = []
    vertices = []
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        key = tuple(round(value, 5) for value in point)
        if key not in table:
            table[key] = len(vertices)
            vertices.append(point)
        index_map.append(table[key])
    obj.data.calc_loop_triangles()
    triangles = []
    seen = set()
    for triangle in obj.data.loop_triangles:
        face = tuple(index_map[v] for v in triangle.vertices)
        stamp = frozenset(face)
        if len(stamp) == 3 and stamp not in seen:
            triangles.append(face)
            seen.add(stamp)
    return vertices, triangles


def islands(vertices, triangles):
    neighbors = defaultdict(set)
    for a, b, c in triangles:
        neighbors[a].update((b, c))
        neighbors[b].update((a, c))
        neighbors[c].update((a, b))
    tags = {}
    for first in range(len(vertices)):
        if first in tags:
            continue
        tags[first] = ""
        stack = [first]
        for key in stack:
            for neighbor in neighbors[key]:
                if neighbor not in tags:
                    tags[neighbor] = ""
                    stack.append(neighbor)
        center = sum((vertices[key] for key in stack), Vector((0, 0, 0))) / len(stack)
        if len(stack) < 65 and center.x < -.28 and center.z < .35:
            tag = "lower_tooth" if center.z < .25 else "upper_tooth"
        elif len(stack) < 22 and center.z > .41 and -.24 < center.x < .04:
            tag = "lumens"
        else:
            tag = ""
        tags.update((key, tag) for key in stack)
    return tags


def choose_part(point, tag):
    if tag == "lower_tooth":
        return "Jaw"
    if tag in ("upper_tooth", "lumens"):
        return "Body"
    if point.x >= .13:
        return "TailTip" if point.x > .26 and point.z > .625 else "Tail"
    if point.x < -.265 and point.z < .245 + .11*(point.x+.42):
        return "Jaw"
    if -.359 < point.x < .131 and point.z < .447:
        if point.y > .098:
            return "FinLeft"
        if point.y < -.162:
            return "FinRight"
    return "Body"


SWATCHES = (
    "323242",   # slate
    "3A384F",   # flank
    "52496A",   # dorsal crest
    "252331",   # belly
    "50374C",   # jaw ridge
    "1E192E",   # palate
    "C6BEA3",   # tooth
    "EEE0C0",   # lit tooth planes
    "172B33",   # recessed throat
    "51DFE6",   # shallow bioluminescence
    "41324A",   # fin membrane
    "373044",   # narrow joint shadow
)


def choose_swatch(point, normal, tag, part):
    if tag.endswith("tooth"):
        return 7 if normal.z > .38 else 6
    if tag == "lumens":
        return 9
    if part == "Jaw":
        return 5 if normal.z > .43 and abs(point.y+.05) < .088 else 4 if normal.z > -.28 else 3
    if part.startswith("Fin") and point.z < .12 and abs(point.y+.05) > .10:
        return 9 if point.z < .069 else 10
    if part.startswith("Fin"):
        return 10 if normal.z < .25 else 1
    if point.x < -.33 and .235 < point.z < .322 and abs(point.y+.05) < .077:
        return 8
    if point.z >= .55 or normal.z > .64:
        return 2
    if point.z < .34 or normal.z < -.38:
        return 3
    return 1 if normal.z > .12 else 0


def merge_faces(name, faces, coordinates, swatches):
    """Dissolve nearly coplanar interior edges without moving silhouette and rig seams."""
    mesh = bmesh.new()
    indices = {v: mesh.verts.new(coordinates[v]) for v in dict.fromkeys(v for face in faces for v in face)}
    for face, swatch in zip(faces, swatches):
        poly = mesh.faces.new([indices[v] for v in face])
        poly.material_index = swatch
    mesh.normal_update()
    bmesh.ops.dissolve_limit(mesh, angle_limit=radians(9.5 if name == "Body" else 8.0),
                            use_dissolve_boundaries=False, verts=list(mesh.verts),
                            edges=list(mesh.edges), delimit={"MATERIAL"})
    bmesh.ops.triangulate(mesh, faces=list(mesh.faces), quad_method="BEAUTY", ngon_method="BEAUTY")
    result = [(tuple(vert.co.copy() for vert in face.verts), face.material_index)
              for face in mesh.faces if face.calc_area() > 1e-8]
    mesh.free()
    return result


def prevent_open_joints(triangles, coordinates, assigned, clusters):
    """Bind inset copies of neighboring sculpt faces across the moving seams."""
    owners = defaultdict(list)
    normals = [Vector((0, 0, 0)) for point in coordinates]
    for index, face in enumerate(triangles):
        a, b, c = (coordinates[v] for v in face)
        normal = (b-a).cross(c-a)
        for vertex in face:
            normals[vertex] += normal
        for a, b in pairwise(face + (face[0],)):
            owners[tuple(sorted((a,b)))].append(index)
    for normal in normals:
        normal.normalize()

    adjacency = defaultdict(set)
    overlaps = defaultdict(set)
    for adjacent in owners.values():
        if len(adjacent) != 2:
            continue
        first, second = adjacent
        a, b = assigned[first], assigned[second]
        if a == b:
            adjacency[first].add(second)
            adjacency[second].add(first)
        else:
            overlaps[a].add(second)
            overlaps[b].add(first)

    for name, seam in overlaps.items():
        for index in sorted(seam | {other for border in seam for other in adjacency[border]}):
            polygon = tuple(coordinates[v]-normals[v]*.018 for v in triangles[index])
            clusters[name].append((polygon, 11))


def encode(clusters, path):
    output = {"palette": SWATCHES, "parts": {}}
    for name, faces in clusters.items():
        vertices, stored, table = [], [], {}
        for points, swatch in faces:
            ids = []
            for point in points:
                key = tuple(rounded(point))
                if key not in table:
                    table[key] = len(vertices)
                    vertices.append(key)
                ids.append(table[key])
            if len(set(ids)) == len(ids):
                stored.append((swatch, *ids))
        output["parts"][name] = {"pivot": rest_pivot(name), "verts": vertices, "faces": stored}
        print(name, len(vertices), "points", sum(len(face)-3 for face in stored), "triangles")
    path.write_text(json.dumps(output, separators=(",", ":")) + "\n", encoding="utf-8")


def main():
    source = Path(sys.argv[sys.argv.index("--")+1]).resolve(strict=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(source), use_image_search=False)
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(objects) != 1:
        raise RuntimeError("Expected the supplied single-mesh monster fish.")
    vertices, triangles = weld_source(objects[0])
    tags = islands(vertices, triangles)
    coordinates = [place(point) for point in vertices]
    assigned, groups, pigments = [], defaultdict(list), defaultdict(list)
    for face in triangles:
        a, b, c = map(vertices.__getitem__, face)
        center = (a+b+c)/3
        tag = tags[face[0]]
        part = choose_part(center, tag)
        pigment = choose_swatch(center, (b-a).cross(c-a).normalized(), tag, part)
        assigned.append(part)
        groups[part].append(face)
        pigments[part].append(pigment)
    if set(groups) != set(PIVOTS):
        raise RuntimeError("One or more of the authored articulation parts could not be extracted.")
    clusters = {part: merge_faces(part, groups[part], coordinates, pigments[part]) for part in PIVOTS}
    prevent_open_joints(triangles, coordinates, assigned, clusters)
    encode(clusters, Path(__file__).resolve().with_name("StalkerGeometry.json"))


if __name__ == "__main__":
    main()
