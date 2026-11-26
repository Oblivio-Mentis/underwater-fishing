#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed partial class TechCrewTrial
    {
        static string InventoryDigest(PlayerProgress profile) =>
            profile.revision+"|"+string.Join("|",profile.techSlots.Select(s=>(s.kind??"")+"/"+s.count));

        IEnumerator RejectRackRequest(string kind,int slot,string description)
        {
            bool? accepted=null;
            string explanation=null;
            void Receipt(bool success,string message){accepted=success;explanation=message;}
            world.SkillResponded+=Receipt;
            local.CmdRackTake(kind,slot);
            yield return Wait("server replied to rejected dispenser request",()=>accepted.HasValue);
            world.SkillResponded-=Receipt;
            Assert(accepted==false && !string.IsNullOrEmpty(explanation),description+" ("+explanation+")");
            yield return new WaitForSecondsRealtime(.4f);
        }

        IEnumerator FaceStation(Transform target,Vector3 expected,string phase)
        {
            yield return Wait(phase,()=>Vector3.Distance(local.transform.position,expected)<1 && local.IsInside);
            // Teleport operates on the server. Let the remote owner interpolate to the rack before aiming.
            yield return new WaitForSecondsRealtime(.5f);
            local.LookAtConsole(target.position);
            yield return new WaitForSecondsRealtime(.5f);
        }

        IEnumerator VerifyDispenser()
        {
            yield return Wait("physical dispenser attached to the ship",()=>world.InventionRackTransform);
            var rack=world.InventionRackTransform;
            var screen=station.screenAnchor;
            var rackAt=rack.position+rack.forward*1.15f-Vector3.up*.65f;
            var researchAt=screen.position-screen.forward*1.66f-screen.right*.72f-Vector3.up*.39f;
            if(report.seat==1)
            {
                yield return new WaitForSecondsRealtime(.55f);
                local.CmdTechBuy("charge","unlock",0,world.View.teamRevision);
            }
            yield return Wait("charge blueprint for the second independent slot",()=>world.View.TechRank("charge","unlock")==1);
            var before=world.View.Treasury;
            int revision=world.View.teamRevision;
            if(report.seat==1)
            {
                yield return new WaitForSecondsRealtime(.4f);
                yield return RejectRackRequest("cage",0,"research terminal cannot issue equipment remotely");
            }
            Mark("dispenser-ready-"+report.seat);
            if(Host)
            {
                yield return Wait("everyone saw the same pre-dispensing treasury",()=>All("dispenser-ready-"));
                var peer=DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==PeerId);
                peer.Teleport(rackAt);
                Mark("first-rack");
                yield return Wait("operator requested a stack upgrade",()=>Has("need-stack-research"));
                Assert(world.Team.Treasury.Equals(before) && world.Team.teamRevision==revision,
                    "neither filling a stack nor equipping and returning a charge debits the shared treasury");
                peer.Teleport(researchAt);
                Mark("stack-terminal");
                yield return Wait("stack bought at the terminal",()=>world.View.TechRank("cage","stack")==1);
                peer.Teleport(rackAt);
                Mark("upgraded-rack");
            }
            if(report.seat==1)
            {
                yield return Wait("first physical rack approach",()=>Has("first-rack"));
                yield return FaceStation(rack,rackAt,"operator in front of physical dispenser");
                int initial=world.View.TechStackLimit("cage");
                for(int count=1;count<=initial;count++)
                {
                    local.CmdRackTake("cage",0);
                    int expected=count;
                    yield return Wait("first technical slot now holds "+expected,()=>world.LocalProgress.techSlots[0].count==expected);
                    yield return new WaitForSecondsRealtime(.35f);
                }
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)UiQaProbe.CapturePeerCamera(FileFor("dispenser.png"));
                int inventoryRevision=world.LocalProgress.revision;
                yield return RejectRackRequest("cage",0,"dispenser rejected a full stack");
                Assert(world.LocalProgress.techSlots[0].count==initial && world.LocalProgress.revision==inventoryRevision,
                    "attempting to take another cage into the full selected slot leaves its stack unchanged");
                local.CmdRackTake("charge",1);
                yield return Wait("second slot receives a free charge",()=>world.LocalProgress.techSlots[1].kind=="charge" &&
                    world.LocalProgress.techSlots[1].count==1 && world.View.rigs.Count==0);
                yield return new WaitForSecondsRealtime(.35f);
                local.CmdTechEquipSlot(1);
                yield return Wait("owner selected the second slot and equipped that charge",()=>world.LocalProgress.techSlots[1].count==0 &&
                    world.View.rigs.Exists(r=>r.kind=="charge" && r.operatorId==local.OwnerId));
                var charge=world.View.rigs.Single(r=>r.kind=="charge");
                yield return new WaitForSecondsRealtime(.35f);
                local.CmdTechStore(charge.serial,1);
                yield return Wait("charge returned to its original private slot",()=>world.LocalProgress.techSlots[1].count==1 &&
                    world.View.rigs.Count==0);
                Assert(world.View.Treasury.Equals(before) && world.View.teamRevision==revision,
                    "taking, equipping, and storing the charge cost zero materials");
                Mark("need-stack-research");
                yield return Wait("second approach to research terminal",()=>Has("stack-terminal"));
                yield return FaceStation(screen,researchAt,"operator at research terminal for stack upgrade");
                local.CmdTechBuy("cage","stack",0,world.View.teamRevision);
                yield return Wait("stack upgraded on the server",()=>world.View.TechRank("cage","stack")==1);
                int upgraded=world.View.TechStackLimit("cage");
                Assert(upgraded>initial,"purchased stack rank raises the same slot's absolute limit");
                var paid=before.Subtract(TechCatalog.Find("cage").Upgrade("stack").NextCost(0));
                Assert(world.View.Treasury.Equals(paid) && world.View.teamRevision==revision+1,
                    "only the researched stack rank was charged");
                yield return Wait("physical dispenser after research",()=>Has("upgraded-rack"));
                yield return FaceStation(rack,rackAt,"operator returned to physical dispenser");
                local.CmdRackTake("cage",0);
                yield return Wait("same first slot now exceeds its old maximum",()=>world.LocalProgress.techSlots[0].count==initial+1);
                yield return new WaitForSecondsRealtime(.35f);
                local.CmdTechEquipSlot(0);
                yield return Wait("first slot deployed a cage and consumed exactly one",()=>world.LocalProgress.techSlots[0].count==initial &&
                    world.LocalProgress.techSlots[1].count==1 && world.View.rigs.Exists(r=>r.kind=="cage" && r.operatorId==local.OwnerId));
                Assert(world.View.Treasury.Equals(paid) && world.View.teamRevision==revision+1,
                    "free retrieval and equipment use after the upgrade preserve the wallet");
                Mark("private-stacks",InventoryDigest(world.LocalProgress));
            }
            yield return Wait("original owner recorded its private stacks",()=>Has("private-stacks"));
            Assert(world.View.players.Count==0 && (report.seat==1 || world.LocalProgress.techSlots.All(s=>s.count==0)),
                "private technical slots are hidden from other owners and the shared world snapshot");
        }
    }
}
#endif
