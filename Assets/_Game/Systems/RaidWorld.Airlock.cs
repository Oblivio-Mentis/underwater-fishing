using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly SyncVar<AirlockSnapshot> airlock=new(AirlockSnapshot.Dry);
        public AirlockSnapshot Airlock => airlock.Value;

        void ResetAirlock()
        {
            airlock.UpdateSettings(new SyncTypeSettings(.06f));
            airlock.Value=AirlockSnapshot.Dry;
            if(NereidAirlock.Active!=null)NereidAirlock.Active.ApplyDoors(airlock.Value);
        }

        [ServerRpc(RequireOwnership=false)]
        public void RequestAirlockPad(byte padId,Vector3 look,NetworkConnection sender=null)
        {
            var chamber=NereidAirlock.Active;
            if(chamber==null || sender?.FirstObject==null || transitionRemaining>0)return;
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(diver==null || diver.IsHelpless || look.sqrMagnitude<.95f || look.sqrMagnitude>1.05f)return;
            if(!chamber.ValidatePress(diver,padId,look,out var pad))return;
            var state=airlock.Value;
            if(state.Request(pad.Sea)){airlock.Value=state;diver.ConfirmInteraction();}
        }

        void TickAirlock()
        {
            var chamber=NereidAirlock.Active;
            var state=airlock.Value;
            if(chamber==null || !state.Busy)return;
            float multiplier=1/Mathf.Max(.62f,1-TeamSkillFactor(SkillLine.Airlock));
            state.Step(Time.fixedDeltaTime*multiplier,chamber.DoorOccupied(false),chamber.DoorOccupied(true));
            airlock.Value=state;
            chamber.ApplyDoors(state);
        }
    }
}
