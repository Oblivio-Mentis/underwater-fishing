using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class WatchRoomAuthoring
    {
        static void Observation()
        {
            // The specimen bench sits against the window, between the boarding crossover and
            // the reservoir controls. No legs or cabinet doors extend over the glass tank.
            var c=new Vector3(5.02f,0,-5.04f);
            var bench=Group("Wet specimen bench",c);
            Vector3 P(float x,float y,float z)=>c+new Vector3(x,y,z);
            foreach(float end in new[]{-1.13f,1.19f})
            {
                var p=P(.08f,.46f,end);
                Cut(bench,p,Vector3.right,Vector3.up,new Vector2[]{new(-.48f,-.43f),new(.40f,-.43f),
                    new(.40f,.43f),new(-.48f,.43f),new(-.48f,.27f),new(-.32f,.14f),new(-.32f,-.23f),new(-.48f,-.31f)},.19f,enamel,frame);
            }
            bench.Box(frame,P(.19f,.14f,-.03f),new(.64f,.16f,2.43f));
            bench.Box(ink,P(.07f,.48f,.63f),new(.76f,.62f,.85f));
            bench.Box(shell,P(-.333f,.53f,.63f),new(.056f,.48f,.74f));
            bench.Box(ochre,P(-.37f,.70f,.63f),new(.056f,.077f,.37f));
            bench.Box(frame,P(.03f,.87f,0),new(1.16f,.15f,2.93f),default,.13f);
            bench.Box(shell,P(.03f,.951f,0),new(1.12f,.068f,2.88f),default,.11f);
            bench.Box(ice,P(-.545f,.89f,-.55f),new(.025f,.064f,.77f));
            bench.Collision(P(.03f,.48f,0),new(1.14f,.96f,2.89f));

            var reader=Group("C-frame specimen reader",P(0,1.34f,-.49f));
            Vector2[] bow={new(-.31f,-.40f),new(.40f,-.40f),new(.40f,.40f),new(.24f,.54f),
                new(-.28f,.54f),new(-.39f,.38f),new(-.31f,.26f),new(.12f,.26f),new(.18f,.18f),
                new(.18f,-.17f),new(.10f,-.24f),new(-.31f,-.24f)};
            var at=P(.04f,1.42f,-.53f);
            Cut(reader,at,Vector3.right,Vector3.up,bow,.34f,ochre,frame);
            var head=at+new Vector3(-.225f,.23f,0);
            reader.Tube(enamel,head+Vector3.up*.04f,head-Vector3.up*.098f,.16f,.13f,8);
            reader.Tube(ice,head-Vector3.up*.098f,head-Vector3.up*.113f,.118f,-1,8);
            var tray=P(-.13f,1.032f,-.53f);
            reader.Box(frame,tray,new(.56f,.08f,.74f),default,.12f);
            reader.Box(ink,tray+Vector3.up*.057f,new(.49f,.042f,.61f),default,.11f);
            var fossil=tray+Vector3.up*.145f;
            reader.Tube(mineral,fossil+Vector3.back*.16f,fossil+Vector3.forward*.14f+Vector3.up*.045f,.074f,.124f,6);
            reader.Tube(metal,fossil+Vector3.back*.15f-Vector3.up*.028f,fossil+Vector3.back*.12f+Vector3.up*.034f,.076f,-1,6);
            reader.Lamp("Specimen worklight",new(.64f,.87f,.86f),head-Vector3.up*.17f,3.4f,2.25f);
            var prep=Group("Specimen transport pods",P(0,1.18f,.70f));
            foreach(float z in new[]{.42f,.96f})
            {
                var pad=P(.13f,1.005f,z);
                prep.Tube(frame,pad,pad+Vector3.up*.075f,.18f,.17f,8);
                prep.Tube(ice,pad+Vector3.up*.07f,pad+Vector3.up*.335f,.135f,-1,8);
                prep.Tube(z>.5f?mineral:ink,pad+Vector3.up*.335f,pad+Vector3.up*.41f,.159f,-1,8);
                prep.Box(frame,pad+new Vector3(0,.441f,0),new(.075f,.078f,.135f));
            }

            // Shallow flush seats follow the existing gallery guard. The lower aisle and both
            // stair landings remain clear; crew face directly toward the submerged tank.
            var baseAt=new Vector3(-3.85f,0,-1.74f);
            var row=Group("Port tank observation seats",baseAt);
            row.Box(frame,baseAt+new Vector3(-.025f,.34f,0),new(.35f,.14f,2.67f));
            foreach(float z in new[]{-2.62f,-1.74f,-.86f})
            {
                row.Box(enamel,new(-3.88f,.16f,z),new(.28f,.32f,.17f));
                WatchChair(row,new(-3.84f,0,z),Vector3.right,false);
            }
            row.Collision(new(-3.85f,.43f,-1.74f),new(.42f,.86f,2.64f));

            var library=Group("Raised survey sample library",new(-5.57f,2.18f,-2.24f));
            var shelf=new Vector3(-5.70f,2.15f,-2.24f);
            // Between the window sill and pressure brow, wholly outboard of the elevated gallery rail.
            library.Box(frame,shelf+Vector3.up*.19f,new(.18f,.99f,2.66f));
            foreach(float y in new[]{-.23f,.62f})library.Box(metal,shelf+new Vector3(.07f,y,0),new(.31f,.085f,2.74f));
            foreach(float offset in new[]{-1.27f,1.27f})library.Box(ochre,shelf+new Vector3(.081f,.18f,offset),new(.29f,.81f,.09f));
            for(int i=0;i<4;i++)
            {
                var p=shelf+new Vector3(.12f,-.15f,-.94f+i*.62f);
                Material tag=i==0||i==3?ink:mineral;
                library.Box(enamel,p+Vector3.up*.30f,new(.23f,.64f,.53f),default,.075f);
                library.Box(tag,p+new Vector3(.12f,.30f,0),new(.046f,.47f,.43f),default,.04f);
                library.Box(ice,p+new Vector3(.149f,.39f,0),new(.034f,.071f,.22f));
            }
        }
    }
}