#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    internal sealed partial class FisheryTrial
    {
        IEnumerator GoshaDenTrial()
        {
            var vessel=NereidVessel.Active;
            var balance=world.Team.Treasury;
            Check(world.Team.goshaDen.Count==0,"An existing empty berth starts without junk construction");
            Check(world.TrialStartDenSearch(0,out var sea),"Scent rank one uncovers a real seabed field of junk");
            host.Teleport(sea+Vector3.up*1.25f+Vector3.forward*2);
            host.LookAtConsole(sea+Vector3.forward*7);
            if(peer)peer.Teleport(vessel.transform.TransformPoint(new Vector3(2,.8f,1)));
            yield return Wait("Gosha picks up seabed junk at scent rank one",()=>world.TrialJunkState(GoshaJunkLocation.Carried),16);
            yield return Wait("early-rank Gosha leaves the find near a diver",()=>world.TrialJunkState(GoshaJunkLocation.Shown),14);
            Check(world.Team.goshaDen.Count==0 && world.Team.Treasury.Equals(balance),
                "An early find reaches the diver without generating resources or building the den");

            host.Teleport(vessel.transform.TransformPoint(new Vector3(1,.8f,1)));
            Check(world.TrialStartDenSearch(3,out _),"The third delivery rank unlocks returning junk to the berth");
            yield return Wait("rank three Gosha picks up junk",()=>world.TrialJunkState(GoshaJunkLocation.Carried),20);
            yield return Wait("Gosha walks to the bunk and positions the first piece",()=>world.Gosha.DenPlacement>0,67);
            yield return Wait("the first den piece is assembled",()=>world.Team.goshaDen.Count==1,6);
            Check(world.Team.Treasury.Equals(balance) && world.TrialDenSaved(),
                "The carried piece became a persistent den piece, with no change to the treasury");

            world.TrialStartDenFromDeck();
            yield return Wait("Gosha leaves the ship through his service hatch to search",()=>world.Gosha.ServiceHatchActive,27);
            yield return Wait("Gosha finds a new piece on a regular sortie from the ship",()=>world.TrialJunkState(GoshaJunkLocation.Carried),34);
            yield return Wait("Gosha carries the junk back through his own service hatch",()=>world.Gosha.ServiceHatchActive,32);
            yield return Wait("Gosha completes another placement without staging the outing",()=>world.Team.goshaDen.Count==2,37);
            world.TrialDenAtEight();
            var camera=new GameObject("Den inspection camera").AddComponent<Camera>();
            camera.CopyFrom(host.GetComponentInChildren<Camera>());
            camera.enabled=false;
            camera.fieldOfView=55;
            try
            {
                var den=GoshaDenLayout.Origin(vessel);
                var from=den-vessel.transform.right*1.96f-vessel.transform.forward*.28f+Vector3.up*.83f;
                yield return Wait("Gosha arranges the next find by the bunk in the same working shift",()=>world.Gosha.DenPlacement>48,85);
                BowFrame(camera,from,den+Vector3.up*.42f,"gosha-arranges-den.png");
                yield return Wait("Gosha finishes the den around the bunk",()=>world.Team.goshaDen.Count==9,6);
                Check(world.TrialDenSaved(),"All nine pieces deserialize from the saved campaign in the same order");
                Check(world.Team.Treasury.Equals(balance),"The completed den cost no crafting materials and yielded no loot");
                yield return null;
                Check(GameObject.Find("Гошин домик") &&
                    GameObject.Find("Полог из старого брезента") && GameObject.Find("Рыбацкая сетка и подвески"),
                    "The client rendered the scrap frame, patched awning, and hanging net in the berth");
                BowFrame(camera,from,den+Vector3.up*.42f,"gosha-junk-den.png");
                BowFrame(camera,den-vessel.transform.right*1.55f+vessel.transform.forward*.71f+Vector3.up*.60f,
                    den+Vector3.up*.43f,"gosha-den-side.png");
                report.screenshot="gosha-junk-den.png";
            }
            finally { if(camera)Destroy(camera.gameObject); }
        }
    }

    public sealed partial class RaidWorld
    {
        internal bool TrialStartDenSearch(int delivery,out Vector3 at)
        {
            StageWorkForTrial(GoshaWork.Materials,5,6,1,1,delivery);
            usefulBeforeJunk=0;
            SeedGoshaJunk();
            for(int i=0;i<goshaJunk.Count;i++)
            {
                var item=goshaJunk[i];
                if(item.Location!=GoshaJunkLocation.Seabed)continue;
                var candidate=item.Position+Vector3.up*1.19f+Vector3.back*1.12f;
                if(!MarineSpawn.InOpenWater(CurrentDepth,candidate,.52f) ||
                   !swimRoute.Clear(candidate,item.Position+Vector3.up*.49f))continue;
                at=item.Position;
                pet.Position=candidate;
                pet.Heading=(at-candidate).normalized;
                swimRoute.Reset();
                gosha.Value=pet;
                return true;
            }
            at=default;
            return false;
        }

        internal void TrialStartDenFromDeck()
        {
            StageWorkForTrial(GoshaWork.Materials,5,6,1,1,5);
            usefulBeforeJunk=0;
        }

        internal bool TrialJunkState(GoshaJunkLocation state)
        {
            for(int i=0;i<goshaJunk.Count;i++)if(goshaJunk[i].Location==state)return true;
            return false;
        }

        internal void TrialDenAtEight()
        {
            usefulBeforeJunk=0;
            while(team.goshaDen.Count<8)team.goshaDen.Add((GoshaJunkKind)((team.goshaDen.Count+3)%6));
            Publish();
        }

        internal bool TrialDenSaved()=>TeamSave.Load(savePath,progression).goshaDen.SequenceEqual(team.goshaDen);
    }
}
#endif
