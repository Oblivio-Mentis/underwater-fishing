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
    }

    public sealed partial class TeamProgress
    {
        public List<PlayerProgress> players = new();
        public List<SkillId> teamSkills = new();
        public int teamRevision;
        public int legacyCargoCapacityBonus;
        public MaterialCost processingRemainder;
        public const int FoodScale = 100;
        public int offeringPoints;
        public int offering { get => offeringPoints / FoodScale; set => offeringPoints = checked(value * FoodScale); }

        public static string FoodText(int points) =>
            (points / (decimal)FoodScale).ToString("0.##", CultureInfo.GetCultureInfo("ru-RU"));

        public int RequiredFood(ProgressionTuning tuning) =>
            checked(Math.Max(1, tuning.foodRequired + (highestDepth - 1) * tuning.foodPerDepth) * FoodScale);

        public void MigrateV1(int previousFood, int capacityPerWinch)
        {
            if(schemaVersion!=1)return;
            legacyCargoCapacityBonus=checked(winchLevel*capacityPerWinch);
            offering=previousFood;
            schemaVersion=CurrentSchema;
        }

        public void MigrateV2(MaterialCost balance)
        {
            if(schemaVersion!=2)return;
            Treasury=Treasury.Add(balance);
            schemaVersion=CurrentSchema;
        }

        public bool HasTeamSkill(SkillId id) => teamSkills.Contains(id);
        int Bonus(SkillId id, ProgressionTuning tuning) =>
            HasTeamSkill(id) ? (tuning?.Skill(id) ?? SkillTreeDefinition.Get(id)).Magnitude : 0;

        public int FoodValue(CatchItem item, ProgressionTuning tuning)
        {
            int category = item.offering <= 2 ? Bonus(SkillId.N2,tuning) : Bonus(SkillId.N3,tuning);
            return checked(item.offering * (FoodScale + Bonus(SkillId.N1,tuning) + category + Bonus(SkillId.N4,tuning)));
        }

        public MaterialCost ProcessPreview(CatchItem item, ProgressionTuning tuning)
        {
            int general = 100 + Bonus(SkillId.P1,tuning) + Bonus(SkillId.P4,tuning);
            int living = general + Bonus(SkillId.P2,tuning), mineral=general+Bonus(SkillId.P3,tuning);
            return new MaterialCost(checked(item.biomass*living),checked(item.chitin*living),
                checked(item.scrap*mineral),checked(item.electronics*mineral));
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
            projection.tank=tank.ConvertAll(item=>item.PublicCopy());
            projection.cargo=cargo.ConvertAll(item=>item.PublicCopy());
            return projection;
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
            if(teamRevision<0 || legacyCargoCapacityBonus<0 || players==null || teamSkills==null ||
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
        }
    }
}
