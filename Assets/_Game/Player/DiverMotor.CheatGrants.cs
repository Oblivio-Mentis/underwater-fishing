using System;
using FishNet.Connection;
using FishNet.Object;

namespace Raid
{
    public sealed partial class DiverMotor
    {
        public event Action<bool,int,string> CheatGrantResponded;

        [ServerRpc]
        public void CmdCheatGrantResource(string resourceId,int amount) => RequestCheatGrant(resourceId,amount,false);

        [ServerRpc]
        public void CmdCheatGrantFish(string speciesId,int amount) => RequestCheatGrant(speciesId,amount,true);

        void RequestCheatGrant(string id,int amount,bool fish)
        {
            if(Owner?.IsAuthenticated!=true || Owner.FirstObject!=NetworkObject)return;
            var world=RaidWorld.Instance;
            int granted=0;
            string reason="Мир ещё не готов.";
            bool ok=world && world.GrantFromCheatPanel(id,amount,fish,out granted,out reason);
            ReplyCheatGrant(Owner,ok,granted,reason);
        }

        [TargetRpc]
        void ReplyCheatGrant(NetworkConnection target,bool success,int granted,string reason) =>
            CheatGrantResponded?.Invoke(success,granted,reason);
    }
}
