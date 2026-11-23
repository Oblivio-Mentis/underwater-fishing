using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Raid.Tests
{
    public sealed class RadialCatalogTests
    {
        [Test]
        public void CatalogHasEightSectorsAndStableRankIds()
        {
            Assert.That(SkillTreeDefinition.Branches.Count, Is.EqualTo(8));
            Assert.That(SkillTreeDefinition.All.Count, Is.EqualTo(248));
            Assert.That(SkillTreeDefinition.All.Select(node => node.Key).Distinct().Count(), Is.EqualTo(248));
            Assert.That(SkillTreeDefinition.All.Select(node => node.Id).Distinct().Count(), Is.EqualTo(248));
            foreach (var sector in SkillTreeDefinition.All.GroupBy(node => node.Branch))
            {
                Assert.That(sector.Count(), Is.EqualTo(31));
                Assert.That(sector.Count(node => node.Capstone), Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] {10, 5, 5, 5, 5, 1}, sector.GroupBy(node => node.Line).Select(line => line.Count()));
                Assert.That(sector.All(node => node.Stage >= 1 && node.Stage <= 5 && node.Cost.IsValid), Is.True);
            }

            var cable3 = SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.Cable, 3));
            Assert.That(cable3.Key, Is.EqualTo("harpoon.cable.03"));
            Assert.That(cable3.Magnitude, Is.EqualTo(36));
            Assert.That(SkillTreeDefinition.Get(SkillTreeDefinition.Id(SkillLine.Cable, 6)), Is.Null);
        }

        [Test]
        public void SuccessiveRanksRequireTheDirectParent()
        {
            var i = SkillTreeDefinition.Id(SkillLine.Cable, 1);
            var ii = SkillTreeDefinition.Id(SkillLine.Cable, 2);
            var iii = SkillTreeDefinition.Id(SkillLine.Cable, 3);
            var owned = new List<SkillId> { i };
            Assert.That(SkillTreeDefinition.IsAvailable(ii, owned), Is.True);
            Assert.That(SkillTreeDefinition.IsAvailable(iii, owned), Is.False);
            owned.Add(ii);
            Assert.That(SkillTreeDefinition.IsAvailable(iii, owned), Is.True);
            Assert.That(SkillTreeDefinition.Rank(owned, SkillLine.Cable), Is.EqualTo(2));
            Assert.That(SkillTreeDefinition.Value(owned, SkillLine.Cable), Is.EqualTo(22));
            Assert.That(SkillTreeDefinition.IsAvailable(ii, owned), Is.False);
        }

        [Test]
        public void CapstonesRequireAllSpecifiedMilestonesWithoutExhaustingTheSector()
        {
            foreach (var capstone in SkillTreeDefinition.All.Where(node => node.Capstone))
            {
                var prerequisites = SkillTreeDefinition.Dependencies(capstone.Id);
                Assert.That(prerequisites.Count, Is.GreaterThanOrEqualTo(3));
                var owned = new List<SkillId>();
                foreach (var id in prerequisites) AddPath(id, owned);
                Assert.That(owned.Count, Is.LessThan(30));
                Assert.That(SkillTreeDefinition.IsAvailable(capstone.Id, owned), Is.True);
                owned.Remove(prerequisites[0]);
                Assert.That(SkillTreeDefinition.IsAvailable(capstone.Id, owned), Is.False);
            }
        }

        static void AddPath(SkillId id, List<SkillId> owned)
        {
            if (owned.Contains(id)) return;
            foreach (var dependency in SkillTreeDefinition.Dependencies(id)) AddPath(dependency, owned);
            owned.Add(id);
        }
    }
}
