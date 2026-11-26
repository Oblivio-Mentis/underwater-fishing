using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    public sealed partial class NereidFeedingReaction
    {
        // The lip halves share fixed corners. The teeth mounted to each lip use the same deformation.
        sealed class AnchoredLip
        {
            readonly Transform jaw;
            readonly MeshFilter filter;
            readonly Mesh original, deforming;
            readonly Vector3[] rest, working;
            readonly List<(Transform tooth,Vector3 rest)> teeth=new();
            float previousShift=float.NaN,previousWidth;

            internal AnchoredLip(Transform jaw)
            {
                this.jaw=jaw;
                filter=jaw.GetComponent<MeshFilter>();
                original=filter?filter.sharedMesh:null;
                if(!original)return;
                deforming=Instantiate(original);
                deforming.name=original.name+" (anchored mouth)";
                deforming.MarkDynamic();
                rest=deforming.vertices;working=new Vector3[rest.Length];
                var bounds=original.bounds;bounds.Expand(2);deforming.bounds=bounds;
                foreach(Transform child in jaw)teeth.Add((child,child.localPosition));
            }

            internal static float Flex(float x) => 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.66f,1.91f,Mathf.Abs(x)));

            internal void Deform(float travel,float widen)
            {
                if(!deforming)return;
                float shift=travel/Mathf.Max(.01f,jaw.localScale.y);
                if(filter.sharedMesh!=deforming)filter.sharedMesh=deforming;
                if(Mathf.Abs(previousShift-shift)<.0001f && Mathf.Abs(previousWidth-widen)<.0001f)return;
                previousShift=shift;previousWidth=widen;
                for(int i=0;i<rest.Length;i++)working[i]=FlexPoint(rest[i],shift,widen);
                deforming.SetVertices(working,0,working.Length,UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
                deforming.RecalculateNormals();
                foreach(var part in teeth)if(part.tooth)part.tooth.localPosition=FlexPoint(part.rest,shift,widen);
            }

            static Vector3 FlexPoint(Vector3 position,float shift,float widen)
            {
                float movement=Flex(position.x);
                position.y-=shift*(1-movement);
                position.x*=1+widen*movement;
                return position;
            }

            internal void Restore()
            {
                if(filter && original)filter.sharedMesh=original;
                foreach(var part in teeth)if(part.tooth)part.tooth.localPosition=part.rest;
                previousShift=float.NaN;
            }

            internal void Dispose(){if(deforming)Destroy(deforming);}
        }
    }
}
