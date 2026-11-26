using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class TechInventoryTests
    {
        static TeamProgress Team(params string[] equipment)
        {
            var state=new TeamProgress();
            foreach(string id in equipment)state.techPurchases.Add(new TechPurchase{item=id,upgrade="unlock",rank=1});
            return state;
        }

        [Test] public void DroppedUnitMovesBetweenOwnersAtTheTeamLimitAndRollsBackFailedWrites()
        {
            string path=Path.Combine(Path.GetTempPath(),"nereid-loose-invention-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var team=Team("charge");
                var owner=team.RegisterPlayer("sender");
                var receiver=team.RegisterPlayer("receiver");
                int limit=TechRig.TotalLimit(team,TechCatalog.Find("charge"));
                for(int i=0;i<limit;i++)
                    Assert.That(team.TakeTechFromRack(owner,"charge",i/team.TechStackLimit("charge"),()=>true,out _));
                int originalCount=owner.pocketStacks[0].count;
                int revision=owner.revision;
                var at=new Vector3(1,2,3);
                Assert.That(team.DropPocketInvention(owner,0,"charge",at,32,()=>false,out _),Is.False);
                Assert.That(owner.pocketStacks[0].count,Is.EqualTo(originalCount));
                Assert.That(owner.revision,Is.EqualTo(revision));
                Assert.That(team.lastRigSerial,Is.Zero);
                Assert.That(team.rigs,Is.Empty);
                bool Save(){TeamSave.Save(path,team);return true;}
                Assert.That(team.DropPocketInvention(owner,0,"charge",at,32,Save,out _));
                team=TeamSave.Load(path);
                owner=team.Player("sender");receiver=team.Player("receiver");
                var loose=team.rigs.Single();
                Assert.That(loose.mode,Is.EqualTo("dropped"));
                Assert.That(loose.position,Is.EqualTo(at));Assert.That(loose.yaw,Is.EqualTo(32f));
                Assert.That(owner.pocketStacks.Sum(s=>s.count)+team.rigs.Count,Is.EqualTo(limit));
                Assert.Throws<IOException>(()=>team.StoreTechInSlot(receiver,loose.serial,1,19,
                    ()=>throw new IOException("test write"),out _));
                Assert.That(team.rigs.Single(),Is.SameAs(loose));Assert.That(receiver.pocketStacks[1].count,Is.Zero);
                Assert.That(team.StoreTechInSlot(receiver,loose.serial,1,19,Save,out _));
                Assert.That(team.StoreTechInSlot(owner,loose.serial,0,18,Save,out _),Is.False);
                var loaded=TeamSave.Load(path);
                Assert.That(loaded.rigs,Is.Empty);
                Assert.That(loaded.Player("receiver").pocketStacks[1].kind,Is.EqualTo("charge"));
                Assert.That(loaded.Player("receiver").pocketStacks[1].count,Is.EqualTo(1));
                Assert.That(loaded.Player("sender").pocketStacks.Sum(s=>s.count),Is.EqualTo(limit-1));
            }
            finally{File.Delete(path);File.Delete(path+".bak");}
        }

        [Test] public void ReleasingALoadedOrSharedRigKeepsItsSpecimensAndSerial()
        {
            var team=Team("cage");
            var rig=team.AddRig("cage",Vector3.zero,12);
            rig.helperId=17;
            var catchItem=new CatchItem(43,1,0,2,1,1,species:"Bristlefish");
            rig.contents.Add(catchItem);
            Assert.That(team.DropCarriedInvention(rig,12,Vector3.right,30,()=>true,out _));
            Assert.That(rig.operatorId,Is.EqualTo(17));Assert.That(rig.mode,Is.EqualTo("carried"));
            Assert.That(team.DropCarriedInvention(rig,17,Vector3.up,30,()=>false,out _),Is.False);
            Assert.That(rig.position,Is.EqualTo(Vector3.zero));Assert.That(rig.operatorId,Is.EqualTo(17));
            Assert.That(team.DropCarriedInvention(rig,17,Vector3.up,30,()=>true,out _));
            Assert.That(rig.mode,Is.EqualTo("captured"));Assert.That(rig.operatorId,Is.EqualTo(-1));
            Assert.That(rig.contents.Single(),Is.SameAs(catchItem));
            Assert.That(team.rigs.Single().serial,Is.EqualTo(1));
        }

        [Test] public void EachUnlockedInventionIsFreeAndOnlyTheChosenOwnersSlotChanges()
        {
            var ids=TechCatalog.Items.Select(r=>r.id).ToArray();
            foreach(string id in ids)
            {
                var team=Team(id);
                var owner=team.RegisterPlayer("one");
                var observer=team.RegisterPlayer("two");
                int limit=TechCatalog.Find(id).stackBase;
                Assert.That(limit,Is.Positive,id);
                var step=TechCatalog.Find(id).Upgrade("stack");
                Assert.That(step,Is.Not.Null,id);
                int previous=limit;
                foreach(float value in step.values)
                {
                    int next=Mathf.RoundToInt(value);
                    Assert.That(value,Is.EqualTo((float)next),id);
                    Assert.That(next,Is.GreaterThan(previous),id);
                    previous=next;
                }
                for(int i=0;i<limit;i++)Assert.That(team.TakeTechFromRack(owner,id,2,()=>true,out _),Is.True,id);
                Assert.That(team.TakeTechFromRack(owner,id,2,()=>true,out _),Is.False,id);
                Assert.That(team.TakeTechFromRack(owner,id,1,()=>true,out _),Is.True,id);
                Assert.That(owner.pocketStacks[2].count,Is.EqualTo(limit));
                Assert.That(owner.pocketStacks[1].count,Is.EqualTo(1));
                Assert.That(observer.pocketStacks.All(slot=>slot.count==0));
                Assert.That(team.PublicView().players,Is.Empty);
                Assert.That(team.rigs,Is.Empty);Assert.That(team.Treasury,Is.EqualTo(default(MaterialCost)));
                Assert.That(team.teamRevision,Is.Zero);
                Assert.That(team.TakeTechFromRack(new PlayerProgress{key=owner.key},id,0,()=>true,out _),Is.False);
                team.Validate();
            }
        }

        [Test] public void StackResearchIsAnAbsoluteSlotLimitAndDeploymentHasASeparateFleetLimit()
        {
            var team=Team("trap","charge");
            var profile=team.RegisterPlayer("owner");
            var item=TechCatalog.Find("trap");
            int initial=team.TechStackLimit("trap");
            for(int i=0;i<initial;i++)team.TakeTechFromRack(profile,"trap",0,()=>true,out _);
            team.Treasury=new MaterialCost(4000,4000,4000,4000);
            Assert.That(team.TryBuyTech("trap","stack"),Is.True);
            int bigger=Mathf.RoundToInt(item.Upgrade("stack").values[0]);
            Assert.That(team.TechStackLimit("trap"),Is.EqualTo(bigger).And.GreaterThan(initial));
            var afterResearch=team.Treasury;
            Assert.That(team.TakeTechFromRack(profile,"trap",0,()=>true,out _),Is.True);
            Assert.That(team.TakeTechFromRack(profile,"charge",0,()=>true,out _),Is.False);
            for(int i=0;i<TechRig.TotalLimit(team,item);i++)team.AddRig("trap",Vector3.zero,-1).Detach();
            Assert.That(team.DeployTechFromSlot(profile,0,Vector3.one,4,()=>true,out _),Is.False);
            Assert.That(team.TakeTechFromRack(profile,"trap",3,()=>true,out _),Is.True);
            Assert.That(team.Treasury,Is.EqualTo(afterResearch));Assert.That(profile.pocketStacks[0].count,Is.EqualTo(initial+1));
        }

        [Test] public void FailedAndRepeatedDeployOrReturnCannotDuplicateTheSlotOrFieldUnit()
        {
            var team=Team("charge");
            var owner=team.RegisterPlayer("one");
            Assert.That(team.TakeTechFromRack(owner,"charge",0,()=>false,out _),Is.False);
            Assert.That(owner.revision,Is.Zero);
            Assert.That(team.TakeTechFromRack(owner,"charge",0,()=>true,out _),Is.True);
            Assert.That(team.DeployTechFromSlot(owner,0,Vector3.one,8,()=>false,out _),Is.False);
            Assert.That(owner.pocketStacks[0].count,Is.EqualTo(1));
            Assert.That(team.rigs,Is.Empty);Assert.That(team.lastRigSerial,Is.Zero);
            Assert.That(team.DeployTechFromSlot(owner,0,Vector3.one,8,()=>true,out _),Is.True);
            Assert.That(owner.pocketStacks[0].count,Is.Zero);
            Assert.That(team.DeployTechFromSlot(owner,0,Vector3.one,8,()=>true,out _),Is.False);
            var rig=team.rigs.Single();
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,7,()=>true,out _),Is.False);
            rig.contents.Add(new CatchItem(51,1,1,1,1,1));
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.False);
            rig.contents.Clear();rig.health=.5f;
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.False);
            rig.health=1;
            Assert.Throws<IOException>(()=>team.StoreTechInSlot(owner,rig.serial,3,8,()=>throw new IOException("full"),out _));
            Assert.That(team.rigs.Single(),Is.SameAs(rig));Assert.That(owner.pocketStacks[3].count,Is.Zero);
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.True);
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.False);
            Assert.That(owner.pocketStacks[3].count,Is.EqualTo(1));
            Assert.That(team.rigs,Is.Empty);Assert.That(owner.revision,Is.EqualTo(3));
            Assert.That(team.teamRevision,Is.Zero);Assert.That(team.Treasury,Is.EqualTo(default(MaterialCost)));
        }

        [Test] public void RackPreflightValidatesTheLiveOwnerSlotAndCapacityWithoutIssuingOrWriting()
        {
            var team=Team("charge");
            var owner=team.RegisterPlayer("one");
            Assert.That(team.CanTakeTechFromRack(null,"charge",0,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(new PlayerProgress{key="one"},"charge",0,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"charge",-1,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"charge",7,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"charge",6,out _),Is.True);
            Assert.That(team.CanTakeTechFromRack(owner,"cage",0,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"charge",0,out _),Is.True);
            Assert.That(owner.revision,Is.Zero);Assert.That(owner.pocketStacks.All(s=>s.count==0));
            owner.pocketStacks[0]=new TechSlot{kind="charge",count=team.TechStackLimit("charge")};
            Assert.That(team.CanTakeTechFromRack(owner,"charge",0,out _),Is.False);
            Assert.That(owner.revision,Is.Zero);Assert.That(team.teamRevision,Is.Zero);
            Assert.That(team.rigs,Is.Empty);Assert.That(team.Treasury,Is.EqualTo(default(MaterialCost)));
        }

        [Test] public void SchemaFiveMigratesProfilesWithoutManufacturingOrErasingExistingEquipment()
        {
            string path=Path.Combine(Path.GetTempPath(),"nereid-slots-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var old=Team("charge");
                old.schemaVersion=5;old.RegisterPlayer("legacy").revision=8;
                old.AddRig("charge",Vector3.up,-1).Detach();
                string payload=Regex.Replace(JsonUtility.ToJson(old),@",?""pocketStacks"":\[[^\]]*\]","");
                string envelope=WriteEnvelope(path,payload);
                var migrated=TeamSave.Load(path);
                var crew=migrated.Player("legacy");
                Assert.That(migrated.schemaVersion,Is.EqualTo(TeamProgress.CurrentSchema));
                Assert.That(crew.revision,Is.EqualTo(8));
                Assert.That(crew.pocketStacks.Length,Is.EqualTo(7));Assert.That(crew.OccupiedPocketExtent,Is.Zero);
                Assert.That(migrated.rigs.Single().serial,Is.EqualTo(1));Assert.That(File.ReadAllText(path),Is.EqualTo(envelope));
                Assert.That(migrated.TakeTechFromRack(crew,"charge",2,()=>{TeamSave.Save(path,migrated);return true;},out _),Is.True);
                var reloaded=TeamSave.Load(path);
                Assert.That(reloaded.Player("legacy").pocketStacks[2].count,Is.EqualTo(1));
                Assert.That(reloaded.Player("legacy").pocketStacks[2].kind,Is.EqualTo("charge"));
                Assert.That(reloaded.rigs.Single().serial,Is.EqualTo(1));
                Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(envelope));
            }
            finally{File.Delete(path);File.Delete(path+".bak");}
        }

        [Test] public void SchemaSixPreservesBothOwnersAndSparsePositionsThenRoundTripsTheSeventhPocket()
        {
            string path=Path.Combine(Path.GetTempPath(),"nereid-v6-pockets-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var old=Team("charge","trap");
                old.schemaVersion=6;
                var first=old.RegisterPlayer("diver");
                var second=old.RegisterPlayer("buddy");
                first.revision=17;
                second.revision=3;
                first.pocketStacks=new TechSlot[]{new(){kind="trap",count=1},default,default,new(){kind="charge",count=2}};
                second.pocketStacks=new TechSlot[]{default,new(){kind="charge",count=1},default,default};
                string envelope=WriteEnvelope(path,LegacyV6Payload(old));

                var migrated=TeamSave.Load(path);
                var diver=migrated.Player("diver");
                var buddy=migrated.Player("buddy");
                Assert.That(migrated.schemaVersion,Is.EqualTo(7));
                Assert.That(diver.revision,Is.EqualTo(17));Assert.That(buddy.revision,Is.EqualTo(3));
                Assert.That(diver.OccupiedPocketExtent,Is.EqualTo(4));Assert.That(buddy.OccupiedPocketExtent,Is.EqualTo(2));
                Assert.That(diver.pocketStacks.Select(s=>s.count),Is.EqualTo(new[]{1,0,0,2,0,0,0}));
                Assert.That(diver.pocketStacks[0].kind,Is.EqualTo("trap"));Assert.That(diver.pocketStacks[3].kind,Is.EqualTo("charge"));
                Assert.That(buddy.pocketStacks.Select(s=>s.count),Is.EqualTo(new[]{0,1,0,0,0,0,0}));
                Assert.That(buddy.pocketStacks[1].kind,Is.EqualTo("charge"));
                Assert.That(File.ReadAllText(path),Is.EqualTo(envelope));

                Assert.That(migrated.DeployTechFromSlot(diver,3,Vector3.one,9,()=>true,out _),Is.True);
                int serial=migrated.rigs.Single().serial;
                Assert.That(migrated.StoreTechInSlot(diver,serial,6,9,()=>false,out _),Is.False);
                Assert.That(diver.OccupiedPocketExtent,Is.EqualTo(4));Assert.That(migrated.rigs.Single().serial,Is.EqualTo(serial));
                bool Save(){TeamSave.Save(path,migrated);return true;}
                Assert.That(migrated.StoreTechInSlot(diver,serial,6,9,Save,out _),Is.True);
                var stored=TeamSave.Load(path);
                var owner=stored.Player("diver");
                Assert.That(owner.revision,Is.EqualTo(19));Assert.That(owner.OccupiedPocketExtent,Is.EqualTo(7));
                Assert.That(owner.pocketStacks.Select(s=>s.count),Is.EqualTo(new[]{1,0,0,1,0,0,1}));
                Assert.That(owner.pocketStacks[6].kind,Is.EqualTo("charge"));Assert.That(stored.rigs,Is.Empty);
                Assert.That(stored.Player("buddy").pocketStacks.Select(s=>s.count),Is.EqualTo(new[]{0,1,0,0,0,0,0}));
                Assert.That(stored.PublicView().players,Is.Empty);
                var written=JsonUtility.FromJson<Envelope>(File.ReadAllText(path)).payload;
                StringAssert.Contains("\"pocketStacks\"",written);StringAssert.DoesNotContain("\"techSlots\"",written);
                Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(envelope));
            }
            finally{File.Delete(path);File.Delete(path+".bak");}
        }

        [TestCase(3)]
        [TestCase(5)]
        public void MalformedV6InventoryIsRejectedAndTheSourceIsPreserved(int length)
        {
            string path=Path.Combine(Path.GetTempPath(),"nereid-bad-pockets-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                var old=Team("charge");
                old.schemaVersion=6;
                old.RegisterPlayer("diver").pocketStacks=new TechSlot[length];
                string envelope=WriteEnvelope(path,LegacyV6Payload(old));
                Assert.Throws<InvalidDataException>(()=>TeamSave.Load(path));
                Assert.That(File.ReadAllText(path),Is.EqualTo(envelope));
            }
            finally{File.Delete(path);}
        }

        static string LegacyV6Payload(TeamProgress old) =>
            JsonUtility.ToJson(old).Replace("\"pocketStacks\":","\"techSlots\":");

        static string WriteEnvelope(string path,string payload)
        {
            using var sha=SHA256.Create();
            string envelope=JsonUtility.ToJson(new Envelope{format="RAID.TeamProgress",payload=payload,
                sha256=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)))});
            File.WriteAllText(path,envelope);
            return envelope;
        }

        [Test] public void APartiallyDrainedCompressorCannotRefillByPassingThroughAStack()
        {
            var team=Team("oxygen");
            team.techPurchases.Add(new TechPurchase{item="oxygen",upgrade="capacity",rank=1});
            var owner=team.RegisterPlayer("diver");
            var station=team.AddRig("oxygen",Vector3.zero,3);
            int capacity=team.TechUnitCapacity("oxygen");
            station.air=capacity-.25f;
            var view=team.PublicView();
            Assert.That(view.CanPackInvention(view.rigs.Single(),3,out string hint),Is.False);
            Assert.That(team.StoreTechInSlot(owner,station.serial,0,3,()=>true,out string reason),Is.False);
            Assert.That(hint,Is.EqualTo(reason));
            StringAssert.Contains("заправьте",reason);
            Assert.That(station.air,Is.EqualTo(capacity-.25f));
            Assert.That(team.rigs.Single(),Is.SameAs(station));
            Assert.That(owner.pocketStacks[0].count,Is.Zero);Assert.That(owner.revision,Is.Zero);
            station.air=capacity;
            view=team.PublicView();
            Assert.That(view.CanPackInvention(view.rigs.Single(),2,out _),Is.False);
            Assert.That(view.CanPackInvention(view.rigs.Single(),3,out _),Is.True);
            Assert.That(team.StoreTechInSlot(owner,station.serial,0,3,()=>true,out _),Is.True);
            Assert.That(owner.pocketStacks[0].count,Is.EqualTo(1));Assert.That(team.rigs,Is.Empty);
        }

        [Serializable] sealed class Envelope{public string format,payload,sha256;}
    }
}
