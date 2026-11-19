using UnityEngine;

namespace Raid.Nereid
{
    // Local presentation of confirmed feeding. No inventory mutation, RPC or retrospective replay.
    [DefaultExecutionOrder(650)]
    public sealed class NereidFeedingReaction : MonoBehaviour
    {
        [Header("Presentation")]
        [Min(0)] public float burpDelay=.42f;
        [Range(0,1)] public float voiceVolume=.32f;
        [Min(.1f)] public float audibleRange=38f;
        public Factory.FactoryVisuals factory;

        public long SwallowedFishObserved {get;private set;}
        public int BurpCount {get;private set;}
        public int LastBurpSequence {get;private set;}=int.MinValue;
        public bool IsBurping => Time.time<burpUntil;
        public int BurstParticleCount => (mist==null?0:mist.particleCount)+(drops==null?0:drops.particleCount)+(bubbles==null?0:bubbles.particleCount);

        Transform mouth,throat;
        Vector3 upperPosition,lowerPosition,upperScale,lowerScale,throatScale;
        ParticleSystem mist,drops,bubbles;
        AudioSource voice;
        Material particleMaterial,mistMaterial;
        Texture2D mistTexture;
        AudioClip swallowClip,burpClip;
        RaidWorld observedWorld;
        bool ready,hasSnapshot,wasActive,wasFeeding;
        int sequence,completed,lastScheduled=int.MinValue,pendingSequence=int.MinValue;
        float swallowUntil,burpUntil,burpAt=-1,nextSwallowSound,nextBind;

        void Awake() { TryBind(); }
        void OnEnable()
        {
            if(mist!=null)mist.Play();if(drops!=null)drops.Play();if(bubbles!=null)bubbles.Play();
        }

        bool TryBind()
        {
            if(NereidVessel.Active==null||NereidVessel.Active.gameObject.scene!=gameObject.scene)return false;
            if(factory==null)factory=Object.FindAnyObjectByType<Factory.FactoryVisuals>();
            if(factory==null||factory.gameObject.scene!=gameObject.scene||factory.upperJaw==null||factory.lowerJaw==null)return false;
            mouth=factory.upperJaw.parent;throat=mouth.Find("Deep living throat");
            upperPosition=factory.upperJaw.localPosition;lowerPosition=factory.lowerJaw.localPosition;
            upperScale=factory.upperJaw.localScale;lowerScale=factory.lowerJaw.localScale;
            if(throat!=null)throatScale=throat.localScale;
            var source=Resources.Load<Material>("NereidBubbles");
            if(source!=null&&factory.bubbleMesh!=null)
            {
                particleMaterial=new Material(source);
                particleMaterial.SetColor("_BaseColor",new Color(.78f,.86f,.82f,.8f));
                mist=Emitter("Mouth burp vapour",new Color(.48f,.58f,.53f,.10f),.3f,.65f,1.8f,3.5f,1.1f,1.45f,0,true);
                drops=Emitter("Mouth burp droplets",new Color(.38f,.59f,.59f,.32f),.035f,.10f,3.2f,5.7f,.5f,.95f,.5f);
                bubbles=Emitter("Mouth burp pressure bubbles",new Color(.46f,.68f,.68f,.24f),.09f,.21f,1.4f,3.1f,.8f,1.4f,-.075f);
            }
            var go=new GameObject("Living mouth spatial voice");go.transform.SetParent(mouth,false);go.transform.localPosition=new Vector3(0,-.12f,-.3f);
            voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;voice.dopplerLevel=0;
            voice.volume=voiceVolume;voice.minDistance=3;voice.maxDistance=audibleRange;voice.rolloffMode=AudioRolloffMode.Linear;
            swallowClip=MakeVoice(false);burpClip=MakeVoice(true);ready=true;return true;
        }

