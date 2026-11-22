using UnityEngine;

namespace Raid
{
    // Shared by the glove rigs and the single visible ball for each camera.
    internal static class GoshaBallGrip
    {
        internal static float Charge(DiverMotor diver)
        {
            var ui=UI.UiRoot.Instance;
            if(diver.IsOwner && ui!=null)return ui.LocalBallWind;
            var world=RaidWorld.Instance;
            return world==null?0:world.Gosha.BallWind/255f;
        }

        internal static Vector3 CameraCenter(float wind) =>
            Vector3.Lerp(new Vector3(.12f,-.20f,.49f),new Vector3(.30f,-.085f,.42f),Mathf.SmoothStep(0,1,wind));

        internal static Vector3 ObserverCenter(float wind) =>
            Vector3.Lerp(new Vector3(.17f,.25f,.43f),new Vector3(.34f,.46f,-.08f),Mathf.SmoothStep(0,1,wind));

        internal static Vector3 GloveOffset(int side,float wind)
        {
            if(side>0)return new Vector3(.066f,-.060f,-.068f);
            return Vector3.Lerp(new Vector3(-.10f,-.073f,-.018f),new Vector3(-.46f,-.13f,.11f),wind);
        }
    }
}
