using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid
{
    public struct RackParcel
    {
        public int serial,owner;
        public string kind;
        public Vector3 position,velocity;
        public double sampledAt;
        public bool resting;
    }

    public sealed partial class RaidWorld
    {
        sealed class RackOrder
        {
            internal RackParcel parcel;
            internal bool missedCatch;
            internal float age,sendAt;
        }

        sealed class RackParcelView
        {
            public Transform Model;
            public double LaunchedAt;
            public float CatchAt=-1;
            public int Owner;
            public Vector3 CatchFrom, CatchPoint;
        }

        static Vector3 ParcelGravity => Physics.gravity*.57f;
        double ParcelClock => TimeManager.TicksToTime(TimeManager.Tick)+TimeManager.GetTickElapsedAsDouble();
        const float ParcelCatchDuration=.19f;

        readonly SyncList<RackParcel> vendingParcels=new();
        readonly List<RackOrder> orders=new();
        readonly SyncDictionary<string,int> issuedInventions=new();
        readonly Dictionary<string,int> countedInventions=new();
        readonly RaycastHit[] parcelHits=new RaycastHit[12];
        readonly Dictionary<int,RackParcelView> parcelModels=new();
        readonly List<int> obsoleteParcels=new();
        readonly TechSlot[] plannedRackPockets=new TechSlot[TechSlot.PocketCount];
        int vendingSequence;
        public int RackParcelCount => vendingParcels.Count;
        internal int VendorIssuedCount(string kind) => issuedInventions.TryGetValue(kind,out int count)?count:0;

        void RefreshVendorStock()
        {
            if(team==null)return;
            countedInventions.Clear();
            void Count(string kind,int count)
            {
                if(string.IsNullOrEmpty(kind) || count<=0)return;
                countedInventions.TryGetValue(kind,out int previous);
                countedInventions[kind]=previous+count;
            }
            // Disconnected crew keep their saved pockets. Publish only a per-product total.
            foreach(var player in team.players)
                foreach(var stack in player.pocketStacks)Count(stack.kind,stack.count);
            foreach(var rig in team.rigs)
                if(rig.mode!="spent")Count(rig.kind,1);
            foreach(var order in orders)Count(order.parcel.kind,1);
            foreach(var product in TechCatalog.Items)
            {
                countedInventions.TryGetValue(product.id,out int total);
                if(VendorIssuedCount(product.id)!=total)issuedInventions[product.id]=total;
            }
        }

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
        void RemoveOrder(int i)
        {
            orders.RemoveAt(i);
            vendingParcels.RemoveAt(i);
            RefreshVendorStock();
        }
        void CancelRackOrder(int owner)
        {
            for(int i=orders.Count-1;i>=0;i--)if(orders[i].parcel.owner==owner)RemoveOrder(i);
        }

        bool HasRoomForRackOrder(DiverMotor diver,TeamProgress state,string kind)
        {
            var tool=diver.GetComponent<HarpoonTool>();
            int limit=state.TechStackLimit(kind);
            if(!tool || tool.FindInventionPocket(kind,limit)<FirstCargoSlot)return false;
            int slots=tool.SlotCount-FirstCargoSlot;
            for(int pocket=0;pocket<slots;pocket++)
            {
                int slot=FirstCargoSlot+pocket;
                var item=tool.InventionInSlot(slot);
                if(item.count==0 && !tool.CanFitInvention(slot,kind,limit))item.count=-1;
                plannedRackPockets[pocket]=item;
            }

            // Account for orders from this diver before accepting one more click. A pending
            // stack occupies the same pockets as collected stock, even for mixed purchases.
            foreach(var parcel in vendingParcels)
            {
                if(parcel.owner!=diver.OwnerId)continue;
                int reserved=FindPlannedRackPocket(slots,parcel.kind,state.TechStackLimit(parcel.kind));
                if(reserved>=0)
                    plannedRackPockets[reserved]=new TechSlot{kind=parcel.kind,count=plannedRackPockets[reserved].count+1};
            }
            return FindPlannedRackPocket(slots,kind,limit)>=0;
        }

        int FindPlannedRackPocket(int slots,string kind,int limit)
        {
            int empty=-1;
            for(int pocket=0;pocket<slots;pocket++)
            {
                var stack=plannedRackPockets[pocket];
                if(stack.count>0 && stack.kind==kind && stack.count<limit)return pocket;
                if(stack.count==0 && empty<0)empty=pocket;
            }
            return empty;
        }

        void LaunchRackParcel(DiverMotor diver,string kind)
        {
            float lane=(vendingSequence%3-1)*.055f;
            var start=inventionRack.TransformPoint(new Vector3(-.12f+lane,-.55f,.34f));
            var target=RackHand(diver)+diver.transform.right*lane;
            float flight=Mathf.Max(.45f,tuning.dispenserFlightSeconds);
            var order=new RackOrder
            {
                parcel=new RackParcel
                {
                    serial=++vendingSequence,owner=diver.OwnerId,kind=kind,position=start,sampledAt=ParcelClock,
                    velocity=(target-start)/flight-ParcelGravity*(flight*.5f)
                }
            };
            orders.Add(order);vendingParcels.Add(order.parcel);
            RefreshVendorStock();
        }
        bool SettleOrder(int index,DiverMotor diver)
        {
            var order=orders[index];var profile=team.Player(ProgressKey(diver));
            if(profile==null)return false;
            string kind=order.parcel.kind;
            bool ok=FindStoragePocket(diver.GetComponent<HarpoonTool>(),profile,kind,out int slot,out var reason) &&
                team.TakeTechFromRack(profile,kind,slot-FirstCargoSlot,SaveTransaction,out reason);
            // The new pocket item replaces its reservation only after the save commits.
            if(ok)
            {
                if(!order.parcel.resting)RackParcelCaught(order.parcel.serial,diver.OwnerId,RackHand(diver));
                RemoveOrder(index);
            }
            InventoryReceipt(diver,profile,ok,reason);return ok;
        }
        static Vector3 RackHand(DiverMotor diver) => diver.transform.position+Vector3.up*.42f+
            diver.transform.forward*.43f-diver.transform.right*.13f;

        bool ReachesReceiver(Vector3 start,Vector3 end,DiverMotor diver)
        {
            Vector3 chest=RackHand(diver);
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
                var step=order.parcel.velocity*dt+ParcelGravity*(dt*dt*.5f);
                order.parcel.velocity+=ParcelGravity*dt;
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
                    if(nearest.normal.y>.55f){order.parcel.resting=true;order.parcel.velocity=Vector3.zero;}
                    else order.parcel.velocity=Vector3.ProjectOnPlane(order.parcel.velocity,nearest.normal)*.45f;
                }
                order.parcel.position=destination;
                if(!order.missedCatch && order.age>=tuning.dispenserFlightSeconds*.78f && !order.parcel.resting && ReachesReceiver(from,destination,diver))
                {
                    if(SettleOrder(i,diver))continue;
                    // A pocket may fill during flight. Let the case fall so its owner can make room and pick it up.
                    order.missedCatch=true;
                }
                if(order.parcel.resting || order.age>=order.sendAt)
                {
                    order.sendAt=order.age+.08f;
                    order.parcel.sampledAt=ParcelClock;
                    vendingParcels[i]=order.parcel;
                }
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
            if(!diver)return false;
            float best=float.PositiveInfinity;
            foreach(var parcel in vendingParcels)
            {
                if(parcel.owner!=diver.OwnerId || !parcel.resting || !AimAtParcel(diver,parcel.position,true))continue;
                float distance=(diver.transform.position-parcel.position).sqrMagnitude;
                if(distance>=best)continue;
                best=distance;
                id=parcel.serial;
            }
            return id!=0;
        }
        internal void CollectRackParcel(DiverMotor diver,int serial)
        {
            if(!InventoryRequest(diver,out _))return;
            int i=orders.FindIndex(o=>o.parcel.serial==serial && o.parcel.owner==diver.OwnerId && o.parcel.resting);
            if(i>=0 && AimAtParcel(diver,orders[i].parcel.position,false))SettleOrder(i,diver);
        }

        [ObserversRpc]
        void RackParcelCaught(int serial,int owner,Vector3 point)
        {
            if(!parcelModels.TryGetValue(serial,out var parcel))return;
            parcel.CatchAt=Time.unscaledTime;
            parcel.CatchFrom=parcel.Model.position;
            parcel.CatchPoint=point;
            parcel.Owner=owner;
        }

        void PresentRackParcels()
        {
            if(!IsClientInitialized)return;
            obsoleteParcels.Clear();
            obsoleteParcels.AddRange(parcelModels.Keys);
            foreach(var parcel in vendingParcels)
            {
                obsoleteParcels.Remove(parcel.serial);
                if(!parcelModels.TryGetValue(parcel.serial,out var graphic))
                {
                    var go=new GameObject("Выдача · "+parcel.kind+" №"+parcel.serial);
                    go.transform.SetParent(transform,false);
                    graphic=new RackParcelView{Model=go.transform,LaunchedAt=parcel.sampledAt};
                    parcelModels.Add(parcel.serial,graphic);
                    TechRigShell.BuildSaleSample(go,parcel.kind,.32f);
                    graphic.Model.position=parcel.position;
                    if(inventionRack)inventionRack.GetComponent<InventionVendorDisplay>()?.Dispense(parcel.kind);
                }
                if(graphic.CatchAt>=0)continue;
                float lead=parcel.resting?0:Mathf.Clamp((float)(ParcelClock-parcel.sampledAt),0,.13f);
                Vector3 display=parcel.position+parcel.velocity*lead+ParcelGravity*(lead*lead*.5f);
                float t=1-Mathf.Exp(-Time.unscaledDeltaTime*(parcel.resting?22:31));
                var model=graphic.Model;
                model.position=Vector3.Lerp(model.position,display,t);
                float elapsed=(float)(ParcelClock-graphic.LaunchedAt);
                if(parcel.resting)
                    model.rotation=Quaternion.Slerp(model.rotation,Quaternion.Euler(0,parcel.serial*37,0),t*.54f);
                else
                {
                    var tilt=Quaternion.Euler(-26*Mathf.Cos(elapsed*5.2f),parcel.serial*37+elapsed*134,18*Mathf.Sin(elapsed*5.6f));
                    model.rotation=Quaternion.Slerp(model.rotation,tilt,t);
                }
            }
            foreach(var id in obsoleteParcels)
            {
                var graphic=parcelModels[id];
                if(graphic.CatchAt>=0)
                {
                    float phase=(Time.unscaledTime-graphic.CatchAt)/ParcelCatchDuration;
                    if(phase<1)
                    {
                        Vector3 end=graphic.CatchPoint;
                        if(localDiver && localDiver.OwnerId==graphic.Owner && Camera.main is Camera camera)
                            end=camera.ViewportToWorldPoint(new Vector3(.43f,.28f,.54f));
                        float progress=Mathf.SmoothStep(0,1,phase);
                        graphic.Model.position=Vector3.Lerp(graphic.CatchFrom,end,progress)+Vector3.up*(.07f*Mathf.Sin(progress*Mathf.PI));
                        graphic.Model.localScale=Vector3.one*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.46f,1,phase)));
                        continue;
                    }
                }
                Destroy(graphic.Model.gameObject);
                parcelModels.Remove(id);
            }
            if(!UI.UiInputGate.Blocked && !UI.CheatPanel.PointerActive && Keyboard.current?.eKey.wasPressedThisFrame==true &&
                LookAtOwnParcel(localDiver,out int claim))localDiver.CmdCollectRackParcel(claim);
        }
        void ClearRackParcelViews()
        {
            foreach(var item in parcelModels.Values)if(item.Model)Destroy(item.Model.gameObject);
            parcelModels.Clear();
        }
    }
}
