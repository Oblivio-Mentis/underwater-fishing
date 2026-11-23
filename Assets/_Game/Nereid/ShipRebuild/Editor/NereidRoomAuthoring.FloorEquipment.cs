using System;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        static void FitFloorGroup(string module,Vector3 moduleOrigin,string hostPath,Material finish)
        {
            Action build=module switch {
                "AftTransfer"=>TransferFloor,"EquipmentRoom"=>MachineFloor,"BioContainment"=>ContainmentFloor,
                "AirlockCollar"=>LockFloor,"CompanionBerth"=>BerthFloor,_=>null };
            if(build==null)return;
            const string folder=CabinFolder+"/Workstations";
            Folder(folder);Folder(folder+"/Meshes");
            string label=module+" workstations",path=folder+"/"+label+".prefab";
            var group=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!group)
            {
                var root=new GameObject(label);
                try
                {
                    origin=moduleOrigin;room=root.transform;parts.Clear();build();
                    foreach(var part in parts)
                        part.StorePalette(folder+"/Meshes/"+part.node.name+".asset",finish,cabinSwatches);
                    group=PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally {parts.Clear();UnityEngine.Object.DestroyImmediate(root);}
            }
            var host=PrefabUtility.LoadPrefabContents(hostPath);
            try
            {
                foreach(Transform child in host.transform)
                    if(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)==group)return;
                PrefabUtility.InstantiatePrefab(group,host.transform);
                PrefabUtility.SaveAsPrefabAsset(host,hostPath);
            }
            finally {PrefabUtility.UnloadPrefabContents(host);}
        }

        static void TransportCase(ShipSectionMesh s,Vector3 p,float w,float h,float d,Material paint)
        {
            s.Box(rubber,p+Vector3.up*.048f,new(w+.02f,.096f,d+.02f));
            s.Box(paint,p+Vector3.up*(h*.49f),new(w,h*.90f,d),default,.095f);
            s.Box(deck,p+Vector3.up*(h*.94f),new(w+.045f,h*.12f,d+.045f),default,.095f);
            foreach(float sign in new[]{-1f,1f})
            {
                var c=p+Vector3.right*(w*.29f*sign);
                s.Box(hull,c+Vector3.up*(h+.01f),new(w*.078f,.022f,d*.90f));
                s.Box(steel,c+new Vector3(0,h*.77f,d*.506f),new(w*.12f,h*.22f,.041f));
            }
        }

        static void VesselBottle(ShipSectionMesh s,Vector3 foot,float r,float h,Material paint)
        {
            var y=Vector3.up;
            s.Tube(rubber,foot,foot+y*(h*.09f),r*.85f,r,8);
            s.Tube(paint,foot+y*(h*.09f),foot+y*(h*.75f),r,-1,8);
            s.Tube(paint,foot+y*(h*.75f),foot+y*(h*.88f),r,r*.47f,8);
            s.Tube(ceramic,foot+y*(h*.62f),foot+y*(h*.69f),r*1.018f,-1,8);
            s.Tube(deck,foot+y*(h*.88f),foot+y*(h*.97f),r*.32f,-1,6);
            s.Box(ochre,foot+y*(h*.985f),new(r*.96f,h*.052f,r*.60f));
        }

        static void TransferFloor()
        {
            Vector3 p=new(3.55f,0,-15.51f),x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var rack=Part("Freight cradle and lashings",p);
            rack.Box(deck,p+y*.095f,new(2.94f,.19f,.99f),default,.14f);
            foreach(float dx in new[]{-1.28f,1.28f})
            {
                var foot=p+x*dx-z*.39f+y*.12f;
                rack.Beam(enamel,foot,foot+y*1.36f,.13f,.14f);
                rack.Beam(ochre,foot+y*.04f+z*.73f,foot+y*.64f,.075f,.081f);
            }
            rack.Beam(hull,p+new Vector3(-1.30f,1.45f,-.38f),p+new Vector3(1.30f,1.45f,-.38f),.16f,.18f);
            rack.Collision(p+y*.15f,new(2.94f,.30f,.99f));
            var cases=Part("Secured expedition transit cases",p-x*.33f);
            var big=p+new Vector3(-.58f,.19f,.006f);
            TransportCase(cases,big,1.35f,.72f,.77f,enamel);
            cases.Collision(big+y*.37f,new(1.39f,.74f,.81f));
            var small=p+new Vector3(.75f,.19f,-.016f);
            TransportCase(cases,small,.88f,.51f,.72f,hull);
            TransportCase(cases,small+y*.526f,.72f,.40f,.64f,ochre);
            cases.Collision(small+y*.47f,new(.92f,.94f,.76f));

            // A loading dolly is parked outside both the airlock apron and the central door line.
            var cartAt=new Vector3(3.63f,0,-10.75f);
            var cart=Part("Parked capstan dolly",cartAt);
            cart.Box(deck,cartAt+y*.176f,new(1.21f,.14f,.83f));
            foreach(float dx in new[]{-.43f,.43f})foreach(float dz in new[]{-.36f,.36f})
            {
                var hub=cartAt+new Vector3(dx,.112f,dz);
                cart.Tube(rubber,hub-z*.054f,hub+z*.054f,.108f,-1,8);
            }
            var drum=cartAt+new Vector3(-.12f,.48f,0);
            cart.Tube(enamel,drum-x*.34f,drum+x*.34f,.24f,-1,10);
            foreach(float dx in new[]{-.34f,.34f})
            {
                cart.Tube(ochre,drum+x*dx,drum+x*(dx+.045f),.295f,-1,10);
                cart.Beam(steel,cartAt+x*dx+y*.245f-z*.23f,drum+x*dx,.105f,.095f);
            }
            foreach(float dz in new[]{-.29f,.29f})
                cart.Beam(enamel,cartAt+new Vector3(.49f,.20f,dz),cartAt+new Vector3(.66f,.84f,dz),.057f,.062f);
            cart.Tube(deck,cartAt+new Vector3(.66f,.84f,-.32f),cartAt+new Vector3(.66f,.84f,.32f),.045f,-1,8);
            cart.Collision(cartAt+new Vector3(.015f,.34f,0),new(1.27f,.67f,.82f));
        }

        static void MachineFloor()
        {
            Vector3 p=new(-2.54f,0,-22.18f),x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var gen=Part("Stairwell auxiliary power skid",p);
            // The high end of the stair leaves a full machinery bay underneath its landing.
            gen.Box(hull,p+y*.10f,new(1.73f,.20f,2.40f));
            var engine=p+new Vector3(.15f,.59f,.09f);
            gen.Tube(enamel,engine-z*.72f,engine+z*.62f,.42f,-1,8);
            gen.Tube(steel,engine+z*.62f,engine+z*.75f,.44f,.35f,8);
            gen.Tube(deck,engine-z*.81f,engine-z*.68f,.43f,-1,8);
            var intake=engine+z*.757f;
            Band(gen,intake,x,y,.31f,.19f,.047f,ceramic,deck,0,360,8);
            gen.Beam(hull,intake-x*.17f+z*.055f,intake+x*.17f+z*.055f,.059f,.044f);
            foreach(float dz in new[]{-.49f,.40f})
            {
                gen.Tube(hull,engine+z*dz,engine+z*(dz+.085f),.44f,-1,8);
                gen.Box(steel,engine+new Vector3(0,-.30f,dz),new(.93f,.16f,.13f));
            }
            var box=p+new Vector3(-.51f,.69f,-.23f);
            gen.Box(dark,box,new(.44f,1.06f,1.52f));
            gen.Box(enamel,box+new Vector3(0,.54f,0),new(.49f,.09f,1.54f));
            HoodedLamp(gen,box+new Vector3(.226f,.35f,.45f),Vector3.back,y,.30f,.095f);
            gen.Collision(p+new Vector3(0,.53f,0),new(1.73f,1.06f,2.40f));
            var cartAt=new Vector3(-.98f,0,-20.27f);
            var station=Part("Mechanics tool trolley",cartAt);
            foreach(float dx in new[]{-.32f,.32f})foreach(float dz in new[]{-.29f,.29f})
            {
                var wheel=cartAt+new Vector3(dx,.07f,dz);
                station.Tube(rubber,wheel-x*.039f,wheel+x*.039f,.07f,-1,8);
            }
            station.Box(hull,cartAt+y*.33f,new(.89f,.43f,.68f));
            station.Box(steel,cartAt+y*.57f,new(.97f,.078f,.74f));
            for(int i=0;i<2;i++)
            {
                var f=cartAt+new Vector3(0,.27f+i*.16f,.344f);
                station.Box(i==0?enamel:ceramic,f,new(.77f,.13f,.030f));
                station.Beam(deck,f+z*.024f+x*.14f,f+z*.024f-x*.14f,.035f,.032f);
            }
            station.Box(ochre,cartAt+y*.64f+z*.13f,new(.47f,.094f,.23f));
            station.Collision(cartAt+y*.32f,new(.96f,.64f,.74f));
        }

        static void ContainmentFloor()
        {
            Vector3 p=new(-3.94f,0,6.84f),x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var machine=Part("Feeding bay dosing accumulator",p);
            machine.Box(deck,p+y*.085f,new(1.79f,.17f,1.07f));
            var tank=p+new Vector3(0,.62f,.10f);
            machine.Tube(hull,tank-x*.65f,tank+x*.63f,.32f,-1,10);
            machine.Tube(enamel,tank-x*.83f,tank-x*.64f,.19f,.32f,10);
            machine.Tube(enamel,tank+x*.63f,tank+x*.81f,.32f,.14f,10);
            foreach(float dx in new[]{-.40f,.40f})
            {
                machine.Tube(steel,tank+x*dx,tank+x*(dx+.065f),.33f,-1,10);
                machine.Beam(ochre,tank+x*dx-y*.045f-z*.27f,p+x*dx+y*.14f-z*.36f,.125f,.10f);
            }
            var plunger=tank+new Vector3(.56f,.16f,.04f);
            machine.Tube(steel,plunger,plunger+y*.52f,.095f,-1,8);
            machine.Tube(deck,plunger+y*.45f,plunger+y*.72f,.145f,-1,8);
            machine.Tube(hull,plunger+y*.66f,plunger+y*.66f+z*.54f,.097f,-1,8);
            machine.Collision(p+y*.48f,new(1.79f,.96f,1.07f));
            var cabinetAt=new Vector3(-5.13f,0,6.81f);
            var cabinet=Part("Dosing station valve cabinet",cabinetAt);
            cabinet.Box(enamel,cabinetAt+y*.67f,new(.56f,1.34f,.84f),default,.08f);
            var front=cabinetAt+new Vector3(.01f,.90f,-.425f);
            BourdonGauge(cabinet,front,Vector3.left,y,.16f,55);
            cabinet.Beam(ochre,front-y*.40f+x*.12f,front-y*.29f-x*.17f,.055f,.062f);
            cabinet.Collision(cabinetAt+y*.67f,new(.56f,1.34f,.84f));
        }

        static void LockFloor()
        {
            Vector3 p=new(7.52f,0,-15.07f),x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var rig=Part("Emergency breathing gas station",p);
            rig.Box(deck,p+y*.065f,new(1.42f,.13f,.43f));
            foreach(float dx in new[]{-.43f,0,.43f})
                VesselBottle(rig,p+x*dx+y*.13f,.162f,dx==0?.90f:.77f,dx==0?ceramic:enamel);
            foreach(float dx in new[]{-.64f,.64f})
                rig.Beam(hull,p+x*dx+y*.10f-z*.13f,p+x*dx+y*.64f+z*.10f,.079f,.071f);
            rig.Beam(steel,p-x*.67f+y*.57f+z*.155f,p+x*.67f+y*.57f+z*.155f,.056f,.071f);
            rig.Collision(p+y*.48f,new(1.42f,.96f,.43f));
        }

        static void BerthFloor()
        {
            Vector3 p=new(5.18f,0,1.80f),x=Vector3.right,y=Vector3.up,z=Vector3.forward;
            var bin=Part("Companion dry stores",p);
            TransportCase(bin,p,.83f,.47f,.64f,enamel);
            // A folded strap and bedroll identify the animal's stores from the deck.
            var roll=p+new Vector3(-.23f,.61f,.10f);
            bin.Tube(water,roll-z*.20f,roll+z*.20f,.12f,-1,8);
            bin.Tube(ceramic,roll-z*.035f,roll+z*.035f,.123f,-1,8);
            var liner=p+new Vector3(.07f,.55f,-.03f);
            bin.Box(water,liner,new(.58f,.12f,.43f),default,.10f);
            bin.Box(enamel,liner+y*.09f-z*.07f,new(.53f,.085f,.26f),default,.08f);
            bin.Collision(p+y*.31f,new(.87f,.62f,.68f));
            // Gravity feed ends above the berth's existing bowl. The pet can still approach from port.
            var foot=new Vector3(5.33f,0,4.06f);
            var feeder=Part("Berth gravity feeder",foot);
            feeder.Box(deck,foot+y*.043f,new(.44f,.086f,.41f));
            feeder.Beam(enamel,foot+x*.12f+y*.07f,foot+x*.12f+y*.75f,.09f,.11f);
            var hopper=foot+y*.66f;
            feeder.Tube(hull,hopper-y*.19f,hopper,.10f,.245f,6);
            feeder.Tube(water,hopper,hopper+y*.36f,.245f,-1,6);
            feeder.Tube(deck,hopper+y*.36f,hopper+y*.41f,.255f,-1,6);
            var chute=foot+y*.46f;
            feeder.Beam(steel,chute,chute-x*.43f-y*.20f,.11f,.077f);
            feeder.Collision(foot+y*.52f,new(.47f,1.04f,.44f));
        }
    }
}
