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
        // Public hotbar indices: 0 harpoon, 1 hands, 2+ real inventory pockets.
        const int ChargePocket=2,CagePocket=3,FishPocket=4;
        HarpoonTool Gear => local.GetComponent<HarpoonTool>();
        static string StackEntry(TechSlot slot)=>(slot.kind??"")+"/"+slot.count;
        static string InventoryDigest(PlayerProgress profile) =>
            profile.revision+"|"+string.Join("|",profile.pocketStacks.Select(StackEntry));
        string OwnerInventoryDigest() => world.LocalProgress.revision+"|"+
            string.Join("|",Enumerable.Range(2,HarpoonTool.MaximumSlots-2).Select(i=>StackEntry(Gear.InventionInSlot(i))));
        bool OtherOwnerPocketsIntact() => Enumerable.Range(2,Gear.PocketCapacity).All(i=>
            Host && i==2?Gear.InventionInSlot(i).kind=="trap" && Gear.InventionInSlot(i).count==1:Gear.InventionInSlot(i).count==0);

        IEnumerator RejectRackRequest(string kind,string description)
        {
            bool? accepted=null;
            string explanation=null;
            void Receipt(bool success,string message){accepted=success;explanation=message;}
            world.SkillResponded+=Receipt;
            local.CmdRackTake(kind);
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
            yield return Wait("charge blueprint for the independent ordinary pocket",()=>world.View.TechRank("charge","unlock")==1);
            var before=world.View.Treasury;
            int revision=world.View.teamRevision;
            observedParcelOwner=PeerId;
            if(report.seat==1)
            {
                yield return new WaitForSecondsRealtime(.4f);
                yield return RejectRackRequest("cage","research terminal cannot issue equipment remotely");
            }
            Mark("dispenser-ready-"+report.seat);
            if(Host)
            {
                yield return Wait("everyone saw the same pre-dispensing treasury",()=>All("dispenser-ready-"));
                // Each owner requests and catches a product locally from the same physical machine.
                local.Teleport(rackAt);
                yield return FaceStation(rack,rackAt,"host at the rack for its own independent catch");
                local.CmdRackTake("trap");
                yield return Wait("host's trap reached its own ordinary hotbar",()=>
                    Gear.InventionInSlot(2).kind=="trap" && Gear.InventionInSlot(2).count==1 && world.RackParcelCount==0);
                Assert(OtherOwnerPocketsIntact(),"The host received its own trap in its first ordinary pocket.");
                local.Teleport(screen.position-screen.forward*1.71f+screen.right*.64f-Vector3.up*.38f);
                var peer=DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==PeerId);
                var peerGear=peer.GetComponent<HarpoonTool>();
                Assert(peerGear.SlotCount==5 && peerGear.StoreBall() && peerGear.BallSlot==ChargePocket,
                    "The guest's actual ball occupies the first pocket of its three-pocket ordinary inventory.");
                var species=FishActor.ServerSchool.First(f=>f && !f.RequiresPartner);
                peerGear.StagePocketForTrial(species,world.Team.NewTechCatchId(),FishPocket);
                peer.Teleport(rackAt);
                Mark("first-rack");
                yield return Wait("guest observed refusal with cage, ball and fish filling its three pockets",()=>Has("guest-full"));
                peerGear.RemoveBall();
                Mark("free-charge-pocket");
                yield return StageMissedParcel(peer,rack);
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
                Assert(world.InventionRackTargeted(local),"the independent client's own reticle recognizes the cabinet for the E interaction");
                yield return Wait("guest's real ball and fish arrived in the two surrounding pockets",()=>
                    Gear.HasBallInSlot(ChargePocket) && Gear.CatchIdInSlot(FishPocket)>0 && Gear.FishInSlot(FishPocket));
                long fishId=Gear.CatchIdInSlot(FishPocket);
                int initial=world.View.TechStackLimit("cage");
                for(int count=1;count<=initial;count++)
                {
                    local.CmdRackTake("cage");
                    int expected=count;
                    yield return Wait("an actual cage is travelling before any private item is awarded",()=>
                        world.RackParcelInFlight("cage") && world.TryGetRackParcel(local.OwnerId,out _,out _,out bool resting) && !resting);
                    Assert(Gear.InventionInSlot(CagePocket).count==expected-1,"cage in flight has not been credited to the slot");
                    yield return Wait("ordinary cage pocket now holds "+expected,()=>Gear.InventionInSlot(CagePocket).count==expected);
                    yield return new WaitForSecondsRealtime(.35f);
                }
                if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)UiQaProbe.CapturePeerCamera(FileFor("dispenser.png"));
                int inventoryRevision=world.LocalProgress.revision;
                yield return RejectRackRequest("cage","dispenser rejected a full stack");
                Assert(Gear.InventionInSlot(CagePocket).count==initial && Gear.HasBallInSlot(ChargePocket) &&
                    Gear.CatchIdInSlot(FishPocket)==fishId && world.LocalProgress.revision==inventoryRevision,
                    "Full ordinary pockets refused another cage without losing the ball, the real fish or the filled stack.");
                Mark("guest-full");
                yield return Wait("ball was removed to make room for a different product",()=>Has("free-charge-pocket") && !Gear.HasBallInSlot(ChargePocket));
                yield return CollectMissedParcel();
                yield return new WaitForSecondsRealtime(.35f);
                Gear.SelectSlot(ChargePocket);
                yield return Wait("guest selected ordinary charge pocket",()=>Gear.ActiveSlot==ChargePocket);
                local.CmdTechEquipSlot(ChargePocket);
                yield return Wait("owner equipped the charge from the selected ordinary pocket",()=>Gear.InventionInSlot(ChargePocket).count==0 &&
                    world.View.rigs.Exists(r=>r.kind=="charge" && r.operatorId==local.OwnerId));
                var charge=world.View.rigs.Single(r=>r.kind=="charge");
                yield return new WaitForSecondsRealtime(.35f);
                local.CmdTechStore(charge.serial);
                yield return Wait("charge returned to its original private slot",()=>Gear.InventionInSlot(ChargePocket).count==1 &&
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
                local.CmdRackTake("cage");
                yield return Wait("same cage pocket now exceeds its old maximum",()=>Gear.InventionInSlot(CagePocket).count==initial+1);
                yield return new WaitForSecondsRealtime(.35f);
                Gear.SelectSlot(CagePocket);
                yield return Wait("guest selected ordinary cage pocket",()=>Gear.ActiveSlot==CagePocket);
                local.CmdTechEquipSlot(CagePocket);
                yield return Wait("selected cage pocket deployed a cage and consumed exactly one",()=>Gear.InventionInSlot(CagePocket).count==initial &&
                    Gear.InventionInSlot(ChargePocket).count==1 && world.View.rigs.Exists(r=>r.kind=="cage" && r.operatorId==local.OwnerId));
                Assert(world.View.Treasury.Equals(paid) && world.View.teamRevision==revision+1,
                    "free retrieval and equipment use after the upgrade preserve the wallet");
                Mark("private-stacks",OwnerInventoryDigest());
            }
            yield return Wait("original owner recorded its private stacks",()=>Has("private-stacks"));
            Assert(cageParcelIds.Count==TechCatalog.Find("cage").stackBase+1 && chargeParcelIds.Count==1 &&
                cageParcelIds.All(renderedParcelIds.Contains) && chargeParcelIds.All(renderedParcelIds.Contains) &&
                restingParcelIds.Count==1 && report.longestParcelTravel>.05f && world.RackParcelCount==0,
                "this client observed distinct moving models, one resting charge, no ejection for rejected requests and no parcel remaining");
            Assert(world.View.players.Count==0 && (report.seat==1 || OtherOwnerPocketsIntact()),
                "The guest's equipment did not enter another player's pockets or the public profile snapshot.");
        }
    }
}
#endif
