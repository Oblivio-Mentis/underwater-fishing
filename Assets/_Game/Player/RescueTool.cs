using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Raid
{
    public sealed class RescueTool : NetworkBehaviour
    {
        private InputAction rescue;
        private DiverMotor motor;
        private float nextSend,heldAt,progress;
        private DiverMotor target;
        private readonly SyncVar<float> displayProgress=new();
        private readonly SyncVar<int> displayTarget=new(-1);
        public float DisplayProgress => displayProgress.Value;
        public int DisplayTargetOwnerId => displayTarget.Value;
        public override void OnStartNetwork() { motor=GetComponent<DiverMotor>(); }
        public override void OnStartClient()
        {
            if(!IsOwner) return;
            rescue=new InputAction("Rescue",InputActionType.Button,"<Keyboard>/f"); rescue.Enable();
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float qaKeyUntil;
        internal void QaPressF(float seconds)
        {
            if (Application.isBatchMode && IsOwner && !UI.UiInputGate.Modal)
                qaKeyUntil = Time.unscaledTime + Mathf.Clamp(seconds, 0, 10);
        }
        internal void QaStruggle(float seconds)
        {
            if(IsOwner && motor.CanBreakRaidGrip)qaKeyUntil=Time.unscaledTime+seconds;
        }
#endif
        private void Update()
        {
            if(!IsOwner || rescue==null || Time.unscaledTime<nextSend) return;
            nextSend=Time.unscaledTime+.1f;
            bool held = Application.isFocused && (Cursor.lockState==CursorLockMode.Locked || UI.CheatPanel.PointerActive)
                && !UI.UiInputGate.Blocked && rescue.IsPressed();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // In batch mode Unity cannot lock a desktop cursor. The probe supplies the duration of its F press.
            held |= Time.unscaledTime < qaKeyUntil && !UI.UiInputGate.Modal;
#endif
            HoldRescue(held);
        }
        [ServerRpc] private void HoldRescue(bool held) { heldAt=held?Time.unscaledTime:float.NegativeInfinity; }
        private void FixedUpdate()
        {
            if(!IsServerInitialized) return;
            if(motor.IsHelpless || Time.unscaledTime-heldAt>.25f) { progress=0; target=null; PublishDisplay(); return; }
            if(motor.CanBreakRaidGrip)
            {
                if(target!=motor){target=motor;progress=0;}
                progress+=Time.fixedDeltaTime;
                if(progress>=SoloEscapeSeconds)
                {
                    ThreatDirector.Instance?.BreakSoloGrip(motor);
                    progress=0;
                    target=null;
                    heldAt=float.NegativeInfinity;
                }
                PublishDisplay();
                return;
            }
            DiverMotor nearest=null;
            float range=2.5f;
            foreach(var other in DiverMotor.ServerDivers)
            {
                if(other==motor || !other.CanBeRescued) continue;
                float distance=Vector3.Distance(other.transform.position,transform.position);
                if(distance<range) { range=distance; nearest=other; }
            }
            if(nearest==null) { progress=0; target=null; PublishDisplay(); return; }
            if(target!=nearest) { target=nearest; progress=0; }
            progress+=Time.fixedDeltaTime;
            if(progress>=motor.tuning.rescueSeconds) { nearest.Rescue(); progress=0; target=null; }
            PublishDisplay();
        }
        float SoloEscapeSeconds => ThreatDirector.Instance?.defenseTuning?.soloEscapeSeconds??.95f;
        private void PublishDisplay()
        {
            float duration=target==motor?SoloEscapeSeconds:motor.tuning.rescueSeconds;
            displayProgress.Value=target==null?0:Mathf.Clamp01(progress/Mathf.Max(.01f,duration));
            displayTarget.Value=target==null?-1:target.OwnerId;
        }
        private void OnGUI()
        {
            if(UI.UiInputGate.Active) return;
            if(!IsClientInitialized || !IsOwner) return;
            GUI.Label(new Rect(20,Screen.height-145,450,25),"Hold F near a helpless/trapped teammate to rescue");
        }
        public override void OnStopClient() { rescue?.Dispose(); rescue=null; }
        private void OnDestroy() { rescue?.Dispose(); }
    }
}
