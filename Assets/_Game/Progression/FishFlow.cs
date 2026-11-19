using System;
namespace Raid
{
    public static class FishFlow
    {
        public const float EntrySeconds=.9f, Interval=.18f, DeliverySeconds=8f;
        // Fish still approaching the outlet belong in the reservoir when the valve closes.
        public static int EnteredPipe(float age,int count)
        {
            if(count<=0||float.IsNaN(age)||age<EntrySeconds)return 0;
            if(age+.00001f>=EntrySeconds+(count-1)*Interval)return count;
            return Math.Min(count,1+(int)Math.Floor((age-EntrySeconds+.00001f)/Interval));
        }
    }
}
