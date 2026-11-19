using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    // Presentation of confirmed feeding only. Mounting steel, economy, RPCs and player control are untouched.
    [DefaultExecutionOrder(650)]
    public sealed class NereidFeedingReaction : MonoBehaviour
    {
        [Header("Presentation")]
        [Min(.2f)] public float burpDelay=.44f;
        [Range(0,1)] public float voiceVolume=.48f;
        [Min(.1f)] public float audibleRange=46f;
        public Factory.FactoryVisuals factory;

        public long SwallowedFishObserved {get;private set;}
        public int BurpCount {get;private set;}
        public int LastBurpSequence {get;private set;}=int.MinValue;
        public bool IsBurping => BurpAge>=0&&BurpAge<1.7f;
        public float LastBurpTime {get;private set;}=-1;
        public float BurpAge => LastBurpTime<0?-1:Time.time-LastBurpTime;
        public float PeakCaptureDelay => .25f;
        public float AnticipationRemaining => burpAt<0?0:Mathf.Max(0,burpAt-Time.time);
        public float JawOpeningMetres {get;private set;}
        public float ThroatWaveAmount {get;private set;}
        public int PeakParticleCount {get;private set;}
        public int BurstParticleCount => (mist==null?0:mist.particleCount)+(drops==null?0:drops.particleCount)+(bubbles==null?0:bubbles.particleCount);

        Transform mouth,throat;
        Vector3 upperPosition,lowerPosition,upperScale,lowerScale,throatScale;
        readonly List<OrganicPart> dentition=new();
        struct OrganicPart {public Transform transform;public Vector3 position,scale;public bool upper,tongue;}
        MeshFilter throatFilter;
        Mesh originalThroat,animatedThroat;
        Vector3[] throatRest,throatWork;
        bool throatWasDeformed;
        ParticleSystem mist,drops,bubbles;
        readonly LineRenderer[] foam=new LineRenderer[2];
        MaterialPropertyBlock foamBlock;
        Light pressureLight;
        NereidCreatureMotion creature;
        AudioSource voice,pressureVoice;
        Material particleMaterial,mistMaterial;
        Texture2D mistTexture;
        AudioClip swallowClip,burpClip,jetClip,inhaleClip;
        RaidWorld observedWorld;
        bool ready,hasSnapshot,wasActive,wasFeeding;
        int sequence,completed,lastScheduled=int.MinValue,pendingSequence=int.MinValue;
        float swallowAt=-10,burpAt=-1,nextSwallowSound,nextBind,jetUntil;

        void Awake(){TryBind();}
        void OnEnable()
        {
            if(mist!=null)mist.Play();if(drops!=null)drops.Play();if(bubbles!=null)bubbles.Play();
            if(throatFilter!=null&&animatedThroat!=null)throatFilter.sharedMesh=animatedThroat;
        }
        bool TryBind()
        {
            if(NereidVessel.Active==null||NereidVessel.Active.gameObject.scene!=gameObject.scene)return false;
            if(factory==null)factory=Object.FindAnyObjectByType<Factory.FactoryVisuals>();
            if(factory==null||factory.gameObject.scene!=gameObject.scene||factory.upperJaw==null||factory.lowerJaw==null)return false;
            mouth=factory.upperJaw.parent;throat=mouth.Find("Deep living throat");
            upperPosition=factory.upperJaw.localPosition;lowerPosition=factory.lowerJaw.localPosition;
            upperScale=factory.upperJaw.localScale;lowerScale=factory.lowerJaw.localScale;
            creature=Object.FindAnyObjectByType<NereidCreatureMotion>();
            foreach(Transform child in mouth)
            {
                bool fang=child.name.Contains("fang"),tongue=child.name=="Curved muscular tongue",palate=child.name.StartsWith("Palate muscular fold");
                if(!fang&&!tongue&&!palate)continue;
                var filter=child.GetComponent<MeshFilter>();
                bool upper=palate||(!tongue&&filter!=null&&filter.sharedMesh!=null&&filter.sharedMesh.bounds.center.y>0);
                dentition.Add(new OrganicPart{transform=child,position=child.localPosition,scale=child.localScale,upper=upper,tongue=tongue});
            }
            if(throat!=null)
            {
                throatScale=throat.localScale;throatFilter=throat.GetComponent<MeshFilter>();
                if(throatFilter!=null&&throatFilter.sharedMesh!=null)
                {
                    originalThroat=throatFilter.sharedMesh;animatedThroat=Instantiate(originalThroat);animatedThroat.name="Living throat / confirmed meal wave";
                    animatedThroat.MarkDynamic();throatRest=animatedThroat.vertices;throatWork=new Vector3[throatRest.Length];
                    var bounds=animatedThroat.bounds;bounds.Expand(.8f);animatedThroat.bounds=bounds;throatFilter.sharedMesh=animatedThroat;
                }
            }
            var source=Resources.Load<Material>("NereidBubbles");
            if(source!=null&&factory.bubbleMesh!=null)
            {
                particleMaterial=new Material(source);particleMaterial.SetColor("_BaseColor",new Color(.9f,.94f,.89f,.95f));
                mist=Emitter("Burp condensation pressure cloud",new Color(.63f,.69f,.61f,.46f),1.15f,2.4f,6.0f,9.5f,1.0f,1.9f,0,true);
                drops=Emitter("Burp long wet spray",new Color(.55f,.73f,.69f,.72f),.065f,.15f,9,15,.65f,1.15f,.7f);
                bubbles=Emitter("Burp aerated foam",new Color(.7f,.82f,.75f,.48f),.14f,.36f,5,9,.9f,1.5f,-.05f);
                var renderer=drops.GetComponent<ParticleSystemRenderer>();renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=.10f;renderer.lengthScale=2.2f;renderer.sharedMaterial=mistMaterial;
                foamBlock=new MaterialPropertyBlock();
                for(int i=0;i<foam.Length;i++)
                {
                    var go=new GameObject("Broken expelled foam ring "+i);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();
                    line.useWorldSpace=true;line.loop=true;line.positionCount=48;line.sharedMaterial=particleMaterial;line.widthMultiplier=.12f;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.68f,.78f,.69f),0),new GradientColorKey(new Color(.68f,.78f,.69f),1)},
                        new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.7f,.08f),new GradientAlphaKey(0,.22f),new GradientAlphaKey(.8f,.36f),new GradientAlphaKey(0,.53f),new GradientAlphaKey(.65f,.72f),new GradientAlphaKey(0,.88f),new GradientAlphaKey(0,1)});
                    line.colorGradient=gradient;line.enabled=false;foam[i]=line;
                }
            }
            voice=Audio("Mouth low resonant burp",1);pressureVoice=Audio("Mouth wet pressure release",.55f);
            swallowClip=MakeVoice(0);inhaleClip=MakeVoice(1);burpClip=MakeVoice(2);jetClip=MakeVoice(3);
            var flash=new GameObject("Actual burp warm reflected light");flash.transform.SetParent(mouth,false);flash.transform.localPosition=new Vector3(0,0,-.6f);
            pressureLight=flash.AddComponent<Light>();pressureLight.type=LightType.Point;pressureLight.color=new Color(1,.79f,.51f);pressureLight.range=12;pressureLight.intensity=0;pressureLight.shadows=LightShadows.None;
            ready=true;return true;
        }

        void LateUpdate()
        {
            if(!ready){if(Time.unscaledTime>=nextBind){nextBind=Time.unscaledTime+.4f;TryBind();}return;}
            voice.mute=pressureVoice.mute=!Application.isFocused;
            var world=RaidWorld.Instance;
            if(world==null||world.NetworkObject==null||!world.IsClientInitialized||world.View==null)
            {if(hasSnapshot)ResetObservation();AnimateMouth();return;}
            var route=world.FishRoute;
            if(!hasSnapshot||observedWorld!=world)
            {ResetObservation();observedWorld=world;hasSnapshot=true;Baseline(route);AnimateMouth();return;}
            if(route.sequence!=sequence)
            {if(route.sequence<sequence){ResetObservation();observedWorld=world;hasSnapshot=true;}Baseline(route);}
            else
            {
                if(route.feeding&&route.completed>completed)Swallow(route.completed-completed);
                if(wasActive&&!route.active&&wasFeeding&&route.feeding&&route.completed>0&&lastScheduled!=route.sequence)
                {
                    pendingSequence=route.sequence;lastScheduled=route.sequence;burpAt=Time.time+burpDelay;
                    if(voice.isActiveAndEnabled)voice.PlayOneShot(inhaleClip,.48f);
                    if(creature!=null)creature.PrepareBurp(burpDelay);
                }
                completed=route.completed;wasActive=route.active;wasFeeding=route.feeding;
            }
            if(burpAt>=0&&Time.time>=burpAt)Burp();
            AnimateMouth();UpdateBurst();
        }
        void Baseline(FishRouteState route)
        {sequence=route.sequence;completed=route.completed;wasActive=route.active;wasFeeding=route.feeding;if(!route.active)lastScheduled=route.sequence;}
        void Swallow(int count)
        {
            SwallowedFishObserved+=count;swallowAt=Time.time;
            if(voice.isActiveAndEnabled&&Time.time>=nextSwallowSound){nextSwallowSound=Time.time+.26f;voice.PlayOneShot(swallowClip,.7f);}
            if(creature!=null)creature.ConfirmedSwallow();
            if(bubbles!=null)
            {
                Vector3 point=mouth.TransformPoint(new Vector3(0,-.12f,-.48f));
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7+(float)(SwallowedFishObserved%7);var emit=new ParticleSystem.EmitParams();
                    emit.position=point+mouth.right*Mathf.Cos(a)*.65f+mouth.up*Mathf.Sin(a)*.35f;
                    emit.velocity=-mouth.forward*1.4f+mouth.up*.5f;emit.startSize=.13f;emit.startLifetime=.5f;emit.startColor=new Color(.57f,.75f,.66f,.55f);bubbles.Emit(emit,1);
                }
            }
        }
        void Burp()
        {
            burpAt=-1;LastBurpSequence=pendingSequence;pendingSequence=int.MinValue;BurpCount++;LastBurpTime=Time.time;jetUntil=Time.time+.42f;PeakParticleCount=0;
            if(voice.isActiveAndEnabled){voice.Stop();voice.PlayOneShot(burpClip);}
            if(pressureVoice.isActiveAndEnabled)pressureVoice.PlayOneShot(jetClip);
            if(creature!=null)creature.ConfirmedBurp();
            PlaceEmitter(mist);PlaceEmitter(drops);PlaceEmitter(bubbles);
            if(mist!=null)mist.Emit(66);if(drops!=null)drops.Emit(96);if(bubbles!=null)bubbles.Emit(56);
        }

        void AnimateMouth()
        {
            if(factory==null||factory.upperJaw==null||factory.lowerJaw==null)return;
            if(!factory.isActiveAndEnabled){RestoreMouth();return;}
            float swallowAge=Time.time-swallowAt;
            float swallow=swallowAge>=0&&swallowAge<.52f?Mathf.Sin(swallowAge/.52f*Mathf.PI):0;
            float charge=burpAt>=0?Mathf.Clamp01(1-(burpAt-Time.time)/Mathf.Max(.2f,burpDelay)):0;
            float age=BurpAge;
            float release=age>=0&&age<1.7f?Mathf.SmoothStep(0,1,age/.075f)*Mathf.Exp(-age*.9f)*(1-Mathf.SmoothStep(.8f,1.7f,age)):0;
            float opening=swallow*.17f+release*.46f-charge*.11f;
            JawOpeningMetres=Mathf.Max(0,opening*1.65f*Mathf.Abs(mouth.lossyScale.y));
            factory.upperJaw.localPosition=upperPosition+Vector3.up*opening*.65f;
            factory.lowerJaw.localPosition=lowerPosition+Vector3.down*opening;
            float widen=swallow*.045f+release*.07f-charge*.022f;
            // FactoryVisuals has already written ordinary breathing. Only organic lip/teeth geometry moves.
            factory.upperJaw.localScale=Vector3.Scale(factory.upperJaw.localScale,new Vector3(1+widen,1,1));
            factory.lowerJaw.localScale=Vector3.Scale(factory.lowerJaw.localScale,new Vector3(1+widen*1.25f,1,1));
            foreach(var part in dentition)if(part.transform!=null)
            {
                part.transform.localPosition=part.position+Vector3.up*(part.upper?opening*.65f:-opening)+(part.tongue?Vector3.forward*(swallow*.13f-release*.16f):Vector3.zero);
                part.transform.localScale=Vector3.Scale(part.scale,new Vector3(1+widen*(part.upper?1:1.25f),1,part.tongue?1-swallow*.12f:1));
            }
            ThroatWaveAmount=swallow*.14f+release*.19f+charge*.08f;
            if(throat!=null)throat.localScale=Vector3.Scale(throatScale,new Vector3(1+charge*.08f,1+charge*.09f,1-charge*.06f));
            DeformThroat(swallowAge,swallow,release,charge);
            if(pressureLight!=null)
                pressureLight.intensity=swallow*18+charge*8+(age>=0&&age<.75f?Mathf.Sin(Mathf.Clamp01(age/.75f)*Mathf.PI)*85:0);
        }
        void DeformThroat(float swallowAge,float swallow,float release,float charge)
        {
            if(animatedThroat==null||throatRest==null)return;
            bool active=swallow>0||release>0||charge>0;if(!active&&!throatWasDeformed)return;
            float min=originalThroat.bounds.min.z,length=Mathf.Max(.1f,originalThroat.bounds.size.z);
            for(int i=0;i<throatRest.Length;i++)
            {
                Vector3 p=throatRest[i];float z=Mathf.Clamp01((p.z-min)/length);
                float travelling=Mathf.Exp(-Mathf.Pow((z-swallowAge/.55f)/.23f,2))*swallow*.19f;
                float ripple=release*(.12f+.065f*Mathf.Sin(z*10-Time.time*18))+charge*.07f;
                p.x*=1+travelling+ripple;p.y*=1+travelling+ripple;throatWork[i]=p;
            }
            animatedThroat.vertices=throatWork;animatedThroat.RecalculateNormals();throatWasDeformed=active;
        }
        void UpdateBurst()
        {
            bool jet=Time.time<jetUntil&&IsBurping;
            SetRate(mist,jet?72:0);SetRate(drops,jet?130:0);SetRate(bubbles,jet?58:0);
            float age=BurpAge;Vector3 origin=mouth.TransformPoint(new Vector3(0,-.12f,-.48f));
            for(int ring=0;ring<foam.Length;ring++)if(foam[ring]!=null)
            {
                float t=age-ring*.11f;bool visible=t>=0&&t<.78f;foam[ring].enabled=visible;if(!visible)continue;
                float radius=1.1f+t*3.4f;Vector3 centre=origin-mouth.forward*(.4f+t*9);
                for(int i=0;i<48;i++)
                {float a=i*Mathf.PI*2/48;float r=radius*(1+.075f*Mathf.Sin(a*5+t*9+ring));foam[ring].SetPosition(i,centre+mouth.right*(Mathf.Cos(a)*r)+mouth.up*(Mathf.Sin(a)*r*.67f));}
                foamBlock.SetColor("_BaseColor",new Color(.76f,.84f,.75f,(1-t/.78f)*.37f));foam[ring].SetPropertyBlock(foamBlock);foam[ring].widthMultiplier=.12f+t*.08f;
            }
            if(IsBurping)PeakParticleCount=Mathf.Max(PeakParticleCount,BurstParticleCount);
        }
        static void SetRate(ParticleSystem ps,float rate){if(ps!=null){var emission=ps.emission;emission.rateOverTime=rate;}}

        ParticleSystem Emitter(string name,Color tint,float minSize,float maxSize,float minSpeed,float maxSpeed,float minLife,float maxLife,float gravity,bool vapour=false)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);var scale=transform.lossyScale;
            go.transform.localScale=new Vector3(1/Mathf.Max(.001f,scale.x),1/Mathf.Max(.001f,scale.y),1/Mathf.Max(.001f,scale.z));
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Hierarchy;main.maxParticles=200;
            main.startColor=tint;main.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);main.startSpeed=new ParticleSystem.MinMaxCurve(minSpeed,maxSpeed);
            main.startLifetime=new ParticleSystem.MinMaxCurve(minLife,maxLife);main.gravityModifier=gravity;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=vapour?16:23;shape.radius=vapour?1.1f:1.25f;
            var emission=ps.emission;emission.rateOverTime=0;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.72f,.79f,.75f),1)},
                new[]{new GradientAlphaKey(.72f,0),new GradientAlphaKey(1,.09f),new GradientAlphaKey(.75f,.48f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,vapour?.65f:.8f),new Keyframe(.4f,1),new Keyframe(1,vapour?1.55f:.45f)));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=factory.bubbleMesh;
            if(vapour)
            {
                mistMaterial=new Material(particleMaterial);mistTexture=new Texture2D(64,64,TextureFormat.RGBA32,false,true);mistTexture.name="Project-authored turbulent condensation";
                var pixels=new Color[64*64];
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {float u=(x-31.5f)/31.5f,v=(y-31.5f)/31.5f;float radial=Mathf.Pow(Mathf.Clamp01(1-u*u-v*v),1.35f);float wisps=.83f+.17f*Mathf.Sin(u*17+v*11)*Mathf.Sin(v*19-u*7);pixels[y*64+x]=new Color(1,1,1,radial*wisps);}
                mistTexture.SetPixels(pixels);mistTexture.Apply(false,true);mistTexture.wrapMode=TextureWrapMode.Clamp;mistMaterial.SetTexture("_BaseMap",mistTexture);
                renderer.sharedMaterial=mistMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            }
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;PlaceEmitter(ps);ps.Play();return ps;
        }
        void PlaceEmitter(ParticleSystem ps)
        {if(ps==null||mouth==null)return;ps.transform.position=mouth.TransformPoint(new Vector3(0,-.12f,-.48f));ps.transform.rotation=Quaternion.LookRotation(-mouth.forward,mouth.up);}
        AudioSource Audio(string name,float multiplier)
        {
            var go=new GameObject(name);go.transform.SetParent(mouth,false);go.transform.localPosition=new Vector3(0,-.12f,-.3f);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;source.volume=voiceVolume*multiplier;
            source.minDistance=4;source.maxDistance=audibleRange;source.rolloffMode=AudioRolloffMode.Linear;
            var low=go.AddComponent<AudioLowPassFilter>();low.cutoffFrequency=multiplier<1?3300:1550;low.lowpassResonanceQ=1;return source;
        }
        static AudioClip MakeVoice(int kind)
        {
            const int rate=22050;float duration=kind==0?.30f:kind==1?.42f:kind==2?1.35f:1.55f;
            var samples=new float[Mathf.CeilToInt(rate*duration)];var random=new System.Random(12108+kind);float phase=0,noise=0,slower=0,peak=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)rate,u=t/duration;float raw=(float)(random.NextDouble()*2-1);noise=noise*.81f+raw*.19f;slower=slower*.965f+raw*.035f;
                float f=kind==2?Mathf.Lerp(94,35,u)+Mathf.Sin(t*22)*5:Mathf.Lerp(120,55,u);phase+=2*Mathf.PI*f/rate;
                float harmonic=Mathf.Sin(phase)*.48f+Mathf.Sin(phase*2+.12f)*.18f+Mathf.Sin(phase*3+.3f)*.09f;
                float attack=Mathf.SmoothStep(0,1,t/(kind==2?.04f:.025f));
                float envelope=attack*Mathf.Pow(1-u,kind==2?1.2f:1.1f);
                float flutter=.78f+.22f*Mathf.Sin(t*(kind==2?51:87)+Mathf.Sin(t*11));
                float value=kind==3?(noise*.75f+slower*.35f)*envelope*(.75f+.25f*Mathf.Sin(t*37)):
                    kind==1?(noise*.2f+Mathf.Sin(phase)*.06f)*Mathf.Sin(Mathf.PI*u)*(.25f+.6f*u):(harmonic+slower*.5f)*envelope*flutter;
                samples[i]=value;peak=Mathf.Max(peak,Mathf.Abs(value));
            }
            float ceiling=kind==2?.62f:kind==3?.45f:.50f;float normalise=peak>.0001f?ceiling/peak:1;
            for(int i=0;i<samples.Length;i++)samples[i]*=normalise;
            var clip=AudioClip.Create(kind==0?"NEREID heavy confirmed gulp":kind==1?"NEREID throat pressure anticipation":kind==2?"NEREID resonant wet burp":"NEREID layered pressure spray",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }

        void ResetObservation()
        {
            hasSnapshot=false;observedWorld=null;burpAt=-1;pendingSequence=int.MinValue;lastScheduled=int.MinValue;swallowAt=-10;LastBurpTime=-1;jetUntil=0;JawOpeningMetres=ThroatWaveAmount=0;
            if(voice!=null)voice.Stop();if(pressureVoice!=null)pressureVoice.Stop();
            if(mist!=null){SetRate(mist,0);mist.Clear();}if(drops!=null){SetRate(drops,0);drops.Clear();}if(bubbles!=null){SetRate(bubbles,0);bubbles.Clear();}
            foreach(var ring in foam)if(ring!=null)ring.enabled=false;if(pressureLight!=null)pressureLight.intensity=0;
            if(creature!=null)creature.ClearFeedingReaction();
        }
        void RestoreMouth()
        {
            if(factory!=null&&factory.upperJaw!=null){factory.upperJaw.localPosition=upperPosition;factory.upperJaw.localScale=upperScale;}
            if(factory!=null&&factory.lowerJaw!=null){factory.lowerJaw.localPosition=lowerPosition;factory.lowerJaw.localScale=lowerScale;}
            if(throat!=null)throat.localScale=throatScale;
            foreach(var part in dentition)if(part.transform!=null){part.transform.localPosition=part.position;part.transform.localScale=part.scale;}
            if(throatFilter!=null&&originalThroat!=null)throatFilter.sharedMesh=originalThroat;
        }
        void OnDisable(){ResetObservation();if(ready)RestoreMouth();}
        void OnDestroy()
        {
            RestoreMouth();if(animatedThroat!=null)Destroy(animatedThroat);
            if(particleMaterial!=null)Destroy(particleMaterial);if(mistMaterial!=null)Destroy(mistMaterial);if(mistTexture!=null)Destroy(mistTexture);
            if(swallowClip!=null)Destroy(swallowClip);if(burpClip!=null)Destroy(burpClip);if(jetClip!=null)Destroy(jetClip);if(inhaleClip!=null)Destroy(inhaleClip);
        }
    }
}
