using System.Collections.Generic;
using System.Linq;
using FishNet.Object.Synchronizing;
using UnityEngine;
namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly SyncVar<FishRouteState> fishRoute=new();
        readonly List<long> routeIds=new();
        float nextRoutePublish;
        public FishRouteState FishRoute => fishRoute.Value;
        bool IsRouting(long id) => fishRoute.Value.active && routeIds.Contains(id);
        void ResetFishRoute() { routeIds.Clear();fishRoute.Value=default;nextRoutePublish=0;routeElapsed=0; }
        bool BeginFishRoute(bool feeding)
        {
            if(fishRoute.Value.active || (!feeding&&!AtBase))return false;
            routeIds.Clear();routeIds.AddRange(team.tank.Where(x=>x.offering>0).Select(x=>x.id));
            if(routeIds.Count==0)return false;
            routeElapsed=0;
            fishRoute.Value=new FishRouteState{sequence=fishRoute.Value.sequence+1,total=routeIds.Count,feeding=feeding,active=true};
            message.Value=feeding?"Рыба направлена к чудовищу":"Рыба направлена на переработку";
            return true;
        }
        float routeElapsed;
        void TickRouteClock()
        {
            var state=fishRoute.Value;
            if(!state.active){routeElapsed=0;return;}
            if(routeElapsed>state.elapsed)state.elapsed=routeElapsed;
            state.elapsed+=Time.fixedDeltaTime;
            routeElapsed=state.elapsed;
            bool changed=false;
            while(state.completed<routeIds.Count && state.elapsed>=8f+state.completed*.18f)
            {
                long id=routeIds[state.completed];
                try {changed|=state.feeding?team.Feed(id):team.Process(id);state.completed++;}
                catch(System.OverflowException)
                {
                    state.active=false;state.failed=true;routeIds.Clear();changed=true;
                    message.Value="Подача остановлена: достигнут предел ресурса. Оставшаяся рыба сохранена";
                    break;
                }
            }
            if(state.active && state.completed>=routeIds.Count){state.active=false;routeIds.Clear();message.Value=state.feeding?"Корм доставлен чудовищу":"Переработка завершена";changed=true;}
            if(changed){Publish();Save();}
            if(changed || Time.unscaledTime>=nextRoutePublish){nextRoutePublish=Time.unscaledTime+.1f;fishRoute.Value=state;}
        }
    }
}
