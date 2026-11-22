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
                new Collar(-21.77f,2.36f,4.12f,4.60f,2.74f,2.56f,2.34f),
                new Collar(-22.87f,3.04f,4.55f,4.97f,3.42f,3.20f,2.61f),
                new Collar(-24.69f,4.45f,4.89f,5.30f,4.58f,4.17f,2.81f),
                new Collar(-25.54f,4.61f,4.97f,5.26f,4.76f,4.18f,2.95f),
                new Collar(-27.25f,4.62f,5.01f,5.17f,4.78f,4.33f,3.06f),
                new Collar(-29.42f,4.53f,4.85f,5.12f,4.77f,4.12f,2.84f),
                new Collar(-30.73f,4.27f,4.58f,4.83f,4.71f,3.92f,2.73f),
                new Collar(-31.78f,3.54f,4.01f,4.33f,4.34f,3.81f,2.70f),
                new Collar(-33.29f,2.21f,2.80f,3.34f,3.60f,3.22f,2.68f)
            };
            var canopy=new[]{
                new Collar(-20.93f,1.16f,1.70f,1.67f,5.51f,5.28f,4.99f),
                new Collar(-22.76f,2.28f,2.79f,2.59f,5.99f,5.76f,5.20f),
                new Collar(-23.98f,3.60f,3.98f,3.84f,6.74f,6.47f,6.12f),
                new Collar(-24.76f,4.23f,4.52f,4.35f,7.24f,6.99f,6.70f),
                new Collar(-26.00f,4.41f,4.72f,4.56f,7.31f,7.08f,6.72f),
                new Collar(-28.42f,4.40f,4.71f,4.54f,7.29f,7.06f,6.72f),
                new Collar(-30.24f,4.29f,4.54f,4.37f,7.19f,6.98f,6.70f),
                new Collar(-31.38f,3.48f,3.80f,3.59f,6.95f,6.74f,6.57f),
                new Collar(-32.33f,2.28f,2.69f,2.60f,6.54f,6.31f,6.12f)
            };
            var shell=new ReefGeometry();
            var joins=new ReefGeometry();
            var brow=new ReefGeometry();
            Sweep(shell,joins,stem,3);
            Sweep(brow,joins,canopy,2);
            var ceramic=Finish(coating.shader,"Ceramic survey brow",new(.357f,.426f,.432f),.26f,.41f);
            PresentShape("Flared continuous pressure hill",shell,coating);
            PresentShape("Scarfed panoramic shelter",brow,ceramic);
            PresentShape("Cast and welded bridge seams",joins,steel);

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
