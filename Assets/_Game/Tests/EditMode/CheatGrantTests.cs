using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class CheatGrantTests
    {
        static CatchItem Ray(long id) => new(id,9,3,5,3,2,2,1,"Sailray");

        [TestCase("biomass")]
        [TestCase("chitin")]
        [TestCase("scrap")]
        [TestCase("electronics")]
        public void EveryTreasuryFieldAcceptsTheEntireIntegerRange(string resource)
        {
            var team=new TeamProgress();
            Assert.That(team.TryGrantResource(resource,int.MaxValue,()=>true,out var granted,out _),Is.True);
            Assert.That(granted,Is.EqualTo(int.MaxValue));
            int balance=resource switch{"biomass"=>team.biomass,"chitin"=>team.chitin,"scrap"=>team.scrap,_=>team.electronics};
            Assert.That(balance,Is.EqualTo(int.MaxValue));
            Assert.That(team.TryGrantResource(resource,1,()=>throw new Exception(),out granted,out _),Is.False);
            Assert.That(granted,Is.Zero);Assert.That(team.teamRevision,Is.EqualTo(1));
        }

        [Test] public void PartialGrantPreservesFoodFractionsRejectsInvalidRequestsAndRollsBackOnWriteFailure()
        {
            var team=new TeamProgress{chitin=int.MaxValue-9,offeringPoints=13};
            Assert.That(team.TryGrantResource("chitin",int.MaxValue,()=>false,out int granted,out _),Is.False);
            Assert.That(granted,Is.Zero);Assert.That(team.chitin,Is.EqualTo(int.MaxValue-9));
            Assert.Throws<IOException>(()=>team.TryGrantResource("chitin",10,()=>throw new IOException(),out _,out _));
            Assert.That(team.teamRevision,Is.Zero);Assert.That(team.chitin,Is.EqualTo(int.MaxValue-9));
            Assert.That(team.TryGrantResource("chitin",int.MaxValue,()=>true,out granted,out _),Is.True);
            Assert.That(granted,Is.EqualTo(9));Assert.That(team.chitin,Is.EqualTo(int.MaxValue));
            Assert.That(team.TryGrantResource("offering",int.MaxValue,()=>true,out granted,out _),Is.True);
            Assert.That(granted,Is.EqualTo((int.MaxValue-13)/TeamProgress.FoodScale));
            Assert.That(team.offeringPoints%TeamProgress.FoodScale,Is.EqualTo(13));
            Assert.That(team.TryGrantResource("offering",1,()=>throw new Exception(),out _,out _),Is.False);
            Assert.That(team.TryGrantResource("xp",1,()=>throw new Exception(),out _,out _),Is.False);
            Assert.That(team.TryGrantResource("scrap",-1,()=>throw new Exception(),out _,out _),Is.False);
            Assert.That(team.teamRevision,Is.EqualTo(1));
        }

        [Test] public void FishGrantPreservesYieldAndSerialsAcrossRollbackSaveReloadAndHoistReservation()
        {
            var team=new TeamProgress();
            var tuning=ScriptableObject.CreateInstance<ProgressionTuning>();
            string path=Path.Combine(Path.GetTempPath(),"raid-cheat-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                tuning.reservoirCapacity=4;
                long legacy=(1L<<60)+1;
                Assert.That(team.Deposit(Ray(legacy),tuning),Is.True);
                Assert.That(team.LoadCargo(Ray(19),tuning),Is.True);
                Assert.That(team.BeginHoist(tuning),Is.True);
                TeamSave.Save(path,team);
                bool Fail(){team.Validate();return false;}
                Assert.That(team.TryGrantFish(Ray(1),int.MaxValue,tuning,Fail,out int granted,out _),Is.False);
                Assert.That(granted,Is.Zero);Assert.That(team.techCatchSequence,Is.Zero);
                Assert.Throws<IOException>(()=>team.TryGrantFish(Ray(1),50,tuning,()=>throw new IOException(),out _,out _));
                Assert.That(team.deliveredIds,Is.EqualTo(new[]{legacy}));
                Assert.That(team.tank.Count,Is.EqualTo(1));Assert.That(team.lifetimeCatch,Is.EqualTo(15));
                bool Save(){TeamSave.Save(path,team);return true;}
                Assert.That(team.TryGrantFish(Ray(1),int.MaxValue,tuning,Save,out granted,out _),Is.True);
                Assert.That(granted,Is.EqualTo(2));
                var loaded=TeamSave.Load(path);
                Assert.That(loaded.deliveredIds,Is.EqualTo(new[]{legacy,legacy+1,legacy+2}));
                Assert.That(loaded.ReservoirRoom(tuning),Is.Zero);
                Assert.That(loaded.tank.All(f=>f.species=="Sailray" && f.cargoSize==3 && f.depthTier==2 &&
                    f.biomass==9 && f.chitin==3 && f.scrap==2 && f.electronics==1 && f.offering==5),Is.True);
                Assert.That(loaded.TryGrantFish(Ray(1),1,tuning,()=>throw new Exception(),out granted,out _),Is.False);
                loaded.TickHoist(100,tuning);
                Assert.That(loaded.tank.Select(f=>f.id),Is.EqualTo(new[]{legacy,legacy+1,legacy+2,19}));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tuning);
                File.Delete(path);File.Delete(path+".bak");
            }
        }

        [Test] public void FullWalletPreservesFishSalvageAndInFlightCargoUntilThereIsSpace()
        {
            var team=new TeamProgress{biomass=int.MaxValue,offeringPoints=int.MaxValue};
            var tuning=ScriptableObject.CreateInstance<ProgressionTuning>();
            try
            {
                Assert.That(team.Deposit(Ray(9),tuning),Is.True);
                Assert.That(team.CreditProcessedCatch(9,tuning,out _),Is.False);
                Assert.That(team.Feed(9,tuning),Is.False);
                Assert.That(team.tank.Single().id,Is.EqualTo(9));
                Assert.That(team.LoadCargo(CatchItem.Salvage(19,1,2,1),tuning),Is.True);
                Assert.That(team.BeginHoist(tuning),Is.True);
                Assert.That(team.TryGrantResource("scrap",int.MaxValue,()=>true,out _,out _),Is.True);
                Assert.That(team.Deposit(CatchItem.Salvage(20,1,1,0),tuning),Is.False);
                Assert.That(team.TickHoist(100,tuning).IsEmpty,Is.True);
                Assert.That(team.hoistPhase,Is.EqualTo(HoistPhase.Loading));
                Assert.That(team.BeginHoist(tuning),Is.False);
                Assert.That(team.cargo.Single().id,Is.EqualTo(19));
                Assert.That(team.deliveredIds,Is.EqualTo(new long[]{9}));
                team.scrap-=2;
                Assert.That(team.BeginHoist(tuning),Is.True);
                Assert.That(team.TickHoist(100,tuning),Is.EqualTo(new MaterialCost(0,0,2,1)));
                Assert.That(team.deliveredIds,Is.EqualTo(new long[]{9,19}));
                Assert.That(team.cargo,Is.Empty);
            }
            finally{UnityEngine.Object.DestroyImmediate(tuning);}
        }
    }
}
