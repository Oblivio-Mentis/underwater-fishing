using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Presentation for the one local eye. World-space leaves and specks move in their vertex shaders.
    [DefaultExecutionOrder(540)]
    public sealed class NereidWaterLife : MonoBehaviour
    {
        readonly List<Object> owned = new();
        readonly List<MeshRenderer> groves = new();
        readonly MeshRenderer[] clouds = new MeshRenderer[4];
        readonly Vector4[] swimmers = new Vector4[5];
        readonly System.Random random = new(14029);
        NereidWaterTuning tuning;
        Material kelp, specks;
        public Vector3 FirstHoldfast { get; private set; }
        public bool ShowingSuspension => clouds[0] != null && clouds[0].enabled;
        public int VisibleGroves { get; private set; }

        float Sample(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

        internal void Build(MeshCollider bottom)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { enabled = false; return; }
            tuning = Resources.Load<NereidWaterTuning>("NereidWaterTuning");
            kelp = MaterialFor("NereidTallKelp");
            specks = MaterialFor("NereidSuspension");
            if (!tuning || !kelp || !specks) { enabled = false; return; }
            kelp.SetFloat("_BendRadius", tuning.bendRadius);
            kelp.SetFloat("_BendMetres", tuning.bendMetres);
            foreach (var zone in tuning.groves) Plant(zone, bottom);

            var nearMesh = FlakeMesh(tuning.localFlakes, tuning.localExtent);
            clouds[0] = Draw("Drifting organic fines around mask", nearMesh, specks, Vector3.zero);
            var farMesh = FlakeMesh(tuning.lampFlakes, tuning.lampExtent);
            Vector3[] positions = {new(22,-10,-28), new(17,-11,-15), new(19,-11,-40)};
            for (int i = 0; i < positions.Length; i++)
                clouds[i+1] = Draw("Suspension beneath work lamp", farMesh, specks, positions[i]);
        }

        Material MaterialFor(string resource)
        {
            var shader = Resources.Load<Shader>(resource);
            if (!shader || !shader.isSupported) { Debug.LogError("Unavailable water shader " + resource); return null; }
            var m = new Material(shader) {name = resource};
            owned.Add(m);
            return m;
        }

        MeshRenderer Draw(string label, Mesh mesh, Material material, Vector3 at)
        {
            var node = new GameObject(label);
            node.transform.SetParent(transform, false);
            node.transform.position = at;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false;
            return renderer;
        }

        void Plant(Vector4 zone, MeshCollider bottom)
        {
            var centre = new Vector3(zone.x, 0, zone.y);
            var geometry = new ReefGeometry();
            var midribs = new List<Vector3>();
            int plants = 0;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < tuning.holdfastsPerBank; i++)
            {
                // Two broken banks leave a sightline along the grove rather than a solid wall.
                float x = zone.x + Sample(tuning.passageHalfWidth, zone.w)*side;
                float z = zone.y + zone.z*(2*(i+Sample(.05f,.95f))/tuning.holdfastsPerBank-1);
                if (NereidSalvage.Reserved(x,z) ||
                    !bottom.Raycast(new Ray(new Vector3(x,16,z), Vector3.down), out var hit, 60) ||
                    hit.normal.y < .72f) continue;
                if (groves.Count == 0 && plants == 0) FirstHoldfast = hit.point;
                plants++;
                var root = hit.point-centre;
                float height = Sample(tuning.bladeHeight.x,tuning.bladeHeight.y);
                float angle = Sample(0,6.283f);
                float warm = Sample(0,1);
                for (int j=0; j<3; j++)
                    Blade(geometry, midribs, root, height*(j==0?1:Sample(.65f,.92f)), angle+j*2.1f, warm);
            }
            if (plants == 0) return;
            var mesh = geometry.Mesh("Ribbed laminaria bank");
            mesh.SetUVs(1, midribs);
            var bounds = mesh.bounds;
            bounds.Expand(2*(tuning.bendMetres+.5f));
            mesh.bounds = bounds;
            owned.Add(mesh);
            groves.Add(Draw("Tall laminaria grove", mesh, kelp, centre));
        }

        void Blade(ReefGeometry g, List<Vector3> midribs, Vector3 root, float height, float angle, float warm)
        {
            var heading = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            float wide = Sample(.18f,.30f);
            var shade = Color.Lerp(new Color(.041f,.15f,.086f),new Color(.16f,.19f,.061f),warm);
            const int segments = 17;
            for (int row=0; row<=segments; row++)
            {
                float h = row/(float)segments;
                float blade = Mathf.SmoothStep(0, 1, (h-.12f)*5);
                float breadth = wide*(.045f+blade*Mathf.Sin(h*Mathf.PI)*(1+.15f*Mathf.Sin(h*33+angle)));
                var across = Quaternion.AngleAxis(46*Mathf.Sin(6*h+angle),Vector3.up)*Vector3.Cross(Vector3.up,heading);
                var mid = root+Vector3.up*height*(h-.14f*h*h*h)+heading*height*(.07f*h+.30f*h*h*h);
                mid += across*(height*.078f*Mathf.Sin(h*8+angle)*h);
                var col = Color.Lerp(shade*.40f,shade,h);
                col.a = h; // Bend weight; the holdfast stays attached to its rock.
                var rib = col; rib.r *= 1.42f; rib.g *= 1.24f;
                float ripple = .055f*blade*Mathf.Sin(h*42+angle);
                int a = g.Add(mid-across*breadth+heading*ripple, col);
                g.Add(mid+heading*breadth*.34f, rib);
                g.Add(mid+across*breadth*(1+.12f*Mathf.Sin(row+angle))-heading*ripple,col);
                for (int i=0; i<3; i++) midribs.Add(mid);
                if (row == 0) continue;
                g.Quad(a-3,a,a+1,a-2);
                g.Quad(a-2,a+1,a+2,a-1);
            }
        }

        Mesh FlakeMesh(int count, float extent)
        {
            var pos = new Vector3[count*4];
            var uv = new Vector2[count*4];
            var shape = new Vector2[count*4];
            var idx = new int[count*6];
            for (int i=0; i<count; i++)
            {
                var seed = new Vector3(Sample(0,1),Sample(0,1),Sample(0,1));
                var radius = new Vector2(Sample(.004f,.014f),extent);
                for (int j=0; j<4; j++)
                {
                    pos[i*4+j] = seed;
                    uv[i*4+j] = new Vector2(j==0||j==3?-1:1, j<2?-1:1);
                    shape[i*4+j] = radius;
                }
                int k = i*6, v = i*4;
                idx[k]=v; idx[k+1]=v+1; idx[k+2]=v+2; idx[k+3]=v; idx[k+4]=v+2; idx[k+5]=v+3;
            }
            var mesh = new Mesh {name="Suspended detritus", vertices=pos, uv=uv, uv2=shape, triangles=idx,
                bounds=new Bounds(Vector3.zero,Vector3.one*(extent+.2f))};
            owned.Add(mesh);
            return mesh;
        }

        void LateUpdate()
        {
            var look = UnderwaterLook.Instance;
            var eye = look != null ? look.DiverCamera : null;
            bool wet = eye != null && eye.isActiveAndEnabled && look.EyeSubmerged;
            VisibleGroves = 0;
            foreach (var r in groves)
            {
                r.enabled = eye != null && Vector3.SqrMagnitude(r.bounds.ClosestPoint(eye.transform.position)-eye.transform.position)
                    < tuning.kelpVisibility*tuning.kelpVisibility;
                if (r.enabled) VisibleGroves++;
            }
            for (int i=0; i<clouds.Length; i++)
            {
                var r = clouds[i];
                r.enabled = wet && (i==0 || Vector3.SqrMagnitude(r.transform.position-eye.transform.position)<28*28);
            }
            if (wet)
            {
                clouds[0].transform.position = eye.transform.position;
                specks.SetFloat("_Density",RaidWorld.Instance != null && RaidWorld.Instance.CurrentDepth>=2
                    ? tuning.deepDensity : tuning.shallowDensity);
                var lockRoom = NereidAirlock.Active;
                specks.SetFloat("_Waterline", lockRoom != null && lockRoom.Contains(eye.transform.position)
                    ? lockRoom.WaterHeight : 10000f);
            }
            if (VisibleGroves == 0) return;
            var root = UI.UiRoot.Instance;
            int n = 0;
            if (root != null)
            {
                foreach (var d in root.Context.Players)
                {
                    if (!d || d.IsInside || d.IsDead) continue;
                    var p = d.transform.position;
                    swimmers[n++] = new Vector4(p.x,p.y,p.z,1);
                    if (n == swimmers.Length) break;
                }
            }
            for (int i=n; i<swimmers.Length; i++) swimmers[i] = Vector4.zero;
            kelp.SetVectorArray("_KelpSwimmers",swimmers);
        }

        void OnDisable()
        {
            foreach (var r in groves) if (r) r.enabled = false;
            foreach (var r in clouds) if (r) r.enabled = false;
            VisibleGroves = 0;
        }

        void OnDestroy()
        {
            foreach (var asset in owned) if (asset) Destroy(asset);
        }
    }
}
