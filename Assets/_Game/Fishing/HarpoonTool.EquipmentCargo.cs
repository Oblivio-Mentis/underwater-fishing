using System.Collections.Generic;

namespace Raid
{
    public sealed partial class HarpoonTool
    {
        internal bool TakeEquipmentCatch(CatchItem cargo,System.Func<bool> commit=null,bool allowHeavy=false)
        {
            if(!MayHandleItems || HandsFull || Motor.IsTrapped || TetherActive || HasBallLine || HookedFish ||
               AssistTarget || RaidWorld.Instance.IsHoldingGosha(Motor) || cargo.IsSalvage || !allowHeavy && cargo.cargoSize>1)return false;
            foreach(var fish in FishActor.ServerSchool)
            {
                if(!fish || fish.SpeciesId!=cargo.species || fish.depthTier!=cargo.depthTier)continue;
                // A pocket stores species and capture ID only. Preserve modified yield by leaving
                // special cargo in the robot hold until it can reach the aquarium.
                var plain=fish.ToCargo(cargo.id);
                if(plain.biomass!=cargo.biomass || plain.chitin!=cargo.chitin || plain.offering!=cargo.offering ||
                    plain.scrap!=cargo.scrap || plain.electronics!=cargo.electronics || plain.cargoSize!=cargo.cargoSize)continue;
                int slot=fish.RequiresPartner?1:FreePocket();
                if(slot<0 || fish.RequiresPartner && !allowHeavy || commit!=null && !commit())return false;
                var received=new PocketItem{species=fish.NetworkObject,id=cargo.id};
                if(fish.RequiresPartner)_liftedHeavy.Value=received;
                else SetPocket(slot,received);
                EquipSlot(slot);
                return true;
            }
            return false;
        }

        internal bool StowSelectedInRig(List<CatchItem> destination, int space, int requiredDepth = 0, string broodSpecies = null, bool breed = false)
        {
            if (!IsServerInitialized || !MayHandleItems || SelectedItem(out var id) != DroppedKind.Catch) return false;
            var fish = HoldingHeavyCatch ? HeavySpecies : FishInSlot(ActiveSlot);
            if (!fish || id <= 0 || fish.cargoSize > space || destination.Exists(c => c.id == id)) return false;
            if (breed && (fish.depthTier!=requiredDepth || fish.RequiresPartner || (!string.IsNullOrEmpty(broodSpecies) && fish.SpeciesId != broodSpecies))) return false;
            var cargo = RaidWorld.Instance.AttributeCatch(fish.ToCargo(id));
            if (HoldingHeavyCatch ? !RemoveHeavy() : !EmptyPocket(ActiveSlot, false)) return false;
            destination.Add(cargo);
            return true;
        }
    }
}
