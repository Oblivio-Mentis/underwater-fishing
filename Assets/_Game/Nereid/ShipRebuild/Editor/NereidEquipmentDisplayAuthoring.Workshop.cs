using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidEquipmentDisplayAuthoring
    {
        // Furnishings are authored in metres. The pressure hull uses two metres per local unit.
        static void FitWorkshop(Transform room, Finishes m)
        {
            FitChargeLines(room, m);
            FitServiceAccess(room, m);
            FitHydraulicSet(EquipmentBay(room, "Hydraulic proof pump", new(-1.08f, 0, -1.55f), 90), m);
            FitBench(EquipmentBay(room, "Valve repair station", new(4.02f, 0, -.68f), -90), m);
            FitBottleBank(EquipmentBay(room, "Breathing air bank", new(3.75f, 0, -4.49f), 0), m);
            FitCart(EquipmentBay(room, "Docked battery trolley", new(-.92f, 0, .69f), 76), m);
        }

        static Transform EquipmentBay(Transform room, string name, Vector3 at, float yaw)
        {
            var mount=Group(room,name,at);
            mount.localScale=Vector3.one*.5f;
            mount.localRotation=Quaternion.Euler(0,yaw,0);
            return mount;
        }

        static void StoreWorkshopMesh(ShipSectionMesh part,string label)
        {
            part.Store(Root+"/Meshes/Workshop - "+label+".asset");
        }

        static void Pipe(ShipSectionMesh s,Material finish,float radius,params Vector3[] points)
        {
            for(int i=1;i<points.Length;i++) s.Tube(finish,points[i-1],points[i],radius,-1,8);
        }

        static void FitChargeLines(Transform room, Finishes m)
        {
            var rail=new ShipSectionMesh(room,Vector3.zero,"Power feed and air lines",Vector3.zero);
            // Supply and return follow the edge of the stair and turn up the aft bulkhead.
            for(int i=0;i<2;i++)
            {
                float x=-1.48f+i*.105f;
                float top=1.61f+i*.12f;
                Pipe(rail,i==0?m.steel:m.ochre,.047f,new(x,.37f,-1.60f),new(x,.19f,-1.88f),
                    new(x,.19f,-4.34f),new(x,.35f,-4.65f),new(x,top-.21f,-4.65f),
                    new(x+.21f,top,-4.65f),new(3.67f,top,-4.65f));
                foreach(float z in new[]{-2.30f,-3.30f,-4.00f})
                    rail.Box(m.frame,new(x,.19f,z),new(.16f,.14f,.07f));
                for(int bracket=0;bracket<5;bracket++)
                    rail.Box(m.frame,new(-1.02f+bracket*1.06f,top,-4.65f),new(.072f,.14f,.14f));
            }
            foreach(float locker in new[]{-.96f,-.38f,.20f,.78f})
            {
                rail.Box(m.paint,new(locker,1.43f,-4.65f),new(.28f,.21f,.13f));
                rail.Box(m.steel,new(locker,1.43f,-4.572f),new(.19f,.13f,.035f));
                Pipe(rail,m.rubber,.035f,new(locker,1.60f,-4.61f),new(locker,1.24f,-4.54f),new(locker,1.10f,-4.56f));
            }
            // The two main feeder cables terminate on the back of the gun charging board.
            foreach(float x in new[]{1.43f,1.76f})
            {
                rail.Box(m.frame,new(x,1.46f,-4.63f),new(.21f,.23f,.13f));
                Pipe(rail,m.rubber,.051f,new(x,1.61f,-4.62f),new(x,1.33f,-4.60f),new(x,1.09f,-4.55f));
            }
            Pipe(rail,m.paint,.067f,new(2.79f,.95f,-4.62f),new(2.79f,1.60f,-4.62f));
            rail.Box(m.ochre,new(2.79f,1.23f,-4.60f),new(.25f,.23f,.20f));
            StoreWorkshopMesh(rail,"distribution lines");
        }

        static void FitServiceAccess(Transform room,Finishes m)
        {
            var s=new ShipSectionMesh(room,Vector3.zero,"Service deck covers and overhead lighting",Vector3.zero);
            // Flush access plates above the aft feed trunk run between the two work zones.
            foreach(float z in new[]{.65f,-.48f,-1.61f})
            {
                s.Box(m.frame,new(.73f,.011f,z),new(1.48f,.020f,1.11f),default,.085f);
                s.Box(m.paint,new(.73f,.026f,z),new(1.39f,.012f,1.02f),default,.095f);
                foreach(float x in new[]{.23f,1.23f})
                {
                    s.Box(m.rubber,new(x,.036f,z),new(.094f,.008f,.27f));
                    s.Box(m.steel,new(x,.041f,z),new(.039f,.010f,.17f));
                }
            }
            // A continuous drain receives the wet suits and the proof-pump discharge.
            s.Box(m.frame,new(-.444f,.015f,-2.22f),new(.27f,.032f,3.49f));
            for(int i=0;i<16;i++)
                s.Box(m.steel,new(-.444f,.036f,-.625f-i*.213f),new(.198f,.012f,.09f));
            // Connect the bottle regulator to the return line above the rear service panel.
            Pipe(s,m.paint,.053f,new(3.67f,1.73f,-4.65f),new(3.67f,1.25f,-4.65f),
                new(3.74f,1.10f,-4.62f),new(3.74f,1.05f,-4.62f));
            foreach(float z in new[]{1.23f,-2.10f})
            {
                s.Box(m.frame,new(.22f,3.32f,z),new(1.75f,.23f,.50f));
                s.Box(m.steel,new(.22f,3.172f,z),new(1.61f,.09f,.39f));
                s.Box(m.warm,new(.22f,3.114f,z),new(1.45f,.030f,.25f));
                s.Lamp("Service deck downlight",new Color(.95f,.82f,.65f),new(.22f,2.10f,z),13f,8.2f);
            }
            StoreWorkshopMesh(s,"service deck covers");
        }

        static void FitHydraulicSet(Transform mount,Finishes m)
        {
            var s=new ShipSectionMesh(mount,Vector3.zero,"Pressure test skid",Vector3.zero);
            s.Box(m.frame,new(0,.13f,0),new(2.15f,.21f,1.12f));
            foreach(float x in new[]{-.78f,.78f})
            {
                s.Box(m.rubber,new(x,.07f,0),new(.31f,.14f,1.17f));
                s.Box(m.steel,new(x,.31f,0),new(.24f,.30f,.79f));
            }
            // The electric motor drives the flanged pump through a guarded coupling.
            s.Tube(m.paint,new(-.94f,.68f,0),new(-.22f,.68f,0),.37f,-1,10);
            for(int i=0;i<5;i++)
            {
                float x=-.86f+i*.125f;
                s.Tube(m.frame,new(x,.68f,0),new(x+.034f,.68f,0),.39f,-1,10);
            }
            s.Tube(m.ochre,new(-.20f,.68f,0),new(.15f,.68f,0),.25f,-1,8);
            s.Tube(m.steel,new(.16f,.68f,0),new(.68f,.68f,0),.35f,.43f,10);
            s.Tube(m.paint,new(.59f,.68f,.08f),new(.59f,.68f,.44f),.32f,-1,10);
            s.Tube(m.frame,new(.59f,.68f,.42f),new(.59f,.68f,.49f),.37f,-1,10);
            s.Tube(m.steel,new(.59f,.68f,.49f),new(.59f,.68f,.53f),.24f,-1,10);
            Pipe(s,m.paint,.102f,new(.70f,.74f,-.03f),new(.88f,1.12f,-.03f),new(.88f,1.12f,-.58f));
            s.Box(m.frame,new(-.33f,1.18f,.13f),new(.76f,.36f,.36f),Quaternion.Euler(-9,0,0));
            s.Tube(m.steel,new(-.48f,1.18f,.24f),new(-.48f,1.18f,.35f),.128f,-1,12);
            s.Tube(m.rubber,new(-.48f,1.18f,.35f),new(-.48f,1.18f,.362f),.104f,-1,12);
            s.Beam(m.warm,new(-.48f,1.18f,.37f),new(-.43f,1.24f,.37f),.012f,.012f);
            s.Box(m.ochre,new(-.12f,1.17f,.32f),new(.113f,.084f,.050f));
            // High grab rail and foot guard protect the service side from passing crew.
            Pipe(s,m.ochre,.032f,new(-.98f,.25f,-.47f),new(-.98f,1.30f,-.47f),
                new(-.83f,1.44f,-.47f),new(.82f,1.44f,-.47f),new(.99f,1.28f,-.47f),new(.99f,.25f,-.47f));
            s.Lamp("Pump service bounce",new Color(.69f,.83f,.89f),new(0,1.75f,.82f),3.4f,3.4f);
            StoreWorkshopMesh(s,"proof pump");
        }

        static void FitBench(Transform mount,Finishes m)
        {
            var s=new ShipSectionMesh(mount,Vector3.zero,"Workbench and tool wall",Vector3.zero);
            s.Box(m.frame,new(0,.13f,0),new(3.03f,.17f,.75f));
            s.Box(m.paint,new(-.78f,.56f,0),new(1.33f,.83f,.71f));
            for(int i=0;i<3;i++)
            {
                float y=.34f+i*.23f;
                s.Box(m.frame,new(-.78f,y,.37f),new(1.20f,.204f,.065f));
                s.Box(m.steel,new(-.78f,y+.036f,.421f),new(.58f,.039f,.058f));
            }
            s.Box(m.paint,new(1.26f,.56f,0),new(.23f,.84f,.76f));
            s.Box(m.grate,new(.57f,.30f,-.04f),new(1.19f,.067f,.65f));
            s.Box(m.frame,new(0,1.03f,.02f),new(3.24f,.16f,.98f));
            s.Box(m.steel,new(0,1.119f,.065f),new(3.19f,.037f,.88f));
            s.Box(m.frame,new(0,1.87f,-.38f),new(3.02f,1.56f,.13f));
            s.Box(m.paint,new(0,1.87f,-.29f),new(2.88f,1.42f,.085f));
            foreach(float y in new[]{1.49f,2.14f})
                s.Box(m.steel,new(0,y,-.22f),new(2.61f,.050f,.061f));
            Wrench(s,m,new(-.98f,1.52f,-.14f),.67f,-12);
            Wrench(s,m,new(-.51f,1.52f,-.14f),.55f,-8);
            // Paired test leads hang from pegs above the vice.
            for(int i=0;i<2;i++)
            {
                float x=.36f+i*.46f;
                s.Tube(m.steel,new(x,2.20f,-.24f),new(x,2.20f,-.13f),.051f,-1,8);
                Pipe(s,i==0?m.rubber:m.ochre,.029f,new(x-.10f,1.45f,-.13f),new(x-.13f,1.94f,-.13f),
                    new(x-.05f,2.20f,-.13f),new(x+.07f,2.20f,-.13f),new(x+.14f,1.94f,-.13f),new(x+.10f,1.57f,-.13f));
            }
            s.Box(m.frame,new(.77f,1.16f,.18f),new(.55f,.079f,.39f));
            s.Box(m.ochre,new(.77f,1.27f,.20f),new(.44f,.19f,.26f));
            foreach(float x in new[]{.62f,.91f})
                s.Box(m.steel,new(x,1.39f,.20f),new(.13f,.14f,.32f));
            s.Tube(m.steel,new(.77f,1.24f,.32f),new(.77f,1.24f,.51f),.034f,-1,8);
            s.Beam(m.rubber,new(.68f,1.13f,.51f),new(.86f,1.35f,.51f),.035f,.035f);
            s.Box(m.frame,new(0,2.69f,-.12f),new(3.17f,.12f,.52f));
            s.Box(m.warm,new(0,2.616f,.012f),new(2.54f,.036f,.104f));
            s.Lamp("Repair work light",new Color(1,.81f,.60f),new(0,1.91f,.80f),5.2f,3.2f);
            StoreWorkshopMesh(s,"valve bench");
        }

        static void Wrench(ShipSectionMesh s,Finishes m,Vector3 foot,float height,float slope)
        {
            var rotation=Quaternion.Euler(0,0,slope);
            Vector3 At(float x,float y)=>foot+rotation*new Vector3(x,y,0);
            s.Beam(m.steel,At(0,0),At(0,height),.068f,.038f);
            s.Beam(m.rubber,At(0,.02f),At(0,height*.40f),.088f,.05f);
            foreach(int sign in new[]{-1,1})
            {
                s.Beam(m.steel,At(0,height-.08f),At(sign*.10f,height+.005f),.060f,.047f);
                s.Beam(m.steel,At(sign*.10f,height),At(sign*.103f,height+.093f),.051f,.045f);
            }
        }

        static void FitBottleBank(Transform mount,Finishes m)
        {
            var s=new ShipSectionMesh(mount,Vector3.zero,"Breathing air bottle cage",Vector3.zero);
            s.Box(m.frame,new(0,.12f,0),new(1.52f,.24f,.84f));
            s.Box(m.paint,new(0,1.08f,-.32f),new(1.51f,1.99f,.13f));
            foreach(float x in new[]{-.35f,.35f})
            {
                s.Tube(m.frame,new(x,.20f,.044f),new(x,.39f,.044f),.25f,.29f,10);
                s.Tube(m.ochre,new(x,.39f,.044f),new(x,1.39f,.044f),.29f,-1,10);
                s.Tube(m.ochre,new(x,1.39f,.044f),new(x,1.63f,.044f),.29f,.117f,10);
                s.Tube(m.steel,new(x,1.63f,.044f),new(x,1.82f,.044f),.080f,-1,8);
                s.Tube(m.frame,new(x,.95f,.044f),new(x,1.05f,.044f),.306f,-1,10);
                Pipe(s,m.rubber,.042f,new(x,1.78f,.044f),new(x,2.04f,-.033f),new(x,2.12f,-.26f));
            }
            foreach(float x in new[]{-.72f,.72f})
                s.Beam(m.steel,new(x,.20f,.33f),new(x,1.11f,.33f),.071f,.071f);
            s.Beam(m.paint,new(-.73f,.71f,.33f),new(.73f,.71f,.33f),.097f,.079f);
            s.Box(m.steel,new(0,2.11f,-.26f),new(1.38f,.14f,.15f));
            StoreWorkshopMesh(s,"breathing air cascade");
        }

        static void FitCart(Transform mount,Finishes m)
        {
            var cart=new ShipSectionMesh(mount,Vector3.zero,"Cell trolley",Vector3.zero);
            // Outrigger casters and a low handle keep this dock below the sightline to the bridge stairs.
            foreach(float x in new[]{-.58f,.58f}) foreach(float z in new[]{-.31f,.31f})
            {
                cart.Box(m.steel,new(x,.245f,z),new(.11f,.18f,.10f));
                cart.Tube(m.rubber,new(x-.07f,.135f,z),new(x+.07f,.135f,z),.12f,-1,8);
            }
            cart.Box(m.frame,new(0,.34f,0),new(1.48f,.14f,.88f));
            cart.Box(m.paint,new(0,.50f,0),new(1.41f,.24f,.82f));
            foreach(float z in new[]{-.32f,.32f})
                Pipe(cart,m.ochre,.035f,new(-.61f,.53f,z),new(-.80f,.97f,z),new(-.80f,1.18f,z));
            cart.Tube(m.rubber,new(-.80f,1.18f,-.36f),new(-.80f,1.18f,.36f),.051f,-1,8);
            StoreWorkshopMesh(cart,"cell trolley");
            foreach(float x in new[]{-.31f,.31f})
            {
                var cell=new ShipSectionMesh(mount,Vector3.zero,"Quick-release power cell",new(x,.64f,0));
                Vector3 p=new(x,.64f,0);
                cell.Box(m.rubber,p+Vector3.up*.036f,new(.44f,.075f,.62f));
                cell.Box(m.ochre,p+Vector3.up*.27f,new(.39f,.45f,.55f),default,.095f);
                cell.Box(m.frame,p+Vector3.up*.49f,new(.43f,.098f,.61f));
                cell.Box(m.steel,p+new Vector3(0,.55f,.15f),new(.25f,.055f,.079f));
                cell.Box(m.blue,p+new Vector3(0,.32f,.282f),new(.17f,.045f,.017f));
                StoreWorkshopMesh(cell,"exchange cell");
            }
        }
    }
}
