using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Raid.Nereid
{
    static class NereidSalvage
    {
        static readonly Vector2[] Berths={new(24,-35),new(34,-33),new(32,-46),new(35,-48),new(46,-53),new(52,-38)};
        static readonly Color Steel=new(.17f,.23f,.23f),Oxide=new(.23f,.107f,.059f),Iron=new(.056f,.094f,.111f),Ochre=new(.67f,.39f,.077f);

        public static bool Reserved(float x,float z)
        {
            foreach(var berth in Berths)if((berth-new Vector2(x,z)).sqrMagnitude<2.25f)return true;
            return false;
        }

        public static void Place(MeshCollider bed,Material metal,List<Object> owned)
        {
            var drum=CableDrum().Mesh("Broken winch drum");
            var locker=PressureLocker().Mesh("Recovered instrument locker");
            owned.Add(drum);owned.Add(locker);
            var counters=new Dictionary<int,int>();
            foreach(var loot in Object.FindObjectsByType<LootActor>().Where(l=>l.gameObject.scene==bed.gameObject.scene).OrderBy(l=>l.transform.GetSiblingIndex()))
            {
                counters.TryGetValue(loot.tier,out int slot);counters[loot.tier]=slot+1;
                int index=(loot.tier==1?0:3)+slot%3;
                var location=Berths[index];
                // Cast directly on the finished terrain; ship fixtures, fish and other players cannot intercept it.
                if(!bed.Raycast(new Ray(new Vector3(location.x,15,location.y),Vector3.down),out var hit,60))continue;
                var mesh=loot.tier==1?drum:locker;
                loot.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=loot.GetComponent<MeshRenderer>();renderer.sharedMaterial=metal;
                var box=loot.GetComponent<BoxCollider>();box.center=mesh.bounds.center;box.size=mesh.bounds.size;
                loot.transform.rotation=Quaternion.Euler(0,index*79+21,0);
                float clearance=-mesh.bounds.min.y*Mathf.Abs(loot.transform.lossyScale.y);
                loot.SetReefBerth(hit.point+Vector3.up*clearance);
            }
        }

        static ReefGeometry CableDrum()
        {
            var g=new ReefGeometry();
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*.64f;
                g.Tube(new[]{new Vector3(x-.05f,0,0),new(x-.028f,0,0),new(x+.035f,0,0),new(x+.066f,0,0)},
                    new[]{.55f,.65f,.65f,.54f},Steel,14);
                g.Tube(new[]{new Vector3(x,0,0),new(x+side*.18f,0,0)},new[]{.22f,.16f},Oxide,8);
                for(int i=0;i<6;i++)
                {
                    float a=i*Mathf.PI/3;
                    var p=new Vector3(x+side*.071f,Mathf.Cos(a)*.46f,Mathf.Sin(a)*.46f);
                    g.Tube(new[]{p,p+Vector3.right*side*.041f},new[]{.046f,.042f},Iron,6);
                }
                Box(g,new Vector3(x,-.685f,0),new(.24f,.15f,1.13f),Iron);
                g.Tube(new[]{new Vector3(x,-.64f,-.42f),new(x,-.12f,0),new(x,-.64f,.42f)},new[]{.1f,.12f,.1f},Ochre,5);
            }
            g.Tube(new[]{Vector3.left*.62f,Vector3.right*.62f},new[]{.37f,.37f},Iron,13);
            var helix=new Vector3[127];var gauge=new float[helix.Length];
            for(int i=0;i<helix.Length;i++)
            {
                float u=i/(float)(helix.Length-1),angle=u*Mathf.PI*18;
                helix[i]=new Vector3(Mathf.Lerp(-.54f,.54f,u),Mathf.Cos(angle)*.423f,Mathf.Sin(angle)*.423f);
                gauge[i]=.06f;
            }
            g.Tube(helix,gauge,Oxide,6);
            g.Tube(new[]{helix[helix.Length-1],new Vector3(.54f,.31f,.35f),new(.4f,-.16f,.57f),new(.14f,-.57f,.7f)},new[]{.058f,.054f,.05f,.043f},Ochre,6);
            return g;
        }

        static ReefGeometry PressureLocker()
        {
            var g=new ReefGeometry();
            Box(g,new Vector3(0,-.035f,0),new(1.55f,.90f,.87f),new Color(.087f,.18f,.186f));
            Box(g,new Vector3(0,.44f,0),new(1.63f,.11f,.91f),Steel);
            foreach(float side in new[]{-1f,1f})
            {
                float x=side*.54f;
                Box(g,new Vector3(x,-.43f,0),new(.23f,.21f,1.02f),Iron);
                Box(g,new Vector3(x,.17f,-.451f),new(.14f,.37f,.075f),Ochre);
                Box(g,new Vector3(x,.22f,-.507f),new(.085f,.095f,.058f),Steel);
                g.Tube(new[]{new Vector3(x,.48f,-.22f),new(x,.59f,-.19f),new(x,.59f,.18f),new(x,.48f,.22f)},
                    new[]{.046f,.046f,.046f,.046f},Iron,7);
            }
            for(int i=0;i<5;i++)Box(g,new Vector3(-.195f+i*.095f,-.12f,-.45f),new(.049f,.23f,.035f),Iron);
            Box(g,new Vector3(0,.19f,-.456f),new(.38f,.11f,.036f),Ochre);
            return g;
        }

        static void Box(ReefGeometry g,Vector3 center,Vector3 size,Color tone)
        {
            var extents=size*.5f;
            var v=new Vector3[8];
            for(int i=0;i<8;i++)v[i]=center+Vector3.Scale(extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            int[] corners={0,2,3,1,4,5,7,6,0,4,6,2,1,3,7,5,2,6,7,3,0,1,5,4};
            for(int i=0;i<corners.Length;i+=4)
            {
                int a=g.Add(v[corners[i]],tone);g.Add(v[corners[i+1]],tone);g.Add(v[corners[i+2]],tone);g.Add(v[corners[i+3]],tone);g.Quad(a,a+1,a+2,a+3);
            }
        }
    }
}
