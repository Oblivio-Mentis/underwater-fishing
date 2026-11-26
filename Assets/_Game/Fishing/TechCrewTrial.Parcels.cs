#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    internal sealed partial class TechCrewTrial
    {
        int observedParcelOwner=-1;
        readonly HashSet<int> cageParcelIds=new(), chargeParcelIds=new(), restingParcelIds=new();
        readonly HashSet<int> renderedParcelIds=new();
        readonly Dictionary<int,Vector3> firstParcelPosition=new();
        bool capturedFlight;
        float nextTraceAt;

        void LateUpdate()
        {
            ConfirmRackAimFromGuest();
            if(observedParcelOwner<0 || !world || !world.NetworkObject || !world.IsClientInitialized ||
                !world.TryGetRackParcel(observedParcelOwner,out int serial,out var pos,out bool resting))return;
            if(Host && Time.unscaledTime>=nextTraceAt)
            {
                nextTraceAt=Time.unscaledTime+.06f;
                string trace=world.QaDescribeRackShot(observedParcelOwner);
                if(trace!=null)File.AppendAllText(FileFor("parcel-trajectory.tsv"),trace+System.Environment.NewLine);
            }
            if(world.RackParcelInFlight("cage"))cageParcelIds.Add(serial);
            if(world.RackParcelInFlight("charge"))chargeParcelIds.Add(serial);
            if(!renderedParcelIds.Contains(serial))
            {
                string kind=cageParcelIds.Contains(serial)?"cage":chargeParcelIds.Contains(serial)?"charge":null;
                var sample=kind==null?null:world.transform.Find($"Выдача · {kind} №{serial}/Образец · {kind}");
                if(sample && sample.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh && filter.sharedMesh.vertexCount>0 &&
                    sample.TryGetComponent<MeshRenderer>(out var renderer) && renderer.enabled && renderer.gameObject.activeInHierarchy)
                    renderedParcelIds.Add(serial);
            }
            if(resting)restingParcelIds.Add(serial);
            if(!firstParcelPosition.TryGetValue(serial,out var start)){firstParcelPosition.Add(serial,pos);start=pos;}
            float distance=Vector3.Distance(start,pos);
            report.visibleCageParcels=cageParcelIds.Count;report.visibleChargeParcels=chargeParcelIds.Count;
            report.longestParcelTravel=Mathf.Max(report.longestParcelTravel,distance);
            if(!capturedFlight && report.seat==1 && cageParcelIds.Contains(serial) && !resting &&
                distance>.08f && SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                capturedFlight=true;
                UiQaProbe.CapturePeerCamera(FileFor("cage-ejection.png"));
            }
        }

        IEnumerator StageMissedParcel(DiverMotor peer,Transform rack)
        {
            yield return Wait("host sees the charge just fired from the cabinet",()=>Has("stage-charge-miss") &&
                world.RackParcelInFlight("charge") && world.TryGetRackParcel(PeerId,out _,out _,out bool resting) && !resting);
            // Moving away after launch deliberately makes the authored ballistic shot miss the receiver.
            peer.Teleport(world.CargoLoadingPosition+Vector3.up*1.3f);
            yield return Wait("missed physical case has landed",()=>world.TryGetRackParcel(PeerId,out _,out _,out bool resting) && resting);
            yield return Wait("independent receiver observed no award while the case lay on the deck",()=>Has("charge-on-deck"));
            world.TryGetRackParcel(PeerId,out int serial,out var point,out _);
            peer.Teleport(point+rack.forward*1.2f+Vector3.up*.48f);
            Mark("deck-pickup",serial.ToString());
        }

        IEnumerator CollectMissedParcel()
        {
            yield return AimAtRackProduct("charge");
            Mark("stage-charge-miss");
            Assert(UI.QaClickReticle(),"A left click on the charge cell requested the other visible product.");
            yield return Wait("replicated charge case landed without a receiver",()=>world.TryGetRackParcel(local.OwnerId,out _,out _,out bool resting) && resting);
            Assert(Gear.InventionInSlot(ChargePocket).count==0,"missed charge remains physical and has not entered the owner's slot");
            Mark("charge-on-deck");
            yield return Wait("owner approaches the resting case",()=>Has("deck-pickup") &&
                world.TryGetRackParcel(local.OwnerId,out _,out var at,out bool resting) && resting && local.IsInside &&
                Vector3.Distance(local.transform.position,at)<2.2f);
            yield return new WaitForSecondsRealtime(.5f);
            world.TryGetRackParcel(local.OwnerId,out int serial,out var point,out _);
            Assert(serial==int.Parse(File.ReadAllText(FileFor("deck-pickup"))),"the guest targets the same serialized case that missed");
            local.LookAtConsole(point);
            yield return new WaitForSecondsRealtime(.4f);
            if(SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
                UiQaProbe.CapturePeerCamera(FileFor("charge-on-deck.png"));
            local.CmdCollectRackParcel(serial);
            yield return Wait("E pickup transfers the resting charge exactly once",()=>Gear.InventionInSlot(ChargePocket).kind=="charge" &&
                Gear.InventionInSlot(ChargePocket).count==1 && world.RackParcelCount==0 && world.View.rigs.Count==0);
            yield return new WaitForSecondsRealtime(.35f);
            local.CmdCollectRackParcel(serial);
            yield return new WaitForSecondsRealtime(.35f);
            Assert(Gear.InventionInSlot(ChargePocket).count==1,"repeating E for the removed case cannot generate another charge");
        }

        IEnumerator VerifyInterruptedParcel(string firstProfile)
        {
            int sender=int.Parse(File.ReadAllText(FileFor("returned-owner")));
            observedParcelOwner=sender;
            var rack=world.InventionRackTransform;
            var approach=rack.position+rack.forward*1.15f-Vector3.up*.65f;
            Mark("cancel-ready-"+report.seat);
            if(Host)
            {
                yield return Wait("whole crew ready to observe a canceled ejection",()=>All("cancel-ready-"));
                DiverMotor.ServerDivers.Single(d=>d && d.OwnerId==sender).Teleport(approach);
                Mark("cancel-rack");
            }
            yield return Wait("previous receiver goes back to the cabinet",()=>Has("cancel-rack"));
            if(report.seat==1)
            {
                yield return FaceStation(rack,approach,"returned player faces cabinet for an interrupted catch");
                yield return AimAtRackProduct("charge");
                local.CmdRackTake("charge");
                yield return Wait("owner sees the returning character's charge leave the tray",()=>
                    world.RackParcelInFlight("charge") && world.TryGetRackParcel(sender,out _,out _,out bool still) && !still);
                world.TryGetRackParcel(sender,out int serial,out _,out _);
                Assert(OwnerInventoryDigest()==File.ReadAllText(FileFor("private-stacks")),
                    "launching a charge has not changed the ordinary inventory before contact");
                Mark("cancel-serial",serial.ToString());
                yield return new WaitForSecondsRealtime(.12f);
                var session=FindAnyObjectByType<RaidSession>();
                session.Disconnect();
                yield return Wait("receiver left during midair flight",()=>session.manager.IsOffline);
                Mark("cancel-left");
            }
            else
            {
                yield return Wait("world discarded the disconnected receiver's in-flight parcel",()=>Has("cancel-left") &&
                    !world.TryGetRackParcel(sender,out _,out _,out _) && world.RackParcelCount==0);
                int serial=int.Parse(File.ReadAllText(FileFor("cancel-serial")));
                Assert(chargeParcelIds.Contains(serial),"this observer saw the individual airborne charge before disconnect removed it");
                if(Host)
                {
                    Assert(InventoryDigest(world.Team.Player(firstProfile))==File.ReadAllText(FileFor("private-stacks")),
                        "server retained the private profile revision and stack counts without awarding the interrupted charge");
                    Mark("cancel-cleared");
                }
            }
            yield return Wait("host confirmed canceled parcel cleanup",()=>Has("cancel-cleared"));
            if(report.seat==1)
            {
                var session=FindAnyObjectByType<RaidSession>();
                session.JoinLocal();
                yield return Wait("disconnected receiver recovered its profile again",()=>
                {
                    world=RaidWorld.Instance;
                    local=UiRoot.Instance?.Context?.Player;
                    return local && local.IsOwner && world && world.NetworkObject && world.IsClientInitialized &&
                        world.View?.rigs.Count==report.fleetCount && world.LocalProgress!=null && Gear &&
                        OwnerInventoryDigest()==File.ReadAllText(FileFor("private-stacks"));
                },60);
                Assert(OwnerInventoryDigest()==File.ReadAllText(FileFor("private-stacks")) && world.RackParcelCount==0,
                    "second reconnect recovered no case, no new charge and no extra stored copy");
                Mark("cancel-restored");
            }
            yield return Wait("owner checked restored inventory after canceled ejection",()=>Has("cancel-restored"));
            Assert(FleetDigest(world.View)==report.fleetHash,"canceling issuance preserved the entire fleet, stock and shared treasury");
            Mark("cancel-done-"+report.seat);
            yield return Wait("every seat finished the canceled-flight check",()=>All("cancel-done-"));
        }
    }

    public sealed partial class RaidWorld
    {
        internal string QaDescribeRackShot(int owner)
        {
            var order=orders.Find(o=>o.parcel.owner==owner);
            var diver=TechDiver(owner);
            if(order==null || !diver)return null;
            var point=order.parcel.position;
            string RayTo(Vector3 target)
            {
                bool hit=Physics.Linecast(point,target,out var contact,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                string obstacle=hit?contact.collider.name+(contact.collider.GetComponentInParent<DiverMotor>()==diver?"(owner)":""):"clear";
                return Vector3.Distance(point,target).ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+"/"+obstacle;
            }
            var root=diver.transform.position;
            return string.Join("\t",order.parcel.serial,order.parcel.kind,order.age.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),
                order.parcel.resting,point.ToString("F3"),root.ToString("F3"),RayTo(root+Vector3.up*.53f),
                RayTo(root+Vector3.up*.65f),RayTo(root+Vector3.up*.79f-diver.transform.right*.18f));
        }
    }
}
#endif
