using NUnit.Framework;
using UnityEngine;

namespace Raid.Tests
{
    public sealed class IndustrialHarvestTests
    {
        static CatchItem Fish(long id) => new CatchItem(id,2,1,1,1,1,species:"shallow-small");

        [Test] public void NurseryKeepsTwoBreedersAndNeverQueuesIncomeWhenFull()
        {
            var team=new TeamProgress();
            var rig=new TechRig{kind="farm",depth=1,care=30f};
            rig.brood.Add(Fish(91));rig.brood.Add(Fish(92));
            Assert.IsTrue(IndustryManifest.AdvanceFarm(team,rig,300,10,4,false));
            Assert.AreEqual(2,rig.contents.Count);
            Assert.AreNotEqual(rig.contents[0].id,rig.contents[1].id);
            Assert.AreEqual(2,rig.brood.Count);
            Assert.AreEqual(0,rig.progress);
            rig.contents.Clear();
            Assert.IsFalse(IndustryManifest.AdvanceFarm(team,rig,300,10,4,false));
            Assert.AreEqual(0,rig.contents.Count);
            Assert.IsTrue(IndustryManifest.AdvanceFarm(team,rig,10,10,4,true));
            Assert.AreEqual(1,rig.contents.Count);
        }

        [Test] public void CapsuleWaitsAtFullReservoirAndSettlesEachIdOnceAfterSerialization()
        {
            var tune=ScriptableObject.CreateInstance<ProgressionTuning>();
            try
            {
                tune.reservoirCapacity=1;
                var team=new TeamProgress(tune);
                var capsule=new TechRig{serial=1,kind="mail",depth=1,flight="transit"};
                capsule.contents.Add(Fish(2));capsule.contents.Add(Fish(3));
                team.rigs.Add(capsule);
                Assert.AreEqual(1,IndustryManifest.Arrive(team,capsule,tune));
                var restored=JsonUtility.FromJson<TeamProgress>(JsonUtility.ToJson(team));
                var remaining=restored.rigs[0];
                Assert.AreEqual(0,IndustryManifest.Arrive(restored,remaining,tune));
                Assert.AreEqual(3,remaining.contents[0].id);
                Assert.IsTrue(restored.Process(2));
                Assert.AreEqual(1,IndustryManifest.Arrive(restored,remaining,tune));
                Assert.AreEqual(0,IndustryManifest.Arrive(restored,remaining,tune));
                CollectionAssert.AreEquivalent(new long[]{2,3},restored.deliveredIds);
            }
            finally{Object.DestroyImmediate(tune);}
        }
    }
}
