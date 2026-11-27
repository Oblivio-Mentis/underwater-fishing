#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using UnityEngine;

namespace Raid
{
    static partial class ExpeditionDefenseTrial
    {
        const int FilmSeed=44617;
        const int FilmFps=12;

        static async Awaitable Film(Report report,string folder,string[] args)
        {
            var world=RaidWorld.Instance;
            var d=world.threats;
            var host=UI.UiRoot.Instance.Context.Player;
            int depth=Array.IndexOf(args,"-raid-film-depth-2")>=0?2:1;
            if(!world.StageDefenseFilmDepth(depth))throw new InvalidOperationException("Film depth not available");
            await Awaitable.WaitForSecondsAsync(.65f);
            var team=world.Team;
            FishActor specimen=null;
            foreach(var fish in FishActor.ServerSchool)if(fish && fish.depthTier==depth && !fish.RequiresPartner){specimen=fish;break;}
            if(!specimen)throw new InvalidOperationException("Film has no fish for the cargo");
            for(int i=1;i<=20;i++)team.Deposit(specimen.ToCargo(900000+i),world.progression);
            var record=new DefenseRecord{
                seed=FilmSeed,budget=22,appetite=team.RequiredFood(world.progression),deficit=.78f,
                integrity=d.defenseTuning.maximumIntegrity
            };
            team.ongoingDefense=record;
            d.ResumeDefense(record);
            host.CameraImpulse.BeginBackgroundQa(60);
            await Wait(()=>d.DefenseCountdown<7.7f,"film begins with the incoming school",50);
            ReportPhase("film observation window",report,folder);
            var vessel=Nereid.NereidVessel.Active;
            Vector3 bearing=Vector3.ProjectOnPlane(d.Defense.approachEnd-d.DefenseCenter,Vector3.up).normalized;
            Vector3 arrival=d.DefenseCenter+bearing*55+Vector3.up*3.8f;
            float broadside=Vector3.Dot(bearing,vessel.transform.right)<0?-1:1;
            var room=vessel.ObservationRoom();
            var window=room?room.Find(broadside<0?"Port observation window 2":"Starboard observation window 2"):null;
            if(!window)throw new InvalidOperationException("Film needs the port or starboard observation window");
            host.Teleport(window.position-vessel.transform.right*(1.64f*broadside)-Vector3.up*.64f);
            await Awaitable.WaitForSecondsAsync(.24f);
            await Clip(folder,"01-window",5,host,()=>arrival);

            ReportPhase("film exterior approach",report,folder);
            Vector3 side=Vector3.Cross(Vector3.up,bearing);
            bool exterior=false;
            for(int step=0;step<16;step++)
            {
                var point=d.DefenseCenter+bearing*(15+step*.9f)+side*(step%2==0?7:-7)+Vector3.up*(2.4f+step*.38f);
                var eye=point+Vector3.up*.6f;
                if(vessel.ContainsSealed(eye) || !MarineSpawn.InOpenWater(depth,point,.66f) ||
                    !MarineSpawn.ClearLeg(eye,arrival,.18f,Vector3.Distance(eye,arrival)*.87f))continue;
                host.Teleport(point);
                exterior=true;
                break;
            }
            if(!exterior)throw new InvalidOperationException("Film needs a clear exterior view of the attack lane");
            await Clip(folder,"02-outside",7,host,()=>arrival);

            ReportPhase("film shield contact",report,folder);
            await Wait(()=>d.Defense.phase==DefensePhase.Assault,"film assault",10);
            d.DefenseFixturePauseGroups();
            d.DefenseFixtureSpawn(DefenseSpecies.Remora,false,12,9);
            await Wait(()=>d.Defense.impact>0,"film shield contact",24);
            Vector3 contact=d.Defense.impactPoint;
            WatchField(host,d,contact,8);
            await Clip(folder,"03-shield",3,host,()=>contact);
            d.DefenseFixtureSetIntegrity(0);
            contact=d.Defense.impactPoint;
            await Clip(folder,"04-breach",4,host,()=>contact);
            report.Check(world.CurrentDepth==depth && d.Defense.breached,
                $"Seed {FilmSeed} on Depth {depth}: window, exterior, contact and breach recorded.");
            report.phase="done";
        }

        static async Awaitable Clip(string root,string name,float duration,DiverMotor host,Func<Vector3> focus)
        {
            string folder=Path.Combine(root,name);
            Directory.CreateDirectory(folder);
            int frames=Mathf.RoundToInt(duration*FilmFps);
            float start=Time.realtimeSinceStartup;
            for(int frame=0;frame<frames;frame++)
            {
                float ready=start+frame/(float)FilmFps;
                do
                {
                    host.LookAtConsole(focus());
                    await Awaitable.EndOfFrameAsync();
                } while(Time.realtimeSinceStartup<ready);
                UI.UiQaProbe.CapturePeerCamera(Path.Combine(folder,$"{frame:D04}.png"),864,false);
            }
        }
    }

    public sealed partial class RaidWorld
    {
        internal bool StageDefenseFilmDepth(int tier)
        {
            if(!IsServerInitialized || team==null || tier<1 || tier>2)return false;
            if(CurrentDepth==tier)return true;
            pendingDepth=tier;
            CompleteTravel();
            return CurrentDepth==tier;
        }
    }
}
#endif
