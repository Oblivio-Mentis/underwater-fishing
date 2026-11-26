#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using System.Linq;
using Raid.Nereid;
using Raid.UI;
using UnityEngine;

namespace Raid
{
    internal sealed partial class FisheryTrial
    {
        long settlingCorpse;

        IEnumerator CrabHaulTrial()
        {
            Vector3 floor=default;
            bool found=false;
            for(int i=0;i<24;i++)
            {
                float angle=i*2.39f;
                var candidate=ground+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*i*.53f+Vector3.up*6;
                if(!Physics.Raycast(candidate,Vector3.down,out var bed,60,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                    bed.normal.y<.6f || Vector3.Distance(bed.point,world.CargoLoadingPosition)<12)continue;
                floor=bed.point;found=true;break;
            }
            Check(found,"A natural patch of seabed is available for the fish pile");
            var ids=new long[5];
            for(int i=0;i<ids.Length;i++)
            {
                ids[i]=world.Team.NewTechCatchId();
                world.RegisterDrop(new DroppedItem
                {
                    id=ids[i],kind=DroppedKind.Catch,species=sample.NetworkObject,handDropped=true,
                    position=floor+new Vector3((i-2)*.27f,.16f,Mathf.Sin(i*1.4f)*.28f)
                });
            }
            Vector3 aboveFloor=floor+Vector3.right*4+Vector3.up*2.2f;
            settlingCorpse=world.Team.NewTechCatchId();
            world.RegisterDrop(new DroppedItem
            {
                id=settlingCorpse,kind=DroppedKind.Catch,species=sample.NetworkObject,dead=true,roll=91,position=aboveFloor
            });
            host.Teleport(floor+Vector3.back*2.9f+Vector3.up*.65f);
            host.LookAtConsole(floor+Vector3.up*.14f);
            yield return Wait("crabs dig out next to the discarded pile",()=>world.TrialCrabHaulers,13);
            yield return new WaitForSeconds(1.03f);
            UiQaProbe.CapturePeerCamera(Path.Combine(output,"crabs-digging.png"));
            yield return Wait("reef crabs grab actual fish IDs",()=>Enumerable.Range(0,world.DroppedCount)
                .Select(world.DroppedAt).Any(item=>ids.Contains(item.id) && item.crabCarried),8);
            UiQaProbe.CapturePeerCamera(Path.Combine(output,"crabs-carrying.png"));
            yield return Wait("the whole discarded pile reaches a fissure",()=>!Enumerable.Range(0,world.DroppedCount)
                .Select(world.DroppedAt).Any(item=>ids.Contains(item.id)),18);
            Check(true,"All five fish were physically carried out in groups of two to four crabs");
            var corpse=Enumerable.Range(0,world.DroppedCount).Select(world.DroppedAt)
                .Single(item=>item.id==settlingCorpse);
            Check(corpse.position.y<aboveFloor.y-1,"A dead fish sinks and remains while a diver is close");
            host.Teleport(corpse.position+Vector3.back*8+Vector3.up*.25f);
            host.LookAtConsole(corpse.position);
            yield return Wait("the dead fish remains outlined at eight metres",()=>world.TrialFishOutline(settlingCorpse),4);
            UiQaProbe.CapturePeerCamera(Path.Combine(output,"distant-catch-outline.png"));
        }

        IEnumerator GoshaHatchTrial()
        {
            var receiver=GoshaReceiver.Active;
            if(peer)peer.Teleport(NereidVessel.Active.transform.TransformPoint(new Vector3(1,1,-9)));
            host.Teleport(receiver?receiver.TransferPosition+Vector3.up*1.1f:ground+Vector3.up*35);
            Check(receiver && receiver.Ready && receiver.Pipe && receiver.Pipe.IsConnected,"The authored hull hatch connects to the aquarium");
            world.StageWorkForTrial(GoshaWork.Fish,5,6,3,1,5);
            var lockBefore=world.Airlock.Phase;
            yield return Wait("Gosha starts inboard and enters his service tube",()=>world.Gosha.ServiceHatchActive,18);
            var pipe=receiver.ServiceRoute(NereidVessel.Active.transform.position);
            yield return Wait("Gosha emerges from his service tube directly into the sea",
                ()=>!world.Gosha.ServiceHatchActive && !NereidVessel.Active.ContainsSealed(world.Gosha.Position) &&
                    Vector3.Distance(world.Gosha.Position,pipe.WorldPoint(0))<2,10);
            Check(world.Airlock.Phase==lockBefore,"Gosha's route leaves the main airlock untouched");
            host.Teleport(pipe.WorldPoint(0)+receiver.transform.forward*3.2f+Vector3.up*.28f);
            host.LookAtConsole(Vector3.Lerp(pipe.WorldPoint(0),receiver.MouthPosition,.45f));
            yield return null;
            UiQaProbe.CapturePeerCamera(Path.Combine(output,"gosha-service-tube.png"));
            uint deliveriesBefore=world.Gosha.HatchDeliveries;
            FishAt(receiver.TransferPosition+Vector3.right*.36f);
            Check(world.StageHatchCatch(sample),"Staged one real fish in Gosha's jaws");
            long fish=sample.CargoId;
            yield return Wait("Gosha sends the same catch through the hull receiver",
                ()=>world.Gosha.HatchDeliveries>deliveriesBefore && world.Team.tank.Any(item=>item.id==fish),23);
            var loot=LootActor.VisibleStock.FirstOrDefault(item=>item && item.IsFreeOnSeabed && item.tier==world.CurrentDepth);
            Check(loot && world.StageBerthLoot(loot),"Staged a real material pickup with Gosha in open water");
            yield return Wait("Gosha enters his own hatch carrying the material",()=>world.Gosha.ServiceHatchActive,20);
            yield return Wait("Gosha leaves the collected material by his berth",
                ()=>loot.IsFreeOnSeabed && NereidVessel.Active.ContainsSealed(loot.transform.position) && world.Gosha.MaterialsDelivered>0,28);
            world.StopWorkForTrial();
            Check(true,"The material remained a real pickup inside the ship");
            yield return Wait("unattended dead fish despawn after both players leave",
                ()=>!Enumerable.Range(0,world.DroppedCount).Select(world.DroppedAt).Any(item=>item.id==settlingCorpse),18);
        }
    }

    public sealed partial class RaidWorld
    {
        internal bool TrialCrabHaulers => crabHauls.Count>0 && crabHauls.All(haul=>haul.Crew>=2 && haul.Crew<=4);
        internal bool StageHatchCatch(FishActor fish)
        {
            if(!fish.CaptureForGosha())return false;
            huntFish=null;
            carried.Add(new GoshaCatch{Fish=fish,Id=fish.CargoId});
            return true;
        }
        internal bool StageBerthLoot(LootActor item)
        {
            if(!item.TakeByGosha())return false;
            huntFish=null;
            work=GoshaWork.Materials;
            carried.Add(new GoshaCatch{Loot=item,Id=item.CargoId});
            return true;
        }
    }
}
#endif
