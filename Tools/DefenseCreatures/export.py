"""Rebuild the editable Unity prefabs and meshes from geometry.py, using stock Python 3.10+."""

from hashlib import md5
from pathlib import Path
from struct import pack
from geometry import creatures, sub

ASSETS = Path(__file__).resolve().parents[2] / "Assets"
ROOT = ASSETS / "_Game/Threats/DefenseCreatures"
LIBRARY = ROOT / "Data"
RESOURCES = ROOT / "Resources/DefenseCreatures"
SCHOOL = RESOURCES / "Impostors"
SCHOOL_SHADER = LIBRARY / "DefenseSchool.shader"
REEF_SHADER = "f723a9f5441f4abd9ef668ec7118f5ce"
HEADER = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n"
UNLINKED = """  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
"""


def guid(path):
    return md5(("RaidNereidDefense-" + path.relative_to(ASSETS).as_posix()).encode()).hexdigest()


def write(path, contents):
    path.write_text(contents, encoding="utf-8", newline="\n")


def meta(path, importer, properties="", main_id=None):
    root = f"  mainObjectFileID: {main_id}\n" if main_id else ""
    write(Path(str(path) + ".meta"), f"""fileFormatVersion: 2
guid: {guid(path)}
{properties}{importer}:
  externalObjects: {{}}
{root}  userData:
  assetBundleName:
  assetBundleVariant:
""")


def folder(path):
    path.mkdir(exist_ok=True, parents=True)
    meta(path, "DefaultImporter", "folderAsset: yes\n")


def number(value):
    return format(value, ".7g") if abs(value) > 1e-8 else "0"


def vec(v, names="xyz"):
    return "{" + ", ".join(f"{axis}: {number(x)}" for axis, x in zip(names, v)) + "}"


def bounds(points):
    mins = tuple(min(values) for values in zip(*points))
    maxes = tuple(max(values) for values in zip(*points))
    return (tuple((lower+upper)*.5 for lower, upper in zip(mins, maxes)),
            tuple((upper-lower)*.5 for lower, upper in zip(mins, maxes)))


def type_ref(file_id, path=None, typ=2):
    return ("{fileID: %s}" % file_id) if path is None else (
        "{fileID: %s, guid: %s, type: %s}" % (file_id, guid(path), typ))


