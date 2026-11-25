using System;
using System.Linq;
using NUnit.Framework;
using OblivioMentis.UniversalFSM;
using UnityEditor;

namespace Raid.Tests
{
    public class FishingLineTests
    {
        sealed class LineOwner { public int Id; }
        sealed class Hooks : FishingHooks<LineOwner>
        {
            protected override int GetKeyForItem(LineOwner line) => line.Id;
        }

        [Test] public void DistinctConnectionsShareOneFishUpToTheCrewLimit()
        {
            var lines=new Hooks();
            for(int i=0;i<5;i++) Assert.That(lines.Attach(new LineOwner {Id=i+40}),Is.True);
            Assert.That(lines.Attach(new LineOwner {Id=50}),Is.False);
            lines.RemoveAt(0);
            Assert.That(lines.At(0).Id,Is.EqualTo(41));
            Assert.That(lines.Attach(new LineOwner {Id=41}),Is.False);
            Assert.That(lines.Attach(new LineOwner {Id=50}),Is.True);
            lines.Clear();
            Assert.That(lines.Attach(new LineOwner {Id=41}),Is.True);
        }

        [TestCase("Struggling")]
        [TestCase("Tired")]
        public void ShippedGraphCanLandFromEitherFightState(string mode)
        {
            var asset=AssetDatabase.LoadAssetAtPath<UniversalFsmDefinitionAsset>("Assets/_Game/Data/Fishing/FishGraph.asset");
            Assert.That(asset.Validate("RaidFishing",Type.GetType("Raid.FishContext, Assembly-CSharp",true)).IsValid,Is.True);
            var graph=asset.Definition;
            int source=graph.states.Single(s=>s.name==mode).id, captured=graph.states.Single(s=>s.name=="Captured").id;
            var checks=graph.condition_nodes.Where(c=>c.variable_id=="DesiredState" && c.compare_value==captured
                && c.compare==UniversalCompareOperator.Equal).Select(c=>c.node_uid).ToHashSet();
            var gates=graph.edges.Where(e=>e.from_state_id==source && checks.Contains(e.to_condition_uid)).Select(e=>e.to_condition_uid);
            Assert.That(gates.Any(uid=>graph.edges.Any(e=>e.from_condition_uid==uid
                && graph.states.Any(s=>s.id==e.to_state_id && (s.id==captured || s.mirror_of_id==captured)))),Is.True);
        }

        [Test] public void SwimmingToTheHookCannotWindOrLandIt()
        {
            var line = new FishingLine(5);
            for (int i = 0; i < 100; i++) line.Wind(.3f, .12f, 3, .02f);
            Assert.That(line.PaidOut, Is.EqualTo(5));
            Assert.That(line.AtHand(.3f, .7f, .35f), Is.False);
        }

        [Test] public void EvenAtContactLandingRequiresTimeUnderLoad()
        {
            var line = new FishingLine(.18f);
            Assert.That(line.AtHand(.18f, .7f, .35f), Is.False);
            line.Wind(.18f, .12f, 2, .1f);
            Assert.That(line.AtHand(.18f, .7f, .35f), Is.False);
            line.Wind(.18f, .12f, 2, .3f);
            Assert.That(line.AtHand(.18f, .7f, .35f), Is.True);
            line.Slip(2);
            Assert.That(line.AtHand(.18f, .7f, .35f), Is.False);
        }

        [Test] public void HeavyTowContinuesUntilACoordinatedPairExists()
        {
            Assert.That(FishingLine.RequiresSoloTow(true,false),Is.True);
            Assert.That(FishingLine.RequiresSoloTow(true,true),Is.False);
            Assert.That(FishingLine.RequiresSoloTow(false,false),Is.False);
        }

        [Test] public void HeavyLandingRequiresCoordinationAndTwoHooks()
        {
            Assert.That(FishingLine.CanLandHeavy(false,2),Is.False);
            Assert.That(FishingLine.CanLandHeavy(true,1),Is.False);
            Assert.That(FishingLine.CanLandHeavy(true,2),Is.True);
            Assert.That(FishingLine.CanLandHeavy(true,5),Is.True);
        }
    }
}
