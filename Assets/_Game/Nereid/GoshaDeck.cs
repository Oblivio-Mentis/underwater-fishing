using UnityEngine;

namespace Raid.Nereid
{
    // Hull-local service aisles, both approaches to the observation stairs and the raised grating.
    // Edges are discovered from the actual generated colliders; a railing never becomes a shortcut.
    internal sealed class GoshaDeck
    {
        const float Pivot=.14f, StairX=-4.7f, Gallery=1.5f;
        readonly Vector3[] marks={
            new(4,Pivot,-14),new(0,Pivot,-14),new(0,Pivot,-9),new(0,Pivot,-7),
            new(3,Pivot,-6),new(3,Pivot,-2),new(3,Pivot,1.6f),new(0,Pivot,2),
            new(-3,Pivot,1.6f),new(-3,Pivot,-2),new(-3,Pivot,-6),
            new(-3.55f,Pivot,-7.25f),new(-3.55f,Pivot,5.85f),
            new(-4.7f,Pivot,-7.74f),new(-4.7f,.89f,-6),new(-4.7f,Gallery+Pivot,-4.5f),
            new(-4.7f,Gallery+Pivot,-.75f),new(-4.7f,Gallery+Pivot,3),new(-4.7f,.89f,4.5f),
            new(-4.7f,Pivot,6.27f),new(-2.75f,Pivot,5.8f),new(0,Pivot,5.75f),
            new(3.2f,Pivot,5.5f),new(-3,Pivot,-13.3f),new(3,Pivot,-11f),
            new(-3.65f,Pivot,2.9f),new(-3.6f,Pivot,-4.65f)
        };
        readonly RaycastHit[] hits=new RaycastHit[32];
        readonly Collider[] overlaps=new Collider[32];
        readonly float[,] edges;
        readonly float[] cost;
        readonly int[] first;
        readonly bool[] visited;
        bool baked;
        public GoshaDeck(){int n=marks.Length;edges=new float[n,n];cost=new float[n];first=new int[n];visited=new bool[n];}
        Transform Hull=>NereidVessel.Active.transform;
        public Vector3 Mark(int id)=>Hull.TransformPoint(marks[id]);
        public int Count=>marks.Length;

