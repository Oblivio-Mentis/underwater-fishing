using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        const string Fittings="Assets/_Game/Nereid/Resources/NereidServiceFittings/Models/";
        const string Expedition="Assets/_Game/Nereid/Resources/ExpeditionAssets/NereidExpeditionProps/";

        static void Model(string path,string name,Vector3 at,Vector3 size,Material finish,float yaw=0)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)throw new InvalidOperationException("Missing NEREID modular asset "+path);
            var seat=new GameObject(name).transform;seat.SetParent(room,false);seat.localPosition=at-origin;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,seat);
            var renders=model.GetComponentsInChildren<MeshRenderer>();
            if(renders.Length==0)throw new InvalidOperationException(path+" has no renderable meshes.");
            Bounds bounds=renders[0].bounds;foreach(var r in renders)bounds.Encapsulate(r.bounds);
            var ext=bounds.size;
            Vector3 scale=new(size.x/Mathf.Max(.001f,ext.x),size.y/Mathf.Max(.001f,ext.y),size.z/Mathf.Max(.001f,ext.z));
            var center=seat.InverseTransformPoint(bounds.center);
            model.transform.localScale=Vector3.Scale(model.transform.localScale,scale);
            model.transform.localPosition-=Vector3.Scale(center,scale);
            foreach(var r in renders)
            {
                r.sharedMaterials=Enumerable.Repeat(finish,r.sharedMaterials.Length).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
            seat.localRotation=Quaternion.Euler(0,yaw,0);
        }

        static void LightStrip(ShipSectionMesh s,Vector3 at,float width,Material lens,Color light,float power)
        {
            s.Box(dark,at,new(width+.24f,.22f,.43f));
            s.Box(lens,at-Vector3.up*.12f,new(width,.058f,.27f));
            foreach(int side in new[]{-1,1})s.Box(ochre,at+new Vector3(side*(width*.50f+.065f),-.053f,0),new(.13f,.17f,.44f));
            s.Lamp("Task light",light,at-Vector3.up*.28f,power,8.6f);
        }

        static void CargoDeck()
        {
            Plate("Cargo and process deck",-5.96f,5.96f,-16.12f,-8.10f,0);
            DoorFrame("Process hall pressure arch",-8.08f,5.93f,1.88f,2.72f,3.64f);
            WallBay("Transfer equipment wall",-5.94f,-14.11f,4.01f,0,3.64f,false);
            WallBay("Crusher inspection window",-5.94f,-10.10f,4.02f,0,3.64f,true);
            WallBay("Transit side panels",5.94f,-10.13f,4.10f,0,3.64f,false);
            WallBay("Aft jamb",5.94f,-15.97f,.35f,0,3.64f,false);
            var canopy=Part("Overhead chain and pump services",new(0,3.60f,-12));
            canopy.Slab(dark,new(0,3.76f,-12.08f),new(11.91f,.22f,8.04f));
            canopy.Slab(enamel,new(5.95f,2.96f,-13.98f),new(.25f,1.36f,3.86f));
            for(int i=0;i<3;i++)
            {
                float z=-14.85f+i*2.76f;
                canopy.Beam(steel,new(-5.67f,3.56f,z),new(5.67f,3.56f,z),.12f,.18f);
                LightStrip(canopy,new(i%2==0?1.79f:-1.49f,3.38f,z),1.06f,warm,incandescent,39);
            }
            // Source props remain nested model prefabs so their mesh importers and material slots can be changed.
            Model(Expedition+"Prop_Crate4.fbx","Latched transit case",new(-5.24f,.41f,-12.08f),new(.93f,.77f,.98f),ochre,-4);
            Model(Fittings+"Prop_Vent_Wide.fbx","Pump intake louvre",new(3.42f,2.8f,-15.88f),new(1.9f,.26f,.48f),steel);
            var hoses=Part("High pressure cylinders",new(5.23f,.80f,-9.35f));
            for(int i=0;i<3;i++)
            {
                var p=new Vector3(5.56f,0,-9.05f-i*.44f);
                hoses.Tube(ochre,p+Vector3.up*.16f,p+Vector3.up*1.01f,.16f);
                hoses.Tube(steel,p+Vector3.up*1.01f,p+Vector3.up*1.15f,.16f,.061f);
                hoses.Tube(dark,p+Vector3.up*.51f,p+Vector3.up*.59f,.17f);
                hoses.Tube(cyan,p+Vector3.up*1.15f,p+Vector3.up*1.20f,.055f);
            }
        }

        static void EquipmentRoom()
        {
            Plate("Service deck",-4.27f,4.27f,-24.85f,-16.10f,0);
            DoorFrame("Service deck pressure arch",-16.11f,5.96f,1.54f,2.72f,3.62f);
            BridgeTrunkAuthoring.Attach(room);
            var aft=Part("Engine division",new(0,1.58f,-24.88f));
            aft.Slab(enamel,new(0,1.74f,-24.98f),new(8.53f,3.49f,.20f));
            aft.Box(steel,new(.75f,1.62f,-24.82f),new(4.57f,.19f,.21f));
            foreach(int side in new[]{-1,1}) for(int k=0;k<2;k++)
                WallBay((side<0?"Port":"Starboard")+" machinery lining "+k,side*4.26f,-22.66f+k*4.37f,4.38f,0,3.58f,false);
            Stairs("Engineering to bridge",-2.58f,1.88f,-17.65f,-24.85f,4.85f,false);
            var bench=Part("Harpoon fitter display",new(4.00f,1.03f,-18.43f));
            bench.Box(dark,new(4.03f,1.02f,-18.62f),new(.28f,1.76f,3.43f));
            bench.Box(steel,new(3.88f,.48f,-18.62f),new(.36f,.14f,3.36f));
            for(int i=0;i<3;i++)
            {
                float z=-19.57f+i*.92f;
                var lower=new Vector3(3.78f,.60f,z-.18f);var upper=new Vector3(3.78f,1.73f,z+.19f);
                Vector3 axis=(upper-lower).normalized;
                bench.Tube(steel,lower,upper,.039f, .030f);
                bench.Tube(ochre,lower,Vector3.Lerp(lower,upper,.45f),.073f,.061f);
                bench.Tube(dark,lower+Vector3.right*.042f,lower-Vector3.right*.13f,.115f,-1,14);
                bench.Tube(steel,upper,upper+axis*.14f,.071f,.009f);
                bench.Beam(rubber,lower,lower+new Vector3(0,-.09f,-.17f),.074f,.11f);
            }
            LightStrip(bench,new(3.20f,2.82f,-18.60f),.72f,warm,incandescent,36);
            Model(Expedition+"Prop_AccessPoint.fbx","Workshop battery cabinet",new(.87f,.73f,-24.39f),new(1.03f,1.43f,.53f),enamel);
            var rack=Part("Diving equipment drying rack",new(-.60f,1.04f,-24.47f));
            rack.Box(ochre,new(-.65f,1.89f,-24.53f),new(2.26f,.13f,.21f));
            for(int i=0;i<2;i++)
            {
                float x=-1.18f+i*.93f;
                rack.Box(rubber,new(x,.98f,-24.46f),new(.44f,.60f,.27f),default,.14f);
                rack.Tube(ochre,new(x,1.43f,-24.46f),new(x,1.66f,-24.46f),.18f,.145f,16);
                rack.Box(glass,new(x,1.56f,-24.29f),new(.25f,.12f,.055f));
                foreach(int side in new[]{-1,1})
                {
                    rack.Beam(dark,new(x+side*.17f,1.22f,-24.45f),new(x+side*.29f,.78f,-24.45f),.145f,.15f);
                    rack.Beam(dark,new(x+side*.11f,.75f,-24.46f),new(x+side*.12f,.30f,-24.46f),.18f,.19f);
                    rack.Box(ochre,new(x+side*.12f,.29f,-24.37f),new(.17f,.087f,.29f));
                }
            }
            LightStrip(rack,new(-.65f,2.25f,-24.20f),1.74f,warm,incandescent,28);
        }

        static void CommandBridge()
        {
            const float floor=4.85f;
            Plate("Command deck",-4.34f,4.34f,-30.50f,-24.85f,floor);
            foreach(int side in new[]{-1,1})for(int k=0;k<2;k++)
                WallBay((side<0?"Port":"Starboard")+" command glazing "+k,side*4.34f,-29.10f+k*2.82f,2.83f,floor,2.60f,true);
            var panorama=Part("Panoramic bridge windscreen",new(0,6.19f,-30.4f));
            panorama.Slab(dark,new(0,5.10f,-30.48f),new(8.70f,.50f,.23f));
            panorama.Slab(glass,new(0,6.10f,-30.43f),new(8.54f,1.53f,.092f));
            panorama.Slab(enamel,new(0,7.20f,-30.4f),new(8.70f,.69f,.25f));
            for(int i=0;i<5;i++)panorama.Beam(steel,new(-4.27f+i*2.14f,5.35f,-30.42f),new(-4.27f+i*2.14f,6.88f,-30.42f),.083f,.14f);
            var crown=Part("Chart room crown",new(0,7.38f,-27.64f));
            crown.Slab(hull,new(0,7.52f,-27.65f),new(8.73f,.22f,5.66f));
            foreach(int d in new[]{-1,1})LightStrip(crown,new(d*2.64f,7.29f,-27.82f),1.35f,warm,incandescent,28);
            // Full headroom over the landing, split wall flanks leave the proper stair aperture.
            crown.Slab(enamel,new(1.425f,6.15f,-24.73f),new(5.82f,2.60f,.19f));
            crown.Slab(enamel,new(-3.98f,6.15f,-24.73f),new(.70f,2.60f,.19f));
            crown.Box(ochre,new(-2.58f,7.23f,-24.76f),new(2.17f,.19f,.29f));
            foreach(int side in new[]{-1,1})
            {
                float x=side*3.28f;
                var p=Part((side<0?"Port":"Starboard")+" chart table",new(x,5.42f,-29.22f));
                p.Box(dark,new(x,5.16f,-29.37f),new(1.30f,.59f,.67f));
                p.Box(steel,new(x,5.50f,-29.37f),new(1.48f,.13f,.81f));
                for(int i=0;i<4;i++)p.Box(i%2==0?cyan:ochre,new(x-.42f+i*.28f,5.58f,-29.21f),new(.19f,.054f,.13f));
            }
        }

        static void FeedingRoom()
        {
            Plate("Containment work floor",-5.96f,5.96f,4.50f,8.17f,0);
            foreach(int d in new[]{-1,1})WallBay((d<0?"Port":"Starboard")+" ward light",d*5.94f,6.33f,3.67f,0,4.59f,true);
            var s=Part("Bow graft restraint",new(0,2,7.94f));
            foreach(int d in new[]{-1,1})
            {
                s.Slab(enamel,new(d*4.26f,2.20f,8.14f),new(3.42f,4.40f,.25f));
                s.Box(dark,new(d*2.63f,1.49f,8.02f),new(.64f,2.96f,.64f));
                for(int i=0;i<4;i++)s.Box(ochre,new(d*2.63f,.52f+i*.57f,7.70f),new(.62f,.097f,.18f));
                LightStrip(s,new(d*2.84f,3.49f,6.96f),.75f,alarm,new Color(1,.32f,.17f),26);
                s.Tube(rubber,new(d*3.13f,.31f,7.98f),new(d*3.13f,3.64f,7.98f),.13f,-1,16);
            }
            s.Slab(dark,new(0,4.09f,8.14f),new(5.30f,1.19f,.28f));
            s.Slab(dark,new(0,4.68f,6.36f),new(11.90f,.20f,3.73f));
            s.Box(alarm,new(0,3.60f,7.95f),new(1.13f,.11f,.14f));
            CrossFrame("Containment arch",4.56f,5.83f,4.51f,alarm);
            BuildContainmentPressureJacket(room);
        }

        static void CompanionBerth()
        {
            var s=Part("Gosha sleeping berth",new(5.09f,.18f,2.80f));
            s.Box(dark,new(5.04f,.103f,3.0f),new(1.19f,.20f,1.42f),default,.22f);
            s.Box(ochre,new(5.04f,.20f,3.0f),new(1.11f,.12f,1.32f),default,.25f);
            s.Box(rubber,new(5.04f,.25f,3.0f),new(.98f,.14f,1.17f),default,.24f);
            s.Collision(new(5.04f,.13f,3),new(1.17f,.26f,1.36f));
            s.Tube(steel,new(4.79f,.02f,3.99f),new(4.79f,.132f,3.99f),.17f,.20f);
            s.Tube(dark,new(4.79f,.133f,3.99f),new(4.79f,.135f,3.99f),.168f);
            s.Box(dark,new(5.61f,1.21f,3.03f),new(.20f,.22f,.65f));
            s.Box(warm,new(5.50f,1.19f,3.03f),new(.031f,.11f,.53f));
        }
    }
}
