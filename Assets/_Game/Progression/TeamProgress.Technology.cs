using System;
using System.Collections.Generic;

namespace Raid
{
    [Serializable]
    public sealed class TechPurchase
    {
        public string item, upgrade;
        public int rank;
    }

    public sealed partial class TeamProgress
    {
        public List<TechPurchase> techPurchases=new();

        public int TechRank(string item,string upgrade)
        {
            if(techPurchases!=null)
                foreach(var entry in techPurchases)
                    if(entry.item==item && entry.upgrade==upgrade)return entry.rank;
            return 0;
        }

        public float TechValue(string item,string upgrade,float fallback=0)
        {
            int rank=TechRank(item,upgrade);
            if(rank==0)return fallback;
            var node=TechCatalog.Find(item)?.Upgrade(upgrade);
            return node!=null && rank<=node.values.Length?node.values[rank-1]:fallback;
        }

        public string TechRequirement(TechItem item,TechUpgrade node)
        {
            if(item==null || node==null)return "Неизвестный чертёж";
            int rank=TechRank(item.id,node.id);
            if(rank>=node.values.Length)return "Приобретено";
            if(node.id!="unlock" && TechRank(item.id,"unlock")==0)return "Сначала откройте "+item.title;
            string stage=TechCatalog.GateReason(this,TechCatalog.Stage(item,node));
            if(stage!=null)return stage;
            return TechCatalog.RecipeReason(this,item,node);
        }

        public bool TryBuyTech(string item,string upgrade) =>
            TryBuyTech(item,upgrade,TechRank(item,upgrade),teamRevision,null,out _);

        // The expected rank/revision belongs to the purchase shown on the client's screen.
        public bool TryBuyTech(string item,string upgrade,int expectedRank,int revision,Func<bool> persist,out string reason)
        {
            int current=TechRank(item,upgrade);
            if(current!=expectedRank || revision!=teamRevision)
            {reason="Казна или ранг изменились. Обновите выбор";return false;}
            var definition=TechCatalog.Find(item);
            var node=definition?.Upgrade(upgrade);
            reason=TechRequirement(definition,node);
            if(reason!=null)return false;
            var before=Treasury;
            var cost=node.NextCost(current);
            if(!before.Covers(cost)){reason="Не хватает материалов";return false;}
            if(teamRevision==int.MaxValue){reason="Достигнут предел ревизий";return false;}
            var entry=techPurchases.Find(p=>p.item==item && p.upgrade==upgrade);
            if(entry==null)
            {
                entry=new TechPurchase{item=item,upgrade=upgrade};
                techPurchases.Add(entry);
            }
            entry.rank=current+1;
            Treasury=before.Subtract(cost);
            teamRevision++;
            bool committed=false;
            try { committed=persist==null || persist(); }
            finally
            {
                if(!committed)
                {
                    if(current==0)techPurchases.Remove(entry);
                    else entry.rank=current;
                    Treasury=before;
                    teamRevision=revision;
                }
            }
            reason=committed?node.title:"Не удалось сохранить покупку";
            return committed;
        }

        void ValidateTechnologies()
        {
            if(techPurchases==null)throw new InvalidOperationException("Missing technology purchases.");
            var unique=new HashSet<(string,string)>();
            foreach(var entry in techPurchases)
            {
                var node=entry==null?null:TechCatalog.Find(entry.item)?.Upgrade(entry.upgrade);
                if(node==null || !unique.Add((entry.item,entry.upgrade)) || entry.rank<1 || entry.rank>node.values.Length)
                    throw new InvalidOperationException("Invalid technology purchase.");
            }
            foreach(var entry in techPurchases)
                if(entry.upgrade!="unlock" && !unique.Contains((entry.item,"unlock")))
                    throw new InvalidOperationException("Equipment upgrade requires a blueprint.");
        }
    }
}
