namespace Raid
{
    public sealed partial class HarpoonTool
    {
        public TechSlot InventionInSlot(int hotbarSlot) => Pocket(hotbarSlot).invention;

        // Saved indices are zero-based pockets. The hotbar reserves slots 0 and 1 for the gun and hands.
        internal void SyncInventionPockets(TechSlot[] saved)
        {
            if(!IsServerInitialized || saved==null || pockets.Count!=MaximumSlots-2)return;
            for(int i=0;i<pockets.Count;i++)
            {
                var item=pockets[i];
                if(item.id!=0 || item.ball || item.species!=null)continue;
                var stack=i<saved.Length?saved[i]:default;
                if(stack.count<=0 || string.IsNullOrEmpty(stack.kind))stack=default;
                if(item.invention.kind==stack.kind && item.invention.count==stack.count)continue;
                item.invention=stack;
                pockets[i]=item;
            }
            inventionPocketsRestored=true;
            SetPocketCapacity(requestedPocketCapacity);
        }

        internal bool CanFitInvention(int hotbarSlot,string kind,int limit)
        {
            if(hotbarSlot<2 || hotbarSlot>=SlotCount || hotbarSlot-2>=pockets.Count ||
                limit<=0 || string.IsNullOrEmpty(kind))return false;
            if(IsServerInitialized && RaidWorld.Instance!=null && !inventionPocketsRestored)return false;
            var item=Pocket(hotbarSlot);
            return !item.ball && item.id==0 && item.species==null &&
                (item.invention.count==0 || item.invention.kind==kind && item.invention.count<limit);
        }

        internal int FindInventionPocket(string kind,int limit)
        {
            int empty=-1;
            for(int slot=2;slot<SlotCount;slot++)
            {
                if(!CanFitInvention(slot,kind,limit))continue;
                if(InventionInSlot(slot).count>0)return slot;
                if(empty<0)empty=slot;
            }
            return empty;
        }

        internal TechRig CarriedInvention
        {
            get
            {
                var world=RaidWorld.Instance;
                if(!world || !NetworkObject || OwnerId<0)return null;
                var rigs=(IsServerInitialized?world.Team:world.View)?.rigs;
                if(rigs==null)return null;
                foreach(var rig in rigs)
                    if(rig.Carrying(OwnerId))return rig;
                return null;
            }
        }

        internal bool CanDrawPocketInvention => MayHandleItems && !Motor.IsTrapped && !HandsFull &&
            !TetherActive && !HasBallLine && AssistTarget==null && HookedFish==null &&
            !RaidWorld.Instance.IsHoldingGosha(Motor) && !RaidWorld.Instance.GoshaBallInHands(Motor);

        bool TryDrawPocketInvention()
        {
            if(!IsClientInitialized || !IsOwner || UI.UiInputGate.Blocked ||
                InventionInSlot(ActiveSlot).count<=0 || !CanDrawPocketInvention)return false;
            _primaryConsumedUntilRelease=true;
            Motor.CmdTechEquipSlot(ActiveSlot);
            return true;
        }

        internal bool CanStowCarriedInvention(TechRig rig,out string reason)
        {
            var world=RaidWorld.Instance;
            var team=world?world.View:null;
            reason=null;
            if(team==null || rig==null || rig.mode!="carried" || rig.operatorId!=OwnerId ||
                !MayHandleItems || Motor.IsTrapped)return false;
            if(!team.CanPackInvention(rig,OwnerId,out reason))return false;
            if(FindInventionPocket(rig.kind,team.TechStackLimit(rig.kind))>=0)return true;
            reason="Для установки нет свободного места в карманах";
            return false;
        }

        void TryStowCarriedInvention(TechRig rig)
        {
            if(!CanStowCarriedInvention(rig,out var reason))
            {
                if(!string.IsNullOrEmpty(reason))UI.UiRoot.Instance?.Toast(reason);
                return;
            }
            Motor.CmdTechStore(rig.serial);
        }
    }
}
