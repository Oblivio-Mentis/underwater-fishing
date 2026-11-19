using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FishNet.Object.Synchronizing;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    // Run on a host in Play Mode. Fixtures stage positions, travel and inventory.
    // Predators decide, move, telegraph and deal damage on normal FishNet simulation ticks.
    public static class NereidEncounterChecks
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        sealed class Until
        {
            public string label;
            public Func<bool> ready;
            public float seconds;
        }
        sealed class SavedDiver
        {
            public DiverMotor motor;
            public Vector3 position;
            public DiverMotor.DiverState state;
        }
        static RaidWorld world;
        static ThreatDirector threats;
        static DiverMotor diver;
        static HarpoonTool tool;
        static string savePath;
        static TeamProgress original;
        static int originalDepth;
        static readonly List<SavedDiver> saved=new();
        static readonly List<string> passed=new();
        static IEnumerator<Until> checks;
        static Until waiting;
        static double deadline;
        public static string Status { get;private set; }="Idle";

        [MenuItem("RAID/QA/Run reef encounters on host")]
        public static void Start()
        {
            if(checks!=null)throw new InvalidOperationException("Reef probe is already running.");
            world=RaidWorld.Instance;threats=ThreatDirector.Instance;
            if(!EditorApplication.isPlaying || world==null || !world.IsServerInitialized || threats==null || NereidVessel.Active==null || world.Team==null)
                throw new InvalidOperationException("Start the NEREID scene as host before running the encounter check.");
            if(world.TravelRemaining>0 || world.FishRoute.active || world.Team.hoistPhase!=HoistPhase.Loading)
                throw new InvalidOperationException("Finish the active travel, fish route, or hoist before running the encounter check.");
            saved.Clear();passed.Clear();
            foreach(var candidate in DiverMotor.ServerDivers)
            {
                var gear=candidate.GetComponent<HarpoonTool>();
                if(candidate.IsHelpless || candidate.IsTrapped || gear==null || gear.CatchCount!=0 || gear.HookedFish!=null || gear.TetherActive)
                    throw new InvalidOperationException("Run the probe with living divers and empty hands and pockets.");
                saved.Add(new SavedDiver{motor=candidate,position=candidate.transform.position,state=DiverState(candidate).Value});
            }
            diver=DiverMotor.ServerDivers.FirstOrDefault(x=>x.IsOwner)??DiverMotor.ServerDivers.FirstOrDefault();
            if(diver==null)throw new InvalidOperationException("Wait for the host diver to spawn.");
            tool=diver.GetComponent<HarpoonTool>();original=world.Team;originalDepth=world.CurrentDepth;
            savePath=(string)Field(world,"savePath").GetValue(world);
            // The real save object never receives fixture cargo or a save write.
            Field(world,"savePath").SetValue(world,null);
            checks=Sequence().GetEnumerator();waiting=null;Status="Running";
            EditorApplication.update+=Tick;
        }

        static IEnumerable<Until> Sequence()
        {
            Stage(1);
            var school=UnityEngine.Object.FindObjectsByType<FishActor>().Where(f=>f.depthTier==1).ToArray();
            yield return Wait("Travel begins with a quiet fishing ground",()=>school.All(f=>!f.InHabitat));
            yield return Wait("A single scout swims into the habitat",()=>school.Count(f=>f.InHabitat)==1,4);
            var scout=school.First(f=>f.InHabitat);
            var start=scout.transform.position;
            yield return Wait("Scout swims several body lengths on patrol",()=>Vector3.Distance(scout.transform.position,start)>2f,4);
            var aim=diver.AimDirection;
            var side=Vector3.Cross(Vector3.up,aim).normalized;
            var eye=scout.transform.position-aim*6.7f+side*1.65f;
            Place(eye-Vector3.up*.6f);
            int accepted=tool.ShotSequence;
            if(!(bool)typeof(HarpoonTool).GetMethod("QaFireOwned",Private).Invoke(tool,null))throw new InvalidOperationException("Keep the harpoon equipped and the HUD closed to menus.");
            yield return Wait("An accepted near miss startles a free fish",()=>tool.ShotSequence>accepted && scout.Startled && tool.HookedFish==null);
            Place(new Vector3(1,1.1f,1));
            yield return Wait("Fish settles after the shot",()=>!scout.Startled,6);
            start=scout.transform.position;
            Place(start+new Vector3(.3f,.15f,1.65f));
            yield return Wait("A close living swimmer makes the scout flee",()=>scout.Startled);
            yield return Wait("Startled fish actually moves away",()=>Vector3.Distance(diver.transform.position,scout.transform.position)>2.5f);
            Place(new Vector3(1,1.1f,1));
            yield return Wait("Three fish arrive over separate release windows",()=>school.Count(f=>f.InHabitat)==3,18);
            yield return Pause(6.6f);
            if(school.Count(f=>f.InHabitat)>3)throw new InvalidOperationException("A fourth wild fish exceeded the cap.");
            foreach(bool breakLine in new[]{false,true})
            {
                // Stage only the shooter's position. Both the impact and line loss run through
                // the real harpoon, collider, network command and server fish FSM.
                Vector3 direction=diver.AimDirection;
                var body=scout.ImpactSurface(false);
                Place(body.TransformPoint(Vector3.forward*.09f)-direction*3.7f-Vector3.up*.6f);
                if(!(bool)typeof(HarpoonTool).GetMethod("QaFireOwned",Private).Invoke(tool,null))
                    throw new InvalidOperationException("Owned shot unavailable in escape check.");
                yield return Wait("Harpoon attached before "+(breakLine?"range break":"release"),()=>tool.HookedFish==scout && scout.Mode==FishMode.Struggling);
                if(breakLine) Place(diver.transform.position-direction*(tool.tuning.maxRange+1));
                else if(!(bool)typeof(HarpoonTool).GetMethod("QaReleaseOwned",Private).Invoke(tool,null))
                    throw new InvalidOperationException("Owned release unavailable.");
                Vector3 releasePoint=diver.transform.position+Vector3.up*.6f;
                float releaseRange=Vector3.Distance(scout.transform.position,releasePoint);
                yield return Wait("Fish clears the angler after "+(breakLine?"a broken line":"manual release"),
                    ()=>tool.HookedFish==null && scout.Startled && Vector3.Distance(scout.transform.position,releasePoint)>releaseRange+3f,3);
                Place(new Vector3(1,1.1f,1));
                yield return Wait("Escaped fish resumes its patrol",()=>scout.Mode==FishMode.Roam && !scout.Startled,6);
            }
            yield return Wait("Dry deck restores authored visibility",()=>UnderwaterLook.Instance.WaterBlend<.015f && RenderSettings.fogDensity<.03f);
            yield return Wait("Depth one active",()=>threats.Reef.Active&&!threats.Reef.Deep);
            var angler=threats.tuning.predatorSpawn;
            Place(angler+new Vector3(0,.3f,7.5f));
            yield return Wait("Nearby passive angler watches the diver in murky water",()=>
                threats.AnglerStage==ThreatDirector.HuntStage.Watching && threats.NearbyDanger(diver.transform.position)>.16f &&
                UnderwaterLook.Instance.WaterBlend>.95f && RenderSettings.fogDensity>.03f && RenderSettings.fogDensity<.05f);
            float health=diver.Oxygen;
            Place(angler+new Vector3(0,.1f,3.6f));
            yield return Wait("Angler gives a display before contact",()=>threats.AnglerStage==ThreatDirector.HuntStage.Display);
            yield return Wait("Angler pursues after its warning",()=>threats.AnglerStage==ThreatDirector.HuntStage.Pursuit);
            yield return Wait("Angler ambush removes oxygen",()=>diver.Oxygen<=health-10 && threats.Message.Contains("Фонарник"));
            Place(threats.Reef.Clicker+Vector3.right*5.8f);
            yield return Pause(.3f);
            health=diver.Oxygen;
            Place(threats.Reef.Clicker+Vector3.right*.75f);
            yield return Wait("Clicker strikes a sudden acceleration",()=>diver.Oxygen<=health-10 && threats.Message.Contains("Щелкун"));

            Place(ThreatDirector.ClamBed+Vector3.up*.21f);
            yield return Wait("Bottom trap closes and holds",()=>diver.IsTrapped&&threats.Reef.ClamClosed);
            diver.Rescue();
            yield return Wait("Clam releases after server rescue",()=>!diver.IsTrapped&&!threats.Reef.ClamClosed);

            Stage(2);
            yield return Wait("Depth two active",()=>threats.Reef.Active&&threats.Reef.Deep);
            FillCreel();Place(threats.Reef.Eel+Vector3.forward*1.2f);
            yield return Wait("Deep water keeps its shorter sightline",()=>UnderwaterLook.Instance.WaterBlend>.95f && RenderSettings.fogDensity>.10f);
            yield return Wait("Eel warns with three occupied pockets",()=>threats.Reef.EelPhase==1&&tool.HasFullCreel);
            if(!tool.DropOneCatch())throw new InvalidOperationException("Could not drop the selected catch.");
            yield return Wait("Dropped fish diverts eel",()=>threats.Reef.EelPhase==3&&tool.CatchCount==2);
            Place(new Vector3(0,1.5f,0));
            tool.DropCargo();Stage(2);
            yield return Wait("Deep encounter restarted",()=>threats.Reef.Active&&threats.Reef.Deep);
            FillCreel();Place(threats.Reef.Eel+Vector3.forward*.85f);health=diver.Oxygen;
            yield return Wait("Eel bites and steals exactly one fish",()=>tool.CatchCount==2&&diver.Oxygen<=health-6&&threats.Message.Contains("Сетевик"));
            tool.DropCargo();

            Stage(2);
            yield return Wait("Rift reset",()=>threats.Reef.Deep);
            Place(ThreatDirector.Rift+new Vector3(0,3.1f,4.2f));
            Hoist();
            yield return Wait("Sustained winch telegraphs locked Kraken sweep",()=>threats.Reef.KrakenPhase==1,6);
            health=diver.Oxygen;
            yield return Wait("Exposed target takes Kraken sweep",()=>threats.Reef.KrakenPhase==2&&diver.Oxygen<=health-22&&threats.Message.Contains("Кракен"),4);

            Stage(2);
            yield return Wait("Rift reset for escape",()=>threats.Reef.Deep);
            Place(ThreatDirector.Rift+new Vector3(0,3.1f,4.2f));
            Hoist();
            yield return Wait("Second sweep telegraphs",()=>threats.Reef.KrakenPhase==1,6);
            health=diver.Oxygen;Vector3 locked=threats.Reef.Sweep;
            Place(locked+Vector3.forward*7.3f);
            yield return Wait("Swimming out of the locked area avoids hit",()=>threats.Reef.KrakenPhase==2&&Vector3.Distance(locked,threats.Reef.Sweep)<.1f&&diver.Oxygen>health-8,4);
        }

        static void Stage(int tier)
        {
            tool.DropCargo();diver.Rescue();
            var fixture=new TeamProgress(world.progression){highestDepth=2,hullLevel=1,hungerRemaining=3600};
            Field(world,"team").SetValue(world,fixture);
            Travel(tier);
            var value=DiverState(diver);var state=value.Value;
            state.Oxygen=diver.tuning.oxygenCapacity;state.Stress=0;value.Value=state;
        }

        static void FillCreel()
        {
            var catches=UnityEngine.Object.FindObjectsByType<FishActor>().Where(f=>!f.RequiresPartner&&f.IsServerInitialized).Take(3).ToArray();
            if(catches.Length<3)throw new InvalidOperationException("Three ordinary fish are required for full-creel fixture.");
            foreach(var fish in catches)
            {
                fish.SetDepthActive(true);
                if(!fish.TryHook(tool))throw new InvalidOperationException("Fixture fish could not attach to the server tool.");
                Field(tool,"_fish").SetValue(tool,fish);
                fish.EnterState(FishMode.Captured);
            }
            if(!tool.HasFullCreel)throw new InvalidOperationException("Fish did not occupy three real inventory slots.");
        }

        static void Hoist()
        {
            if(!world.Team.LoadCargo(new CatchItem(710012,1,0,1,1,2),world.progression)||!world.Team.BeginHoist(world.progression))
                throw new InvalidOperationException("Real hoist rejected staged cargo.");
        }

        static void Place(Vector3 position){diver.Teleport(position);Physics.SyncTransforms();}
        static FieldInfo Field(object owner,string name)=>owner.GetType().GetField(name,Private)??throw new MissingFieldException(owner.GetType().Name,name);
        static SyncVar<DiverMotor.DiverState> DiverState(DiverMotor owner)=>(SyncVar<DiverMotor.DiverState>)Field(owner,"_state").GetValue(owner);
        static void Travel(int tier)
        {
            Field(world,"pendingDepth").SetValue(world,tier);
            typeof(RaidWorld).GetMethod("CompleteTravel",Private).Invoke(world,null);
            Physics.SyncTransforms();
        }
        static Until Wait(string label,Func<bool> condition,float seconds=3)=>new(){label=label,ready=condition,seconds=seconds};
        static Until Pause(float seconds)
        {
            float time=Time.time+seconds;return Wait(null,()=>Time.time>=time,seconds+2);
        }

        static void Tick()
        {
            try
            {
                if(!EditorApplication.isPlaying||world==null||!world.IsServerInitialized)throw new InvalidOperationException("Host session ended.");
                if(waiting!=null)
                {
                    if(!waiting.ready())
                    {
                        if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException(waiting.label??"Fixed tick timeout.");
                        return;
                    }
                    if(waiting.label!=null)passed.Add(waiting.label);
                }
                if(!checks.MoveNext()){Finish(null);return;}
                waiting=checks.Current;
                // Allow a slower player renderer to advance enough simulation ticks.
                deadline=EditorApplication.timeSinceStartup+2+waiting.seconds*3;
                Status="Running: "+waiting.label+" | passed "+passed.Count;
            }
            catch(Exception e){Finish(e.GetBaseException().Message);}
        }

        static void Finish(string failure)
        {
            EditorApplication.update-=Tick;
            checks?.Dispose();checks=null;waiting=null;
            try
            {
                if(world!=null&&world.IsServerInitialized)
                {
                    tool.DropCargo();diver.Rescue();
                    Field(world,"team").SetValue(world,original);Travel(originalDepth);
                    foreach(var player in saved)if(player.motor!=null&&player.motor.IsServerInitialized)
                    {player.motor.Teleport(player.position);DiverState(player.motor).Value=player.state;}
                    typeof(RaidWorld).GetMethod("Publish",Private).Invoke(world,null);
                }
            }
            catch(Exception e){failure=(failure??"")+" Restore: "+e.GetBaseException().Message;}
            finally{if(world!=null)Field(world,"savePath").SetValue(world,savePath);}
            Status=(failure==null?"PASS":"FAIL "+failure)+" | "+string.Join(" / ",passed);
            if(failure==null)Debug.Log("[RAID reef] "+Status);else Debug.LogWarning("[RAID reef] "+Status);
        }
    }
}
