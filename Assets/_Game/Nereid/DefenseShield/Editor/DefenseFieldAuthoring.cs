using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class DefenseFieldAuthoring
    {
        const string AssetRoot = "Assets/_Game/Nereid/DefenseShield";
        const string PrefabRoot = AssetRoot + "/Resources/DefenseShield";
        const string PrefabPath = PrefabRoot + "/Field.prefab";

        [MenuItem("RAID/Defense/Create or select reservoir field prefab")]
        public static void CreatePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing)
            {
                Selection.activeObject = existing;
                return;
            }

            Directory.CreateDirectory(PrefabRoot);
            AssetDatabase.Refresh();
            var membrane = Asset("Containment membrane", Envelope());
            var housing = Asset("Induction generator", Housing());
            var lens = Asset("Induction ceramic", Lenses());
            var sparks = Asset("Displaced field shards", Shards());
            var fieldMaterial = Material("Containment membrane", "DefenseField");
            var housingMaterial = new Material(Resources.Load<Shader>("NereidReef"))
            {
                name = "Induction generator enamel"
            };
            housingMaterial.SetFloat("_PigmentLightCap", 1.23f);
            housingMaterial = Asset(housingMaterial.name, housingMaterial);
            var lensMaterial = Material("Induction ceramic", "DefenseGeneratorLamp");
            var sparksMaterial = Material("Displaced field shards", "DefenseImpactShards");

            var root = new GameObject("Containment field");
            try
            {
                var view = root.AddComponent<DefenseFieldView>();
                view.surface = MakeMesh(root.transform, "Field envelope", membrane, fieldMaterial);
                view.surface.transform.localScale = view.radii;
                view.sparks = MakeMesh(root.transform, "Local impact fragments", sparks, sparksMaterial);
                view.sparks.enabled = false;
                Transform generators = Node(root.transform, "Hull induction generators", Vector3.zero);
                view.generatorLenses = new MeshRenderer[6];
                int index = 0;
                foreach (int side in new[] {-1, 1})
                for (int station = -1; station <= 1; station++)
                {
                    string label = (side < 0 ? "Port" : "Starboard") + " " + (station + 2);
                    var mount = Node(generators, label, new Vector3(side * 1.54f, 1.17f, station * 2.31f));
                    mount.localRotation = Quaternion.Euler(0, side < 0 ? 180 : 0, 0);
                    MakeMesh(mount, "Bevelled induction housing", housing, housingMaterial);
                    view.generatorLenses[index++] = MakeMesh(mount, "Status glazing and emitter", lens, lensMaterial);
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log("[Defense] Editable field prefab at " + PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("RAID/Defense/Capture field material preview")]
        public static void CapturePreview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab)
            {
                CreatePrefab();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            }

            string path = Path.Combine(Path.GetTempPath(), "RAID-DefenseShield-Preview");
            Directory.CreateDirectory(path);
            Vector4 oldClarity = Shader.GetGlobalVector("_NereidReefClarity");
            Color oldTint = Shader.GetGlobalColor("_NereidWaterTint");
            Shader.SetGlobalVector("_NereidReefClarity", new Vector4(.0063f, .48f, .47f, 0));
            Shader.SetGlobalColor("_NereidWaterTint", new Color(.012f,.058f,.080f));
            var preview = new PreviewRenderUtility();
            try
            {
                var stand = Object.Instantiate(prefab);
                preview.AddSingleGO(stand);
                var field = stand.GetComponent<DefenseFieldView>();
                var camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.014f, .040f, .056f);
                camera.transform.position = new Vector3(8.5f, 3.0f, -7.8f);
                camera.transform.LookAt(Vector3.zero);
                camera.fieldOfView = 40;
                camera.farClipPlane = 60;
                preview.lights[0].intensity = 1.3f;
                preview.lights[0].transform.rotation = Quaternion.Euler(58, 50, 0);
                preview.lights[1].intensity = 1.0f;
                preview.lights[1].transform.rotation = Quaternion.Euler(31, -100, 0);
                preview.ambientColor = new Color(.33f,.49f,.54f);
                Draw("normal",1,1,0,Vector3.zero,2);
                Draw("two-hits",.70f,1,1,new Vector3(2.3f,-.27f,1.2f),.25f);
                Draw("two-hits",.56f,1,2,new Vector3(2.08f,-.36f,-2.2f),.10f);
                Draw("critical",.18f,1,3,new Vector3(2.35f,-.55f,.19f),.21f);
                Draw("collapse",0,.52f,4,new Vector3(1.85f,-.71f,-1.44f),.36f);
                Draw("offline",0,0,0,Vector3.zero,2);
                Draw("restoring",.58f,.54f,0,Vector3.zero,2);

                void Draw(string filename,float integrity,float formation,int sequence,Vector3 point,float age)
                {
                    field.Apply(integrity,formation,sequence,point,age);
                    preview.BeginStaticPreview(new Rect(0,0,960,600));
                    preview.Render(true);
                    var frame = preview.EndStaticPreview();
                    try { File.WriteAllBytes(Path.Combine(path,filename+".png"),frame.EncodeToPNG()); }
                    finally { Object.DestroyImmediate(frame); }
                }
            }
            finally
            {
                preview.Cleanup();
                Shader.SetGlobalColor("_NereidWaterTint",oldTint);
                Shader.SetGlobalVector("_NereidReefClarity",oldClarity);
            }
            Debug.Log("[Defense] Field previews saved to " + path);
        }

        static Material Material(string name, string source)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>($"{AssetRoot}/{source}.shader");
            return Asset(name, new Material(shader) { name = name });
        }

        static T Asset<T>(string name, T value) where T : Object
        {
            const string folder = AssetRoot + "/Models";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            string extension = value is Material ? ".mat" : ".asset";
            string path = $"{folder}/{name}{extension}";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing)
            {
                Object.DestroyImmediate(value);
                return existing;
            }

            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        static Transform Node(Transform parent,string name,Vector3 position)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        static MeshRenderer MakeMesh(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = Node(parent, name, Vector3.zero);
            child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        static Mesh Envelope()
        {
            const int levels = 16, slices = 36;
            var points = new List<Vector3> {Vector3.up};
            var triangles = new List<int>();
            for (int level = 1; level < levels; level++)
            {
                float latitude = Mathf.PI * level / levels;
                for (int ring = 0; ring < slices; ring++)
                {
                    float longitude = Mathf.PI * 2 * (ring + (level % 2 == 0 ? .5f : 0)) / slices;
                    points.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(longitude), Mathf.Cos(latitude),
                        Mathf.Sin(latitude) * Mathf.Sin(longitude)));
                }
            }
            points.Add(Vector3.down);
            int end = points.Count - 1;
            for (int ring = 0; ring < slices; ring++)
            {
                int next = (ring + 1) % slices;
                Face(0, 1 + ring, 1 + next);
                Face(end, end - slices + next, end - slices + ring);
                for (int level = 0; level < levels - 2; level++)
                {
                    int a = 1 + level * slices, b = a + slices;
                    Face(a + ring, b + ring, a + next);
                    Face(a + next, b + ring, b + next);
                }
            }
            var mesh = new Mesh { name = "Containment membrane" };
            mesh.SetVertices(points);
            mesh.SetNormals(points);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;

            void Face(int a,int b,int c)
            {
                bool reverse = Vector3.Dot(Vector3.Cross(points[b]-points[a],points[c]-points[a]),points[a]) < 0;
                triangles.Add(a);
                triangles.Add(reverse ? c : b);
                triangles.Add(reverse ? b : c);
            }
        }

        static Mesh Housing()
        {
            var a = new PlateMesh();
            var deep = new Color(.073f,.102f,.115f);
            var plate = new Color(.16f,.23f,.24f);
            var edge = new Color(.30f,.39f,.40f);
            var enamel = new Color(.21f,.34f,.37f);
            var safety = new Color(.51f,.30f,.085f);
            a.Bevel(Vector3.zero, new Vector3(.74f,.10f,1.35f),.11f, deep);
            a.Bevel(new Vector3(0,-.076f,0), new Vector3(.62f,.15f,1.20f),.092f, plate);
            a.Bevel(new Vector3(0,-.20f,.024f),new Vector3(.48f,.19f,.96f),.09f,enamel);
            a.Bevel(new Vector3(0,-.289f,0),new Vector3(.37f,.072f,.68f),.073f,deep);
            foreach (int side in new[]{-1,1})
            {
                a.Bevel(new Vector3(side*.241f,-.188f,.013f),new Vector3(.079f,.15f,.92f),.025f,edge);
                for (int rung=0; rung<4; rung++)
                {
                    float z=(rung-1.5f)*.178f;
                    a.Bevel(new Vector3(side*.264f,-.177f,z),new Vector3(.05f,.075f,.042f),.01f,deep);
                }
                foreach (int end in new[]{-1,1})
                {
                    a.Bevel(new Vector3(side*.252f,-.05f,end*.508f),new Vector3(.116f,.037f,.125f),.033f,safety);
                    a.Bevel(new Vector3(side*.257f,-.074f,end*.517f),new Vector3(.047f,.042f,.052f),.013f,edge);
                }
            }
            a.Bevel(new Vector3(0,-.175f,-.48f),new Vector3(.22f,.123f,.148f),.034f,edge);
            a.Bevel(new Vector3(0,-.165f,.49f),new Vector3(.28f,.11f,.142f),.034f,plate);
            return a.Save("Induction generator");
        }

        static Mesh Lenses()
        {
            var a = new PlateMesh();
            a.Bevel(new Vector3(0,-.322f,0),new Vector3(.26f,.026f,.53f),.055f,Color.white);
            for (int tab=0;tab<3;tab++)
                a.Bevel(new Vector3((tab-1)*.062f,-.174f,-.554f),new Vector3(.038f,.033f,.016f),.006f,Color.white);
            a.Bevel(new Vector3(0,-.16f,.562f),new Vector3(.16f,.033f,.019f),.005f,Color.white);
            return a.Save("Induction ceramic");
        }

        static Mesh Shards()
        {
            const int shardsPerHit = 24;
            var local = new[]
            {
                new Vector3(-.52f, -.29f, -.25f), new Vector3(.64f, -.24f, -.13f),
                new Vector3(.10f, .82f, -.13f), new Vector3(-.07f, -.22f, .48f)
            };
            var triangles = new[] {0,2,1,0,1,3,0,3,2,1,2,3};
            var points = new List<Vector3>(shardsPerHit * local.Length * 3);
            var seeds = new List<Vector2>(points.Capacity);
            var indices = new List<int>(shardsPerHit * triangles.Length * 3);
            for (int group=0;group<3;group++)
            for (int i=0;i<shardsPerHit;i++)
            {
                int start = points.Count;
                foreach (var point in local)
                {
                    points.Add(point);
                    seeds.Add(new Vector2(group*shardsPerHit+i+.71f,group));
                }
                foreach (int index in triangles) indices.Add(start+index);
            }
            var mesh = new Mesh { name = "Displaced field shards" };
            mesh.SetVertices(points);
            mesh.SetUVs(0,seeds);
            mesh.SetTriangles(indices,0,false);
            mesh.bounds = new Bounds(Vector3.zero,new Vector3(11,9,16));
            return mesh;
        }

        sealed class PlateMesh
        {
            readonly List<Vector3> points = new();
            readonly List<Color> pigments = new();
            readonly List<int> triangles = new();

            public void Bevel(Vector3 center,Vector3 dimensions,float chamfer,Color tint)
            {
                float hx=dimensions.x*.5f, hz=dimensions.z*.5f, hy=dimensions.y*.5f;
                chamfer = Mathf.Min(chamfer, Mathf.Min(hx,hz)*.93f);
                var outline = new[] {new Vector2(-hx+chamfer,-hz),new Vector2(hx-chamfer,-hz),
                    new Vector2(hx,-hz+chamfer),new Vector2(hx,hz-chamfer),
                    new Vector2(hx-chamfer,hz),new Vector2(-hx+chamfer,hz),
                    new Vector2(-hx,hz-chamfer),new Vector2(-hx,-hz+chamfer)};
                Vector3 Pos(Vector2 p,float y,float edge) => center + new Vector3(p.x*edge,y,p.y*edge);
                float bevelY = Mathf.Min(hy*.32f,chamfer*.42f);
                for (int i=0;i<outline.Length;i++)
                {
                    var a = outline[i];
                    var b = outline[(i+1)%outline.Length];
                    Vector3 lowerA=Pos(a,-hy,.88f), lowerB=Pos(b,-hy,.88f);
                    Vector3 upperA=Pos(a,hy,.87f), upperB=Pos(b,hy,.87f);
                    Vector3 wallA=Pos(a,-hy+bevelY,1), wallB=Pos(b,-hy+bevelY,1);
                    Vector3 topA=Pos(a,hy-bevelY,1), topB=Pos(b,hy-bevelY,1);
                    Vector3 outwards=(topA+topB)*.5f-center;
                    Quad(lowerA,lowerB,wallB,wallA,outwards+Vector3.down*.2f,tint*.77f);
                    Quad(wallA,wallB,topB,topA,outwards,tint);
                    Quad(topA,topB,upperB,upperA,outwards+Vector3.up*.2f,tint*1.18f);
                    Triangle(center-Vector3.up*hy,lowerA,lowerB,Vector3.down,tint*.84f);
                    Triangle(center+Vector3.up*hy,upperA,upperB,Vector3.up,tint*1.1f);
                }
            }

            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward,Color tint)
            {
                Triangle(a,b,c,outward,tint);
                Triangle(a,c,d,outward,tint);
            }

            void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector3 outward,Color tint)
            {
                if (Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0) (b,c)=(c,b);
                int address = points.Count;
                points.Add(a); points.Add(b); points.Add(c);
                pigments.Add(tint); pigments.Add(tint); pigments.Add(tint);
                triangles.Add(address); triangles.Add(address+1); triangles.Add(address+2);
            }

            public Mesh Save(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(points);
                mesh.SetColors(pigments);
                mesh.SetTriangles(triangles,0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
