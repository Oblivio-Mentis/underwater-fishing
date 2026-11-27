using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Raid
{
    public sealed partial class UnderwaterLook
    {
        DiverMotor viewed;
        bool initialFog,appliedFog;
        FogMode initialMode;
        float initialDensity,wetBlend;
        Color initialColor;
        UnityEngine.Rendering.AmbientMode deckAmbientMode;
        Color deckSky,deckHorizon,deckGround;
        UnderwaterGrading grading;
        Light oceanFill;
        float oceanFillBase=1.5f;
        int shownState;
        float predatorProximity, breathPhase;
        const float BeamCandela=245f;
        bool lampOn;
        float lampGain=.78f,targetLampGain=.78f,nextLampSample;
        Light diveLamp;
        static readonly int WaterShader=Shader.PropertyToID("_NereidTurbidity");
        static readonly int LampShader=Shader.PropertyToID("_NereidLampForward");
        static readonly int LampOriginShader=Shader.PropertyToID("_NereidLampOriginRange");
        static readonly int LampConeShader=Shader.PropertyToID("_NereidLampCone");
        static readonly int WaterTintShader=Shader.PropertyToID("_NereidWaterTint");
        static readonly int ReefClarityShader=Shader.PropertyToID("_NereidReefClarity");
        public float WaterBlend=>wetBlend;
        internal Camera DiverCamera => camera;
        internal bool EyeSubmerged => IsEyeUnderwater();

        void SaveOceanSettings()
        {
            initialFog=RenderSettings.fog;
            initialMode=RenderSettings.fogMode;
            initialDensity=RenderSettings.fogDensity;
            initialColor=RenderSettings.fogColor;
            deckAmbientMode=RenderSettings.ambientMode;
            deckSky=RenderSettings.ambientSkyColor;
            deckHorizon=RenderSettings.ambientEquatorColor;
            deckGround=RenderSettings.ambientGroundColor;
            grading=UnderwaterGrading.Active;
            foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if(light!=null && light.name=="Ocean reflected fill"){oceanFill=light;oceanFillBase=light.intensity;break;}
        }

        // The dry ship and the airlock keep their authored ambience; the sea states replace it.
        void ApplyAmbient(UnderwaterGrading.State state,float blend)
        {
            RenderSettings.ambientMode=blend>0?UnityEngine.Rendering.AmbientMode.Trilight:deckAmbientMode;
            if(blend<=0)return;
            RenderSettings.ambientSkyColor=Color.Lerp(deckSky,state.sky,blend);
            RenderSettings.ambientEquatorColor=Color.Lerp(deckHorizon,state.equator,blend);
            RenderSettings.ambientGroundColor=Color.Lerp(deckGround,state.ground,blend);
        }

        void BindDiveLamp(Transform eye)
        {
            if(diveLamp!=null)Destroy(diveLamp.gameObject);
            var node=new GameObject("Diver helmet inspection light");
            node.transform.SetParent(eye,false);
            node.transform.localPosition=new Vector3(-.17f,.20f,.22f);
            // The helmet-mounted lamp converges on the reticle at normal fishing distance.
            node.transform.localRotation=Quaternion.LookRotation(Vector3.forward*8f-node.transform.localPosition);
            diveLamp=node.AddComponent<Light>();
            diveLamp.type=LightType.Spot;diveLamp.spotAngle=56;diveLamp.innerSpotAngle=24;
            diveLamp.range=24;diveLamp.color=new Color(.91f,.96f,1f);
            diveLamp.renderMode=LightRenderMode.ForcePixel;
            diveLamp.lightUnit=UnityEngine.Rendering.LightUnit.Candela;
            diveLamp.shadows=LightShadows.None;diveLamp.intensity=BeamCandela;diveLamp.enabled=false;
            float outer=Mathf.Cos(.5f*diveLamp.spotAngle*Mathf.Deg2Rad);
            float inner=Mathf.Cos(.5f*diveLamp.innerSpotAngle*Mathf.Deg2Rad);
            Shader.SetGlobalVector(LampConeShader,new Vector4(outer,inner,0,0));
            lampOn=true;
            nextLampSample=0;
        }

        void UpdateDiveLamp(bool submerged)
        {
            if(diveLamp==null)return;
            if(submerged && viewed!=null && viewed.IsOwner && !viewed.IsDead &&
                !UI.UiInputGate.Blocked && Keyboard.current!=null && Keyboard.current.lKey.wasPressedThisFrame)
                lampOn=!lampOn;

            diveLamp.enabled=lampOn && viewed!=null && !viewed.IsDead && wetBlend>.04f;
            var lamp=diveLamp.transform;
            var pos=lamp.position;
            var dir=lamp.forward;
            if(diveLamp.enabled && Time.unscaledTime>=nextLampSample)
            {
                nextLampSample=Time.unscaledTime+.10f;
                // Wreck plating and close rocks should retain their colour inside the beam.
                targetLampGain=Physics.Raycast(pos,dir,out var surface,9f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    ?Mathf.Lerp(.24f,.78f,Mathf.InverseLerp(.8f,8f,surface.distance)): .78f;
            }
            lampGain=Mathf.Lerp(lampGain,targetLampGain,1-Mathf.Exp(-Time.unscaledDeltaTime*9f));
            diveLamp.intensity=BeamCandela*wetBlend*lampGain;
            Shader.SetGlobalVector(LampShader,new Vector4(dir.x,dir.y,dir.z,diveLamp.enabled?diveLamp.intensity:0));
            Shader.SetGlobalVector(LampOriginShader,new Vector4(pos.x,pos.y,pos.z,diveLamp.range));
        }

        bool IsEyeUnderwater()
        {
            if(camera==null || viewed==null) return false;
            var chamber=Nereid.NereidAirlock.Active;
            var eye=camera.transform.position;
            return chamber!=null && chamber.Contains(eye)?chamber.IsSubmerged(eye):!viewed.IsInside;
        }

        bool InAirlock()
        {
            if(camera==null)return false;
            var chamber=Nereid.NereidAirlock.Active;
            return chamber!=null && chamber.Contains(camera.transform.position);
        }

        void LateUpdate()
        {
            if(Nereid.NereidVessel.Active==null)return;
            bool wet=IsEyeUnderwater();
            float dt=Time.unscaledDeltaTime;
            wetBlend=Mathf.MoveTowards(wetBlend,wet?1:0,dt*2.4f);
            if(wetBlend<=0 && !appliedFog && !InAirlock())return;
            UpdateDiveLamp(wet);
            bool abyss=RaidWorld.Instance!=null && RaidWorld.Instance.CurrentDepth>=2;
            bool inLock=InAirlock();
            float height=camera!=null?camera.transform.position.y:0f;
            float belowShelf=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-13f,-24f,height));
            float belowRift=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-24f,-40f,height));
            float lowerWater=abyss?belowRift:belowShelf;
            var state=!wet&&inLock?grading.Airlock:abyss?grading.Abyss:grading.Shallow;
            float gradeBlend=wet?wetBlend:inLock?.65f:0f;
            ApplyAmbient(wet?state:grading.Deck,gradeBlend);
            if(wetBlend>0)
            {
                appliedFog=true;
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
                float density=Mathf.Lerp(state.fogTop,state.fogBottom,lowerWater);
                var raid=ThreatDirector.Instance;
                float concealment=raid?raid.DefenseConcealment:0;
                float ink=raid && camera?raid.DefenseInkAt(camera.transform.position):0;
                float murk=Mathf.Clamp01(concealment+ink*.55f);
                RenderSettings.fogDensity=Mathf.Lerp(initialDensity,density+wetBlend*(concealment*.043f+ink*.16f),wetBlend);
                var waterTint=Color.Lerp(Color.Lerp(state.tintTop,state.tintBottom,lowerWater),new Color(.006f,.013f,.021f),murk);
                Shader.SetGlobalFloat("_RaidConcealment",murk*wetBlend);
                // Custom reef and hull transmission follows the camera through the same layers.
                var clarity=Vector4.Lerp(state.clarityTop,state.clarityBottom,lowerWater);
                clarity.x=Mathf.Lerp(clarity.x,Mathf.Max(.013f,clarity.x),murk);
                Shader.SetGlobalVector(ReefClarityShader,clarity);
                // RenderSettings accepts a display-space colour. Our scattering shaders use scene radiance.
                var fogTint=!abyss && QualitySettings.activeColorSpace==ColorSpace.Linear?waterTint.gamma:waterTint;
                RenderSettings.fogColor=Color.Lerp(initialColor,fogTint,wetBlend);
                Shader.SetGlobalVector(WaterTintShader,(Vector4)waterTint);
                if(camera!=null)camera.backgroundColor=RenderSettings.fogColor;
            }
            else ResetFog();
            Shader.SetGlobalFloat(WaterShader,wetBlend);
            if(oceanFill!=null)
            {
                // A flat 1.5-intensity fill erased every form in the shallows. On deck it stays;
                // underwater the sea state decides how much skylight replaces it.
                float target=wetBlend>0?state.oceanFill:Mathf.Lerp(oceanFillBase,state.oceanFill,gradeBlend);
                oceanFill.intensity=target;
            }
            // Keep a continuous breathing rhythm as the animal approaches or retreats.
            var threats=ThreatDirector.Instance;
            float danger=wet && viewed!=null && !viewed.IsDead && threats!=null
                ? threats.NearbyDanger(camera.transform.position) : 0f;
            predatorProximity=Mathf.MoveTowards(predatorProximity,danger,dt*(danger>predatorProximity?.8f:.48f));
            breathPhase=Mathf.Repeat(breathPhase+dt*Mathf.Lerp(2.2f,3.65f,predatorProximity),2*Mathf.PI);
            float fear=predatorProximity*wetBlend*(viewed!=null?1f-viewed.HypoxiaSeverity:0f);
            float breath=Mathf.Sin(breathPhase);
            var deckState=grading.Deck;
            if(profile!=null)
            {
                if(profile.TryGet<ColorAdjustments>(out var color))
                {
                    float exposure=Mathf.Lerp(state.exposureTop,state.exposureBottom,lowerWater);
                    color.postExposure.Override(Mathf.Lerp(deckState.exposureTop,exposure,gradeBlend)+fear*(.43f*breath-.06f));
                    color.saturation.Override(Mathf.Lerp(deckState.saturationTop,Mathf.Lerp(state.saturationTop,state.saturationBottom,lowerWater),gradeBlend));
                    color.contrast.Override(Mathf.Lerp(deckState.contrastTop,Mathf.Lerp(state.contrastTop,state.contrastBottom,lowerWater),gradeBlend));
                }
                if(profile.TryGet<Bloom>(out var bloom))
                {
                    bloom.intensity.Override(Mathf.Lerp(deckState.bloom,state.bloom,gradeBlend));
                    bloom.threshold.Override(Mathf.Lerp(deckState.bloomThreshold,state.bloomThreshold,gradeBlend));
                }
                if(profile.TryGet<Vignette>(out var vignette))
                    vignette.intensity.Override(Mathf.Lerp(deckState.vignette,state.vignette,gradeBlend)+fear*(.065f-.05f*breath));
            }
        }

        void ResetFog()
        {
            RenderSettings.fog=initialFog;RenderSettings.fogMode=initialMode;
            RenderSettings.fogDensity=initialDensity;RenderSettings.fogColor=initialColor;
            if(camera!=null)camera.backgroundColor=initialColor;
            appliedFog=false;Shader.SetGlobalFloat(WaterShader,0);
            Shader.SetGlobalFloat("_RaidConcealment",0);
        }

        void RestoreOceanSettings()
        {
            RenderSettings.ambientMode=deckAmbientMode;
            RenderSettings.ambientSkyColor=deckSky;
            RenderSettings.ambientEquatorColor=deckHorizon;
            RenderSettings.ambientGroundColor=deckGround;
            if(oceanFill!=null)oceanFill.intensity=oceanFillBase;
            if(appliedFog)ResetFog();
            Shader.SetGlobalVector(LampShader,Vector4.zero);
            if(diveLamp!=null)Destroy(diveLamp.gameObject);
        }
    }
}
