using UnityEngine;

namespace Raid.Nereid
{
    internal sealed class GoshaSwimRoute
    {
        const float Radius=.23f;
        readonly RaycastHit[] hits=new RaycastHit[32];
        Vector3 avoiding;
        float keepHeadingUntil;

        public void Reset(){avoiding=Vector3.zero;keepHeadingUntil=0;}

        public bool Clear(Vector3 from,Vector3 to)
        {
            var delta=to-from;
            if(delta.sqrMagnitude<.000001f)return true;
            int count=Physics.SphereCastNonAlloc(from,Radius,delta.normalized,hits,delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var c=hits[i].collider;
                if(c.GetComponentInParent<DiverMotor>()==null && c.GetComponentInParent<FishActor>()==null &&
                    !c.TryGetComponent<GoshaBallBody>(out _))return false;
            }
            return count<hits.Length;
        }

        public static float Risk(Vector3 p)
        {
            var threats=ThreatDirector.Instance;
            if(threats==null)return 0;
            float danger=threats.NearbyDanger(p);
            var r=threats.Reef;
            if(r.Active)danger=Mathf.Max(danger,1-Mathf.InverseLerp(1.6f,4.7f,Vector3.Distance(p,ThreatDirector.ClamBed)));
            if(r.Active && r.Deep)danger=Mathf.Max(danger,1-Mathf.InverseLerp(2.7f,6f,Vector3.Distance(p,ThreatDirector.Rift)));
            return danger;
        }

        public Vector3 Step(Vector3 from,Vector3 goal,float distance,bool detour,bool cautious)
        {
            var delta=goal-from;
            if(delta.sqrMagnitude<.000001f)return from;
            var towards=delta.normalized;
            float step=Mathf.Min(distance,delta.magnitude);
            float ahead=detour?Mathf.Min(delta.magnitude,3f):step;
            float startingRisk=cautious?Risk(from):0;
            bool Open(Vector3 direction)
            {
                var end=from+direction*ahead;
                if(!Clear(from,end))return false;
                if(!cautious)return true;
                float risk=Mathf.Max(Risk(Vector3.Lerp(from,end,.5f)),Risk(end));
                // If a predator swims up to Gosha, let him escape to a safer position.
                return risk<.68f || risk<startingRisk-.004f;
            }
            if(Time.time<keepHeadingUntil && detour && Open(avoiding))return from+avoiding*step;
            if(Open(towards))return from+towards*step;
            if(!detour)return from;
            // Check port, starboard, ascent and descent around the obstruction. Pick the open
            // direction that retains the most forward progress and keep it through the turn.
            var right=Vector3.Cross(Vector3.up,towards);
            if(right.sqrMagnitude<.001f)right=Vector3.right;
            right.Normalize();var up=Vector3.Cross(towards,right);
            float best=float.NegativeInfinity;Vector3 chosen=Vector3.zero;
            for(int ring=1;ring<=3;ring++)
            {
                float angle=(ring==3?103:ring*34)*Mathf.Deg2Rad;
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI*.25f;
                    var side=right*Mathf.Cos(a)+up*Mathf.Sin(a);
                    var candidate=towards*Mathf.Cos(angle)+side*Mathf.Sin(angle);
                    if(!Open(candidate))continue;
                    float score=Vector3.Dot(towards,candidate)+candidate.y*.035f;
                    if(cautious)score-=Risk(from+candidate*ahead)*.9f;
                    if(score>best){best=score;chosen=candidate;}
                }
                if(best>float.NegativeInfinity)break;
            }
            avoiding=chosen;keepHeadingUntil=Time.time+.62f;
            return from+chosen*step;
        }
    }
}
