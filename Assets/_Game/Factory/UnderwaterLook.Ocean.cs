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
        float shallowFill;
        float predatorProximity, breathPhase;
        const float BeamCandela=245f;
        bool lampOn;
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
        }

        void ApplyShallowFill(float blend)
        {
            if(Mathf.Approximately(shallowFill,blend))return;
            shallowFill=blend;
            RenderSettings.ambientMode=blend>0?UnityEngine.Rendering.AmbientMode.Trilight:deckAmbientMode;
            RenderSettings.ambientSkyColor=Color.Lerp(deckSky,new Color(.49f,.65f,.72f),blend);
            RenderSettings.ambientEquatorColor=Color.Lerp(deckHorizon,new Color(.34f,.44f,.46f),blend);
            RenderSettings.ambientGroundColor=Color.Lerp(deckGround,new Color(.27f,.35f,.33f),blend);
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
        }

        void UpdateDiveLamp(bool submerged)
        {
            if(diveLamp==null)return;
            if(submerged && viewed!=null && viewed.IsOwner && !viewed.IsDead &&
                !UI.UiInputGate.Blocked && Keyboard.current!=null && Keyboard.current.lKey.wasPressedThisFrame)
                lampOn=!lampOn;

            diveLamp.enabled=lampOn && viewed!=null && !viewed.IsDead && wetBlend>.04f;
            diveLamp.intensity=BeamCandela*wetBlend;
            var lamp=diveLamp.transform;
            var pos=lamp.position;
            var dir=lamp.forward;
            Shader.SetGlobalVector(LampShader,new Vector4(dir.x,dir.y,dir.z,diveLamp.enabled?diveLamp.intensity:0));
            Shader.SetGlobalVector(LampOriginShader,new Vector4(pos.x,pos.y,pos.z,diveLamp.range));
        }

        bool IsEyeUnderwater()
        {
            if(camera==null || viewed==null)return false;
            var chamber=Nereid.NereidAirlock.Active;
            var eye=camera.transform.position;
            return chamber!=null && chamber.Contains(eye)?chamber.IsSubmerged(eye):!viewed.IsInside;
        }

        void LateUpdate()
        {
            if(Nereid.NereidVessel.Active==null)return;
            bool wet=IsEyeUnderwater();
            float dt=Time.unscaledDeltaTime;
            wetBlend=Mathf.MoveTowards(wetBlend,wet?1:0,dt*2.4f);
            if(wetBlend<=0 && !appliedFog)return;
            UpdateDiveLamp(wet);
            bool abyss=RaidWorld.Instance!=null && RaidWorld.Instance.CurrentDepth>=2;
            ApplyShallowFill(abyss?0f:wetBlend);
            if(wetBlend>0)
            {
                appliedFog=true;
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
                RenderSettings.fogDensity=Mathf.Lerp(initialDensity,abyss?.108f:.038f,wetBlend);
                var waterTint=abyss?new Color(.032f,.075f,.106f):new Color(.025f,.098f,.148f);
                // Match the reef's custom transmission to the visible range of fish and hull materials.
                Shader.SetGlobalVector(ReefClarityShader,abyss
                    ?new Vector4(.0072f,.82f,1f,0f)
                    :new Vector4(.00136f,1.17f,.68f,0f));
                // RenderSettings accepts a display-space colour. Our scattering shaders use scene radiance.
                var fogTint=!abyss && QualitySettings.activeColorSpace==ColorSpace.Linear?waterTint.gamma:waterTint;
                RenderSettings.fogColor=Color.Lerp(initialColor,fogTint,wetBlend);
                Shader.SetGlobalVector(WaterTintShader,(Vector4)waterTint);
                if(camera!=null)camera.backgroundColor=RenderSettings.fogColor;
            }
            else ResetFog();
            Shader.SetGlobalFloat(WaterShader,wetBlend);
            // Keep a continuous breathing rhythm as the animal approaches or retreats.
            var threats=ThreatDirector.Instance;
            float danger=wet && viewed!=null && !viewed.IsDead && threats!=null
                ? threats.NearbyDanger(camera.transform.position) : 0f;
            predatorProximity=Mathf.MoveTowards(predatorProximity,danger,dt*(danger>predatorProximity?.8f:.48f));
            breathPhase=Mathf.Repeat(breathPhase+dt*Mathf.Lerp(2.2f,3.65f,predatorProximity),2*Mathf.PI);
            float fear=predatorProximity*wetBlend*(viewed!=null?1f-viewed.HypoxiaSeverity:0f);
            float breath=Mathf.Sin(breathPhase);
            if(profile!=null && profile.TryGet<ColorAdjustments>(out var color))
            {
                color.postExposure.Override(Mathf.Lerp(.12f,abyss?-.19f:.28f,wetBlend)+fear*(.43f*breath-.06f));
                color.saturation.Override(Mathf.Lerp(-5,-12,wetBlend));
                color.contrast.Override(Mathf.Lerp(8,3,wetBlend));
            }
            if(profile!=null && profile.TryGet<Vignette>(out var vignette))
                vignette.intensity.Override(Mathf.Lerp(.16f,.14f,wetBlend)+fear*(.065f-.05f*breath));
        }

        void ResetFog()
        {
            RenderSettings.fog=initialFog;RenderSettings.fogMode=initialMode;
            RenderSettings.fogDensity=initialDensity;RenderSettings.fogColor=initialColor;
            if(camera!=null)camera.backgroundColor=initialColor;
            appliedFog=false;Shader.SetGlobalFloat(WaterShader,0);
        }

        void RestoreOceanSettings()
        {
            ApplyShallowFill(0);
            if(appliedFog)ResetFog();
            Shader.SetGlobalVector(LampShader,Vector4.zero);
            if(diveLamp!=null)Destroy(diveLamp.gameObject);
        }
    }
}
