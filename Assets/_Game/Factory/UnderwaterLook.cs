using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid
{
    // Local presentation only. All generated audio/VFX belongs to this prototype.
    public sealed class UnderwaterLook : MonoBehaviour
    {
        public static UnderwaterLook Instance {get;private set;}
        Volume volume;
        VolumeProfile original,profile;
        AudioSource ambient,oneShot;
        AudioClip hum,click,route;
        Camera camera;
        Material bubbleMaterial;
        ParticleSystem bubbles;
        float nextCamera;
        void Awake()
        {
            Instance=this;
            volume=Object.FindAnyObjectByType<Volume>();
            if(volume!=null && volume.sharedProfile!=null)
            {
                original=volume.sharedProfile;profile=Instantiate(original);volume.sharedProfile=profile;
                Bloom bloom;if(!profile.TryGet(out bloom))bloom=profile.Add<Bloom>();
                bloom.intensity.Override(.24f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.48f);
                ColorAdjustments color;if(!profile.TryGet(out color))color=profile.Add<ColorAdjustments>();
                color.contrast.Override(8);color.saturation.Override(-5);color.postExposure.Override(.12f);
                Vignette vignette;if(!profile.TryGet(out vignette))vignette=profile.Add<Vignette>();
                vignette.intensity.Override(.16f);vignette.smoothness.Override(.65f);
            }
            ambient=gameObject.AddComponent<AudioSource>();ambient.loop=true;ambient.spatialBlend=0;ambient.volume=.08f;
            oneShot=gameObject.AddComponent<AudioSource>();oneShot.spatialBlend=0;oneShot.volume=.12f;
            hum=MakeHum();click=MakePulse("Console relay",.075f,740);route=MakePulse("Valve relay",.32f,115);
            ambient.clip=hum;ambient.Play();
        }
        void Update()
        {
            if(Time.unscaledTime<nextCamera)return;nextCamera=Time.unscaledTime+.2f;
            var root=UI.UiRoot.Instance;var player=root==null?null:root.Context.Player;
            var owned=player==null?null:player.GetComponentInChildren<Camera>();
            if(owned!=camera)
            {
                camera=owned;
                if(camera!=null)
                {
                    var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
                    CreateBubbles(camera.transform);
                }
            }
            bool wet=player!=null&&!player.IsInside;
            ambient.volume=Mathf.Lerp(ambient.volume,wet?.035f:.075f,.3f);
            if(bubbles!=null){var emission=bubbles.emission;emission.rateOverTime=wet&&root.Page==UI.UiPage.None?4:0;}
        }
        void CreateBubbles(Transform parent)
        {
            if(bubbles!=null)Destroy(bubbles.gameObject);
            var go=new GameObject("Local exhaust microbubbles");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(.35f,-.45f,.8f);
            bubbles=go.AddComponent<ParticleSystem>();bubbles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=bubbles.main;main.startLifetime=1.7f;main.startSpeed=.32f;main.startSize=new ParticleSystem.MinMaxCurve(.008f,.028f);main.maxParticles=24;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=new Color(.4f,.8f,.85f,.25f);
            var shape=bubbles.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.16f;
            var velocity=bubbles.velocityOverLifetime;velocity.enabled=true;velocity.y=.35f;
            var emission=bubbles.emission;emission.rateOverTime=0;
            if(bubbleMaterial==null)
            {
                var source=Resources.Load<Material>("NereidBubbles");
                bubbleMaterial=source!=null?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                bubbleMaterial.SetColor("_BaseColor",new Color(.35f,.85f,.9f,.22f));bubbleMaterial.SetFloat("_Surface",1);bubbleMaterial.SetFloat("_Blend",0);
                bubbleMaterial.SetFloat("_SrcBlend",5);bubbleMaterial.SetFloat("_DstBlend",10);bubbleMaterial.SetFloat("_ZWrite",0);bubbleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");bubbleMaterial.renderQueue=3000;
            }
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=bubbleMaterial;bubbles.Play();
        }
        public void Click() { if(oneShot!=null)oneShot.PlayOneShot(click,.6f); }
        public void Valve() { if(oneShot!=null)oneShot.PlayOneShot(route,.7f); }
        AudioClip MakeHum()
        {
            const int rate=22050;int count=rate*8;var samples=new float[count];var random=new System.Random(11011);float noise=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate;noise=(noise+(float)(random.NextDouble()*2-1)*.018f)*.985f;
                samples[i]=noise*.28f+Mathf.Sin(2*Mathf.PI*48*t)*.13f+Mathf.Sin(2*Mathf.PI*96*t)*.035f;
            }
            var clip=AudioClip.Create("NEREID machinery / generated",count,1,rate,false);clip.SetData(samples,0);return clip;
        }
        AudioClip MakePulse(string name,float duration,float frequency)
        {
            const int rate=22050;var samples=new float[(int)(rate*duration)];
            for(int i=0;i<samples.Length;i++){float t=(float)i/rate;float envelope=Mathf.Sin(Mathf.PI*i/samples.Length)*Mathf.Exp(-t*16);samples[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*envelope*.45f;}
            var clip=AudioClip.Create(name,samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        void OnDestroy()
        {
            if(Instance==this)Instance=null;if(volume!=null&&original!=null)volume.sharedProfile=original;
            if(profile!=null)Destroy(profile);if(bubbleMaterial!=null)Destroy(bubbleMaterial);
            if(hum!=null)Destroy(hum);if(click!=null)Destroy(click);if(route!=null)Destroy(route);
        }
    }
}
