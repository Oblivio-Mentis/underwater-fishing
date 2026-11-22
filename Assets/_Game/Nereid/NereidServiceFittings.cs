using System.Collections.Generic;
using Raid.Factory;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Render-only Quaternius service modules. The rebuilt vessel still owns its original
    // pressure barriers, work surfaces, routes, lamps and interaction anchors.
    internal sealed class NereidServiceFittings : MonoBehaviour
    {
        const string Folder = "NereidServiceFittings/";
        const string Vent = "Prop_Vent_Wide";
        const string Cable = "TopCables_Straight";
        const string Light = "Prop_Light_Wide";
        readonly Dictionary<string, GameObject> models = new();
        readonly List<Material> materials = new();
        readonly List<Mesh> meshes = new();
        Material enamel, steel, cabling, housing, grille, warmLens;
        Material classicShell, classicTrim, classicOchre, classicBronze;

        internal static void Install(Transform hull, NereidAirlock chamber)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            var group = Group(hull, "NEREID service fittings");
            var artist = group.gameObject.AddComponent<NereidServiceFittings>();
            artist.CreateMaterials(shader);
            artist.FitMainHall();
            artist.FitAftHall(hull);
            if (chamber != null) artist.FitAirlock(chamber.transform);
        }

        void CreateMaterials(Shader shader)
        {
            enamel = Metal(shader, "Petrol pressure enamel", new Color(.58f, .72f, .73f), .40f, .31f, "T_Trim_01_BaseColor");
            steel = Metal(shader, "Weathered service steel", new Color(.75f, .79f, .77f), .53f, .39f, "T_Trim_02_BaseColor");
            cabling = Metal(shader, "Petrol cable insulation", new Color(.43f, .62f, .65f), .06f, .22f, "T_Trim_03_Cables");
            housing = Metal(shader, "Pressure-rated ochre insulation", new Color(.63f, .59f, .42f), .12f, .25f, "T_PaddedWall_BaseColor");
            grille = Metal(shader, "Black intake backing", new Color(.075f, .11f, .125f), .18f, .24f);
            warmLens = Metal(shader, "Warm caged luminaire", new Color(.78f, .66f, .41f), .08f, .36f);
            warmLens.SetColor("_EmissionColor", new Color(1.04f, .64f, .28f));
            warmLens.EnableKeyword("_EMISSION");
            classicShell = Metal(shader, "Classic cast steel", new Color(.31f, .36f, .36f), .36f, .30f);
            classicTrim = Metal(shader, "Classic pressure panel edges", new Color(.12f, .17f, .18f), .47f, .31f);
            classicOchre = Metal(shader, "Classic ochre fitting", new Color(.55f, .32f, .13f), .29f, .32f);
            classicBronze = Metal(shader, "Classic inlet blades", new Color(.24f, .16f, .10f), .45f, .37f);
        }

        void FitMainHall()
        {
            var reservoir = new Bay(this, transform, "Reservoir upper service manifold");
            reservoir.Add("Prop_PipeHolder", new Vector3(0, 3.32f, -3.83f), new Vector3(2.6f, .41f, .77f), Quaternion.identity);
            foreach (int side in new[] { -1, 1 })
            {
                reservoir.Add("Column_Pipes", new Vector3(side * 1.34f, 3.51f, -3.77f),
                    new Vector3(.38f, .96f, .38f), Quaternion.Euler(0, side * 90, 0));
                var catwalk = new Bay(this, transform, side < 0 ? "Port service runs" : "Starboard service runs");
                foreach (float z in new[] { -6.6f, -1f, 4.6f })
                {
                    catwalk.Add(Light, new Vector3(side * 2.7f, 3.72f, z), new Vector3(1.10f, .15f, .39f), Quaternion.identity);
                    catwalk.Add(Cable, new Vector3(side * 5.76f, 3.94f, z), new Vector3(.29f, .94f, 3.20f),
                        Quaternion.Euler(0, side < 0 ? 180 : 0, 0));
                }
                catwalk.Build();
            }
            reservoir.Build();
        }

        void FitAftHall(Transform hull)
        {
            var bulkhead = new Bay(this, transform, "Aft workshop service bulkhead");
            bulkhead.Add(Cable, new Vector3(-3.56f, 2.70f, -15.90f), new Vector3(.31f, .83f, 3.28f), Quaternion.Euler(0, 90, 0));
            bulkhead.Add(Vent, new Vector3(3.8f, 1.76f, -15.90f), new Vector3(2.3f, .15f, .65f), Quaternion.Euler(90, 0, 0));
            bulkhead.Add("Details_Plate_Details", new Vector3(3.77f, 2.38f, -15.89f),
                new Vector3(.93f, .79f, .10f), Quaternion.identity);
            bulkhead.Add(Light, new Vector3(-2.5f, 3.18f, -11.5f), new Vector3(1.26f, .16f, .43f), Quaternion.identity);
            bulkhead.Build();

            var factory = FindAnyObjectByType<FactoryVisuals>();
            if (factory != null)
            {
                var machine = new Bay(this, factory.transform, "Processor service grilles");
                Vector3 scale = factory.processorScale;
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 position = factory.ProcessorPoint(new Vector3(-3.45f + side * 2.195f, 1.53f, -7f));
                    if (!machine.Add("Details_Vent_4", position, new Vector3(.62f * scale.z, .91f * scale.y, .105f * scale.x),
                        Quaternion.LookRotation(Vector3.right * side, Vector3.up)))
                        machine.Add("Prop_Vent_Small", position, new Vector3(.62f * scale.z, .105f * scale.x, .91f * scale.y),
                            Quaternion.LookRotation(Vector3.up, Vector3.right * side));
                }
                machine.Build();
            }

            foreach (var station in FindObjectsByType<ShipConsole>())
            {
                if (!station.IsMonitor || station.screenAnchor == null || station.transform.root.gameObject.scene != hull.gameObject.scene)
                    continue;
                var rear = new Bay(this, station.transform, "Instrument rear ventilation");
                float ratio = station.screenScale / .0018f;
                rear.Add("Prop_Vent_Small", station.screenAnchor.localPosition + new Vector3(0, 0, .47f),
                    new Vector3(1.40f * ratio, .105f, .39f * ratio), Quaternion.Euler(90, 0, 0));
                rear.Build();
            }
        }

        void FitAirlock(Transform chamber)
        {
            var bay = new Bay(this, chamber, "Pressure chamber service hardware");
            if (bay.Add(Vent, new Vector3(2.4f, .38f, 2.89f), new Vector3(1.48f, .17f, .58f), Quaternion.Euler(-90, 0, 0)))
            {
                HideChamberSkin(chamber, "Pump strainer");
                HideChamberSkin(chamber, "Strainer fin");
                // Move only the original moving gauge rotor to the face of the new grille.
                var rotor = chamber.Find("Pump rotor");
                if (rotor != null)
                    rotor.localPosition = new Vector3(2.4f, .38f, 2.790f);
            }
            for (int i = 0; i < 3; i++)
            {
                float x = 1.4f + i * 2.82f;
                if (bay.Add(Light, new Vector3(x, 3.91f, 0), new Vector3(1.47f, .12f, .28f), Quaternion.Euler(0, 90, 0)))
                    HideChamberSkin(chamber, "Inspection light", x);
            }
            bay.Add("RoofTile_Pipes1", new Vector3(4.22f, 4.03f, -1.68f), new Vector3(2.43f, .23f, 2.32f), Quaternion.Euler(180, 0, 0));
            bay.Add(Cable, new Vector3(3.95f, 3.57f, -3.02f), new Vector3(.23f, .67f, 4.6f), Quaternion.Euler(0, 90, 0));
            bay.Build();
        }

        static void HideChamberSkin(Transform chamber, string label, float x = float.NaN)
        {
            foreach (Transform child in chamber)
                if (child.name == label && (float.IsNaN(x) || Mathf.Abs(child.localPosition.x - x) < .01f) &&
                    child.TryGetComponent<MeshRenderer>(out var renderer))
                    renderer.enabled = false;
        }

        Material Metal(Shader shader, string name, Color color, float metallic, float smoothness, string atlas = null)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (atlas != null) material.SetTexture("_BaseMap", Resources.Load<Texture2D>(Folder + "Textures/" + atlas));
            materials.Add(material);
            return material;
        }

        Material Finish(Material source)
        {
            string label = source == null ? "" : source.name;
            if (label.StartsWith("MI_Trim_02")) return steel;
            if (label.StartsWith("MI_Trim_03_Cables")) return cabling;
            if (label.StartsWith("MI_Trim_03")) return grille;
            if (label.StartsWith("Q_Classic_Main")) return classicShell;
            if (label.StartsWith("Q_Classic_DarkGrey")) return classicTrim;
            if (label.StartsWith("Q_Classic_Accent")) return classicOchre;
            if (label.StartsWith("Q_Classic_DarkAccent")) return classicBronze;
            if (label.StartsWith("Q_Classic_Black")) return grille;
            if (label.StartsWith("MI_PaddedWall")) return housing;
            if (label.StartsWith("M_Black")) return grille;
            if (label.StartsWith("M_Light")) return warmLens;
            return enamel;
        }

        GameObject Model(string name)
        {
            if (!models.TryGetValue(name, out var model))
            {
                model = Resources.Load<GameObject>(Folder + "Models/" + name);
                models.Add(name, model);
            }
            return model;
        }

        static Transform Group(Transform parent, string name)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        void OnDestroy()
        {
            // Some fittings live under the resident machinery or chamber to inherit its scale.
            foreach (var mesh in meshes) Destroy(mesh);
            foreach (var material in materials) Destroy(material);
        }

        // Pool submissions per material and room. Keep neighboring rooms independently
        // culled and inspect the original mesh rather than bringing in any pack colliders.
        sealed class Bay
        {
            readonly NereidServiceFittings owner;
            readonly Transform root;
            readonly Dictionary<Material, List<CombineInstance>> pieces = new();

            internal Bay(NereidServiceFittings owner, Transform parent, string name)
            {
                this.owner = owner;
                root = Group(parent, name);
            }

            internal bool Add(string name, Vector3 center, Vector3 dimensions, Quaternion rotation)
            {
                var model = owner.Model(name);
                if (model == null) return false;
                var filters = model.GetComponentsInChildren<MeshFilter>();
                Bounds bounds = default;
                bool found = false;
                var modelSpace = model.transform.worldToLocalMatrix;
                foreach (var filter in filters)
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable || !filter.TryGetComponent<MeshRenderer>(out _)) continue;
                    var relative = modelSpace * filter.transform.localToWorldMatrix;
                    var min = mesh.bounds.min;
                    var max = mesh.bounds.max;
                    for (int i = 0; i < 8; i++)
                    {
                        var corner = relative.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? min.x : max.x,
                            (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
                        if (!found) { bounds = new Bounds(corner, Vector3.zero); found = true; }
                        else bounds.Encapsulate(corner);
                    }
                }
                if (!found) return false;

                var size = bounds.size;
                var factor = new Vector3(dimensions.x / Mathf.Max(.001f, size.x),
                    dimensions.y / Mathf.Max(.001f, size.y), dimensions.z / Mathf.Max(.001f, size.z));
                var placement = Matrix4x4.TRS(center, rotation, factor) * Matrix4x4.Translate(-bounds.center) * modelSpace;
                foreach (var filter in filters)
                {
                    var mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable || !filter.TryGetComponent<MeshRenderer>(out var renderer)) continue;
                    var slots = renderer.sharedMaterials;
                    var matrix = placement * filter.transform.localToWorldMatrix;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        var finish = owner.Finish(i < slots.Length ? slots[i] : null);
                        if (!pieces.TryGetValue(finish, out var list)) pieces.Add(finish, list = new List<CombineInstance>());
                        list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = matrix });
                    }
                }
                return true;
            }

            internal void Build()
            {
                foreach (var piece in pieces)
                {
                    var mesh = new Mesh { name = root.name + " / " + piece.Key.name, indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(piece.Value.ToArray(), true, true);
                    mesh.UploadMeshData(true);
                    owner.meshes.Add(mesh);
                    var group = Group(root, piece.Key.name);
                    group.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = group.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = piece.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }
    }
}
