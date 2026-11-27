using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    public sealed partial class DefenseAtmosphereView
    {
        static readonly int CurrentId=Shader.PropertyToID("_Current");
        readonly MeshRenderer[] currentStrips=new MeshRenderer[PodCount+1];
        Mesh currentMesh;
        Material currentMaterial;
        MaterialPropertyBlock currentBlock;
        bool checkedCurrentShader;

        void PresentCurrents(DefenseAtmosphereSettings profile,float approach,float retreat)
        {
            bool distantStage=phase>=2 || warningReveal>.12f;
            if(!distantStage || profile.currentOpacity<=0 || retreat>8)
            {
                HideCurrents();
                return;
            }
            if(!checkedCurrentShader)BindCurrents();
            if(!currentMaterial)return;

            float fade=phase==1?Mathf.SmoothStep(0,1,warningReveal):1;
            fade*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1,8,retreat));
            var inherited=transform.lossyScale;
            for(int index=0;index<currentStrips.Length;index++)
            {
                bool wing=index==PodCount;
                float front=wing?Mathf.SmoothStep(0,1,Mathf.InverseLerp(-7,-2.3f,approach))*
                    (1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.3f,7,approach))):fade;
                var strip=currentStrips[index];
                strip.enabled=front>.008f;
                if(!strip.enabled)continue;

                int slot=wing?3:SlotCapacity+index*SpeciesCount;
                var fish=SampleShoal(raidSeed,slot,approach,approachSeconds,anchor,approachStart,approachEnd,
                    retreat,!wing,profile);
                float key=Unit(SlotKey(raidSeed,slot+21));
                float length=profile.currentLength*(wing?.86f:Mathf.Lerp(.81f,1.22f,key));
                float height=profile.currentHeight*(wing?.66f:Mathf.Lerp(.72f,1.16f,key));
                Vector3 trail=-fish.Forward;
                strip.transform.SetPositionAndRotation(fish.Position+trail*(wing?4.5f:6.8f)-Vector3.up*.7f,
                    Quaternion.LookRotation(trail)*Quaternion.AngleAxis(fish.Bank*.33f,Vector3.forward));
                strip.transform.localScale=new Vector3(height/inherited.x,height/inherited.y,length/inherited.z);
                currentBlock.SetVector(CurrentId,new Vector4(front*profile.currentOpacity*(wing?1:.65f),
                    key*17f,wing?.75f:.18f,wing?1:0));
                strip.SetPropertyBlock(currentBlock);
            }
        }

        void BindCurrents()
        {
            checkedCurrentShader=true;
            var shader=Resources.Load<Shader>("DefenseAtmosphere/SuspendedCurrent");
            if(!shader)return;
            currentMaterial=new Material(shader){name="Raid shoal particulates",hideFlags=HideFlags.DontSave};
            currentBlock=new MaterialPropertyBlock();
            currentMesh=BuildCurrentMesh();
            for(int i=0;i<currentStrips.Length;i++)
            {
                var node=new GameObject("Water displaced by shoal",typeof(MeshFilter),typeof(MeshRenderer));
                node.transform.SetParent(transform,false);
                node.GetComponent<MeshFilter>().sharedMesh=currentMesh;
                var renderer=node.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=currentMaterial;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                renderer.enabled=false;
                currentStrips[i]=renderer;
            }
        }

        static Mesh BuildCurrentMesh()
        {
            const int bands=3, stations=9;
            var vertices=new Vector3[bands*stations*2];
            var uv=new Vector2[vertices.Length];
            var triangles=new int[bands*(stations-1)*6];
            int cursor=0;
            for(int sheet=0;sheet<bands;sheet++)
            {
                Vector3 across=Quaternion.AngleAxis(sheet*60,Vector3.forward)*Vector3.right;
                for(int station=0;station<stations;station++)
                {
                    float t=station/(stations-1f);
                    float spread=Mathf.Sin(Mathf.PI*t)*.61f+.13f+t*.18f;
                    var center=new Vector3(Mathf.Sin(t*4.7f+sheet*.92f)*t*.18f,-t*t*.20f,t);
                    int v=(sheet*stations+station)*2;
                    vertices[v]=center-across*spread;
                    vertices[v+1]=center+across*spread;
                    uv[v]=new Vector2(t,-1);
                    uv[v+1]=new Vector2(t,1);
                    if(station==stations-1)continue;
                    triangles[cursor++]=v;
                    triangles[cursor++]=v+1;
                    triangles[cursor++]=v+2;
                    triangles[cursor++]=v+1;
                    triangles[cursor++]=v+3;
                    triangles[cursor++]=v+2;
                }
            }
            var mesh=new Mesh{name="Tapered shoal sediment ribbons",vertices=vertices,uv=uv,triangles=triangles};
            mesh.RecalculateBounds();
            return mesh;
        }

        void HideCurrents()
        {
            foreach(var strip in currentStrips)if(strip)strip.enabled=false;
        }

        void DisposeCurrents()
        {
            if(currentMaterial)Destroy(currentMaterial);
            if(currentMesh)Destroy(currentMesh);
        }
    }
}
