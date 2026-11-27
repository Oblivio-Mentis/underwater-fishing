using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    internal struct FishingFightView
    {
        internal FishActor fish;
        internal int predatorId, tugSequence, hookSequence;
        internal Vector3 position;
        internal float tug, tremble;
        internal FishBehaviour phase;
        internal bool tired, partner;

        internal bool Warning => fish?fish.SurgeWarning:phase==FishBehaviour.SurgeWindup;
        internal bool Surging => fish?fish.Surging:phase is FishBehaviour.Surge or FishBehaviour.LastEscape;
        internal bool SameTarget(FishingFightView other) => fish==other.fish && predatorId==other.predatorId;
    }

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

        internal bool TryFishingFight(out FishingFightView fight)
        {
            var fish=HookedFish;
            if(fish && fish.Mode>=FishMode.Hooked && fish.Mode<=FishMode.Tired)
            {
                fight=new FishingFightView {
                    fish=fish, position=fish.transform.position, phase=fish.Behaviour,
                    tug=fish.TugStrength, tugSequence=fish.TugSequence, hookSequence=fish.HookSequence,
                    tremble=fish.tuning.fightTrembleMultiplier, partner=fish.RequiresPartner,
                    tired=fish.Mode==FishMode.Tired || fish.Behaviour==FishBehaviour.Weakened
                };
                return true;
            }
            if(TryHookedDefense(out var predator))
            {
                fight=new FishingFightView {
                    predatorId=predator.id, position=predator.position, phase=predator.fightPhase,
                    tug=predator.effort, tugSequence=predator.fightTug, hookSequence=ShotSequence,
                    tremble=tuning.fightTrembleMultiplier, partner=predator.hookCount>1,
                    tired=predator.fightPhase==FishBehaviour.Weakened
                };
                return true;
            }
            fight=default;
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
