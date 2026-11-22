using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    // Client silhouettes follow the existing threat views, visibility and hit logic.
    [DisallowMultipleComponent,DefaultExecutionOrder(200)]
    public sealed class ThreatPresentation : MonoBehaviour
    {
        public Material steel,skin,dark,tooth,cyan,amber;
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<Visual> visuals=new List<Visual>();
        ThreatDirector director;
        Material wetHide,wetFin,gum,enamel;
        void BuildTissues()
        {
            if(wetHide!=null)return;
            var shader=Resources.Load<Shader>("NereidFishSkin");
            if(shader==null)return;
            wetHide=Tissue(shader,"Blue-black dermis",new Color(.32f,.44f,.53f),new Color(.44f,.69f,.72f),.56f);
            wetFin=Tissue(shader,"Fin and throat",new Color(.13f,.18f,.24f),new Color(.28f,.46f,.55f),.35f);
            gum=Tissue(shader,"Exposed gill tissue",new Color(.55f,.27f,.22f),new Color(.68f,.40f,.31f),.74f);
            enamel=Tissue(shader,"Weathered ivory",new Color(.84f,.77f,.55f),new Color(.94f,.93f,.74f),.8f);
        }
        static Material Tissue(Shader shader,string label,Color baseColor,Color rim,float gloss)
        {
            var mat=new Material(shader){name=label,hideFlags=HideFlags.DontSave};
            mat.SetColor("_BaseColor",baseColor);mat.SetColor("_RimColor",rim);mat.SetFloat("_Smoothness",gloss);
            return mat;
        }
        float searchAt;
        int seenWaveHits;
        float recoilUntil;
        sealed class Visual
        {
            public Transform source,root,tail,leftFin,rightFin,jaw;
            public Renderer original;
            public bool originalEnabled;
            public float phase,finSpread;
            public bool ambusher;
        }

        void LateUpdate()
        {
            if(!UI.UiInputGate.Active){RestoreOriginals();return;}
            if(director==null)
            {
                Release();if(Time.unscaledTime<searchAt)return;
                searchAt=Time.unscaledTime+.3f;director=Object.FindAnyObjectByType<ThreatDirector>();
            }
            if(director==null||director.NetworkObject==null||(!director.IsClientInitialized && !director.IsServerInitialized)){RestoreOriginals();return;}
            if(visuals.Count==0&&skin!=null&&dark!=null&&tooth!=null)
            {
                foreach(Transform child in director.transform)
                {
                    if(child.name=="Predator")Build(child,false);
                    else if(child.name=="Reservoir raider")Build(child,true);
                }
            }
            director.ShowPrimitiveHitbox(visuals.Count == 0);
            if (seenWaveHits != director.WaveHits)
            {
                seenWaveHits = director.WaveHits;
                recoilUntil = Time.time + .22f;
            }
            foreach(var visual in visuals)
            {
                if(visual.source==null||visual.root==null)continue;
                if(visual.original!=null&&visual.original.enabled)visual.original.enabled=false;
                if(!visual.root.gameObject.activeSelf)visual.root.gameObject.SetActive(true);
                if (!visual.ambusher)
                    visual.root.localRotation = Quaternion.Euler(0, 0, Mathf.Clamp01((recoilUntil-Time.time)/.22f)*20f);
                var hunt=visual.ambusher?director.AnglerStage:ThreatDirector.HuntStage.Watching;
                bool display=hunt==ThreatDirector.HuntStage.Display;
                bool rushing=hunt==ThreatDirector.HuntStage.Pursuit;
                visual.phase=Mathf.Repeat(visual.phase+Time.deltaTime*(rushing?9f:3.4f),Mathf.PI*200);
                float clock=visual.phase;
                // Pectorals fan outward during the warning and sweep back into the body for the pursuit.
                visual.finSpread=Mathf.MoveTowards(visual.finSpread,display?23f:rushing?-9f:0f,Time.deltaTime*80f);
                visual.tail.localRotation=Quaternion.Euler(0,Mathf.Sin(clock)*(rushing?21:6),0);
                visual.leftFin.localRotation=Quaternion.Euler(0,0,Mathf.Sin(clock*.72f)*3-visual.finSpread);
                visual.rightFin.localRotation=Quaternion.Euler(0,0,-Mathf.Sin(clock*.72f+.7f)*3+visual.finSpread);
                float gape=visual.ambusher?(display?-2f:rushing?12f:-20f):6f;
                visual.jaw.localRotation=Quaternion.Slerp(visual.jaw.localRotation,Quaternion.Euler(gape+Mathf.Sin(clock*.43f)*1.8f,0,0),
                    1-Mathf.Exp(-Time.deltaTime*10));
            }
        }

        void Build(Transform source,bool raider)
        {
            BuildTissues();
            var root=new GameObject(raider?"Prototype raider silhouette":"Prototype predator silhouette").transform;
            root.gameObject.hideFlags=HideFlags.DontSave;root.SetParent(source,false);
            var visual=new Visual{ambusher=!raider,source=source,root=root,original=source.GetComponent<Renderer>(),phase=raider?1.7f:0};
            visual.originalEnabled=visual.original!=null&&visual.original.enabled;
            float width=raider?1.08f:1;
            Part(root,"Tapered asymmetric body",Loft(new[]{new Vector4(-.27f,.038f,.055f,.025f),new Vector4(-.18f,.105f,.13f,.035f),new Vector4(-.065f,.215f*width,.19f,.055f),new Vector4(.095f,.267f*width,.19f,.068f),new Vector4(.245f,.205f*width,.135f,.085f),new Vector4(.31f,.11f*width,.07f,.105f)},false),skin);
            Part(root,"Recessed mouth cavity",Loft(new[]{new Vector4(.19f,.063f,.045f,-.015f),new Vector4(.275f,.13f,.092f,-.035f),new Vector4(.365f,.178f,.123f,-.042f)},true),dark);
            var lip=raider && gum!=null?gum:dark;
            Tube(root,"Uneven upper lip",new[]{new Vector3(-.175f,-.041f,.353f),new Vector3(-.13f,.063f,.365f),new Vector3(-.025f,.091f,.38f),new Vector3(.112f,.071f,.363f),new Vector3(.178f,-.025f,.345f)},.019f,.015f,lip,7);

            visual.jaw=Pivot(root,"Open lower jaw",new Vector3(0,-.078f,.17f));
            Vector3 jawOrigin=visual.jaw.localPosition;
            var jawOutline=new[]{new Vector3(-.158f,-.144f,.15f),new Vector3(-.19f,-.16f,.27f),new Vector3(-.108f,-.177f,.375f),new Vector3(.005f,-.183f,.399f),new Vector3(.116f,-.167f,.366f),new Vector3(.18f,-.146f,.26f),new Vector3(.15f,-.132f,.15f)};
            Part(visual.jaw,"Bony lower mandible",Extruded(Offset(jawOutline,-jawOrigin),Vector3.up*.049f),skin);
            Tube(visual.jaw,"Jaw inner ridge",Offset(new[]{jawOutline[1]+Vector3.up*.029f,jawOutline[2]+Vector3.up*.029f,jawOutline[3]+Vector3.up*.029f,jawOutline[4]+Vector3.up*.029f,jawOutline[5]+Vector3.up*.029f},-jawOrigin),.012f,.009f,lip,6);
            if(gum!=null)
            {
                for(int side=-1;side<=1;side+=2)
                    Tube(root,"Zygomatic ridge",new[]{new Vector3(side*.163f,.018f,.345f),
                        new Vector3(side*.211f,.045f,.276f),new Vector3(side*.246f,.071f,.207f)},
                        .013f,.005f,wetHide,7);
            }
            // Sparse, unequal teeth leave the dark mouth readable instead of a uniform white fan.
            Fang(root,"Long left fang",new Vector3(-.128f,.054f,.361f),new Vector3(-.115f,raider?-.085f:-.032f,.394f),.014f);
            Fang(root,"Short left fang",new Vector3(-.057f,.079f,.376f),new Vector3(-.052f,-.008f,.399f),.009f);
            Fang(root,"Right hooked fang",new Vector3(.126f,.055f,.35f),new Vector3(.103f,raider?-.109f:-.042f,.391f),.016f);
            Fang(root,"Inner needle",new Vector3(.045f,.079f,.374f),new Vector3(.037f,-.013f,.398f),.008f);
            Fang(visual.jaw,"Left lower tusk",new Vector3(-.114f,-.139f,.335f)-jawOrigin,new Vector3(-.105f,-.043f,.384f)-jawOrigin,.012f);
            Fang(visual.jaw,"Right lower tusk",new Vector3(.13f,-.13f,.318f)-jawOrigin,new Vector3(.116f,-.039f,.365f)-jawOrigin,.010f);
            if(!raider)Fang(visual.jaw,"Broken central tusk",new Vector3(.011f,-.156f,.368f)-jawOrigin,new Vector3(.009f,-.101f,.39f)-jawOrigin,.008f);

            Material eye=raider?(cyan!=null?cyan:tooth):(amber!=null?amber:tooth);
            Gem(root,"Left sunken eye socket",new Vector3(-.193f,.134f,.242f),new Vector3(.060f,.039f,.042f),dark);
            Gem(root,"Left light-sensitive eye",new Vector3(-.217f,.139f,.264f),new Vector3(.025f,.019f,.023f),eye);
            Gem(root,"Right damaged socket",new Vector3(.192f,.134f,.231f),new Vector3(.048f,.031f,.033f),dark);
            Gem(root,"Right narrow eye",new Vector3(.211f,.139f,.251f),new Vector3(.014f,.009f,.015f),eye);
            Tube(root,"Scarred left brow",new[]{new Vector3(-.124f,.157f,.271f),new Vector3(-.203f,.177f,.245f),new Vector3(-.248f,.139f,.2f)},.017f,.011f,skin,7);
            Tube(root,"Uneven right brow",new[]{new Vector3(.132f,.156f,.271f),new Vector3(.203f,.158f,.232f),new Vector3(.23f,.129f,.191f)},.014f,.008f,skin,7);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<3;i++)
            {
                float z=.069f-i*.047f;
                var slit=new[]{new Vector3(side*(.265f-i*.013f)*width,.126f,z),new Vector3(side*(.277f-i*.013f)*width,-.025f,z-.013f),new Vector3(side*(.263f-i*.013f)*width,-.016f,z-.027f)};
                Part(root,"Recessed gill "+side+" "+i,Extruded(slit,Vector3.right*.004f),dark);
            }

            Part(root,"Ragged dorsal membrane",Extruded(new[]{new Vector3(0,.18f,-.19f),new Vector3(-.008f,.355f,-.105f),new Vector3(.004f,.31f,.072f),new Vector3(0,.175f,.168f)},Vector3.right*.012f),dark);
            for(int i=0;i<4;i++)
            {
                float z=-.15f+i*.087f,y=.20f+Mathf.Sin(i*.8f)*.045f;
                Tube(root,"Dorsal spine "+i,new[]{new Vector3(0,y,z),new Vector3(-.012f,y+.11f+(i==1 ? .045f : 0),z-.033f),new Vector3(-.015f,y+.14f+(i==1 ? .045f : 0),z-.064f)},.012f,.0015f,tooth,5);
            }
            if(raider&&steel!=null)
                Part(root,"Scavenged dorsal plate",Extruded(new[]{new Vector3(-.072f,.235f,-.055f),new Vector3(-.084f,.25f,.04f),new Vector3(.051f,.259f,.066f),new Vector3(.068f,.229f,-.047f)},Vector3.up*.012f),steel);

            visual.leftFin=Pivot(root,"Left pectoral fin",new Vector3(-.145f,-.005f,.07f));
            Part(visual.leftFin,"Long torn left fin",Extruded(Offset(new[]{new Vector3(-.145f,-.005f,.07f),new Vector3(-.445f,-.101f,-.12f),new Vector3(-.36f,-.119f,-.075f),new Vector3(-.168f,-.067f,-.135f)},-visual.leftFin.localPosition),Vector3.up*.008f),dark);
            visual.rightFin=Pivot(root,"Right pectoral fin",new Vector3(.15f,-.012f,.063f));
            Part(visual.rightFin,"Short right fin",Extruded(Offset(new[]{new Vector3(.15f,-.012f,.063f),new Vector3(.385f,-.163f,-.089f),new Vector3(.185f,-.075f,-.148f)},-visual.rightFin.localPosition),Vector3.up*.009f),skin);
            visual.tail=Pivot(root,"Swimming tail",new Vector3(0,.025f,-.225f));
            Tube(visual.tail,"Tapered caudal stalk",new[]{Vector3.zero,new Vector3(0,-.005f,-.115f)},.045f,.022f,skin,8);
            Part(visual.tail,"Upper caudal fin",Extruded(new[]{new Vector3(0,0,-.089f),new Vector3(.008f,.278f,-.171f),new Vector3(-.009f,.11f,-.192f),new Vector3(0,.01f,-.139f)},Vector3.right*.013f),dark);
            Part(visual.tail,"Lower caudal fin",Extruded(new[]{new Vector3(0,-.01f,-.085f),new Vector3(-.008f,-.208f,-.17f),new Vector3(.006f,-.092f,-.187f),new Vector3(0,-.013f,-.14f)},Vector3.right*.013f),skin);
            Tube(root,"Bent lure stalk",new[]{new Vector3(.028f,.189f,.163f),new Vector3(.04f,.303f,.214f),new Vector3(.089f,.417f,.302f),new Vector3(.13f,.344f,.372f)},.009f,.005f,dark,7);
            Gem(root,"Small bioluminescent lure",new Vector3(.131f,.326f,.381f),new Vector3(.021f,.028f,.025f),eye);

            // Presentation bounds fit both the original unit view and the unchanged strike sphere.
            float radius=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))foreach(var vertex in filter.sharedMesh.vertices)
            {
                var point=root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                radius=Mathf.Max(radius,source.TransformVector(point).magnitude);
            }
            float hitRadius=director.tuning==null?1.25f:Mathf.Max(.1f,director.tuning.strikeRadius);
            root.localScale=Vector3.one*Mathf.Min(.98f,hitRadius*.92f/Mathf.Max(.01f,radius));
            if (raider && director.IsServerInitialized)
            {
                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    filter.gameObject.layer = ThreatDirector.RaiderLayer;
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                }
            }
            visuals.Add(visual);
        }

        static Transform Pivot(Transform parent,string name,Vector3 position)
        {var node=new GameObject(name).transform;node.gameObject.hideFlags=HideFlags.DontSave;node.SetParent(parent,false);node.localPosition=position;return node;}

        void Part(Transform parent,string name,Mesh mesh,Material material)
        {
            mesh.name=name+" / threat presentation";mesh.hideFlags=HideFlags.DontSave;meshes.Add(mesh);
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.hideFlags=HideFlags.DontSave;part.transform.SetParent(parent,false);
            part.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=part.GetComponent<MeshRenderer>();
            if(wetHide!=null)
            {
                if(material==skin)material=wetHide;
                else if(material==dark)material=wetFin;
                else if(material==tooth)material=enamel;
            }
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }

        void Fang(Transform parent,string name,Vector3 root,Vector3 tip,float radius)
        {Tube(parent,name,new[]{root,Vector3.Lerp(root,tip,.63f)+Vector3.forward*.016f,tip},radius,.001f,tooth,5);}

        void Tube(Transform parent,string name,Vector3[] path,float firstRadius,float lastRadius,Material material,int sides)
        {
            var data=new MeshData();var rings=new Vector3[path.Length][];
            for(int i=0;i<path.Length;i++)
            {
                Vector3 tangent=(path[Mathf.Min(path.Length-1,i+1)]-path[Mathf.Max(0,i-1)]).normalized;
                Vector3 normal=Vector3.Cross(tangent,Mathf.Abs(Vector3.Dot(tangent,Vector3.up))>.95f?Vector3.forward:Vector3.up).normalized;
                Vector3 binormal=Vector3.Cross(tangent,normal);float radius=Mathf.Lerp(firstRadius,lastRadius,(float)i/(path.Length-1));
                rings[i]=new Vector3[sides];
                for(int j=0;j<sides;j++){float angle=j*Mathf.PI*2/sides;rings[i][j]=path[i]+(normal*Mathf.Cos(angle)+binormal*Mathf.Sin(angle))*radius;}
            }
            data.Cap(rings[0],true);data.Cap(rings[rings.Length-1],false);
            for(int i=0;i<rings.Length-1;i++)for(int j=0;j<sides;j++){int k=(j+1)%sides;data.Quad(rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]);}
            Part(parent,name,data.Finish(),material);
        }

        void Gem(Transform parent,string name,Vector3 centre,Vector3 radius,Material material)
        {
            var data=new MeshData();var ring=new[]{centre+Vector3.right*radius.x,centre+Vector3.forward*radius.z,centre-Vector3.right*radius.x,centre-Vector3.forward*radius.z};
            for(int i=0;i<4;i++){int j=(i+1)%4;data.Triangle(centre+Vector3.up*radius.y,ring[j],ring[i]);data.Triangle(centre-Vector3.up*radius.y,ring[i],ring[j]);}
            Part(parent,name,data.Finish(),material);
        }

        static Mesh Loft(Vector4[] sections,bool cavity)
        {
            const int sides=12;var rings=new Vector3[sections.Length][];var data=new MeshData();
            for(int i=0;i<sections.Length;i++)
            {
                rings[i]=new Vector3[sides];var section=sections[i];
                for(int j=0;j<sides;j++){float a=j*Mathf.PI*2/sides;float x=Mathf.Cos(a)*section.y;rings[i][j]=new Vector3(x*(x<0?1.025f:1),section.w+Mathf.Sin(a)*section.z,section.x);}
            }
            if(!cavity){data.Cap(rings[0],true);data.Cap(rings[rings.Length-1],false);}else data.Cap(rings[0],false);
            for(int i=0;i<rings.Length-1;i++)for(int j=0;j<sides;j++)
            {
                int k=(j+1)%sides;
                if(cavity)data.Quad(rings[i][k],rings[i][j],rings[i+1][j],rings[i+1][k]);
                else data.Quad(rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]);
            }
            return data.Finish();
        }

        static Mesh Extruded(Vector3[] outline,Vector3 thickness)
        {
            var data=new MeshData();int count=outline.Length;var top=new Vector3[count];var bottom=new Vector3[count];
            bool reverse=Vector3.Dot(Vector3.Cross(outline[1]-outline[0],outline[2]-outline[0]),thickness)<0;
            for(int i=0;i<count;i++){Vector3 p=outline[reverse?count-1-i:i];top[i]=p+thickness*.5f;bottom[i]=p-thickness*.5f;}
            data.Cap(top,false);data.Cap(bottom,true);
            for(int i=0;i<count;i++){int j=(i+1)%count;data.Quad(top[i],bottom[i],bottom[j],top[j]);}
            return data.Finish();
        }

        static Vector3[] Offset(Vector3[] points,Vector3 offset)
        {var result=new Vector3[points.Length];for(int i=0;i<points.Length;i++)result[i]=points[i]+offset;return result;}

        sealed class MeshData
        {
            readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c){int i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.Add(i);indices.Add(i+1);indices.Add(i+2);}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){Triangle(a,b,c);Triangle(a,c,d);}
            public void Cap(Vector3[] ring,bool reverse){for(int i=1;i<ring.Length-1;i++){if(reverse)Triangle(ring[0],ring[i+1],ring[i]);else Triangle(ring[0],ring[i],ring[i+1]);}}
            public Mesh Finish(){var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;}
        }

        void RestoreOriginals()
        {
            if (director != null) director.ShowPrimitiveHitbox(true);
            foreach(var visual in visuals)
            {
                if(visual.original!=null)visual.original.enabled=visual.originalEnabled;
                if(visual.root!=null&&visual.root.gameObject.activeSelf)visual.root.gameObject.SetActive(false);
            }
        }
        void Release()
        {
            RestoreOriginals();foreach(var visual in visuals)if(visual.root!=null)Destroy(visual.root.gameObject);
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            meshes.Clear();visuals.Clear();director=null;
            if(wetHide!=null)Destroy(wetHide);
            if(wetFin!=null)Destroy(wetFin);
            if(gum!=null)Destroy(gum);
            if(enamel!=null)Destroy(enamel);
            wetHide=wetFin=gum=enamel=null;
        }
        void OnDisable() { Release(); }
        void OnDestroy() { Release(); }
    }
}
