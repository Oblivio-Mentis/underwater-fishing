using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        int raidCaptive;
        double raidCaptureSafeUntil;
        readonly SyncVar<int> raidGripView=new();
        readonly SyncVar<int> raidBreakSequence=new();
        int seenRaidBreak;
        float raidBreakAt=-10;
        internal int RaidCaptive=>raidCaptive;
        internal int RaidGripVisual=>Mathf.Abs(raidGripView.Value);
        internal bool CanBreakRaidGrip=>raidGripView.Value<0;
        internal bool CanBeRaidCaptive=>raidCaptive==0 && Time.timeAsDouble>=raidCaptureSafeUntil;

        void ResetRaidGrip()
        {
            raidCaptive=0;
            raidCaptureSafeUntil=0;
            raidGripView.Value=0;
            raidBreakSequence.Value=0;
        }

        internal bool BeginRaidCarry(int id,bool solo)
        {
            if(!IsServerInitialized || IsDead || IsInside || IsHelpless || IsTrapped || !CanBeRaidCaptive || id<=0)return false;
            raidCaptive=id;
            raidGripView.Value=solo?-id:id;
            return true;
        }

        internal void EndRaidCarry(int id)
        {
            if(raidCaptive!=id)return;
            raidCaptive=0;
            raidGripView.Value=0;
            raidCaptureSafeUntil=Time.timeAsDouble+6;
        }

        internal void PresentRaidBreak(Vector3 away)
        {
            if(!IsServerInitialized)return;
            ApplyTetherMotion(away*.55f);
            swimVelocity=away*3.2f;
            raidBreakSequence.Value++;
        }

        internal void ConsumeBySquid()
        {
            if(IsServerInitialized && !IsDead)BeginDeath(true);
        }

        internal bool RaidStrugglePose(out float brace,out float push)
        {
            if(seenRaidBreak!=raidBreakSequence.Value)
            {
                seenRaidBreak=raidBreakSequence.Value;
                raidBreakAt=Time.unscaledTime;
            }
            bool caught=RaidGripVisual!=0;
            float progress=0;
            if(caught && TryGetComponent<RescueTool>(out var rescue) && rescue.DisplayTargetOwnerId==OwnerId)
                progress=rescue.DisplayProgress;
            brace=caught?Mathf.Lerp(.22f,1,progress):0;
            float t=Mathf.Clamp01((Time.unscaledTime-raidBreakAt)/.58f);
            push=t<1?Mathf.SmoothStep(0,1,t/.18f)*(1-Mathf.SmoothStep(0,1,(t-.20f)/.8f)):0;
            return caught || push>.001f;
        }

        internal static void RaidStruggleTorso(float brace,float kick,out Vector3 offset,out Quaternion pose)
        {
            float strain=Mathf.Sin(Time.time*10.9f);
            offset=new Vector3(0,.08f*brace,.12f*kick);
            pose=Quaternion.Euler(13-27*kick+strain*6,5*strain,17*strain*(.65f+.33f*brace));
        }
    }
}
