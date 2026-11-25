using System.Linq;
using UnityEngine;
using Key = Raid.UI.HudInputGlyph.Control;
using Mark = Raid.UI.DiveInstruments.Mark;

namespace Raid.UI
{
    // All prompts are built from the same authoritative targets and interaction gates as the input handlers.
    public static partial class UiText
    {
        internal static HudHint ContextAction(UiContext c)
        {
            var player=c.Player;var world=c.World;var tool=c.Harpoon;var ui=UiRoot.Instance;
            if(!player || !world || !tool)return default;
            if(player.IsDead)
            {
                var watch=c.Players.Any(member=>member && member!=player && !member.IsHelpless)
                    ?new HudBinding(Key.Right,"Следующий водолаз"):default;
                return new(Mark.Helmet,player.CanRespawn?new(Key.E,"Вернуться на борт"):watch,player.CanRespawn?watch:default);
            }
            if(player.IsHelpless)return default;
            if(c.Interaction==UiInteraction.Rescue && c.RescueTarget!=null)
            {
                float progress=c.Rescue?.DisplayProgress??0;
                return new HudHint(Mark.Glove,new(Key.F,"Удерживайте для спасения"),note:UiText.PlayerName(c.RescueTarget)){progress=progress};
            }
            if(world.HasGoshaBall(player) && world.Gosha.Mode is GoshaMode.Chasing or GoshaMode.Pouncing)
                return new(Mark.Ball,new(Key.Q,"Выложить мяч"),note:"Гоша бежит за вами");
            if(world.GoshaBallInHands(player))
                return new(Mark.Ball,new(ui!=null && ui.WindingBall?Key.LiftLeft:Key.Left,ui!=null && ui.WindingBall?"Отпустите для броска":"Удерживайте для замаха"),new(Key.Q,"Выложить мяч"));
            if(world.IsHoldingGosha(player))return new(Mark.Glove,new(Key.E,"Опустить Гошу"));
            if(tool.IsPulled)
                return new HudHint(Mark.Spear,new(Key.R,"Отцепиться от троса"),note:UiText.HudTetherActor(tool.TetherOwner,true)){caution=true};
            if(tool.HasBallLine)return new(Mark.Ball,new(Key.Left,"Подтянуть мяч"),new(Key.R,"Отцепить мяч"));
            if(tool.TetherActive && tool.HookedFish==null)
            {
                var other=tool.TetherTarget;
                string distance=other?UiText.HudTetherDistance(Vector3.Distance(player.transform.position,other.transform.position),tool.TetherMaxRange):null;
                return new(Mark.Spear,new(Key.Left,"Подтянуть напарника"),new(Key.R,"Отпустить трос"),note:distance);
            }
            var assist=tool.AssistTarget;
            var owner=assist?assist.GetComponent<HarpoonTool>():tool;
            var hooked=owner?owner.HookedFish:null;
            if(hooked && hooked.Mode>=FishMode.Hooked && hooked.Mode<=FishMode.Tired)
            {
                if(assist)return new(Mark.Glove,new(tool.ActiveSlot==1?Key.Left:Key.E,"Стабилизировать напарника"),note:"Хват не заменяет второй гарпун");
                bool release=hooked.Surging || hooked.Behaviour==FishBehaviour.LastEscape || tool.LineTension>=LineTensionArc.Danger;
                var input=release?Key.LiftLeft:hooked.RequiresPartner && hooked.HookCount<2?Key.None:Key.Left;
                string remaining=tool.IsAssisted?"Хват ослабляет унос":tool.StabilizerCooldown>0?"Стабилизатор · "+tool.StabilizerCooldown+" с":null;
                return new HudHint(Mark.Spear,new(input,ReelReadout.ActionHint(tool,hooked)),new(Key.R,"Освободить трос"),note:remaining)
                    {caution=release||hooked.SurgeWarning};
            }
            if(ui && ui.AimedCargoDispatch)return Dispatch(c);
            var kind=tool.SelectedItem(out _);
            if(c.View!=null && c.Tuning && CargoDispatch.CanLoad(world,player))
            {
                if(kind==DroppedKind.Salvage)return Load(c,1,true,kind,tool.CarriedSalvage.DisplayName);
                if(tool.FishLoadTarget(out var fish,out _))return Load(c,fish.cargoSize,false,kind,ReelReadout.Species(fish.SpeciesId));
                if(kind!=DroppedKind.None)return new HudHint(Mark.Crate,note:"Этот предмет нельзя поместить в клеть"){caution=true};
            }
            if(ui && ui.AimedPad!=null)
            {
                var pad=ui.AimedPad;
                if(pad.CanRequest)return new(Mark.Submarine,new(Key.Left,pad.Sea?"Затопить шлюз":"Осушить шлюз"));
                return new HudHint(Mark.Submarine,note:world.Airlock.Caption){caution=true};
            }
            switch(c.Interaction)
            {
                case UiInteraction.PetGosha:return new(Mark.Glove,new(Key.E,"Взять и погладить Гошу"));
                case UiInteraction.PickUpBall:return new(Mark.Ball,new(Key.E,"Поднять мяч"));
                case UiInteraction.DroppedItem when tool.CanPickUp(c.AimedDrop):
                    var item=c.AimedDrop;
                    return item.kind==DroppedKind.Harpoon && tool.OwnsHarpoon?new(Mark.Spear,new(Key.E,"Обменять гарпун")):
                        new(Icon(item.kind),new(Key.E,"Поднять"),note:item.Label);
                case UiInteraction.Loot when !tool.HandsFull:
                    return new(Mark.Crate,new(Key.E,"Взять обеими руками"),note:c.AimedLoot.DisplayName);
                case UiInteraction.Assist:
                    return new(Mark.Glove,new(tool.ActiveSlot==1?Key.Left:Key.E,"Удерживать напарника"));
                case UiInteraction.Cargo:
                    return new(Mark.Crate,new(Key.E,c.HasCargo && c.View?.hoistPhase==HoistPhase.Loading?"Загрузить и открыть клеть":"Осмотреть клеть"));
                case UiInteraction.Terminal:return Station(c);
                case UiInteraction.Fish when c.AimedFish && !tool.HandsFull:
                    var fish=c.AimedFish;
                    if(!tool.OwnsHarpoon)break;
                    if(!tool.HarpoonInHands)return new(Mark.Spear,new(Key.One,"Взять гарпун"));
                    if(fish.Mode==FishMode.Roam)
                    {
                        if(tool.HasFullCreel && !fish.RequiresPartner)return new HudHint(Mark.Crate,note:"В инвентаре нет места для рыбы"){caution=true};
                        return new(Mark.Spear,new(Key.Left,"Зацепить рыбу"),new(Key.Right,"Прицелиться"),note:fish.RequiresPartner?"Нужен второй гарпун":null);
                    }
                    if(fish.RequiresPartner && fish.Mode>=FishMode.Hooked && fish.Mode<=FishMode.Tired)
                        return new(Mark.Spear,new(Key.Left,"Поставить второй гарпун"));
                    return new(Mark.Spear,note:"Рыба уже зацеплена");
            }
            if(tool.HandsFull || kind==DroppedKind.Catch)
                return new(Icon(kind),new(Key.Q,"Выбросить"),note:tool.CarriedSalvage!=null?tool.CarriedSalvage.DisplayName:ReelReadout.Species((tool.HoldingHeavyCatch?tool.CarriedFish:tool.FishInSlot(tool.ActiveSlot))?.SpeciesId));
            var chamber=Nereid.NereidAirlock.Active;
            if(chamber && chamber.Contains(player.transform.position))
            {
                var state=world.Airlock;
                string caption=state.Busy?state.Caption+" · "+Mathf.RoundToInt(state.Water*100)+"%":
                    state.Phase==AirlockPhase.InnerOpen?"Выход в море · кнопка у внешней двери":"Вход на борт · кнопка у внутренней двери";
                return new(Mark.Submarine,note:caption);
            }
            bool raidNearby=c.DefendingReservoir && (c.Threats.WavePosition-player.transform.position).sqrMagnitude<15*15;
            if(raidNearby || !player.IsInside && HudWarnings.AttackNearby(c))
            {
                if(tool.HarpoonInHands)return new(Mark.Jaw,new(Key.Left,"Выстрелить в хищника"),new(Key.Right,"Прицелиться"));
                if(tool.OwnsHarpoon)return new(Mark.Spear,new(Key.One,"Взять гарпун"));
            }
            if(tool.HarpoonInHands)return new HudHint(Mark.Spear,new(Key.Left,"Выстрелить"),new(Key.Right,"Прицелиться"),kind==DroppedKind.Harpoon?new(Key.Q,"Выбросить"):default){idle=true};
            string orders=world.GoshaTrainingHint(player);
            if(!string.IsNullOrEmpty(orders))return Training(world,orders);
            return default;
        }
        static Mark Icon(DroppedKind kind)=>kind switch {DroppedKind.Harpoon=>Mark.Spear,DroppedKind.Ball=>Mark.Ball,DroppedKind.Catch=>Mark.Lantern,_=>Mark.Crate};
        static HudHint Training(RaidWorld world,string orders)
        {
            if(world.Gosha.Mode is GoshaMode.Coming or GoshaMode.Fishing or GoshaMode.BringingFish)
                return new(Mark.Glove,note:orders);
            var hint=new HudHint(Mark.Glove){idle=true};
            var team=world.View;
            void Add(bool unlocked,Key key,string action)
            {
                if(!unlocked)return;
                var row=new HudBinding(key,action);
                if(hint.first.label==null)hint.first=row;
                else if(hint.second.label==null)hint.second=row;
                else hint.third=row;
            }
            Add(true,Key.G,"Позвать Гошу");
            Add(team!=null && team.TeamRank(SkillLine.GoshaAutonomy)>0,Key.H,(team?.TeamRank(SkillLine.GoshaAutonomy)??0)>=5?"Гоша, на промысел":"Гоша, принеси рыбку");
            Add(true,Key.J,"Сопровождать");
            return hint;
        }
        static HudHint Load(UiContext c,int size,bool salvage,DroppedKind selected,string itemName)
        {
            var v=c.View;int capacity=v.Capacity(c.Tuning);
            string dropText=selected==DroppedKind.Harpoon?"Выбросить гарпун":salvage?"Выбросить груз":"Выбросить рыбу";
            var drop=new HudBinding(Key.Q,dropText);
            if(v.hoistPhase!=HoistPhase.Loading || v.CargoUsed+size>capacity)
                return new HudHint(Mark.Crate,drop,note:v.hoistPhase!=HoistPhase.Loading?"Клеть в пути":"В клети не хватает места"){caution=true};
            return new(Mark.Crate,new(Key.Left,"Положить в клеть"),drop,note:itemName+" · "+UiText.HudCargoCount(v.CargoUsed,capacity));
        }
        internal static HudHint MonitorActions(ShipConsole station)
        {
            if(!station)return default;
            return station.kind switch
            {
                ShipConsoleKind.SkillTree=>new HudHint(Mark.Wrench,new(Key.Left,"Выбрать навык"),new(Key.Right,"Купить доступный навык"),new(Key.Wheel,"Масштаб дерева")){idle=true},
                ShipConsoleKind.Reservoir=>new HudHint(Mark.Lantern,new(Key.Left,"Выбрать назначение"),new(Key.Wheel,"Прокрутить список")){idle=true},
                _=>new HudHint(Mark.Bathymetry,new(Key.Left,"Выбрать на экране")){idle=true}
            };
        }
        static HudHint Dispatch(UiContext c)
        {
            var v=c.View;
            if(v==null||c.Tuning==null)return default;
            if(v.hoistPhase!=HoistPhase.Loading)return new(Mark.Crate,note:UiText.Phase(v.hoistPhase));
            if(v.CargoUsed==0)return new(Mark.Crate,note:"Клеть пуста");
            return new(Mark.Crate,new(Key.Left,"Поднять клеть"),note:UiText.HudCargoCount(v.CargoUsed,v.Capacity(c.Tuning)));
        }
        static HudHint Station(UiContext c)
        {
            var console=c.AimedConsole;
            if(!console)return new(Mark.Submarine,new(Key.E,"Открыть терминал"));
            if(console.kind==ShipConsoleKind.CargoControl)return Dispatch(c);
            if(console.IsMonitor)return new(Mark.Submarine,new(Key.Left,"Выбрать на экране"),note:console.Caption);
            bool feed=console.kind==ShipConsoleKind.Feed;
            if(!feed && console.kind!=ShipConsoleKind.Process)return new(Mark.Wrench,new(Key.Left,console.Caption));
            var route=c.World.FishRoute;
            var icon=feed?Mark.Jaw:Mark.Wrench;
            if(route.active)return route.feeding!=feed||route.stopping
                ?new(icon,note:route.stopping?"Клапан закрывается":"Работает другая линия")
                :new(icon,new(Key.Left,"Остановить подачу"),note:"Рыб в пути · "+(route.total-route.completed));
            var dest=feed?CatchDestination.Feed:CatchDestination.Process;
            int selected=c.View?.tank.Count(item=>FishRouteQuote.IsDraft(item,dest))??0;
            return selected>0?new(icon,new(Key.Left,feed?"Отправить на кормление":"Начать переработку"),note:"Выбрано рыб · "+selected)
                :new(icon,note:"Назначьте рыбу на экране аквариума");
        }
        internal static void Warning(HudWarnings.Signal s,UiContext c,float air,out string name,out string advice,out Mark mark,out bool critical)
        {
            var p=c.Player;
            (name,advice,mark,critical)=s switch
            {
                HudWarnings.Signal.Dead=>(p.Spectating?"Наблюдение · "+UiText.HudPlayerName(p.Spectating):p.IsConsumed?"Вас проглотил левиафан":"Водолаз погиб",
                    !p.IsConsumed && !p.CanRespawn && c.Players.Exists(other=>other && other!=p && !other.IsHelpless)?"Напарник может вас спасти":"",Mark.Helmet,false),
                HudWarnings.Signal.Trap=>("Костюм зажат","Напарник может разжать створки",Mark.Glove,true),
                HudWarnings.Signal.Down=>("Нужна помощь","Дождитесь напарника",Mark.Helmet,true),
                HudWarnings.Signal.Hypoxia=>("Гипоксия","Доберитесь до сухого отсека"+(p.AirReserveSpent&&p.AirReserveCooldown>0?" · резерв через "+p.AirReserveCooldown+" с":""),Mark.Bottle,true),
                HudWarnings.Signal.Impact=>("Получен удар","Уходите в укрытие",Mark.Shield,true),
                HudWarnings.Signal.Attack=>("Хищник атакует рядом","Смените направление",Mark.Jaw,true),
                HudWarnings.Signal.Reserve=>("Аварийный воздух",Mathf.CeilToInt(p.AirReserveRemaining)+" с до истощения резерва",Mark.Bottle,false),
                HudWarnings.Signal.CriticalAir=>("Кислород · "+Mathf.CeilToInt(air*100)+"%","Срочно вернитесь в шлюз",Mark.Bottle,true),
                HudWarnings.Signal.Oxygen=>("Мало воздуха · "+Mathf.CeilToInt(air*100)+"%","Возвращайтесь к шлюзу",Mark.Bottle,false),
                HudWarnings.Signal.CriticalPressure=>("Опасное давление","Поднимитесь выше",Mark.Shield,true),
                HudWarnings.Signal.Pressure=>("Давление растёт","Уменьшите глубину",Mark.Shield,false),
                _=>("Атака на резервуар","Защитите улов гарпуном",Mark.Jaw,true)
            };
        }
    }
}