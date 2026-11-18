using FishNet.Object;
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
            HoldRescue(Cursor.lockState==CursorLockMode.Locked && rescue.IsPressed());
        }
        [ServerRpc] private void HoldRescue(bool held) { heldAt=held?Time.unscaledTime:float.NegativeInfinity; }
        private void FixedUpdate()
        {
            if(!IsServerInitialized) return;
            if(motor.IsHelpless || Time.unscaledTime-heldAt>.25f) { progress=0; target=null; return; }
            DiverMotor nearest=null;
            float range=2.5f;
            foreach(var other in DiverMotor.ServerDivers)
            {
                if(other==motor || (!other.IsHelpless && !other.IsTrapped)) continue;
                float distance=Vector3.Distance(other.transform.position,transform.position);
                if(distance<range) { range=distance; nearest=other; }
            }
            if(nearest==null) { progress=0; target=null; return; }
            if(target!=nearest) { target=nearest; progress=0; }
            progress+=Time.fixedDeltaTime;
            if(progress>=motor.tuning.rescueSeconds) { nearest.Rescue(); progress=0; target=null; }
        }
        private void OnGUI()
        {
            if(!IsClientInitialized || !IsOwner) return;
            GUI.Label(new Rect(20,Screen.height-145,450,25),"Hold F near a helpless/trapped teammate to rescue");
        }
        public override void OnStopClient() { rescue?.Dispose(); rescue=null; }
        private void OnDestroy() { rescue?.Dispose(); }
    }
}
