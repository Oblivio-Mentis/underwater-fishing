#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Raid
{
    public sealed partial class FishingTrial
    {
        static bool VerifyDropTransfers => Array.IndexOf(Environment.GetCommandLineArgs(), "-raid-verify-drops") >= 0;
        static readonly Vector3 HandoffWater = new(29,-5,7);

        bool WorldHas(long id) => Enumerable.Range(0,world.DroppedCount).Any(i=>world.DroppedAt(i).id==id);
        DroppedItem FindDrop(long id) => Enumerable.Range(0,world.DroppedCount).Select(world.DroppedAt).Single(d=>d.id==id);
        bool PeerHasWorldItem(long id) => ReadClient()?.droppedItems?.Count(d=>d.instance==id.ToString())==1;

        void AtSight(HarpoonTool viewer, Vector3 target, float distance)
        {
            viewer.Motor.Teleport(target - viewer.Motor.AimDirection * distance - Vector3.up*.6f);
            Physics.SyncTransforms();
        }

        IEnumerator SalvageHandoff()
        {
            Phase("Original salvage object Q and E on independent owners");
            var loot=FindObjectsByType<LootActor>().First(l=>l.IsVisible && l.CarrierOwnerId<0);
            long original=loot.itemId;
            AtSight(host,loot.transform.position,1.25f);
            yield return Pause(.35f);
            world.Command(WorldAction.Collect,0);
            yield return Wait(()=>loot.IsCarriedBy(host.Motor),4,"Host failed to pick up initial salvage.");
            Require(host.ActiveSlot==1 && host.SelectedItem(out _)==DroppedKind.Salvage,"Salvage did not select the hands.");
            host.Motor.Teleport(HandoffWater);
            yield return Pause(.4f);
            Require(host.RequestDropSelected(),"Host failed to request a salvage drop.");
            yield return Wait(()=>loot.CarrierOwnerId<0,4,"Salvage stayed equipped.");
            var target=loot.transform.position;
            host.Motor.Teleport(HandoffWater+Vector3.right*6);
            AtSight(helper,target,1.25f);
            yield return Pause(.75f);
            Send("key",.15f,"E");
            yield return Wait(()=>loot.IsCarriedBy(helper.Motor),6,"Remote E did not pick up salvage.");
            Require(loot.itemId==original,"Salvage changed its cargo ID on pickup.");
            Send("key",.15f,"Q");
            yield return Wait(()=>loot.CarrierOwnerId<0 && loot.IsVisible,6,"Remote Q did not put salvage down.");
            Check("Salvage "+original+" moved from host hands to the scene, to remote hands, and back via Q/E; source object and cargo ID persisted.");
            yield return RefillOnDeck();
        }

        IEnumerator GiveToRemote(long id)
        {
            Require(host.RequestDropSelected(),"Host owner refused Q for catch "+id);
            yield return Wait(()=>WorldHas(id) && PeerHasWorldItem(id),5,"Catch did not appear once on each peer.");
            var target=FindDrop(id).position;
            host.Motor.Teleport(HandoffWater+Vector3.right*7);
            AtSight(helper,target,1.4f);
            yield return Pause(.8f);
            Send("key",.15f,"E");
            yield return Wait(()=>helper.SelectedItem(out long picked)==DroppedKind.Catch && picked==id && !WorldHas(id),6,
                "Remote physical E did not pick up catch "+id);
            yield return Wait(()=>ReadClient()?.selectedInstance==id.ToString() && !PeerHasWorldItem(id),5,"Remote owner or world copy disagreed.");
            Send("pickup_drop",0,id.ToString());
            yield return Pause(.45f);
            Require(helper.CatchCount==1 && !WorldHas(id),"Stale duplicate pickup recreated the catch.");
            Send("key",.15f,"Q");
            yield return Wait(()=>WorldHas(id) && PeerHasWorldItem(id) && helper.CatchCount==0,6,"Remote Q did not put catch back.");
            helper.Motor.Teleport(HandoffWater+Vector3.right*12);
        }

        IEnumerator TakeBack(long id)
        {
            var target=FindDrop(id).position;
            AtSight(host,target,1.25f);
            yield return Pause(.35f);
            Require(host.RequestPickup(id),"Host pickup blocked.");
            yield return Wait(()=>host.SelectedItem(out long taken)==DroppedKind.Catch && taken==id && !WorldHas(id),5,
                "Host did not receive the original catch "+id);
        }

        IEnumerator CatchHandoffs(long heavy)
        {
            Phase("Remote handoff of captured two-hand and selected pocket catches");
            yield return RefillOnDeck();
            host.Motor.Teleport(HandoffWater);
            helper.Motor.Teleport(HandoffWater+Vector3.right*12);
            yield return Pause(.35f);
            yield return GiveToRemote(heavy);
            Require(host.CatchCount==3 && !host.HandsFull && !helper.HandsFull,"Heavy drop altered pockets or left hands locked.");
            long small=host.CatchIdInSlot(3);
            Require(host.SelectSlot(3),"Middle occupied pocket is not selectable.");
            yield return Wait(()=>host.ActiveSlot==3,3,"Server did not select the requested pocket.");
            host.Motor.Teleport(HandoffWater+Vector3.right*4);
            yield return Pause(.35f);
            yield return GiveToRemote(small);
            Require(host.CatchCount==2 && host.CatchIdInSlot(3)==0,"Q touched an unselected pocket.");
            yield return TakeBack(small);
            Require(host.CatchIdInSlot(3)==small,"Returned fish lost its original ID.");
            yield return TakeBack(heavy);
            Require(host.HandsFull && host.LiftedHeavy!=null && host.CatchCount==4 && !host.SelectSlot(0)
                && helper.CatchCount==0 && world.DroppedCount==0,"Restored heavy carry state was inconsistent.");
            Check("Physical E/Q on remote plus stale duplicate RPC transferred heavy catch "+heavy+" and selected pocket "+small+" back to the original owner. Three pockets, two occupied hands and zero world records restored for cargo deposit.");
        }
    }
}
#endif
