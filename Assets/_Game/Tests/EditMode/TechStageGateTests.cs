using NUnit.Framework;

namespace Raid.Tests
{
    public sealed class TechStageGateTests
    {
        [Test]
        public void CageSensorBaitAndAutonomousHarvestRequireDistinctStagesAndKit()
        {
            var team=new TeamProgress { Treasury=new MaterialCost(9000,9000,9000,9000) };
            Assert.That(team.TryBuyTech("trap","unlock"),Is.True);
            Assert.That(team.TryBuyTech("cage","unlock"),Is.True);
            Assert.That(team.TryBuyTech("net","unlock"),Is.True);
            Assert.That(team.TryBuyTech("cage","select"),Is.False);

            team.teamSkills.Add(SkillTreeDefinition.Id(SkillLine.Hull,1));
            Assert.That(team.TryBuyTech("mail","unlock"),Is.True);
            Assert.That(team.TryBuyTech("cage","bait"),Is.False);
            Assert.That(team.TryBuyTech("cage","select"),Is.True);
            Assert.That(team.TryBuyTech("cage","bait"),Is.True);
            Assert.That(team.TryBuyTech("cage","delivery"),Is.False);
            Assert.That(team.TryBuyTech("robot","unlock"),Is.True);
            Assert.That(team.TryBuyTech("cage","delivery"),Is.True);
            Assert.That(team.TryBuyTech("cage","auto"),Is.False);
            Assert.That(team.TryBuyTech("magnet","unlock"),Is.True);
            Assert.That(team.TryBuyTech("cage","auto"),Is.True);
            Assert.That(team.rigs,Is.Empty);
        }
    }
}
