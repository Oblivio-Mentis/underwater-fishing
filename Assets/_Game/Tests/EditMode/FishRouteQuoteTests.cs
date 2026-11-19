using NUnit.Framework;
namespace Raid.Tests
{
    public class FishRouteQuoteTests
    {
        [Test] public void EqualCountReplacementInvalidatesFishConfirmation()
        {
            var a=new CatchItem(11,2,0,3,1,1);var b=new CatchItem(12,6,1,4,1,2);
            long quote=FishRouteQuote.Compute(new[]{a,b},12);
            Assert.AreNotEqual(quote,FishRouteQuote.Compute(new[]{a,new CatchItem(13,6,1,4,1,2)},12));
        }
        [Test] public void YieldChangeAndNewFoodRequirementInvalidateConfirmation()
        {
            var fish=new CatchItem(11,2,0,3,1,1);long quote=FishRouteQuote.Compute(new[]{fish},12);
            fish.biomass=8;Assert.AreNotEqual(quote,FishRouteQuote.Compute(new[]{fish},12));
            fish.biomass=2;Assert.AreNotEqual(quote,FishRouteQuote.Compute(new[]{fish},18));
        }
        [Test] public void DisplayOrderAndUnrelatedTechnicalStockDoNotInvalidateFishConfirmation()
        {
            var a=new CatchItem(11,2,0,3,1,1);var b=new CatchItem(12,6,1,4,1,2);
            long quote=FishRouteQuote.Compute(new[]{a,b},12);
            Assert.AreEqual(quote,FishRouteQuote.Compute(new[]{b,new CatchItem(99,0,0,0,1,2,4,2),a},12));
        }
    }
}
