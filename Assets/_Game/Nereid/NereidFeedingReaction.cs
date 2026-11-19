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
        public bool IsBurping => BurpAge>=0&&BurpAge<burstDuration;
        public float LastBurpTime {get;private set;}=-1;
        public float BurpAge => LastBurpTime<0?-1:Time.time-LastBurpTime;
        public float PeakCaptureDelay => .25f;
        public float AnticipationRemaining => burpAt<0?0:Mathf.Max(0,burpAt-Time.time);
        public float JawOpeningMetres {get;private set;}
        public float ThroatWaveAmount {get;private set;}
        public int PeakParticleCount {get;private set;}
        public int LastBurpFood {get;private set;}
        public int LastBurpRequired {get;private set;}
        public float LastBurpHungerRemaining {get;private set;}
        public float LastBurpSeverity {get;private set;}=1;
        public bool LastBurpWasOverfed => LastBurpFood>LastBurpRequired&&LastBurpRequired>0;
        public float AppliedCameraPeakMetres => cameraImpulse==null?0:cameraImpulse.AppliedPeakMetres;
        public float AppliedCameraPeakDegrees => cameraImpulse==null?0:cameraImpulse.AppliedPeakDegrees;
        public bool CameraImpulseAppliedThisFrame => cameraImpulse!=null&&cameraImpulse.AppliedThisFrame;
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
        NereidFeedingCameraImpulse cameraImpulse;
        bool ownsCameraImpulse;
        AudioSource voice,pressureVoice;
        Material particleMaterial,mistMaterial,dropMaterial,bubbleMaterial;
        Texture2D mistTexture,dropTexture,bubbleTexture;
        Mesh dropletMesh;
        AudioClip swallowClip,burpClip,jetClip,inhaleClip;
        RaidWorld observedWorld;
        bool ready,hasSnapshot,wasActive,wasFeeding;
        int sequence,completed,lastScheduled=int.MinValue,pendingSequence=int.MinValue,foodPeak,foodRequired;
        float swallowAt=-10,burpAt=-1,nextSwallowSound,nextBind,jetUntil,burstDuration=1.7f,maxLowerTravel=.3f;

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
            var lowerMesh=factory.lowerJaw.GetComponent<MeshFilter>();
            if(lowerMesh!=null&&lowerMesh.sharedMesh!=null)
            {
                float floor=mouth.TransformPoint(lowerPosition+Vector3.up*(lowerMesh.sharedMesh.bounds.min.y*lowerScale.y)).y;
                maxLowerTravel=Mathf.Max(.035f,(floor-.06f)/Mathf.Max(.001f,Mathf.Abs(mouth.lossyScale.y)));
            }
            creature=Object.FindAnyObjectByType<NereidCreatureMotion>();
            cameraImpulse=GetComponent<NereidFeedingCameraImpulse>();if(cameraImpulse==null){cameraImpulse=gameObject.AddComponent<NereidFeedingCameraImpulse>();ownsCameraImpulse=true;}
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
                particleMaterial=new Material(source);particleMaterial.SetColor("_BaseColor",Color.white);particleMaterial.SetColor("_Color",Color.white);
                mistTexture=ParticleTexture(0);dropTexture=ParticleTexture(1);bubbleTexture=ParticleTexture(2);
                mistMaterial=ParticleMaterial("Soft turbulent condensation",mistTexture);
                dropMaterial=ParticleMaterial("Clear rounded water drops",dropTexture);
                bubbleMaterial=ParticleMaterial("Hollow wet foam highlights",bubbleTexture);
                dropletMesh=MakeDroplet();
                mist=Emitter("Burp condensation pressure cloud",new Color(.87f,.9f,.92f,.23f),.58f,1.25f,6.0f,9.5f,1.0f,1.9f,0,true);
                drops=Emitter("Burp short dimensional wet spray",new Color(.82f,.91f,.97f,.78f),.09f,.20f,9,15,.65f,1.15f,.7f);
                bubbles=Emitter("Burp hollow aerated foam",new Color(.88f,.95f,1,.8f),.12f,.32f,5,9,.9f,1.5f,-.05f);
                foamBlock=new MaterialPropertyBlock();
                for(int i=0;i<foam.Length;i++)
                {
                    var go=new GameObject("Broken expelled foam ring "+i);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();
                    line.useWorldSpace=true;line.loop=true;line.positionCount=48;line.sharedMaterial=particleMaterial;line.widthMultiplier=.12f;
                    var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(new Color(.84f,.91f,.94f),0),new GradientColorKey(new Color(.73f,.82f,.9f),1)},
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
                    voice.pitch=1;
                    if(voice.isActiveAndEnabled)voice.PlayOneShot(inhaleClip,.48f);
                    if(creature!=null)creature.PrepareBurp(burpDelay);
                    if(cameraImpulse!=null)cameraImpulse.Anticipate(mouth.position,burpDelay);
                }
                completed=route.completed;wasActive=route.active;wasFeeding=route.feeding;
            }
            if(route.feeding&&(route.active||burpAt>=0))ObserveFood(world);
            if(burpAt>=0&&Time.time>=burpAt)Burp();
            AnimateMouth();UpdateBurst();
        }
        void Baseline(FishRouteState route)
        {sequence=route.sequence;completed=route.completed;wasActive=route.active;wasFeeding=route.feeding;foodPeak=0;foodRequired=0;if(route.feeding)ObserveFood(observedWorld);if(!route.active)lastScheduled=route.sequence;}
        void ObserveFood(RaidWorld world)
        {
            if(world==null||world.View==null||world.progression==null)return;
            foodPeak=Mathf.Max(foodPeak,world.View.offering);
            long required=(long)world.progression.foodRequired+((long)world.View.highestDepth-1)*world.progression.foodPerDepth;
            foodRequired=(int)System.Math.Max(1,System.Math.Min(int.MaxValue,required));
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void BeginBackgroundCameraQa(float seconds){if(cameraImpulse!=null)cameraImpulse.BeginBackgroundQa(seconds);}
#endif
        void Swallow(int count)
        {
            SwallowedFishObserved+=count;swallowAt=Time.time;
            voice.pitch=1;
            if(voice.isActiveAndEnabled&&Time.time>=nextSwallowSound){nextSwallowSound=Time.time+.26f;voice.PlayOneShot(swallowClip,.7f);}
            if(creature!=null)creature.ConfirmedSwallow();
            if(cameraImpulse!=null)cameraImpulse.Swallow(mouth.position);
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
            ObserveFood(observedWorld);LastBurpFood=foodPeak;LastBurpRequired=Mathf.Max(1,foodRequired);
            LastBurpHungerRemaining=observedWorld!=null&&observedWorld.View!=null?observedWorld.View.hungerRemaining:0;
            float surplus=Mathf.Clamp01((LastBurpFood-LastBurpRequired)/(float)LastBurpRequired);
            LastBurpSeverity=1+1.35f*Mathf.Sqrt(surplus);float visualGain=Mathf.Lerp(1,1.55f,(LastBurpSeverity-1)/1.35f);
            burpAt=-1;LastBurpSequence=pendingSequence;pendingSequence=int.MinValue;BurpCount++;LastBurpTime=Time.time;jetUntil=Time.time+.42f;PeakParticleCount=0;burstDuration=1.7f+(LastBurpSeverity-1)*.35f;
            voice.pitch=Mathf.Lerp(1,.84f,surplus);pressureVoice.pitch=Mathf.Lerp(1,.91f,surplus);
            voice.volume=Mathf.Min(.88f,voiceVolume*Mathf.Lerp(1,1.2f,surplus));pressureVoice.volume=Mathf.Min(.48f,voiceVolume*.55f*Mathf.Lerp(1,1.12f,surplus));
            if(voice.isActiveAndEnabled){voice.Stop();voice.PlayOneShot(burpClip);}
            if(pressureVoice.isActiveAndEnabled)pressureVoice.PlayOneShot(jetClip);
            if(creature!=null)creature.ConfirmedBurp(LastBurpSeverity);
            if(cameraImpulse!=null)cameraImpulse.Release(mouth.position,LastBurpSeverity);
            PlaceEmitter(mist);PlaceEmitter(drops);PlaceEmitter(bubbles);
            foreach(var ps in new[]{mist,drops,bubbles})if(ps!=null)
            {var main=ps.main;float low=ps==mist?.58f:ps==drops?.09f:.12f,high=ps==mist?1.25f:ps==drops?.20f:.32f;main.startSize=new ParticleSystem.MinMaxCurve(low*visualGain,high*visualGain);}
            if(mist!=null)mist.Emit(Mathf.RoundToInt(38*visualGain));if(drops!=null)drops.Emit(Mathf.RoundToInt(96*visualGain));if(bubbles!=null)bubbles.Emit(Mathf.RoundToInt(42*visualGain));
        }

        void AnimateMouth()
        {
            if(factory==null||factory.upperJaw==null||factory.lowerJaw==null)return;
            if(!factory.isActiveAndEnabled){RestoreMouth();return;}
            float swallowAge=Time.time-swallowAt;
            float swallow=swallowAge>=0&&swallowAge<.52f?Mathf.Sin(swallowAge/.52f*Mathf.PI):0;
            float charge=burpAt>=0?Mathf.Clamp01(1-(burpAt-Time.time)/Mathf.Max(.2f,burpDelay)):0;
            float age=BurpAge;
            float release=age>=0&&age<burstDuration?Mathf.SmoothStep(0,1,age/.075f)*Mathf.Exp(-age*.9f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.8f,burstDuration,age))):0;
            float opening=swallow*.17f+release*.46f*Mathf.Sqrt(LastBurpSeverity)-charge*.11f;
            float lowerOpening=Mathf.Min(opening,maxLowerTravel),upperOpening=opening*.65f+Mathf.Max(0,opening-lowerOpening)*.8f;
            JawOpeningMetres=Mathf.Max(0,(lowerOpening+upperOpening)*Mathf.Abs(mouth.lossyScale.y));
            factory.upperJaw.localPosition=upperPosition+Vector3.up*upperOpening;
            factory.lowerJaw.localPosition=lowerPosition+Vector3.down*lowerOpening;
            float widen=swallow*.045f+release*.07f-charge*.022f;
            // FactoryVisuals has already written ordinary breathing. Only organic lip/teeth geometry moves.
            factory.upperJaw.localScale=Vector3.Scale(factory.upperJaw.localScale,new Vector3(1+widen,1,1));
            factory.lowerJaw.localScale=Vector3.Scale(factory.lowerJaw.localScale,new Vector3(1+widen*1.25f,1,1));
            foreach(var part in dentition)if(part.transform!=null)
            {
                part.transform.localPosition=part.position+Vector3.up*(part.upper?upperOpening:-lowerOpening)+(part.tongue?Vector3.forward*(swallow*.13f-release*.16f):Vector3.zero);
                part.transform.localScale=Vector3.Scale(part.scale,new Vector3(1+widen*(part.upper?1:1.25f),1,part.tongue?1-swallow*.12f:1));
            }
            ThroatWaveAmount=swallow*.14f+release*.19f+charge*.08f;
            if(throat!=null)throat.localScale=Vector3.Scale(throatScale,new Vector3(1+charge*.08f,1+charge*.09f,1-charge*.06f));
            DeformThroat(swallowAge,swallow,release,charge);
            if(pressureLight!=null)
                pressureLight.intensity=swallow*18+charge*8+(age>=0&&age<.75f?Mathf.Sin(Mathf.Clamp01(age/.75f)*Mathf.PI)*85*Mathf.Sqrt(LastBurpSeverity):0);
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
            float gain=Mathf.Lerp(1,1.55f,(LastBurpSeverity-1)/1.35f);
            SetRate(mist,jet?38*gain:0);SetRate(drops,jet?130*gain:0);SetRate(bubbles,jet?48*gain:0);
            float age=BurpAge;Vector3 origin=mouth.TransformPoint(new Vector3(0,-.12f,-.48f));
            for(int ring=0;ring<foam.Length;ring++)if(foam[ring]!=null)
            {
                float t=age-ring*.11f;bool visible=t>=0&&t<.78f;foam[ring].enabled=visible;if(!visible)continue;
                float radius=(1.1f+t*3.4f)*Mathf.Sqrt(gain);Vector3 centre=origin-mouth.forward*(.4f+t*9);
                for(int i=0;i<48;i++)
                {float a=i*Mathf.PI*2/48;float r=radius*(1+.075f*Mathf.Sin(a*5+t*9+ring));foam[ring].SetPosition(i,centre+mouth.right*(Mathf.Cos(a)*r)+mouth.up*(Mathf.Sin(a)*r*.67f));}
                foamBlock.SetColor("_BaseColor",new Color(.84f,.92f,.97f,(1-t/.78f)*.37f));foam[ring].SetPropertyBlock(foamBlock);foam[ring].widthMultiplier=.12f+t*.08f;
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
            main.startColor=new ParticleSystem.MinMaxGradient(new Color(tint.r*.78f,tint.g*.83f,tint.b*.88f,tint.a*.7f),tint);main.startSize=new ParticleSystem.MinMaxCurve(minSize,maxSize);main.startSpeed=new ParticleSystem.MinMaxCurve(minSpeed,maxSpeed);
            main.startLifetime=new ParticleSystem.MinMaxCurve(minLife,maxLife);main.gravityModifier=gravity;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=vapour?16:23;shape.radius=vapour?1.1f:1.25f;
            var emission=ps.emission;emission.rateOverTime=0;
            var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(new Color(1,.96f,.89f),0),new GradientColorKey(new Color(.83f,.9f,1),1)},
                new[]{new GradientAlphaKey(.72f,0),new GradientAlphaKey(1,.09f),new GradientAlphaKey(.75f,.48f),new GradientAlphaKey(0,1)});color.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,vapour?.65f:.8f),new Keyframe(.4f,1),new Keyframe(1,vapour?1.55f:.45f)));
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=bubbleMaterial;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Normal,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
            renderer.sortMode=ParticleSystemSortMode.Distance;
            if(vapour)
            {
                renderer.sharedMaterial=mistMaterial;
                main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);
                var noise=ps.noise;noise.enabled=true;noise.strength=.65f;noise.frequency=.85f;noise.scrollSpeed=.6f;noise.octaveCount=2;noise.damping=true;
                var limit=ps.limitVelocityOverLifetime;limit.enabled=true;limit.limit=4.5f;limit.dampen=.15f;
            }
            else if(gravity>.5f)
            {renderer.sharedMaterial=dropMaterial;renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=dropletMesh;renderer.alignment=ParticleSystemRenderSpace.Velocity;}
            else
            {var noise=ps.noise;noise.enabled=true;noise.strength=.25f;noise.frequency=1.15f;noise.scrollSpeed=.8f;}
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;PlaceEmitter(ps);ps.Play();return ps;
        }
        Material ParticleMaterial(string name,Texture2D texture)
        {
            var material=new Material(particleMaterial);material.name=name;material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",Color.white);material.SetColor("_Color",Color.white);return material;
        }
        // Independent neutral textures prevent opaque bubble disks and the broad stretched cloud boards.
        // Each puff has its own rotating, scalloped density field; bubbles keep only a thin rim and glints.
        static Texture2D ParticleTexture(int kind)
        {
            const int n=96;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false,true);
            texture.name=kind==0?"Authored billowing condensation":kind==1?"Authored clear droplet highlights":"Authored hollow bubble rim";
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n,v=(y+.5f)/n,px=u*2-1,py=v*2-1,r=Mathf.Sqrt(px*px+py*py);
                float a,luminance;
                if(kind==0)
                {
                    float noise=Mathf.PerlinNoise(u*5.7f+7.1f,v*5.7f+2.3f)*.65f+Mathf.PerlinNoise(u*11.3f,v*11.3f)*.35f;
                    float edge=Mathf.Clamp01((1-r-(noise-.5f)*.28f)*1.25f);
                    a=Mathf.Pow(edge,1.7f)*Mathf.Lerp(.32f,1,noise);luminance=.72f+noise*.28f;
                }
                else if(kind==1)
                {
                    // Latitude/longitude UVs on a small real 3D teardrop, without speed-based stretching.
                    float glint=Mathf.Exp(-Mathf.Pow((u-.3f)/.075f,2)-Mathf.Pow((v-.7f)/.18f,2));
                    float rim=Mathf.Pow(Mathf.Abs(Mathf.Cos(u*Mathf.PI*2)),5);
                    a=.12f+rim*.28f+glint*.58f;luminance=.48f+rim*.34f+glint*.35f;
                }
                else
                {
                    float rim=Mathf.Exp(-Mathf.Pow((r-.82f)/.046f,2));
                    float glint=Mathf.Exp(-Mathf.Pow((px+.36f)/.13f,2)-Mathf.Pow((py-.55f)/.095f,2));
                    float lower=Mathf.Exp(-Mathf.Pow((r-.68f)/.055f,2))*Mathf.Clamp01(-py)*.24f;
                    a=(rim*(.3f+.5f*Mathf.Clamp01(py+.45f))+glint*.95f+lower)*Mathf.Clamp01((1-r)*12);
                    luminance=.75f+.25f*Mathf.Clamp01(glint+py*.35f);
                }
                pixels[y*n+x]=new Color(luminance,luminance,luminance,Mathf.Clamp01(a));
            }
            texture.SetPixels(pixels);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;texture.Apply(false,true);return texture;
        }
        static Mesh MakeDroplet()
        {
            const int sides=10,rings=7;var vertices=new Vector3[(sides+1)*(rings+1)];var uv=new Vector2[vertices.Length];var triangles=new int[sides*rings*6];int t=0;
            for(int ring=0;ring<=rings;ring++)for(int side=0;side<=sides;side++)
            {
                float v=ring/(float)rings,u=side/(float)sides,theta=v*Mathf.PI,angle=u*Mathf.PI*2;
                float radius=Mathf.Sin(theta)*.33f*(.86f+.14f*v);int i=ring*(sides+1)+side;
                vertices[i]=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,Mathf.Cos(theta)*.75f);uv[i]=new Vector2(u,v);
                if(ring<rings&&side<sides){int j=i+sides+1;triangles[t++]=i;triangles[t++]=j;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=j;triangles[t++]=j+1;}
            }
            var mesh=new Mesh{name="Short rounded velocity-aligned water droplets"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
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
            if(cameraImpulse!=null)cameraImpulse.Cancel();
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
            if(dropMaterial!=null)Destroy(dropMaterial);if(bubbleMaterial!=null)Destroy(bubbleMaterial);
            if(dropTexture!=null)Destroy(dropTexture);if(bubbleTexture!=null)Destroy(bubbleTexture);if(dropletMesh!=null)Destroy(dropletMesh);
            if(swallowClip!=null)Destroy(swallowClip);if(burpClip!=null)Destroy(burpClip);if(jetClip!=null)Destroy(jetClip);if(inhaleClip!=null)Destroy(inhaleClip);
            if(ownsCameraImpulse&&cameraImpulse!=null)Destroy(cameraImpulse);
        }
    }
}
