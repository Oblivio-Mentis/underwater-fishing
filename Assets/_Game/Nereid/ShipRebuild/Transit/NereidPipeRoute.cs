using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public enum PipeLine { Inlet, Process, Feed }
    public enum PipeSection { Glass, Metal, ElbowFlange, Hanger, Valve }

    [Serializable]
    public struct PipeKnot
    {
        public Vector3 position;
        public bool manualTangents;
        public Vector3 incoming, outgoing;
        public PipeSection section;
        public Material materialOverride;
    }

    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class NereidPipeRoute : MonoBehaviour
    {
        public PipeLine line;
        public PipeSocket source, destination;
        public List<PipeKnot> knots = new();
        [Min(.03f)] public float radius = .42f;
        [Range(0,1)] public float smoothing = .8f;
        [Range(3,48)] public int curveSteps = 12;
        [Range(6,40)] public int circumference = 16;
        public Material sightGlass, metal, enamel;

        [Header("Outlet isolation gate")]
        public Transform valveFrame, portLeaf, starboardLeaf;
        [Range(0,1)] public float valveAt=.95f;
        [Min(.05f)] public float valveSeconds=.32f;
        float valveOpening;

        [NonSerialized] Mesh skin;
        readonly List<Vector3> samples = new();
        readonly List<float> distances = new();
        Vector3 previousStart, previousEnd;
        bool pending = true;
        MaterialPropertyBlock flow;
        MeshRenderer shell;
        PipeInteriorWater interiorWater;
        static readonly int FlowActive = Shader.PropertyToID("_FlowActive");
        public bool IsConnected => source && destination && knots.Count >= 2;
        public float Length { get { EnsureCurrent(); return distances.Count == 0 ? 0 : distances[^1]; } }

        public Vector3 LocalPoint(int i)
        {
            if (i == 0 && source) return transform.InverseTransformPoint(source.transform.position);
            if (i == knots.Count-1 && destination) return transform.InverseTransformPoint(destination.transform.position);
            return knots[i].position;
        }

        public Vector3 Tangent(int i, bool outgoing)
        {
            var k = knots[i];
            if (k.manualTangents) return outgoing ? k.outgoing : k.incoming;
            Vector3 here = LocalPoint(i);
            Vector3 previous = LocalPoint(Mathf.Max(0,i-1));
            Vector3 next = LocalPoint(Mathf.Min(knots.Count-1,i+1));
            return (next-previous).normalized * ((outgoing ? Vector3.Distance(here,next) : -Vector3.Distance(previous,here)) * smoothing / 3f);
        }

        public Vector3 WorldPoint(float fraction)
        {
            EnsureCurrent();
            if (samples.Count == 0) return transform.position;
            float distance = Mathf.Clamp01(fraction) * distances[^1];
            int i = distances.BinarySearch(distance);
            if (i >= 0) return transform.TransformPoint(samples[i]);
            i = ~i;
            if (i <= 0 || i >= samples.Count) return transform.TransformPoint(samples[Mathf.Clamp(i,0,samples.Count-1)]);
            return transform.TransformPoint(Vector3.Lerp(samples[i-1], samples[i], Mathf.InverseLerp(distances[i-1], distances[i], distance)));
        }

        void OnValidate() => pending = true;
        void OnEnable() => pending = true;
        void Update() => EnsureCurrent();

        void EnsureCurrent()
        {
            if (knots.Count < 2)
            {
                if (samples.Count != 0 || pending) Rebuild();
                return;
            }
            if (pending || (previousStart-LocalPoint(0)).sqrMagnitude > .000001f ||
                (previousEnd-LocalPoint(knots.Count-1)).sqrMagnitude > .000001f) Rebuild();
        }

        public void Rebuild()
        {
            pending = false;
            samples.Clear();
            distances.Clear();
            if (!skin)
            {
                skin = new Mesh { name = "Pressure line · " + line, hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
                GetComponent<MeshFilter>().sharedMesh = skin;
            }
            skin.Clear();
            if (knots.Count < 2) return;
            previousStart = LocalPoint(0);
            previousEnd = LocalPoint(knots.Count-1);
            samples.Add(previousStart);
            distances.Add(0);
            int steps = Mathf.Clamp(curveSteps, 3, 48);
            for (int i = 0; i + 1 < knots.Count; i++)
            {
                Vector3 a = LocalPoint(i), d = LocalPoint(i+1), b = a + Tangent(i,true), c = d + Tangent(i+1,false);
                for (int j = 1; j <= steps; j++)
                {
                    float t = (float)j / steps, u = 1-t;
                    var p = u*u*u*a + 3*u*u*t*b + 3*u*t*t*c + t*t*t*d;
                    distances.Add(distances[^1] + Vector3.Distance(p,samples[^1]));
                    samples.Add(p);
                }
            }
            var build = new PipeSkin(Mathf.Max(.03f,radius), Mathf.Clamp(circumference,6,40), metal, enamel);
            for (int i = 0; i+1 < knots.Count; i++)
            {
                var k = knots[i];
                Material surface = k.materialOverride ? k.materialOverride : (k.section == PipeSection.Metal ? metal : sightGlass);
                build.Section(samples, i*steps, steps, surface, k.section);
            }
            shell = GetComponent<MeshRenderer>();
            shell.sharedMaterials = build.Apply(skin);
            shell.shadowCastingMode = ShadowCastingMode.Off;
            PositionValve();
            interiorWater?.Rebuild(samples,distances,radius);
        }

        void PositionValve()
        {
            if(!valveFrame || samples.Count<2)return;
            var at=WorldPoint(valveAt);
            var heading=WorldPoint(Mathf.Min(1,valveAt+.012f))-WorldPoint(Mathf.Max(0,valveAt-.012f));
            if(heading.sqrMagnitude>.00001f)valveFrame.SetPositionAndRotation(at,Quaternion.LookRotation(heading,Mathf.Abs(Vector3.Dot(heading.normalized,Vector3.up))>.92f?Vector3.forward:Vector3.up));
            valveFrame.localScale=Vector3.one*radius;
        }

        public void SetFlow(float amount)
        {
            if(valveFrame && portLeaf && starboardLeaf)
            {
                valveOpening=Mathf.MoveTowards(valveOpening,amount>.04f?1:0,Time.deltaTime/Mathf.Max(.05f,valveSeconds));
                float distance=Mathf.SmoothStep(0,1, valveOpening)*1.08f;
                portLeaf.localPosition=Vector3.left*distance;starboardLeaf.localPosition=Vector3.right*distance;
            }
            if (!shell) return;
            flow ??= new MaterialPropertyBlock();
            shell.GetPropertyBlock(flow);
            flow.SetFloat(FlowActive,amount);
            shell.SetPropertyBlock(flow);
        }

        internal void PresentInteriorWater(float head,float tail,float pressure)
        {
            if(pressure<=.01f || head<=tail+.001f)
            {
                interiorWater?.Hide();
                return;
            }
            EnsureCurrent();
            if(samples.Count<2)return;
            if(interiorWater==null)
            {
                interiorWater=new PipeInteriorWater(transform);
                interiorWater.Rebuild(samples,distances,radius);
            }
            interiorWater.Present(head,tail,pressure);
        }

        void OnDisable()
        {
            interiorWater?.Dispose();
            interiorWater=null;
            if (!skin) return;
            var filter=GetComponent<MeshFilter>();
            if (filter && filter.sharedMesh == skin) filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(skin); else DestroyImmediate(skin);
            skin = null;
        }
    }
}

