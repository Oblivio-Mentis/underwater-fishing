using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        static readonly Vector3 SquidBeak = new(0,-.12f,.54f);

        static Vector3 SquidMouth(AttackerSnapshot squid) =>
            squid.position+Quaternion.LookRotation(squid.forward)*SquidBeak*squid.scale;

        static Vector3 CaptiveChest(DiverMotor victim) => victim.transform.position+Vector3.up*.62f;

        bool TonguePathClear(Vector3 from,Vector3 to,DiverMotor victim)
        {
            if(!ClearDefenseLine(from,to,victim))return false;
            if(Defense.breached)return true;
            for(int i=0;i<=6;i++)
                if(ShieldContains(Vector3.Lerp(from,to,i/6f)))return false;
            return true;
        }

        void BeginTongueWindup(DefenseAttacker a,Vector3 chest,float dt)
        {
            Vector3 mouth=SquidMouth(a.data);
            float distance=Vector3.Distance(mouth,chest);
            if(!a.target.CanBeRaidCaptive)
            {
                a.target=null;
                return;
            }
            if(distance>defenseTuning.squidTongueRange*.96f || !TonguePathClear(mouth,chest,a.target))
            {
                Steer(a,chest,a.card.speed,dt,Defense.breached,a.target);
                return;
            }
            a.data.tongueAim=chest;
            a.data.tongueReleasedAt=0;
            SetPose(a,AttackerPose.Windup,a.card.windup);
        }

        void TickTongue(DefenseAttacker a,float dt)
        {
            double now=DefenseClock;
            var prey=a.target;
            if(a.data.pose==AttackerPose.Windup)
            {
                if(!EligibleVictim(prey) || !prey.CanBeRaidCaptive){EndTonguePass(a);return;}
                var aim=CaptiveChest(prey);
                if(now<a.until-.19)
                    a.data.tongueAim=Vector3.Lerp(a.data.tongueAim,aim,1-Mathf.Exp(-dt*10));
                a.data.forward=Vector3.RotateTowards(a.data.forward,(a.data.tongueAim-a.data.position).normalized,dt*7,1);
                if(now<a.until)return;
                Vector3 origin=SquidMouth(a.data);
                a.data.tongueAim=origin+Vector3.ClampMagnitude(a.data.tongueAim-origin,defenseTuning.squidTongueRange);
                SetPose(a,AttackerPose.Tongue,defenseTuning.squidTongueSeconds);
            }

            Vector3 mouth=SquidMouth(a.data);
            float age=(float)(now-a.data.poseAt);
            float t0=TongueTravel((age-dt)/defenseTuning.squidTongueSeconds);
            float t1=TongueTravel(age/defenseTuning.squidTongueSeconds);
            Vector3 from=Vector3.Lerp(mouth,a.data.tongueAim,t0);
            Vector3 tip=Vector3.Lerp(mouth,a.data.tongueAim,t1);
            if(!TonguePathClear(mouth,tip,prey))
            {
                a.data.tongueAim=from;
                EndTonguePass(a);
                return;
            }
            if(EligibleVictim(prey) && prey.CanBeRaidCaptive)
            {
                var chest=CaptiveChest(prey);
                var sweep=tip-from;
                float fraction=Mathf.Clamp01(Vector3.Dot(chest-from,sweep)/Mathf.Max(.0001f,sweep.sqrMagnitude));
                if((chest-from-sweep*fraction).sqrMagnitude<.57f*.57f &&
                    TonguePathClear(mouth,chest,prey) && TryCapture(a,prey))return;
            }
            if(now>=a.until)EndTonguePass(a);
        }

        static float TongueTravel(float t)
        {
            t=Mathf.Clamp01(t);
            return t*(1.85f-.85f*t);
        }

        void EndTonguePass(DefenseAttacker a)
        {
            a.data.tongueReleasedAt=DefenseClock;
            a.counter=false;
            a.target=null;
            a.goal=a.data.position-a.data.forward*4.2f+Vector3.up*.65f;
            SetPose(a,AttackerPose.Retreat,a.card.cooldown);
        }
    }
}
