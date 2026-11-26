using System;

namespace Raid
{
    public sealed partial class TeamProgress
    {
        // Resource IDs are part of the owner RPC contract. Food requests are in whole rations.
        public bool TryGrantResource(string id,int amount,Func<bool> persist,out int granted,out string reason)
        {
            switch(id)
            {
                case "biomass": return GrantBalance(ref biomass,"Биомасса",1,amount,persist,out granted,out reason);
                case "chitin": return GrantBalance(ref chitin,"Хитин",1,amount,persist,out granted,out reason);
                case "scrap": return GrantBalance(ref scrap,"Металлолом",1,amount,persist,out granted,out reason);
                case "electronics": return GrantBalance(ref electronics,"Электроника",1,amount,persist,out granted,out reason);
                case "offering": return GrantBalance(ref offeringPoints,"Пища",FoodScale,amount,persist,out granted,out reason);
                default: granted=0;reason="Неизвестный ресурс.";return false;
            }
        }

        bool GrantBalance(ref int balance,string label,int unit,int amount,Func<bool> persist,out int granted,out string reason)
        {
            granted=0;
            if(amount<=0){reason="Введите положительное целое количество.";return false;}
            int accepted=Math.Min(amount,(int.MaxValue-balance)/unit);
            if(accepted==0){reason="Достигнут предел запаса ресурса.";return false;}
            bool changesTreasury=unit==1;
            if(changesTreasury && teamRevision==int.MaxValue){reason="Достигнут предел ревизий казны.";return false;}
            int before=balance, revision=teamRevision;
            bool saved=false;
            try
            {
                balance+=accepted*unit;
                if(changesTreasury)teamRevision++;
                saved=persist();
            }
            finally
            {
                if(!saved){balance=before;teamRevision=revision;}
            }
            if(!saved){reason="Не удалось сохранить выдачу.";return false;}
            granted=accepted;
            reason=$"{label} +{accepted} из {amount}.";
            return true;
        }

        public int ReservoirRoom(ProgressionTuning tuning=null)
        {
            int reserved=hoistPhase==HoistPhase.Loading?0:CargoFishCount;
            return (int)Math.Max(0L,(long)ReservoirCapacity(tuning)-tank.Count-reserved);
        }

        public bool TryGrantFish(CatchItem specimen,int amount,ProgressionTuning tuning,Func<bool> persist,
            out int granted,out string reason)
        {
            granted=0;
            if(amount<=0){reason="Введите положительное целое количество.";return false;}
            if(specimen==null || !specimen.IsValid || specimen.IsSalvage || string.IsNullOrEmpty(specimen.species) ||
               specimen.destination!=CatchDestination.Hold)
            {reason="Вид рыбы недоступен.";return false;}
            int accepted=Math.Min(amount,ReservoirRoom(tuning));
            if(accepted==0){reason="В резервуаре нет свободных мест.";return false;}

            int previousTank=tank.Count, previousLedger=deliveredIds.Count;
            long sequence=techCatchSequence, lifetime=lifetimeCatch;
            bool saved=false;
            try
            {
                var fish=specimen.PublicCopy();
                for(int i=0;i<accepted;i++)
                {
                    if(!TryNewTechCatchId(out fish.id)){reason="Исчерпаны номера экземпляров улова.";return false;}
                    if(!Deposit(fish,tuning)){reason="Достигнут предел журнала улова.";return false;}
                }
                saved=persist();
            }
            finally
            {
                if(!saved)
                {
                    tank.RemoveRange(previousTank,tank.Count-previousTank);
                    deliveredIds.RemoveRange(previousLedger,deliveredIds.Count-previousLedger);
                    lifetimeCatch=lifetime;
                    techCatchSequence=sequence;
                    // Save validates the ledger and rebuilds the legacy-ID reservation cache.
                    // Restore those reservations for the original sequence after a failed write.
                    ValidateTechRigs();
                }
            }
            if(!saved){reason="Не удалось сохранить улов.";return false;}
            granted=accepted;
            reason=$"В резервуар добавлено {accepted} из {amount}.";
            return true;
        }
    }
}
