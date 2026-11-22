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
            public Material[] colors, wildColors;
        }

        private static readonly Dictionary<string, Shape> Shapes = new();
        private Transform tail;
        public Transform Tail => tail;
        public long CargoId { get; internal set; }
        private float phase, strokeWeight;
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
            var finish=live==null || shape.wildColors==null?shape.colors:shape.wildColors;
            var body = MakePart(species, parent, shape.body, finish);
            var model = body.AddComponent<CatchFishModel>();
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

        private void LateUpdate()
        {
            if (escapeAt >= 0)
            {
                float age = Time.time - escapeAt;
                if (age >= 2.3f) { Destroy(gameObject); return; }
                transform.position += escapeDirection * ((2.2f + Mathf.Sin(age*6)*.15f)*Time.deltaTime);
                transform.localScale = Vector3.one * (.65f*Mathf.Clamp01(2.3f-age));
            }
            bool burst = escapeAt >= 0 || actor != null && (actor.Mode == FishMode.Struggling || actor.Mode == FishMode.Escaped || actor.Startled);
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
            strokeWeight=Mathf.MoveTowards(strokeWeight,burst?1f:0f,Time.deltaTime*6f);
            float cruise=clock*(ray?3.7f:7.1f)+offset, sprint=clock*14f+offset;
            float lead=Mathf.Lerp(Mathf.Sin(cruise),Mathf.Sin(sprint),strokeWeight);
            float whip=Mathf.Lerp(Mathf.Sin(cruise-.9f)*25f,Mathf.Sin(sprint-.9f)*39f,strokeWeight);
            tail.localRotation=Quaternion.Euler(ray?whip*.52f:0,ray?whip*.42f:whip,0);
            if(actor!=null)
            {
                float bank=ray?8f:3.5f;
                transform.localRotation=Quaternion.Euler(Mathf.Sin(cruise*.48f)*1.8f,
                    lead*Mathf.Lerp(3.5f,8f,strokeWeight),Mathf.Sin(cruise-.6f)*bank);
            }
        }

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
            Material[] wild=null;
            if(wetShader!=null)
            {
                wild=new Material[colors.Length];
                for(int i=0;i<wild.Length;i++)
                {
                    var lit=colors[i];
                    var waterSkin=new Material(wetShader){name=species+" / wet scales "+i,hideFlags=HideFlags.DontSave};
                    var albedo=lit.GetColor("_BaseColor");
                    waterSkin.SetColor("_BaseColor",albedo);
                    waterSkin.SetColor("_RimColor",Color.Lerp(albedo,new Color(.60f,.81f,.81f),.56f));
                    waterSkin.SetColor("_EmissionColor",lit.GetColor("_EmissionColor"));
                    waterSkin.SetFloat("_Smoothness",lit.GetFloat("_Smoothness"));wild[i]=waterSkin;
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
            return new Shape {body=body.Mesh(species), tail=tail.Mesh(species + " tail"), colors=colors,wildColors=wild};
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
