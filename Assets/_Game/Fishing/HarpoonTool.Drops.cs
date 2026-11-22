using System;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public enum DroppedKind : byte { None, Harpoon, Catch, Ball, Salvage }

    public sealed partial class HarpoonTool
    {
        // -1 marks the default starting harpoon until the authoritative instance ID arrives.
        readonly SyncVar<long> _harpoonId = new(-1);
        readonly SyncVar<PocketItem> _liftedHeavy = new();
        public bool OwnsHarpoon => _harpoonId.Value != 0;
        public long HarpoonId => _harpoonId.Value;
        public FishActor LiftedHeavy => _liftedHeavy.Value.species == null ? null : _liftedHeavy.Value.species.GetComponent<FishActor>();
        FishActor HeavySpecies => LiftedHeavy ?? _fish;
        public long HeldCargoId => HandsFull?HeavyId:CatchIdInSlot(ActiveSlot);
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
            if (HandsFull) { id = HeavyId; return DroppedKind.Catch; }
            if (RaidWorld.Instance?.GoshaBallInHands(Motor) == true) { id = 1; return DroppedKind.Ball; }
            if (ActiveSlot == 0 && HarpoonId > 0) { id = HarpoonId; return DroppedKind.Harpoon; }
            if (ActiveSlot >= 2 && (id = CatchIdInSlot(ActiveSlot)) > 0) return DroppedKind.Catch;
            if (ActiveSlot == 1)
            {
                var salvage = LootActor.HeldBy(Motor);
                if (salvage != null) { id = salvage.ObjectId; return DroppedKind.Salvage; }
            }
            return DroppedKind.None;
        }

        bool MayHandleItems => RaidWorld.Instance != null && Motor != null && !Motor.IsHelpless
            && !RaidWorld.Instance.GoshaKnocked(Motor);

        public bool RequestDropSelected()
        {
            if (!IsClientInitialized || !IsOwner || !MayHandleItems || UI.UiInputGate.Blocked) return false;
            var kind = SelectedItem(out var id);
            if (kind == DroppedKind.None) return false;
            DropSelectedServer(kind, id);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        void DropSelectedServer(DroppedKind expectedKind, long expectedId)
        {
            if (!MayHandleItems || SelectedItem(out long id) != expectedKind || id != expectedId) return;
            var world = RaidWorld.Instance;
            if(Nereid.NereidVessel.Active!=null && (expectedKind==DroppedKind.Catch || expectedKind==DroppedKind.Salvage) &&
                CargoDispatch.CanLoad(world,Motor))
            {
                if(world.LoadSelectedCargo(this,expectedKind,id))Motor.ConfirmInteraction();
                return;
            }
            var at = world.PlaceItem(Motor);
            bool placed = false;
            switch (expectedKind)
            {
                case DroppedKind.Ball:
                    placed = world.PutDownHeldBall(Motor, at);
                    break;
                case DroppedKind.Salvage:
                    placed = LootActor.HeldBy(Motor)?.PutDown(Motor, at) == true;
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
                    var fish = HandsFull ? HeavySpecies : FishInSlot(ActiveSlot);
                    if (fish == null || !world.CanRegisterDrop(id)) break;
                    var item = new DroppedItem { id = id, kind = DroppedKind.Catch, species = fish.NetworkObject,
                        position = at, yaw = transform.eulerAngles.y };
                    if (HandsFull ? !RemoveHeavy() : !EmptyPocket(ActiveSlot, false)) break;
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

        internal bool EquipSalvageHands()
        {
            if (!MayHandleItems || !CanSelect(1) || RaidWorld.Instance.IsHoldingGosha(Motor)) return false;
            if (ActiveSlot == 0) UnequipGun();
            EquipSlot(1);
            return true;
        }

        public bool RequestPickup(long id)
        {
            if (!IsClientInitialized || !IsOwner || !MayHandleItems || UI.UiInputGate.Blocked) return false;
            PickupServer(id);
            return true;
        }

        [ServerRpc(RequireOwnership = true)]
        void PickupServer(long id)
        {
            var world = RaidWorld.Instance;
            if (!MayHandleItems || !world.TryAimDrop(Motor, out var item) || item.id != id) return;
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
                if(slot<0 || fish.RequiresPartner && HandsFull)return;
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
            return item.kind == DroppedKind.Harpoon || item.Fish != null && (item.Fish.RequiresPartner || FreePocket() >= 0);
        }

        internal bool PlaceSelectedCatch(TeamProgress team,ProgressionTuning tuning,long id)
        {
            bool heavy=HandsFull;
            var fish=heavy?HeavySpecies:FishInSlot(ActiveSlot);
            if(fish==null || !team.LoadCargo(RaidWorld.Instance.AttributeCatch(fish.ToCargo(id)),tuning))return false;
            if(heavy?RemoveHeavy():EmptyPocket(ActiveSlot,false))return true;
            team.cargo.RemoveAll(item=>item.id==id);
            return false;
        }

        internal bool AcceptGoshaCatch(FishActor fish)
        {
            int slot=FreePocket();
            if(!IsServerInitialized || slot<0 || Motor.IsHelpless || Motor.IsTrapped)return false;
            SetPocket(slot,new PocketItem { species=fish.NetworkObject,id=fish.CargoId });
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void StagePocketForTrial(FishActor fish,long id,int slot=2) => SetPocket(slot,new PocketItem {species=fish.NetworkObject,id=id});
#endif
        bool pocketTheftShieldUsed;
        internal void RearmPocketShield() => pocketTheftShieldUsed=false;
        public bool ResistPocketTheft()
        {
            if(pocketTheftShieldUsed || PocketCount==0 ||
               RaidWorld.Instance?.HasSkill(Motor,SkillId.C3)!=true)return false;
            pocketTheftShieldUsed=true;
            return true;
        }

        int FreePocket()
        {
            for (int slot = 2; slot < SlotCount; slot++) if (!PocketOccupied(slot)) return slot;
            return -1;
        }
    }
}
