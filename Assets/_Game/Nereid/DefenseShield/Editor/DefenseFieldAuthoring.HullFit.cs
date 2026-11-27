using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static partial class DefenseFieldAuthoring
    {
        const string ExpeditionScene = "Assets/_Game/Scenes/InterfaceVerticalSlice.unity";

        [MenuItem("RAID/Defense/Fit field mounts to observation deck")]
        public static void FitHullMounts()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!source)
            {
                CreatePrefab();
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var field = root.GetComponent<DefenseFieldView>();
                var fittings = root.transform.Find("Hull induction generators");
                var material = fittings.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                field.radii = new Vector3(7.13f,4.05f,4.70f);
                field.rimHeight = .885f;
                if (Fit(field, fittings, material))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                    AssetDatabase.SaveAssets();
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static bool Fit(DefenseFieldView view, Transform generators, Material housingMaterial)
        {
            if (generators.Find("Hull retaining rails")) return false;

            var lensMaterial = view.generatorLenses[0].sharedMaterial;
            var saddle = Asset("Aquarium retaining saddle", RetainingSaddle());
            var arm = Asset("Aquarium field brace", Brace());
            var lens = Asset("Aquarium field collar", ContactLens());
            var skirt = Asset("Hull-conforming membrane", Envelope());
            view.surface.GetComponent<MeshFilter>().sharedMesh = skirt;
            view.surface.transform.localPosition = Vector3.up * view.RimHeight;
            view.surface.transform.localScale = view.EnvelopeScale;

            var rails = Node(generators, "Hull retaining rails", Vector3.zero);
            MakeMesh(rails, "Machined reservoir opening", Asset("Aquarium aperture lip", ApertureLip(view)), housingMaterial);
            foreach (int side in new[] {-1, 1})
            {
                var rail = Node(rails, side < 0 ? "Port sill" : "Starboard sill",
                    new Vector3(side * (view.radii.x - .12f), view.RimHeight - 1.10f, 0));
                rail.localRotation = Quaternion.Euler(0, side < 0 ? 180 : 0, 0);
                rail.localScale = new Vector3(1, 1, view.radii.z / 4.70f);
                MakeMesh(rail, "Bolted induction carrier rail", saddle, housingMaterial);
            }

            view.contactLenses = new MeshRenderer[view.generatorLenses.Length];
            var extents = view.EnvelopeScale;
            for (int i = 0; i < view.generatorLenses.Length; i++)
            {
                var lamp = view.generatorLenses[i];
                var mounting = lamp.transform.parent;
                float side = i < view.generatorLenses.Length / 2 ? -1 : 1;
                float z = (i % 3 - 1) * view.radii.z * .58f;
                float y = view.RimHeight - .19f;
                float ny = (y - view.RimHeight) / extents.y;
                float along = Mathf.Pow(Mathf.Abs(z/extents.z),6);
                float width = extents.x * Mathf.Pow(Mathf.Max(0, Mathf.Pow(1 - ny*ny,3) - along),1f/6);
                mounting.localPosition = new Vector3(side*(width-.145f), view.RimHeight + .07f, z);
                mounting.localRotation = Quaternion.Euler(0, side < 0 ? 180 : 0, 0);

                var strap = MakeMesh(mounting, "Pressure-sill suspension strap", arm, housingMaterial);
                strap.transform.localPosition = new Vector3(.077f, -.045f, 0);
                strap.transform.localScale = new Vector3(.38f, 1, 1);

                var contact = MakeMesh(mounting, "Induction contact collar", lens, lensMaterial);
                contact.transform.localPosition = new Vector3(.145f,-.26f,0);
                contact.transform.localRotation = Quaternion.Euler(0,0,-24);
                view.contactLenses[i] = contact;
            }
            return true;
        }

        static Mesh ApertureLip(DefenseFieldView view)
        {
            var a = new PlateMesh();
            var face = new Color(.21f,.30f,.32f);
            var gasket = new Color(.073f,.118f,.132f);
            const int divisions = 64;
            Vector3 Contour(float angle,bool outer,float height)
            {
                var t = DefenseFieldView.RimDirection(angle);
                if (outer) t /= Mathf.Max(Mathf.Abs(t.x),Mathf.Abs(t.z));
                return new Vector3(t.x * (view.radii.x + (outer ? .08f : 0)), view.RimHeight + height,
                    t.z * view.radii.z);
            }

            for (int i = 0; i < divisions; i++)
            {
                float p = Mathf.PI * 2 * i / divisions, q = Mathf.PI * 2 * (i + 1) / divisions;
                var innerA = Contour(p,false,.008f);
                var innerB = Contour(q,false,.008f);
                var outerA = Contour(p,true,.055f);
                var outerB = Contour(q,true,.055f);
                a.Facing(innerA,outerA,outerB,innerB,Vector3.down,face);
                a.Facing(innerA,innerB,innerB+Vector3.up*.1f,innerA+Vector3.up*.1f,
                    -new Vector3(innerA.x,0,innerA.z),gasket);
            }
            return a.Save("Aquarium aperture lip");
        }

        public static void FitAndCaptureInstalledField()
        {
            OceanEnvelopeAuthoring.CutReservoirOpening();
            FitHullMounts();
            CaptureInstalledField();
        }

        static Mesh RetainingSaddle()
        {
            var a = new PlateMesh();
            var inset = new Color(.065f, .109f, .123f);
            var dark = new Color(.123f, .181f, .198f);
            var enamel = new Color(.218f, .293f, .305f);
            var shoulder = new Color(.364f, .453f, .433f);
            var warning = new Color(.60f, .38f, .132f);
            a.Bevel(new Vector3(0, 1.11f, 0), new Vector3(.29f, .12f, 8.51f), .118f, dark);
            a.Bevel(new Vector3(-.018f, 1.024f, 0), new Vector3(.215f, .14f, 8.27f), .09f, enamel);
            a.Bevel(new Vector3(-.005f, .963f, 0), new Vector3(.143f, .069f, 8.08f), .059f, inset);

            for (int bay = 0; bay < 6; bay++)
            {
                float z = (bay - 2.5f) * 1.56f;
                a.Bevel(new Vector3(0, 1.133f, z), new Vector3(.47f, .113f, .44f), .081f, dark);
                a.Bevel(new Vector3(.14f, 1.019f, z), new Vector3(.13f, .115f, .327f), .041f, shoulder);
                a.Bevel(new Vector3(.14f, .956f, z), new Vector3(.15f, .026f, .218f), .036f, warning);
                for (int edge = -1; edge <= 1; edge += 2)
                    a.Bevel(new Vector3(.086f, .94f, z + edge * .121f), new Vector3(.054f, .045f, .048f),
                        .018f, shoulder);
            }

            for (int end = -1; end <= 1; end += 2)
            {
                a.Bevel(new Vector3(-.016f, 1.045f, 4.18f * end), new Vector3(.304f, .161f, .225f),
                    .072f, dark);
                a.Bevel(new Vector3(-.016f, .964f, 4.18f * end), new Vector3(.192f, .030f, .137f),
                    .028f, warning);
            }
            return a.Save("Aquarium retaining saddle");
        }

        static Mesh Brace()
        {
            var a = new PlateMesh();
            var dark = new Color(.122f, .181f, .198f);
            var enamel = new Color(.224f, .314f, .327f);
            var bronze = new Color(.39f, .29f, .14f);
            a.Bevel(Vector3.zero, new Vector3(1, .12f, .42f), .105f, dark);
            a.Bevel(new Vector3(0, -.076f, 0), new Vector3(.85f, .079f, .275f), .085f, enamel);
            a.Bevel(new Vector3(.36f, -.125f, 0), new Vector3(.227f, .052f, .22f), .052f, bronze);
            return a.Save("Aquarium field brace");
        }

        static Mesh ContactLens()
        {
            var a = new PlateMesh();
            a.Bevel(new Vector3(0, -.014f, 0), new Vector3(.229f, .025f, .43f), .054f, Color.white);
            a.Bevel(new Vector3(0, -.031f, 0), new Vector3(.155f, .018f, .361f), .04f,
                new Color(.74f, .90f, .94f));
            return a.Save("Aquarium field collar");
        }

        // Use the actual installed ship and its normal renderer for authoring checks.
        public static void CaptureInstalledField()
        {
            EditorSceneManager.OpenScene(ExpeditionScene);
            var field = Object.FindAnyObjectByType<DefenseFieldView>();
            if (!field || field.contactLenses.Length != 6 ||
                field.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("The aquarium must contain six hull contacts and a collider-free field.");

            var navigation = Object.FindAnyObjectByType<NereidDeckNavigation>();
            if (!navigation || !navigation.reservoirGlass || !navigation.reservoirGlass.enabled ||
                !navigation.reservoirGlass.gameObject.activeInHierarchy)
                throw new InvalidOperationException("The observation glass collision is missing.");

            float maximumError = field.contactLenses.Max(contact =>
                Vector3.Distance(contact.transform.position,field.ContactWorld(contact.transform.position)));
            if (maximumError > .095f) throw new InvalidOperationException("An induction collar misses the envelope.");
            Physics.SyncTransforms();
            if (!navigation.reservoirGlass.Raycast(new Ray(field.transform.TransformPoint(Vector3.up * 2.1f),
                -field.transform.up), out _, 2.5f))
                throw new InvalidOperationException("The observation window no longer supports the upper deck.");
            var belly = Object.FindObjectsByType<MeshCollider>().Where(c =>
                c.name == "Port bathysphere plating" || c.name == "Starboard bathysphere plating").ToArray();
            if (belly.Length != 2 ||
                belly.Any(c => c.Raycast(SideRay(c,0),out _,19)) ||
                belly.Any(c => !c.Raycast(SideRay(c,-5.55f),out _,19)))
                throw new InvalidOperationException("The hull collider must open only inside the stock bay.");
            var approach = field.transform.TransformPoint(new Vector3(11,-1.6f,1.0f));
            if (!field.ContainsWorld(field.transform.TransformPoint(Vector3.down * (field.DepthBelowCenter-.15f))) ||
                field.ContainsWorld(field.transform.TransformPoint(Vector3.down * (field.DepthBelowCenter+.15f))) ||
                field.ContainsWorld(field.ContactWorld(approach,.15f)) ||
                !field.ContainsWorld(field.ContactWorld(approach,-.15f)))
                throw new InvalidOperationException("The field collider does not match its closed bowl.");

            Debug.Log($"[Defense] Six membrane seats, max gap {maximumError:F3} m. Tested both hull openings, adjacent hull collision, upper glass and closed field bounds.");

            Ray SideRay(Collider side, float z)
            {
                float direction = side.name.StartsWith("Port") ? -1 : 1;
                return new Ray(field.transform.TransformPoint(new Vector3(direction*9.50f,-2.12f,z)),
                    field.transform.right * -direction);
            }

            var sourceCamera = Camera.main;
            if (!sourceCamera) sourceCamera = Object.FindAnyObjectByType<Camera>();
            var camera = new GameObject("Temporary hull fit camera").AddComponent<Camera>();
            var priorClarity = Shader.GetGlobalVector("_NereidReefClarity");
            var priorWaterTint = Shader.GetGlobalColor("_NereidWaterTint");
            var render = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            try
            {
                if (sourceCamera) camera.CopyFrom(sourceCamera);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.010f,.045f,.065f);
                camera.targetTexture = render;
                camera.cullingMask = -1;
                camera.fieldOfView = 59;
                const float extinction = .0039f;
                Shader.SetGlobalVector("_NereidReefClarity", new Vector4(extinction,.48f,.47f,0));
                Shader.SetGlobalColor("_NereidWaterTint", new Color(.014f,.066f,.088f));
                string output = Path.Combine(Path.GetTempPath(),"RAID-DefenseShield-Hull-Fit");
                Directory.CreateDirectory(output);

                RenderAt("01-installed",new Vector3(13.4f,-2.1f,-4.8f), new Vector3(0,-.46f,0));
                RenderAt("02-seating",new Vector3(9.82f,-.90f,-1.5f), new Vector3(6.75f,.70f,.55f));
                var contact = field.ContactWorld(field.transform.TransformPoint(new Vector3(10.4f,-1.7f,.8f)));
                field.Apply(.60f,1,1,contact,.15f);
                RenderAt("03-impact",new Vector3(13.4f,-2.1f,-4.8f), new Vector3(0,-.46f,0));

                void RenderAt(string name, Vector3 eye, Vector3 target)
                {
                    camera.transform.position = field.transform.TransformPoint(eye);
                    camera.transform.LookAt(field.transform.TransformPoint(target), Vector3.up);
                    camera.Render();
                    var previous = RenderTexture.active;
                    RenderTexture.active = render;
                    var png = new Texture2D(render.width,render.height,TextureFormat.RGB24,false);
                    try
                    {
                        png.ReadPixels(new Rect(0,0,render.width,render.height),0,0);
                        File.WriteAllBytes(Path.Combine(output,name+".png"), png.EncodeToPNG());
                    }
                    finally
                    {
                        RenderTexture.active = previous;
                        Object.DestroyImmediate(png);
                    }
                }
            }
            finally
            {
                Shader.SetGlobalVector("_NereidReefClarity",priorClarity);
                Shader.SetGlobalColor("_NereidWaterTint",priorWaterTint);
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(render);
                Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
