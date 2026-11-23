using UnityEngine;

namespace Raid
{
    internal static class GoshaBallGrip
    {
        internal const float SwapDuration=.34f, ForwardStroke=.14f, FollowThrough=.20f;
        internal static float Stow(float secondsLeft) => Mathf.Sin(Mathf.Clamp01(secondsLeft/SwapDuration)*Mathf.PI);
        internal static Vector3 Lower(float stow) => new(0,-.20f*stow,-.036f*stow);

        internal static double Clock(RaidWorld world) =>
            world.TimeManager.TicksToTime(world.TimeManager.GetPreciseTick(world.TimeManager.Tick));

        internal static float Stroke(RaidWorld world,GoshaSnapshot s) => s.BallStrokeAt<=0 ? 0 :
            Mathf.Clamp01((float)(Clock(world)-s.BallStrokeAt)/ForwardStroke);

        internal static float Charge(DiverMotor diver)
        {
            var world=RaidWorld.Instance;
            var ui=UI.UiRoot.Instance;
            if(diver.IsOwner && ui!=null && ui.WindingBall)return ui.LocalBallWind;
            float confirmed=world==null?0:world.Gosha.BallWind/255f;
            if(diver.IsOwner && ui!=null && world!=null && world.Gosha.BallWind!=0 && world.Gosha.BallStrokeAt<=0)
                return Mathf.Max(confirmed,ui.ReleasedBallWind);
            return confirmed;
        }

        internal static Vector3 CameraCenter(DiverAvatarRig rig,float wind,float stroke=0)
        {
            var ready=Vector3.Lerp(rig?rig.ballRest:new Vector3(.05f,-.16f,.43f),rig?rig.ballWind:new Vector3(.192f,-.036f,.326f),Mathf.SmoothStep(0,1,wind));
            var centre=Vector3.Lerp(ready,rig?rig.ballRelease:new Vector3(.10f,-.095f,.575f),stroke*stroke);
            centre.y+=Mathf.Sin(stroke*Mathf.PI)*.042f*wind;
            return centre;
        }

        internal static Vector3 WorldCenter(DiverMotor actor,Vector3 look,float wind,float stroke)
        {
            var eye=actor.transform.position+Vector3.up*.6f;
            return eye+Quaternion.LookRotation(look,Vector3.up)*CameraCenter(actor.firstPersonAppearance,wind,stroke);
        }

        internal static Vector3 GloveOffset(int side,float wind)
        {
            if(side>0)return new Vector3(.066f,-.060f,-.068f);
            return Vector3.Lerp(new Vector3(-.10f,-.073f,-.018f),new Vector3(-.46f,-.13f,.11f),wind);
        }
    }
}
