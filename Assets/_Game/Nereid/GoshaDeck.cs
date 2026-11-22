using UnityEngine;

namespace Raid.Nereid
{
    // Waypoints in authored hull coordinates. The middle pair crosses the aft partition
    // through its central opening; the two aisles skirt the cylindrical reservoir.
    internal sealed class GoshaDeck
    {
        readonly Vector3[] marks = {
            new(4,.14f,-14), new(0,.14f,-14), new(0,.14f,-9), new(0,.14f,-7),
            new(3,.14f,-6), new(3,.14f,-2), new(3,.14f,1.6f), new(0,.14f,2),
            new(-3,.14f,1.6f), new(-3,.14f,-2), new(-3,.14f,-6)
        };
        readonly int[][] links = {
            new[]{1},new[]{0,2},new[]{1,3},new[]{2,4,10},new[]{3,5},new[]{4,6},
            new[]{5,7},new[]{6,8},new[]{7,9},new[]{8,10},new[]{9,3}
        };
        readonly RaycastHit[] hits=new RaycastHit[24];
        readonly int[] previous=new int[11],queue=new int[11];
        public Vector3 Mark(int id)=>NereidVessel.Active.transform.TransformPoint(marks[id]);
        public int Count=>marks.Length;

        public Vector3 RecoveryMark(Vector3 position)
        {
            Vector3 nearest=Mark(0);
            for(int i=1;i<marks.Length;i++)
                if((Mark(i)-position).sqrMagnitude<(nearest-position).sqrMagnitude)nearest=Mark(i);
            return nearest;
        }

        public bool Interior(Vector3 world)
        {
            var vessel=NereidVessel.Active;
            if(vessel==null || !vessel.ContainsSealed(world) || NereidAirlock.Active.Contains(world))return false;
            Vector3 p=vessel.transform.InverseTransformPoint(world);
            return p.z<6.6f && p.y<2.0f && (new Vector2(p.x,p.z+3)).sqrMagnitude>3.75f;
        }

        public Vector3 Ground(Vector3 point)
        {
            // Use the main walking deck; upper galleries are outside this pet's route.
            point.y=Mark(0).y;
            return point;
        }

        public bool Clear(Vector3 from,Vector3 to,float radius=.15f)
        {
            var delta=to-from;
            if(delta.sqrMagnitude<.0001f)return true;
            int count=Physics.SphereCastNonAlloc(from,radius,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
                if(hits[i].collider.GetComponentInParent<DiverMotor>()==null && !hits[i].collider.TryGetComponent<GoshaBallBody>(out _))return false;
            return count<hits.Length;
        }

        int NearestVisible(Vector3 point)
        {
            int best=-1; float distance=float.PositiveInfinity;
            for(int i=0;i<marks.Length;i++)
            {
                var pos=Mark(i);float d=(pos-point).sqrMagnitude;
                if(d<distance&&Clear(point,pos)){best=i;distance=d;}
            }
            return best;
        }

        public Vector3 Next(Vector3 from,Vector3 to)
        {
            to=Ground(to);
            if(!Interior(to))return from;
            if(Clear(from,to))return to;
            int start=NearestVisible(from),end=NearestVisible(to);
            if(start<0||end<0)return from;
            if(start==end)return Mark(start);
            System.Array.Fill(previous,-1);int front=0,back=1;
            queue[0]=start;previous[start]=start;
            while(front<back)
            {
                int current=queue[front++];
                foreach(int n in links[current])
                {
                    if(previous[n]>=0 || !Clear(Mark(current),Mark(n)))continue;
                    previous[n]=current;queue[back++]=n;
                    if(n!=end)continue;
                    int next=n;
                    while(previous[next]!=start)next=previous[next];
                    return Clear(from,Mark(next))?Mark(next):Mark(start);
                }
            }
            return from;
        }
    }
}
