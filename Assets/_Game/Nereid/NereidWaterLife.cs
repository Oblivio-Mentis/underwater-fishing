using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    // Presentation for the one local eye. World-space leaves and specks move in their vertex shaders.
    [DefaultExecutionOrder(540)]
    public sealed class NereidWaterLife : MonoBehaviour
    {
        [SerializeField] Transform shallowBeds, deepBeds;
        [SerializeField] MeshRenderer[] clouds = new MeshRenderer[4];
        readonly List<MeshRenderer>[] groves = {new(),new()};
        readonly Vector4[] swimmers = new Vector4[5];
        [SerializeField] NereidWaterTuning tuning;
        [SerializeField] Material kelp, specks;
        [SerializeField] Vector3 firstHoldfast;
        public Vector3 FirstHoldfast => firstHoldfast;
        public bool ShowingSuspension => clouds[0] != null && clouds[0].enabled;
        public int VisibleGroves { get; private set; }

        bool runtimeMaterials;

        void Awake()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { enabled=false; return; }
            if (!shallowBeds || !deepBeds || !kelp || !specks || !tuning) { enabled=false; return; }
            shallowBeds.GetComponentsInChildren(true,groves[0]);
            deepBeds.GetComponentsInChildren(true,groves[1]);
            kelp = new Material(kelp);
            specks = new Material(specks);
            runtimeMaterials=true;
            foreach (var region in groves)
                foreach (var renderer in region) renderer.sharedMaterial=kelp;
            foreach (var cloud in clouds) if (cloud) cloud.sharedMaterial=specks;
            kelp.SetFloat("_BendRadius", tuning.bendRadius);
            kelp.SetFloat("_BendMetres", tuning.bendMetres);
        }

