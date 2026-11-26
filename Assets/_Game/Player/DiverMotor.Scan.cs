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
            int rank=world.SkillRank(this,SkillLine.Scanner);
            float radius=Mathf.Clamp(world.SkillValue(this,SkillLine.Scanner),0,35);
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
                if(rank>=2)
                {
                    label=UI.UiText.CargoName(fish.ToCargo(fish.CargoId))+" · "+
                        (fish.RequiresPartner?"2 гарпуна":"1 гарпун")+" · "+fish.biomass+" Б / "+fish.chitin+" Х";
                }
                else label="Рыба";
            }
            if(rank>=3)
            {
                foreach(var loot in LootActor.VisibleStock)
                {
                    if(!loot.IsVisible || loot.CarrierOwnerId>=0)continue;
                    var at=loot.transform.position;
                    if(!ScanSight(eye,aim,at,radius,loot.transform,ref score))continue;
                    point=at;
                    label="Утиль · "+loot.scrap+" Л / "+loot.electronics+" Э";
                }
            }
            if(world.Team.TechRank("trap","beacon")>0)
                foreach(var rig in world.Team.rigs)
                {
                    if(rig.depth!=world.CurrentDepth || !RaidWorld.TrapAlarm(world.Team,rig) ||
                        !ScanSight(eye,aim,rig.position,radius,null,ref score))continue;
                    point=rig.position;
                    label="Маяк ловушки №"+rig.serial+" · "+RaidWorld.EquipmentReadout(rig,world.Team);
                }
            float duration=world.progression.scanSeconds*(rank>=5?1.5f:1f);
            if(label==null){LocalMarker(Owner,eye,"Сканер · сигнала нет",1.5f);return;}
            if(rank>=4 && Time.time>=beaconReadyAt)
            {
                beaconReadyAt=Time.time+2f*world.progression.scanCooldown;
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
               (subject==null || !hit.transform.IsChildOf(subject)))return false;
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
