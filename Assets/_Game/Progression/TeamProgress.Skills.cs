using System;
using System.Collections.Generic;
using System.Globalization;

namespace Raid
{
    [Serializable]
    public sealed class PlayerProgress
    {
        public string key;
        public List<SkillId> skills = new();
        public int revision;
        public TechSlot[] techSlots=new TechSlot[TechSlot.Slots];
    }

    public sealed partial class TeamProgress
    {
        public List<PlayerProgress> players = new();
        public List<SkillId> teamSkills = new();
        public int teamRevision;
        public MaterialCost processingRemainder;
        public const int FoodScale = 100;
        public int offeringPoints;
        public int offering { get => offeringPoints / FoodScale; set => offeringPoints = checked(value * FoodScale); }

        public static string FoodText(int points) =>
            (points / (decimal)FoodScale).ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));

        public int RequiredFood(ProgressionTuning tuning) =>
            checked(Math.Max(1, tuning.foodRequired + (highestDepth - 1) * tuning.foodPerDepth) * FoodScale);

        public int TeamRank(SkillLine line) => SkillTreeDefinition.Rank(teamSkills,line);
        public int TeamValue(SkillLine line) => SkillTreeDefinition.Value(teamSkills,line);
        public float TeamFactor(SkillLine line) => TeamValue(line)*.01f;
        public bool HasTeamLine(SkillLine line) => TeamRank(line)>0;

        public int FoodValue(CatchItem item, ProgressionTuning tuning)
        {
            int category=TeamValue(item.offering<=2 ? SkillLine.SmallRation : SkillLine.LargeRation);
            return checked(item.offering*(FoodScale+TeamValue(SkillLine.Nutrition)+category));
        }

        public MaterialCost ProcessPreview(CatchItem item, ProgressionTuning tuning)
        {
            int scale=100+TeamValue(SkillLine.Separator)+TeamValue(SkillLine.WasteFree);
            return new MaterialCost(checked(item.biomass*(scale+TeamValue(SkillLine.Biomass))),
                checked(item.chitin*(scale+TeamValue(SkillLine.Chitin))),
                checked(item.scrap*(scale+TeamValue(SkillLine.Scrap))),
                checked(item.electronics*(scale+TeamValue(SkillLine.Electronics))));
        }

        public MaterialCost Treasury
        {
            get => new(biomass,chitin,scrap,electronics);
            set { biomass=value.biomass; chitin=value.chitin; scrap=value.scrap; electronics=value.electronics; }
        }

        public PlayerProgress Player(string key) =>
            string.IsNullOrEmpty(key) ? null : players.Find(player => player.key == key);

        public PlayerProgress RegisterPlayer(string key)
        {
            var record=Player(key);
            if(record!=null)return record;
            record=new PlayerProgress { key=key };
            players.Add(record);
            return record;
        }

        public TeamProgress PublicView()
        {
            var projection=(TeamProgress)MemberwiseClone();
            projection.players=new List<PlayerProgress>();
            projection.rigs=rigs.ConvertAll(r=>r.PublicCopy());
            projection.tank=tank.ConvertAll(item=>item.PublicCopy());
            projection.cargo=cargo.ConvertAll(item=>item.PublicCopy());
            return projection;
        }

        public bool TryPurchase(PlayerProgress buyer, SkillId id, int treasuryRevision, int personalRevision,
            ProgressionTuning tuning, Func<bool> persist, out string reason)
        {
            var perk=SkillTreeDefinition.Get(id);
            if(buyer==null || Player(buyer.key)!=buyer){reason="Профиль не подключён";return false;}
            if(perk==null){reason="Неизвестный узел";return false;}
            var owned=perk.Team ? teamSkills : buyer.skills;
            if(owned.Contains(id)){reason="Ранг уже куплен";return false;}
            if(treasuryRevision!=teamRevision || personalRevision!=buyer.revision)
            {reason="Баланс обновился. Повторите покупку";return false;}
            if(!SkillTreeDefinition.IsAvailable(id,owned)){reason="Сначала купите предыдущий ранг";return false;}
            if(!Treasury.Covers(perk.Cost)){reason="Не хватает материалов";return false;}
            if(teamRevision==int.MaxValue || buyer.revision==int.MaxValue){reason="Достигнут предел ревизий";return false;}

            var before=Treasury;
            float oldHunger=hungerRemaining, oldHoist=hoistRemaining;
            float hungerCycle=HungerInterval(tuning), hoistCycle=HoistDuration(hoistPhase,tuning);
            owned.Add(id);
            Treasury=before.Subtract(perk.Cost);
            teamRevision++;
            if(!perk.Team)buyer.revision++;
            if(perk.Line==SkillLine.Satiety)hungerRemaining=oldHunger*HungerInterval(tuning)/hungerCycle;
            if(hoistPhase!=HoistPhase.Loading && (perk.Line is SkillLine.Winch or SkillLine.Industrial))
                hoistRemaining=oldHoist*HoistDuration(hoistPhase,tuning)/hoistCycle;
            if(!persist())
            {
                owned.Remove(id);
                Treasury=before;
                teamRevision=treasuryRevision;
                buyer.revision=personalRevision;
                hungerRemaining=oldHunger;
                hoistRemaining=oldHoist;
                reason="Ошибка записи. Материалы остались в казне";
                return false;
            }
            reason=perk.Name;
            return true;
        }

        public bool CreditProcessedCatch(long id, ProgressionTuning tuning, out MaterialCost yield)
        {
            yield=default;
            int index=tank.FindIndex(item=>item.id==id);
            if(index<0)return false;
            var fish=tank[index];
            var points=ProcessPreview(fish,tuning);
            int b=checked(points.biomass+processingRemainder.biomass);
            int c=checked(points.chitin+processingRemainder.chitin);
            int s=checked(points.scrap+processingRemainder.scrap);
            int e=checked(points.electronics+processingRemainder.electronics);
            var total=new MaterialCost(b/100,c/100,s/100,e/100);
            int revision=checked(teamRevision+1);
            Treasury=Treasury.Add(total);
            teamRevision=revision;
            processingRemainder=new MaterialCost(b%100,c%100,s%100,e%100);
            tank.RemoveAt(index);
            yield=total;
            return true;
        }

        void ValidateSkills()
        {
            if(teamRevision<0 || players==null || teamSkills==null ||
                !processingRemainder.IsValid || processingRemainder.biomass>=100 || processingRemainder.chitin>=100 ||
                processingRemainder.scrap>=100 || processingRemainder.electronics>=100)
                throw new InvalidOperationException("Invalid skill save.");
            ValidateNodes(teamSkills,true);
            var keys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var player in players)
            {
                if(player==null || string.IsNullOrEmpty(player.key) || !keys.Add(player.key) ||
                    player.revision<0 || player.skills==null)
                    throw new InvalidOperationException("Invalid player progress.");
                ValidateNodes(player.skills,false);
            }
        }

        static void ValidateNodes(List<SkillId> nodes, bool team)
        {
            var unique=new HashSet<SkillId>();
            foreach(var id in nodes)
            {
                var node=SkillTreeDefinition.Get(id);
                if(node==null || node.Team!=team || !unique.Add(id))
                    throw new InvalidOperationException("Invalid or duplicated skill.");
            }
            foreach(var id in unique)
                foreach(var prerequisite in SkillTreeDefinition.Dependencies(id))
                    if(!unique.Contains(prerequisite))throw new InvalidOperationException("Missing prerequisite in save.");
        }
    }
}
