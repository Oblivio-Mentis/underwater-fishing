using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class RigTransactionTests
    {
        ProgressionTuning tuning;
        string path;
        [SetUp] public void SetUp()
        {
            tuning=ScriptableObject.CreateInstance<ProgressionTuning>();
            path=Path.Combine(Path.GetTempPath(),"nereid-ledger-"+Guid.NewGuid().ToString("N")+".json");
        }
        [TearDown] public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(tuning);
            File.Delete(path);File.Delete(path+".bak");
        }
        static CatchItem Fish(long id)=>new(id,4,1,2,1,1,species:"Needlefish");

        [Test] public void RepeatedArrivalAtFullReservoirAndDiskFailureLeaveOneOwnerForEachCatch()
        {
            var team=new TeamProgress();
            tuning.reservoirCapacity=1;
            var mail=team.AddRig("mail",Vector3.zero,-1);
            mail.mode="placed";mail.flight="transit";
            mail.contents.Add(CatchItem.Salvage(1,1,3,2));
            mail.contents.Add(Fish(2));mail.contents.Add(Fish(3));
            TeamSave.Save(path,team);
            Assert.Throws<IOException>(()=>team.SettleRigCargo(mail,tuning,true,()=>throw new IOException("Read-only disk")));
            Assert.That(mail.contents.Select(f=>f.id),Is.EqualTo(new long[]{1,2,3}));
            Assert.That(team.Treasury,Is.EqualTo(default(MaterialCost)));
            Assert.That(team.lifetimeCatch,Is.Zero);Assert.That(team.teamRevision,Is.Zero);
            Assert.That(team.deliveredIds,Is.Empty);Assert.That(team.tank,Is.Empty);
            Assert.That(TeamSave.Load(path).rigs.Single().contents.Count,Is.EqualTo(3));
            bool Save(){TeamSave.Save(path,team);return true;}
            Assert.That(team.SettleRigCargo(mail,tuning,true,Save),Is.EqualTo(2));
            Assert.That(team.SettleRigCargo(mail,tuning,true,Save),Is.Zero);
            var restored=TeamSave.Load(path);
            Assert.That(restored.rigs.Single().contents.Select(f=>f.id),Is.EqualTo(new long[]{3}));
            Assert.That(restored.deliveredIds,Is.EqualTo(new long[]{1,2}));
            Assert.That(restored.tank.Single().id,Is.EqualTo(2));
            Assert.That(restored.Treasury,Is.EqualTo(new MaterialCost(0,0,3,2)));
            Assert.That(restored.teamRevision,Is.EqualTo(1));
        }

        [Test] public void FailedRepairBasketTransferAndHoistRestoreTheSameSerialsAndTreasury()
        {
            var team=new TeamProgress{Treasury=new MaterialCost(10,0,12,0)};
            var trap=team.AddRig("trap",Vector3.one,-1);trap.Detach();
            trap.health=.5f;trap.mode="damaged";
            Assert.That(team.RepairRig(trap,3,()=>false),Is.False);
            Assert.That(trap.health,Is.EqualTo(.5f));Assert.That(trap.mode,Is.EqualTo("damaged"));
            Assert.That(team.scrap,Is.EqualTo(12));Assert.That(team.teamRevision,Is.Zero);
            Assert.That(team.RepairRig(trap,3,()=>true),Is.True);
            Assert.That(team.teamRevision,Is.EqualTo(1));Assert.That(team.scrap,Is.EqualTo(9));
            trap.contents.Add(Fish(15));
            Assert.That(team.PackRigBasket(trap,3,Vector3.zero,0,()=>false),Is.False);
            Assert.That(team.lastRigSerial,Is.EqualTo(1));Assert.That(trap.contents.Single().id,Is.EqualTo(15));
            Assert.That(team.PackRigBasket(trap,3,Vector3.zero,0,()=>true),Is.True);
            var basket=team.rigs.Single(r=>r.kind=="basket");
            Assert.That(trap.contents,Is.Empty);
            Assert.That(team.SettleRigCargo(basket,tuning,false,()=>false),Is.Zero);
            Assert.That(team.cargo,Is.Empty);Assert.That(team.rigs.Contains(basket),Is.True);
            Assert.That(team.SettleRigCargo(basket,tuning,false,()=>true),Is.EqualTo(1));
            Assert.That(team.SettleRigCargo(basket,tuning,false,()=>true),Is.Zero);
            Assert.That(team.rigs.Contains(basket),Is.False);
            Assert.That(team.cargo.Select(c=>c.id),Is.EqualTo(new long[]{15}));
            team.Validate();
        }

        [Test] public void SchemaFourPreservesFishInTransitAndDeliveredIdsAcrossFirstMigratedSave()
        {
            const string old="{\"schemaVersion\":4,\"biomass\":8,\"scrap\":3,\"lifetimeCatch\":5,"+
                "\"expeditionDepth\":1,\"hungerRemaining\":93,\"hoistPhase\":1,\"hoistRemaining\":1.5,"+
                "\"tank\":[{\"id\":41,\"biomass\":3,\"offering\":1,\"cargoSize\":1,\"depthTier\":1}],"+
                "\"cargo\":[{\"id\":42,\"biomass\":2,\"offering\":1,\"cargoSize\":1,\"depthTier\":1}],"+
                "\"deliveredIds\":[41],\"teamSkills\":[],\"players\":[],\"teamRevision\":7,\"offeringPoints\":120}";
            using var hash=SHA256.Create();
            string checksum=Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(old)));
            string envelope=JsonUtility.ToJson(new Envelope{format="RAID.TeamProgress",payload=old,sha256=checksum});
            File.WriteAllText(path,envelope);
            var migrated=TeamSave.Load(path);
            Assert.That(migrated.schemaVersion,Is.EqualTo(TeamProgress.CurrentSchema));
            Assert.That(File.ReadAllText(path),Is.EqualTo(envelope));
            Assert.That(migrated.teamRevision,Is.EqualTo(7));
            Assert.That(migrated.offeringPoints,Is.EqualTo(120));
            Assert.That(migrated.rigs,Is.Empty);Assert.That(migrated.techPurchases,Is.Empty);
            Assert.That(migrated.Deposit(Fish(41),tuning),Is.False);Assert.That(migrated.Deposit(Fish(42),tuning),Is.False);
            TeamSave.Save(path,migrated);
            Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(envelope));
            var restored=TeamSave.Load(path);
            restored.TickHoist(300,tuning);
            Assert.That(restored.deliveredIds,Is.EqualTo(new long[]{41,42}));
            Assert.That(restored.tank.Select(f=>f.id),Is.EqualTo(new long[]{41,42}));
            Assert.That(restored.Treasury,Is.EqualTo(new MaterialCost(8,0,3,0)));
            restored.Validate();
        }

        [Serializable] sealed class Envelope {public string format,payload,sha256;}

        [Test] public void LoadingOldCatchIdsInTheIndustrialRangeNeverReissuesDeliveredOrStoredFish()
        {
            const long prefix=1L<<60;
            var team=new TeamProgress();
            Assert.That(team.Deposit(Fish(prefix+1),tuning),Is.True);
            Assert.That(team.LoadCargo(Fish(prefix+3),tuning),Is.True);
            Assert.That(team.Deposit(Fish(prefix*2-1),tuning),Is.True);
            TeamSave.Save(path,team);
            var restored=TeamSave.Load(path);
            Assert.That(restored.NewTechCatchId(),Is.EqualTo(prefix+2));
            Assert.That(restored.NewTechCatchId(),Is.EqualTo(prefix+4));
        }
    }
}
