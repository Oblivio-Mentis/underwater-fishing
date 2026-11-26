using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        [ServerRpc] public void CmdDispatchTrapNetwork() => RaidWorld.Instance?.DispatchTrapNetwork(this);
    }

    public sealed partial class RaidWorld
    {
        internal static bool TrapAlarm(TeamProgress state,TechRig rig) =>
            rig.kind=="trap" && (rig.health<1 || rig.contents.Count>=Mathf.RoundToInt(TechCatalog.Find("trap").capacity*state.TechValue("trap","capacity",1)));

        sealed class DispatchBackup
        {
            readonly TechRig rig;
            readonly List<CatchItem> manifest;
            readonly string flight;
            readonly float timer;
            internal DispatchBackup(TechRig source)
            {
                rig=source;manifest=new List<CatchItem>(source.contents);flight=source.flight;timer=source.timer;
            }
            internal void Restore(){rig.contents=manifest;rig.flight=flight;rig.timer=timer;}
        }

        internal void DispatchTrapNetwork(DiverMotor diver)
        {
            if(!RigRequest(diver) || !ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree) || team.TechRank("trap","group")==0)return;
            var backups=new Dictionary<int,DispatchBackup>();
            int count=0;
            foreach(var station in team.rigs)
            {
                if(station.kind!="mail" || !IndustryPresent(station) || station.flight=="transit" ||
                    (station.position-CargoLoadingPosition).sqrMagnitude>IndustryRange(station)*IndustryRange(station))continue;
                int before=station.contents.Count;
                foreach(var trap in team.rigs)
                {
                    if(trap.kind!="trap" || trap.depth!=station.depth || !IndustryPresent(trap) || !CanExport(trap,false) ||
                        Vector3.Distance(trap.position,station.position)>IndustrialSettings.transferReach || IndustryFree(station)<=0)continue;
                    if(!backups.ContainsKey(trap.serial))backups.Add(trap.serial,new DispatchBackup(trap));
                    if(!backups.ContainsKey(station.serial))backups.Add(station.serial,new DispatchBackup(station));
                    TransferManifest(trap,station);
                }
                if(station.contents.Count>before && LaunchCapsule(station))count+=station.contents.Count-before;
            }
            if(count==0){SkillReply(diver.Owner,false,"Нет свободной пневмопочты рядом с полными ловушками");return;}
            if(!SaveTransaction())
            {
                foreach(var backup in backups.Values)backup.Restore();
                SkillReply(diver.Owner,false,"Отправка отменена: сохранение недоступно");return;
            }
            Publish();
            SkillReply(diver.Owner,true,"Промысловая сеть отправила улов: "+count);
        }

        internal static string EquipmentReadout(TechRig r,TeamProgress state)
        {
            string manifest="Груз "+r.contents.Count;
            switch(r.kind)
            {
                case "farm": return "Производители "+Mathf.Min(2,r.brood.Count)+"/2  ·  молодь "+Mathf.Max(0,r.brood.Count-2)+"  ·  собрано "+r.contents.Count;
                case "oxygen": return "O₂ "+Mathf.CeilToInt(r.air<0?TechCatalog.Find("oxygen").capacity*state.TechValue("oxygen","capacity",1):r.air);
                case "mail": return manifest+(r.flight=="transit"?(r.timer>0?"  ·  в пути "+Mathf.CeilToInt(r.timer)+" с":"  ·  ждёт места в резервуаре"):"");
                case "lure": return "Сигнал: "+(r.selection==1?"мелкая рыба":r.selection==2?"крупная рыба":"все виды");
                case "magnet": return manifest+"  ·  фильтр: "+(r.selection==1?"металл":r.selection==2?"контейнеры":"всё");
                case "trap":
                    int max=Mathf.RoundToInt(TechCatalog.Find("trap").capacity*state.TechValue("trap","capacity",1));
                    return manifest+"/"+max+(state.TechRank("trap","beacon")>0 && TrapAlarm(state,r)?"  ·  сигнал маяка":"");
                case "robot": return manifest+(r.flight=="piloted"?"  ·  ручное управление":r.flight=="collect"?"  ·  на задании":r.flight=="return"?"  ·  возвращается":"");
                case "cage": return r.baitId>=0?"Доброволец в креплении":r.contents.Count>0?"Пленник внутри":manifest;
                default: return manifest;
            }
        }
    }
}

