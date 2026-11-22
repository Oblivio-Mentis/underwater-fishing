using System.Collections.Generic;
using System.Linq;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly SyncVar<FishRouteState> fishRoute=new();
        readonly SyncList<long> routeIds=new();
        public long ActiveRouteId(int index) => index>=0 && index<routeIds.Count?routeIds[index]:0;
        public event System.Action<MaterialCost,MaterialCost,MaterialCost> ProcessingFinished;
        float nextRoutePublish;
        public FishRouteState FishRoute => fishRoute.Value;
        bool IsRouting(long id) => fishRoute.Value.active && routeIds.Contains(id);
        void ResetFishRoute() { routeIds.Clear();fishRoute.Value=default;nextRoutePublish=0;routeElapsed=0; }
        bool BeginFishRoute(bool feeding,long selected=0)
        {
            if(fishRoute.Value.active)return false;
            routeIds.Clear();
            foreach(var item in team.tank)
                if(selected>0?item.id==selected:feeding?item.offering>0:item.offering>0 || item.biomass+item.chitin+item.scrap+item.electronics>0)
                    routeIds.Add(item.id);
            if(routeIds.Count==0)return false;
            routeElapsed=0;
            fishRoute.Value=new FishRouteState{sequence=fishRoute.Value.sequence+1,total=routeIds.Count,feeding=feeding,active=true};
            message.Value=feeding?"Рыба направлена к чудовищу":"Рыба направлена на переработку";
            return true;
        }
        bool StopFishRoute(bool feeding,long sequence)
        {
            var state=fishRoute.Value;
            if(!state.active||state.stopping||state.feeding!=feeding||state.sequence!=sequence)return false;
            state.elapsed=Mathf.Max(state.elapsed,routeElapsed);
            int entered=Mathf.Max(state.completed,FishFlow.EnteredPipe(state.elapsed,routeIds.Count));
            for(int i=routeIds.Count-1;i>=entered;i--)
            {
                var item=team.tank.Find(fish=>fish.id==routeIds[i]);
                if(item!=null)item.destination=CatchDestination.Hold;
                routeIds.RemoveAt(i);
            }
            var cancelled=feeding?CatchDestination.Feed:CatchDestination.Process;
            foreach(var item in team.tank)
                if(item.destination==cancelled && !routeIds.Contains(item.id))item.destination=CatchDestination.Hold;
            state.total=entered;state.stopping=true;
            if(state.completed>=entered){state.active=false;routeIds.Clear();}
            fishRoute.Value=state;
            message.Value=state.active?"Подача остановлена. Рыба в трубе завершает путь":"Подача остановлена. Оставшаяся рыба в резервуаре";
            return true;
        }
        float routeElapsed;
        void TickRouteClock()
        {
            var state=fishRoute.Value;
            if(!state.active)
            {
                routeElapsed=0;
                if(transitionRemaining>0)return;
                var next=team.tank.Find(item=>item.destination!=CatchDestination.Hold);
                if(next!=null)BeginFishRoute(next.destination==CatchDestination.Feed,next.id);
                return;
            }
            if(routeElapsed>state.elapsed)state.elapsed=routeElapsed;
            state.elapsed+=Time.fixedDeltaTime;
            routeElapsed=state.elapsed;
            bool changed=false;
            while(state.completed<routeIds.Count && state.elapsed>=FishFlow.DeliverySeconds+state.completed*FishFlow.Interval)
            {
                long id=routeIds[state.completed];
                try {changed|=state.feeding?team.Feed(id,progression):ProcessDeliveredFish(id);state.completed++;}
                catch(System.OverflowException)
                {
                    state.active=false;state.failed=true;
                    foreach(long queued in routeIds)
                    {
                        var item=team.tank.Find(fish=>fish.id==queued);
                        if(item!=null)item.destination=CatchDestination.Hold;
                    }
                    routeIds.Clear();changed=true;
                    message.Value="Подача остановлена: достигнут предел ресурса. Оставшаяся рыба сохранена";
                    break;
                }
            }
            if(state.active && state.completed>=routeIds.Count){state.active=false;routeIds.Clear();message.Value=state.feeding?"Корм доставлен чудовищу":"Переработка завершена";changed=true;}
            if(changed){Publish();Save();}
            if(changed || Time.unscaledTime>=nextRoutePublish){nextRoutePublish=Time.unscaledTime+.1f;fishRoute.Value=state;}
        }

        bool ChooseFishRoute(long id,CatchDestination destination)
        {
            var item=team.tank.Find(fish=>fish.id==id);
            if(item==null || IsRouting(id) || item.destination==destination)return false;
            if(destination==CatchDestination.Feed && item.offering<=0)return false;
            if(destination==CatchDestination.Process &&
                item.biomass==0 && item.chitin==0 && item.scrap==0 && item.electronics==0)return false;
            item.destination=destination;
            return true;
        }

        bool ProcessDeliveredFish(long id)
        {
            var before=team.Treasury;
            if(!team.CreditProcessedCatch(id,progression,out var total))return false;
            catchOwners.Remove(id);
            SendProcessingReceipt(total,before,team.Treasury);
            return true;
        }

        [FishNet.Object.ObserversRpc]
        void SendProcessingReceipt(MaterialCost total,MaterialCost before,MaterialCost after)
        {
            ProcessingFinished?.Invoke(total,before,after);
        }
    }
}
