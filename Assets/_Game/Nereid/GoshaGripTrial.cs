#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Raid.UI;
using UnityEngine;

namespace Raid.Nereid
{
    internal static class GoshaGripTrial
    {
        internal static IEnumerator Verify(RaidWorld world,DiverMotor holder,Action<string,bool> check,Action<string> capture)
        {
            var ui=UiRoot.Instance;
            var camera=holder.GetComponentInChildren<Camera>().transform;
            var initialAim=camera.forward;
            yield return new WaitForSeconds(.22f);
            var ball=camera.Find("Ball in diving gloves");
            var icon=System.Array.Find(ui.GetComponentsInChildren<UnityEngine.UI.Image>(),image=>
                image.enabled && image.sprite==DiveInstruments.Art(DiveInstruments.Mark.Ball));
            check("The atlas ball glyph is visible in numbered slot 3",
                ball!=null && ball.gameObject.activeInHierarchy && icon!=null && icon.transform.parent.parent.name=="Slot 3");
            var position=ball.localPosition;
            var rotation=ball.localRotation;
            float maxDistance=0,maxAngle=0,turned=0;
            foreach(float yaw in new[]{-72f,88f,0f})
            {
                var direction=Quaternion.AngleAxis(yaw,Vector3.up)*initialAim;
                holder.LookAtConsole(camera.position+direction*5);
                yield return new WaitForSeconds(.16f);
                turned=Mathf.Max(turned,Vector3.Angle(camera.forward,initialAim));
                maxDistance=Mathf.Max(maxDistance,(position-camera.InverseTransformPoint(ball.position)).magnitude);
                maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(rotation,Quaternion.Inverse(camera.rotation)*ball.rotation));
            }
            check($"Camera turned {turned:F1} deg: held ball drift {maxDistance:F5} m, rotation {maxAngle:F3} deg",
                turned>45 && maxDistance<.001f && maxAngle<.1f);
            capture("ball-in-hand.png");
            ui.HandleBallPointer(true,false);
            float until=Time.realtimeSinceStartup+world.tuning.ballWindSeconds+2;
            while((world.Gosha.BallWind<=240 || (ball.localPosition-position).magnitude<=.18f) &&
                Time.realtimeSinceStartup<until)yield return null;
            check("Holding LMB keeps the single ball in the hand and visibly winds the arm",
                world.HasGoshaBall(holder) && world.Gosha.BallWind>240 && (ball.localPosition-position).magnitude>.18f);
            capture("ball-windup.png");
            ui.ShowPage(UiPage.Pause);
            yield return new WaitForSeconds(.2f);
            ui.ShowPage(UiPage.None);
            yield return new WaitForSeconds(.2f);
            ui.HandleBallPointer(false,true);
            check("Opening the pause menu cancels the wind-up; releasing afterwards retains the ball",
                world.HasGoshaBall(holder) && world.Gosha.BallWind==0);
        }
    }
}
#endif
