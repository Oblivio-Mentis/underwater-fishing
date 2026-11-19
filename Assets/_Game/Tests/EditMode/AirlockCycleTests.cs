using NUnit.Framework;

namespace Raid.Tests
{
    public sealed class AirlockCycleTests
    {
        [Test]
        public void DoorsInterlockThroughoutFloodAndPumpOut()
        {
            var state=AirlockSnapshot.Dry;
            foreach(bool sea in new[]{true,false})
            {
                Assert.That(state.Request(sea),Is.True);
                Assert.That(state.Request(!sea),Is.False);
                for(int i=0;i<480;i++)
                {
                    state.Step(.02f,false,false);
                    Assert.That(state.Inner==0||state.Outer==0,Is.True);
                    if(state.Water>0&&state.Water<1)Assert.That(state.Inner+state.Outer,Is.Zero);
                    if(state.Inner>0)Assert.That(state.Water,Is.Zero);
                    if(state.Outer>0)Assert.That(state.Water,Is.EqualTo(1));
                }
                Assert.That(state.Busy,Is.False);
                Assert.That(state.CanRequest(!sea),Is.True);
            }
        }

        [Test]
        public void OccupiedThresholdKeepsPressureOnCurrentSide()
        {
            var state=AirlockSnapshot.Dry;
            state.Request(true);
            for(int i=0;i<200;i++)state.Step(.025f,true,false);
            Assert.That(state.Inner,Is.EqualTo(1));Assert.That(state.Water,Is.Zero);
            state.Step(.4f,false,false);
            state.Step(.4f,true,false);
            Assert.That(state.Inner,Is.EqualTo(1));
            for(int i=0;i<400;i++)state.Step(.025f,false,false);
            state.Request(false);
            for(int i=0;i<200;i++)state.Step(.025f,false,true);
            Assert.That(state.Outer,Is.EqualTo(1));Assert.That(state.Water,Is.EqualTo(1));
        }
    }
}
