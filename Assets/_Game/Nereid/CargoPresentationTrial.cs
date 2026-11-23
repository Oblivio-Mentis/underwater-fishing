#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.Nereid;
using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Raid
{
    // Opt-in assembly check for the controls and per-ID presentation. Uses only a disposable profile.
    internal sealed class CargoPresentationTrial : MonoBehaviour
    {
        [Serializable]
        sealed class Report
        {
            public string Status="running", Error;
            public List<string> Checks=new();
        }

        string folder;
        bool motion;
        string ReportPath=>Path.Combine(folder,"cargo-trial.json");
        readonly Report report=new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();
            int caseIndex=Array.IndexOf(args,"-raid-cargo-presentation");
            if(caseIndex<0 || caseIndex+1>=args.Length || Array.IndexOf(args,"-raid-save")<0)return;
            var go=new GameObject("Cage QA").AddComponent<CargoPresentationTrial>();
            go.folder=Path.GetFullPath(args[caseIndex+1]);
            go.motion=Array.IndexOf(args,"-raid-cargo-motion")>=0;
            Directory.CreateDirectory(go.folder);
            go.Run();
        }

        async void Run()
        {
            try
            {
                await Until(()=>RaidWorld.Instance?.Team!=null && DiverMotor.ServerDivers.Any(diver=>diver.IsOwner)
                    && RaidWorld.Instance.container.GetComponentInChildren<WetCargoShoal>()!=null,"World and local diver",45);
                var world=RaidWorld.Instance;
                var host=DiverMotor.ServerDivers.Single(diver=>diver.IsOwner);
                var cage=world.container.GetComponentInChildren<WetCargoShoal>();
                var control=world.container.GetComponentInChildren<CargoDispatch>();
                if(world.threats!=null)world.threats.enabled=false;
                // Keep the fixture camera on the cage rather than the optional first-meeting rider.
                world.Team.goshaMet=world.Team.goshaTamed=true;
                var camera=host.GetComponentInChildren<Camera>();
                var awaitingPeer=Array.IndexOf(Environment.GetCommandLineArgs(),"-raid-cargo-peer")>=0;
                if(awaitingPeer)await Until(()=>DiverMotor.ServerDivers.Count==2,"Standalone Windows peer",45);
                Check(world.Team.cargo.Count==0 && world.Team.tank.Count==0,"Fixture started with a clean manifest.");
                Vector3 center=world.container.position;
                Vector3 start=center+new Vector3(4.2f,.28f,.13f);
                await KeyPress(Key.L);
                host.Teleport(start);
                host.LookAtConsole(center);
                await Awaitable.WaitForSecondsAsync(.4f);
                ShootFrame("01-empty-cage.png");
                Check(!CargoDispatch.CanLoad(world,host),"Q outside release reach does not load the physical cage.");
                host.Teleport(center+new Vector3(2.14f,-.18f,-.40f));
                host.LookAtConsole(center);
                await Awaitable.WaitForSecondsAsync(.3f);
                Check(CargoDispatch.CanLoad(world,host),"Q from the open loading face targets the cage.");
                host.Teleport(center+new Vector3(0,-.17f,-2.19f));
                host.LookAtConsole(center);
                await Awaitable.WaitForSecondsAsync(.3f);
                Check(!CargoDispatch.CanLoad(world,host),"Solid rear grille blocks loading from outside.");

                var tool=host.GetComponent<HarpoonTool>();
                var species=FindObjectsByType<FishActor>().First(fish=>fish.SpeciesId=="Needlefish" && fish.depthTier==1);
                const long pocketCatch=15000001;
                var stock=new[]
                {
                    species.ToCargo(pocketCatch),
                    new CatchItem(15000002,7,3,3,3,1,species:"Bristlefish"),
                    new CatchItem(15000003,2,0,1,1,1,species:"Lanternfish"),
                    new CatchItem(15000004,8,1,3,3,1,species:"Sailray")
                };
                // Stage a caught pocket entry, then use owner input like an ordinary HUD session.
                host.Teleport(center+new Vector3(2.14f,-.18f,-.40f));
                host.LookAtConsole(center);
                tool.StagePocketForTrial(species,pocketCatch);
                Check(tool.SelectSlot(2),"Select the real Needlefish in the filled pocket.");
                await Until(()=>tool.ActiveSlot==2 && tool.HeldCargoId==pocketCatch && CargoDispatch.CanLoad(world,host),"Aim and cargo ready");
                await Until(()=>host.GetComponentInChildren<Camera>().GetComponentsInChildren<CatchFishModel>()
                    .Any(model=>model.CargoId==pocketCatch),"Hands display this catch ID");
                await KeyPress(Key.Q);
                await Until(()=>tool.CatchIdInSlot(2)==0 && world.Team.cargo.Any(item=>item.id==pocketCatch),"Owned Q transfers the same pocket catch");
                Check(world.DroppedCount==0,"Valid Q in the cage creates no duplicate world drop.");
                Check(world.Team.LoadCargo(stock[1],world.progression),"Staged "+stock[1].species+" #"+stock[1].id);
                host.Teleport(start);
                host.LookAtConsole(center+Vector3.up*.35f);
                await Until(()=>cage.Count==2,"Two individually shaped catches under the work light");
                await Awaitable.WaitForSecondsAsync(.18f);
                ShootFrame("02-half-load.png");
                foreach(var fish in stock.Skip(2))Check(world.Team.LoadCargo(fish,world.progression),"Staged "+fish.species+" #"+fish.id);
                await Until(()=>cage.Count==stock.Length,"Full cage showing all four species");
                await Awaitable.WaitForSecondsAsync(.18f);
                ShootFrame("03-full-cage.png");
                Check(!world.Team.LoadCargo(new CatchItem(15000005,2,0,1,1,1,species:"Needlefish"),world.progression),
                    "Cargo-size limit rejects an additional fish.");
                var original=stock.ToDictionary(fish=>fish.id,fish=>cage.ModelFor(fish.id));
                foreach(var entry in original)
                    Check(entry.Value!=null && entry.Value.GetComponent<CatchFishModel>().CargoId==entry.Key,
                        "Swimming model keeps cargo identity #"+entry.Key);
                host.Teleport(center+new Vector3(3.33f,.19f,1.32f));
                await Awaitable.WaitForSecondsAsync(.28f);
                host.LookAtConsole(control.transform.position);
                await Until(()=>CargoDispatch.Aimed(host,camera)==control && CargoDispatch.CanUse(host),"Both authoritative and local rays hit the mechanical button");
                ShootFrame("04-physical-button.png");
                Check(UiRoot.Instance.QaClickReticle(),"The owned world pointer dispatches the button click.");
                await Until(()=>world.Team.hoistPhase==HoistPhase.Raising,"Physical console starts hoist");
                if(motion)
                    await Track(host,()=>world.container.position+Vector3.up*.40f,"closing",17,.15f);
                else await Awaitable.WaitForSecondsAsync(.72f);
                host.LookAtConsole(world.container.position+Vector3.up*.50f);
                ShootFrame("05-closing-lid.png");
                await Until(()=>world.container.position.y>center.y+1,"Ascending cage");
                host.Teleport(new Vector3(27,-12.35f,-23.7f));
                host.LookAtConsole(world.container.position+Vector3.up*.42f);
                await Awaitable.WaitForSecondsAsync(.26f);
                ShootFrame("06-rising.png");
                host.Teleport(new Vector3(6.75f,1.48f,-11.5f));
                await Awaitable.WaitForSecondsAsync(.35f);
                var vessel=NereidVessel.Active;
                host.LookAtConsole(vessel.transform.TransformPoint(new Vector3(0,3.1f,-3)));
                await Until(()=>world.Team.hoistPhase==HoistPhase.Unloading,"Cage reaches the reservoir hopper",30);
                float duration=world.Team.HoistDuration(HoistPhase.Unloading,world.progression);
                if(motion)
                    await Track(host,()=>vessel.transform.TransformPoint(new Vector3(0,2.70f,-3)),"discharge",20,.125f);
                else await Until(()=>world.Team.hoistRemaining<duration*.28f,"Cage trapdoor opens and water descends");
                ShootFrame("07-discharge.png");
                await Until(()=>world.Team.cargo.Count==0 && cage.TankCount==stock.Length,"Original IDs reach reservoir");
                await Awaitable.WaitForSecondsAsync(.33f);
                foreach(var fish in stock)
                    Check(world.Team.tank.Count(x=>x.id==fish.id && x.species==fish.species)==1 && cage.ModelFor(fish.id)==original[fish.id],
                        "Original "+fish.species+" #"+fish.id+" uses the exact same visible model in the aquarium.");
                Vector3 tankCenter=vessel.transform.TransformPoint(new Vector3(0,1.54f,-3));
                await Until(()=>stock.All(catchItem=>
                {
                    var model=cage.ModelFor(catchItem.id);
                    if(model==null || !model.gameObject.activeInHierarchy || !model.GetComponent<Renderer>().enabled)return false;
                    var delta=model.position-tankCenter;
                    return Mathf.Abs(delta.y)<1.60f && Vector2.SqrMagnitude(new Vector2(delta.x,delta.z))<6.25f;
                }),"All four visible models remain inside the glass tank as the cage returns",1.4f);
                report.Checks.Add("All four active meshes remain in the aquarium while the lift returns.");
                host.LookAtConsole(tankCenter);
                await Awaitable.WaitForSecondsAsync(.70f);
                ShootFrame("08-aquarium.png");
                await Until(()=>world.Team.hoistPhase==HoistPhase.Loading,"Winch cycles back to its original load point");
                Check(world.Team.CargoUsed==0 && Vector3.Distance(world.container.position,center)<.12f,
                    "Cage reopens at its original loading position with only one delivered copy per ID.");
                report.Status="passed";
            }
            catch(Exception ex)
            {
                report.Status="failed";report.Error=ex.ToString();Debug.LogException(ex);
            }
            finally { Write(); }
        }

        async Awaitable Track(DiverMotor diver,Func<Vector3> point,string stage,int frames,float interval)
        {
            for(int i=0;i<frames;i++)
            {
                diver.LookAtConsole(point());
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                ShootFrame($"{stage}-{i:D2}.png");
                await Awaitable.WaitForSecondsAsync(interval);
            }
        }

        static async Awaitable KeyPress(Key key)
        {
            var keyboard=Keyboard.current??InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));
            await Awaitable.WaitForSecondsAsync(.14f);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        }

        void ShootFrame(string name)=>UiQaProbe.CapturePeerCamera(Path.Combine(folder,name));

        async Awaitable Until(Func<bool> ready,string description,float seconds=7f)
        {
            float until=Time.realtimeSinceStartup+seconds;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>=until)throw new TimeoutException(description);
                await Awaitable.NextFrameAsync();
            }
        }

        void Check(bool condition,string description)
        {
            if(!condition)throw new InvalidOperationException(description);
            report.Checks.Add(description);
            Write();
        }

        void Write()=>File.WriteAllText(ReportPath,JsonUtility.ToJson(report,true));
    }
}
#endif
