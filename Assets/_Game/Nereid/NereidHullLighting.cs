using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid.Nereid
{
    [DefaultExecutionOrder(520), DisallowMultipleComponent]
    public sealed class NereidHullLighting : MonoBehaviour
    {
        static readonly int Positions=Shader.PropertyToID("_NereidWorkLightPositions");
        static readonly int Directions=Shader.PropertyToID("_NereidWorkLightDirections");
        static readonly int Radiance=Shader.PropertyToID("_NereidWorkLightRadiance");
        readonly Vector4[] positions=new Vector4[3], directions=new Vector4[3], radiance=new Vector4[3];
        readonly Light[] beams=new Light[3];
        readonly List<Object> owned=new();
        readonly bool[] nearby=new bool[3];
        Transform fixtures, cradle;
        NereidVessel vessel;
        Material iron, brass, blueLens, amberLens;
        Vector3 landing;
        bool initialized;

        void Awake()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){enabled=false;return;}
            vessel=GetComponent<NereidVessel>();
            landing=transform.TransformPoint(vessel.cargoRail!=null && vessel.cargoRail.Length>0?vessel.cargoRail[0]:new Vector3(11.5f,-8.2f,-14));
            fixtures=Pivot(transform,"Exterior work lamps",Vector3.zero);
            iron=Finish("Cast pressure housing",new Color(.17f,.23f,.25f),.72f);
            brass=Finish("Retaining collars",new Color(.53f,.34f,.13f),.63f);
            blueLens=Finish("Opal work glass",new Color(.63f,.9f,1f),.15f,2.6f);
            amberLens=Finish("Amber work glass",new Color(1f,.68f,.36f),.12f,2.8f);
            // Ship coordinates are in hull units (2 metres). The cage lamp hangs clear of the dock overhang.
            beams[0]=Fixture("Cargo cradle searchlight",new Vector3(10.41f,-.24f,-16.03f),landing+Vector3.up*.65f,
                new Color(1f,.77f,.49f),amberLens,2100,27,42,60);
            cradle=beams[0].transform.parent;
            beams[1]=Fixture("Starboard approach flood",new Vector3(6.8f,-2.37f,-7.8f),transform.TransformPoint(new Vector3(9.8f,-8.62f,-7.3f)),
                new Color(.68f,.87f,1f),blueLens,1550,23,43,67);
            beams[2]=Fixture("Stern return flood",new Vector3(6.8f,-2.37f,-19.7f),transform.TransformPoint(new Vector3(10.6f,-8.62f,-20.7f)),
                new Color(.77f,.91f,.91f),blueLens,1700,24,42,65);
            var cargo=beams[0];
            cargo.shadows=LightShadows.Hard;cargo.shadowNearPlane=.23f;cargo.shadowNormalBias=.18f;
            cargo.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            for(int side=-1;side<=1;side+=2)
                foreach(float z in new[]{-20f,2.8f})Marker(side,z);
            initialized=true;
        }

        Light Fixture(string label,Vector3 mount,Vector3 target,Color color,Material lens,float intensity,float range,float inner,float outer)
        {
            var seat=Pivot(fixtures,label,mount);
            var yoke=new ReefGeometry();
            Pipe(yoke,new Vector3(0,.065f,0),new Vector3(0,-.065f,0),.17f);
            for(int s=-1;s<=1;s+=2)
                yoke.Tube(new[]{new Vector3(0,-.055f,0),new Vector3(s*.24f,-.11f,0),new Vector3(s*.24f,-.31f,0)},
                    new[]{.047f,.047f,.047f},Color.white,8);
            Shape(seat,"Suspension yoke",yoke,iron);
            var head=Pivot(seat,"Trunnion",new Vector3(0,-.31f,0));
            head.rotation=Quaternion.LookRotation((target-head.position).normalized,transform.up);
            var body=new ReefGeometry();
            body.Tube(new[]{Vector3.back*.28f,Vector3.back*.20f,Vector3.forward*.1f,Vector3.forward*.17f},
                new[]{.105f,.165f,.181f,.221f},Color.white,16);
            for(int i=0;i<7;i++)
            {
                float a=i*2*Mathf.PI/7;
                var radial=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.16f;
                Pipe(body,radial+Vector3.back*.19f,radial+Vector3.forward*.045f,.022f);
            }
            Shape(head,"Finned cast body",body,iron);
            var rim=new ReefGeometry();
            Ring(rim,.213f,.175f,.018f);
            for(int s=-1;s<=1;s+=2)Pipe(rim,new Vector3(-.163f,s*.082f,.185f),new Vector3(.163f,s*.082f,.185f),.011f);
            Shape(head,"Bronze lens guard",rim,brass);
            var glass=new ReefGeometry();
            glass.Tube(new[]{Vector3.forward*.15f,Vector3.forward*.174f},new[]{.204f,.20f},Color.white,16);
            Shape(head,"Prismatic glass",glass,lens);
            var optic=Pivot(head,"Optical axis",new Vector3(0,0,.20f));
            var light=optic.gameObject.AddComponent<Light>();
            light.type=LightType.Spot;light.color=color;light.spotAngle=outer;light.innerSpotAngle=inner;
            light.lightUnit=LightUnit.Candela;light.range=range;light.intensity=intensity;
            light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;light.enabled=false;
            return light;
        }

        void Marker(int side,float z)
        {
            // Hooded marker lenses do not consume realtime lights.
            var seat=Pivot(fixtures,"Hull position marker",new Vector3(side*7.88f,.1f,z));
            var hood=new ReefGeometry();
            Pipe(hood,Vector3.zero,Vector3.right*side*.16f,.11f);
            Shape(seat,"Protective rim",hood,iron);
            var lens=new ReefGeometry();
            Pipe(lens,Vector3.right*side*.162f,Vector3.right*side*.18f,.076f);
            Shape(seat,"Navigation glass",lens,side>0?blueLens:amberLens);
        }

        static void Pipe(ReefGeometry mesh,Vector3 a,Vector3 b,float radius)
            => mesh.Tube(new[]{a,b},new[]{radius,radius},Color.white,10);

        static void Ring(ReefGeometry mesh,float radius,float depth,float thickness)
        {
            var p=new Vector3[17];var widths=new float[p.Length];
            for(int i=0;i<p.Length;i++)
            {
                float a=i*Mathf.PI*2/(p.Length-1);
                p[i]=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,depth);widths[i]=thickness;
            }
            mesh.Tube(p,widths,Color.white,6);
        }

        static Transform Pivot(Transform parent,string label,Vector3 local)
        {
            var node=new GameObject(label).transform;node.SetParent(parent,false);node.localPosition=local;
            return node;
        }

        Material Finish(string label,Color tint,float metal,float glow=0)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=label,hideFlags=HideFlags.DontSave};
            mat.SetColor("_BaseColor",tint);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.55f);
            if(glow>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",tint.linear*glow);}
            owned.Add(mat);return mat;
        }

        void Shape(Transform parent,string label,ReefGeometry geometry,Material material)
        {
            var node=Pivot(parent,label,Vector3.zero);
            var mesh=geometry.Mesh(label);owned.Add(mesh);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            var world=RaidWorld.Instance;
            var target=landing+transform.up*.65f;
            if(world!=null && world.container!=null && (world.IsClientInitialized || world.IsServerInitialized))
            {
                // Follow the outdoor lift. After it crosses above the hull, hold the receiving pad for the next descent.
                var point=world.container.position;
                if(transform.InverseTransformPoint(point).x>=9.3f)target=point+transform.up*.65f;
            }
            var direction=target-cradle.position;
            if(direction.sqrMagnitude>.001f)
                cradle.rotation=Quaternion.RotateTowards(cradle.rotation,Quaternion.LookRotation(direction,transform.up),Time.deltaTime*175);
            var camera=Camera.main;
            for(int i=0;i<beams.Length;i++)
            {
                var light=beams[i];var tr=light.transform;
                bool active=camera!=null && (camera.transform.position-tr.position).sqrMagnitude<Mathf.Pow(nearby[i]?62:56,2);
                if(active!=nearby[i]){light.enabled=active;nearby[i]=active;}
                float outer=Mathf.Cos(light.spotAngle*Mathf.Deg2Rad*.5f);
                float inner=Mathf.Cos(light.innerSpotAngle*Mathf.Deg2Rad*.5f);
                var pos=tr.position;var axis=tr.forward;var tint=light.color.linear;
                float power=light.isActiveAndEnabled?light.intensity:0;
                positions[i]=new Vector4(pos.x,pos.y,pos.z,1/(light.range*light.range));
                directions[i]=new Vector4(axis.x,axis.y,axis.z,outer);
                radiance[i]=new Vector4(tint.r*power,tint.g*power,tint.b*power,1/Mathf.Max(.001f,inner-outer));
            }
            Shader.SetGlobalVectorArray(Positions,positions);
            Shader.SetGlobalVectorArray(Directions,directions);
            Shader.SetGlobalVectorArray(Radiance,radiance);
        }

        void OnEnable(){if(initialized)fixtures.gameObject.SetActive(true);}
        void OnDisable()
        {
            if(!initialized)return;
            fixtures.gameObject.SetActive(false);
            for(int i=0;i<beams.Length;i++){nearby[i]=false;beams[i].enabled=false;radiance[i]=Vector4.zero;}
            Shader.SetGlobalVectorArray(Radiance,radiance);
        }

        void OnDestroy()
        {
            if(fixtures!=null)Destroy(fixtures.gameObject);
            foreach(var asset in owned)if(asset!=null)Destroy(asset);
        }
    }
}
