using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    /// <summary>Renders the server-published manifest inside the moving, open-sided winch basket.</summary>
    public sealed class WetCargoShoal : MonoBehaviour
    {
        private sealed class Swimmer
        {
            public long id;
            public float phase, radius, height, speed;
            public Transform model;
        }

        private readonly List<Swimmer> swimmers = new();
        private readonly HashSet<long> ids = new();
        private static Material lensMaterial;
        private TeamProgress manifest;
        private RaidWorld world;

        public int Count => swimmers.Count;

        public static void Attach(RaidWorld world)
        {
            if (world.container == null) return;
            var basket = world.container.Find("RAID010 cargo basket");
            if (basket == null) return;
            var shoal = basket.GetComponent<WetCargoShoal>();
            if (shoal == null) shoal = basket.gameObject.AddComponent<WetCargoShoal>();
            shoal.world = world;
        }

        private void Awake()
        {
            var rim = transform.Find("Basket rim");
            var floor = transform.Find("Basket base");
            if (rim == null || floor == null) return;
            var mesh = rim.GetComponent<MeshFilter>().sharedMesh;
            var frame = floor.GetComponent<Renderer>().sharedMaterial;
            if (lensMaterial == null)
            {
                lensMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                    { name = "Winch / sealed cyan lens", hideFlags = HideFlags.DontSave };
                lensMaterial.SetColor("_BaseColor", new Color(.42f,.82f,.87f));
                lensMaterial.SetFloat("_Smoothness", .74f);
                lensMaterial.EnableKeyword("_EMISSION");
                lensMaterial.SetColor("_EmissionColor", new Color(.27f,1.05f,1.16f));
            }

            foreach (int side in new[]{-1,1})
            {
                Vector3 socket = new(side*.50f,.60f,side*1.074f);
                Fit("Recessed locator housing", mesh, frame, socket, new Vector3(.60f,.095f,.045f));
                Fit("Cage locator strip", mesh, lensMaterial, socket+Vector3.forward*(side*.029f), new Vector3(.49f,.037f,.013f));
            }

            Fit("Work lamp guard", mesh, frame, new Vector3(0,.65f,-.89f), new Vector3(.44f,.18f,.29f));
            Fit("Work lamp diffuser", mesh, lensMaterial, new Vector3(0,.553f,-.85f), new Vector3(.32f,.037f,.18f));
            var lamp = new GameObject("Cage work light").AddComponent<Light>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(0,.49f,-.83f);
            lamp.transform.localRotation = Quaternion.LookRotation(new Vector3(0,-.68f,.85f));
            lamp.type = LightType.Spot;
            lamp.color = new Color(.57f,.90f,.98f);
            lamp.intensity = 4.2f;
            lamp.range = 4.2f;
            lamp.spotAngle = 118;
            lamp.innerSpotAngle = 76;
            lamp.shadows = LightShadows.None;
        }

        private void Fit(string label, Mesh mesh, Material material, Vector3 position, Vector3 scale)
        {
            var part = new GameObject(label);
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void LateUpdate()
        {
            var state = world != null && world.IsClientInitialized ? world.View : null;
            if (state != manifest)
            {
                manifest = state;
                Refresh(state);
            }
            foreach (var fish in swimmers)
            {
                float a = Time.time * fish.speed + fish.phase;
                fish.model.localPosition = new Vector3(Mathf.Cos(a)*fish.radius,
                    fish.height + Mathf.Sin(a*2 + fish.phase)*.055f, Mathf.Sin(a)*fish.radius*.84f);
                Vector3 heading = new(-Mathf.Sin(a), .12f*Mathf.Cos(a*2+fish.phase), .84f*Mathf.Cos(a));
                fish.model.localRotation = Quaternion.LookRotation(heading.normalized, Vector3.up);
            }
        }

        private void Refresh(TeamProgress state)
        {
            ids.Clear();
            if (state != null)
                foreach (var item in state.cargo)
                    if (item.offering > 0 || !string.IsNullOrEmpty(item.species)) ids.Add(item.id);
            for (int i = swimmers.Count-1; i >= 0; i--)
            {
                if (ids.Remove(swimmers[i].id)) continue;
                Destroy(swimmers[i].model.gameObject);
                swimmers.RemoveAt(i);
            }
            if (state == null || ids.Count == 0) return;
            foreach (var item in state.cargo)
            {
                if (!ids.Contains(item.id)) continue;
                bool large = item.cargoSize > 1;
                string species = string.IsNullOrEmpty(item.species) ? large ? "Bristlefish" : "Lanternfish" : item.species;
                var model = CatchFishModel.Create(transform, species).transform;
                float phase = (item.id % 1009) / 1009f;
                model.localScale = Vector3.one * (large ? .53f : .41f);
                swimmers.Add(new Swimmer {
                    id=item.id, phase=phase*Mathf.PI*2, model=model,
                    radius=.34f+.20f*phase, height=-.25f+.52f*((item.id%41)/41f), speed=.9f+phase*.55f
                });
            }
        }
    }
}
