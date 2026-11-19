using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid
{
    // Camera-local visual treatment for the diver and spectator.
    [DefaultExecutionOrder(275)] // Publish the lamp direction after DiverMotor positions the first-person camera.
    public sealed partial class UnderwaterLook : MonoBehaviour
    {
        public static UnderwaterLook Instance {get;private set;}
        Volume volume;
        VolumeProfile original,profile;
        Camera camera;
        Material bubbleMaterial;
        ParticleSystem bubbles;
        float nextCamera;
        void Awake()
        {
            Instance=this;
            SaveOceanSettings();
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
                    BindDiveLamp(camera.transform);
                }
            }
            viewed=player!=null && player.Spectating!=null?player.Spectating:player;
            bool wet=IsEyeUnderwater();
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
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=bubbleMaterial;
            var factory=Object.FindAnyObjectByType<Factory.FactoryVisuals>();
            if(factory!=null&&factory.bubbleMesh!=null){renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=factory.bubbleMesh;}
            bubbles.Play();
        }
        void OnDestroy()
        {
            RestoreOceanSettings();
            if(Instance==this)Instance=null;if(volume!=null&&original!=null)volume.sharedProfile=original;
            if(profile!=null)Destroy(profile);if(bubbleMaterial!=null)Destroy(bubbleMaterial);
        }
    }
}
