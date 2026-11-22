using UnityEngine;

namespace Raid.Nereid
{
    internal sealed partial class NereidDeckExtension
    {
        // Lofted pressure saddle ties the full-width bridge into the old
        // tapered load-bearing shell. All stations are in hull coordinates.
        readonly struct Collar
        {
            internal readonly float Z, TopX, TurnX, BottomX, Top, Turn, Bottom;

            internal Collar(float z,float topX,float turnX,float bottomX,float top,float turn,float bottom)
            {
                Z=z;
                TopX=topX;
                TurnX=turnX;
                BottomX=bottomX;
                Top=top;
                Turn=turn;
                Bottom=bottom;
            }

            internal Vector3[] Rim() => new[]{
                new Vector3(-BottomX,Bottom,Z),new Vector3(BottomX,Bottom,Z),
                new Vector3(TurnX,Turn,Z),new Vector3(TopX,Top-.16f,Z),
                new Vector3(TopX*.88f,Top,Z),new Vector3(-TopX*.88f,Top,Z),
                new Vector3(-TopX,Top-.16f,Z),new Vector3(-TurnX,Turn,Z)
            };
        }

        void BuildBridgePressureFairing()
        {
            var stem=new[]{
                new Collar(-21.35f,1.21f,2.82f,3.32f,2.56f,2.41f,2.21f),
                new Collar(-22.83f,2.86f,4.40f,4.58f,3.42f,3.06f,2.60f),
                new Collar(-24.69f,4.42f,4.83f,4.72f,4.59f,4.19f,2.81f),
                new Collar(-25.84f,4.53f,4.78f,4.68f,4.78f,4.12f,2.27f),
                new Collar(-27.40f,4.54f,4.60f,3.95f,4.78f,3.79f,1.77f),
                new Collar(-29.32f,4.53f,4.31f,3.01f,4.78f,3.31f,1.80f),
                new Collar(-30.57f,4.32f,3.91f,2.27f,4.75f,3.14f,2.03f),
                new Collar(-31.47f,3.43f,3.16f,1.90f,4.46f,3.02f,2.11f),
                new Collar(-32.32f,1.94f,2.02f,1.19f,3.59f,2.67f,2.16f),
                new Collar(-32.63f,1.12f,1.25f,.84f,2.81f,2.46f,2.18f)
            };
            var canopy=new[]{
                new Collar(-22.09f,1.54f,1.72f,1.62f,5.25f,5.04f,4.81f),
                new Collar(-23.32f,3.25f,3.67f,3.47f,6.16f,5.88f,5.67f),
                new Collar(-24.65f,4.20f,4.57f,4.43f,7.30f,6.93f,6.70f),
                new Collar(-25.53f,4.28f,4.68f,4.52f,7.41f,7.10f,6.69f),
                new Collar(-28.31f,4.26f,4.69f,4.53f,7.37f,7.06f,6.69f),
                new Collar(-30.23f,4.12f,4.48f,4.37f,7.23f,6.96f,6.69f),
                new Collar(-31.20f,3.29f,3.67f,3.58f,6.91f,6.74f,6.57f),
                new Collar(-31.83f,2.47f,2.71f,2.55f,6.57f,6.46f,6.37f)
            };
            var shell=new ReefGeometry();
            var joins=new ReefGeometry();
            Sweep(shell,joins,stem,3);
            Sweep(shell,joins,canopy,2);
            PresentShape("Continuous bridge pressure saddle",shell,coating);
            PresentShape("Cast fins and welded bridge seams",joins,steel);

            // Angled side braces land directly on the new pressure saddle and cap shell.
            for(int side=-1;side<=1;side+=2)
            {
                foreach(float z in new[]{-25.17f,-27.34f,-29.34f})
                {
                    Beam("Reinforced bridge mullion",new(side*4.35f,5.38f,z),new(side*4.51f,6.72f,z+.28f),.163f,.205f,coating);
                    Tube(new[]{new Vector3(side*4.66f,4.26f,z+.25f),new Vector3(side*4.54f,4.59f,z+.23f),new Vector3(side*4.40f,4.93f,z+.16f)},
                        .058f,steel);
                }
            }
        }

        static void Sweep(ReefGeometry shell,ReefGeometry joins,Collar[] stations,int seamStart)
        {
            Vector3[] last=stations[0].Rim();
            NereidFairing.Cap(shell,last,false);
            for(int i=1;i<stations.Length;i++)
            {
                Vector3[] next=stations[i].Rim();
                NereidFairing.Join(shell,last,next);
                if(i>=seamStart && i%2==seamStart%2)NereidFairing.Seam(joins,next,.034f);
                last=next;
            }
            NereidFairing.Cap(shell,last,true);
        }
    }
}
