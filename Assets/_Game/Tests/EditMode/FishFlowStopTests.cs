using NUnit.Framework;
namespace Raid.Tests
{
    public class FishFlowStopTests
    {
        [TestCase(0f,16,0)]
        [TestCase(.899f,16,0)]
        [TestCase(.9f,16,1)]
        [TestCase(1.079f,16,1)]
        [TestCase(1.08f,16,2)]
        [TestCase(1.26f,16,3)]
        [TestCase(3.6f,16,16)]
        [TestCase(100f,16,16)]
        [TestCase(100f,0,0)]
        public void ClosingValveReservesOnlyFishAlreadyInsidePipe(float age,int stock,int expected)
        {
            Assert.AreEqual(expected,FishFlow.EnteredPipe(age,stock));
        }
        [Test] public void InvalidAgeCannotReserveStock()
        {
            Assert.AreEqual(0,FishFlow.EnteredPipe(float.NaN,16));
            Assert.AreEqual(0,FishFlow.EnteredPipe(-1,16));
        }
    }
}
