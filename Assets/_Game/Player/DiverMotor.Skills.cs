using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        Vector3 swimVelocity;
        float dashUntil,dashReadyAt,reserveSeconds;
        bool usedReserve,rearmedAtDeck;
        const float DryRearmSeconds=2f;
        float dryDuration;
        HarpoonTool skillEquipment;
        float Personal(SkillId id) => RaidWorld.Instance?.SkillFactor(this,id) ?? 0;

        [ServerRpc]
        void StartHydroDash()
        {
            if(IsHelpless || IsTrapped || IsInside || UsesIndoorWalking(transform.position) ||
                RaidWorld.Instance==null || Nereid.NereidAirlock.Active?.Contains(transform.position)==true ||
                Time.time<dashReadyAt || !RaidWorld.Instance.HasSkill(this,SkillId.F4))return;
            dashUntil=Time.time+RaidWorld.Instance.progression.hydroSeconds;
            dashReadyAt=Time.time+RaidWorld.Instance.progression.hydroCooldown;
        }

        void TickExpeditionSkills(bool inside,float dt)
        {
            if(!inside){dryDuration=0;rearmedAtDeck=false;return;}
            dashUntil=0;
            if(rearmedAtDeck)return;
            dryDuration+=dt;
            if(dryDuration<DryRearmSeconds || Nereid.NereidAirlock.Active?.Contains(transform.position)==true)return;
            usedReserve=false;
            reserveSeconds=0;
            rearmedAtDeck=true;
            skillEquipment?.RearmPocketShield();
        }

        bool ReserveBreath(bool inside,float oxygen,float dt)
        {
            if(inside || oxygen>0)return false;
            if(!usedReserve)
            {
                usedReserve=true;
                reserveSeconds=RaidWorld.Instance?.SkillValue(this,SkillId.S4) ?? 0;
            }
            if(reserveSeconds<=0)return false;
            reserveSeconds=Mathf.Max(0,reserveSeconds-dt);
            return true;
        }
    }
}
