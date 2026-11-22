using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        float scanReadyAt, beaconReadyAt, markerEndsAt;
        public Vector3 MarkerPosition { get; private set; }
        public string ScanLabel { get; private set; }
        public bool MarkerActive => Time.time<markerEndsAt;

        [ServerRpc]
        void ScanWater()
        {
            var world=RaidWorld.Instance;
            if(world==null || IsHelpless || IsTrapped || IsInside || world.TravelRemaining>0 || Time.time<scanReadyAt)return;
            float radius=world.SkillValue(this,SkillId.R1);
            if(radius<=0)return;
            scanReadyAt=Time.time+world.progression.scanCooldown;
            var eye=transform.position+Vector3.up*.6f;
            var aim=AimDirection;
            float score=float.MaxValue;
            var point=Vector3.zero;
            string label=null;
            foreach(var fish in FishActor.ServerSchool)
            {
                if(fish==null || !fish.InHabitat || fish.Mode>=FishMode.Captured)continue;
                var at=fish.transform.position;
                if(!ScanSight(eye,aim,at,radius,fish.transform,ref score))continue;
                point=at;
                if(world.HasSkill(this,SkillId.R2))
                {
                    label=UI.UiText.CargoName(fish.ToCargo(fish.CargoId))+" · "+
                        (fish.RequiresPartner?"крупный улов":"малый улов")+" · "+fish.biomass+" Б / "+fish.chitin+" Х";
                }
                else label="Рыба";
            }
            float salvageRadius=world.SkillValue(this,SkillId.R3);
            if(salvageRadius>0)
            {
                foreach(var loot in LootActor.VisibleStock)
                {
                    if(!loot.IsVisible || loot.CarrierOwnerId>=0)continue;
                    var at=loot.transform.position;
                    if(!ScanSight(eye,aim,at,salvageRadius,loot.transform,ref score))continue;
                    point=at;
                    label="Утиль · "+loot.scrap+" Л / "+loot.electronics+" Э";
                }
            }
            float duration=world.progression.scanSeconds;
            if(label==null){LocalMarker(Owner,eye,"Сканер · сигнала нет",1.5f);return;}
            int beaconCooldown=world.SkillValue(this,SkillId.R4);
            if(beaconCooldown>0 && Time.time>=beaconReadyAt)
            {
                beaconReadyAt=Time.time+beaconCooldown;
                foreach(var diver in ServerDivers)
                    if(diver!=null && !diver.IsDead && diver.Owner!=null)
                        diver.LocalMarker(diver.Owner,point,"Метка экипажа · "+label,duration);
            }
            else LocalMarker(Owner,point,label,duration);
        }

        static bool ScanSight(Vector3 eye,Vector3 direction,Vector3 target,float radius,Transform subject,ref float best)
        {
            var offset=target-eye;
            float distance=offset.magnitude;
            if(distance<.02f || distance>radius)return false;
            float facing=Vector3.Dot(direction,offset/distance);
            float score=distance+(1-facing)*radius;
            if(facing<.77f || score>=best)return false;
            if(Physics.Raycast(eye,offset/distance,out var hit,distance,~0,QueryTriggerInteraction.Ignore) &&
               !hit.transform.IsChildOf(subject))return false;
            best=score;
            return true;
        }

        [TargetRpc]
        void LocalMarker(NetworkConnection recipient,Vector3 point,string text,float duration)
        {
            MarkerPosition=point;
            ScanLabel=text;
            markerEndsAt=Time.time+duration;
        }
    }
}
