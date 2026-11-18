using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace Raid
{
    public sealed class LootActor : NetworkBehaviour
    {
        public int tier=1,scrap=2,electronics;
        public long itemId;
        private readonly SyncVar<Vector3> position=new();
        private readonly SyncVar<bool> visible=new();
        private DiverMotor carrier;
        private Renderer body;
        private Collider shape;
        private Vector3 spawn;
        private bool enabledAtDepth;
        private void Awake() { body=GetComponent<Renderer>(); shape=GetComponent<Collider>(); spawn=transform.position; }
        private static long NewId()=>System.BitConverter.ToInt64(System.Guid.NewGuid().ToByteArray(),0)&long.MaxValue;
        public override void OnStartServer() { enabledAtDepth=false; visible.Value=false; carrier=null; itemId=NewId(); position.Value=spawn; }
        public void SetDepth(int depth)
        {
            if(!IsServerInitialized) return;
            enabledAtDepth=depth==tier; carrier=null; transform.position=spawn; position.Value=spawn; visible.Value=enabledAtDepth;
            itemId=NewId();
        }
        public bool Pickup(DiverMotor diver)
        {
            if(!IsServerInitialized || !enabledAtDepth || !visible.Value || carrier!=null || diver.IsHelpless || Vector3.Distance(diver.transform.position,transform.position)>3f) return false;
            foreach(var loot in FindObjectsByType<LootActor>()) if(loot.carrier==diver) return false;
            carrier=diver; return true;
        }
        public bool IsCarriedBy(DiverMotor diver)=>carrier==diver;
        public void Consume() { if(!IsServerInitialized) return; visible.Value=false; enabledAtDepth=false; carrier=null; }
        private void FixedUpdate()
        {
            if(!IsServerInitialized) return;
            if(carrier!=null)
            {
                if(carrier.IsHelpless) carrier=null;
                else transform.position=carrier.transform.position+carrier.transform.right*.8f;
            }
            position.Value=transform.position;
        }
        private void LateUpdate()
        {
            if(!IsClientInitialized) return;
            body.enabled=visible.Value; shape.enabled=visible.Value && carrier==null;
            if(!IsServerInitialized) transform.position=Vector3.Lerp(transform.position,position.Value,1-Mathf.Exp(-Time.deltaTime*12));
        }
    }
}
