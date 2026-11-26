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
        public event System.Action<MaterialCost,MaterialCost,MaterialCost,bool> ProcessingFinished;
        float nextRoutePublish;
        public FishRouteState FishRoute => fishRoute.Value;
        bool IsRouting(long id) => fishRoute.Value.active && routeIds.Contains(id);
        bool HasCommittedDispatch => team.tank.Exists(item=>item.dispatchCommitted);
        public bool IsInFishPipe(long id)
        {
            var route=FishRoute;
            int index=routeIds.IndexOf(id);
            return route.active && index>=route.completed && index<FishFlow.EnteredPipe(route.elapsed,routeIds.Count);
        }
        public bool HasFishDispatch => FishRoute.active || View!=null && View.tank.Exists(item=>item.dispatchCommitted);
        void ResetFishRoute() { routeIds.Clear();fishRoute.Value=default;nextRoutePublish=0;routeElapsed=0; }
        bool BeginFishRoute(bool feeding,bool committedOnly=false)
        {
            if(fishRoute.Value.active)return false;
            routeIds.Clear();
            var destination=feeding?CatchDestination.Feed:CatchDestination.Process;
            foreach(var item in team.tank)
            {
                if(committedOnly && (!item.dispatchCommitted || item.destination!=destination))continue;
                if(CanRoute(item,destination))routeIds.Add(item.id);
            }
            if(routeIds.Count==0)return false;
            routeElapsed=0;
            fishRoute.Value=new FishRouteState{sequence=fishRoute.Value.sequence+1,total=routeIds.Count,feeding=feeding,active=true};
            message.Value=feeding?"Рыба направлена к чудовищу":"Рыба направлена на переработку";
            return true;
        }
        bool LaunchCommittedFish()
        {
            var selected=team.tank.Find(item=>item.dispatchCommitted);
            return selected!=null && BeginFishRoute(selected.destination==CatchDestination.Feed,true);
        }

        bool CommitFishDispatch(CatchDestination? destination)
        {
            if(fishRoute.Value.active || HasCommittedDispatch)return false;
            bool any=false;
            foreach(var item in team.tank)
            {
                if(!FishRouteQuote.IsDraft(item,destination))continue;
                if(!CanRoute(item,item.destination))return false;
                any=true;
            }
            if(!any)return false;
            foreach(var item in team.tank)
                item.dispatchCommitted=FishRouteQuote.IsDraft(item,destination);
            return LaunchCommittedFish();
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
                if(item!=null)
                {
                    item.destination=CatchDestination.Hold;
                    item.dispatchCommitted=false;
                }
                routeIds.RemoveAt(i);
            }
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
                LaunchCommittedFish();
                return;
            }
            if(routeElapsed>state.elapsed)state.elapsed=routeElapsed;
            state.elapsed+=Time.fixedDeltaTime;
            routeElapsed=state.elapsed;
            bool changed=false;
            while(state.completed<routeIds.Count && state.elapsed>=FishFlow.DeliverySeconds+state.completed*FishFlow.Interval)
            {
                long id=routeIds[state.completed];
                try
                {
                    bool delivered=state.feeding?team.Feed(id,progression):ProcessDeliveredFish(id);
                    if(!delivered && team.tank.Exists(item=>item.id==id && CanRoute(item,state.feeding?CatchDestination.Feed:CatchDestination.Process)))
                    {
                        StopFullResourceRoute(ref state);
                        changed=true;
                        break;
                    }
                    if(!delivered) ReleaseFishDispatch(id);
                    else if(state.feeding) catchOwners.Remove(id);
                    state.completed++;
                    changed=true;
                }
                catch(System.OverflowException)
                {
                    StopFullResourceRoute(ref state);
                    changed=true;
                    break;
                }
            }
            if(state.active && state.completed>=routeIds.Count){state.active=false;routeIds.Clear();message.Value=state.feeding?"Корм доставлен чудовищу":"Переработка завершена";changed=true;}
            if(changed){Publish();Save();}
            if(changed || Time.unscaledTime>=nextRoutePublish){nextRoutePublish=Time.unscaledTime+.1f;fishRoute.Value=state;}
        }

        void StopFullResourceRoute(ref FishRouteState state)
        {
            state.active=false;state.failed=true;
            foreach(long queued in routeIds)ReleaseFishDispatch(queued);
            routeIds.Clear();
            message.Value="Подача остановлена. Запас ресурса заполнен, оставшаяся рыба сохранена.";
        }

        void ReleaseFishDispatch(long id)
        {
            var fish=team.tank.Find(item=>item.id==id);
            if(fish==null)return;
            fish.destination=CatchDestination.Hold;
            fish.dispatchCommitted=false;
        }

        [FishNet.Object.ServerRpc(RequireOwnership=false)]
        internal void AssignFishRoutes(CatchDestination source,CatchDestination destination,long[] ids,Vector3 contact,int requestId,
            FishNet.Connection.NetworkConnection sender=null)
        {
            if(sender?.FirstObject==null || team==null)return;
            if(transitionRemaining>0){Respond(sender,requestId,false,"travel");return;}
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(!diver || diver.IsHelpless){Respond(sender,requestId,false,"helpless");return;}
            if(!diver.IsInside || !ShipConsole.CanPressAt(diver,ShipConsoleKind.Reservoir,contact))
            {Respond(sender,requestId,false,"range");return;}
            if(commandTimes.TryGetValue(sender.ClientId,out float last) && Time.unscaledTime-last<.15f)
            {Respond(sender,requestId,false,"cooldown");return;}
            commandTimes[sender.ClientId]=Time.unscaledTime;
            if(source==destination || (byte)source>(byte)CatchDestination.Feed || (byte)destination>(byte)CatchDestination.Feed ||
                ids==null || ids.Length==0 || ids.Length>team.tank.Count)
            {Respond(sender,requestId,false,"changed");return;}
            var selected=new System.Collections.Generic.HashSet<long>(ids);
            var batch=team.tank.FindAll(item=>selected.Contains(item.id) && item.destination==source &&
                !item.dispatchCommitted && !IsRouting(item.id) && CanRoute(item,destination));
            if(batch.Count!=ids.Length){Respond(sender,requestId,false,"changed");return;}
            foreach(var item in batch)item.destination=destination;
            if(!SaveTransaction())
            {
                foreach(var item in batch)item.destination=source;
                Respond(sender,requestId,false,"unavailable");
                return;
            }
            diver.ConfirmInteraction();
            Publish();
            Respond(sender,requestId,true,"ok");
        }

        bool ChooseFishRoute(long id,CatchDestination destination)
        {
            var item=team.tank.Find(fish=>fish.id==id);
            if(item==null || item.dispatchCommitted || IsRouting(id) || item.destination==destination || !CanRoute(item,destination))return false;
            item.destination=destination;
            return true;
        }

        internal static bool CanRoute(CatchItem item,CatchDestination destination) => destination switch
        {
            CatchDestination.Feed=>!item.IsSalvage && item.offering>0,
            CatchDestination.Process=>!item.IsSalvage && (item.biomass>0 || item.chitin>0 || item.scrap>0 || item.electronics>0),
            _=>true
        };

        bool ProcessDeliveredFish(long id)
        {
            var before=team.Treasury;
            if(!team.CreditProcessedCatch(id,progression,out var total))return false;
            catchOwners.Remove(id);
            SendProcessingReceipt(total,before,team.Treasury);
            return true;
        }

        [FishNet.Object.ObserversRpc]
        void SendProcessingReceipt(MaterialCost total,MaterialCost before,MaterialCost after,bool fromSalvage=false)
        {
            ProcessingFinished?.Invoke(total,before,after,fromSalvage);
        }
    }
}