        void LateUpdate()
        {
            if(!ready)
            {
                if(Time.unscaledTime>=nextBind){nextBind=Time.unscaledTime+.4f;TryBind();}
                return;
            }
            voice.mute=!Application.isFocused;
            var world=RaidWorld.Instance;
            if(world==null||world.NetworkObject==null||!world.IsClientInitialized||world.View==null)
            {
                if(hasSnapshot)ResetObservation();
                AnimateMouth();return;
            }
            var route=world.FishRoute;
            if(!hasSnapshot||observedWorld!=world)
            {
                ResetObservation();observedWorld=world;hasSnapshot=true;Baseline(route);AnimateMouth();return;
            }
            if(route.sequence!=sequence)
            {
                // A new snapshot can already contain progress. It becomes the baseline, not old gulp events.
                if(route.sequence<sequence){ResetObservation();observedWorld=world;hasSnapshot=true;}
                Baseline(route);
            }
            else
            {
                if(route.feeding&&route.completed>completed)Swallow(route.completed-completed);
                if(wasActive&&!route.active&&wasFeeding&&route.feeding&&route.completed>0&&lastScheduled!=route.sequence)
                {
                    pendingSequence=route.sequence;lastScheduled=route.sequence;burpAt=Time.time+burpDelay;
                }
                completed=route.completed;wasActive=route.active;wasFeeding=route.feeding;
            }
            if(burpAt>=0&&Time.time>=burpAt)Burp();
            AnimateMouth();
        }

        void Baseline(FishRouteState route)
        {
            sequence=route.sequence;completed=route.completed;wasActive=route.active;wasFeeding=route.feeding;
            if(!route.active)lastScheduled=route.sequence;
        }

        void Swallow(int count)
        {
            SwallowedFishObserved+=count;swallowUntil=Time.time+.3f;
            if(voice!=null&&voice.isActiveAndEnabled&&Time.time>=nextSwallowSound)
            {nextSwallowSound=Time.time+.28f;voice.PlayOneShot(swallowClip,.48f);}
        }

        void Burp()
        {
            burpAt=-1;LastBurpSequence=pendingSequence;pendingSequence=int.MinValue;BurpCount++;burpUntil=Time.time+1.0f;
            if(voice!=null&&voice.isActiveAndEnabled){voice.Stop();voice.PlayOneShot(burpClip);}
            PlaceEmitter(mist);PlaceEmitter(drops);PlaceEmitter(bubbles);
            if(mist!=null)mist.Emit(22);if(drops!=null)drops.Emit(30);if(bubbles!=null)bubbles.Emit(24);
        }

        void AnimateMouth()
        {
            if(factory==null||factory.upperJaw==null||factory.lowerJaw==null)return;
            if(!factory.isActiveAndEnabled){RestoreMouth();return;}
            float swallow=Mathf.Clamp01((swallowUntil-Time.time)/.3f);
            swallow=Mathf.Sin(swallow*Mathf.PI);
            float age=1-(burpUntil-Time.time);float belch=age>=0&&age<1?Mathf.Sin(age*Mathf.PI)*Mathf.Exp(-age*1.4f):0;
            float opening=swallow*.045f+belch*.16f;
            // FactoryVisuals has already written its ordinary breath scale this frame.
            factory.upperJaw.localPosition=upperPosition+Vector3.up*opening*.5f;
            factory.lowerJaw.localPosition=lowerPosition+Vector3.down*opening;
            factory.upperJaw.localScale=Vector3.Scale(factory.upperJaw.localScale,new Vector3(1+swallow*.018f+belch*.025f,1,1));
            factory.lowerJaw.localScale=Vector3.Scale(factory.lowerJaw.localScale,new Vector3(1+swallow*.025f+belch*.045f,1,1));
            if(throat!=null)throat.localScale=Vector3.Scale(throatScale,new Vector3(1+swallow*.04f+belch*.055f,1+swallow*.045f+belch*.075f,1-swallow*.07f-belch*.08f));
        }