#if UNITY_EDITOR
        readonly System.Random random = new(14029);
        float Sample(float low, float high) => Mathf.Lerp(low, high, (float)random.NextDouble());

        internal void Build(MeshCollider shallowBed,MeshCollider deepBed)
        {
            tuning = Resources.Load<NereidWaterTuning>("NereidWaterTuning");
            kelp = MaterialFor("NereidTallKelp");
            specks = MaterialFor("NereidSuspension");
            if (!tuning || !kelp || !specks) { enabled = false; return; }
            kelp.SetFloat("_BendRadius", tuning.bendRadius);
            kelp.SetFloat("_BendMetres", tuning.bendMetres);
            shallowBeds=new GameObject("Laminaria beds").transform;
            shallowBeds.SetParent(shallowBed.transform.parent,false);
            deepBeds=new GameObject("Rift holdfasts").transform;
            deepBeds.SetParent(deepBed.transform.parent,false);
            foreach (var zone in tuning.groves) Plant(zone,shallowBed,false);
            foreach (var zone in tuning.deepGroves) Plant(zone,deepBed,true);

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
            if (!shader) { Debug.LogError("Unavailable water shader " + resource); return null; }
            var m = new Material(shader) {name = resource};
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

        void Plant(Vector4 zone,MeshCollider bottom,bool deep)
        {
            var centre = new Vector3(zone.x, 0, zone.y);
            var geometry = new ReefGeometry();
            var midribs = new List<Vector3>();
            int plants = 0;
            int count=deep?Mathf.Max(3,tuning.holdfastsPerBank/3):tuning.holdfastsPerBank;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < count; i++)
            {
                // Two broken banks leave a sightline along the grove rather than a solid wall.
                float x = zone.x + Sample(tuning.passageHalfWidth, zone.w)*side;
                float z = zone.y + zone.z*(2*(i+Sample(.05f,.95f))/count-1);
                if (NereidSalvage.Reserved(x,z) ||
                    !bottom.Raycast(new Ray(new Vector3(x,16,z), Vector3.down), out var hit, 60) ||
                    hit.normal.y < .72f) continue;
                if (!deep && firstHoldfast == Vector3.zero) firstHoldfast = hit.point;
                plants++;
                var root = hit.point-centre;
                float height = deep?Sample(1.6f,3.65f):Sample(tuning.bladeHeight.x,tuning.bladeHeight.y);
                float angle = Sample(0,6.283f);
                float warm = Sample(0,1);
                for (int j=0; j<(deep?2:3); j++)
                    Blade(geometry,midribs,root,height*(j==0?1:Sample(.63f,.91f)),angle+j*2.1f,warm,deep);
            }
            if (plants == 0) return;
            var mesh = geometry.Mesh("Ribbed laminaria bank");
            mesh.SetUVs(1, midribs);
            var bounds = mesh.bounds;
            bounds.Expand(2*(tuning.bendMetres+.5f));
            mesh.bounds = bounds;
            var grove=Draw(deep?"Sporadic rift holdfasts":"Tall laminaria grove",mesh,kelp,centre);
            grove.transform.SetParent(deep?deepBeds:shallowBeds,true);
            grove.enabled=true;
        }

        void Blade(ReefGeometry g,List<Vector3> midribs,Vector3 root,float height,float angle,float warm,bool deep)
        {
            var heading = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            float wide = deep?Sample(.095f,.18f):Sample(.21f,.39f);
            var shade = deep?Color.Lerp(new Color(.046f,.131f,.139f),new Color(.109f,.139f,.125f),warm):
                Color.Lerp(new Color(.048f,.195f,.102f),new Color(.225f,.208f,.064f),warm);
            const int segments = 17;
            for (int row=0; row<=segments; row++)
            {
                float h = row/(float)segments;
                float blade = Mathf.SmoothStep(0, 1, (h-.12f)*5);
                float shoulder=Mathf.Sin(h*Mathf.PI)*(1+.19f*Mathf.Sin(h*31+angle));
                float rip=(deep?.26f:.11f)*Mathf.Pow(Mathf.Max(0,Mathf.Sin(h*24+angle)),4);
                float breadth = wide*(.037f+blade*shoulder*(1-rip));
                var across = Quaternion.AngleAxis(46*Mathf.Sin(6*h+angle),Vector3.up)*Vector3.Cross(Vector3.up,heading);
                var mid = root+Vector3.up*height*(h-.14f*h*h*h)+heading*height*(.07f*h+.30f*h*h*h);
                mid += across*(height*.078f*Mathf.Sin(h*8+angle)*h);
                var col = Color.Lerp(shade*.41f,shade,h)*Mathf.Lerp(.84f,1.09f,.5f+.5f*Mathf.Sin(h*43+angle*.4f));
                col.a = h; // Bend weight; the holdfast stays attached to its rock.
                var rib = col; rib.r *= 1.45f; rib.g *= 1.29f;
                float ripple = .069f*blade*Mathf.Sin(h*44+angle);
                int a = g.Add(mid-across*breadth+heading*ripple,col);
                g.Add(mid+heading*breadth*.34f,rib);
                g.Add(mid+across*breadth*(1+.19f*Mathf.Sin(row+angle))-heading*ripple,col);
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
            return mesh;
        }

#endif

        void LateUpdate()
        {
            var look = UnderwaterLook.Instance;
            var eye = look != null ? look.DiverCamera : null;
            bool wet = eye != null && eye.isActiveAndEnabled && look.EyeSubmerged;
            bool deep=RaidWorld.Instance!=null && RaidWorld.Instance.CurrentDepth>=2;
            VisibleGroves = 0;
            for(int region=0;region<groves.Length;region++)
            foreach(var r in groves[region])
            {
                float range=deep?tuning.kelpVisibility*.72f:tuning.kelpVisibility;
                r.enabled=wet && region==(deep?1:0) && Vector3.SqrMagnitude(r.bounds.ClosestPoint(eye.transform.position)-eye.transform.position)<range*range;
                if(r.enabled)VisibleGroves++;
            }
            for (int i=0; i<clouds.Length; i++)
            {
                var r = clouds[i];
                if(!r)continue;
                r.enabled = wet && (i==0 || Vector3.SqrMagnitude(r.transform.position-eye.transform.position)<28*28);
            }
            if (wet)
            {
                clouds[0].transform.position = eye.transform.position;
                specks.SetFloat("_Density",deep?tuning.deepDensity:tuning.shallowDensity);
                specks.SetFloat("_DeepWater",deep?1:0);
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
            foreach(var region in groves)
            foreach(var r in region)
                if(r)r.enabled=false;
            foreach (var r in clouds) if (r) r.enabled = false;
            VisibleGroves = 0;
        }

        void OnDestroy()
        {
            if (runtimeMaterials) { Destroy(kelp); Destroy(specks); }
        }
    }
}
