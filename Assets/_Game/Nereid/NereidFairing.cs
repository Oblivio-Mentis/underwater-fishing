using UnityEngine;

namespace Raid.Nereid
{
    internal static class NereidFairing
    {
        internal static void Join(ReefGeometry mesh,Vector3[] from,Vector3[] to,bool inside=false)
        {
            for(int i=0;i<from.Length;i++)
            {
                int next=(i+1)%from.Length;
                int a=mesh.Add(from[i],Color.white);
                int b=mesh.Add(from[next],Color.white);
                int c=mesh.Add(to[next],Color.white);
                int d=mesh.Add(to[i],Color.white);
                if(inside)mesh.Quad(a,b,c,d);
                else mesh.Quad(b,a,d,c);
            }
        }

        internal static void Cap(ReefGeometry mesh,Vector3[] rim,bool reverse)
        {
            Vector3 center=Vector3.zero;
            foreach(var point in rim)center+=point;
            center/=rim.Length;
            int a=mesh.Add(center,Color.white);
            for(int i=0;i<rim.Length;i++)
            {
                int b=mesh.Add(rim[i],Color.white);
                int c=mesh.Add(rim[(i+1)%rim.Length],Color.white);
                if(reverse)mesh.Face(a,c,b);
                else mesh.Face(a,b,c);
            }
        }

        internal static void Seam(ReefGeometry mesh,Vector3[] rim,float radius)
        {
            var path=new Vector3[rim.Length+1];
            var thickness=new float[path.Length];
            for(int i=0;i<path.Length;i++)
            {
                path[i]=rim[i%rim.Length];
                thickness[i]=radius;
            }
            mesh.Tube(path,thickness,Color.white,6);
        }
    }
}
