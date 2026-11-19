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
        const float BeamCandela=245f;
        bool lampOn;
        Light diveLamp;
        static readonly int WaterShader=Shader.PropertyToID("_NereidTurbidity");
        static readonly int LampShader=Shader.PropertyToID("_NereidLampForward");
        static readonly int LampOriginShader=Shader.PropertyToID("_NereidLampOriginRange");
        static readonly int LampConeShader=Shader.PropertyToID("_NereidLampCone");
        static readonly int WaterTintShader=Shader.PropertyToID("_NereidWaterTint");
        public float WaterBlend=>wetBlend;

        void SaveOceanSettings()
        {
            initialFog=RenderSettings.fog;
            initialMode=RenderSettings.fogMode;
            initialDensity=RenderSettings.fogDensity;
            initialColor=RenderSettings.fogColor;
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
            if(wetBlend>0)
            {
                appliedFog=true;
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
                bool abyss=RaidWorld.Instance!=null && RaidWorld.Instance.CurrentDepth>=2;
                RenderSettings.fogDensity=Mathf.Lerp(initialDensity,abyss?.108f:.084f,wetBlend);
                var waterTint=abyss?new Color(.032f,.075f,.106f):new Color(.044f,.103f,.128f);
                RenderSettings.fogColor=Color.Lerp(initialColor,waterTint,wetBlend);
                Shader.SetGlobalColor(WaterTintShader,waterTint);
                if(camera!=null)camera.backgroundColor=RenderSettings.fogColor;
            }
            else ResetFog();
            Shader.SetGlobalFloat(WaterShader,wetBlend);
            if(profile!=null && profile.TryGet<ColorAdjustments>(out var color))
            {
                color.postExposure.Override(Mathf.Lerp(.12f,-.19f,wetBlend));
                color.saturation.Override(Mathf.Lerp(-5,-12,wetBlend));
                color.contrast.Override(Mathf.Lerp(8,3,wetBlend));
            }
            if(profile!=null && profile.TryGet<Vignette>(out var vignette))
                vignette.intensity.Override(Mathf.Lerp(.16f,.14f,wetBlend));
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
            if(appliedFog)ResetFog();
            Shader.SetGlobalVector(LampShader,Vector4.zero);
            if(diveLamp!=null)Destroy(diveLamp.gameObject);
        }
    }
}
