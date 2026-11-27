using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly Dictionary<long,CatchItem> defenseLoot=new();
        FishActor defenseSpecimen;

        internal bool ArchiveDefense()
        {
            if(!IsServerInitialized)return false;
            Publish();
            return SaveTransaction();
        }

        bool ExposedStock(CatchItem item) => !item.IsSalvage && !item.dispatchCommitted && !IsRouting(item.id);

        internal int DefenseLossLimit(float severity,int budget,DefenseTuning options)
        {
            int available=0;
            foreach(var specimen in team.tank)if(ExposedStock(specimen))available++;
            float fraction=Mathf.Lerp(options.minorLossFraction,
                Mathf.Min(progression.maximumStockLossFraction,options.severeLossFraction),Mathf.Clamp01(severity));
            float protection=1-Mathf.Clamp01(team.TeamFactor(SkillLine.CargoDefense));
            int cap=Mathf.Clamp(Mathf.Max(progression.maximumStockLossCount,Mathf.CeilToInt(budget*.36f)),0,options.lateLossCap);
            return Mathf.Clamp(Mathf.FloorToInt(available*fraction*protection),0,cap);
        }

        internal CatchItem TakeDefenseCatch()
        {
            var raid=team.ongoingDefense;
            if(!IsServerInitialized || raid==null || raid.lost+raid.inTransit.Count>=raid.lossLimit)return null;
            int index=team.tank.FindLastIndex(ExposedStock);
            if(index<0)return null;
            var item=team.tank[index];
            team.tank.RemoveAt(index);raid.inTransit.Add(item);
            if(SaveTransaction()){Publish();return item;}
            raid.inTransit.Remove(item);team.tank.Insert(index,item);
            return null;
        }

        internal void RecoverDefenseCatch(CatchItem item,bool publish)
        {
            if(item==null || team?.ongoingDefense?.inTransit.Remove(item)!=true)return;
            team.tank.Add(item);
            if(publish)Publish();
        }

        internal bool LoseDefenseCatch(CatchItem item)
        {
            var raid=team?.ongoingDefense;
            if(item==null || raid?.inTransit.Remove(item)!=true)return false;
            raid.lost++;team.cumulativeRaidLoss++;
            if(!SaveTransaction())
            {
                raid.lost--;team.cumulativeRaidLoss--;
                team.tank.Add(item);Publish();
                return false;
            }
            stockLossDisplay.Value++;Publish();
            return true;
        }

        internal bool TryGetDefenseLoot(long id,out CatchItem item)=>defenseLoot.TryGetValue(id,out item);
        internal void ForgetDefenseLoot(long id)=>defenseLoot.Remove(id);

        internal void CreateDefenseCatch(DefenseSpecies kind,Vector3 at,Vector3 facing,HarpoonTool cutter,Vector3? blastOrigin=null,float blastRadius=0)
        {
            if(!defenseSpecimen)
            {
                foreach(var fish in FishActor.ServerSchool)
                {
                    if(!fish || fish.RequiresPartner)continue;
                    defenseSpecimen=fish;
                    if(fish.SpeciesId=="Lanternfish")break;
                }
            }
            if(!defenseSpecimen)return;
            long id;
            do{id=BitConverter.ToInt64(Guid.NewGuid().ToByteArray(),0)&long.MaxValue;}
            while(id==0 || defenseLoot.ContainsKey(id) || team.deliveredIds.Contains(id));
            var rules=threats.defenseTuning;
            var item=new CatchItem(id,rules.salvageBiomass,0,rules.salvageOffering,1,CurrentDepth,
                species:DefenseCreatureAssets.Key(kind));
            defenseLoot.Add(id,item);
            if(cutter && cutter.StoreDefenseCatch(defenseSpecimen,id,kind))return;
            var corpse=new DroppedItem {
                id=id, kind=DroppedKind.Catch, species=defenseSpecimen.NetworkObject,
                position=at, yaw=facing.sqrMagnitude>.01f?Quaternion.LookRotation(facing).eulerAngles.y:0,
                dead=true, roll=82, defenseSpecies=(byte)((int)kind+1)
            };
            if(blastOrigin.HasValue && blastRadius>0)StartFishDrift(ref corpse,blastOrigin.Value,blastRadius);
            RegisterDrop(corpse);
        }
    }

    public static class DefenseCreatureAssets
    {
        public static string Key(DefenseSpecies kind)=>"Defense."+kind;
        public static bool ReadKey(string value,out DefenseSpecies kind)
        {
            kind=default;
            return value!=null && value.StartsWith("Defense.",StringComparison.Ordinal) && Enum.TryParse(value[8..],out kind);
        }
        public static string Name(DefenseSpecies kind)=>kind switch {
            DefenseSpecies.Rammer=>"Таранщик",DefenseSpecies.Remora=>"Прилипала",DefenseSpecies.Thief=>"Вор",
            DefenseSpecies.Abductor=>"Похититель",_=>"Сталкер"
        };
        public static GameObject Model(DefenseSpecies kind,Transform parent)
        {
            var model=Resources.Load<GameObject>("DefensePredators/"+kind);
            if(!model)model=Resources.Load<GameObject>("DefenseCreatures/"+kind);
            return model?UnityEngine.Object.Instantiate(model,parent,false):null;
        }

        internal static GameObject RemoraFragment(bool head,Transform parent)
        {
            var model=Resources.Load<GameObject>(head?"DefensePredators/RemoraInkHead":"DefensePredators/RemoraInkTail");
            return model?UnityEngine.Object.Instantiate(model,parent,false):null;
        }
    }
}
