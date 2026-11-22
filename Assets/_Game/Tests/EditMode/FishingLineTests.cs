using NUnit.Framework;

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
    }
}
