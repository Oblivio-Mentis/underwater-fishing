using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    // Restores the native equipment exhibit when authoring an equipment room from scratch.
    // An existing exhibit belongs to the level artist and is never regenerated in place.
    public static class NereidEquipmentDisplayAuthoring
    {
        const string Root = "Assets/_Game/Nereid/ShipRebuild";
        const string Room = Root + "/Prefabs/Rooms/EquipmentRoom.prefab";
        const string Diver = "Assets/_Game/Factory/Diver/CrewDiver.prefab";
        const string Harpoon = "Assets/_Game/Factory/Resources/ExpeditionHarpoon.prefab";
        const string Exhibit = "Expedition equipment displays";

        [MenuItem("RAID/NEREID/Install expedition equipment displays")]
        public static void Install()
        {
            var room = PrefabUtility.LoadPrefabContents(Room);
            try
            {
                if (room.transform.Find(Exhibit)) return;
                var rack = room.transform.Find("Diving equipment drying rack");
                var fitter = room.transform.Find("Harpoon fitter display");
                var battery = room.transform.Find("Workshop battery cabinet");
                if (!rack || !fitter || !battery)
                    throw new System.InvalidOperationException("The equipment room is missing its original prop mounts.");
                Object.DestroyImmediate(rack.gameObject);
                Object.DestroyImmediate(fitter.gameObject);
                battery.localPosition = new Vector3(2.31f,.73f,-4.58f);
                var exhibit = Group(room.transform,Exhibit,Vector3.zero);
                var palette = new Finishes();

                Suit(exhibit,palette,-.38f,0);
                Suit(exhibit,palette,.20f,1);
                GunCabinet(exhibit,palette);
                ServiceBench(exhibit,palette);

                if (exhibit.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new System.InvalidOperationException("Equipment on display must leave the walking volume free.");
                PrefabUtility.SaveAsPrefabAsset(room,Room);
                AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        sealed class Finishes
        {
            public readonly Material frame = Material("Graphite forged frames");
            public readonly Material steel = Material("Machined titanium");
            public readonly Material paint = Material("Petrol marine enamel");
            public readonly Material rubber = Material("Reinforced hose rubber");
            public readonly Material ochre = Material("Rescue ochre");
            public readonly Material warm = Lens("Amber work light","Locker glass - warm",new Color(1f,.69f,.37f));
            public readonly Material blue = Lens("Reservoir teal light","Locker glass - cool",new Color(.32f,.80f,.97f));
            public readonly Material grate = Material("Embossed graphite tread");
            static Material Lens(string source,string name,Color color)
            {
                var existing=Material(name);
                if(existing)return existing;
                var lens=new Material(Material(source)){name=name};
                lens.EnableKeyword("_EMISSION");
                lens.SetColor("_EmissionColor",color*2.4f);
                lens.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;
                AssetDatabase.CreateAsset(lens,Root+"/Materials/"+name+".mat");
                return lens;
            }
            static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + name + ".mat");
        }

        static Transform Group(Transform parent,string name,Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent,false);
            t.localPosition=position;
            return t;
        }

        static void Mount(Transform root,Vector3 at,Quaternion rotation)
        {
            root.SetLocalPositionAndRotation(at,rotation);
            PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        }

        static void Tilt(Transform bone,Transform frame,Vector3 euler)
        {
            bone.rotation=frame.rotation*Quaternion.Euler(euler)*Quaternion.Inverse(frame.rotation)*bone.rotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
        }

        static void Suit(Transform parent,Finishes m,float x,int index)
        {
            var bay=Group(parent,index==0?"Port suit locker":"Starboard suit locker",new(x,0,-4.60f));
            bay.localScale=Vector3.one*.5f;
            var s=new ShipSectionMesh(bay,Vector3.zero,"Drying cabinet",Vector3.zero);
            s.Box(m.frame,new(0,1.11f,-.22f),new(1.10f,2.20f,.16f));
            s.Box(m.paint,new(0,1.17f,-.12f),new(.94f,1.96f,.055f));
            s.Box(m.frame,new(0,.10f,.03f),new(1.11f,.20f,.76f));
            s.Box(m.grate,new(0,.215f,.055f),new(.95f,.045f,.60f));
            // Drain, return air and grounded toe stops frame the boot well.
            for(int i=-3;i<=3;i++)
                s.Box(m.rubber,new(i*.092f,.242f,.252f),new(.052f,.009f,.13f));
            for(int side=-1;side<=1;side+=2)
            {
                float post=side*.525f;
                s.Box(m.paint,new(post,1.17f,0),new(.068f,1.97f,.56f));
                s.Box(m.frame,new(post,1.18f,.298f),new(.094f,1.99f,.075f));
                s.Box(m.steel,new(post,1.24f,.342f),new(.066f,1.70f,.022f));
                s.Box(m.blue,new(post,1.32f,.357f),new(.018f,1.35f,.012f));
                foreach(float y in new[]{.35f,1.93f})
                {
                    s.Box(m.frame,new(post,y,.32f),new(.116f,.14f,.095f));
                    s.Tube(m.steel,new(post,y,.37f),new(post,y,.382f),.022f,-1,8);
                }
                s.Beam(m.steel,new(post,.28f,-.12f),new(post,.46f,.26f),.044f,.055f);
            }
            s.Box(m.frame,new(0,2.18f,.018f),new(1.12f,.20f,.71f));
            s.Box(m.warm,new(0,2.067f,.19f),new(.80f,.027f,.135f));
            s.Box(m.ochre,new(0,2.16f,.38f),new(.45f,.055f,.025f));
            for(int i=-3;i<=3;i++)
                s.Box(m.rubber,new(i*.095f,1.95f,-.073f),new(.062f,.074f,.03f));
            // The neck yoke and central post support the unworn pressure suit.
            s.Tube(m.steel,new(0,.25f,-.10f),new(0,1.81f,-.10f),.037f,-1,12);
            s.Beam(m.rubber,new(-.26f,1.56f,-.02f),new(0,1.69f,-.10f),.068f,.10f);
            s.Beam(m.rubber,new(0,1.69f,-.10f),new(.26f,1.56f,-.02f),.068f,.10f);
            s.Store(Root+"/Meshes/Equipment display - suit cabinet.asset");

            var suit=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Diver),bay);
            Mount(suit.transform,new(0,1.135f,.17f),Quaternion.Euler(0,index==0?7:-8,0));
            var rig=suit.GetComponent<DiverAvatarRig>();
            var axis=suit.transform;
            Tilt(rig.leftArm.upper,axis,new(-4,0,6));
            Tilt(rig.rightArm.upper,axis,new(-3,0,-5));
            Tilt(rig.leftArm.lower,axis,new(index==0?-12:-7,0,0));
            Tilt(rig.rightArm.lower,axis,new(index==0?-8:-13,0,0));
            Tilt(rig.head,axis,new(3,index==0?-4:6,0));
            rig.enabled=false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            if(index==0)
                s.Lamp("Suit inspection light",new Color(1,.82f,.66f),new(.58f,1.68f,1.39f),10.0f,3.5f);
            if(index==1)
                s.Lamp("Boot well inspection light",new Color(.63f,.84f,1),new(.26f,.56f,.88f),1.9f,2.0f);
        }

        static void GunCabinet(Transform parent,Finishes m)
        {
            var bay=Group(parent,"Harpoon charging cabinet",new(1.00f,0,-4.60f));
            bay.localScale=Vector3.one*.5f;
            var s=new ShipSectionMesh(bay,Vector3.zero,"Charging board",Vector3.zero);
            s.Box(m.frame,new(0,1.11f,-.16f),new(2.06f,2.15f,.22f));
            s.Box(m.paint,new(0,1.14f,-.027f),new(1.92f,1.90f,.062f));
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*.99f;
                s.Box(m.frame,new(x,1.11f,.03f),new(.11f,2.10f,.36f));
                s.Box(m.steel,new(x,.99f,.22f),new(.055f,1.48f,.018f));
                s.Box(m.blue,new(x,1.30f,.23f),new(.019f,.72f,.014f));
            }
            s.Box(m.frame,new(0,.13f,.015f),new(2.08f,.24f,.57f));
            s.Box(m.grate,new(0,.267f,.045f),new(1.93f,.038f,.45f));
            s.Box(m.frame,new(0,2.18f,.005f),new(2.08f,.17f,.58f));
            s.Box(m.warm,new(0,2.076f,.16f),new(1.47f,.034f,.11f));
            for(int i=0;i<6;i++)s.Box(m.rubber,new(-.74f+i*.27f,1.13f,.014f),new(.018f,1.70f,.02f));
            for(int i=0;i<2;i++)
            {
                float y=.78f+i*.72f;
                s.Box(m.frame,new(-.82f,y,.09f),new(.16f,.33f,.19f));
                s.Box(m.ochre,new(-.82f,y+.085f,.193f),new(.097f,.030f,.012f));
                s.Box(m.blue,new(-.82f,y-.042f,.191f),new(.046f,.046f,.013f));
                GunCradle(s,m,new(-.12f,y,.26f),7);
                Gun(bay,new(-.12f,y,.26f),Quaternion.AngleAxis(7,Vector3.forward)*Quaternion.Euler(0,90,0));
            }
            s.Lamp("Charge bay lamp",new Color(.71f,.87f,1),new(.09f,1.37f,.98f),6.8f,2.7f);
            s.Store(Root+"/Meshes/Equipment display - charging board.asset");
        }

        static void GunCradle(ShipSectionMesh s,Finishes m,Vector3 centre,float degrees)
        {
            var angle=Quaternion.AngleAxis(degrees,Vector3.forward);
            foreach(float offset in new[]{-.13f,.20f})
            {
                var at=centre+angle*new Vector3(offset,-.072f,0);
                s.Box(m.steel,at+new Vector3(0,-.04f,-.102f),new(.072f,.07f,.31f));
                s.Box(m.rubber,at+new Vector3(0,-.007f,.009f),new(.085f,.031f,.15f));
                s.Box(m.ochre,at+new Vector3(0,.03f,.086f),new(.081f,.102f,.037f));
            }
        }

        static void Gun(Transform parent,Vector3 at,Quaternion rotation)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Harpoon);
            var gun=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            Mount(gun.transform,at,rotation);
        }

        static void ServiceBench(Transform parent,Finishes m)
        {
            var bay=Group(parent,"Barrel inspection bench",new(4.00f,0,1.05f));
            bay.localScale=Vector3.one*.5f;
            bay.localRotation=Quaternion.Euler(0,-90,0);
            var s=new ShipSectionMesh(bay,Vector3.zero,"Service cradle",Vector3.zero);
            s.Box(m.paint,new(0,.55f,-.11f),new(2.20f,.91f,.35f));
            s.Box(m.frame,new(0,.99f,.055f),new(2.42f,.14f,.66f));
            s.Box(m.grate,new(0,1.071f,.07f),new(2.25f,.035f,.56f));
            s.Box(m.frame,new(0,1.49f,-.21f),new(2.36f,.82f,.12f));
            for(int i=-3;i<=3;i++)
                s.Box(m.rubber,new(i*.27f,1.48f,-.132f),new(.13f,.46f,.025f));
            foreach(float x in new[]{-.97f,.97f})
            {
                s.Box(m.ochre,new(x,.59f,.09f),new(.10f,.70f,.10f));
                s.Box(m.steel,new(x,1.53f,-.10f),new(.08f,.98f,.105f));
            }
            s.Box(m.frame,new(0,2.02f,.015f),new(2.25f,.13f,.38f));
            s.Box(m.warm,new(0,1.94f,.115f),new(1.72f,.028f,.095f));
            var centre=new Vector3(-.14f,1.27f,.11f);
            GunCradle(s,m,centre,0);
            Gun(bay,centre,Quaternion.Euler(0,90,-11));
            s.Lamp("Bench task light",new Color(1,.80f,.63f),new(0,1.81f,.57f),6.8f,2.5f);
            s.Store(Root+"/Meshes/Equipment display - service cradle.asset");
        }
    }
}
