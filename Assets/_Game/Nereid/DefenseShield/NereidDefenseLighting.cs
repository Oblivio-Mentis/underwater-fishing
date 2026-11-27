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
        [Min(1)] public float turnSpeed=96;
        readonly Vector4[] origins=new Vector4[LampCount];
        readonly Vector4[] directions=new Vector4[LampCount];
        public Vector3 Target {get;private set;}
        public bool AlarmActive {get;private set;}

        void Awake()
        {
            var optical=new MaterialPropertyBlock();
            for(int i=0;i<volumes.Length;i++)
            {
                optical.SetFloat("_FloodIndex",i);
                optical.SetFloat("_DefenseBeam",1);
                volumes[i].SetPropertyBlock(optical);
                var lamp=searchlights[i];
                var scale=lamp.transform.lossyScale;
                float radius=lamp.range*Mathf.Tan(lamp.spotAngle*.5f*Mathf.Deg2Rad);
                volumes[i].transform.localScale=new Vector3(radius/scale.x,radius/scale.y,lamp.range/scale.z);
            }
        }

        public void Present(bool active,Vector3 target,double clock)
        {
            AlarmActive=active;Target=target;
            float flash=.32f+.68f*Mathf.Pow(.5f+.5f*Mathf.Cos((float)(clock%1.4)*Mathf.PI*2/1.4f),1.65f);
            foreach(var lamp in alarmLights)
            {
                lamp.enabled=active;
                lamp.intensity=alarmIntensity*flash;
            }
            float side=transform.InverseTransformPoint(target).x;
            var eye=UnderwaterLook.Instance?UnderwaterLook.Instance.DiverCamera:Camera.main;
            bool depth=eye && eye.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.requiresDepthTexture;
            for(int i=0;i<searchlights.Length;i++)
            {
                var lamp=searchlights[i];
                var axis=lamp.transform;
                bool selected=active&&(i<2?side<0:side>=0);
                lamp.enabled=selected;
                lamp.intensity=searchIntensity;
                volumes[i].enabled=selected&&depth;
                Vector3 desired=target-axis.position;
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

        void OnDisable()=>Shader.SetGlobalFloat(Activity,0);
    }
}
