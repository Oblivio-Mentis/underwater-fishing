using UnityEngine;
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
        const float BeamCandela=58f;
        Light diveLamp;
        static readonly int WaterShader=Shader.PropertyToID("_NereidTurbidity");
        static readonly int LampShader=Shader.PropertyToID("_NereidLampForward");
        static readonly int LampOriginShader=Shader.PropertyToID("_NereidLampOriginRange");
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
            node.transform.localPosition=new Vector3(.15f,.12f,.09f);
            diveLamp=node.AddComponent<Light>();
            diveLamp.type=LightType.Spot;diveLamp.spotAngle=68;diveLamp.innerSpotAngle=36;
            diveLamp.range=11;diveLamp.color=new Color(.79f,.92f,.96f);
            diveLamp.renderMode=LightRenderMode.ForcePixel;
            diveLamp.lightUnit=UnityEngine.Rendering.LightUnit.Candela;
            diveLamp.shadows=LightShadows.None;diveLamp.intensity=BeamCandela;diveLamp.enabled=false;
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
                if(diveLamp!=null)
                {
                    var t=diveLamp.transform;var p=t.position;
                    Shader.SetGlobalVector(LampShader,t.forward);
                    Shader.SetGlobalVector(LampOriginShader,new Vector4(p.x,p.y,p.z,diveLamp.range));
                }
            }
            else ResetFog();
            Shader.SetGlobalFloat(WaterShader,wetBlend);
            if(diveLamp!=null){diveLamp.enabled=wetBlend>.04f;diveLamp.intensity=BeamCandela*wetBlend;}
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
            if(diveLamp!=null)Destroy(diveLamp.gameObject);
        }
    }
}
