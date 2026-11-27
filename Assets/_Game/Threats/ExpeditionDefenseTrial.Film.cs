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
            await Wait(()=>d.DefenseCountdown<20.8f,"film begins with the first glints beyond the window",50);
            ReportPhase("film observation window",report,folder);
            var vessel=Nereid.NereidVessel.Active;
            Vector3 bearing=Vector3.ProjectOnPlane(d.Defense.approachEnd-d.DefenseCenter,Vector3.up).normalized;
            Vector3 arrival=d.DefenseCenter+bearing*55+Vector3.up*3.8f;
            if(!d.TryDefenseWindow(d.Defense.approachEnd,d.Defense.approachStart,out var eyeAtWindow))
                throw new InvalidOperationException("No window with a clear line to the approaching shoal");
            host.Teleport(eyeAtWindow-Vector3.up*.60f);
            await Awaitable.WaitForSecondsAsync(.24f);
            var windowFocus=Vector3.Lerp(d.Defense.approachEnd,d.Defense.approachStart,.43f);
            var camera=host.GetComponentInChildren<Camera>();
            if(!host.IsInside || Vector3.Distance(camera.transform.position,eyeAtWindow)>.32f)
                throw new InvalidOperationException($"The diver did not settle at the observation window: eye {camera.transform.position:F2}, expected {eyeAtWindow:F2}");
            await Clip(folder,"01-window",9,host,()=>windowFocus);
            await Wait(()=>d.DefenseCountdown<4.4f,"the wing banks away from the reserve",22);

            ReportPhase("film exterior approach",report,folder);
            Vector3 side=Vector3.Cross(Vector3.up,bearing);
            Vector3 exterior=default;
            int best=0;
            for(int step=0;step<25;step++)
            {
                int port=step%5-2;
                var point=d.DefenseCenter+bearing*(16+Mathf.Abs(port)*2)+side*(port*5.8f)+Vector3.up*(2.8f+step/5*2.7f);
                var eye=point+Vector3.up*.6f;
                if(vessel.ContainsSealed(eye) || !MarineSpawn.InOpenWater(depth,point,.66f) ||
                    !ClearFrame(eye,arrival))continue;
                int score=1;
                if(ClearFrame(eye,arrival+side*14))score++;
                if(ClearFrame(eye,arrival-side*14))score++;
                if(ClearFrame(eye,arrival+Vector3.up*13))score++;
                if(ClearFrame(eye,d.Defense.approachStart))score++;
                if(score<=best)continue;
                best=score;
                exterior=point;
            }
            if(best<3)throw new InvalidOperationException("Film needs a clear exterior view of the attack lane");
            host.Teleport(exterior);
            await Clip(folder,"02-outside",9,host,()=>Vector3.Lerp(arrival,d.DefenseApproachFocus,.26f));

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

            bool ClearFrame(Vector3 from,Vector3 to) => MarineSpawn.ClearLeg(from,to,.20f,Vector3.Distance(from,to)-.5f);
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
            if(!team.CanVisitDepth(tier))team.teamSkills.Add(SkillTreeDefinition.Id(SkillLine.Hull,1));
            pendingDepth=tier;
            CompleteTravel();
            return CurrentDepth==tier;
        }
    }
}
#endif
