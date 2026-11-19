using UnityEngine;

namespace Raid.Nereid
{
    public sealed class AirlockPad : MonoBehaviour
    {
        public const float Reach=3.4f;
        public byte Id {get;internal set;}
        public bool Sea {get;internal set;}
        public bool CanRequest => RaidWorld.Instance!=null && RaidWorld.Instance.Airlock.CanRequest(Sea);
        public string Caption => CanRequest?(Sea?"Затопить · выход в море":"Осушить · вход на борт"):(RaidWorld.Instance==null?"Шлюз":RaidWorld.Instance.Airlock.Caption);

        public static AirlockPad Aimed(DiverMotor diver,Camera camera)
        {
            if(diver==null || camera==null || diver.IsDead || !Physics.Raycast(camera.transform.position,camera.transform.forward,out var hit,Reach,~0,QueryTriggerInteraction.Ignore))return null;
            return hit.collider.GetComponent<AirlockPad>();
        }

        public void Press(DiverMotor owner)
        {
            if(owner==null || !owner.IsOwner || !owner.IsClientInitialized || !CanRequest)return;
            var camera=owner.GetComponentInChildren<Camera>();
            if(Aimed(owner,camera)==this)RaidWorld.Instance.RequestAirlockPad(Id,camera.transform.forward);
        }
    }
}
