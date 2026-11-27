using System;
using System.Collections.Generic;

namespace Raid
{
    [Serializable]
    public sealed class DefenseRecord
    {
        public int seed, budget, spent, lost, lossLimit, appetite;
        public float deficit, mitigation, integrity;
        public bool breached, resolutionComplete, provoked;
        public int remainingCarriers;
        public List<CatchItem> inTransit=new();
    }

    public sealed partial class TeamProgress
    {
        public int feedingCycle;
        public int cumulativeRaidLoss;
        public DefenseRecord ongoingDefense;

        internal int FeedingBase(ProgressionTuning tuning) =>
            checked(Math.Max(1,tuning.foodRequired+(highestDepth-1)*tuning.foodPerDepth)*FoodScale);

        internal int GrowingFood(ProgressionTuning tuning)
        {
            double initial = FeedingBase(tuning);
            double growth = Math.Max(1, tuning.foodGrowthPerCycle);
            double cycles = tuning.foodGrowthCurve is {length:>0}
                ? Math.Max(0,tuning.foodGrowthCurve.Evaluate(feedingCycle)) : feedingCycle;
            double cap = Math.Min(int.MaxValue,Math.Max(initial,(double)tuning.foodHardCap*FoodScale));
            double calculated = initial*Math.Pow(growth,cycles);
            return (int)Math.Ceiling(Math.Min(cap,double.IsNaN(calculated)?initial:calculated));
        }

        internal int HeldRaidStock=>ongoingDefense?.inTransit?.Count??0;

        internal void ValidateDefense(HashSet<long> stock, HashSet<long> delivered)
        {
            if(feedingCycle<0 || cumulativeRaidLoss<0)throw new InvalidOperationException("Invalid feeding cycle.");
            var raid=ongoingDefense;
            if(raid==null)return;
            if(raid.seed==0 || raid.budget<=0 || raid.spent<0 || raid.spent>raid.budget || raid.appetite<=0 ||
                raid.lossLimit<0 || raid.lost<0 || raid.lost>raid.lossLimit || !float.IsFinite(raid.deficit) ||
                raid.deficit<=0 || raid.deficit>1 || !float.IsFinite(raid.mitigation) || raid.mitigation<0 || raid.mitigation>1 ||
                !float.IsFinite(raid.integrity) || raid.integrity<0 ||
                raid.remainingCarriers<0 || raid.remainingCarriers>32 || raid.inTransit==null || raid.inTransit.Count+raid.lost>raid.lossLimit)
                throw new InvalidOperationException("Invalid raid checkpoint.");
            foreach(var item in raid.inTransit)
                if(item==null || !item.IsValid || item.dispatchCommitted || item.IsSalvage || !delivered.Contains(item.id) || !stock.Add(item.id))
                    throw new InvalidOperationException("Invalid stolen catch checkpoint.");
        }
    }
}
