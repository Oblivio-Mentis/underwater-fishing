using TMPro;
using UnityEngine;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        static Mesh dispenserMesh;
        internal static void BuildDispenser(GameObject housing)
        {
            Materials();
            if(!dispenserMesh)
            {
                var p=new Press();
                p.Box(new(0,-.24f,-.27f),new(1.08f,1.86f,.40f),0);
                p.Box(new(0,-1.11f,-.21f),new(1.16f,.14f,.58f),1);
                p.Box(new(0,.60f,-.21f),new(1.11f,.18f,.56f),2);
                p.Box(new(0,.42f,-.01f),new(.95f,.23f,.06f),0);
                p.Box(new(0,.25f,.002f),new(.79f,.013f,.028f),4);
                for(int row=0;row<3;row++)
                {
                    float y=.06f-.25f*row;
                    p.Box(new(0,y,-.035f),new(.88f,.214f,.12f),3);
                    p.Box(new(.33f,y+.02f,.036f),new(.075f,.036f,.022f),4);
                    p.Rod(new(-.23f,y+.024f,.05f),new(-.23f,y+.026f,.095f),.018f,1);
                    p.Rod(new(.09f,y+.024f,.05f),new(.09f,y+.026f,.095f),.018f,1);
                    p.Rod(new(-.23f,y+.026f,.095f),new(.09f,y+.026f,.095f),.018f,1);
                }
                p.Box(new(0,-.76f,.0f),new(.87f,.24f,.075f),1);
                p.Box(new(0,-.74f,.047f),new(.68f,.125f,.036f),0);
                p.Box(new(0,-.88f,.095f),new(.86f,.04f,.21f),2);
                foreach(int side in new[]{-1,1})
                {
                    float x=side*.5f;
                    p.Rod(new(x,-.9f,-.019f),new(x,.52f,-.019f),.035f,1);
                    foreach(float y in new[]{-.90f,.14f,.53f})p.Tube(new(x,y,-.04f),new(x,y,.031f),.042f,.038f,2,8);
                }
                dispenserMesh=p.Bake("NEREID · ship equipment dispenser");
            }
            housing.AddComponent<MeshFilter>().sharedMesh=dispenserMesh;
            housing.AddComponent<MeshRenderer>().sharedMaterials=palette;
            var sign=new GameObject("Выдача снаряжения");sign.transform.SetParent(housing.transform,false);
            sign.transform.SetLocalPositionAndRotation(new(0,.42f,.034f),Quaternion.Euler(0,180,0));
            var label=sign.AddComponent<TextMeshPro>();
            if(UI.UiRoot.Instance && UI.UiRoot.Instance.font)label.font=UI.UiRoot.Instance.font;
            label.text="ВЫДАЧА СНАРЯЖЕНИЯ";
            label.fontSize=.67f;label.rectTransform.sizeDelta=new(.90f,.18f);
            label.alignment=TextAlignmentOptions.Center;label.textWrappingMode=TextWrappingModes.NoWrap;
            label.color=new Color(.72f,.94f,.88f);label.raycastTarget=false;
        }
    }
}

