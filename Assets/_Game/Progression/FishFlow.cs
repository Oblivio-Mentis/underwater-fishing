namespace Raid
{
    public static class FishFlow
    {
        public const float EntrySeconds=.9f, DeliverySeconds=8f, ProcessOutlet=4.7f, FeedOutlet=7.02f, GrindAt=7.56f;
        static readonly float[] Group={0,.13f,.31f,.56f};

        public static float LaunchAt(int index) => (index/Group.Length)*.92f+Group[index%Group.Length];

        public static int Started(float age,int count)
        {
            if(count<=0 || float.IsNaN(age) || age<0)return 0;
            int first=0,last=count;
            while(first<last)
            {
                int middle=first+(last-first)/2;
                if(LaunchAt(middle)<=age+.00001f)first=middle+1;
                else last=middle;
            }
            return first;
        }

        // A shut valve returns fish still approaching the inlet and drains those already inside.
        public static int EnteredPipe(float age,int count) => Started(age-EntrySeconds,count);
    }
}
