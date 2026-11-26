using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        internal static DiverMotor CrewView(int owner)
        {
            if (owner < 0) return null;
            foreach (var member in ClientCrew) if(member && member.OwnerId==owner && !member.IsDead) return member;
            return null;
        }
    }

    public sealed class RigFishingLines : MonoBehaviour
    {
        const int Columns=15, Rows=4;
        LineRenderer[] ropes;
        static Material meshCord;
        Vector3[] slack;
        public static void Present(GameObject model, TechRig rig)
        {
            if (!model || (rig.kind!="net" && rig.kind!="vacuum")) return;
            var lines=model.GetComponent<RigFishingLines>();
            bool active=rig.mode is "netting" or "pumping" or "working" or "jammed";
            if (!active && !lines) return;
            if (!lines) lines=model.AddComponent<RigFishingLines>();
            lines.Draw(rig,active);
        }

        LineRenderer Cord(string label, float width, Color color, int count)
        {
            var go=new GameObject(label);go.transform.SetParent(transform,false);
            var rope=go.AddComponent<LineRenderer>();
            if (!meshCord)
            {
                var shader=Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if(shader)meshCord=new Material(shader){name="Woven industrial gear cords"};
            }
            rope.sharedMaterial=meshCord;
            rope.startColor=rope.endColor=color;rope.widthMultiplier=width;rope.positionCount=count;
            rope.shadowCastingMode=ShadowCastingMode.Off;rope.receiveShadows=false;rope.useWorldSpace=true;
            rope.numCornerVertices=1;return rope;
        }

        void Draw(TechRig rig,bool active)
        {
            bool net=rig.kind=="net";
            if(ropes==null)
            {
                if(net)
                {
                    ropes=new LineRenderer[2*Rows+2];
                    for(int i=0;i<ropes.Length;i++)
                    {
                        bool hem=i>=2*Rows;
                        ropes[i]=Cord(hem?"Buoyant hem":"Braided seine web",hem?.043f:.021f,
                            hem?new Color(.95f,.55f,.18f):new Color(.26f,.73f,.66f),Columns);
                    }
                }
                else ropes=new[]{Cord("Pressure hose · jacket",.115f,new Color(.13f,.22f,.23f),13),
                                 Cord("Pressure hose · tracer",.022f,new Color(.94f,.59f,.22f),13)};
                slack=new Vector3[net?Columns:13];
            }
            foreach(var rope in ropes)rope.enabled=active;
            if(!active)return;
            var a=DiverMotor.CrewView(rig.operatorId);
            var b=DiverMotor.CrewView(rig.helperId);
            if(net)
            {
                Vector3 left=a?a.transform.position+a.transform.forward*.45f:rig.position-Vector3.right*2;
                Vector3 right=b?b.transform.position+b.transform.forward*.45f:
                    rig.mode=="working"?rig.position+Vector3.right*2:rig.anchor;
                for(int strand=0;strand<ropes.Length;strand++)
                {
                    for(int x=0;x<Columns;x++)
                    {
                        float t=(float)x/(Columns-1);
                        float row=strand>=Rows*2?(strand==Rows*2?0:Rows):
                            strand/2+((strand+x)%2);
                        float swell=Mathf.Sin(t*Mathf.PI);
                        float billow=(.18f+.12f*Mathf.Sin(t*5-Time.time*1.4f))*swell;
                        slack[x]=Vector3.Lerp(left,right,t)+Vector3.down*(.4f*row+.32f*swell)+
                            Vector3.Cross((right-left).normalized,Vector3.up)*billow;
                    }
                    ropes[strand].SetPositions(slack);
                }
            }
            else
            {
                Vector3 start=rig.position+Vector3.up*.45f;
                Vector3 end=a?a.transform.position+Vector3.up*.25f+a.transform.forward*.72f:
                    start+rig.anchor.normalized*.95f;
                for(int x=0;x<slack.Length;x++)
                {
                    float t=(float)x/(slack.Length-1);
                    slack[x]=Vector3.Lerp(start,end,t)+Vector3.down*Mathf.Sin(t*Mathf.PI)*.32f;
                }
                ropes[0].SetPositions(slack);
                for(int x=0;x<slack.Length;x++)slack[x]+=Vector3.up*.055f;
                ropes[1].SetPositions(slack);
            }
        }
    }
}
