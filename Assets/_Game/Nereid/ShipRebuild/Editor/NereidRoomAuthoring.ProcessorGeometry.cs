using System;
using UnityEngine;
using Raid.Factory;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        // The mill datum is the centre of the existing four grinder lanes. Distances below
        // are in vessel metres before the vessel root scale, as for the modular compartments.
        static void ReceivingHead(ProcessorDriveVisual drive)
        {
            Vector3 x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var inlet=new Vector3(0,2.35f,1.99f);
            var bell=Part("Pressure union and open bell",inlet);
            drive.intakeCollar=bell.node;
            Band(bell,inlet,x,y,.38f,.26f,.18f,hull,deck,0,360,12);
            Band(bell,inlet+z*.15f,x,y,.405f,.272f,.08f,steel,dark,0,360,12);
            var back=Part("Flared inspection chute",new(0,1.40f,1.76f));
            // The spreader has a viewing bay between its folded guide cheeks. From the
            // reservoir side a diver can watch the specimen fall, reach a lane and enter the press.
            foreach(int sign in new[]{-1,1})
            {
                var high=new Vector3(sign*.38f,2.22f,1.79f);var low=new Vector3(sign*1.79f,.54f,1.79f);
                var edge=low+z*.34f;var lip=high+z*.22f;
                var shoulder=Vector3.Lerp(high,low,.34f);var heel=Vector3.Lerp(high,low,.78f);
                back.Beam(enamel,heel,shoulder,.10f,.14f);
                Vector3[] shield={low,heel,heel-x*sign*.19f,low-x*sign*.38f};
                back.Face(ceramic,shield);
                Array.Reverse(shield);back.Face(enamel,shield);
                // The throat and lower wear shoes are solid. The middle remains inspectable.
                back.Beam(ceramic,shoulder,high,.16f,.21f);
                back.Beam(steel,low,edge,.13f,.14f);
                back.Beam(steel,edge,lip,.085f,.09f);
                back.Beam(ochre,edge,Vector3.Lerp(edge,lip,.17f),.091f,.096f);
                var post=Part((sign<0?"Port":"Starboard")+" press mounting cheek",new(sign*1.86f,.85f,.10f));
                var cross=Vector3.back*sign;
                var contour=new[]{new Vector2(-.67f,.13f),new(.08f,.13f),new(.63f,.34f),new(.77f,.71f),
                    new(.66f,1.07f),new(.30f,1.32f),new(-.30f,1.43f),new(-.67f,1.03f)};
                for(int i=0;i<contour.Length;i++)contour[i].x*=-sign;
                if(sign>0)Array.Reverse(contour);
                Casting(post,x*(sign*1.72f),cross,y,contour,.18f,ceramic,steel,hull,.96f);
                var foot=new Vector3(sign*1.77f,.09f,.59f);
                post.Beam(hull,foot,new Vector3(sign*1.77f,.57f,.48f),.22f,.24f);
                post.Box(deck,new Vector3(sign*1.77f,.078f,.11f),new(.41f,.156f,1.43f));
                var bearing=new Vector3(sign*1.90f,.47f,.08f);
                Band(post,bearing,cross,y,.285f,.16f,.082f,steel,deck,0,360,10);
                post.Tube(hull,bearing+x*sign*.018f,bearing+x*sign*.099f,.162f,-1,8);
                // The pickup end is suspended from the same longitudinal frame as the cutter.
                back.Beam(hull,low-z*.05f,new(sign*1.79f,.88f,.50f),.135f,.165f);
                back.Beam(deck,new(sign*1.79f,.58f,1.79f),new(sign*1.79f,.105f,1.82f),.13f,.15f);
                back.Box(steel,new(sign*1.79f,.075f,1.82f),new(.29f,.15f,.31f));
            }
            var bed=Part("Four-lane cast conveyor bed",new(0,.25f,1.25f));
            bed.Box(hull,new(0,.205f,1.26f),new(3.72f,.23f,1.79f),default,.12f);
            foreach(int sign in new[]{-1,1})
            {
                var a=new Vector3(sign*1.79f,.45f,.43f);var b=new Vector3(a.x,.50f,2.08f);
                bed.Beam(steel,a,b,.092f,.095f);
                bed.Beam(ochre,Vector3.Lerp(a,b,.77f),b,.10f,.108f);
            }
            foreach(float lane in new[]{-.90f,0,.90f})
                bed.Beam(deck,new(lane,.44f,.45f),new(lane,.44f,1.96f),.060f,.065f);
            // Leave the upper belt faces and the fall out of the pipe visible from the gallery.
        }

        static void CutterAndBonnet(ProcessorDriveVisual drive)
        {
            var x=Vector3.right;var y=Vector3.up;var z=Vector3.forward;
            var cover=Part("Hinged press bonnet and exit manifold",new(0,1.07f,-.30f));
            var cross=new[]{new Vector2(-.74f,.43f),new(-.64f,.43f),new(-.42f,.94f),new(-.15f,1.22f),
                new(.18f,1.18f),new(.19f,1.31f),new(-.23f,1.43f),new(-.60f,1.13f),new(-.79f,.78f)};
            // Extrude a folded cover with an open front mouth and an open lower discharge.
            var left=new Vector3[cross.Length];var right=new Vector3[cross.Length];
            for(int i=0;i<cross.Length;i++)
            {
                left[i]=new Vector3(-1.77f,cross[i].y,cross[i].x);right[i]=new Vector3(1.77f,cross[i].y,cross[i].x);
            }
            for(int i=0;i<cross.Length;i++)
            {
                int n=(i+1)%cross.Length;
                var paint=i is 4 or 5?steel:i is 2 or 3?ceramic:enamel;
                cover.Face(paint,left[i],left[n],right[n],right[i]);
                cover.Face(paint,right[i],right[n],left[n],left[i]);
            }
            cover.Face(hull,left);Array.Reverse(right);cover.Face(hull,right);
            foreach(float end in new[]{-1.17f,1.17f})
            {
                var hinge=new Vector3(end,1.24f,-.43f);
                cover.Tube(steel,hinge-x*.18f,hinge+x*.18f,.091f,-1,8);
                cover.Beam(hull,hinge+x*.12f,hinge+x*.12f-z*.22f-y*.29f,.11f,.10f);
            }
            var service=Part("Removable drive-side hatch",new(1.96f,.88f,-.26f));
            var at=new Vector3(1.91f,.95f,-.26f);
            Casting(service,at,Vector3.back,y,Shield(.63f,.48f,.43f),.045f,hull,steel,deck);
            service.Beam(deck,at+x*.070f-y*.024f-z*.12f,at+x*.070f-y*.024f+z*.12f,.052f,.051f);
            foreach(float s in new[]{-1f,1f})
                SocketBolt(service,at+x*.047f+z*(.22f*s)+y*.14f,Vector3.back,y,.048f);

            drive.firstShaft=CuttingShaft("Front cutting shaft",new(0,.323f,.215f),0,false);
            drive.secondShaft=CuttingShaft("Return cutting shaft",new(0,.332f,-.083f),.18f,true);
            var signal=Part("Press working light",new(.0f,1.227f,.212f));
            var center=new Vector3(0,1.229f,.213f);
            signal.Face(warm,center-x*.50f-y*.029f,center+x*.50f-y*.029f,center+x*.50f+y*.029f,center-x*.50f+y*.029f);
            var bracket=Part("Press light recess",center);
            Casting(bracket,center-z*.025f,x,y,Shield(1.22f,.16f),.020f,deck,hull,dark);
        }

        static Transform CuttingShaft(string name,Vector3 axis,float angle,bool rear)
        {
            var part=Part(name,axis);var x=Vector3.right;
            part.Tube(steel,axis-x*1.82f,axis+x*1.82f,.078f,-1,10);
            for(int k=0;k<9;k++)
            {
                float xx=-1.56f+k*.39f+(rear?.06f:0);
                var head=axis+x*xx;var a=new Vector3[12];var b=new Vector3[12];
                for(int v=0;v<a.Length;v++)
                {
                    float t=v*Mathf.PI/6f+angle+k*.43f;float r=v%2==0?.177f:.112f;
                    a[v]=head+new Vector3(-.076f,Mathf.Cos(t)*r,Mathf.Sin(t)*r);
                    b[v]=a[v]+x*.152f;
                }
                for(int v=0;v<a.Length;v++)
                {
                    int next=(v+1)%a.Length;
                    part.Face(v%2==0?steel:deck,a[v],a[next],b[next],b[v]);
                    part.Face(hull,head+x*.076f,b[v],b[next]);
                    part.Face(hull,head-x*.076f,a[next],a[v]);
                }
            }
            if(!rear)
            {
                var hub=axis+x*1.92f;
                Band(part,hub,Vector3.back,Vector3.up,.216f,.15f,.10f,ochre,deck,0,360,10);
                part.Beam(hull,hub+Vector3.up*.16f+x*.105f,hub-Vector3.up*.16f+x*.105f,.063f,.06f);
            }
            return part.node;
        }

        static void SeparationDrawers(ProcessorDriveVisual drive)
        {
            Vector3 x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var drawers=Part("Four fraction discharge drawers",new(0,.28f,-.69f));
            drive.outlets=new ProcessorDriveVisual.Outlet[4];
            Material[] material={water,ceramic,steel,cyan};
            string[] names={"Organic fraction","Shell fraction","Alloy fraction","Circuit fraction"};
            for(int i=0;i<4;i++)
            {
                float xx=(i-1.5f)*.90f;
                var mouth=new Vector3(xx,.63f,-.63f);var pocket=new Vector3(xx,.24f,-1.09f);
                var tail=mouth+z*.06f;
                drawers.Box(deck,new(xx,.10f,-.78f),new(.77f,.20f,.82f));
                foreach(float dx in new[]{-.354f,.354f})
                {
                    drawers.Beam(hull,mouth+x*dx,pocket+x*dx,.10f,.13f);
                    drawers.Box(enamel,new(xx+dx,.235f,-.87f),new(.066f,.33f,.66f));
                }
                var width=x*.315f;
                drawers.Face(steel,tail-width,pocket-width,pocket+width,tail+width);
                drawers.Face(deck,tail+width,pocket+width,pocket-width,tail-width);
                drawers.Box(ochre,new(xx,.19f,-1.185f),new(.72f,.10f,.084f));
                drawers.Box(material[i],new(xx,.202f,-1.229f),new(.34f,.065f,.01f));
                var sample=Part(names[i]+" discharge sample",mouth);
                switch(i)
                {
                    case 0:sample.Tube(water,mouth-y*.060f,mouth+y*.066f,.103f,.076f,6);break;
                    case 1:sample.Tube(ceramic,mouth-x*.131f,mouth+x*.103f,.087f,.014f,5);break;
                    case 2:sample.Box(steel,mouth,new(.24f,.105f,.14f),default,.043f);break;
                    default:sample.Box(hull,mouth,new(.21f,.045f,.14f));sample.Box(cyan,mouth+y*.032f,new(.085f,.027f,.069f));break;
                }
                var gate=new GameObject(names[i]+" gate").transform;
                var bin=new GameObject(names[i]+" bin").transform;
                gate.SetParent(room,false);gate.localPosition=mouth;
                bin.SetParent(room,false);bin.localPosition=pocket+y*.056f;
                drive.outlets[i]=new ProcessorDriveVisual.Outlet {gate=gate,bin=bin,sample=sample.node};
                sample.node.gameObject.SetActive(false);
            }
        }
    }
}
