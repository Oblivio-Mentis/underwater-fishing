using FishNet.Object.Synchronizing;

namespace Raid
{
    public sealed partial class HarpoonTool
    {
        readonly SyncVar<int> defenseHook=new();
        public bool HasDefenseLine => defenseHook.Value>0;

        internal void AttachDefenseLine(int target)
        {
            defenseHook.Value=target;
        }

        internal void ClearDefenseLine()
        {
            defenseHook.Value=0;
            FinishFishingLine();
            ToolOf(_assistant.Value)?.DetachGrip();
        }

        void DetachDefenseLine()
        {
            if(HasDefenseLine)ThreatDirector.Instance?.ReleaseDefenseHook(this);
        }

        internal bool StoreDefenseCatch(FishActor template,long id)
        {
            if(!IsServerInitialized || !template || HandsFull)return false;
            int slot=FreePocket();
            if(slot<0)return false;
            SetPocket(slot,new PocketItem{species=template.NetworkObject,id=id});
            EquipSlot(slot);
            Motor.ConfirmInteraction();
            return true;
        }
    }
}
