using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class NereidDefenseLighting : MonoBehaviour
    {
        const int LampCount=4;
        static readonly int Origins=Shader.PropertyToID("_RaidLampOrigins");
        static readonly int Directions=Shader.PropertyToID("_RaidLampDirections");
        static readonly int Activity=Shader.PropertyToID("_RaidLampsActive");
        [SerializeField] internal Light[] searchlights;
        [SerializeField] internal MeshRenderer[] volumes;
        [SerializeField] internal Light[] alarmLights;
        [Min(0)] public float alarmIntensity=58;
        [Min(0)] public float searchIntensity=2150;
        [Min(15)] public float searchRange=190;
        [Min(1)] public float turnSpeed=96;
        readonly Vector4[] origins=new Vector4[LampCount];
        readonly Vector4[] directions=new Vector4[LampCount];
        readonly List<(Light source,float intensity)> workLights=new();
        readonly List<(Renderer renderer,int slot,Color color,MaterialPropertyBlock original)> lightPanels=new();
        MaterialPropertyBlock emissionBlock;
        bool workLightsCached;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor");
        public Vector3 Target {get;private set;}
        public bool AlarmActive {get;private set;}
        public float WorkLightGain {get;private set;}=1;

        void Awake()
        {
            emissionBlock=new MaterialPropertyBlock();
            var optical=new MaterialPropertyBlock();
            for(int i=0;i<volumes.Length;i++)
            {
                optical.SetFloat("_FloodIndex",i);
                optical.SetFloat("_DefenseBeam",1);
                volumes[i].SetPropertyBlock(optical);
                var lamp=searchlights[i];
                lamp.range=Mathf.Max(lamp.range,searchRange);
                var scale=lamp.transform.lossyScale;
                float radius=lamp.range*Mathf.Tan(lamp.spotAngle*.5f*Mathf.Deg2Rad);
                volumes[i].transform.localScale=new Vector3(radius/scale.x,radius/scale.y,lamp.range/scale.z);
            }
        }

        public void Present(bool active,Vector3 target,double clock,float darkness=0)
        {
            AlarmActive=active;Target=target;
            float flash=.32f+.68f*Mathf.Pow(.5f+.5f*Mathf.Cos((float)(clock%1.4)*Mathf.PI*2/1.4f),1.65f);
            PresentWorkLights(darkness,clock);
            foreach(var lamp in alarmLights)
            {
                lamp.enabled=active;
                lamp.intensity=alarmIntensity*flash;
            }
            float focusSide=transform.InverseTransformPoint(target).x<0?-1:1;
            Vector3 radial=Vector3.ProjectOnPlane(target-transform.position,Vector3.up).normalized;
            var eye=UnderwaterLook.Instance?UnderwaterLook.Instance.DiverCamera:Camera.main;
            bool depth=eye && eye.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.requiresDepthTexture;
            for(int i=0;i<searchlights.Length;i++)
            {
                var lamp=searchlights[i];
                var axis=lamp.transform;
                bool selected=active;
                lamp.enabled=selected;
                lamp.intensity=searchIntensity;
                volumes[i].enabled=selected&&depth;
                float side=i<2?-1:1;
                Vector3 outward=transform.right*side;
                float sweep=Mathf.Sin((float)clock*.57f+i*1.76f);
                Vector3 scan=Quaternion.AngleAxis((i%2==0?-28:34)+sweep*24,Vector3.up)*outward;
                Vector3 heading=side==focusSide?Vector3.Slerp(scan,radial,i%2==0?.84f:.38f):scan;
                if(Vector3.Dot(heading,outward)<.14f)heading=(heading+outward*.72f).normalized;
                Vector3 desired=heading*(searchRange*.72f)+Vector3.up*(3.4f+(i%2)*7+sweep*4);
                if(side==focusSide && i%2==0 && Vector3.Dot((target-axis.position).normalized,outward)>.22f)
                    desired=target-axis.position;
                if(selected && desired.sqrMagnitude>.01f)
                    axis.rotation=Quaternion.RotateTowards(axis.rotation,Quaternion.LookRotation(desired),turnSpeed*Time.deltaTime);
                var pos=axis.position;var direction=axis.forward;
                origins[i]=new Vector4(pos.x,pos.y,pos.z,selected?lamp.range:0);
                directions[i]=new Vector4(direction.x,direction.y,direction.z,selected?searchIntensity:0);
            }
            Shader.SetGlobalFloat(Activity,active?1:0);
            Shader.SetGlobalVectorArray(Origins,origins);
            Shader.SetGlobalVectorArray(Directions,directions);
        }

        void PresentWorkLights(float darkness,double clock)
        {
            if(darkness>.005f && !workLightsCached)
            {
                var ship=GetComponentInParent<NereidVessel>();
                if(!ship)return;
                workLightsCached=true;
                foreach(var lamp in ship.GetComponentsInChildren<Light>(true))
                    if(lamp && !lamp.transform.IsChildOf(transform) && lamp.type!=LightType.Directional)
                        workLights.Add((lamp,lamp.intensity));
                var materials=new List<Material>();
                foreach(var renderer in ship.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if(renderer.transform.IsChildOf(transform))continue;
                    renderer.GetSharedMaterials(materials);
                    for(int slot=0;slot<materials.Count;slot++)
                    {
                        var material=materials[slot];
                        if(!material || !material.IsKeywordEnabled("_EMISSION") || !material.HasProperty(Emission))continue;
                        var emission=material.GetColor(Emission);
                        if(emission.maxColorComponent<.12f)continue;
                        var saved=new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(saved,slot);
                        lightPanels.Add((renderer,slot,emission,saved));
                    }
                }
            }
            float gain=Mathf.Lerp(1,.065f,darkness);
            gain*=1-darkness*.11f*Mathf.Pow(Mathf.Abs(Mathf.Sin((float)clock*4.37f)),12);
            if(Mathf.Abs(gain-WorkLightGain)<.018f && gain<.999f || gain==1 && WorkLightGain==1)return;
            WorkLightGain=gain;
            foreach(var lamp in workLights)if(lamp.source)lamp.source.intensity=lamp.intensity*gain;
            foreach(var panel in lightPanels)
            {
                if(!panel.renderer)continue;
                if(gain>=.999f){panel.renderer.SetPropertyBlock(panel.original,panel.slot);continue;}
                panel.renderer.GetPropertyBlock(emissionBlock,panel.slot);
                emissionBlock.SetColor(Emission,panel.color*gain);
                panel.renderer.SetPropertyBlock(emissionBlock,panel.slot);
            }
            if(gain==1){workLightsCached=false;workLights.Clear();lightPanels.Clear();}
        }

        void OnDisable()
        {
            PresentWorkLights(0,0);
            Shader.SetGlobalFloat(Activity,0);
        }
    }
}