def mesh_data(part, file_id):
    data = bytearray()
    indices = bytearray()
    vertices = []
    submeshes = []
    for emissive in (False, True):
        first_vertex, first_byte = len(vertices), len(indices)
        for face in part.faces:
            if face.emissive != emissive:
                continue
            start = len(vertices)
            for point in face.points:
                local = sub(point, part.pivot)
                data.extend(pack("<10f", *local, *face.normal, *face.color, 1))
                vertices.append(local)
            for j in range(1, len(face.points)-1):
                indices.extend(pack("<3H", start, start+j, start+j+1))
        if len(vertices) == first_vertex:
            continue
        center, extents = bounds(vertices[first_vertex:])
        count = (len(indices)-first_byte) // 2
        submeshes.append(f"""  - serializedVersion: 2
    firstByte: {first_byte}
    indexCount: {count}
    topology: 0
    baseVertex: 0
    firstVertex: {first_vertex}
    vertexCount: {len(vertices)-first_vertex}
    localAABB:
      m_Center: {vec(center)}
      m_Extent: {vec(extents)}
""")
    center, extents = bounds(vertices)
    channels = [(0, 3), (12, 3), (0, 0), (24, 4)] + [(0, 0)] * 10
    channels_yaml = "".join(f"    - stream: 0\n      offset: {offset}\n      format: 0\n      dimension: {dim}\n"
                            for offset, dim in channels)
    compressed = "".join("    m_%s:\n      m_NumItems: 0\n%s      m_Data:\n      m_BitSize: 0\n" %
                         (entry, "      m_Range: 0\n      m_Start: 0\n" if is_float else "")
                         for entry, is_float in (
                             ("Vertices", True), ("UV", True), ("Normals", True),
                             ("Tangents", True), ("Weights", False), ("NormalSigns", False),
                             ("TangentSigns", False), ("FloatColors", True), ("BoneIndices", False),
                             ("Triangles", False)))
    lod_yaml = """    - serializedVersion: 2
      m_Levels:
      - serializedVersion: 1
        m_IndexStart: 0
        m_IndexCount: 0
""" * len(submeshes)
    return f"""--- !u!43 &{file_id}
Mesh:
{UNLINKED}  m_Name: {part.name}
  serializedVersion: 12
  m_SubMeshes:
{''.join(submeshes)}  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes:
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data:
  m_MeshCompression: 0
  m_IsReadable: 1
  m_KeepVertices: 1
  m_KeepIndices: 1
  m_IndexFormat: 0
  m_IndexBuffer: {indices.hex()}
  m_VertexData:
    serializedVersion: 3
    m_VertexCount: {len(vertices)}
    m_Channels:
{channels_yaml}    m_DataSize: {len(data)}
    _typelessdata: {data.hex()}
  m_CompressedMesh:
{compressed}    m_UVInfo: 0
  m_LocalAABB:
    m_Center: {vec(center)}
    m_Extent: {vec(extents)}
  m_MeshUsageFlags: 0
  m_CookingOptions: 30
  m_BakedConvexCollisionMesh:
  m_BakedTriangleCollisionMesh:
  'm_MeshMetrics[0]': 1
  'm_MeshMetrics[1]': 1
  m_MeshOptimizationFlags: 1
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path:
  m_MeshLodInfo:
    serializedVersion: 2
    m_LodSelectionCurve:
      serializedVersion: 1
      m_LodSlope: 0
      m_LodBias: 0
    m_NumLevels: 1
    m_SubMeshes:
{lod_yaml}""", len(indices)//6


def material(name, glow, wet, shader=REEF_SHADER):
    path = LIBRARY / f"{name}.mat"
    write(path, HEADER + f"""--- !u!21 &2100000
Material:
  serializedVersion: 8
{UNLINKED}  m_Name: {name}
  m_Shader: {{fileID: 4800000, guid: {shader}, type: 3}}
  m_Parent: {{fileID: 0}}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords: []
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: -1
  stringTagMap: {{}}
  disabledShaderPasses: []
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs: []
    m_Ints: []
    m_Floats:
    - _Glow: {glow}
    - _Hide: {wet}
    - _PigmentLightCap: 1.26
    m_Colors:
    - _Tint: {{r: 1, g: 1, b: 1, a: 1}}
  m_BuildTextureStacks: []
  m_AllowLocking: 1
""")
    meta(path, "NativeFormatImporter", main_id=2100000)
    return path


