using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Raid.Nereid;

namespace Raid.Editor
{
    public static class TripoPredatorAuthoring
    {
        const string Root="Assets/_Game/Threats/DefenseCreatures";
        const string Atmosphere="Assets/_Game/Nereid/DefenseAtmosphere/Resources/DefenseAtmosphere/Atmosphere settings.asset";
        const string Meshes=Root+"/Tripo";
        const string Prefabs=Root+"/Resources/DefensePredators";
        static readonly string[] ModelNames={"Body","Tail","TailTip","FinLeft","FinRight","Jaw","MouthSocket"};

        [Serializable]
        sealed class Geometry
        {
            public float[] points,normals,colors,weights;
            public int[] indices;
            public int bodyCount;
        }

        [Serializable]
        sealed class Specimen
        {
            public string species;
            public float[] pivots;
            public Geometry main,shoal;
        }

        [MenuItem("RAID/Defense/Import prepared predator models")]
        public static void Import()
        {
            string directory=Application.isBatchMode?FindArgument("-predator-input"):
                EditorUtility.OpenFolderPanel("Prepared predator models",string.Empty,string.Empty);
            if(string.IsNullOrEmpty(directory))return;
            EnsureFolder(Root,"Tripo");
            EnsureFolder(Root+"/Resources","DefensePredators");
            var atmosphere=AssetDatabase.LoadAssetAtPath<DefenseAtmosphereSettings>(Atmosphere);
            var school=new Mesh[5];
            foreach(DefenseSpecies species in Enum.GetValues(typeof(DefenseSpecies)))
            {
                var model=JsonUtility.FromJson<Specimen>(File.ReadAllText(Path.Combine(directory,species+"-unity.json")));
                if(model.species!=species.ToString() || model.pivots.Length!=ModelNames.Length*3)
                    throw new InvalidDataException("Unexpected model in "+species+" bake input.");
                var pigment=AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Data/{species}Pigment.mat");
                var eyes=AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Data/{species}Eye.mat");
                if(!pigment || !eyes)throw new InvalidDataException("Missing reef materials for "+species);
                var materials=new[]{pigment,eyes};
                BakeModel(model,materials);
                school[(int)species]=Save(MakeMesh(model.shoal),Meshes+"/"+species+"Shoal.asset");
                if(species==DefenseSpecies.Remora)
                {
                    BakeRemoraFragment(model.main,materials,true);
                    BakeRemoraFragment(model.main,materials,false);
                }
            }
            atmosphere.silhouetteMeshes=school;
            EditorUtility.SetDirty(atmosphere);
            BakeReview();
            AssetDatabase.SaveAssets();
        }

        static void BakeReview()
        {
            EnsureFolder(Root,"Review");
            var group=new GameObject("Predator Review");
            try
            {
                foreach(DefenseSpecies species in Enum.GetValues(typeof(DefenseSpecies)))
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{species}.prefab");
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.transform);
                    model.transform.localPosition=new Vector3(((int)species-2)*3.2f,0,0);
                }
                PrefabUtility.SaveAsPrefabAsset(group,Root+"/Review/Predator Review.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(group); }
        }

        static void BakeModel(Specimen model,Material[] materials)
        {
            var go=new GameObject(model.species);
            try
            {
                var bones=new Transform[6];
                var bindPoses=new Matrix4x4[6];
                for(int i=0;i<ModelNames.Length;i++)
                {
                    var joint=new GameObject(ModelNames[i]).transform;
                    joint.SetParent(i==2?bones[1]:go.transform,false);
                    joint.position=Point(model.pivots,i);
                    if(i==bones.Length)break;
                    bones[i]=joint;
                    bindPoses[i]=joint.worldToLocalMatrix*go.transform.localToWorldMatrix;
                }
                var mesh=MakeMesh(model.main);
                var weights=new BoneWeight[mesh.vertexCount];
                for(int i=0;i<weights.Length;i++)
                {
                    int at=i*4;
                    var index=model.main.indices;
                    var weight=model.main.weights;
                    weights[i]=new BoneWeight{
                        boneIndex0=index[at],boneIndex1=index[at+1],boneIndex2=index[at+2],boneIndex3=index[at+3],
                        weight0=weight[at],weight1=weight[at+1],weight2=weight[at+2],weight3=weight[at+3]};
                }
                mesh.bindposes=bindPoses;
                mesh.boneWeights=weights;
                mesh=Save(mesh,Meshes+"/"+model.species+"Swim.asset");
                var skin=go.AddComponent<SkinnedMeshRenderer>();
                skin.sharedMesh=mesh;
                skin.sharedMaterials=materials;
                skin.bones=bones;
                skin.rootBone=go.transform;
                skin.quality=SkinQuality.Bone4;
                skin.localBounds=mesh.bounds;
                var bounds=skin.localBounds;
                bounds.Expand(.85f);
                skin.localBounds=bounds;
                skin.shadowCastingMode=ShadowCastingMode.Off;
                skin.receiveShadows=false;
                PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/"+model.species+".prefab");
                Debug.Log($"{model.species} has {mesh.vertexCount/3} runtime triangles, bounds {mesh.bounds.size}, and {bones.Length} swim joints.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static void BakeRemoraFragment(Geometry source,Material[] materials,bool head)
        {
            string name=head?"RemoraInkHead":"RemoraInkTail";
            var mesh=Save(MakeMesh(source,triangle=>((source.points[triangle*3+2]+source.points[triangle*3+5]+
                source.points[triangle*3+8])/3f>.1f)==head),Meshes+"/"+name+".asset");
            var go=new GameObject(name);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials=materials;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/"+name+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        static Mesh MakeMesh(Geometry source,Predicate<int> keep=null)
        {
            var vertices=new System.Collections.Generic.List<Vector3>();
            var normals=new System.Collections.Generic.List<Vector3>();
            var colors=new System.Collections.Generic.List<Color>();
            var skinTriangles=new System.Collections.Generic.List<int>();
            var eyeTriangles=new System.Collections.Generic.List<int>();
            for(int start=0;start<source.points.Length/3;start+=3)
            {
                if(keep!=null && !keep(start))continue;
                var triangles=start<source.bodyCount?skinTriangles:eyeTriangles;
                for(int i=start;i<start+3;i++)
                {
                    triangles.Add(vertices.Count);
                    vertices.Add(Point(source.points,i));
                    normals.Add(Point(source.normals,i));
                    colors.Add(new Color(source.colors[i*3],source.colors[i*3+1],source.colors[i*3+2],1));
                }
            }
            var mesh=new Mesh{subMeshCount=2};
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(skinTriangles,0,false);
            mesh.SetTriangles(eyeTriangles,1,false);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector3 Point(float[] component,int index) =>
            new(component[index*3],component[index*3+1],component[index*3+2]);

        static Mesh Save(Mesh mesh,string path)
        {
            mesh.name=Path.GetFileNameWithoutExtension(path);
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!existing){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            return existing;
        }

        static void EnsureFolder(string parent,string child)
        {
            if(!AssetDatabase.IsValidFolder(parent+"/"+child))AssetDatabase.CreateFolder(parent,child);
        }

        static string FindArgument(string key)
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,key);
            if(index<0 || index+1>=args.Length)throw new ArgumentException("Missing "+key);
            return args[index+1];
        }
    }
}
