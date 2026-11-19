using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Raid
{
    public sealed partial class UnderwaterLook
    {
        DiverMotor viewed;
        bool initialFog,appliedFog;
        FogMode initialMode;
        float initialDensity,wetBlend,threatBlend;
        Color initialColor;
        const float BeamCandela=76f;
        Light diveLamp;
        AudioSource regulator,heartbeat;
        AudioClip regulatorLoop,pulseLoop;
        static readonly int WaterShader=Shader.PropertyToID("_NereidTurbidity");
        static readonly int LampShader=Shader.PropertyToID("_NereidLampForward");
        static readonly int LampOriginShader=Shader.PropertyToID("_NereidLampOriginRange");
        public float WaterBlend=>wetBlend;
        public float ProximityFear=>threatBlend;

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
            if(regulator!=null)return;
            regulatorLoop=MakeDivingAudio(false);pulseLoop=MakeDivingAudio(true);
            regulator=MaskSound(regulatorLoop);heartbeat=MaskSound(pulseLoop);
        }

        AudioSource MaskSound(AudioClip clip)
        {
            var source=gameObject.AddComponent<AudioSource>();
            source.clip=clip;source.loop=true;source.playOnAwake=false;source.volume=0;source.spatialBlend=0;source.Play();
            return source;
        }

        static AudioClip MakeDivingAudio(bool heart)
        {
            const int rate=22050;
            int count=heart?rate:rate*5;
            var samples=new float[count];var random=new System.Random(735);float low=0,deep=0;
            for(int i=0;i<count;i++)
            {
                float t=i/(float)rate;
                if(heart)
                {
                    float u=Mathf.Max(0,t-.19f);
                    samples[i]=Mathf.Sin(2*Mathf.PI*(51*t+1.3f*Mathf.Sin(t*8)))*Mathf.Exp(-22*t)*.62f+
                        Mathf.Sin(2*Mathf.PI*63*u)*Mathf.Exp(-32*u)*.39f;
                }
                else
                {
                    low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.13f);
                    deep=Mathf.Lerp(deep,low,.018f);
                    float breath=Mathf.Sin(2*Mathf.PI*t/5);
                    samples[i]=(low-deep)*(.14f+.8f*Mathf.Pow(Mathf.Abs(breath),2.4f))
                        +Mathf.Sin(2*Mathf.PI*42*t)*.08f*Mathf.Max(0,-breath);
                }
            }
            var clip=AudioClip.Create(heart?"Mask / pulse":"Mask / regulator breathing",count,1,rate,false);
            clip.SetData(samples,0);return clip;
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
            float danger=wet && !viewed.IsDead && ThreatDirector.Instance!=null?
                ThreatDirector.Instance.NearbyDanger(camera.transform.position):0;
            threatBlend=Mathf.MoveTowards(threatBlend,danger,dt*(danger>threatBlend?1.5f:.47f));
            if(wetBlend<=0 && !appliedFog)return;
            if(wetBlend>0)
            {
                appliedFog=true;
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
                bool abyss=RaidWorld.Instance!=null && RaidWorld.Instance.CurrentDepth>=2;
                RenderSettings.fogDensity=Mathf.Lerp(initialDensity,abyss?.108f:.084f,wetBlend);
                RenderSettings.fogColor=Color.Lerp(initialColor,new Color(.029f,.077f,.093f),wetBlend);
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
                color.postExposure.Override(Mathf.Lerp(.12f,-.42f,wetBlend));
                color.saturation.Override(Mathf.Lerp(-5,-25,wetBlend));
                color.contrast.Override(Mathf.Lerp(8,14,wetBlend));
            }
            if(profile!=null && profile.TryGet<Vignette>(out var vignette))
                vignette.intensity.Override(.16f+wetBlend*(.08f+threatBlend*.095f));
            if(regulator!=null)
            {
                regulator.mute=heartbeat.mute=!Application.isFocused;
                regulator.volume=wetBlend*(.13f+.18f*threatBlend);regulator.pitch=1+.4f*threatBlend;
                heartbeat.volume=.41f*threatBlend*wetBlend;heartbeat.pitch=.86f+.65f*threatBlend;
            }
        }

        void ResetFog()
        {
            RenderSettings.fog=initialFog;RenderSettings.fogMode=initialMode;
            RenderSettings.fogDensity=initialDensity;RenderSettings.fogColor=initialColor;
            appliedFog=false;Shader.SetGlobalFloat(WaterShader,0);
        }

        void RestoreOceanSettings()
        {
            if(appliedFog)ResetFog();
            if(diveLamp!=null)Destroy(diveLamp.gameObject);
            if(regulatorLoop!=null)Destroy(regulatorLoop);if(pulseLoop!=null)Destroy(pulseLoop);
        }
    }
}
