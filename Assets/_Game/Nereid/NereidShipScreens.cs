using TMPro;
using UnityEngine;

namespace Raid.Nereid
{
    // Idle physical displays read the same confirmed team state as the interactive console UI.
    public sealed class NereidShipScreens : MonoBehaviour
    {
        public ShipConsole[] stations;
        public TMP_Text[] screens;
        float next;
        void Update()
        {
            if(Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            var w=RaidWorld.Instance;
            if(w==null||w.NetworkObject==null||!w.IsClientInitialized)return;
            var team=w.View;if(team==null)return;
            var ui=UI.UiRoot.Instance;
            for(int i=0;i<screens.Length;i++)
            {
                var station=stations[i];var text=screens[i];if(station==null||text==null)continue;
                bool interactive=ui!=null&&ui.Page==UI.UiPage.Terminal&&ui.Context.SelectedConsole==station;
                text.gameObject.SetActive(!interactive);if(interactive)continue;
                string status;
                if(station.kind==ShipConsoleKind.Upgrades)
                    status="ОБОРУДОВАНИЕ ЭКИПАЖА\n\nБИОМАССА  "+team.biomass+"   ХИТИН  "+team.chitin+"\nМЕТАЛЛ  "+team.scrap+"   ЭЛЕКТРОНИКА  "+team.electronics+"\n\nКАТУШКА "+team.reelLevel+"  /  КОРПУС "+team.hullLevel+"\nЛАСТЫ "+team.finsLevel+"  /  БАЛЛОН "+team.tankLevel+"\nЛЕБЁДКА "+team.winchLevel+"\n\nE / ЛКМ · ОТКРЫТЬ";
                else if(station.kind==ShipConsoleKind.Depths)
                    status="НАВИГАЦИЯ NEREID\n\nГЛУБИНА "+w.CurrentDepth+"\n\nПЕРВАЯ ГЛУБИНА · ДОСТУПНА\nВТОРАЯ ГЛУБИНА · "+(team.CanVisitDepth(2)?"ДОСТУПНА":"НУЖЕН КОРПУС")+"\n\n"+(w.TravelRemaining>0?"ПЕРЕХОД · "+Mathf.CeilToInt(w.TravelRemaining)+" С":"E / ЛКМ · ВЫБОР КУРСА");
                else
                {
                    string route=w.FishRoute.active
                        ? (w.FishRoute.feeding?"КОРМЛЕНИЕ ":"ПЕРЕРАБОТКА ")+w.FishRoute.completed+" / "+w.FishRoute.total
                        : "ЛИНИИ СВОБОДНЫ";
                    status="РЕЗЕРВУАР\n\nРЫБА  "+team.tank.Count+"\nНАСЫЩЕНИЕ  "+TeamProgress.FoodText(team.offeringPoints)+
                        "\n\n"+route+"\n\nE / ЛКМ · ВЫБОР УЛОВА";
                }
                if(text.text!=status)text.SetText(status);
            }
        }
    }
}
