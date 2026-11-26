namespace Raid
{
    internal enum VendorAction { Idle, Locked, Full, Ready, TeamLimit }

    public sealed partial class RaidWorld
    {
        internal bool AimingAtVendingMachine { get; private set; }
        internal const string VendorTeamLimitReason="Достигнут лимит команды";

        internal void ClearVendingAim()
        {
            if(!AimingAtVendingMachine)return;
            AimingAtVendingMachine=false;
            if(rackDisplay)rackDisplay.PresentAim(null,VendorAction.Idle);
        }

        // A hit on the cabinet also consumes the primary input between products.
        internal bool PointAtVendingMachine(DiverMotor diver,bool clicked)
        {
            if(!IsClientInitialized || !diver || !diver.IsOwner || !AimAtCabinet(diver,true,out var product))
            {
                ClearVendingAim();
                return false;
            }
            AimingAtVendingMachine=true;
            var action=VendingAction(diver,product);
            if(rackDisplay)rackDisplay.PresentAim(product,action);
            if(clicked && action==VendorAction.Ready)diver.CmdRackTake(product.id);
            return true;
        }

        VendorAction VendingAction(DiverMotor diver,TechItem product)
        {
            if(product==null || view==null || LocalProgress==null || TravelRemaining>0)return VendorAction.Idle;
            if(view.TechRank(product.id,"unlock")==0)return VendorAction.Locked;
            if(TechRig.HasTeamLimit(product.id) && VendorIssuedCount(product.id)>=TechRig.TotalLimit(view,product))return VendorAction.TeamLimit;
            return HasRoomForRackOrder(diver,view,product.id)?VendorAction.Ready:VendorAction.Full;
        }
    }
}
