using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class TechnologyTests
    {
        private TeamProgress team;
        private string savePath;

        [SetUp]
        public void SetUp()
        {
            team = new TeamProgress();
            team.Treasury = new MaterialCost(10000, 10000, 10000, 10000);
            savePath = Path.Combine(Path.GetTempPath(), "raid-technology-test-" + Guid.NewGuid().ToString("N") + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(savePath)) File.Delete(savePath);
            if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
        }

        [TestCase("charge")]
        [TestCase("trap")]
        public void EntryUnlockSpendsOnceAtRankOneWithoutCreatingARig(string id)
        {
            MaterialCost before = team.Treasury;
            int revision = team.teamRevision;
            int persistCalls = 0;

            bool bought = team.TryBuyTech(id, "unlock", 0, revision, () =>
            {
                persistCalls++;
                return true;
            }, out string reason);

            Assert.That(bought, Is.True, reason);
            Assert.That(persistCalls, Is.EqualTo(1));
            Assert.That(team.TechRank(id, "unlock"), Is.EqualTo(1));
            Assert.That(team.teamRevision, Is.EqualTo(revision + 1));
            Assert.That(team.rigs, Is.Empty, "Buying a technology must not place a physical rig.");
            AssertSpentSomething(before, team.Treasury);

            MaterialCost after = team.Treasury;
            int unlockedRevision = team.teamRevision;
            Assert.That(team.TryBuyTech(id, "unlock", 1, unlockedRevision, () =>
            {
                persistCalls++;
                return true;
            }, out _), Is.False, "The rank-one unlock cannot be bought twice.");
            Assert.That(persistCalls, Is.EqualTo(1), "A rejected repeat purchase must not be persisted.");
            Assert.That(team.TechRank(id, "unlock"), Is.EqualTo(1));
            Assert.That(team.Treasury, Is.EqualTo(after));
            Assert.That(team.teamRevision, Is.EqualTo(unlockedRevision));
            Assert.That(team.rigs, Is.Empty);
        }

        [Test]
        public void StaleRevisionCannotSpendOrInvokePersistence()
        {
            MaterialCost before = team.Treasury;
            int revision = team.teamRevision;
            bool persistCalled = false;

            Assert.That(team.TryBuyTech("trap", "unlock", 0, revision + 1, () =>
            {
                persistCalled = true;
                return true;
            }, out _), Is.False);

            Assert.That(persistCalled, Is.False);
            Assert.That(team.Treasury, Is.EqualTo(before));
            Assert.That(team.teamRevision, Is.EqualTo(revision));
            Assert.That(team.TechRank("trap", "unlock"), Is.Zero);
            Assert.That(team.rigs, Is.Empty);
        }

        [Test]
        public void FailedSaveRollsBackUnlockBalanceAndRevision()
        {
            MaterialCost before = team.Treasury;
            int revision = team.teamRevision;
            int persistCalls = 0;

            Assert.That(team.TryBuyTech("charge", "unlock", 0, revision, () =>
            {
                persistCalls++;
                return false;
            }, out _), Is.False);

            Assert.That(persistCalls, Is.EqualTo(1));
            Assert.That(team.Treasury, Is.EqualTo(before));
            Assert.That(team.teamRevision, Is.EqualTo(revision));
            Assert.That(team.TechRank("charge", "unlock"), Is.Zero);
            Assert.That(team.rigs, Is.Empty);
        }

        [Test]
        public void SchemaFourChecksummedFixtureMigratesWithEmptyTechnologyState()
        {
            const string payload = "{\"schemaVersion\":4,\"biomass\":17,\"chitin\":4,\"scrap\":9," +
                "\"electronics\":3,\"lifetimeCatch\":28,\"goshaMet\":false,\"goshaTamed\":false," +
                "\"expeditionDepth\":1,\"rationCyclesUntilReady\":0,\"hungerRemaining\":180.0," +
                "\"tank\":[],\"cargo\":[],\"deliveredIds\":[],\"hoistPhase\":0,\"hoistRemaining\":0.0," +
                "\"players\":[],\"teamSkills\":[],\"teamRevision\":7," +
                "\"processingRemainder\":{\"biomass\":0,\"chitin\":0,\"scrap\":0,\"electronics\":0}," +
                "\"offeringPoints\":0}";
            WriteEnvelope(payload);

            TeamProgress migrated = TeamSave.Load(savePath);

            Assert.That(migrated.schemaVersion, Is.EqualTo(TeamProgress.CurrentSchema));
            Assert.That(migrated.Treasury, Is.EqualTo(new MaterialCost(17, 4, 9, 3)));
            Assert.That(migrated.teamRevision, Is.EqualTo(7));
            Assert.That(migrated.TechRank("charge", "unlock"), Is.Zero);
            Assert.That(migrated.TechRank("trap", "unlock"), Is.Zero);
            Assert.That(migrated.rigs, Is.Not.Null.And.Empty);
            Assert.That(migrated.NewTechCatchId(), Is.GreaterThan(0));
        }

        [Test]
        public void SaveRoundTripPreservesUnlockRigContentsAndCatchIdCounter()
        {
            Assert.That(team.TryBuyTech("trap", "unlock", 0, team.teamRevision, () => true, out string reason),
                Is.True, reason);

            const int serial = 1;
            long catchId = team.NewTechCatchId();
            team.rigs.Add(new TechRig
            {
                serial = serial,
                kind = "trap",
                mode = "placed",
                position = new Vector3(1.25f, -2.5f, 3.75f),
                depth = 1,
                contents = new List<CatchItem>
                {
                    new CatchItem(catchId, 4, 1, 3, 1, 1, 2, 5, "Lanternfish")
                }
            });
            TeamSave.Save(savePath, team);

            TeamProgress restored = TeamSave.Load(savePath);

            Assert.That(restored.TechRank("trap", "unlock"), Is.EqualTo(1));
            Assert.That(restored.rigs.Count, Is.EqualTo(1));
            TechRig rig = restored.rigs[0];
            Assert.That(rig.serial, Is.EqualTo(serial));
            Assert.That(rig.kind, Is.EqualTo("trap"));
            Assert.That(rig.mode, Is.EqualTo("placed"));
            Assert.That(rig.position, Is.EqualTo(new Vector3(1.25f, -2.5f, 3.75f)));
            Assert.That(rig.depth, Is.EqualTo(1));
            Assert.That(rig.contents.Count, Is.EqualTo(1));
            CatchItem contents = rig.contents[0];
            Assert.That(contents.id, Is.EqualTo(catchId));
            Assert.That(contents.species, Is.EqualTo("Lanternfish"));
            Assert.That(contents.biomass, Is.EqualTo(4));
            Assert.That(contents.chitin, Is.EqualTo(1));
            Assert.That(contents.offering, Is.EqualTo(3));
            Assert.That(contents.scrap, Is.EqualTo(2));
            Assert.That(contents.electronics, Is.EqualTo(5));

            long nextId = restored.NewTechCatchId();
            Assert.That(nextId, Is.GreaterThan(catchId), "The persisted counter must never reissue a rig-content catch ID.");
        }

        [Test]
        public void CatalogHasThirteenStableIdsAndUniqueUpgradesPerTechnology()
        {
            var expectedIds = new HashSet<string>(new[]
            {
                "charge", "vacuum", "net", "trap", "lure", "bubbles", "farm",
                "cage", "winch", "magnet", "oxygen", "robot", "mail"
            }, StringComparer.Ordinal);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            foreach (var technology in TechCatalog.Items)
            {
                count++;
                Assert.That(technology.id, Is.Not.Null.And.Not.Empty);
                Assert.That(ids.Add(technology.id), Is.True, "Duplicate technology ID: " + technology.id);
                Assert.That(TechCatalog.Find(technology.id), Is.Not.Null);
                Assert.That(TechCatalog.Find(technology.id).id, Is.EqualTo(technology.id));

                var upgrades = new HashSet<string>(StringComparer.Ordinal);
                foreach (var upgrade in technology.upgrades)
                {
                    Assert.That(upgrade.id, Is.Not.Null.And.Not.Empty);
                    Assert.That(upgrades.Add(upgrade.id), Is.True,
                        "Duplicate upgrade '" + upgrade.id + "' in technology '" + technology.id + "'.");
                }
                Assert.That(upgrades, Is.Not.Empty, "Technology has no upgrades: " + technology.id);
            }

            Assert.That(count, Is.EqualTo(13));
            Assert.That(ids.SetEquals(expectedIds), Is.True,
                "Technology IDs changed. Expected: " + string.Join(", ", expectedIds) +
                "; actual: " + string.Join(", ", ids));
            Assert.That(TechCatalog.Find("trap"), Is.Not.Null);
        }

        private static void AssertSpentSomething(MaterialCost before, MaterialCost after)
        {
            Assert.That(after.biomass, Is.LessThanOrEqualTo(before.biomass));
            Assert.That(after.chitin, Is.LessThanOrEqualTo(before.chitin));
            Assert.That(after.scrap, Is.LessThanOrEqualTo(before.scrap));
            Assert.That(after.electronics, Is.LessThanOrEqualTo(before.electronics));
            Assert.That(after.biomass < before.biomass || after.chitin < before.chitin ||
                after.scrap < before.scrap || after.electronics < before.electronics,
                Is.True, "A successful technology purchase must charge its catalog cost.");
        }

        private void WriteEnvelope(string payload)
        {
            string hash;
            using (SHA256 sha = SHA256.Create())
                hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            string document = JsonUtility.ToJson(new SaveEnvelope
            {
                format = "RAID.TeamProgress",
                payload = payload,
                sha256 = hash
            });
            File.WriteAllText(savePath, document, new UTF8Encoding(false));
        }

        [Serializable]
        private sealed class SaveEnvelope
        {
            public string format;
            public string payload;
            public string sha256;
        }
    }
}
