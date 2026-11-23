using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    /// <summary>Shared silhouettes for live fish, held catches and the shoal inside the recovery cage.</summary>
    public sealed class CatchFishModel : MonoBehaviour
    {
        private sealed class Shape
        {
            public Mesh body, tail;
            public Vector3[] restVertices;
            public Material[] colors, wildColors, catchColors;
        }
        // Procedural spine: a travelling wave from head to tail plus a C-bend into the turn.
        // The collider keeps the rest mesh; the harpoon anchor is displaced by the same function
        // on every peer (see HarpoonContact.ResolveOn), so the bolt rides the bent body.
        private Shape shape;
        private Mesh spineMesh;
        private Vector3[] spineWork;
        private float wavePhase, waveAmplitude, bendCurve, yawRate, tailSlope;
        private Vector3 lastHeading, lastAnchorForBend;
        private bool hasHeading;
        private const float HeadZ = .52f, TailZ = -.47f;

        private static readonly Dictionary<string, Shape> Shapes = new();
        private Transform tail;
        public Transform Tail => tail;
        public long CargoId { get; internal set; }
        private float phase, strokeWeight, speedWeight, hitRecoil;
        private int seenHook;
        private Vector3 lastPosition;
        private bool ray;
        private FishActor actor;
        private float escapeAt = -1;
        private Vector3 escapeDirection;

        public static bool Supports(string species) => species == "Lanternfish" || species == "Bristlefish"
            || species == "Needlefish" || species == "Sailray";

        public static GameObject Create(Transform parent, string species, long cargoId = 0)
        {
            if (!Supports(species)) species = "Lanternfish";
            if (!Shapes.TryGetValue(species, out var shape) || shape.body == null)
                Shapes[species] = shape = Build(species);
            var live=parent==null?null:parent.GetComponent<FishActor>();
            var finish=live!=null?shape.wildColors: cargoId>0?shape.catchColors:null;
            finish ??= shape.colors;
            var body = MakePart(species, parent, shape.body, finish);
            var model = body.AddComponent<CatchFishModel>();
            model.shape = shape;
            model.tail = MakePart("Caudal fin", body.transform, shape.tail, finish).transform;
            model.tail.localPosition = Vector3.back * .43f;
            model.CargoId = cargoId;
            model.phase = cargoId>0?(cargoId%8191)/8191f*Mathf.PI*2:Random.value * 6.28f;
            model.actor = live;
            model.ray = species == "Sailray";
            return body;
        }

        public static void Release(string species, Vector3 position, Quaternion facing)
        {
            var model = Create(null, species).GetComponent<CatchFishModel>();
            model.escapeAt = Time.time;
            model.escapeDirection = facing * new Vector3(Random.Range(-.6f,.6f),.18f,1).normalized;
            model.transform.SetPositionAndRotation(position + model.escapeDirection*.55f, Quaternion.LookRotation(model.escapeDirection));
        }

        private static GameObject MakePart(string label, Transform parent, Mesh mesh, Material[] materials)
        {
            var part = new GameObject(label);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return part;
        }

        public Vector3 Displace(Vector3 local)
        {
            float t = Mathf.Clamp01((HeadZ - local.z) / (HeadZ - TailZ));
            float envelope = .08f + t * t * (.3f + .7f * t);
            float wave = Mathf.Sin(wavePhase - t * 3.1f) * waveAmplitude * envelope;
            float bend = bendCurve * t * t;
            if (ray)
            {
                // A ray ripples its wings vertically and barely bends its short body.
                local.y += Mathf.Sin(wavePhase - Mathf.Abs(local.x) * 2.4f - t * 1.2f) * waveAmplitude * .9f * Mathf.Clamp01(Mathf.Abs(local.x) * 1.4f);
                local.x += wave * .25f + bend * .6f;
                return local;
            }
            local.x += wave + bend;
            // Pectoral fins flap up and down out of phase with the tail beat.
            float fin = Mathf.Clamp01((Mathf.Abs(local.x) - .22f) / .18f);
            if (fin > 0) local.y += Mathf.Sin(wavePhase * 1.15f + Mathf.Sign(local.x) * .6f) * (.03f + waveAmplitude * .35f) * fin;
            return local;
        }

        private void DeformSpine()
        {
            if (shape == null || shape.body == null) return;
            if (spineMesh == null)
            {
                var filter = GetComponent<MeshFilter>();
                shape.restVertices ??= shape.body.vertices;
                spineMesh = Instantiate(shape.body);
                spineMesh.name = shape.body.name + " / spine";
                spineMesh.hideFlags = HideFlags.DontSave;
                spineMesh.MarkDynamic();
                spineWork = new Vector3[shape.restVertices.Length];
                filter.sharedMesh = spineMesh;
            }
            var rest = shape.restVertices;
            for (int i = 0; i < rest.Length; i++) spineWork[i] = Displace(rest[i]);
            spineMesh.vertices = spineWork;
            spineMesh.RecalculateNormals();
            spineMesh.RecalculateBounds();
            // The hinged caudal fin sits on the displaced spine end and follows its slope.
            Vector3 root = Displace(Vector3.back * .43f), ahead = Displace(Vector3.back * .33f);
            tail.localPosition = root;
            tailSlope = Mathf.Atan2(ahead.x - root.x, ahead.z - root.z) * Mathf.Rad2Deg;
        }

        private void LateUpdate()
        {
            if (actor != null && (!actor.InHabitat || actor.IsStowed || actor.Mode==FishMode.Transported)) return;
            if (escapeAt >= 0)
            {
                float age = Time.time - escapeAt;
                if (age >= 2.3f) { Destroy(gameObject); return; }
                transform.position += escapeDirection * ((2.2f + Mathf.Sin(age*6)*.15f)*Time.deltaTime);
                transform.localScale = Vector3.one * (.65f*Mathf.Clamp01(2.3f-age));
            }
            bool burst = escapeAt >= 0 || actor != null && (actor.Mode == FishMode.Struggling || actor.Mode == FishMode.Escaped || actor.Startled);
            // Actual travel speed drives tail frequency and amplitude; a hover barely moves, a surge whips.
            float dt=Mathf.Max(Time.deltaTime,.0001f);
            Vector3 anchor=transform.parent==null?transform.position:transform.parent.position;
            float travel=Vector3.Distance(anchor,lastPosition)/dt;
            lastPosition=anchor;
            if(actor!=null)
            {
                var behaviour=actor.Behaviour;
                float wanted=Mathf.InverseLerp(.25f,4.5f,travel);
                if(behaviour==FishBehaviour.Hover||behaviour==FishBehaviour.Weakened)wanted*=.55f;
                if(behaviour==FishBehaviour.Surge||behaviour==FishBehaviour.Flee||behaviour==FishBehaviour.LastEscape)wanted=Mathf.Max(wanted,.85f);
                speedWeight=Mathf.MoveTowards(speedWeight,Mathf.Clamp01(wanted),dt*4f);
                if(actor.HookSequence!=seenHook){seenHook=actor.HookSequence;if(seenHook>0)hitRecoil=1;}
            }
            hitRecoil=Mathf.MoveTowards(hitRecoil,0,dt*4.2f);
            // The C-bend follows the curvature of the actual path, not the object's yaw: a fish
            // that is pulled sideways by a line bends into that drift, and a snapped rotation
            // from the network does not kink the body for a frame.
            Vector3 travelDir = anchor - lastAnchorForBend;
            float travelLen = travelDir.magnitude;
            if (travelLen > .004f)
            {
                travelDir /= travelLen;
                if (hasHeading)
                {
                    float signed = Vector3.SignedAngle(lastHeading, travelDir, Vector3.up);
                    float rate = Mathf.Clamp(signed / Mathf.Max(dt, .008f), -360, 360);
                    yawRate = Mathf.Lerp(yawRate, rate, 1 - Mathf.Exp(-dt * 6));
                }
                lastHeading = Vector3.Slerp(hasHeading ? lastHeading : travelDir, travelDir, 1 - Mathf.Exp(-dt * 12));
                hasHeading = true;
                lastAnchorForBend = anchor;
            }
            else yawRate = Mathf.Lerp(yawRate, 0, 1 - Mathf.Exp(-dt * 4));
            float clock=Time.time, offset=phase;
            if(actor!=null && (actor.IsServerInitialized || actor.IsClientInitialized))
            {
                // All peers articulate the collider and the fin holding the bolt at the same network phase.
                var timer=actor.TimeManager;
                clock=(float)(timer.TicksToTime(timer.Tick)+timer.GetTickElapsedAsDouble());
                offset=actor.ObjectId*2.39996f;
            }
            // The head leads each stroke; the hinged caudal fin follows out of phase.
            // Animate the meshes that own the hit colliders so an attached bolt rides
            // the same moving surface for the angler and observers.
            strokeWeight=Mathf.MoveTowards(strokeWeight,Mathf.Max(burst?1f:0f,speedWeight),Time.deltaTime*6f);
            float cruise=clock*(ray?3.7f:7.1f)+offset, sprint=clock*14f+offset;
            float lead=Mathf.Lerp(Mathf.Sin(cruise),Mathf.Sin(sprint),strokeWeight);
            float amplitude=Mathf.Lerp(.55f,1f,Mathf.Max(strokeWeight,speedWeight));
            float whip=Mathf.Lerp(Mathf.Sin(cruise-.9f)*25f,Mathf.Sin(sprint-.9f)*39f,strokeWeight)*amplitude;
            // Hook shock: a short asymmetric flinch of the whole tail, then back to the swim stroke.
            float flinch=hitRecoil>0?Mathf.Sin(hitRecoil*Mathf.PI)*Mathf.Sin(hitRecoil*22f)*28f:0;
            // Spine wave: faster and larger with effort; bend follows the measured turn rate.
            wavePhase = Mathf.Lerp(cruise, sprint, strokeWeight) - .9f;
            // Readable at a glance: a resting Lanternfish swings its tail root ~6 cm, a bolting one ~16 cm.
            float effort = Mathf.Max(strokeWeight, speedWeight);
            waveAmplitude = Mathf.Lerp(ray ? .05f : .075f, ray ? .11f : .19f, effort) + Mathf.Abs(flinch) * .003f;
            float targetBend = Mathf.Clamp(-yawRate / 110f, -1, 1) * (ray ? .18f : .30f) * Mathf.Clamp01(travel / .6f);
            bendCurve = Mathf.Lerp(bendCurve, targetBend, 1 - Mathf.Exp(-dt * 7));
            DeformSpine();
            tail.localRotation=Quaternion.Euler(ray?whip*.52f:0,(ray?whip*.42f:whip)*.55f+flinch+tailSlope,0);
            if(actor!=null)
            {
                // The body itself stays still: the spine wave and the tail carry the stroke.
                // Only a slow roll remains, so the silhouette never vibrates against the spine.
                float bank=ray?4f:1.6f;
                transform.localRotation=Quaternion.Euler(-flinch*.12f,flinch*.25f,Mathf.Sin(cruise*.5f-.6f)*bank+flinch*.3f);
            }
        }

        private void OnDestroy() { if (spineMesh != null) Destroy(spineMesh); }

        private static Shape Build(string species)
        {
            bool ray = species == "Sailray", needle = species == "Needlefish", bristle = species == "Bristlefish";
            // Back, pale lateral stripe, belly, translucent-looking fins and photophores.
            var palette = ray ? new[] {"263D61","4B90AC","9ECAC2","3D6381","BEFFE7"}
                : needle ? new[] {"144749","ABD567","B2CEAE","398464","FFE398"}
                : bristle ? new[] {"57362E","D88A45","CBAD79","9D5537","FFD571"}
                : new[] {"214C60","56C4CA","ACD8CD","39798C","9BFFF2"};
            var colors = new Material[palette.Length];
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            // Weak reflected water color on the back, stronger along the iridescent flank and pale underside.
            // Direct URP lighting still supplies facet shading, highlights, and the moving work-light response.
            float[] shadowFill = { .10f, .34f, .19f, .16f, 1.35f };
            for (int i = 0; i < colors.Length; i++)
            {
                ColorUtility.TryParseHtmlString("#" + palette[i], out var color);
                var material = new Material(shader) { name = species + " / " + i, hideFlags = HideFlags.DontSave };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Smoothness", i == 4 ? .8f : .48f);
                material.EnableKeyword("_EMISSION");
                Color reflected = i == 4 ? color : Color.Lerp(color, new Color(.17f,.46f,.54f), i == 0 ? .45f : .14f);
                material.SetColor("_EmissionColor", reflected * shadowFill[i]);
                colors[i] = material;
            }

            var wetShader=Resources.Load<Shader>("NereidFishSkin");
            Material[] wild=null,loaded=null;
            if(wetShader!=null)
            {
                wild=new Material[colors.Length];loaded=new Material[colors.Length];
                for(int i=0;i<wild.Length;i++)
                {
                    var lit=colors[i];
                    var waterSkin=new Material(wetShader){name=species+" / wet scales "+i,hideFlags=HideFlags.DontSave};
                    var albedo=lit.GetColor("_BaseColor");
                    waterSkin.SetColor("_BaseColor",albedo);
                    waterSkin.SetColor("_RimColor",Color.Lerp(albedo,new Color(.60f,.81f,.81f),.56f));
                    waterSkin.SetColor("_EmissionColor",lit.GetColor("_EmissionColor"));
                    waterSkin.SetFloat("_Smoothness",lit.GetFloat("_Smoothness"));
                    waterSkin.SetFloat("_PatternStrength",i<3?(ray?.78f:.36f):0);
                    wild[i]=waterSkin;
                    loaded[i]=new Material(waterSkin){name=species+" / transported scales "+i,hideFlags=HideFlags.DontSave};
                    loaded[i].SetFloat("_CargoLighting",1);
                }
            }

            var body = new Geometry();
            Vector3[] rings = {
                new(-.47f,.035f,.04f), new(-.35f,.11f,.12f), new(-.15f,.21f,.22f),
                new(.1f,.23f,.24f), new(.30f,.19f,.19f), new(.46f,.105f,.09f), new(.52f,.015f,.014f)
            };
            float width = ray ? 1.36f : needle ? .56f : bristle ? 1.21f : 1f;
            float height = ray ? .31f : needle ? .54f : bristle ? 1.18f : .89f;
            for (int ring = 0; ring < rings.Length; ring++)
            {
                rings[ring].y *= width;
                rings[ring].z *= height;
                if (needle && ring >= 5) rings[ring] = new Vector3(ring == 5 ? .5f : .78f, ring == 5 ? .027f : .006f, .012f);
            }
            const int segments = 12;
            for (int ring = 0; ring < rings.Length - 1; ring++)
                for (int side = 0; side < segments; side++)
                {
                    float a = side * 2f * Mathf.PI / segments, b = (side + 1) * 2f * Mathf.PI / segments;
                    float level = Mathf.Sin((a + b) * .5f);
                    int color = level < -.48f ? 2 : level < .12f ? 1 : 0;
                    body.Quad(Point(rings[ring], a), Point(rings[ring], b), Point(rings[ring+1], b), Point(rings[ring+1], a), color);
                }

            // Close the nose and tail rings so an axial harpoon shot hits the visible silhouette.
            for(int side=0;side<segments;side++)
            {
                float a=side*Mathf.PI*2/segments, b=(side+1)*Mathf.PI*2/segments;
                body.Fin(Vector3.forward*rings[0].x,Point(rings[0],b),Point(rings[0],a),0);
                var nose=rings[rings.Length-1];
                body.Fin(Vector3.forward*nose.x,Point(nose,a),Point(nose,b),0);
            }

            foreach (int sign in new[] {-1,1})
            {
                if (bristle)
                {
                    // Two copper bars wrap around the shoulder instead of sitting flat across its flank.
                    for (int band = 2; band <= 3; band++)
                    {
                        Vector3 shoulder = rings[band];
                        shoulder.y *= 1.017f;
                        shoulder.z *= 1.017f;
                        float previous = .08f;
                        for (int step = 1; step <= 4; step++)
                        {
                            float angle = previous + .20f;
                            Vector3 p = Point(shoulder, sign > 0 ? previous : Mathf.PI - previous);
                            Vector3 q = Point(shoulder, sign > 0 ? angle : Mathf.PI - angle);
                            p.z -= .072f * previous;
                            q.z -= .072f * angle;
                            var edge = Vector3.forward * .017f;
                            body.Fin(p-edge, q-edge, q+edge, 1);
                            body.Fin(p-edge, q+edge, p+edge, 1);
                            previous = angle;
                        }
                    }
                }
                if (ray)
                {
                    Vector3 front = new(sign*.14f,.005f,.36f), peak = new(sign*.39f,.075f,.02f);
                    Vector3 tip = new(sign*.91f,-.01f,-.24f), heel = new(sign*.2f,-.03f,-.36f);
                    body.Fin(front, peak, tip, 1);
                    body.Fin(front, tip, heel, 0);
                    body.Fin(heel, tip, new Vector3(sign*.46f,-.04f,-.41f), 3);
                    for (int spot = 0; spot < 4; spot++)
                        body.Gem(new Vector3(sign*(.28f+spot*.065f), .055f-spot*.008f, -.08f-spot*.037f), .018f, 4);
                }
                else body.Fin(new Vector3(sign*.13f*width,0,.18f),new Vector3(sign*.40f*width,-.11f,-.20f),new Vector3(sign*.12f*width,-.055f,-.07f),3);
                body.Gem(new Vector3(sign*.175f*width, .074f*height, .30f), ray ? .044f : .031f, 4);
                if (!bristle && !needle && !ray)
                    for (int spot=0; spot<5; spot++)
                        body.Gem(new Vector3(sign*(.20f-.007f*Mathf.Abs(spot-2)), -.07f, .19f-spot*.07f), .016f, 4);
            }

            if (bristle)
            {
                for (int spine=0; spine<6; spine++)
                {
                    float z=.28f-spine*.088f, h=.23f-.018f*Mathf.Abs(spine-2);
                    body.Fin(new Vector3(0,h,z+.03f),new Vector3(0,h+.14f,z-.07f),new Vector3(0,h-.01f,z-.09f),1);
                }
            }
            else if (!ray) body.Fin(new Vector3(0,.14f*height,.12f),new Vector3(0,.34f*height,-.2f),new Vector3(0,.08f*height,-.32f),3);

            var tail = new Geometry();
            if (ray)
            {
                tail.Fin(new Vector3(-.035f,.013f,0),new Vector3(.018f,0,-.54f),new Vector3(.04f,-.015f,0),3);
                tail.Fin(new Vector3(-.012f,0,-.38f),new Vector3(.006f,.08f,-.69f),new Vector3(.014f,0,-.48f),1);
            }
            else foreach (int sign in new[]{-1,1})
                tail.Fin(new Vector3(0,sign*.045f,.015f),new Vector3(0,sign*(needle?.16f:.22f),-.30f),
                         new Vector3(0,sign*.035f,-.18f),3);
            return new Shape {body=body.Mesh(species), tail=tail.Mesh(species + " tail"), colors=colors,wildColors=wild,catchColors=loaded};
        }

        private static Vector3 Point(Vector3 ring, float angle) =>
            new(Mathf.Cos(angle)*ring.y, Mathf.Sin(angle)*ring.z, ring.x);

        private sealed class Geometry
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<int>[] triangles = {new(),new(),new(),new(),new()};

            private void Triangle(Vector3 a, Vector3 b, Vector3 c, int color)
            {
                int start=vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles[color].Add(start); triangles[color].Add(start+1); triangles[color].Add(start+2);
            }

            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int color)
            {Triangle(a,b,c,color); Triangle(a,c,d,color);}

            public void Fin(Vector3 a,Vector3 b,Vector3 c,int color)
            {Triangle(a,b,c,color); Triangle(c,b,a,color);}

            public void Gem(Vector3 p,float radius,int color)
            {
                Vector3 up=Vector3.up*radius, right=Vector3.right*radius, front=Vector3.forward*radius;
                foreach(float sign in new[]{-1f,1f})
                {
                    Vector3 pole=p+sign*up;
                    Fin(pole,p+right,p+front,color); Fin(pole,p+front,p-right,color);
                    Fin(pole,p-right,p-front,color); Fin(pole,p-front,p+right,color);
                }
            }

            public Mesh Mesh(string label)
            {
                var mesh = new Mesh {name=label, hideFlags=HideFlags.DontSave};
                mesh.SetVertices(vertices); mesh.subMeshCount=triangles.Length;
                for(int i=0;i<triangles.Length;i++) mesh.SetTriangles(triangles[i],i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
