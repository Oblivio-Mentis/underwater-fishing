using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public enum DroppedKind : byte { None, Harpoon, Catch, Ball, Salvage, Invention }

    public sealed partial class HarpoonTool
    {
        // -1 marks the default starting harpoon until the authoritative instance ID arrives.
        readonly SyncVar<long> _harpoonId = new(-1);
        readonly SyncVar<PocketItem> _liftedHeavy = new();
        public bool OwnsHarpoon => _harpoonId.Value != 0;
        public long HarpoonId => _harpoonId.Value;
        public FishActor LiftedHeavy => _liftedHeavy.Value.species == null ? null : _liftedHeavy.Value.species.GetComponent<FishActor>();
        FishActor HeavySpecies => LiftedHeavy ?? _fish;
        public long HeldCargoId => HoldingHeavyCatch?HeavyId:CatchIdInSlot(ActiveSlot);
        long HeavyId => _liftedHeavy.Value.id > 0 ? _liftedHeavy.Value.id : _fish.CargoId;

        static long NewToolId() => BitConverter.ToInt64(Guid.NewGuid().ToByteArray(), 0) & long.MaxValue;

        bool RemoveHeavy()
        {
            if (_liftedHeavy.Value.id == 0) return _fish != null && _fish.MarkTransported(this);
            _liftedHeavy.Value = default;
            return true;
        }

        public DroppedKind SelectedItem(out long id)
        {
            id = 0;
            if (Motor == null) return DroppedKind.None;
            if (RaidWorld.Instance?.IsHoldingGosha(Motor) == true) return DroppedKind.None;
            var carried=CarriedInvention;
            if(carried!=null){id=-(long)carried.serial;return DroppedKind.Invention;}
            var salvage = CarriedSalvage;
            if (salvage != null) { id = salvage.CargoId; return DroppedKind.Salvage; }
            if (HoldingHeavyCatch) { id = HeavyId; return DroppedKind.Catch; }
            if (RaidWorld.Instance?.GoshaBallInHands(Motor) == true) { id = 1; return DroppedKind.Ball; }
            if (ActiveSlot == 0 && HarpoonId > 0) { id = HarpoonId; return DroppedKind.Harpoon; }
            if (ActiveSlot >= 2 && (id = CatchIdInSlot(ActiveSlot)) > 0) return DroppedKind.Catch;
            if (InventionInSlot(ActiveSlot).count>0){id=ActiveSlot;return DroppedKind.Invention;}
            return DroppedKind.None;
        }

        bool MayHandleItems => RaidWorld.Instance != null && Motor != null && !Motor.IsHelpless
            && !RaidWorld.Instance.GoshaKnocked(Motor);

        public bool RequestDropSelected()
        {
            if (!IsClientInitialized || !IsOwner || !MayHandleItems || UI.UiInputGate.Blocked) return false;
            var kind = SelectedItem(out var id);
            if (kind == DroppedKind.None) return false;
            var invention=kind==DroppedKind.Invention ? CarriedInvention?.kind ?? InventionInSlot(ActiveSlot).kind : null;
            DropSelectedServer(kind,id,invention);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        void DropSelectedServer(DroppedKind expectedKind,long expectedId,string expectedInvention)
        {
            if (!MayHandleItems || SelectedItem(out long id) != expectedKind || id != expectedId) return;
            var world = RaidWorld.Instance;
            var at = expectedKind==DroppedKind.Salvage ? default : world.PlaceItem(Motor);
            bool placed = false;
            switch (expectedKind)
            {
                case DroppedKind.Invention:
                    world.DropOwnedInvention(this,id,expectedInvention,at);
                    return;
                case DroppedKind.Ball:
                    placed = world.PutDownHeldBall(Motor, at);
                    break;
                case DroppedKind.Salvage:
                    placed = CarriedSalvage?.DropAtFeet(Motor) == true;
                    break;
                case DroppedKind.Harpoon:
                    if (!world.CanRegisterDrop(id)) break;
                    UnequipGun();
                    _harpoonId.Value = 0;
                    world.RegisterDrop(DroppedItem.Gun(id, at, transform.eulerAngles.y));
                    EquipSlot(1);
                    placed = true;
                    break;
                case DroppedKind.Catch:
                    var fish = HoldingHeavyCatch ? HeavySpecies : FishInSlot(ActiveSlot);
                    if (fish == null || !world.CanRegisterDrop(id)) break;
                    var item = new DroppedItem { id = id, kind = DroppedKind.Catch, species = fish.NetworkObject,
                        position = at, yaw = transform.eulerAngles.y, handDropped = true };
                    if (HoldingHeavyCatch ? !RemoveHeavy() : !EmptyPocket(ActiveSlot, false)) break;
                    world.RegisterDrop(item);
                    placed = true;
                    break;
            }
            if (placed) Motor.ConfirmInteraction();
        }

        void UnequipGun()
        {
            ReleaseBallLineOnServer();
            ReleaseOutgoingFriend();
            DetachGrip();
            if (_fish != null) _fish.ReleaseHook(this);
            _reelHeld = false;
            FinishFishingLine();
        }

        internal bool CanEquipSalvage => MayHandleItems && CanSelect(1) && !RaidWorld.Instance.IsHoldingGosha(Motor);

        internal void EquipSalvageHands()
        {
            if (ActiveSlot == 0) UnequipGun();
            EquipSlot(1);
        }

        public bool RequestPickup(long id)
        {
            if (!IsClientInitialized || !IsOwner || HandsFull || !MayHandleItems || UI.UiInputGate.Blocked) return false;
            PickupServer(id);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        void PickupServer(long id)
        {
            var world = RaidWorld.Instance;
            if (!MayHandleItems || !world.TryAimDrop(Motor, out var item) || item.id != id) return;
            if(item.kind==DroppedKind.Invention)
            {
                int pocket=world.TakeLooseInvention(this,item);
                if(pocket>=2)EquipSlot(pocket);
                return;
            }
            if (item.kind == DroppedKind.Harpoon)
            {
                // Exchange in place when both divers already received a starting gun.
                long previous = HarpoonId;
                if (previous == id || previous > 0 && !world.CanRegisterDrop(previous)) return;
                UnequipGun();
                world.RemoveDrop(id);
                if (previous > 0) world.RegisterDrop(DroppedItem.Gun(previous, item.position, item.yaw));
                _harpoonId.Value = id;
                EquipSlot(0);
            }
            else
            {
                var fish = item.Fish;
                if (fish == null) return;
                int slot=fish.RequiresPartner?1:FreePocket();
                if(slot<0)return;
                world.RemoveDrop(id);
                var carried=new PocketItem {species=item.species,id=id};
                if(fish.RequiresPartner)_liftedHeavy.Value=carried;
                else SetPocket(slot,carried);
                EquipSlot(slot);
            }
            Motor.ConfirmInteraction();
        }

        internal bool CanPickUp(DroppedItem item)
        {
            if (!MayHandleItems || HandsFull || _fish != null || TetherActive || AssistTarget != null
                || HasBallLine || RaidWorld.Instance.IsHoldingGosha(Motor)) return false;
            if(item.kind==DroppedKind.Invention)
            {
                var world=RaidWorld.Instance;
                var state=IsServerInitialized?world.Team:world.View;
                return state!=null && FindInventionPocket(item.invention,state.TechStackLimit(item.invention))>=0;
            }
            return item.kind == DroppedKind.Harpoon || item.Fish != null && (item.Fish.RequiresPartner || FreePocket() >= 0);
        }

        public bool FishLoadTarget(out FishActor fish,out long cargoId)
        {
            fish=null;cargoId=0;
            if(!MayHandleItems || Motor.IsTrapped || CarriedSalvage || TetherActive || HasBallLine || AssistTarget || CarriedInvention!=null ||
               (!HoldingHeavyCatch && InventionInSlot(ActiveSlot).count>0) ||
               RaidWorld.Instance.IsHoldingGosha(Motor) || HasBallInSlot(ActiveSlot))return false;
            if(HoldingHeavyCatch){fish=HeavySpecies;cargoId=HeavyId;}
            else
            {
                if(HookedFish)return false;
                int slot=FirstOccupiedPocket();
                if(slot<0)return false;
                fish=FishInSlot(slot);cargoId=CatchIdInSlot(slot);
            }
            return fish && cargoId>0;
        }

        static int cargoInputFrame=-1;
        static HarpoonTool cargoInputTool;
        static bool cargoInputConsumed;

        internal bool ConsumeCargoPrimaryInput()
        {
            if(cargoInputFrame==Time.frameCount && cargoInputTool==this)return cargoInputConsumed;
            cargoInputFrame=Time.frameCount;cargoInputTool=this;cargoInputConsumed=false;
            if(!IsClientInitialized || !IsOwner || !MayHandleItems || UI.UiInputGate.Blocked)return false;
            if(CarriedInvention!=null || (!HandsFull && InventionInSlot(ActiveSlot).count>0))return false;
            if(!CargoDispatch.TryLoadAim(RaidWorld.Instance,Motor,out var direction))return false;
            cargoInputConsumed=true;
            _primaryConsumedUntilRelease=true;
            DispatchCargoLoad(direction);
            return true;
        }

        void DispatchCargoLoad(Vector3 direction)
        {
            var salvage=CarriedSalvage;
            if(salvage!=null){LoadSelectedServer(DroppedKind.Salvage,salvage.CargoId,direction);return;}
            if(FishLoadTarget(out _,out long id)){LoadSelectedServer(DroppedKind.Catch,id,direction);return;}
            UI.UiRoot.Instance?.Toast("Этот предмет нельзя поместить в клеть");
        }

        internal void RequestCargoBatch(Vector3 direction)
        {
            if(IsClientInitialized && IsOwner && MayHandleItems)LoadAllFishServer(direction);
        }

        [ServerRpc(RequireOwnership=true)]
        void LoadAllFishServer(Vector3 direction)
        {
            var world=RaidWorld.Instance;
            if(!world || !MayHandleItems || !CargoDispatch.ServerSeesOpening(world,Motor,direction))return;
            int moved=world.LoadAllCargo(this);
            if(moved>0)Motor.ConfirmInteraction();
            CargoLoadRejected(Owner,moved>0?$"В клеть загружено: {moved}":"Клеть заполнена или улова в карманах нет");
        }

        internal int LoadAllFish(TeamProgress team,ProgressionTuning tuning,System.Func<bool> persist)
        {
            var world=RaidWorld.Instance;
            var loaded=new System.Collections.Generic.List<(int slot,long id)>();
            int originalCount=team.cargo.Count;
            void Add(int slot,FishActor fish,long id)
            {
                if(fish && team.LoadCargo(world.AttributeCatch(fish.ToCargo(id)),tuning))loaded.Add((slot,id));
            }
            if(HoldingHeavyCatch)Add(1,HeavySpecies,HeavyId);
            for(int slot=2;slot<SlotCount;slot++)Add(slot,FishInSlot(slot),CatchIdInSlot(slot));
            if(loaded.Count==0)return 0;
            if(!persist())
            {
                team.cargo.RemoveRange(originalCount,team.cargo.Count-originalCount);
                return 0;
            }
            foreach(var item in loaded)
            {
                if(item.slot==1)RemoveHeavy();
                else EmptyPocket(item.slot,false);
                world.ReleaseLoadedCatchOwner(item.id);
            }
            return loaded.Count;
        }

        public bool RequestLoadSelected()
        {
            if(!IsClientInitialized || !IsOwner || !MayHandleItems || UI.UiInputGate.Blocked ||
               !CargoDispatch.TryLoadAim(RaidWorld.Instance,Motor,out var direction))return false;
            DispatchCargoLoad(direction);
            return true;
        }

        public bool RequestLoadFish()
        {
            var world=RaidWorld.Instance;
            if(!IsClientInitialized || !IsOwner || UI.UiInputGate.Blocked ||
               !FishLoadTarget(out var fish,out var id) || !CargoDispatch.TryLoadAim(world,Motor,out var direction))return false;
            var state=world.View;
            if(state==null || state.hoistPhase!=HoistPhase.Loading || (long)state.CargoUsed+fish.cargoSize>state.Capacity(world.progression))return false;
            LoadSelectedServer(DroppedKind.Catch,id,direction);
            return true;
        }

        [ServerRpc(RequireOwnership=true)]
        void LoadSelectedServer(DroppedKind kind,long id,Vector3 direction)
        {
            var world=RaidWorld.Instance;
            if(world==null || !MayHandleItems || kind!=DroppedKind.Catch && kind!=DroppedKind.Salvage)
            {CargoLoadRejected(Owner,"Предмет недоступен");return;}
            bool owned=kind==DroppedKind.Catch
                ?FishLoadTarget(out _,out long ownedId) && ownedId==id
                :CarriedSalvage!=null && CarriedSalvage.CargoId==id;
            if(!owned){CargoLoadRejected(Owner,"Предмет уже перемещён или не принадлежит вам");return;}
            if(!CargoDispatch.ServerSeesOpening(world,Motor,direction))
            {CargoLoadRejected(Owner,"Проём клети недоступен");return;}
            if(world.LoadSelectedCargo(this,kind,id))Motor.ConfirmInteraction();
            else CargoLoadRejected(Owner,"Клеть не принимает предмет: фаза, вместимость или дубликат");
        }

        [FishNet.Object.TargetRpc]
        void CargoLoadRejected(FishNet.Connection.NetworkConnection target,string reason) => UI.UiRoot.Instance?.Toast(reason);

        internal bool PlaceSelectedCatch(TeamProgress team,ProgressionTuning tuning,long id)
        {
            if(!FishLoadTarget(out var fish,out var carriedId) || carriedId!=id ||
               !team.LoadCargo(RaidWorld.Instance.AttributeCatch(fish.ToCargo(id)),tuning))return false;
            if(HoldingHeavyCatch?RemoveHeavy():EmptyPocket(FirstOccupiedPocket(),false))return true;
            team.cargo.RemoveAll(item=>item.id==id);
            return false;
        }

        internal bool AcceptGoshaCatch(FishActor fish)
        {
            int slot=FreePocket();
            if(!IsServerInitialized || HandsFull || slot<0 || Motor.IsHelpless || Motor.IsTrapped)return false;
            SetPocket(slot,new PocketItem { species=fish.NetworkObject,id=fish.CargoId });
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void StageLiftedForTrial(FishActor fish,long id) => _liftedHeavy.Value=new PocketItem{species=fish.NetworkObject,id=id};
        internal void StagePocketForTrial(FishActor fish,long id,int slot=2) => SetPocket(slot,new PocketItem {species=fish.NetworkObject,id=id});
#endif
        bool pocketTheftShieldUsed;
        internal void RearmPocketShield() => pocketTheftShieldUsed=false;
        public bool ResistPocketTheft()
        {
            if(pocketTheftShieldUsed || PocketCount==0 ||
               (RaidWorld.Instance?.SkillRank(Motor,SkillLine.Pockets)??0)<3)return false;
            pocketTheftShieldUsed=true;
            return true;
        }

        int FreePocket()
        {
            if(IsServerInitialized && RaidWorld.Instance!=null && !inventionPocketsRestored)return -1;
            for (int slot = 2; slot < SlotCount; slot++) if (!PocketOccupied(slot)) return slot;
            return -1;
        }
    }
}
