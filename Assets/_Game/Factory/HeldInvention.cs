using UnityEngine;

namespace Raid
{
    sealed class HeldInvention
    {
        readonly Transform parent;
        Transform anchor;
        string previousKind;

        internal HeldInvention(Transform parent) => this.parent=parent;

        internal static float Width(string kind) => kind switch
        {
            "charge"=>.23f,
            "cage" or "farm"=>.37f,
            "vacuum"=>.33f,
            "winch" or "net" or "robot"=>.34f,
            _=>.30f
        };

        internal static Vector3 Grip(string kind,int side) => kind=="charge"
            ?new Vector3(side*.075f,-.047f,-.07f)
            :new Vector3(side*Width(kind)*.39f,-.083f,-.075f);

        internal void Present(string kind,Vector3 center,Quaternion grip)
        {
            if(string.IsNullOrEmpty(kind))
            {
                if(anchor)anchor.gameObject.SetActive(false);
                return;
            }
            if(!anchor || previousKind!=kind)
            {
                if(anchor)
                {
                    anchor.gameObject.SetActive(false);
                    Object.Destroy(anchor.gameObject);
                }
                previousKind=kind;
                anchor=DiveOutfit.Pivot(parent,"Held invention · "+kind,Vector3.zero);
                TechRigShell.BuildSaleSample(anchor.gameObject,kind,Width(kind));
            }
            anchor.SetLocalPositionAndRotation(center,grip);
            anchor.gameObject.SetActive(true);
        }
    }
}
