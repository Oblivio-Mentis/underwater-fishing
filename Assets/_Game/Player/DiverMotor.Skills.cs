using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        Vector3 swimVelocity;
        float dashUntil,dashReadyAt,reserveSeconds,reserveReadyAt;
        bool usedReserve,rearmedAtDeck;
        const float DryRearmSeconds=2f;
        const float ReserveCooldownSeconds=30f;
        float dryDuration;
        HarpoonTool skillEquipment;
        float Personal(SkillLine line) => RaidWorld.Instance?.SkillFactor(this,line) ?? 0;

        public float AirReserveRemaining => _state.Value.ReserveAir;
        public bool AirReserveSpent => _state.Value.ReserveSpent;
        public int AirReserveCooldown => _state.Value.ReserveCooldown;

        [ServerRpc]
        void StartHydroDash()
        {
            if(IsHelpless || IsTrapped || IsInside || UsesIndoorWalking(transform.position) ||
                RaidWorld.Instance==null || Nereid.NereidAirlock.Active?.Contains(transform.position)==true ||
                Time.time<dashReadyAt || !RaidWorld.Instance.HasSkill(this,SkillLine.HydroDash))return;
            dashUntil=Time.time+RaidWorld.Instance.progression.hydroSeconds;
            dashReadyAt=Time.time+RaidWorld.Instance.progression.hydroCooldown;
        }

        void TickExpeditionSkills(bool inside,float dt)
        {
            if(!inside){dryDuration=0;rearmedAtDeck=false;return;}
            dashUntil=0;
            if(rearmedAtDeck && !usedReserve)return;
            dryDuration+=dt;
            if(dryDuration<DryRearmSeconds || Nereid.NereidAirlock.Active?.Contains(transform.position)==true)return;
            if(!rearmedAtDeck)
            {
                rearmedAtDeck=true;
                skillEquipment?.RearmPocketShield();
            }
            if(Time.time>=reserveReadyAt)
            {
                usedReserve=false;
                reserveSeconds=0;
            }
        }

        bool ReserveBreath(bool inside,float oxygen,float dt)
        {
            if(inside || oxygen>0)return false;
            if(!usedReserve)
            {
                if(Time.time<reserveReadyAt)return false;
                reserveSeconds=Mathf.Clamp(RaidWorld.Instance?.SkillValue(this,SkillLine.EmergencyAir)??0,0,9);
                if(reserveSeconds<=0)return false;
                usedReserve=true;
                reserveReadyAt=Time.time+ReserveCooldownSeconds;
            }
            if(reserveSeconds<=0)return false;
            reserveSeconds=Mathf.Max(0,reserveSeconds-dt);
            return true;
        }
    }
}
