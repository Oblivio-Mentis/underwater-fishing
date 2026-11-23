using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class CrewProgressTests
    {
        TeamProgress team;
        ProgressionTuning tuning;
        PlayerProgress first, second;
        string path;
        static SkillId Id(SkillLine line,int rank)=>SkillTreeDefinition.Id(line,rank);

        [SetUp]
        public void Prepare()
        {
            tuning=ScriptableObject.CreateInstance<ProgressionTuning>();
            team=new TeamProgress(tuning){Treasury=new MaterialCost(1000,1000,1000,1000)};
            first=team.RegisterPlayer("lan:first");
            second=team.RegisterPlayer("lan:second");
            path=Path.Combine(Path.GetTempPath(),"raid019-test-"+Guid.NewGuid().ToString("N")+".json");
        }

        [TearDown]
        public void Finish()
        {
            UnityEngine.Object.DestroyImmediate(tuning);
            File.Delete(path);
            File.Delete(path+".bak");
        }

        bool Buy(PlayerProgress player,SkillLine line,int rank,Func<bool> persist=null) =>
            team.TryPurchase(player,Id(line,rank),team.teamRevision,player.revision,tuning,persist??(()=>true),out _);

        [Test]
        public void ThreeCableRanksHaveSequentialDependenciesAndConfirmedExactPrices()
        {
            var initial=team.Treasury;
            Assert.That(Buy(first,SkillLine.Cable,3),Is.False);
            Assert.That(team.Treasury,Is.EqualTo(initial));
            for(int rank=1;rank<=3;rank++)
            {
                Assert.That(Buy(first,SkillLine.Cable,rank),Is.True);
                initial=initial.Subtract(SkillTreeDefinition.Get(Id(SkillLine.Cable,rank)).Cost);
                Assert.That(team.Treasury,Is.EqualTo(initial));
                if(rank==1)Assert.That(Buy(first,SkillLine.Cable,3),Is.False);
            }
            Assert.That(SkillTreeDefinition.Value(first.skills,SkillLine.Cable),Is.EqualTo(36));
            Assert.That(second.skills,Is.Empty);
        }

        [Test]
        public void TwoBuyersCanOnlyPurchaseOneSharedCageRank()
        {
            Assert.That(Buy(first,SkillLine.Cage,1),Is.True);
            int revision=team.teamRevision;
            var before=team.Treasury;
            var target=Id(SkillLine.Cage,2);
            Assert.That(team.TryPurchase(first,target,revision,first.revision,tuning,()=>true,out _),Is.True);
            Assert.That(team.TryPurchase(second,target,revision,second.revision,tuning,()=>true,out _),Is.False);
            Assert.That(team.Treasury,Is.EqualTo(before.Subtract(SkillTreeDefinition.Get(target).Cost)));
            Assert.That(team.teamRevision,Is.EqualTo(revision+1));
            Assert.That(team.PublicView().Capacity(tuning),Is.EqualTo(16));
            Assert.That(first.revision+second.revision,Is.Zero);
        }

        [Test]
        public void WrongProfileReferenceAndStaleRevisionsCannotSpend()
        {
            var before=team.Treasury;
            var target=Id(SkillLine.Reel,1);
            var substitute=new PlayerProgress{key=first.key};
            Assert.That(team.TryPurchase(substitute,target,0,0,tuning,()=>true,out _),Is.False);
            Assert.That(team.TryPurchase(first,target,0,1,tuning,()=>true,out _),Is.False);
            Assert.That(team.TryPurchase(first,target,1,0,tuning,()=>true,out _),Is.False);
            Assert.That(team.Treasury,Is.EqualTo(before));
            Assert.That(first.skills,Is.Empty);
        }

        [Test]
        public void InsufficientResourcesAndFailedWriteLeaveBalancesRanksAndClocksIntact()
        {
            team.Treasury=default;
            Assert.That(Buy(first,SkillLine.Satiety,1),Is.False);
            team.Treasury=new MaterialCost(100,100,100,100);
            var before=team.Treasury;
            var remaining=team.hungerRemaining;
            float observedDuringWrite=0;
            Assert.That(Buy(first,SkillLine.Satiety,1,()=> {observedDuringWrite=team.hungerRemaining;return false;}),Is.False);
            Assert.That(observedDuringWrite,Is.GreaterThan(remaining));
            Assert.That(team.hungerRemaining,Is.EqualTo(remaining));
            Assert.That(team.teamRevision,Is.Zero);
            Assert.That(team.teamSkills,Is.Empty);
            Assert.That(team.Treasury,Is.EqualTo(before));
        }

        [Test]
        public void RoundTripKeepsPersonalAndTeamRanksWithoutPublishingProfileKeys()
        {
            Assert.That(Buy(first,SkillLine.Reel,1),Is.True);
            Assert.That(Buy(second,SkillLine.Oxygen,1),Is.True);
            Assert.That(Buy(second,SkillLine.Hull,1),Is.True);
            team.expeditionDepth=2;
            team.goshaMet=team.goshaTamed=true;
            team.Deposit(new CatchItem(12,3,2,1,1,2,1,4){caughtBy=new[]{first.key,second.key}});
            var before=team.Treasury;
            TeamSave.Save(path,team);
            var loaded=TeamSave.Load(path);
            Assert.That(loaded.Treasury,Is.EqualTo(before));
            Assert.That(loaded.expeditionDepth,Is.EqualTo(2));
            Assert.That(loaded.goshaTamed,Is.True);
            Assert.That(loaded.Player(first.key).skills,Is.EqualTo(new[]{Id(SkillLine.Reel,1)}));
            Assert.That(loaded.Player(second.key).skills,Is.EqualTo(new[]{Id(SkillLine.Oxygen,1)}));
            Assert.That(loaded.TeamRank(SkillLine.Hull),Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(loaded.PublicView()),Does.Not.Contain(first.key).And.Not.Contain(second.key));
        }

        [Test]
        public void HoistReservesRealTankSpaceAndKeepsEachCatch()
        {
            tuning.reservoirCapacity=2;
            var fish1=new CatchItem(1,1,0,1,1,1);
            var fish2=new CatchItem(2,1,0,1,1,1);
            var fish3=new CatchItem(3,1,0,1,1,1);
            Assert.That(team.Deposit(fish1,tuning),Is.True);
            Assert.That(team.LoadCargo(fish2,tuning),Is.True);
            Assert.That(team.LoadCargo(fish3,tuning),Is.True);
            Assert.That(team.BeginHoist(tuning),Is.False);
            Assert.That(team.Feed(1,tuning),Is.True);
            Assert.That(team.BeginHoist(tuning),Is.True);
            Assert.That(team.Deposit(new CatchItem(4,1,0,1,1,1),tuning),Is.False);
            team.TickHoist(75,tuning);
            Assert.That(team.tank.ConvertAll(x=>x.id),Is.EqualTo(new long[]{2,3}));
            Assert.That(team.cargo,Is.Empty);
        }

        [Test]
        public void FoodAndFourYieldsUseTheHighestOwnedRankAndPreserveRemainder()
        {
            Assert.That(Buy(first,SkillLine.Nutrition,1),Is.True);
            Assert.That(Buy(first,SkillLine.SmallRation,1),Is.True);
            Assert.That(Buy(first,SkillLine.Separator,1),Is.True);
            Assert.That(Buy(first,SkillLine.Biomass,1),Is.True);
            var meal=new CatchItem(1,1,1,2,1,1,1,1);
            Assert.That(team.FoodValue(meal,tuning),Is.EqualTo(218));
            Assert.That(team.ProcessPreview(meal,tuning),Is.EqualTo(new MaterialCost(112,105,105,105)));
            Assert.That(team.Deposit(meal),Is.True);
            Assert.That(team.CreditProcessedCatch(meal.id,tuning,out var credited),Is.True);
            Assert.That(credited,Is.EqualTo(new MaterialCost(1,1,1,1)));
            Assert.That(team.processingRemainder,Is.EqualTo(new MaterialCost(12,5,5,5)));
            Assert.That(team.CreditProcessedCatch(meal.id,tuning,out _),Is.False);
        }

        [Test]
        public void SatietyScalesCurrentCycleAndCarryoverStaysCapped()
        {
            team.hungerRemaining=tuning.hungerInterval/2;
            Assert.That(Buy(first,SkillLine.Satiety,1),Is.True);
            Assert.That(team.hungerRemaining,Is.EqualTo(tuning.hungerInterval*.53f).Within(.001));
            Assert.That(Buy(first,SkillLine.Carryover,1),Is.True);
            team.offering=22;
            team.EvaluateHunger(tuning);
            Assert.That(team.offeringPoints,Is.EqualTo(150));
            team.offering=100;
            team.EvaluateHunger(tuning);
            Assert.That(team.offeringPoints,Is.EqualTo(300));
        }
    }
}
