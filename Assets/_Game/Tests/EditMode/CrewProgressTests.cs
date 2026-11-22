using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class CrewProgressTests
    {
        [Test]
        public void ExactCrewYieldWithSmallFishAndReloadedRemainder()
        {
            var campaign=new TeamProgress();
            campaign.teamSkills.Add(SkillId.P1);
            var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".json");
            try
            {
                for(int i=1;i<=10;i++)
                {
                    Assert.That(campaign.Deposit(new CatchItem(i,1,0,1,1,1)));
                    Assert.That(campaign.Process(i));
                    if(i==5){TeamSave.Save(file,campaign);campaign=TeamSave.Load(file);}
                }
                Assert.That(campaign.biomass,Is.EqualTo(11));
                Assert.That(campaign.chitin,Is.Zero);
                campaign.teamSkills.AddRange(new[]{SkillId.P2,SkillId.P3,SkillId.P4});
                for(int i=20;i<=21;i++)
                {
                    campaign.Deposit(new CatchItem(i,10,10,2,1,1,10,10));
                    Assert.That(campaign.Process(i));
                }
                Assert.That(campaign.biomass,Is.EqualTo(38));
                Assert.That(campaign.chitin,Is.EqualTo(27));
                Assert.That(campaign.scrap,Is.EqualTo(27));
                Assert.That(campaign.electronics,Is.EqualTo(27));
            }
            finally { File.Delete(file); }
        }

        [Test]
        public void ASharedCatchCreditsOneYieldAndFeedingLeavesTheWalletAlone()
        {
            var team=new TeamProgress();
            team.RegisterPlayer("zz");
            team.RegisterPlayer("aa");
            team.Deposit(new CatchItem(1,7,3,2,3,1){caughtBy=new[]{"zz","aa","zz"}});
            Assert.That(team.Process(1));
            Assert.That(team.biomass,Is.EqualTo(7));
            Assert.That(team.chitin,Is.EqualTo(3));
            Assert.That(team.offeringPoints,Is.Zero);
            team.teamSkills.AddRange(new[]{SkillId.N1,SkillId.N2,SkillId.N4});
            team.Deposit(new CatchItem(2,2,0,2,1,1){caughtBy=new[]{"aa"}});
            Assert.That(team.Feed(2));
            Assert.That(team.offeringPoints,Is.EqualTo(290));
            Assert.That(team.biomass,Is.EqualTo(7));
            Assert.That(team.PublicView().players,Is.Empty);
        }

        [Test]
        public void VersionTwoBalancesMergeOnceAndSkillsStayWithTheirOwner()
        {
            string file=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".json");
            try
            {
                const string old="{\"schemaVersion\":2,\"biomass\":5,\"scrap\":3,\"offeringPoints\":290,"+
                    "\"players\":[{\"key\":\"one\",\"wallet\":{\"biomass\":2},\"skills\":[1]},{\"key\":\"two\",\"wallet\":{\"biomass\":4,\"scrap\":8},\"skills\":[5]}]}";
                using var sha=SHA256.Create();
                var envelope=new LegacyEnvelope{format="RAID.TeamProgress",payload=old,sha256=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(old)))};
                File.WriteAllText(file,JsonUtility.ToJson(envelope));
                var team=TeamSave.Load(file);
                Assert.That(team.Treasury,Is.EqualTo(new MaterialCost(11,0,11,0)));
                Assert.That(team.Player("one").skills,Is.EqualTo(new[]{SkillId.H1}));
                Assert.That(team.Player("two").skills,Is.EqualTo(new[]{SkillId.S1}));
                Assert.That(team.offeringPoints,Is.EqualTo(290));
                TeamSave.Save(file,team);
                Assert.That(TeamSave.Load(file).Treasury,Is.EqualTo(team.Treasury));
            }
            finally { File.Delete(file);File.Delete(file+".bak"); }
        }

        [Test]
        public void VersionOneCapacityFoodAndCargoSurviveWithoutRepeatingTheMigration()
        {
            var tuning=ScriptableObject.CreateInstance<ProgressionTuning>();
            var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N")+".json");
            try
            {
                const string old="{\"schemaVersion\":1,\"winchLevel\":2,\"offering\":3,\"hungerRemaining\":42,\"goshaTamed\":true,"+
                    "\"cargo\":[{\"id\":11,\"species\":\"Needlefish\",\"offering\":1,\"cargoSize\":1,\"depthTier\":1}]}";
                using var sha=SHA256.Create();
                var envelope=new LegacyEnvelope{format="RAID.TeamProgress",payload=old,sha256=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(old)))};
                File.WriteAllText(file,JsonUtility.ToJson(envelope));
                var campaign=TeamSave.Load(file,tuning);
                Assert.That(campaign.Capacity(tuning),Is.EqualTo(16));
                Assert.That(campaign.offeringPoints,Is.EqualTo(300));
                Assert.That(campaign.hungerRemaining,Is.EqualTo(42));
                Assert.That(campaign.cargo[0].id,Is.EqualTo(11));
                Assert.That(campaign.goshaTamed);
                campaign.winchLevel=3;
                TeamSave.Save(file,campaign);
                campaign=TeamSave.Load(file,tuning);
                Assert.That(campaign.Capacity(tuning),Is.EqualTo(16));
                campaign.teamSkills.AddRange(new[]{SkillId.K1,SkillId.K2,SkillId.K3,SkillId.K4});
                Assert.That(campaign.Capacity(tuning),Is.EqualTo(34));
                Assert.That(File.Exists(file+".bak"));
            }
            finally { UnityEngine.Object.DestroyImmediate(tuning);File.Delete(file);File.Delete(file+".bak"); }
        }

        [Serializable]
        sealed class LegacyEnvelope { public string format,payload,sha256; }
    }
}
