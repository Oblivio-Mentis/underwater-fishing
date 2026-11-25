using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public enum SkillId { None }

    public sealed class SkillBranch
    {
        public int Index { get; }
        public string Name { get; }
        public string ShortName { get; }
        public float Angle => Index * 45f;

        internal SkillBranch(int index, string name, string shortName)
        {
            Index = index;
            Name = name;
            ShortName = shortName;
        }
    }

    public sealed class SkillNode
    {
        readonly SkillLineTuning tuning;

        public SkillId Id { get; }
        public SkillLine Line => tuning.line;
        public int Rank { get; }
        public int Branch => (int)Line / SkillTreeDefinition.LinesPerBranch;
        public int Stage { get; }
        public bool Capstone => SkillTreeDefinition.MaxRank(Line) == 1;
        public bool Team => tuning.team;
        public string LineName => tuning.title;
        public string Name { get; }
        public string Effect { get; }
        public int Magnitude => tuning.ranks[Rank - 1].magnitude;
        public string ValueText => SkillTreeDefinition.FormatValue(Line, Magnitude);
        public MaterialCost Cost { get; }
        public string Key { get; }
        internal IReadOnlyList<SkillId> Requirements { get; }

        internal SkillNode(SkillLineTuning tuning, int rank, int stage, MaterialCost cost, SkillId[] requirements)
        {
            this.tuning = tuning;
            Id = SkillTreeDefinition.Id(tuning.line, rank);
            Rank = rank;
            Stage = stage;
            Cost = cost;
            Name = Capstone ? tuning.title : $"{tuning.title} {SkillTreeDefinition.Roman(rank)}";
            string note = tuning.ranks[rank - 1].note;
            Effect = string.IsNullOrEmpty(note) ? tuning.effect : $"{tuning.effect}\n{note}";
            Key = FormattableString.Invariant($"{tuning.key}.{rank:00}");
            Requirements = Array.AsReadOnly(requirements);
        }
    }

    public static class SkillTreeDefinition
    {
        public const int BranchCount = 8, LinesPerBranch = 6, NodesPerBranch = 31;
        const int LineCount = BranchCount * LinesPerBranch, IdStride = 16;

        static readonly string[] Romans = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        public static IReadOnlyList<SkillBranch> Branches { get; } = Array.AsReadOnly(new SkillBranch[]
        {
            new(0, "Гарпун", "Гарпун"),
            new(1, "Водолазный костюм", "Костюм"),
            new(2, "Манёвренность", "Ласты"),
            new(3, "NEREID", "NEREID"),
            new(4, "Контейнер и логистика", "Логистика"),
            new(5, "Переработка", "Переработка"),
            new(6, "Кормление", "Кормление"),
            new(7, "Гоша", "Гоша")
        });

        static Catalog cached;
        static Catalog Data => cached ??= Load();
        public static IReadOnlyList<SkillNode> All => Data.Nodes;

        public static string Roman(int rank) => (uint)rank < Romans.Length ? Romans[rank] : "";

        public static int MaxRank(SkillLine line)
        {
            if ((uint)line >= LineCount) return 0;
            int slot = (int)line % LinesPerBranch;
            return slot == 0 ? 10 : slot == LinesPerBranch - 1 ? 1 : 5;
        }

        public static SkillId Id(SkillLine line, int rank) =>
            rank > 0 && rank <= MaxRank(line) ? (SkillId)((int)line * IdStride + rank) : SkillId.None;

        public static SkillNode Get(SkillId id)
        {
            int index = (int)id;
            return index > 0 && index < LineCount * IdStride ? Data.ById[index] : null;
        }

        public static IReadOnlyList<SkillId> Dependencies(SkillId id) => Get(id)?.Requirements ?? Array.Empty<SkillId>();

        public static bool IsAvailable(SkillId id, IReadOnlyCollection<SkillId> owned)
        {
            var node = Get(id);
            if (node == null || owned == null || Contains(owned, id)) return false;
            foreach (var requirement in node.Requirements)
                if (!Contains(owned, requirement)) return false;
            return true;
        }

        public static int Rank(IReadOnlyList<SkillId> owned, SkillLine line)
        {
            int maximum = MaxRank(line);
            if (owned == null || maximum == 0) return 0;
            int baseId = (int)line * IdStride, highest = 0;
            for (int i = 0; i < owned.Count; i++)
            {
                int rank = (int)owned[i] - baseId;
                if (rank <= highest || rank > maximum) continue;
                highest = rank;
                if (highest == maximum) break;
            }
            return highest;
        }

        public static int Value(IReadOnlyList<SkillId> owned, SkillLine line) =>
            Get(Id(line, Rank(owned, line)))?.Magnitude ?? 0;

        public static bool IsAbility(SkillLine line) => (uint)line < LineCount && Data.Lines[(int)line].unit == SkillUnit.Ability;

        public static string FormatValue(SkillLine line, int value)
        {
            if ((uint)line >= LineCount || value <= 0) return "—";
            return Data.Lines[(int)line].unit switch
            {
                SkillUnit.ReductionPercent => $"−{value}%",
                SkillUnit.Metres => $"+{value} м",
                SkillUnit.Seconds => $"{value} с",
                SkillUnit.ExtraSlots => $"+{value}",
                SkillUnit.RangeMetres => $"{value} м",
                SkillUnit.Ability => "ранг " + Roman(value),
                _ => $"+{value}%"
            };
        }

        static bool Contains(IReadOnlyCollection<SkillId> owned, SkillId id)
        {
            if (owned is ICollection<SkillId> collection) return collection.Contains(id);
            foreach (var item in owned)
                if (item == id) return true;
            return false;
        }

        sealed class Catalog
        {
            public readonly SkillLineTuning[] Lines = new SkillLineTuning[LineCount];
            public readonly SkillNode[] ById = new SkillNode[LineCount * IdStride];
            public IReadOnlyList<SkillNode> Nodes;
        }

        static Catalog Load()
        {
            var asset = Resources.Load<SkillCatalog>("RadialProgressionCatalog");
            if (asset == null || asset.Lines == null || asset.Lines.Length != LineCount)
                throw new InvalidOperationException("RAID-019 requires Resources/RadialProgressionCatalog with all 48 lines.");
            var catalog = new Catalog();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in asset.Lines)
            {
                if (row == null || (uint)row.line >= LineCount || catalog.Lines[(int)row.line] != null)
                    throw new InvalidOperationException("Invalid or duplicated RAID-019 perk line.");
                ValidateRow(row, keys);
                catalog.Lines[(int)row.line] = row;
            }

            var nodes = new List<SkillNode>(BranchCount * NodesPerBranch);
            foreach (var row in catalog.Lines)
            {
                int maxRank = MaxRank(row.line);
                for (int rank = 1; rank <= maxRank; rank++)
                {
                    int stage = maxRank == 10 ? (rank + 1) / 2 : maxRank == 1 ? 5 : rank;
                    var requirements = new List<SkillId>();
                    if (rank > 1) requirements.Add(Id(row.line, rank - 1));
                    foreach (var milestone in row.milestones ?? Array.Empty<SkillRequirement>())
                    {
                        if (milestone.atRank != rank) continue;
                        var requirement = Id(milestone.line, milestone.rank);
                        if (requirement == SkillId.None || requirements.Contains(requirement))
                            throw new InvalidOperationException($"Invalid milestone on {row.key}.{rank}.");
                        requirements.Add(requirement);
                    }
                    if (maxRank == 1 && requirements.Count < 3)
                        throw new InvalidOperationException($"Capstone {row.key} needs at least three milestones.");
                    var node = new SkillNode(row, rank, stage, Price(row, rank, stage), requirements.ToArray());
                    catalog.ById[(int)node.Id] = node;
                    nodes.Add(node);
                }
            }

            var state = new byte[catalog.ById.Length];
            foreach (var node in nodes) Visit(node, catalog.ById, state);
            catalog.Nodes = nodes.AsReadOnly();
            return catalog;
        }

        static void ValidateRow(SkillLineTuning row, HashSet<string> keys)
        {
            int maxRank = MaxRank(row.line);
            if (string.IsNullOrWhiteSpace(row.key) || !keys.Add(row.key) || string.IsNullOrWhiteSpace(row.title) ||
                string.IsNullOrWhiteSpace(row.effect) || row.ranks == null || row.ranks.Length != maxRank ||
                !row.firstCost.IsValid || row.firstCost.IsEmpty || !row.extraPerStage.IsValid ||
                row.growthPercent is < 1 or > 100 || row.stageGrowthPercent is < 0 or > 40 ||
                !Enum.IsDefined(typeof(SkillUnit), row.unit))
                throw new InvalidOperationException($"Invalid tuning for {row.line}.");

            for (int i = 0; i < row.ranks.Length; i++)
                if (row.ranks[i].magnitude <= 0 || (i > 0 && row.ranks[i].magnitude < row.ranks[i - 1].magnitude))
                    throw new InvalidOperationException($"Invalid magnitude at rank {i + 1} of {row.key}.");
            foreach (var milestone in row.milestones ?? Array.Empty<SkillRequirement>())
                if (milestone.atRank <= 0 || milestone.atRank > maxRank)
                    throw new InvalidOperationException($"Invalid milestone rank on {row.key}.");
        }

        static void Visit(SkillNode node, SkillNode[] lookup, byte[] state)
        {
            int id = (int)node.Id;
            if (state[id] == 2) return;
            if (state[id] == 1) throw new InvalidOperationException($"Cyclic prerequisites on {node.Key}.");
            state[id] = 1;
            foreach (var requirement in node.Requirements)
            {
                var other = lookup[(int)requirement];
                if (other == null || other.Branch != node.Branch || other.Team != node.Team)
                    throw new InvalidOperationException($"Prerequisites cross sector or ownership on {node.Key}.");
                Visit(other, lookup, state);
            }
            state[id] = 2;
        }

        static MaterialCost Price(SkillLineTuning row, int rank, int stage)
        {
            decimal factor = 1m + (stage - 1) * row.stageGrowthPercent * .01m;
            for (int i = 1; i < rank; i++) factor *= 1m + row.growthPercent * .01m;
            int Scale(int basis, int staged) => checked((int)Math.Ceiling(basis * factor) + staged * (stage - 1));
            var initial = row.firstCost;
            var extra = row.extraPerStage;
            return new MaterialCost(Scale(initial.biomass, extra.biomass), Scale(initial.chitin, extra.chitin),
                Scale(initial.scrap, extra.scrap), Scale(initial.electronics, extra.electronics));
        }
    }
}
