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
                Assert.That(owner.techSlots[2].count,Is.EqualTo(limit));
                Assert.That(owner.techSlots[1].count,Is.EqualTo(1));
                Assert.That(observer.techSlots.All(slot=>slot.count==0));
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
            Assert.That(team.Treasury,Is.EqualTo(afterResearch));Assert.That(profile.techSlots[0].count,Is.EqualTo(initial+1));
        }

        [Test] public void FailedAndRepeatedDeployOrReturnCannotDuplicateTheSlotOrFieldUnit()
        {
            var team=Team("charge");
            var owner=team.RegisterPlayer("one");
            Assert.That(team.TakeTechFromRack(owner,"charge",0,()=>false,out _),Is.False);
            Assert.That(owner.revision,Is.Zero);
            Assert.That(team.TakeTechFromRack(owner,"charge",0,()=>true,out _),Is.True);
            Assert.That(team.DeployTechFromSlot(owner,0,Vector3.one,8,()=>false,out _),Is.False);
            Assert.That(owner.techSlots[0].count,Is.EqualTo(1));
            Assert.That(team.rigs,Is.Empty);Assert.That(team.lastRigSerial,Is.Zero);
            Assert.That(team.DeployTechFromSlot(owner,0,Vector3.one,8,()=>true,out _),Is.True);
            Assert.That(owner.techSlots[0].count,Is.Zero);
            Assert.That(team.DeployTechFromSlot(owner,0,Vector3.one,8,()=>true,out _),Is.False);
            var rig=team.rigs.Single();
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,7,()=>true,out _),Is.False);
            rig.contents.Add(new CatchItem(51,1,1,1,1,1));
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.False);
            rig.contents.Clear();rig.health=.5f;
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.False);
            rig.health=1;
            Assert.Throws<IOException>(()=>team.StoreTechInSlot(owner,rig.serial,3,8,()=>throw new IOException("full"),out _));
            Assert.That(team.rigs.Single(),Is.SameAs(rig));Assert.That(owner.techSlots[3].count,Is.Zero);
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.True);
            Assert.That(team.StoreTechInSlot(owner,rig.serial,3,8,()=>true,out _),Is.False);
            Assert.That(owner.techSlots[3].count,Is.EqualTo(1));
            Assert.That(team.rigs,Is.Empty);Assert.That(owner.revision,Is.EqualTo(3));
            Assert.That(team.teamRevision,Is.Zero);Assert.That(team.Treasury,Is.EqualTo(default(MaterialCost)));
        }

        [Test] public void RackPreflightValidatesTheLiveOwnerSlotAndCapacityWithoutIssuingOrWriting()
        {
            var team=Team("charge");
            var owner=team.RegisterPlayer("one");
            Assert.That(team.CanTakeTechFromRack(null,"charge",0,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(new PlayerProgress{key="one"},"charge",0,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"charge",4,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"cage",0,out _),Is.False);
            Assert.That(team.CanTakeTechFromRack(owner,"charge",0,out _),Is.True);
            Assert.That(owner.revision,Is.Zero);Assert.That(owner.techSlots.All(s=>s.count==0));
            owner.techSlots[0]=new TechSlot{kind="charge",count=team.TechStackLimit("charge")};
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
                string payload=Regex.Replace(JsonUtility.ToJson(old),@",?""techSlots"":\[[^\]]*\]","");
                using var sha=SHA256.Create();
                string envelope=JsonUtility.ToJson(new Envelope{format="RAID.TeamProgress",payload=payload,
                    sha256=Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)))});
                File.WriteAllText(path,envelope);
                var migrated=TeamSave.Load(path);
                var crew=migrated.Player("legacy");
                Assert.That(migrated.schemaVersion,Is.EqualTo(6));
                Assert.That(crew.revision,Is.EqualTo(8));
                Assert.That(crew.techSlots.Length,Is.EqualTo(4));Assert.That(crew.techSlots.All(s=>s.count==0));
                Assert.That(migrated.rigs.Single().serial,Is.EqualTo(1));Assert.That(File.ReadAllText(path),Is.EqualTo(envelope));
                Assert.That(migrated.TakeTechFromRack(crew,"charge",2,()=>{TeamSave.Save(path,migrated);return true;},out _),Is.True);
                var reloaded=TeamSave.Load(path);
                Assert.That(reloaded.Player("legacy").techSlots[2].count,Is.EqualTo(1));
                Assert.That(reloaded.Player("legacy").techSlots[2].kind,Is.EqualTo("charge"));
                Assert.That(reloaded.rigs.Single().serial,Is.EqualTo(1));
                Assert.That(File.ReadAllText(path+".bak"),Is.EqualTo(envelope));
            }
            finally{File.Delete(path);File.Delete(path+".bak");}
        }

        [Test] public void APartiallyDrainedCompressorCannotRefillByPassingThroughAStack()
        {
            var team=Team("oxygen");
            team.techPurchases.Add(new TechPurchase{item="oxygen",upgrade="capacity",rank=1});
            var owner=team.RegisterPlayer("diver");
            var station=team.AddRig("oxygen",Vector3.zero,3);
            int capacity=team.TechUnitCapacity("oxygen");
            station.air=capacity-.25f;
            Assert.That(team.StoreTechInSlot(owner,station.serial,0,3,()=>true,out string reason),Is.False);
            StringAssert.Contains("заправьте",reason);
            Assert.That(station.air,Is.EqualTo(capacity-.25f));
            Assert.That(team.rigs.Single(),Is.SameAs(station));
            Assert.That(owner.techSlots[0].count,Is.Zero);Assert.That(owner.revision,Is.Zero);
            station.air=capacity;
            Assert.That(team.StoreTechInSlot(owner,station.serial,0,3,()=>true,out _),Is.True);
            Assert.That(owner.techSlots[0].count,Is.EqualTo(1));Assert.That(team.rigs,Is.Empty);
        }

        [Serializable] sealed class Envelope{public string format,payload,sha256;}
    }
}
