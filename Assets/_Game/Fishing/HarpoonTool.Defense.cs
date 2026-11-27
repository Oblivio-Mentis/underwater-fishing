using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public sealed partial class HarpoonTool
    {
        readonly SyncVar<int> defenseHook=new();
        public bool HasDefenseLine => defenseHook.Value>0;

        internal bool TryHookedDefense(out ThreatDirector.AttackerSnapshot target)
        {
            var director=ThreatDirector.Instance;
            if(director)return director.TryDefenseFight(defenseHook.Value,out target);
            target=default;
            return false;
        }

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void DefenseFixtureSelect(int slot) => SelectSlotServer(slot);
        internal void DefenseFixtureFire() => FireServer();
        float defenseFixtureWindUntil;
        bool DefenseFixtureWinding => Time.unscaledTime<defenseFixtureWindUntil;
        internal void DefenseFixtureWind(bool held)
        {
            defenseFixtureWindUntil=held?Time.unscaledTime+.16f:-1;
            SetControlsServer(held,false);
        }
#endif

        internal bool StoreDefenseCatch(FishActor template,long id,DefenseSpecies kind)
        {
            if(!IsServerInitialized || !template || HandsFull)return false;
            int slot=FreePocket();
            if(slot<0)return false;
            SetPocket(slot,new PocketItem{species=template.NetworkObject,id=id,defenseSpecies=(byte)((int)kind+1)});
            EquipSlot(slot);
            Motor.ConfirmInteraction();
            return true;
        }
    }
}
