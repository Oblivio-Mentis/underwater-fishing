namespace Raid
{
    public sealed partial class DiverMotor
    {
        int raidCaptive;
        internal int RaidCaptive=>raidCaptive;

        internal bool BeginRaidCarry(int id)
        {
            if(!IsServerInitialized || IsDead || IsInside || IsHelpless || IsTrapped || id<=0)return false;
            raidCaptive=id;
            return true;
        }

        internal void EndRaidCarry(int id)
        {
            if(raidCaptive==id)raidCaptive=0;
        }
    }
}