        ParticleSystem Emitter(string name,Color tint,float minSize,float maxSize,float minSpeed,float maxSpeed,float minLife,float maxLife,float gravity,bool vapour=false)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var scale=transform.lossyScale;go.transform.localScale=new Vector3(1/Mathf.Max(.001f,scale.x),1/Mathf.Max(.001f,scale.y),1/Mathf.Max(.001f,scale.z));
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.maxParticles=48;
            main.startColor=tint;main.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);main.startSpeed=new ParticleSystem.MinMaxCurve(minSpeed,maxSpeed);
            main.startLifetime=new ParticleSystem.MinMaxCurve(minLife,maxLife);main.gravityModifier=gravity;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=20;shape.radius=.8f;
            var emission=ps.emission;emission.rateOverTime=0;
            var color=ps.colorOverLifetime;color.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.7f,.5f),new GradientAlphaKey(0,1)});color.color=gradient;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=factory.bubbleMesh;
            if(vapour)
            {
                mistMaterial=new Material(particleMaterial);mistTexture=new Texture2D(64,64,TextureFormat.RGBA32,false,true);mistTexture.name="Project-authored soft mouth vapour";
                var pixels=new Color[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {float u=(x-31.5f)/31.5f,v=(y-31.5f)/31.5f;float alpha=Mathf.Pow(Mathf.Clamp01(1-u*u-v*v),2);pixels[y*64+x]=new Color(1,1,1,alpha);}
                mistTexture.SetPixels(pixels);mistTexture.Apply(false,true);mistTexture.wrapMode=TextureWrapMode.Clamp;mistMaterial.SetTexture("_BaseMap",mistTexture);
                renderer.sharedMaterial=mistMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            }
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            PlaceEmitter(ps);ps.Play();return ps;
        }

        void PlaceEmitter(ParticleSystem ps)
        {
            if(ps==null||mouth==null)return;
            ps.transform.position=mouth.TransformPoint(new Vector3(0,-.12f,-.48f));ps.transform.rotation=Quaternion.LookRotation(-mouth.forward,mouth.up);
        }

        static AudioClip MakeVoice(bool burp)
        {
            const int rate=22050;float duration=burp?.95f:.22f;var samples=new float[Mathf.CeilToInt(rate*duration)];
            var random=new System.Random(burp?12109:12108);float phase=0,noise=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=t/duration;
                noise=noise*.91f+(float)(random.NextDouble()*2-1)*.09f;
                float f=burp?Mathf.Lerp(86,43,u)+Mathf.Sin(t*21)*4:Mathf.Lerp(112,67,u);
                phase+=2*Mathf.PI*f/rate;
                float voice=Mathf.Sin(phase)*.46f+Mathf.Sin(phase*2+.12f)*.16f+Mathf.Sin(phase*3+.3f)*.08f;
                float attack=Mathf.SmoothStep(0,1,t/(burp?.065f:.035f));
                float envelope=attack*Mathf.Pow(1-u,burp?1.6f:1.1f);
                float flutter=.78f+.22f*Mathf.Sin(t*(burp?51:87)+Mathf.Sin(t*11));
                samples[i]=(voice+noise*(burp?.4f:.65f))*envelope*flutter;
            }
            var clip=AudioClip.Create(burp?"NEREID muffled wet burp":"NEREID confirmed swallow",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void ResetObservation()
        {
            hasSnapshot=false;observedWorld=null;burpAt=-1;pendingSequence=int.MinValue;lastScheduled=int.MinValue;swallowUntil=burpUntil=0;
            if(voice!=null)voice.Stop();
            foreach(var ps in new[]{mist,drops,bubbles})if(ps!=null)ps.Clear();
        }
        void RestoreMouth()
        {
            if(factory!=null&&factory.upperJaw!=null){factory.upperJaw.localPosition=upperPosition;factory.upperJaw.localScale=upperScale;}
            if(factory!=null&&factory.lowerJaw!=null){factory.lowerJaw.localPosition=lowerPosition;factory.lowerJaw.localScale=lowerScale;}
            if(throat!=null)throat.localScale=throatScale;
        }
        void OnDisable(){ResetObservation();if(ready)RestoreMouth();}
        void OnDestroy()
        {if(particleMaterial!=null)Destroy(particleMaterial);if(mistMaterial!=null)Destroy(mistMaterial);if(mistTexture!=null)Destroy(mistTexture);if(swallowClip!=null)Destroy(swallowClip);if(burpClip!=null)Destroy(burpClip);}
    }
}
