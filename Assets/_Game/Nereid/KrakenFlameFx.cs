using UnityEngine;

namespace Raid.Nereid
{
    internal sealed class KrakenFlameFx : MonoBehaviour
    {
        const int Tongues=7, Rings=12;
        FacetedFlowMesh tongues;
        FlowFragments sparks;
        Light spill;
        float nextSparks;
        int seenSequence;
        readonly System.Random cinderNoise=new(1317);
        float Range(float low,float high) => Mathf.LerpUnclamped(low,high,(float)cinderNoise.NextDouble());

        void Awake()
        {
            tongues=new FacetedFlowMesh(transform,"Faceted tongues of fire",Tongues,Rings,6);
            sparks=new FlowFragments(transform,"Cinders thrown from the throat",-.15f,256);
            var node=new GameObject("Firelight on the hull",typeof(Light));
            node.transform.SetParent(transform,false);
            spill=node.GetComponent<Light>();
            spill.type=LightType.Point;spill.color=new Color(1,.37f,.09f);
            spill.range=10;spill.shadows=LightShadows.None;spill.enabled=false;
        }

        internal void Present(KrakenBreathState state,float age)
        {
            if(seenSequence!=state.sequence){seenSequence=state.sequence;nextSparks=age;}
            float fire=state.Flame(age);
            if(fire<=.01f){Hide();return;}
            var direction=state.Direction(age);
            var orientation=Quaternion.LookRotation(direction,Vector3.up);
            tongues.Place(state.origin,orientation);
            float length=state.FlameReach(age);
            float flow=age-KrakenBreathState.Ignite;
            for(int strand=0;strand<Tongues;strand++)
            {
                bool core=strand==0;
                float angle=(strand-1)*Mathf.PI*2/(Tongues-1);
                float seed=strand*2.39f;
                float span=core?.61f:Mathf.Lerp(.54f,1,Mathf.Sin(seed+flow*17)*.5f+.5f);
                for(int ring=0;ring<Rings;ring++)
                {
                    float t=ring/(float)(Rings-1), z=Mathf.Lerp(-.67f,t*length*span,Mathf.SmoothStep(0,1,t*8));
                    float drift=core?0:(.18f+z*.22f)*Mathf.SmoothStep(0,1,t*3.5f);
                    float wave=Mathf.Sin(z*1.64f-flow*18+seed);
                    float flutter=Mathf.Sin(z*3.8f-flow*27+seed*1.8f);
                    float curl=angle+z*.19f-flow*.96f;
                    var center=new Vector3(Mathf.Cos(curl)*(drift+.16f*t*wave),
                        Mathf.Sin(curl)*drift*.74f+Mathf.Pow(t,2)*(.49f+.37f*wave),z);
                    float body=Mathf.Pow(1-t,.64f)*(.74f+Mathf.Sin(t*Mathf.PI)*.68f);
                    float width=Mathf.Max(.015f,body*(core?.51f:.21f+z*.039f)*(1+.24f*wave+.14f*flutter));
                    float heatPulse=.92f+.11f*wave+.08f*flutter;
                    width*=Mathf.Lerp(.37f,1,Mathf.SmoothStep(0,1,t*6.2f));
                    Color heat=Color.Lerp(core?new Color(3.45f,2.45f,1.22f):new Color(2.80f,1.18f,.17f),
                        new Color(1.45f,.18f,.025f),Mathf.Pow(t,.58f))*heatPulse;
                    heat.a=fire*Mathf.SmoothStep(0,1,t*10)*Mathf.Lerp(core?.93f:.76f,0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,1,t)));
                    tongues.Ring(strand,ring,center,width,width*(core?.73f:1.11f),wave*.31f+seed,heat);
                }
            }
            tongues.Draw();
            spill.transform.position=state.origin+direction*Mathf.Min(2.5f,length*.57f);
            spill.enabled=true;
            spill.intensity=fire*(18+4*Mathf.Sin(flow*106)+3*Mathf.Sin(flow*73));

            if(age<nextSparks)return;
            nextSparks=age+.031f;
            for(int i=0;i<8;i++)
            {
                float u=Range(0,1), lateral=Range(-1f,1f);
                var point=state.origin+orientation*new Vector3(lateral*.42f,Range(-.3f,.45f),length*u*.82f);
                var kick=orientation*new Vector3(lateral*Range(.7f,3.1f),Range(.8f,4.2f),Range(3.3f,10.6f));
                Color heat=i%3==0?new Color(1,.64f,.20f,.95f):i%4==0?new Color(.32f,.09f,.045f,.61f):new Color(1,.24f,.045f,.82f);
                sparks.Emit(point,kick,heat,Range(.045f,.142f),Range(.25f,.69f));
            }
        }

        internal void Hide(){tongues?.Hide();if(spill)spill.enabled=false;}
        void OnDestroy(){tongues?.Dispose();sparks?.Dispose();}
    }
}
