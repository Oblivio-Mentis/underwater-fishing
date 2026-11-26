using UnityEngine;

namespace Raid.Nereid
{
    // Additive owned-camera response after DiverMotor's ordinary view update. Never moves a player or changes aim input.
    [DisallowMultipleComponent, RequireComponent(typeof(Camera)), DefaultExecutionOrder(950)]
    public sealed class NereidFeedingCameraImpulse : MonoBehaviour
    {
        public float AppliedPeakMetres {get;private set;}
        public float AppliedPeakDegrees {get;private set;}
        public Vector3 CurrentOffset {get;private set;}
        public bool AppliedThisFrame {get;private set;}
        public int AppliedOwnerId {get;private set;}=-1;
        DiverMotor owner;
        Vector3 origin;

        void Awake() => owner=GetComponentInParent<DiverMotor>();
        float swallowAt=-10,releaseAt=-10,chargeUntil=-10,chargeDuration=.44f,severity=1;
        // RAID-018 one-shot impacts: a single kick per server-confirmed hook, tug or bite.
        float hitAt=-10, hitMetres, hitDegrees, hitDirection;
        float blastAt=-10,blastStrength,blastSide;

        public void Blast(Vector3 point,float radius)
        {
            var delta=transform.position-point;
            float distance=delta.magnitude;
            float strength=Mathf.Pow(1-Mathf.Clamp01(distance/radius),1.15f)*shakeScale;
            if(strength<=.01f || Time.unscaledTime-blastAt<.12f && strength<blastStrength)return;
            blastAt=Time.unscaledTime+distance/39f;
            blastStrength=strength;
            blastSide=Vector3.Dot(transform.right,delta.normalized);
        }
        /// <summary>One short directional shake. Called only for a locally observed confirmed event.</summary>
        public void Hit(float metres,float degrees,float direction)
        {
            float impulse=uiShake;
            if(impulse<=.001f)return;
            hitMetres=Mathf.Max(hitMetres,Mathf.Max(0,metres)*impulse);
            hitDegrees=Mathf.Max(hitDegrees,Mathf.Max(0,degrees)*impulse);
            hitDirection=direction;hitAt=Time.unscaledTime;
        }
        static float uiShake
        {
            get { var ui=UI.UiRoot.Instance; return ui==null?1:Mathf.Clamp01(ui.Settings.hitImpulse*ui.Settings.cameraShake); }
        }
        static float shakeScale => UI.UiRoot.Instance==null?1:Mathf.Clamp01(UI.UiRoot.Instance.Settings.cameraShake);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float backgroundQaUntil;
        public void BeginBackgroundQa(float seconds)
        {
            var ui=UI.UiRoot.Instance;
            if(ui==null||!ui.Context.Connected||ui.Page!=UI.UiPage.None||UI.UiInputGate.Modal)return;
            backgroundQaUntil=Time.unscaledTime+Mathf.Clamp(seconds,0,60);
        }
#endif
        public void Swallow(Vector3 point){origin=point;swallowAt=Time.time;}
        public void Anticipate(Vector3 point,float seconds){origin=point;chargeDuration=Mathf.Max(.2f,seconds);chargeUntil=Time.time+chargeDuration;}
        public void Release(Vector3 point,float strength)
        {origin=point;severity=Mathf.Clamp(strength,1,2.35f);releaseAt=Time.time;chargeUntil=-10;AppliedPeakMetres=AppliedPeakDegrees=0;}
        public void Cancel() => swallowAt=releaseAt=chargeUntil=-10;
        float strain, strainTarget;
        /// <summary>0..1 sustained fishing strain for this frame; decays on its own when not set.</summary>
        public void SetStrain(float amount) => strainTarget=Mathf.Clamp01(amount);

        void LateUpdate()
        {
            AppliedThisFrame=false;
            CurrentOffset=Vector3.zero;
            AppliedOwnerId=-1;
            var ui=UI.UiRoot.Instance;
            bool focused=Application.isFocused;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            focused|=Time.unscaledTime<backgroundQaUntil;
#endif
            if(!focused || ui==null || ui.Page!=UI.UiPage.None || UI.UiInputGate.Modal ||
                owner==null || owner!=ui.Context.Player || !ui.Context.Connected || owner.IsHelpless)return;

            float age=Time.time-releaseAt;
            float release=age>=0&&age<1.75f?Mathf.SmoothStep(0,1,age/.045f)*Mathf.Exp(-age*1.7f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.1f,1.75f,age))):0;
            float gulpAge=Time.time-swallowAt;
            float gulp=gulpAge>=0&&gulpAge<.38f?Mathf.Sin(gulpAge/.38f*Mathf.PI):0;
            float charge=chargeUntil>Time.time?1-(chargeUntil-Time.time)/chargeDuration:0;
            float distance=Mathf.Clamp01(1-Vector3.Distance(transform.position,origin)/36);
            strain=Mathf.Lerp(strain,strainTarget,1-Mathf.Exp(-Time.unscaledDeltaTime*7));strainTarget*=Mathf.Exp(-Time.unscaledDeltaTime*4);
            float amplitude=(release*.055f*Mathf.Pow(severity,.78f)+gulp*.006f+charge*.008f)*distance*shakeScale+strain*strain*.0075f*shakeScale;
            float hitAge=Time.unscaledTime-hitAt;
            float hit=hitAge>=0 && hitAge<.42f ? Mathf.Exp(-hitAge*7.5f) : 0;
            float blastAge=Time.unscaledTime-blastAt;
            float shock=blastAge>=0 && blastAge<1f
                ?blastStrength*Mathf.SmoothStep(0,1,blastAge/.036f)*Mathf.Exp(-5.1f*blastAge):0;
            if(hitAge>=.42f){hitMetres=hitDegrees=0;}
            if(amplitude<.00001f && hit<=0 && shock<=0)return;
            float t=Time.time;Vector3 noise=new Vector3(Mathf.Sin(t*47)+.35f*Mathf.Sin(t*79+.3f),Mathf.Sin(t*61+.7f)+.3f*Mathf.Sin(t*97),Mathf.Sin(t*37)*.35f)/1.35f;
            // DiverMotor has already written the live eye pose for this frame. Apply it once here.
            var eyeRotation=transform.localRotation;
            CurrentOffset=noise*(amplitude+.077f*shock)
                + eyeRotation * (Vector3.right*(hit*hitMetres*hitDirection) + Vector3.up*(hit*hitMetres*.35f));
            Vector3 angles=new Vector3(noise.y,-noise.x,noise.z)*(amplitude*17+shock*6.7f)
                + new Vector3(-hit*hitDegrees,hit*hitDegrees*hitDirection,hit*hitDegrees*.4f*hitDirection)
                + shock*new Vector3(-4.4f,blastSide*2.6f,blastSide*3.2f);
            var rotation=eyeRotation*Quaternion.Euler(angles);
            transform.SetLocalPositionAndRotation(transform.localPosition+CurrentOffset,rotation);
            AppliedThisFrame=true;
            AppliedOwnerId=owner.OwnerId;
            AppliedPeakMetres=Mathf.Max(AppliedPeakMetres,CurrentOffset.magnitude);
            AppliedPeakDegrees=Mathf.Max(AppliedPeakDegrees,Quaternion.Angle(eyeRotation,rotation));
        }

    }
}
