using System;
using UnityEngine;

namespace Raid
{
    [Serializable]
    public struct TechSlot
    {
        public const int Slots=4;
        public string kind;
        public int count;
    }

    public sealed partial class TeamProgress
    {
        public int TechStackLimit(string kind)
        {
            var definition=TechCatalog.Find(kind);
            return definition==null?0:Mathf.Max(1,Mathf.RoundToInt(TechValue(kind,"stack",definition.stackBase)));
        }

        public int TechUnitCapacity(string kind)
        {
            var definition=TechCatalog.Find(kind);
            return definition==null?0:Mathf.Max(1,Mathf.RoundToInt(definition.capacity*TechValue(kind,"capacity",1)));
        }

        bool InventorySlot(PlayerProgress profile,int index,out string reason)
        {
            if(profile==null || Player(profile.key)!=profile){reason="Профиль не подключён";return false;}
            if(index<0 || index>=TechSlot.Slots || profile.techSlots?.Length!=TechSlot.Slots)
            {reason="Выберите один из четырёх слотов снаряжения";return false;}
            if(profile.revision==int.MaxValue){reason="Достигнут предел ревизий";return false;}
            reason=null;return true;
        }

        bool CanPack(PlayerProgress profile,int index,string kind,out string reason)
        {
            if(!InventorySlot(profile,index,out reason))return false;
            int limit=TechStackLimit(kind);
            if(limit==0 || TechRank(kind,"unlock")==0){reason="Сначала откройте чертёж";return false;}
            var slot=profile.techSlots[index];
            if(slot.count>0 && slot.kind!=kind){reason="Выбранный слот занят другим оборудованием";return false;}
            if(slot.count>=limit){reason="Стак заполнен";return false;}
            return true;
        }

        // Persist the private inventory change and any caller's staged world mutation as one team snapshot.
        static bool ChangeTechSlot(PlayerProgress profile,int index,string kind,int delta,Func<bool> persist)
        {
            var before=profile.techSlots[index];
            int revision=profile.revision;
            int count=checked(before.count+delta);
            profile.techSlots[index]=count==0?default:new TechSlot{kind=kind,count=count};
            profile.revision++;
            bool committed=false;
            try{return committed=persist==null || persist();}
            finally
            {
                if(!committed){profile.techSlots[index]=before;profile.revision=revision;}
            }
        }

        public bool TakeTechFromRack(PlayerProgress profile,string kind,int preferredSlot,Func<bool> persist,out string reason)
        {
            if(!CanPack(profile,preferredSlot,kind,out reason))return false;
            bool ok=ChangeTechSlot(profile,preferredSlot,kind,1,persist);
            reason=ok?"Оборудование в слоте":"Не удалось записать выдачу";
            return ok;
        }

        public bool DeployTechFromSlot(PlayerProgress profile,int index,Vector3 position,int owner,Func<bool> persist,out string reason)
        {
            if(!InventorySlot(profile,index,out reason))return false;
            var slot=profile.techSlots[index];
            var definition=TechCatalog.Find(slot.kind);
            if(slot.count<=0 || definition==null || TechRank(slot.kind,"unlock")==0){reason="Слот пуст";return false;}
            if(owner<0 || !FiniteRig(position) || rigs.Exists(r=>r.operatorId==owner || r.helperId==owner || r.baitId==owner))
            {reason="Сначала освободите руки";return false;}
            if(lastRigSerial==int.MaxValue || rigs.FindAll(r=>r.kind==slot.kind).Count>=TechRig.TotalLimit(this,definition))
            {reason="Достигнут лимит одновременно выставленного оборудования";return false;}
            int serial=lastRigSerial;
            var rig=AddRig(slot.kind,position,owner);
            bool committed=false;
            try
            {
                committed=ChangeTechSlot(profile,index,slot.kind,-1,persist);
                reason=committed?"Снаряжение в руках":"Не удалось записать развёртывание";
                return committed;
            }
            finally
            {
                if(!committed){rigs.Remove(rig);lastRigSerial=serial;}
            }
        }

        public bool StoreTechInSlot(PlayerProgress profile,int serial,int preferredSlot,int owner,Func<bool> persist,out string reason)
        {
            int row=rigs.FindIndex(r=>r.serial==serial);
            if(row<0 || owner<0){reason="Оборудование недоступно";return false;}
            var rig=rigs[row];
            if(!CanPack(profile,preferredSlot,rig.kind,out reason))return false;
            if(rig.kind=="oxygen" && rig.air>=0 && rig.air<TechUnitCapacity(rig.kind))
            {reason="Перед упаковкой заправьте кислородную станцию у корабля";return false;}
            bool ownCarry=rig.mode=="carried" && rig.operatorId==owner;
            bool parked=rig.mode=="placed" && rig.operatorId<0;
            if(!(ownCarry || parked) || rig.helperId>=0 || rig.baitId>=0 || rig.towDiver>=0 || rig.towCargo!=0 ||
                rig.targetSerial!=0 || !string.IsNullOrEmpty(rig.flight) || rig.health<1 ||
                rig.contents.Count>0 || rig.brood.Count>0 || rigs.Exists(other=>other.targetSerial==serial))
            {reason="Освободите, разгрузите и отремонтируйте оборудование";return false;}
            rigs.RemoveAt(row);
            bool committed=false;
            try
            {
                committed=ChangeTechSlot(profile,preferredSlot,rig.kind,1,persist);
                reason=committed?"Оборудование возвращено в слот":"Не удалось записать возврат";
                return committed;
            }
            finally{if(!committed)rigs.Insert(row,rig);}
        }

        void ValidateTechInventory()
        {
            foreach(var player in players)
            {
                if(player.techSlots?.Length!=TechSlot.Slots)throw new InvalidOperationException("Invalid equipment slot count.");
                foreach(var slot in player.techSlots)
                {
                    if(slot.count==0 && string.IsNullOrEmpty(slot.kind))continue;
                    if(slot.count<=0 || string.IsNullOrEmpty(slot.kind) || TechRank(slot.kind,"unlock")==0 || slot.count>TechStackLimit(slot.kind))
                        throw new InvalidOperationException("Invalid equipment stack.");
                }
            }
        }
    }
}
