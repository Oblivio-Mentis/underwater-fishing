using TMPro;
using UnityEngine;

namespace Raid.Nereid
{
    // Each instrument is an ordinary prefab. The display observes the same snapshot as the HUD.
    public sealed class NereidInstrument : MonoBehaviour
    {
        public ShipConsole console;
        public TMP_Text readout;
        public Transform gauge;
        float nextSample;

        void LateUpdate()
        {
            if (console == null || readout == null) return;
            var ui = UI.UiRoot.Instance;
            bool open = ui != null && ui.Context.SelectedConsole == console &&
                (ui.Page == UI.UiPage.Terminal || ui.Page == UI.UiPage.SkillTree);
            if (readout.gameObject.activeSelf == open) readout.gameObject.SetActive(!open);
            if (open || Time.unscaledTime < nextSample) return;
            nextSample = Time.unscaledTime + .25f;
            var world = RaidWorld.Instance;
            if (world == null || !world.IsClientInitialized || world.View == null) return;
            var team = world.View;
            var route = world.FishRoute;
            string heading, value, detail;
            float ratio;
            switch (console.kind)
            {
                case ShipConsoleKind.Process:
                case ShipConsoleKind.Feed:
                    bool feed = console.kind == ShipConsoleKind.Feed;
                    var target = feed ? CatchDestination.Feed : CatchDestination.Process;
                    int count = team.tank.FindAll(item => FishRouteQuote.IsDraft(item,target)).Count;
                    bool flowing = route.active && route.feeding == feed;
                    heading = feed ? "КОРМЛЕНИЕ" : "ПЕРЕРАБОТКА";
                    value = flowing ? $"Поток   {route.completed} / {route.total}" : $"Назначено   {count}";
                    detail = feed
                        ? $"Рацион   {TeamProgress.FoodText(team.offeringPoints)} / {TeamProgress.FoodText(team.RequiredFood(world.progression))}\nДо кормления   {Mathf.CeilToInt(team.hungerRemaining)} с"
                        : $"Биомасса  {team.biomass}     Хитин  {team.chitin}\nМеталл  {team.scrap}     Электроника  {team.electronics}";
                    detail += flowing ? (route.stopping ? "\nКлапан закрыт" : "\nЛКМ  ·  остановить подачу") :
                        (route.active ? "\nДругая линия занята" : count > 0 ? "\nЛКМ  ·  начать подачу" : "\nОжидание назначения");
                    ratio = flowing ? (float)route.completed / Mathf.Max(1,route.total) : (float)count / Mathf.Max(1,team.tank.Count);
                    break;
                case ShipConsoleKind.CargoControl:
                    int cap = team.Capacity(world.progression);
                    heading = "ПРИЁМКА УЛОВА";
                    value = $"Загрузка   {team.CargoUsed} / {cap}";
                    detail = team.hoistPhase switch {
                        HoistPhase.Loading => team.CargoUsed>0 ? "ЛКМ  ·  поднять клеть" : "Клеть готова к загрузке",
                        HoistPhase.Raising => "Подъём к приёмнику",
                        HoistPhase.Unloading => "Выгрузка в резервуар",
                        _ => "Возврат клети"
                    };
                    ratio = (float)team.CargoUsed / Mathf.Max(1,cap);
                    break;
                case ShipConsoleKind.Depths:
                    heading = "НАВИГАЦИЯ"; value = $"Глубина   {world.CurrentDepth}";
                    detail = world.TravelRemaining > 0 ? $"Переход   {Mathf.CeilToInt(world.TravelRemaining)} с" : "Выбрать глубину";
                    ratio = world.CurrentDepth / 2f; break;
                case ShipConsoleKind.SkillTree:
                    heading = "ОСНАЩЕНИЕ"; value = "Модули экипажа"; detail = "Дерево развития"; ratio = 0; break;
                default:
                    heading = "РЕЗЕРВУАР"; value = $"Улов   {team.tank.Count} / {team.ReservoirCapacity(world.progression)}";
                    detail = route.active ? (route.feeding ? "Подача к пасти" : "Подача в переработчик") : "Выбрать назначение рыбы";
                    ratio = (float)team.tank.Count / Mathf.Max(1,team.ReservoirCapacity(world.progression)); break;
            }
            string label = $"<color=#76CCD0>{heading}</color>\n\n<size=132%>{value}</size>\n\n{detail}";
            if (readout.text != label) readout.SetText(label);
            if (gauge != null)
            {
                var scale = gauge.localScale;
                scale.x = Mathf.Clamp01(ratio);
                gauge.localScale = scale;
            }
        }
    }
}
