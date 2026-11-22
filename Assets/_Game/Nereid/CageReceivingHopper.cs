using System.Collections.Generic;
using Raid.Factory;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    internal sealed class CageReceivingHopper : MonoBehaviour
    {
        readonly List<Mesh> meshes=new();
        readonly Transform[] bubbles=new Transform[22];
        readonly Renderer[] lamps=new Renderer[6];
        Material lampMaterial, pressure, glass;
        MaterialPropertyBlock block;
        Mesh cube;
        Transform trolley,irisA,irisB,fixture;
        Transform cableSocket;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor");
        static readonly int Flow=Shader.PropertyToID("_FlowActive");

        internal static CageReceivingHopper Install(NereidVessel vessel,RaidWorld world)
        {
            var hopper=vessel.transform.Find("Sealed reservoir overhead hopper");
            var throat=vessel.transform.Find("Reservoir delivery throat");
            if(hopper==null || throat==null)return null;
            var node=new GameObject("Cage receiving lock").transform;
            node.SetParent(vessel.transform,false);
            node.localPosition=new Vector3(0,0,-3);
            var result=node.gameObject.AddComponent<CageReceivingHopper>();
            result.Build(hopper,throat,world);
            return result;
        }

        void Build(Transform oldHopper,Transform oldThroat,RaidWorld world)
        {
            var shell=oldHopper.GetComponent<MeshRenderer>();
            var outlet=oldThroat.GetComponent<MeshRenderer>();
            cube=oldHopper.GetComponent<MeshFilter>().sharedMesh;
            pressure=shell.sharedMaterial;
            var dark=outlet.sharedMaterial;
            shell.enabled=false;
            outlet.enabled=false;
            var factory=FindAnyObjectByType<FactoryVisuals>();
            if(factory!=null && factory.processGlass!=null && factory.processGlass.Length>0 && factory.processGlass[0]!=null)
                glass=new Material(factory.processGlass[0].sharedMaterial){name="Transfer pressure sightglass",hideFlags=HideFlags.DontSave};
            else glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=new Color(.38f,.59f,.62f,.22f)};
            block=new MaterialPropertyBlock();
            var blue=new Color(.28f,.80f,1);
            lampMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Wet transfer • sapphire inspection diffusers"};
            lampMaterial.SetColor("_BaseColor",blue);lampMaterial.EnableKeyword("_EMISSION");lampMaterial.SetColor(Emission,blue*.3f);
            var flange=new[]{new Vector2(.36f,2.98f),new Vector2(.56f,2.98f),new Vector2(.56f,3.19f),new Vector2(.36f,3.19f)};
            Lathe("Tank seal flange",flange,pressure);
            Lathe("Machined inlet cone",new[]{new Vector2(.58f,4.58f),new Vector2(1.11f,5.17f),new Vector2(1.17f,5.23f),new Vector2(1.17f,5.33f),new Vector2(1.09f,5.39f)},pressure);
            Lathe("Pressure sightglass",new[]{new Vector2(.386f,3.13f),new Vector2(.46f,3.41f),new Vector2(.46f,4.52f),new Vector2(.58f,4.68f)},glass);
            for(int i=0;i<3;i++)
            {
                float y=3.27f+i*.65f,radius=.49f+i*.022f;
                Lathe("Sightglass clamp",new[]{new Vector2(radius-.02f,y-.06f),new Vector2(radius+.08f,y-.06f),new Vector2(radius+.08f,y+.06f),new Vector2(radius-.02f,y+.06f)},dark);
            }
            for(int i=0;i<6;i++)
            {
                float a=i*Mathf.PI/3f;
                var outward=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Box(transform,"Sightglass strut",outward*.50f+Vector3.up*3.96f,new Vector3(.065f,1.33f,.065f),pressure);
                lamps[i]=Box(transform,"Pressure lock indicator",outward*.56f+Vector3.up*3.89f,new Vector3(.047f,.38f,.047f),lampMaterial);
                Box(transform,"Receiver clamp",outward*1.04f+Vector3.up*5.35f,new Vector3(.16f,.26f,.16f),dark);
            }

            irisA=Box(transform,"Sliding pressure shutter",new Vector3(.28f,4.60f,0),new Vector3(.61f,.072f,1.13f),dark).transform;
            irisB=Box(transform,"Sliding pressure shutter",new Vector3(-.28f,4.60f,0),new Vector3(.61f,.072f,1.13f),dark).transform;
            cableSocket=world.container.GetComponentInChildren<CargoBasket>().CableSocket;
            trolley=Pivot(transform,"Rail traveller",Vector3.zero);
            Box(trolley,"Traveller frame",Vector3.zero,new Vector3(.65f,.23f,.65f),pressure);
            Box(trolley,"Traveller axle",Vector3.up*.138f,new Vector3(.44f,.10f,.78f),dark);
            Box(trolley,"Hydraulic damper",Vector3.down*.20f,new Vector3(.17f,.29f,.17f),pressure);
            Box(trolley,"Traveller telltale",new Vector3(.34f,-.01f,0),new Vector3(.033f,.08f,.23f),lampMaterial);

            fixture=Pivot(transform,"Aerated water on the return line",Vector3.zero);
            for(int i=0;i<bubbles.Length;i++)
            {
                var particle=Box(fixture,"Transfer microbubble",Vector3.zero,Vector3.one,
                    factory!=null && factory.bubbleMaterial!=null?factory.bubbleMaterial:lampMaterial);
                particle.shadowCastingMode=ShadowCastingMode.Off;
                if(factory!=null && factory.bubbleMesh!=null)particle.GetComponent<MeshFilter>().sharedMesh=factory.bubbleMesh;
                bubbles[i]=particle.transform;
            }
            fixture.gameObject.SetActive(false);
        }

        internal void Present(TeamProgress state,float progress)
        {
            bool unload=state.hoistPhase==HoistPhase.Unloading;
            float valve=unload?CargoBasket.Ease(Mathf.InverseLerp(.055f,.27f,progress))*
                (1-CargoBasket.Ease(Mathf.InverseLerp(.84f,1,progress))):0;
            irisA.localPosition=new Vector3(.28f+.54f*valve,4.60f,0);
            irisB.localPosition=new Vector3(-.28f-.54f*valve,4.60f,0);
            trolley.position=new Vector3(cableSocket.position.x,transform.TransformPoint(Vector3.up*7.04f).y,cableSocket.position.z);
            if(!unload && fixture.gameObject.activeSelf)fixture.gameObject.SetActive(false);
            float flow=unload?valve*Mathf.Sin(Mathf.PI*Mathf.InverseLerp(.14f,.98f,progress)):0;
            bool showing=unload && progress>.22f && progress<.99f && state.cargo.Count>0;
            if(fixture.gameObject.activeSelf!=showing)fixture.gameObject.SetActive(showing);
            if(showing)
            {
                for(int i=0;i<bubbles.Length;i++)
                {
                    float age=Mathf.Repeat((progress-.22f)*2.4f+i*.419f,1);
                    float spread=.12f+.29f*age;
                    var p=bubbles[i];
                    p.localPosition=new Vector3(Mathf.Sin(i*2.399f+age)*spread,4.75f-age*2.18f,Mathf.Cos(i*2.399f+age*.7f)*spread);
                    p.localRotation=Quaternion.Euler(13*i,age*174,i*19);
                    p.localScale=Vector3.one*((.023f+(i%4)*.010f)*Mathf.Sin(Mathf.PI*age)*Mathf.Lerp(.25f,1,flow));
                }
            }
            block.Clear();block.SetColor(Emission,new Color(.26f,.78f,1)*(.35f+3.4f*flow));
            foreach(var lens in lamps)lens.SetPropertyBlock(block);
            block.Clear();block.SetFloat(Flow,.45f*flow);
            glassRenderer.SetPropertyBlock(block);
        }
        Renderer glassRenderer;

        void Lathe(string name,Vector2[] profile,Material material)
        {
            const int count=28;
            var points=new Vector3[(count+1)*profile.Length];var uv=new Vector2[points.Length];
            var triangles=new int[count*(profile.Length-1)*6];
            int k=0;
            for(int band=0;band<profile.Length;band++)
                for(int i=0;i<=count;i++)
                {
                    float angle=i*Mathf.PI*2/count;
                    int index=band*(count+1)+i;
                    points[index]=new Vector3(profile[band].x*Mathf.Cos(angle),profile[band].y,profile[band].x*Mathf.Sin(angle));
                    uv[index]=new Vector2((float)i/count,profile[band].y);
                    if(i==0||band==0)continue;
                    int low=index-(count+1);
                    triangles[k++]=low-1;triangles[k++]=index-1;triangles[k++]=low;
                    triangles[k++]=low;triangles[k++]=index-1;triangles[k++]=index;
                }
            var mesh=new Mesh{name=name};
            mesh.vertices=points;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();
            meshes.Add(mesh);
            var node=Pivot(transform,name,Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;
            if(material==glass){glassRenderer=renderer;renderer.shadowCastingMode=ShadowCastingMode.Off;}
        }

        static Transform Pivot(Transform parent,string name,Vector3 at)
        {
            var p=new GameObject(name).transform;p.SetParent(parent,false);p.localPosition=at;return p;
        }

        MeshRenderer Box(Transform parent,string name,Vector3 at,Vector3 scale,Material material)
        {
            var p=Pivot(parent,name,at);p.localScale=scale;
            p.gameObject.AddComponent<MeshFilter>().sharedMesh=cube;
            var r=p.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=material;return r;
        }

        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);
            if(lampMaterial!=null)Destroy(lampMaterial);
            if(glass!=null)Destroy(glass);
        }
    }
}