def go_yaml(name, node, parent_id, child_ids, local_position, mesh_ref=None, materials=()):
    go, transform, mesh, renderer = range(node*8, node*8+4)
    components = [transform, mesh, renderer] if mesh_ref else [transform]
    component_lines = "".join("  - component: %s\n" % type_ref(component) for component in components)
    children = "\n" + "".join(f"  - {type_ref(i)}\n" for i in child_ids) if child_ids else " []\n"
    base = f"""--- !u!1 &{go}
GameObject:
{UNLINKED}  serializedVersion: 6
  m_Component:
{component_lines}  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &{transform}
Transform:
{UNLINKED}  m_GameObject: {type_ref(go)}
  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {vec(local_position)}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children:{children}  m_Father: {type_ref(parent_id)}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
"""
    if not mesh_ref:
        return base
    material_lines = "".join("  - %s\n" % type_ref(2100000, path) for path in materials)
    return base + f"""--- !u!33 &{mesh}
MeshFilter:
{UNLINKED}  m_GameObject: {type_ref(go)}
  m_Mesh: {mesh_ref}
--- !u!23 &{renderer}
MeshRenderer:
{UNLINKED}  m_GameObject: {type_ref(go)}
  m_Enabled: 1
  m_CastShadows: 0
  m_ReceiveShadows: 0
  m_DynamicOccludee: 1
  m_StaticShadowCaster: 0
  m_MotionVectors: 1
  m_LightProbeUsage: 1
  m_ReflectionProbeUsage: 0
  m_RayTracingMode: 0
  m_SmallMeshCulling: 0
  m_ForceMeshLod: -1
  m_RenderingLayerMask: 1
  m_RendererPriority: 0
  m_Materials:
{material_lines}  m_StaticBatchInfo:
    firstSubMesh: 0
    subMeshCount: 0
  m_StaticBatchRoot: {{fileID: 0}}
  m_ProbeAnchor: {{fileID: 0}}
  m_LightProbeVolumeOverride: {{fileID: 0}}
  m_ScaleInLightmap: 1
  m_ReceiveGI: 1
  m_PreserveUVs: 0
  m_IgnoreNormalsForChartDetection: 0
  m_ImportantGI: 0
  m_StitchLightmapSeams: 1
  m_SelectedEditorRenderState: 3
  m_MinimumChartSize: 4
  m_AutoUVMaxDistance: .5
  m_AutoUVMaxAngle: 89
  m_LightmapParameters: {{fileID: 0}}
  m_GlobalIlluminationMeshLod: 0
  m_SortingLayerID: 0
  m_SortingLayer: 0
  m_SortingOrder: 0
  m_MaskInteraction: 0
  m_AdditionalVertexStreams: {{fileID: 0}}
"""


def export(creature, eye, school_materials):
    name = creature.name
    stock = RESOURCES / f"{name}.prefab"
    library = LIBRARY / f"{name}Parts.asset"
    geometry = creature.parts()
    skin = material(f"{name}Pigment", 0, .72)
    node = {part.name: i for i, part in enumerate(geometry, 2)}
    node[None] = 1
    pivot = {part.name: part.pivot for part in geometry}
    pivot[None] = (0, 0, 0)

    def children(parent):
        return [node[part.name]*8+1 for part in geometry if part.parent == parent]

    stock_yaml = HEADER + go_yaml(name, 1, 0, children(None), pivot[None])
    meshes_yaml = HEADER
    total_tris = 0
    for part in geometry:
        local = sub(part.pivot, pivot[part.parent])
        part_node = node[part.name]
        mesh_id = 4300000+part_node-2
        if part.faces:
            mesh_yaml, triangle_count = mesh_data(part, mesh_id)
            meshes_yaml += mesh_yaml
            total_tris += triangle_count
            materials = [skin, eye] if any(face.emissive for face in part.faces) else [skin]
            mesh_ref = type_ref(mesh_id, library)
        else:
            materials, mesh_ref = [], None
        stock_yaml += go_yaml(part.name, part_node, node[part.parent]*8+1,
                              children(part.name), local, mesh_ref, materials)
    write(stock, stock_yaml)
    meta(stock, "PrefabImporter")
    write(library, meshes_yaml)
    meta(library, "NativeFormatImporter", main_id=4300000)
    school_mesh = LIBRARY / f"{name}School.asset"
    school_yaml, school_tris = mesh_data(creature.school, 4300000)
    write(school_mesh, HEADER + school_yaml)
    meta(school_mesh, "NativeFormatImporter", main_id=4300000)
    school_prefab = SCHOOL / f"{name}.prefab"
    write(school_prefab, HEADER + go_yaml(name, 1, 0, [], (0, 0, 0),
                                        type_ref(4300000, school_mesh), school_materials))
    meta(school_prefab, "PrefabImporter")
    print(f"{name:<10} {total_tris:>4} triangles, school {school_tris:>3} triangles")


def main():
    for path in (ROOT, LIBRARY, ROOT/"Resources", RESOURCES, SCHOOL):
        folder(path)
    school = [material("SchoolPigment", 0, 0, guid(SCHOOL_SHADER)),
              material("SchoolEye", 1.35, 0, guid(SCHOOL_SHADER))]
    for creature in creatures():
        eye = material(f"{creature.name}Eye", 1.31, .18)
        export(creature, eye, school)


if __name__ == "__main__":
    main()
