using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    internal sealed class CrabHaulVisual : MonoBehaviour
    {
        sealed class Crab
        {
            public Transform Root,Shell;
            public readonly Transform[] Legs=new Transform[8], Claws=new Transform[2];
        }

        static Material carapace,undershell,stone,sand;
        static Mesh sphere;
        Crab[] crew;
        ParticleSystem silt;

        static void Materials()
        {
            if(carapace)return;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            carapace=new Material(shader){name="Reef crabs · rust carapace",color=new Color(.67f,.20f,.089f)};
            undershell=new Material(shader){name="Reef crabs · warm joint",color=new Color(.38f,.16f,.08f)};
            stone=new Material(shader){name="Reef fissure · rock",color=new Color(.19f,.225f,.23f)};
            sand=new Material(shader){name="Reef fissure · silt",color=new Color(.55f,.46f,.31f)};
            var geometry=new ReefGeometry();
            geometry.Tube(new[]{Vector3.down*.49f,Vector3.down*.33f,Vector3.zero,Vector3.up*.33f,Vector3.up*.49f},
                new[]{.095f,.375f,.50f,.375f,.095f},Color.white,10);
            sphere=geometry.Mesh("Reef crab · rounded shell and joints");
        }

        static Transform Pivot(Transform parent,string name,Vector3 local)
        {
            var tr=new GameObject(name).transform;
            tr.SetParent(parent,false);
            tr.localPosition=local;
            return tr;
        }

        static Transform Part(Transform parent,string name,Vector3 local,Vector3 scale,Material material)
        {
            var piece=Pivot(parent,name,local);
            piece.localScale=scale;
            piece.gameObject.AddComponent<MeshFilter>().sharedMesh=sphere;
            piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
            return piece;
        }

        public static GameObject BuildDen(Transform parent,Vector3 position)
        {
            Materials();
            var rocks=Pivot(parent,"Reef · narrow crab crevice",Vector3.zero);
            rocks.position=position;
            Part(rocks,"Dark seam",new(0,.056f,0),new(.45f,.12f,.48f),undershell);
            var roof=Part(rocks,"Overhang",new(.02f,.235f,-.08f),new(.90f,.28f,.64f),stone);
            roof.localRotation=Quaternion.Euler(8,-26,9);
            for(int i=0;i<6;i++)
            {
                float angle=i*Mathf.PI/3;
                var chip=Part(rocks,"Fractured stone",new(Mathf.Cos(angle)*.44f,.045f,Mathf.Sin(angle)*.35f),
                    new(.28f+(i%3)*.05f,.18f,.31f),stone);
                chip.localRotation=Quaternion.Euler(i*11,i*29,i*17);
            }
            return rocks.gameObject;
        }

        public void Build(CrabHaul haul)
        {
            Materials();
            crew=new Crab[haul.Crew];
            for(int i=0;i<crew.Length;i++)
            {
                var root=Pivot(transform,"Crab "+(i+1),Vector3.zero);
                var crab=crew[i]=new Crab{Root=root,Shell=Pivot(root,"Body",Vector3.zero)};
                Part(crab.Shell,"Shell",new(0,.145f,0),new(.38f,.17f,.30f),carapace);
                Part(crab.Shell,"Belly",new(0,.092f,0),new(.30f,.08f,.26f),undershell);
                for(int j=0;j<8;j++)
                {
                    int side=j<4?-1:1;
                    var joint=crab.Legs[j]=Pivot(crab.Shell,"Leg",new(side*.13f,.10f,.115f-(j%4)*.074f));
                    Part(joint,"Upper leg",new(side*.066f,-.012f,0),new(.19f,.039f,.045f),carapace).localRotation=Quaternion.Euler(0,0,side*10);
                    Part(joint,"Foot",new(side*.151f,-.055f,0),new(.085f,.03f,.034f),undershell).localRotation=Quaternion.Euler(0,0,-side*36);
                }
                for(int j=0;j<2;j++)
                {
                    float side=j==0?-1:1;
                    var claw=crab.Claws[j]=Pivot(crab.Shell,"Claw",new(side*.145f,.103f,.141f));
                    Part(claw,"Pincer body",new(side*.043f,0,.062f),new(.092f,.067f,.15f),carapace);
                    Part(claw,"Pincer tip",new(side*.073f,0,.119f),new(.044f,.030f,.10f),undershell).localRotation=Quaternion.Euler(0,side*23,0);
                    Part(crab.Shell,"Eye",new(side*.086f,.205f,.107f),new(.038f,.044f,.058f),undershell);
                }
            }
            var dust=Pivot(transform,"Sediment eruption",Vector3.zero);
            dust.position=haul.Burrow+Vector3.up*.04f;
            silt=dust.gameObject.AddComponent<ParticleSystem>();
            silt.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=silt.main;
            main.loop=false;main.duration=1.2f;main.startLifetime=new ParticleSystem.MinMaxCurve(.55f,1.25f);
            main.startSpeed=new ParticleSystem.MinMaxCurve(.2f,1.1f);
            main.startSize=new ParticleSystem.MinMaxCurve(.025f,.092f);
            main.gravityModifier=-.036f;
            var shape=silt.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.36f;
            var emission=silt.emission;emission.rateOverTime=28;
            emission.SetBursts(new[]{new ParticleSystem.Burst(0,26),new ParticleSystem.Burst(.43f,16)});
            var renderer=silt.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode=ParticleSystemRenderMode.Mesh;renderer.mesh=sphere;
            renderer.sharedMaterial=sand;renderer.shadowCastingMode=ShadowCastingMode.Off;
            silt.Play();
        }

        public static Vector3 Payload(CrabHaul haul,float age)
        {
            float t=Mathf.InverseLerp(RaidWorld.CrabPick,RaidWorld.CrabFinish-.5f,age);
            Vector3 point=Vector3.Lerp(haul.Source,haul.Den+Vector3.up*.13f,t);
            point.y+=.022f*Mathf.Sin(age*21f);
            return point;
        }

        public void Present(CrabHaul haul,float age)
        {
            Vector3 direction=Vector3.ProjectOnPlane(haul.Den-haul.Source,Vector3.up).normalized;
            if(direction.sqrMagnitude<.01f)direction=Vector3.forward;
            for(int i=0;i<crew.Length;i++)
            {
                var crab=crew[i];
                float arc=i*Mathf.PI*2/crew.Length+((haul.FishId%37)*.16f);
                Vector3 ring=new Vector3(Mathf.Cos(arc),0,Mathf.Sin(arc));
                float dig=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.07f+i*.13f,RaidWorld.CrabDig,age));
                float reach=Mathf.SmoothStep(0,1,Mathf.InverseLerp(RaidWorld.CrabDig,RaidWorld.CrabPick,age));
                float hide=Mathf.SmoothStep(0,1,Mathf.InverseLerp(RaidWorld.CrabFinish-.78f,RaidWorld.CrabFinish,age));
                Vector3 point=age<RaidWorld.CrabPick?Vector3.Lerp(haul.Burrow+ring*.54f,haul.Source+ring*.23f,reach):
                    Vector3.Lerp(Payload(haul,age)+ring*.27f,haul.Den,hide);
                if(Physics.Raycast(point+Vector3.up*.64f,Vector3.down,out var hit,1.7f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                    && hit.normal.y>.48f)point.y=hit.point.y;
                point.y-=.25f*(1-dig)+hide*.27f;
                var facing=Vector3.Slerp(-ring,direction,Mathf.InverseLerp(RaidWorld.CrabPick-.28f,RaidWorld.CrabPick+.25f,age));
                crab.Root.SetPositionAndRotation(point,Quaternion.LookRotation(facing,Vector3.up));
                float stride=age*19f+i*.7f;
                crab.Shell.localPosition=Vector3.up*(.010f+.016f*Mathf.Sin(stride*2));
                for(int j=0;j<8;j++)
                {
                    float wave=Mathf.Sin(stride+j*1.23f);
                    crab.Legs[j].localRotation=Quaternion.Euler(0,wave*24f,Mathf.Max(0,wave)*14f*(j<4?-1:1));
                }
                for(int j=0;j<2;j++)
                    crab.Claws[j].localRotation=Quaternion.Euler(Mathf.Sin(stride*.7f+j)*8f,Mathf.Sin(stride+j)*13f,0);
            }
        }
    }
}
