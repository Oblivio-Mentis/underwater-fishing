using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class TechRigShell
    {
        static Mesh cageHandsetMesh;
        Transform handset, handsetPalm, handsetFrame;
        DiverMotor handsetUser;
        int handsetOwner=-1;

        internal void DisplayCageHandset(TechRig rig)
        {
            int owner=rig.mode=="armed" || rig.mode=="closing"?rig.operatorId:-1;
            if(owner<0){if(handset)handset.gameObject.SetActive(false);handsetOwner=-1;return;}
            if(owner!=handsetOwner || !handsetUser)
            {
                handsetOwner=owner;
                handsetUser=DiverMotor.CrewView(owner);
                if(!handsetUser){if(handset)handset.gameObject.SetActive(false);return;}
                var eye=handsetUser.GetComponentInChildren<Camera>(true);
                handsetFrame=handsetUser.IsOwner && eye?eye.transform:handsetUser.transform;
                handsetPalm=null;
                foreach(var avatar in handsetUser.GetComponentsInChildren<DiverAvatarRig>(true))
                {
                    if(!avatar.leftArm?.end)continue;
                    bool eyeRig=eye && avatar.transform.IsChildOf(eye.transform);
                    if(eyeRig!=handsetUser.IsOwner)continue;
                    handsetPalm=avatar.leftArm.end;break;
                }
            }
            if(!handset)
            {
                Materials();
                if(!cageHandsetMesh)
                {
                    var p=new Press();
                    p.Box(Vector3.zero,new Vector3(.114f,.044f,.171f),2);
                    p.Box(new Vector3(0,.022f,-.014f),new Vector3(.099f,.012f,.126f),0);
                    p.Box(new Vector3(0,.031f,.019f),new Vector3(.076f,.009f,.038f),4);
                    p.Tube(new Vector3(.023f,.027f,-.043f),new Vector3(.023f,.043f,-.043f),.018f,.014f,5);
                    p.Tube(new Vector3(-.034f,.002f,.079f),new Vector3(-.034f,.01f,.165f),.007f,.004f,1);
                    p.Rod(new Vector3(-.046f,.045f,-.022f),new Vector3(-.046f,.045f,-.064f),.006f,1);
                    cageHandsetMesh=p.Bake("NEREID / cage receiver handset");
                }
                var handle=new GameObject("Пульт клеточного спуска");
                handset=handle.transform;handset.SetParent(transform,false);
                handle.AddComponent<MeshFilter>().sharedMesh=cageHandsetMesh;
                var renderer=handle.AddComponent<MeshRenderer>();renderer.sharedMaterials=palette;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
            handset.gameObject.SetActive(true);
            var frame=handsetFrame;
            Vector3 location=handsetPalm?handsetPalm.position+frame.forward*.064f+frame.up*.012f:
                frame.TransformPoint(handsetUser.IsOwner?new Vector3(-.20f,-.16f,.4f):new Vector3(-.25f,.37f,.29f));
            handset.SetPositionAndRotation(location,frame.rotation*Quaternion.Euler(-25,8,12));
        }
    }
}
