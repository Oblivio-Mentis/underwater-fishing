using System;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        readonly RaycastHit[] escapeSightHits=new RaycastHit[20];

        void TickDefenseEscape(int index,DefenseAttacker a,float dt,bool carrying)
        {
            AttackerPose pose=carrying?AttackerPose.Carry:AttackerPose.Retreat;
            if(a.data.pose!=pose)
            {
                SetPose(a,pose,0);
                a.unseenSince=-1;
            }
            Vector3 origin=carrying?a.data.snatchPoint:DefenseCenter;
            if(!a.hasFleeRoute && DefenseClock>=a.nextFleeSearch)
            {
                a.nextFleeSearch=DefenseClock+.30;
                a.hasFleeRoute=FindDefenseEscape(a,origin,out a.fleeGoal);
                if(a.hasFleeRoute)a.nextRoute=0;
            }
            float age=(float)(DefenseClock-(carrying?a.data.stolenAt:a.data.poseAt));
            float sprint=carrying?defenseTuning.escapeSpeedMultiplier:1.10f;
            float speed=a.card.burstSpeed*sprint*Mathf.SmoothStep(.35f,1,age/.72f);
            if(a.hasFleeRoute && (Steer(a,a.fleeGoal,speed,dt,true) ||
                a.life%1.5f<dt && !DefensePathClear(a.data.position,a.fleeGoal)))
                a.hasFleeRoute=false;

            float minimumEscapeAge=carrying?1.15f:defenseTuning.retreatSeconds;
            if(DefenseClock<a.nextSightTest || age<minimumEscapeAge ||
                (a.data.position-origin).sqrMagnitude<25) return;
            a.nextSightTest=DefenseClock+.15;
            Vector3 nose=a.data.position+a.data.forward*(.7f*a.data.scale);
            if(CrewSeesEscape(a.data.position) || CrewSeesEscape(nose))
            {
                a.unseenSince=-1;
                return;
            }
            if(a.unseenSince<0)a.unseenSince=DefenseClock;
            if(DefenseClock-a.unseenSince>=.48)RemoveAttacker(index,false,carrying);
        }

        bool FindDefenseEscape(DefenseAttacker a,Vector3 origin,out Vector3 goal)
        {
            Vector3 start=a.data.position;
            var away=Vector3.ProjectOnPlane(start-origin,Vector3.up);
            if(away.sqrMagnitude<.25f)away=Vector3.ProjectOnPlane(a.exit-origin,Vector3.up);
            if(away.sqrMagnitude<.01f)away=Vector3.ProjectOnPlane(a.data.forward,Vector3.up);
            away.Normalize();
            goal=default;
            float best=float.NegativeInfinity;
            for(int i=0;i<16;i++)
            {
                float angle=((i%8+1)/2)*(i%2==0?-38:38);
                var direction=Quaternion.AngleAxis(angle,Vector3.up)*away;
                float reach=i<8?16:7;
                var candidate=start+direction*reach+Vector3.up*((i%3) switch {0=>-.65f,1=>1.55f,_=>3.2f});
                if(!MarineSpawn.InOpenWater(depthTier,candidate,defenseTuning.sceneryClearance+.15f) ||
                    !DefensePathClear(start,candidate))continue;
                float score=Vector3.Dot(direction,away)*2+reach*.03f+
                    (CrewSeesEscape(candidate)?0:5);
                if(score<=best)continue;
                best=score;
                goal=candidate;
            }
            return best>float.NegativeInfinity;
        }

        bool CrewSeesEscape(Vector3 point)
        {
            float range=defenseTuning.escapeSightDistance;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!diver || !diver.IsServerInitialized || diver.IsDead || diver.gameObject.scene!=gameObject.scene)continue;
                Vector3 eye=diver.transform.position+Vector3.up*.60f;
                Vector3 delta=point-eye;
                if(delta.sqrMagnitude>range*range)continue;
                if(delta.sqrMagnitude<2.25f)return true;
                if(Vector3.Dot(diver.AimDirection,delta.normalized)<.23f)continue;
                int hits=Physics.RaycastNonAlloc(eye,delta.normalized,escapeSightHits,delta.magnitude-.20f,
                    obstacleMask,QueryTriggerInteraction.Ignore);
                if(hits>=escapeSightHits.Length)return true;
                bool obstructed=false;
                for(int i=0;i<hits;i++)
                {
                    var collider=escapeSightHits[i].collider;
                    if(collider.GetComponentInParent<DiverMotor>() || IsObservationGlass(collider.transform))continue;
                    obstructed=true;
                    break;
                }
                if(!obstructed)return true;
            }
            return false;
        }

        static bool IsObservationGlass(Transform part)
        {
            for(int i=0;i<3 && part;i++,part=part.parent)
                if(part.name.Contains("observation window",StringComparison.OrdinalIgnoreCase) ||
                    part.name.Contains("walkable glass",StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
