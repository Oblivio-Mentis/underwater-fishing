using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public struct RackParcel
    {
        public int serial,owner;
        public string kind;
        public Vector3 position;
        public bool resting;
    }

    public sealed partial class RaidWorld
    {
        sealed class RackOrder
        {
            internal RackParcel parcel;
            internal bool missedCatch;
            internal float age,sendAt;
            internal Vector3 velocity;
        }

        readonly SyncList<RackParcel> vendingParcels=new();
        readonly List<RackOrder> orders=new();
        readonly RaycastHit[] parcelHits=new RaycastHit[12];
        readonly Dictionary<int,Transform> parcelModels=new();
        readonly List<int> obsoleteParcels=new();
        int vendingSequence;
        public int RackParcelCount => vendingParcels.Count;
        public bool RackParcelInFlight(string kind)
        {
            foreach(var parcel in vendingParcels)if(parcel.kind==kind && !parcel.resting)return true;
            return false;
        }
        public bool TryGetRackParcel(int ownerId,out int serial,out Vector3 position,out bool resting)
        {
            foreach(var parcel in vendingParcels)if(parcel.owner==ownerId)
            {serial=parcel.serial;position=parcel.position;resting=parcel.resting;return true;}
            serial=0;position=Vector3.zero;resting=false;return false;
        }

        void ClearRackOrders(){orders.Clear();vendingParcels.Clear();vendingSequence=0;}
        void RemoveOrder(int i){orders.RemoveAt(i);vendingParcels.RemoveAt(i);}
        void CancelRackOrder(int owner)
        {
            for(int i=orders.Count-1;i>=0;i--)if(orders[i].parcel.owner==owner)RemoveOrder(i);
        }
        void LaunchRackParcel(DiverMotor diver,string kind)
        {
            foreach(var previous in orders)if(previous.parcel.owner==diver.OwnerId)
            {
                if(previous.parcel.resting)SkillReply(diver.Owner,false,"Заберите предмет с пола или дождитесь его возврата");
                return;
            }
            var start=inventionRack.TransformPoint(new Vector3(-.12f,-.55f,.34f));
            var target=diver.transform.position+Vector3.up*.79f-diver.transform.right*.18f;
            float flight=Mathf.Max(.45f,tuning.dispenserFlightSeconds);
            var order=new RackOrder
            {
                parcel=new RackParcel{serial=++vendingSequence,owner=diver.OwnerId,kind=kind,position=start},
                velocity=(target-start)/flight-Physics.gravity*(flight*.5f)
            };
            orders.Add(order);vendingParcels.Add(order.parcel);
        }
        bool SettleOrder(int index,DiverMotor diver)
        {
            var order=orders[index];var profile=team.Player(ProgressKey(diver));
            if(profile==null)return false;
            string kind=order.parcel.kind;
            bool ok=FindStoragePocket(diver.GetComponent<HarpoonTool>(),profile,kind,out int slot,out var reason) &&
                team.TakeTechFromRack(profile,kind,slot-FirstCargoSlot,SaveTransaction,out reason);
            if(ok)RemoveOrder(index);
            InventoryReceipt(diver,profile,ok,reason);return ok;
        }
        bool ReachesReceiver(Vector3 start,Vector3 end,DiverMotor diver)
        {
            Vector3 chest=diver.transform.position+Vector3.up*.65f;
            Vector3 step=end-start;
            float t=Mathf.Clamp01(Vector3.Dot(chest-start,step)/Mathf.Max(.00001f,step.sqrMagnitude));
            Vector3 near=start+t*step;
            var toHands=chest-near;
            if(toHands.sqrMagnitude>tuning.dispenserCatchRadius*tuning.dispenserCatchRadius)return false;
            float distance=toHands.magnitude;
            if(distance<.001f)return true;
            int n=Physics.RaycastNonAlloc(near,toHands/distance,parcelHits,distance,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)
            {
                var collider=parcelHits[i].collider;
                if(collider && collider.GetComponentInParent<DiverMotor>()!=diver)return false;
            }
            return true;
        }
        void TickRackOrders(float dt)
        {
            for(int i=orders.Count-1;i>=0;i--)
            {
                var order=orders[i];
                var diver=TechDiver(order.parcel.owner);order.age+=dt;
                if(!diver || order.age>tuning.dispenserReturnSeconds){RemoveOrder(i);continue;}
                if(order.parcel.resting)continue;
                var from=order.parcel.position;
                var step=order.velocity*dt+Physics.gravity*(dt*dt*.5f);
                order.velocity+=Physics.gravity*dt;
                float length=step.magnitude;
                Vector3 destination=from+step;
                bool blocked=false;RaycastHit nearest=default;
                if(length>.0001f)
                {
                    int n=Physics.SphereCastNonAlloc(from,tuning.dispenserParcelRadius,step/length,parcelHits,length,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                    for(int k=0;k<n;k++)
                    {
                        var hit=parcelHits[k];
                        if(hit.collider==rackContact || hit.collider.GetComponentInParent<DiverMotor>() || (blocked && hit.distance>=nearest.distance))continue;
                        nearest=hit;blocked=true;
                    }
                }
                if(blocked)
                {
                    destination=from+step.normalized*Mathf.Max(0,nearest.distance-.005f);
                    if(nearest.normal.y>.55f){order.parcel.resting=true;order.velocity=Vector3.zero;}
                    else order.velocity=Vector3.ProjectOnPlane(order.velocity,nearest.normal)*.45f;
                }
                order.parcel.position=destination;
                if(!order.missedCatch && order.age>=tuning.dispenserFlightSeconds*.78f && !order.parcel.resting && ReachesReceiver(from,destination,diver))
                {
                    if(SettleOrder(i,diver))continue;
                    // A pocket may fill during flight. Let the case fall so its owner can make room and pick it up.
                    order.missedCatch=true;
                }
                if(order.parcel.resting || order.age>=order.sendAt)
                {order.sendAt=order.age+.08f;vendingParcels[i]=order.parcel;}
            }
        }

        bool AimAtParcel(DiverMotor diver,Vector3 target,bool local)
        {
            if(!diver || !diver.IsInside || diver.IsHelpless)return false;
            var camera=local?diver.GetComponentInChildren<Camera>():null;
            var ray=camera?camera.ViewportPointToRay(new Vector3(.5f,.5f,0)):new Ray(diver.transform.position+Vector3.up*.6f,diver.AimDirection);
            Vector3 delta=target-ray.origin;float along=Vector3.Dot(delta,ray.direction);
            if(along<=0 || delta.magnitude>tuning.interactionRange || (delta-ray.direction*along).sqrMagnitude>.09f)return false;
            return !Physics.Linecast(ray.origin,target,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        }
        internal bool LookAtOwnParcel(DiverMotor diver,out int id)
        {
            id=0;
            return diver && TryGetRackParcel(diver.OwnerId,out id,out var position,out bool resting) && resting && AimAtParcel(diver,position,true);
        }
        internal void CollectRackParcel(DiverMotor diver,int serial)
        {
            if(!InventoryRequest(diver,out _))return;
            int i=orders.FindIndex(o=>o.parcel.serial==serial && o.parcel.owner==diver.OwnerId && o.parcel.resting);
            if(i>=0 && AimAtParcel(diver,orders[i].parcel.position,false))SettleOrder(i,diver);
        }

        void PresentRackParcels()
        {
            if(!IsClientInitialized)return;
            obsoleteParcels.Clear();obsoleteParcels.AddRange(parcelModels.Keys);
            foreach(var parcel in vendingParcels)
            {
                obsoleteParcels.Remove(parcel.serial);
                if(!parcelModels.TryGetValue(parcel.serial,out var model))
                {
                    var go=new GameObject("Выдача · "+parcel.kind+" №"+parcel.serial);go.transform.SetParent(transform,false);
                    model=go.transform;parcelModels.Add(parcel.serial,model);
                    TechRigShell.BuildSaleSample(go,parcel.kind,.32f);
                    model.position=parcel.position;
                    if(inventionRack)inventionRack.GetComponent<InventionVendorDisplay>()?.Dispense(parcel.kind);
                }
                model.position=Vector3.Lerp(model.position,parcel.position,1-Mathf.Exp(-Time.unscaledDeltaTime*36));
                if(!parcel.resting)model.Rotate(13*Time.deltaTime,78*Time.deltaTime,32*Time.deltaTime,Space.Self);
            }
            foreach(var id in obsoleteParcels){Destroy(parcelModels[id].gameObject);parcelModels.Remove(id);}
            if(!UI.UiInputGate.Blocked && !UI.CheatPanel.PointerActive && Keyboard.current?.eKey.wasPressedThisFrame==true &&
                LookAtOwnParcel(localDiver,out int claim))localDiver.CmdCollectRackParcel(claim);
        }
        void ClearRackParcelViews()
        {
            foreach(var item in parcelModels.Values)if(item)Destroy(item.gameObject);
            parcelModels.Clear();
        }
    }
}
