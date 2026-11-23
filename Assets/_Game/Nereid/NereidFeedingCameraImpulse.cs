using UnityEngine;

namespace Raid.Nereid
{
    // Additive owned-camera response after DiverMotor's ordinary view update. Never moves a player or changes aim input.
    [DefaultExecutionOrder(950)]
    public sealed class NereidFeedingCameraImpulse : MonoBehaviour
    {
        public float AppliedPeakMetres {get;private set;}
        public float AppliedPeakDegrees {get;private set;}
        public Vector3 CurrentOffset {get;private set;}
        public bool AppliedThisFrame {get;private set;}
        public int AppliedOwnerId {get;private set;}=-1;
        Camera ownedCamera;
        DiverMotor owner;
        Vector3 basePosition,lastAppliedPosition,origin;
        Quaternion baseRotation,lastAppliedRotation;
        bool applied;
        float fishingOffset;
        float swallowAt=-10,releaseAt=-10,chargeUntil=-10,chargeDuration=.44f,severity=1;
        // RAID-018 one-shot impacts: a single kick per server-confirmed hook, tug or bite.
        float hitAt=-10, hitMetres, hitDegrees, hitDirection;
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
        public void Cancel(){swallowAt=releaseAt=chargeUntil=-10;Restore();}

        void LateUpdate()
        {
            var ui=UI.UiRoot.Instance;var player=ui==null?null:ui.Context.Player;
            bool valid=player!=null&&player.NetworkObject!=null&&player.IsClientInitialized&&player.IsOwner&&ui.Context.Connected;
            if(owner!=player)
            {
                Restore();owner=player;ownedCamera=valid?player.GetComponentInChildren<Camera>():null;
                if(ownedCamera!=null){basePosition=ownedCamera.transform.localPosition;baseRotation=ownedCamera.transform.localRotation;}
            }
            if(valid&&ownedCamera==null){ownedCamera=player.GetComponentInChildren<Camera>();if(ownedCamera!=null)basePosition=ownedCamera.transform.localPosition;}
            bool focused=Application.isFocused;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            focused|=Time.unscaledTime<backgroundQaUntil;
#endif
            bool permitted=valid&&ownedCamera!=null&&focused&&ui.Page==UI.UiPage.None&&!UI.UiInputGate.Modal&&!player.IsHelpless;
            if(!permitted){fishingOffset=0;Restore();return;}
            var cameraTransform=ownedCamera.transform;
            // The motor rewrites rotation every frame; only remove our last pose if it is still present.
            if(applied)
            {
                if((cameraTransform.localPosition-lastAppliedPosition).sqrMagnitude<.00000001f)cameraTransform.localPosition=basePosition;
                else basePosition=cameraTransform.localPosition;
                if(Quaternion.Angle(cameraTransform.localRotation,lastAppliedRotation)<.0001f)cameraTransform.localRotation=baseRotation;
            }
            else basePosition=cameraTransform.localPosition;
            baseRotation=cameraTransform.localRotation;
            applied=false;AppliedThisFrame=false;CurrentOffset=Vector3.zero;AppliedOwnerId=-1;
            float age=Time.time-releaseAt;
            float release=age>=0&&age<1.75f?Mathf.SmoothStep(0,1,age/.045f)*Mathf.Exp(-age*1.7f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.1f,1.75f,age))):0;
            float gulpAge=Time.time-swallowAt;
            float gulp=gulpAge>=0&&gulpAge<.38f?Mathf.Sin(gulpAge/.38f*Mathf.PI):0;
            float charge=chargeUntil>Time.time?1-(chargeUntil-Time.time)/chargeDuration:0;
            float distance=Mathf.Clamp01(1-Vector3.Distance(cameraTransform.position,origin)/36);
            float amplitude=(release*.055f*Mathf.Pow(severity,.78f)+gulp*.006f+charge*.008f)*distance*shakeScale;
            var tool=ui.Context.Harpoon;
            var fish=tool?.HookedFish;
            float pull=fish!=null && fish.HookCount>0 ? (tool.LineTension*.009f+(fish.Surging?.016f:0))*ui.Settings.fishingCamera : 0;
            fishingOffset=Mathf.Lerp(fishingOffset,pull*shakeScale,1-Mathf.Exp(-Time.unscaledDeltaTime*9));
            float hitAge=Time.unscaledTime-hitAt;
            float hit=hitAge>=0 && hitAge<.42f ? Mathf.Exp(-hitAge*7.5f) : 0;
            if(hitAge>=.42f){hitMetres=hitDegrees=0;}
            if(amplitude<.00001f && fishingOffset<.0001f && hit<=0)return;
            float t=Time.time;Vector3 noise=new Vector3(Mathf.Sin(t*47)+.35f*Mathf.Sin(t*79+.3f),Mathf.Sin(t*61+.7f)+.3f*Mathf.Sin(t*97),Mathf.Sin(t*37)*.35f)/1.35f;
            CurrentOffset=noise*amplitude + baseRotation * Vector3.back * fishingOffset
                + baseRotation * (Vector3.right*(hit*hitMetres*hitDirection) + Vector3.up*(hit*hitMetres*.35f));
            Vector3 angles=new Vector3(noise.y,-noise.x,noise.z)*amplitude*17
                + new Vector3(-hit*hitDegrees,hit*hitDegrees*hitDirection,hit*hitDegrees*.4f*hitDirection);
            cameraTransform.localPosition=basePosition+CurrentOffset;cameraTransform.localRotation=baseRotation*Quaternion.Euler(angles);
            lastAppliedPosition=cameraTransform.localPosition;lastAppliedRotation=cameraTransform.localRotation;applied=true;AppliedThisFrame=true;AppliedOwnerId=player.OwnerId;
            AppliedPeakMetres=Mathf.Max(AppliedPeakMetres,CurrentOffset.magnitude);AppliedPeakDegrees=Mathf.Max(AppliedPeakDegrees,Quaternion.Angle(baseRotation,lastAppliedRotation));
        }
        void Restore()
        {
            if(applied&&ownedCamera!=null)
            {
                var cameraTransform=ownedCamera.transform;
                if((cameraTransform.localPosition-lastAppliedPosition).sqrMagnitude<.00000001f)cameraTransform.localPosition=basePosition;
                if(Quaternion.Angle(cameraTransform.localRotation,lastAppliedRotation)<.0001f)cameraTransform.localRotation=baseRotation;
            }
            applied=false;AppliedThisFrame=false;CurrentOffset=Vector3.zero;AppliedOwnerId=-1;
        }
        void OnApplicationFocus(bool focused){if(!focused)Restore();}
        void OnDisable(){Cancel();}
        void OnDestroy(){Restore();}
    }
}
