"""Build a Blender look-dev scene from the same faceted source exported to Unity.

Run with Blender -b -t 4 -P Tools/DefenseCreatures/preview.py -- <output folder>.
"""

from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from geometry import creatures, sub


def blender_point(point):
    return (point[0], -point[2], point[1])


def make_material(name, emissive):
    material = bpy.data.materials.new(name)
    material.use_nodes = True
    nodes = material.node_tree.nodes
    pigment = nodes.new("ShaderNodeVertexColor")
    pigment.layer_name = "Pigment"
    shader = nodes.get("Principled BSDF")
    material.node_tree.links.new(pigment.outputs["Color"], shader.inputs["Base Color"])
    shader.inputs["Roughness"].default_value = .68
    if emissive:
        material.node_tree.links.new(pigment.outputs["Color"], shader.inputs["Emission Color"])
        shader.inputs["Emission Strength"].default_value = 1.12
    return material


def import_part(part, parent, collection, skin, eye):
    if not part.faces:
        obj = bpy.data.objects.new(part.name, None)
        obj.empty_display_size = .09
    else:
        positions, indices = [], []
        for face in part.faces:
            start = len(positions)
            positions.extend(blender_point(sub(point, part.pivot)) for point in face.points)
            indices.append(tuple(range(start, len(positions))))
        mesh = bpy.data.meshes.new(part.name)
        mesh.from_pydata(positions, [], indices)
        mesh.materials.append(skin)
        mesh.materials.append(eye)
        pigment = mesh.color_attributes.new(name="Pigment", type="FLOAT_COLOR", domain="CORNER")
        for polygon, face in zip(mesh.polygons, part.faces):
            polygon.material_index = int(face.emissive)
            for index in polygon.loop_indices:
                pigment.data[index].color = (*face.color, 1)
        obj = bpy.data.objects.new(part.name, mesh)
    collection.objects.link(obj)
    obj.parent = parent[0] if parent else None
    obj.location = blender_point(sub(part.pivot, parent[1] if parent else (0, 0, 0)))
    return obj


def aim(obj, target):
    obj.rotation_euler = (Vector(target)-obj.location).to_track_quat("-Z", "Y").to_euler()


def light(name, point, energy, color, size):
    data = bpy.data.lights.new(name, "AREA")
    obj = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = point
    aim(obj, (0, 0, 0))
    data.energy, data.size, data.color = energy, size, color


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x, scene.render.resolution_y = 720, 435
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.world = bpy.data.worlds.new("Depth blue")
    scene.world.use_nodes = True
    environment = scene.world.node_tree.nodes["Background"]
    environment.inputs["Color"].default_value = (.013, .025, .038, 1)
    environment.inputs["Strength"].default_value = .38
    camera = bpy.data.objects.new("Inspection camera", bpy.data.cameras.new("Inspection camera"))
    scene.collection.objects.link(camera)
    camera.data.type, camera.data.ortho_scale = "ORTHO", 3.2
    camera.location = (5.55, -3.8, 2.17)
    aim(camera, (0, .05, -.02))
    scene.camera = camera
    light("Dive key", (1.82, -2.17, 3.6), 400, (.63, .83, 1), 3.1)
    light("Biolume fill", (2.25, .65, -.94), 145, (.40, .83, .77), 2.5)
    light("Cabin spill", (-1.12, -3, 1.37), 165, (1, .64, .43), 2.15)
    light("Fin rim", (-1.55, 1.77, 2.93), 500, (.49, .79, 1), 2.6)
    return scene


def main():
    output = Path(sys.argv[sys.argv.index("--")+1])
    output.mkdir(parents=True, exist_ok=True)
    scene = setup()
    skin, eye = make_material("Flat pigment", False), make_material("Bioluminescent lens", True)
    collection_roots = []
    for creature in creatures():
        collection = bpy.data.collections.new(creature.name)
        scene.collection.children.link(collection)
        root = bpy.data.objects.new(creature.name, None)
        collection.objects.link(root)
        collection_roots.append(root)
        rig = {None: (root, (0, 0, 0))}
        for part in creature.parts():
            obj = import_part(part, rig[part.parent], collection, skin, eye)
            rig[part.name] = (obj, part.pivot)
        for existing in bpy.data.collections:
            if existing.name in ("Rammer", "Remora", "Thief", "Abductor", "Stalker"):
                existing.hide_render = existing is not collection
        scene.render.filepath = str(output / (creature.name + ".png"))
        bpy.ops.render.render(write_still=True)
    for i, root in enumerate(collection_roots):
        root.location.x = 3.4*i
        root.users_collection[0].hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(output / "DefenseCreatures.blend"))


if __name__ == "__main__":
    main()
