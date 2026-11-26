using NUnit.Framework;
namespace Raid.Tests
{
    public class FishFlowStopTests
    {
        [TestCase(0f,16,0)]
        [TestCase(.899f,16,0)]
        [TestCase(.9f,16,1)]
        [TestCase(1.029f,16,1)]
        [TestCase(1.03f,16,2)]
        [TestCase(1.21f,16,3)]
        [TestCase(1.46f,16,4)]
        [TestCase(1.819f,16,4)]
        [TestCase(1.82f,16,5)]
        [TestCase(4.22f,16,16)]
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