        public Vector3 RecoveryMark(Vector3 position)
        {
            Vector3 nearest=Mark(7);float nearestDistance=float.PositiveInfinity;
            for(int i=0;i<marks.Length;i++)
            {
                Vector3 p=Mark(i);float d=(p-position).sqrMagnitude;
                if(d<nearestDistance&&RoomForBody(p,.18f)){nearest=p;nearestDistance=d;}
            }
            return nearest;
        }
        public bool Interior(Vector3 world)
        {
            var vessel=NereidVessel.Active;
            if(vessel==null || !vessel.ContainsSealed(world) || NereidAirlock.Active.Contains(world))return false;
            var p=Hull.InverseTransformPoint(world);
            return p.z<6.72f && p.y<3.35f && (new Vector2(p.x,p.z+3)).sqrMagnitude>3.72f;
        }
        public bool Mouth(Vector3 world)
        {
            var p=Hull.InverseTransformPoint(world);
            return Mathf.Abs(p.x)<2.45f && p.z>6.73f && p.z<10.2f && p.y>-.4f && p.y<3.9f;
        }
        public bool InSea(Vector3 world)
        {
            if(NereidAirlock.Active.Contains(world))return NereidAirlock.Active.IsSubmerged(world);
            if(NereidVessel.Active.ContainsSealed(world)||Mouth(world))return false;
            var p=Hull.InverseTransformPoint(world);
            return Mathf.Abs(p.x)>6.15f || p.z< -16.5f || p.z>10.25f || p.y<-.65f || p.y>4.75f;
        }
        static float Surface(Vector3 p)
        {
            if(Mathf.Abs(p.x-StairX)>.63f || p.z< -7.5f || p.z>6)return 0;
            if(p.z< -4.5f)return Gallery*Mathf.Clamp01((p.z+7.5f)/3);
            if(p.z>3)return Gallery*Mathf.Clamp01((6-p.z)/3);
            // The level below the gallery is traversable between its supports.
            return p.y<Gallery-.32f?0:Gallery;
        }
        public Vector3 Ground(Vector3 world)
        {var p=Hull.InverseTransformPoint(world);p.y=Surface(p)+Pivot;return Hull.TransformPoint(p);}
        static bool Obstacle(Collider c)=>c.GetComponentInParent<DiverMotor>()==null&&!c.TryGetComponent<GoshaBallBody>(out _);
        bool RoomForBody(Vector3 p,float radius)
        {
            int n=Physics.OverlapSphereNonAlloc(p,radius,overlaps,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(Obstacle(overlaps[i]))return false;
            return n<overlaps.Length;
        }
        public bool Clear(Vector3 from,Vector3 to,float radius=.15f)
        {
            var delta=to-from;
            if(delta.sqrMagnitude<.0001f)return true;
            int n=Physics.SphereCastNonAlloc(from,radius,delta.normalized,hits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
            for(int i=0;i<n;i++)if(Obstacle(hits[i].collider))return false;
            return n<hits.Length;
        }
        bool Walkable(Vector3 a,Vector3 b)
        {
            if(!Clear(a,b,.15f))return false;
            // Check the walk surface between endpoints as well as obstacle clearance:
            // a line passing above a stair rail or straight across an open drop is not a path.
            int steps=Mathf.CeilToInt(Vector3.Distance(a,b)/.62f);
            for(int i=1;i<steps;i++)
            {
                var p=Vector3.Lerp(a,b,i/(float)steps);
                if(!Interior(p) || Mathf.Abs(p.y-Ground(p).y)>.22f)return false;
            }
            return true;
        }
        void Bake()
        {
            for(int i=0;i<Count;i++)for(int j=0;j<i;j++)
            {
                var a=Mark(i);var b=Mark(j);
                if(RoomForBody(a,.15f)&&RoomForBody(b,.15f)&&Walkable(a,b))edges[i,j]=edges[j,i]=Vector3.Distance(a,b);
            }
            baked=true;
        }
        bool Route(Vector3 from,Vector3 to,out Vector3 next)
        {
            next=from;
            if(!Interior(to)||!RoomForBody(to,.15f))return false;
            if(Walkable(from,to)){next=to;return true;}
            if(!baked)Bake();
            for(int i=0;i<Count;i++)
            {
                var p=Mark(i);
                cost[i]=Walkable(from,p)?Vector3.Distance(from,p):float.PositiveInfinity;
                first[i]=i;visited[i]=false;
            }
            float best=float.PositiveInfinity;
            for(int k=0;k<Count;k++)
            {
                int node=-1;float closest=best;
                for(int i=0;i<Count;i++)if(!visited[i]&&cost[i]<closest){closest=cost[i];node=i;}
                if(node<0)break;
                visited[node]=true;var p=Mark(node);
                float finish=closest+Vector3.Distance(p,to);
                if(finish<best && Walkable(p,to)){best=finish;next=Mark(first[node]);}
                for(int j=0;j<Count;j++)
                {
                    float edge=edges[node,j];if(visited[j]||edge<=0||closest+edge>=cost[j])continue;
                    cost[j]=closest+edge;first[j]=Vector3.Distance(from,Mark(first[node]))<.2f?j:first[node];
                }
            }
            return !float.IsPositiveInfinity(best);
        }
        public Vector3 Next(Vector3 from,Vector3 target)
        {
            var ground=Ground(target);
            if(Route(from,ground,out var next))return next;
            // A rubber ball can rest against a solid stair cheek with no room for a pet-sized belly.
            // Reach it from an exposed side using the mandibles, with a clear sightline to the ball.
            for(int i=0;i<12;i++)
            {
                float a=i*Mathf.PI/6;
                var lip=Ground(ground+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.53f);
                if(Clear(lip,target,.035f)&&Route(from,lip,out next))return next;
            }
            return from;
        }
    }
}
