using UnityEngine;
using System.Collections.Generic;
namespace Raid.VisualSlice.Editor
{
    public static class ShipVisual
    {
        static Material M(string key)=>VisualKit.Mat(key);
        static GameObject Box(string n,Transform p,Vector3 pos,Vector3 scale,string mat)=>VisualKit.Part(n,p,PrimitiveType.Cube,pos,scale,M(mat));
        public static void Build(Transform parent)
        {
            var hull=VisualKit.Group("RV NEREID / pressure hull",parent);
            float[] zs={-27,-23,-17,-9,8,11};float[] width={.8f,5.5f,7,7,7,4.6f};float[] height={1,4.7f,5.2f,5.2f,5.2f,3.8f};
            var v=new List<Vector3>();var tri=new List<int>();int sides=24;
            for(int band=0;band<zs.Length-1;band++)for(int s=0;s<sides;s++)
            {
                float a=s*2*Mathf.PI/sides,b=(s+1)*2*Mathf.PI/sides;
                float middle=(a+b)*.5f;bool window=band==3 && (Mathf.Abs(Mathf.Sin(middle))<.5f);
                if(window)continue;
                int i=v.Count;
                v.Add(new Vector3(Mathf.Cos(a)*width[band],1+Mathf.Sin(a)*height[band],zs[band]));
                v.Add(new Vector3(Mathf.Cos(b)*width[band],1+Mathf.Sin(b)*height[band],zs[band]));
                v.Add(new Vector3(Mathf.Cos(a)*width[band+1],1+Mathf.Sin(a)*height[band+1],zs[band+1]));
                v.Add(new Vector3(Mathf.Cos(b)*width[band+1],1+Mathf.Sin(b)*height[band+1],zs[band+1]));
                tri.AddRange(new[]{i,i+1,i+2,i+1,i+3,i+2});
            }
            var mesh=new Mesh{name="Nereid shell"};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();
            VisualKit.MeshObject("Nereid_shell",hull,mesh,M("Hull"));
            for(int z=-22;z<=8;z+=5)
            {
                var points=new Vector3[25];var r=new float[25];for(int i=0;i<25;i++){float a=i*2*Mathf.PI/24;points[i]=new Vector3(Mathf.Cos(a)*7.05f,1+Mathf.Sin(a)*5.25f,z);r[i]=.09f;}
                VisualKit.Tube("Hull rib "+z,hull,points,r,M("Navy"));
            }
            Box("Research conning tower",hull,new Vector3(0,6.5f,-10),new Vector3(4,3.7f,5),"Navy");
            Box("Tower amber band",hull,new Vector3(0,7.1f,-10),new Vector3(4.05f,.35f,5.05f),"Amber");
            for(int i=0;i<3;i++)Box("Tower survey glass",hull,new Vector3((i-1)*1.15f,7.6f,-7.46f),new Vector3(.85f,.7f,.05f),"Glass");
            VisualKit.Part("Sonar mast",hull,PrimitiveType.Cylinder,new Vector3(0,10,-11),new Vector3(.15f,2,.15f),M("Trim"));
            Box("Hydrophone array",hull,new Vector3(0,11.8f,-11),new Vector3(3.5f,.15f,.5f),"Trim");
            for(int side=-1;side<=1;side+=2)
            {
                Box("Ballast pod",hull,new Vector3(side*7,-1,-7),new Vector3(2.6f,2.6f,22),"Navy");
                Box("Ballast safety rail",hull,new Vector3(side*8.33f,-.7f,-7),new Vector3(.12f,.3f,19),"Amber");
                Box("Stabiliser",hull,new Vector3(side*7.5f,1,-21),new Vector3(8,.3f,4),"Hull");
                VisualKit.Label("NEREID  /  07",hull,new Vector3(side*7.13f,2.7f,-14),.16f,new Color(.7f,.84f,.8f),Quaternion.Euler(0,side>0?-90:90,0));
            }
            // Bow shoulder panels leave the existing central hatch open.
            Box("Bow port shoulder",hull,new Vector3(-4.5f,1.5f,9.3f),new Vector3(4.7f,5.2f,2.5f),"Hull");
            Box("Bow starboard shoulder",hull,new Vector3(4.5f,1.5f,9.3f),new Vector3(4.7f,5.2f,2.5f),"Hull");
            Box("Bow crown",hull,new Vector3(0,4.5f,9.3f),new Vector3(5,1.5f,2.5f),"Hull");
            Interior(parent);Basket();
        }
        static void Interior(Transform parent)
        {
            var room=VisualKit.Group("Main research compartment",parent);
            for(int x=-5;x<=5;x+=2)for(int z=-7;z<=7;z+=2)
                Box("Deck plate",room,new Vector3(x,-.03f,z),new Vector3(1.96f,.035f,1.96f),"Hull");
            for(int z=-7;z<=7;z+=3)
            {
                Box("Ceiling panel",room,new Vector3(0,4.1f,z),new Vector3(11.8f,.06f,2.92f),"Navy");
                Box("Structural crossbeam",room,new Vector3(0,4.04f,z),new Vector3(11.9f,.16f,.18f),"Trim");
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Compartment upright",room,new Vector3(side*5.85f,2,z),new Vector3(.16f,4,.18f),"Trim");
                    Box("Warm luminaire",room,new Vector3(side*3.8f,3.94f,z),new Vector3(1.8f,.055f,.16f),"Light");
                }
            }
            for(int side=-1;side<=1;side+=2)
            {
                Box("Window sill",room,new Vector3(side*5.9f,.55f,0),new Vector3(.17f,1.15f,15.8f),"Navy");
                Box("Window brow",room,new Vector3(side*5.9f,3.35f,0),new Vector3(.17f,1.45f,15.8f),"Hull");
                for(int z=-6;z<7;z+=3)
                {
                    for(int y=1;y<=2;y++)Box("Porthole horizontal frame",room,new Vector3(side*5.8f,y==1?1.15f:2.6f,z),new Vector3(.13f,.08f,2.8f),"Amber");
                    Box("Window mullion",room,new Vector3(side*5.8f,1.9f,z+1.4f),new Vector3(.15f,1.6f,.1f),"Trim");
                }
                VisualKit.Tube("Service conduit "+side,room,new[]{new Vector3(side*5.45f,3.7f,-7.5f),new Vector3(side*5.45f,3.7f,7.5f)},new[]{.095f,.095f},M("Amber"));
            }
            for(int side=-1;side<=1;side+=2)
            {
                Box("Hatch frame",room,new Vector3(side*1.95f,1.55f,7.78f),new Vector3(.22f,3.1f,.2f),"Amber");
                Box("Hatch status strip",room,new Vector3(side*1.8f,1.6f,7.65f),new Vector3(.06f,2.6f,.04f),"Glow");
            }
            VisualKit.Label("DIVE LOCK  /  01",room,new Vector3(0,3.27f,7.66f),.065f,new Color(1,.75f,.3f));
            Box("Aft service door",room,new Vector3(0,1.6f,-7.78f),new Vector3(2.7f,3.1f,.1f),"Navy");
            VisualKit.Label("ENGINEERING\nSEALED DECK",room,new Vector3(0,2,-7.66f),.06f,Color.gray,Quaternion.Euler(0,180,0));
            var tank=VisualKit.Group("Central aquarium",room,new Vector3(0,0,-3));
            Box("Tank plinth",tank,new Vector3(0,.1f,0),new Vector3(3.45f,.3f,3.45f),"Navy");
            Box("Water",tank,new Vector3(0,.65f,0),new Vector3(2.9f,.82f,2.9f),"Glass");
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box("Tank corner",tank,new Vector3(x*1.52f,.75f,z*1.52f),new Vector3(.13f,1.5f,.13f),"Trim");
            for(int side=-1;side<=1;side+=2)
            {
                Box("Tank top rail",tank,new Vector3(0,1.45f,side*1.52f),new Vector3(3.2f,.12f,.12f),"Amber");
                Box("Tank top rail",tank,new Vector3(side*1.52f,1.45f,0),new Vector3(.12f,.12f,3.2f),"Amber");
                Box("Tank cyan strip",tank,new Vector3(0,.25f,side*1.65f),new Vector3(2.4f,.045f,.03f),"Glow");
            }
            VisualKit.Label("CATCH RESERVOIR",tank,new Vector3(0,.5f,1.58f),.05f,Color.white,Quaternion.Euler(0,180,0));
            var console=VisualKit.Group("Crew terminal",room,new Vector3(3.15f,0,-3));
            Box("Console pedestal",console,new Vector3(0,.45f,0),new Vector3(.9f,.9f,.7f),"Navy");
            Box("Terminal chassis",console,new Vector3(0,1.2f,0),new Vector3(1.45f,.85f,.4f),"Trim");
            Box("Terminal screen",console,new Vector3(0,1.25f,.215f),new Vector3(1.2f,.6f,.03f),"Navy");
            VisualKit.Label("CREW SYSTEMS\nDEPTH / CATCH / UPGRADES\n[ E ]",console,new Vector3(0,1.25f,.24f),.023f,new Color(.28f,1,.81f),Quaternion.Euler(0,180,0));
            VisualKit.Label("NEREID  //  MARINE RESEARCH",room,new Vector3(-3.7f,2.75f,7.75f),.04f,new Color(.65f,.85f,.83f));
            for(int i=0;i<3;i++)VisualKit.Lamp("Warm overhead",room,new Vector3(0,3.7f,(i-1)*5),new Color(1,.65f,.32f),7,11);
            VisualKit.Lamp("Reservoir bounce",room,new Vector3(0,2.1f,-3),new Color(.22f,.8f,.9f),2,5);
        }
        static void Basket()
        {
            var cargo=GameObject.Find("Cargo container").transform;
            var previous=cargo.Find("RAID010 cargo basket");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
            var root=VisualKit.Group("RAID010 cargo basket",cargo);root.localScale=new Vector3(1/cargo.localScale.x,1/cargo.localScale.y,1/cargo.localScale.z);
            Box("Basket base",root,new Vector3(0,-.55f,0),new Vector3(2,.16f,2),"Navy");
            foreach(int side in new[]{-1,1})for(int i=0;i<7;i++)
            {
                Box("Basket grating",root,new Vector3(-.9f+i*.3f,0,side*.98f),new Vector3(.045f,1.1f,.05f),"Amber");
                Box("Basket grating",root,new Vector3(side*.98f,0,-.9f+i*.3f),new Vector3(.05f,1.1f,.045f),"Amber");
            }
            foreach(int side in new[]{-1,1})
            {Box("Basket rim",root,new Vector3(0,.6f,side),new Vector3(2.08f,.13f,.12f),"Amber");Box("Basket rim",root,new Vector3(side,.6f,0),new Vector3(.12f,.13f,2.08f),"Amber");}
            VisualKit.Label("CARGO / 01",root,new Vector3(0,.2f,-1.03f),.033f,Color.white);
        }
    }
}
