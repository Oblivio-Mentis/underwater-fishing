using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        float serviceProgress;
        bool serviceExiting;

        bool TryEnterServiceHatch(bool exiting,float dt)
        {
            if(!KnowsServiceHatch || SwimZone(pet.Position)!=(exiting?PetZone.Deck:PetZone.Sea))return false;
            var receiver=GoshaReceiver.Active;
            var pipe=receiver?receiver.ServiceRoute(deck.Mark(5)):null;
            if(!pipe || !pipe.IsConnected)return false;
            Vector3 entrance=pipe.WorldPoint(exiting?1:0);
            bool reached=exiting?Walk(entrance,tuning.petFetchSpeed,dt):Approach(entrance,.67f,dt);
            if(!reached)return true;
            serviceExiting=exiting;
            serviceProgress=0;
            pet.ServiceHatchActive=true;
            hunt.Stop();
            return true;
        }

        bool TickServiceTransit(float dt)
        {
            if(!pet.ServiceHatchActive)return false;
            var receiver=GoshaReceiver.Active;
            var pipe=receiver?receiver.ServiceRoute(deck.Mark(5)):null;
            if(!pipe){pet.ServiceHatchActive=false;return false;}
            float duration=Mathf.Max(1.15f,pipe.Length/3.1f);
            serviceProgress=Mathf.Min(1,serviceProgress+dt/duration);
            float curved=Mathf.SmoothStep(0,1,serviceProgress);
            Vector3 at=pipe.WorldPoint(serviceExiting?1-curved:curved);
            Face(at-pet.Position);
            pet.Position=at;
            if(serviceProgress>=1)
            {
                pet.ServiceHatchActive=false;
                swimRoute.Reset();
                clearingSeaDoor=false;
                hunt.Stop();
            }
            return true;
        }

        void DeliverMaterialsHome(float dt)
        {
            Vector3 home=deck.Mark(0);
            if(TryEnterServiceHatch(false,dt) || !TravelTo(home,dt))return;
            var vessel=NereidVessel.Active;
            if(!vessel)return;
            for(int i=carried.Count-1;i>=0;i--)
            {
                var item=carried[i];
                if(!item.Loot)continue;
                int cell=(int)(item.Id%9);
                Vector3 stash=home+vessel.transform.right*(.33f+(cell%3)*.23f)
                    +vessel.transform.forward*((cell/3-1)*.33f);
                if(!vessel.ContainsSealed(stash))stash=home;
                if(!item.Loot.PutDownByGosha(stash,berth:true))continue;
                carried.RemoveAt(i);
                Tally(item);
            }
            if(carried.Count==0)DeliveryComplete();
        }
    }
}
