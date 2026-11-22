using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Raid.Nereid
{
    [DefaultExecutionOrder(520), DisallowMultipleComponent]
    public sealed partial class NereidHullLighting : MonoBehaviour
    {
        static readonly int Positions=Shader.PropertyToID("_NereidWorkLightPositions");
        static readonly int Directions=Shader.PropertyToID("_NereidWorkLightDirections");
        static readonly int Radiance=Shader.PropertyToID("_NereidWorkLightRadiance");
        static readonly int Seabed=Shader.PropertyToID("_NereidWorkLightSeabed");
        readonly Vector4[] positions=new Vector4[3], directions=new Vector4[3], radiance=new Vector4[3];
        readonly Light[] beams=new Light[5];
        readonly MeshRenderer[] waterBeams=new MeshRenderer[3];
        readonly List<Object> owned=new();
        readonly bool[] nearby=new bool[5];
        Transform fixtures, cradle;
        NereidVessel vessel;
        Material iron, brass, blueLens, amberLens;
        Vector3 landing;
        bool initialized;
        readonly Dictionary<Renderer,Material[]> originalCoatings=new();

        void Awake()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null){enabled=false;return;}
            vessel=GetComponent<NereidVessel>();
            FitExteriorCoatings();
            landing=transform.TransformPoint(vessel.cargoRail!=null && vessel.cargoRail.Length>0?vessel.cargoRail[0]:new Vector3(11.5f,-8.2f,-14));
            Shader.SetGlobalFloat(Seabed,landing.y-1f);
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
            BuildWaterBeams();
            var cargo=beams[0];
            cargo.shadows=LightShadows.Hard;cargo.shadowNearPlane=.23f;cargo.shadowNormalBias=.18f;
            cargo.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier=UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            BuildHullGuides();
            beams[3]=Fixture("Outer hatch wall wash",new Vector3(11.54f,2.02f,-15.49f),transform.TransformPoint(new Vector3(10.30f,1.0f,-13.7f)),
                new Color(1.0f,.71f,.45f),amberLens,480,11.5f,48,82);
            beams[4]=Fixture("Saddle-tank grazing light",new Vector3(9.53f,-.24f,-15.84f),transform.TransformPoint(new Vector3(7.50f,-.68f,-20.18f)),
                new Color(.54f,.82f,1.0f),blueLens,1950,19,52,80);
            BuildExteriorDetail();
            initialized=true;
        }

        void BuildWaterBeams()
        {
            var shader=Resources.Load<Shader>("NereidWorkBeam");
            if(shader==null || !shader.isSupported)return;
            var material=new Material(shader){name="Work light in suspended water",hideFlags=HideFlags.DontSave};
            owned.Add(material);
            var shape=new ReefGeometry();
            shape.Tube(new[]{Vector3.zero,Vector3.forward},new[]{.001f,1f},Color.white,24);
            var mesh=shape.Mesh("Submerged spot light bounds");
            owned.Add(mesh);
            for(int i=0;i<waterBeams.Length;i++)
            {
                var lamp=beams[i];
                var volume=Pivot(lamp.transform,"Illuminated water",Vector3.zero);
                float length=lamp.range/lamp.transform.lossyScale.z;
                float radius=length*Mathf.Tan(lamp.spotAngle*.5f*Mathf.Deg2Rad);
                volume.localScale=new Vector3(radius,radius,length);
                volume.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=waterBeams[i]=volume.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=material;
                var properties=new MaterialPropertyBlock();
                properties.SetFloat("_FloodIndex",i);
                renderer.SetPropertyBlock(properties);
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                renderer.receiveShadows=false;
                renderer.enabled=false;
            }
        }

        void FitExteriorCoatings()
        {
            var shell=transform.Find("Nereid_Exterior");
            var shader=Resources.Load<Shader>("NereidHullCoating");
            if(shell==null || shader==null)return;
            var finishes=new Dictionary<Material,Material>();
            foreach(var renderer in shell.GetComponentsInChildren<MeshRenderer>())
            {
                var original=renderer.sharedMaterials;
                var updated=(Material[])original.Clone();
                bool changed=false;
                for(int i=0;i<updated.Length;i++)
                {
                    var source=original[i];
                    if(source==null)continue;
                    if(!finishes.TryGetValue(source,out var finish))
                    {
                        Color paint;float metal;
                        switch(source.name)
                        {
                            case "Nereid_Hull":  paint=new Color(.174f,.261f,.286f);metal=.23f;break;
                            case "Nereid_Steel": paint=new Color(.488f,.547f,.548f);metal=.20f;break;
                            case "Nereid_Dark":  paint=new Color(.093f,.128f,.143f);metal=.30f;break;
                            case "Nereid_Grate": paint=new Color(.24f,.30f,.30f);metal=.38f;break;
                            case "Nereid_Trim":  paint=new Color(.321f,.392f,.393f);metal=.54f;break;
                            case "Nereid_Ochre": paint=new Color(.61f,.39f,.15f);metal=.15f;break;
                            default: continue;
                        }
                        finish=new Material(shader){name=source.name+" marine finish",hideFlags=HideFlags.DontSave};
                        finish.SetColor("_BaseColor",paint);
                        finish.SetFloat("_Metallic",metal);
                        finish.SetFloat("_Smoothness",source.GetFloat("_Smoothness"));
                        finishes.Add(source,finish);
                        owned.Add(finish);
                    }
                    updated[i]=finish;
                    changed=true;
                }
                if(!changed)continue;
                originalCoatings.Add(renderer,original);
                renderer.sharedMaterials=updated;
            }
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

        void BuildHullGuides()
        {
            var carrier=new ReefGeometry();
            var hatch=new ReefGeometry();
            var warm=Finish("Ceramic hatch guide",new Color(.95f,.63f,.35f),.16f,1.30f);
            // This bracket cantilevers from the solid header; the lamp grazes the sea-facing armour and leaves.
            carrier.Tube(new[]{new Vector3(10.28f,2.0f,-15.49f),new Vector3(10.48f,2.18f,-15.49f),new Vector3(11.54f,2.18f,-15.49f),new Vector3(11.54f,2.02f,-15.49f)},
                new[]{.093f,.065f,.065f,.078f},Color.white,8);
            Pipe(carrier,new Vector3(10.35f,1.59f,-15.49f),new Vector3(11.15f,2.17f,-15.49f),.037f);

            // The FBX vertically compresses the saddle tanks. Carry the floor projectors at their
            // established optical height on paired stays fixed to the underside of the real tanks.
            foreach(float z in new[]{-7.8f,-19.7f})
            {
                Pipe(carrier,new Vector3(6.8f,-1.14f,z-.31f),new Vector3(6.8f,-2.34f,z),.06f);
                Pipe(carrier,new Vector3(6.8f,-1.14f,z+.31f),new Vector3(6.8f,-2.34f,z),.06f);
            }

            // The outboard lip is visible from below the platform, where the door face itself is foreshortened.
            for(int i=0;i<3;i++)
            {
                float z=-16.13f+i*1.48f;
                Channel(carrier,hatch,new Vector3(10.78f,-.145f,z),new Vector3(10.78f,-.145f,z+1.11f),Vector3.right,.040f);
            }
            for(int side=-1;side<=1;side+=2)
            {
                float z=-14f+side*.829f;
                // Static jambs clear the moving door leaves and the illuminated sea-side push plate.
                Channel(carrier,hatch,new Vector3(10.405f,.13f,z),new Vector3(10.405f,.60f,z),Vector3.right,.032f);
                Channel(carrier,hatch,new Vector3(10.405f,.83f,z),new Vector3(10.405f,1.57f,z),Vector3.right,.032f);
                Channel(carrier,hatch,new Vector3(10.405f,1.65f,z),new Vector3(10.405f,1.65f,z-side*.49f),Vector3.right,.032f);
            }
            Shape(fixtures,"Guide lamp armour and bracket",carrier,iron);
            Shape(fixtures,"Dock lip and pressure door outline",hatch,warm);
        }

        static void Channel(ReefGeometry channel,ReefGeometry lens,Vector3 from,Vector3 to,Vector3 outward,float width)
        {
            var along=(to-from).normalized;
            Pipe(channel,from-along*.047f,to+along*.047f,width*1.5f);
            Pipe(lens,from+outward*width,to+outward*width,width);
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
            if(!initialized || cradle==null)return;
            var world=RaidWorld.Instance;
            var target=landing+transform.up*.65f;
            if(world!=null && world.NetworkObject!=null && world.container!=null && (world.IsClientInitialized || world.IsServerInitialized))
                target=world.container.position+transform.up*.65f;
            var direction=target-cradle.position;
            if(direction.sqrMagnitude>.001f)
                cradle.rotation=Quaternion.RotateTowards(cradle.rotation,Quaternion.LookRotation(direction,transform.up),Time.deltaTime*175);
            var camera=Camera.main;
            var underwater=UnderwaterLook.Instance;
            bool scatter=camera!=null && underwater!=null && underwater.WaterBlend>.01f &&
                camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData) && cameraData.requiresDepthTexture;
            for(int i=0;i<beams.Length;i++)
            {
                var light=beams[i];var tr=light.transform;
                // The two grazing lights only cover the ship skin around the exterior airlock.
                float distance=i<positions.Length?(nearby[i]?62:56):(nearby[i]?40:34);
                bool active=camera!=null && (camera.transform.position-tr.position).sqrMagnitude<distance*distance;
                if(active!=nearby[i]){light.enabled=active;nearby[i]=active;}
                if(i>=positions.Length)continue;
                if(waterBeams[i]!=null)waterBeams[i].enabled=scatter && active && light.isActiveAndEnabled;
                float outer=Mathf.Cos(light.spotAngle*Mathf.Deg2Rad*.5f);
                float inner=Mathf.Cos(light.innerSpotAngle*Mathf.Deg2Rad*.5f);
                var pos=tr.position;var axis=tr.forward;var tint=light.color.linear;
                float power=light.isActiveAndEnabled?light.intensity:0;
                positions[i]=new Vector4(pos.x,pos.y,pos.z,1/(light.range*light.range));
                directions[i]=new Vector4(axis.x,axis.y,axis.z,outer);
                radiance[i]=new Vector4(tint.r*power,tint.g*power,tint.b*power,1/Mathf.Max(.001f,inner-outer));
            }
            PresentExteriorLamps(camera,underwater!=null && underwater.WaterBlend>.25f);
            Shader.SetGlobalVectorArray(Positions,positions);
            Shader.SetGlobalVectorArray(Directions,directions);
            Shader.SetGlobalVectorArray(Radiance,radiance);
        }

        void OnEnable(){if(initialized && fixtures!=null)fixtures.gameObject.SetActive(true);}
        void OnDisable()
        {
            if(!initialized)return;
            if(fixtures!=null)fixtures.gameObject.SetActive(false);
            for(int i=0;i<beams.Length;i++)
            {
                nearby[i]=false;
                if(beams[i]!=null)beams[i].enabled=false;
            }
            PresentExteriorLamps(null,false);
            System.Array.Clear(radiance,0,radiance.Length);
            Shader.SetGlobalVectorArray(Radiance,radiance);
        }

        void OnDestroy()
        {
            foreach(var pair in originalCoatings)
                if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;
            if(fixtures!=null)Destroy(fixtures.gameObject);
            foreach(var asset in owned)if(asset!=null)Destroy(asset);
        }
    }
}
