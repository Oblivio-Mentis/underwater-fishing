using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    internal sealed class FlowFragments : System.IDisposable
    {
        readonly ParticleSystem system;
        readonly Material material;
        readonly Mesh fragment;

        internal FlowFragments(Transform parent,string name,float gravity,int limit,bool water=false)
        {
            var node=new GameObject(name);
            node.transform.SetParent(parent,false);
            system=node.AddComponent<ParticleSystem>();
            system.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main;
            main.playOnAwake=false;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;
            main.maxParticles=limit;
            main.gravityModifier=gravity;
            main.startRotation=new ParticleSystem.MinMaxCurve(0,6.283f);
            var emission=system.emission;emission.enabled=false;
            var shape=system.shape;shape.enabled=false;
            var rotation=system.rotationOverLifetime;
            rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-4.6f,5.2f);
            var scale=system.sizeOverLifetime;
            scale.enabled=true;
            scale.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,1),new Keyframe(.65f,.95f),new Keyframe(1,0)));

            fragment=new Mesh{name=name};
            if(water)
            {
                fragment.vertices=new[]{new Vector3(.34f,-.13f,0),new Vector3(0,-.13f,.34f),
                    new Vector3(-.34f,-.13f,0),new Vector3(0,-.13f,-.34f),
                    new Vector3(.03f,.75f,0),new Vector3(0,-.43f,0)};
                fragment.uv=new[]{Vector2.zero,Vector2.right,Vector2.zero,Vector2.right,Vector2.up,Vector2.one};
                fragment.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0,0,1,5,1,2,5,2,3,5,3,0,5};
            }
            else
            {
                fragment.vertices=new[]{new Vector3(.44f,-.41f,-.22f),new Vector3(-.47f,-.22f,-.32f),
                new Vector3(.07f,-.14f,.46f),new Vector3(.04f,.57f,.07f)};
                fragment.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(.56f,0),new Vector2(1,.72f)};
                fragment.triangles=new[]{0,2,1,0,3,2,0,1,3,1,2,3};
            }
            fragment.RecalculateBounds();
            var renderer=node.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=fragment;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            var shader=Resources.Load<Shader>("RaidFacetedFlow");
            material=new Material(shader?shader:Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if(shader)material.SetFloat("_Bands",0);
            renderer.sharedMaterial=material;
            system.Play(false);
        }

        internal void Emit(Vector3 point,Vector3 velocity,Color color,float size,float lifetime)
        {
            var p=new ParticleSystem.EmitParams
            {
                position=point,velocity=velocity,startColor=color,startSize=size,startLifetime=lifetime
            };
            system.Emit(p,1);
        }
        public void Dispose()
        {
            if(system)Object.Destroy(system.gameObject);
            if(material)Object.Destroy(material);
            if(fragment)Object.Destroy(fragment);
        }
    }
}
