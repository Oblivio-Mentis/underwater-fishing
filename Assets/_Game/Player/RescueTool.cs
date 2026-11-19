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
        private void Update()
        {
            if(!IsOwner || rescue==null || Time.unscaledTime<nextSend) return;
            nextSend=Time.unscaledTime+.1f;
            HoldRescue(Application.isFocused && Cursor.lockState==CursorLockMode.Locked && !UI.UiInputGate.Blocked && rescue.IsPressed());
        }
        [ServerRpc] private void HoldRescue(bool held) { heldAt=held?Time.unscaledTime:float.NegativeInfinity; }
        private void FixedUpdate()
        {
            if(!IsServerInitialized) return;
            if(motor.IsHelpless || Time.unscaledTime-heldAt>.25f) { progress=0; target=null; PublishDisplay(); return; }
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
        private void PublishDisplay() { displayProgress.Value=target==null?0:Mathf.Clamp01(progress/Mathf.Max(.01f,motor.tuning.rescueSeconds));displayTarget.Value=target==null?-1:target.OwnerId; }
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
