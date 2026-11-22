using System.Collections.Generic;
using UnityEngine;

namespace Raid.UI
{
    public static partial class UiText
    {
        public const string TerminalTitle="РЕЗЕРВУАР И БОРТОВЫЕ СИСТЕМЫ";
        public const string TerminalKicker="NEREID  /  БОРТОВОЙ ТЕРМИНАЛ";
        public const string CloseTerminal="Закрыть";
        public const string CatchTab="Улов";
        public const string UpgradesTab="Улучшения";
        public const string DepthsTab="Глубины";
        public const string FeedingTab="Кормление";
        public const string BiomassLabel="БИОМАССА";
        public const string ChitinLabel="ХИТИН";
        public const string ScrapLabel="МЕТАЛЛОЛОМ";
        public const string ElectronicsLabel="ЭЛЕКТРОНИКА";
        public const string NoConfirmedState="Ожидание подтверждённого состояния с сервера…";
        public const string NoStock="Резервуар пуст";
        public const string NoStockHint="Поймайте рыбу или подберите груз, загрузите контейнер и запустите лебёдку. Доставленный улов появится здесь.";
        public const string ProcessAction="Переработать";
        public const string FeedAction="Покормить";
        public const string ProcessTitle="Переработать этот улов?";
        public const string FeedTitle="Отдать эту рыбу чудовищу?";
        public const string ConsumeWarning="Единица улова будет израсходована без возможности отмены.";
        public const string WaitCommand="Ожидание ответа сервера…";
        public const string NeedTerminal="Подойдите к терминалу резервуара";
        public const string NeedCargo="Подойдите к контейнеру";
        public const string NeedRescue="Сначала дождитесь спасения";
        public const string TravelWait="Дождитесь завершения перехода";
        public const string ItemGone="Эта единица улова уже израсходована или потеряна";
        public const string CannotFeed="Этот предмет не является пищей";
        public const string NoYield="Этот предмет не содержит перерабатываемых материалов";
        public const string StockIdentityHint="Каждая строка — отдельная доставленная единица. Список прокручивается.";
        public const string BuyUpgrade="Улучшить";
        public const string MaxUpgrade="Максимальный уровень";
        public const string MissingRecipe="Рецепт этого улучшения не задан";
        public const string UpgradeReady="Материалы доступны";
        public const string BuyConfirmTitle="Установить улучшение?";
        public const string UpgradeConfirmHint="Материалы спишутся после проверки сервером.";
        public const string DepthOneTitle="ГЛУБИНА 1  /  МЕЛКОВОДЬЕ";
        public const string DepthTwoTitle="ГЛУБИНА 2  /  ГЛУБОКАЯ ЗОНА";
        public const string DepthOneLoot="Биомасса и металлолом. Небольшая рыба, крупная добыча и грузовые ящики.";
        public const string DepthTwoLoot="Хитин и электроника. Сильнее сопротивление крупной рыбы и опаснее хищник.";
        public const string DepthOneRisk="Следите за кислородом и хищником. Возвращайте улов через лебёдку.";
        public const string DepthTwoRisk="Большая глубина требует усиленного корпуса. Следите за давлением и кислородом.";
        public const string DepthAvailable="Доступ открыт";
        public const string DepthLocked="Доступ закрыт: нужен Корпус I";
        public const string StartExpedition="Перейти на глубину";
        public const string DiveConfirmTitle="Сменить глубину?";
        public const string DiveConfirmHint="Вся команда должна быть на борту. Незавершённый грузовой цикл блокирует переход.";
        public const string AlreadyHere="Подлодка уже на этой глубине";
        public const string NotAllInside="Не все участники находятся на борту";
        public const string FinishCargoCycle="Дождитесь завершения грузового цикла";
        public const string EmptyCargoBeforeTravel="Сначала доставьте груз в резервуар";
        public const string CargoFromTerminal="Контейнер и лебёдка";
        public const string CargoFromTerminalHint="Подъём запускается у контейнера. Доставленный улов поступает в этот резервуар.";
        public const string HungerTitle="ПИТАНИЕ И ЗАЩИТА РЕЗЕРВУАРА";
        public const string FeedingHint="Выберите рыбу для кормления и отправьте улов. Насыщение засчитывается при доставке в пасть.";
        public const string NoFoodStock="В резервуаре нет рыбы для кормления";
        public const string NoFoodHint="Доставьте биологический улов через лебёдку. Металлолом и электронику перерабатывают в материалы.";
        public const string FeedingOutcomes="ИСХОДЫ ЦИКЛА";
        public const string OutcomeNone="Если к концу срока пасть не получит пищи, налётчик пойдёт к резервуару.";
        public const string OutcomeUnder="Даже частичная порция предотвращает налёт в этом цикле.";
        public const string OutcomeEnough="Полный рацион утоляет голод чудовища.";
        public const string TechnologiesSafe="Нападение угрожает необработанному улову. Технологии и улучшения сохраняются.";
        public const string WaveActiveTitle="НАПАДЕНИЕ: ЗАЩИТИТЕ РЕЗЕРВУАР";
        public const string CargoTitle="КОНТЕЙНЕР И ЛЕБЁДКА";
        public const string CargoKicker="NEREID  /  ГРУЗОВОЙ ПОСТ";
        public const string CapacityTitle="ЗАНЯТО / ВМЕСТИМОСТЬ";
        public const string CargoPhaseTitle="ТЕКУЩАЯ ФАЗА";
        public const string CargoPhaseSequence="Загрузка  /  Подъём  /  Разгрузка  /  Спуск";
        public const string LoadCargoAction="Загрузить переносимый улов";
        public const string StartHoistAction="Запустить подъём";
        public const string EmptyCarry="Вы не переносите рыбу или технический груз";
        public const string CargoFull="В контейнере недостаточно свободного места";
        public const string CargoLocked="Загрузка закрыта до возвращения контейнера";
        public const string CargoEmpty="Сначала загрузите улов в контейнер";
        public const string LoadReady="Улов можно загрузить";
        public const string HoistReady="Контейнер готов к подъёму";
        public const string CargoLoadingHint="Рыба в контейнере ещё не доставлена. Запустите подъём и дождитесь разгрузки.";
        public const string CargoRaisingHint="Контейнер поднимается. Новая загрузка временно закрыта.";
        public const string CargoUnloadingHint="Улов передаётся в резервуар после завершения разгрузки.";
        public const string CargoReturningHint="Улов уже в резервуаре. Дождитесь возвращения пустой корзины.";
        public static string TerminalSummary(TeamProgress view,int required,bool defending) => "Улов: "+view.tank.Count+"   •   Пища: "+TeamProgress.FoodText(view.offeringPoints)+" / "+required+"   •   "+(defending?"Оборона резервуара":"До кормления "+Time(view.hungerRemaining));
        public static string CargoSummary(TeamProgress view,ProgressionTuning tuning) => "Контейнер "+view.CargoUsed+" / "+view.Capacity(tuning)+"   •   "+Phase(view.hoistPhase)+(view.hoistPhase==HoistPhase.Loading?"":"   "+Time(view.hoistRemaining));
        public static string YieldLabel(CatchItem item) => Materials(item.biomass,item.chitin,item.scrap,item.electronics);
        public static string ItemTitle(int index,CatchItem item) => (index+1).ToString("00")+"  /  "+ItemName(item);
        public static string FoodValue(int value) => "Пища: "+value;
        public static string ItemUnits(int value) => "Занимает в контейнере: "+value;
        public static string UpgradeLevel(int level,int maximum) => "Уровень "+level+" / "+maximum;
        public static string UpgradeTarget(int level) => "Следующий уровень: "+level;
        public static string BuyUpgradeLevel(int level) => "Улучшить до "+level;
        public static string UpgradeCost(MaterialCost cost) => "Стоимость: "+Materials(cost);
        public static string LifetimeNeed(long current,int required) => "Накопленная добыча: "+current+" / "+required+" ед. материалов";
        public static string HullUnlock(long current,int required) => "Нужен Корпус I. Добыча "+current+" / "+required+(current>=required?" — условие выполнено":" — накопите материалы");
        public static string NeedMaterials(MaterialCost cost,TeamProgress view)
        {
            var items=new List<string>();
            if(cost.biomass>view.biomass)items.Add("биомасса "+(cost.biomass-view.biomass));
            if(cost.chitin>view.chitin)items.Add("хитин "+(cost.chitin-view.chitin));
            if(cost.scrap>view.scrap)items.Add("лом "+(cost.scrap-view.scrap));
            if(cost.electronics>view.electronics)items.Add("электроника "+(cost.electronics-view.electronics));
            return items.Count==0?"":"Не хватает: "+string.Join(", ",items);
        }
        public static string UpgradeBonus(Upgrade upgrade,int level,UiContext ctx)
        {
            var t=ctx.Tuning;int next=level+1;
            switch(upgrade)
            {
                case Upgrade.Reel: return "Подтягивание и утомление добычи: ×"+(1+level*t.reelPowerPerLevel).ToString("F2")+" → ×"+(1+next*t.reelPowerPerLevel).ToString("F2");
                case Upgrade.Fins: return "Скорость плавания: ×"+(1+level*t.finsSpeedPerLevel).ToString("F2")+" → ×"+(1+next*t.finsSpeedPerLevel).ToString("F2");
                case Upgrade.Tank:
                    float oxygen=ctx.Player==null?0:ctx.Player.tuning.oxygenCapacity;
                    return "Запас кислорода: "+(oxygen*(1+level*t.oxygenPerTankLevel)).ToString("F0")+" → "+(oxygen*(1+next*t.oxygenPerTankLevel)).ToString("F0");
                case Upgrade.Hull:
                    float safe=ctx.Player==null?0:ctx.Player.tuning.safeDepth;
                    return "Открывает глубину 2. Допуск давления: "+(safe+level*ctx.World.tuning.deepDepth).ToString("F0")+" → "+(safe+next*ctx.World.tuning.deepDepth).ToString("F0")+" м";
                default:
                    float cycle=t.raisingSeconds+t.unloadingSeconds+t.returningSeconds;
                    return "Полный цикл: "+(cycle/(1+level*t.winchSpeedPerLevel)).ToString("F1")+" → "+(cycle/(1+next*t.winchSpeedPerLevel)).ToString("F1")+" с";
            }
        }
        public static string CurrentUpgradeBonus(Upgrade upgrade,int level,UiContext ctx)
        {
            var t=ctx.Tuning;
            switch(upgrade)
            {
                case Upgrade.Reel: return "Подтягивание и утомление добычи: ×"+(1+level*t.reelPowerPerLevel).ToString("F2");
                case Upgrade.Fins: return "Скорость плавания: ×"+(1+level*t.finsSpeedPerLevel).ToString("F2");
                case Upgrade.Tank: return "Запас кислорода: "+((ctx.Player==null?0:ctx.Player.tuning.oxygenCapacity)*(1+level*t.oxygenPerTankLevel)).ToString("F0");
                case Upgrade.Hull: return "Глубина 2 открыта. Допуск давления: "+((ctx.Player==null?0:ctx.Player.tuning.safeDepth)+level*ctx.World.tuning.deepDepth).ToString("F0")+" м";
                default: return "Полный цикл: "+((t.raisingSeconds+t.unloadingSeconds+t.returningSeconds)/(1+level*t.winchSpeedPerLevel)).ToString("F1")+" с";
            }
        }
        public static string OverfeedingRule(int carry) => "Перекорм — избыток переносится в следующий цикл, максимум "+carry+" пищи; других бонусов нет.";
        public static string FoodProjection(int offering,int required) => offering==0?"Чудовище ждёт первую порцию":offering<required?"Часть рациона получена":offering==required?"Полный рацион получен":"Получено сверх рациона";
        public static string FeedingAmount(int paid,int required) => "Получено пищи: "+paid+" / "+required;
        public static string HungerCountdown(float time) => "До конца срока кормления "+Time(time);
        public static string CargoCapacity(int used,int total) => used+" / "+total;
        public static string DeliveredStock(int count) => "Доставлено в резервуар: "+count+" ед. улова";
        public static string SessionStockLoss(int count) => "Потеряно улова за эту сессию: "+count;
        public static string CargoRemaining(float time) => "До завершения фазы: "+Time(time);
        public static string TravelRemaining(float time) => "Переход выполняется: "+Time(time);
    }
}
