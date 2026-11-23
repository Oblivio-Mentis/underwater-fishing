using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static void MakeCrane(NereidCargoDavit drive)
        {
            var root=drive.transform;var up=Vector3.up;var x=Vector3.right;var z=Vector3.forward;
            var foundation=Part("Deck saddle and kingpost",Vector3.zero);
            // Welded shoes sit below the local camber at their outboard ends.
            foreach(int i in new[]{-1,1})foreach(int j in new[]{-1,1})
            {
                var shoe=new Vector3(i*1.06f,-.045f,j*1.03f);
                foundation.Box(deck,shoe,new(.77f,.28f,.88f),Quaternion.Euler(0,i*j*31,0),.14f);
                foundation.Beam(enamel,shoe+up*.07f,new(i*.55f,.83f,j*.51f),.29f,.30f);
                foundation.Tube(steel,shoe+new Vector3(i*.16f,.11f,j*.14f),shoe+new Vector3(i*.16f,.20f,j*.14f),.075f,-1,6);
            }
            foundation.Tube(hull,-up*.04f,up*.48f,1.04f,.94f,12);
            foundation.Tube(enamel,up*.46f,up*1.08f,.94f,.76f,12);
            foundation.Tube(rubber,up*1.07f,up*1.20f,.85f,-1,14);
            foundation.Tube(steel,up*1.18f,up*1.29f,.87f,.89f,14);
            var pivot=drive.bearing=Joint("Slew axis",root,up*2.25f);
            room=pivot;
            var turret=Part("Rotating drawworks and counterweight",Vector3.zero);
            turret.Tube(ochre,-up*1.02f,-up*.84f,.86f,.89f,14);
            turret.Tube(hull,-up*.83f,-up*.48f,.85f,.68f,12);
            turret.Box(enamel,new(0,-.41f,-.27f),new(1.40f,.52f,1.87f),default,.19f);
            foreach(int sign in new[]{-1,1})
            {
                var side=x*sign;
                turret.Beam(ceramic,side*.54f-up*.53f-z*.51f,side*.56f+up*.09f,.24f,.31f);
                turret.Beam(enamel,side*.56f-up*.39f-z*1.07f,side*.56f+up*.05f-z*.93f,.19f,.23f);
                turret.Tube(steel,side*.48f,side*.68f,.25f,-1,10);
                turret.Tube(ochre,side*.66f,side*.71f,.15f,-1,8);
            }
            turret.Box(deck,new(0,-.33f,-1.33f),new(1.32f,.69f,.48f),default,.17f);
            var drumAxis=drive.drum=Joint("Traction drum shaft",pivot,new(0,.10f,-.94f));
            room=drumAxis;
            var drum=Part("Cable drum and flanges",Vector3.zero);
            drum.Tube(steel,-x*.48f,x*.48f,.34f,-1,14);
            for(int i=0;i<8;i++)
            {
                var line=x*(-.43f+i*.119f);
                drum.Tube(i%2==0?deck:hull,line,line+x*.102f,.386f,-1,12);
            }
            foreach(int sign in new[]{-1,1})
            {
                drum.Tube(ochre,x*sign*.45f,x*sign*.54f,.485f,.455f,14);
                drum.Beam(deck,x*(sign*.56f)-up*.30f,x*(sign*.56f)+up*.30f,.105f,.10f);
            }
            drive.innerBoom=Joint("Luffing trunnion",pivot,Vector3.zero);
            drive.flyJib=Joint("Knuckle trunnion",pivot,z*drive.innerLength);
            MakeArm(drive.innerBoom,drive.innerLength,true);
            MakeArm(drive.flyJib,drive.outerLength,false);

            drive.head=Joint("Travelling sheave head",root,Vector3.zero);room=drive.head;
            var plates=Part("Travelling block cheek plates",Vector3.zero);
            for(int sign=-1;sign<=1;sign+=2)
            {
                var at=x*(sign*.17f);
                plates.Tube(enamel,at,at+x*(sign*.10f),.35f,.29f,10);
                plates.Beam(ceramic,at+up*.18f-z*.21f,at-up*.31f+z*.10f,.13f,.12f);
            }
            drive.sheave=Joint("Crown sheave axle",drive.head,Vector3.zero);room=drive.sheave;
            var wheel=Part("Grooved crown pulley",Vector3.zero);
            wheel.Tube(steel,-x*.16f,x*.16f,.27f,-1,16);
            foreach(int sign in new[]{-1,1})wheel.Tube(ochre,x*sign*.12f,x*sign*.165f,.31f,-1,14);
            wheel.Beam(ceramic,x*.18f-Vector3.up*.20f,x*.18f+Vector3.up*.20f,.075f,.066f);

            drive.cylinders=new[]{
                MakeRam("Luff jack",pivot,new(.64f,-.64f,.51f),drive.innerBoom,new(.64f,-.18f,2.53f),1.30f),
                MakeRam("Knuckle jack",drive.innerBoom,new(-.43f,.24f,drive.innerLength-2.19f),
                    drive.flyJib,new(-.43f,.24f,1.71f),.96f)};
            MakeDavitLamps(drive);
            CraneCollision(drive);
            drive.fairleads=new[]{
                Joint("Drum rope departure",pivot,new(.12f,.47f,-.82f)),
                Joint("Heel roller",drive.innerBoom,new(.18f,.43f,.39f)),
                Joint("Knuckle roller",drive.innerBoom,new(.18f,.24f,drive.innerLength)),
                Joint("Crown entry",drive.head,new(.10f,.25f,-.12f)),
                Joint("Crown vertical tangent",drive.head,new(0,-.18f,.05f))};
        }


        static void MakeDavitLamps(NereidCargoDavit drive)
        {
            room=drive.bearing;
            var flood=Part("Guarded slew work lamps",Vector3.zero);
            foreach(int side in new[]{-1,1})
            {
                var foot=new Vector3(side*.48f,-.53f,-.31f);
                var lamp=new Vector3(side*.77f,-.18f,-.35f);
                flood.Beam(enamel,foot,lamp,.10f,.11f);
                Vector3 outward=new Vector3(side*.58f,.68f,.44f).normalized;
                flood.Tube(hull,lamp,lamp+outward*.23f,.19f,.216f,8);
                flood.Tube(warm,lamp+outward*.225f,lamp+outward*.235f,.156f,-1,8);
            }
            flood.Lamp("Drawworks maintenance wash",new Color(1f,.78f,.52f),
                new Vector3(.84f,.46f,-.65f),64f,10.8f);
            room=drive.head;
            var pendant=Part("Load watch lamp and sheave guard",Vector3.zero);
            var pod=new Vector3(.31f,-.11f,-.04f);
            pendant.Beam(hull,new Vector3(.12f,.12f,0),pod,.11f,.11f);
            pendant.Tube(ochre,pod,pod-Vector3.up*.26f,.15f,.18f,8);
            pendant.Tube(warm,pod-Vector3.up*.257f,pod-Vector3.up*.273f,.142f,-1,8);
            var light=Joint("Suspended load flood",pendant.node,pod-Vector3.up*.30f).gameObject.AddComponent<Light>();
            light.type=LightType.Spot;light.intensity=230;light.range=29f;light.spotAngle=40;light.innerSpotAngle=23;
            light.color=new Color(.78f,.88f,1);light.shadows=LightShadows.None;
            light.transform.localRotation=Quaternion.Euler(90,0,0);
            room=drive.innerBoom;
            var hinge=Part("Knuckle inspection caps",Vector3.forward*drive.innerLength);
            foreach(int sign in new[]{-1,1})
            {
                var at=Vector3.forward*drive.innerLength+Vector3.right*(sign*.49f);
                hinge.Tube(ceramic,at,at+Vector3.right*(sign*.03f),.21f,.18f,10);
                hinge.Tube(ochre,at+Vector3.right*(sign*.03f),at+Vector3.right*(sign*.047f),.126f,.114f,8);
                hinge.Tube(warm,at+Vector3.right*(sign*.047f),at+Vector3.right*(sign*.054f),.067f,-1,8);
            }
        }

        static void CraneCollision(NereidCargoDavit drive)
        {
            var body=drive.gameObject.AddComponent<Rigidbody>();
            body.isKinematic=true;body.useGravity=false;
            var post=drive.gameObject.AddComponent<CapsuleCollider>();
            post.radius=.81f;post.height=2.64f;post.center=Vector3.up*1.21f;
            void Enclose(Transform arm,float length,float radius)
            {
                var beam=arm.gameObject.AddComponent<CapsuleCollider>();beam.direction=2;
                beam.center=Vector3.forward*length*.5f;beam.height=length+radius*.88f;beam.radius=radius;
            }
            Enclose(drive.innerBoom,drive.innerLength,.39f);Enclose(drive.flyJib,drive.outerLength,.26f);
            var crown=drive.head.gameObject.AddComponent<SphereCollider>();crown.radius=.32f;
        }

        static void MakeArm(Transform parent,float length,bool main)
        {
            room=parent;var x=Vector3.right;var y=Vector3.up;var z=Vector3.forward;
            var arm=Part(main?"Open web luffing boom":"Octagonal fly jib",Vector3.zero);
            float w=main?.44f:.29f;
            float depth=main?.78f:.47f;
            // Deep taper at the heel; the fly's open web is a single clear silhouette from deck level.
            var run=new[]{0,.16f,.45f,.73f,1};
            var wide=new[]{.63f,1.0f,.79f,.59f,.47f};
            for(int i=0;i+1<run.Length;i++)
            {
                float a=run[i]*length,b=run[i+1]*length;
                var at=z*a;var end=z*b;
                arm.Tube(i==0?steel:ceramic,at+y*(depth*wide[i]*.43f),end+y*(depth*wide[i+1]*.43f),w*wide[i]*.70f,w*wide[i+1]*.70f,6);
                foreach(int sign in new[]{-1,1})
                {
                    var low=at+x*(sign*w*wide[i])-y*(depth*wide[i]*.49f);
                    var next=end+x*(sign*w*wide[i+1])-y*(depth*wide[i+1]*.49f);
                    arm.Tube(main?enamel:ochre,low,next,.12f*wide[i]+.05f,.12f*wide[i+1]+.05f,6);
                    var crown=Vector3.Lerp(at,end,i%2==0?.21f:.78f)+x*(sign*w*.38f)+y*(depth*.26f);
                    arm.Tube(steel,i%2==0?next:low,crown,main?.086f:.061f,-1,6);
                }
            }
            arm.Tube(enamel,-x*(w*.98f),x*(w*.98f),w*.72f,-1,10);
            arm.Tube(steel,z*length-x*(w*.82f),z*length+x*(w*.82f),w*.63f,-1,12);
            foreach(int sign in new[]{-1,1})
            {
                arm.Tube(ochre,z*length+x*(w*.81f*sign),z*length+x*(w*1.08f*sign),w*.67f,w*.55f,10);
                arm.Tube(hull,x*(w*.98f*sign),x*(w*1.1f*sign),w*.43f,-1,8);
            }
        }

        static NereidCargoDavit.Cylinder MakeRam(string key,Transform mount,Vector3 start,Transform link,Vector3 end,float length)
        {
            var foot=Joint(key+" heel",mount,start);var pin=Joint(key+" clevis",link,end);
            room=mount;
            var barrel=Part(key+" hydraulic sleeve",Vector3.zero);
            barrel.Tube(enamel,Vector3.zero,Vector3.forward*.89f,.143f,-1,9);
            barrel.Tube(steel,Vector3.forward*.86f,Vector3.forward,.165f,.12f,9);
            barrel.node.localScale=new Vector3(1,1,length);
            var rod=Part(key+" polished piston",Vector3.zero);
            rod.Tube(steel,Vector3.back,Vector3.zero,.069f,-1,10);
            return new NereidCargoDavit.Cylinder{foot=foot,pin=pin,barrel=barrel.node,rod=rod.node};
        }
    }
}
