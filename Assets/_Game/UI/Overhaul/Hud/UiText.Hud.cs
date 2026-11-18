using System.Globalization;
namespace Raid.UI
{
    public static partial class UiText
    {
        public const string HudCrew="ЭКИПАЖ";
        public const string HudDiverSystems="СИСТЕМЫ ВОДОЛАЗА";
        public const string HudInside="Герметичный отсек · восполнение кислорода";
        public const string HudOutside="Под водой";
        public const string HudBase="БАЗА / NEREID";
        public const string HudHunger="КОРМЛЕНИЕ ЧУДОВИЩА";
        public const string HudTension="НАТЯЖЕНИЕ ТРОСА";
        public const string HudStamina="ВЫНОСЛИВОСТЬ ДОБЫЧИ";
        public const string HudHarpoonTip="Удерживать ЛКМ — подтягивать\nОтпустить при рывке · R — отпустить рыбу";
        public const string HudCaptured="УЛОВ ЗАХВАЧЕН";
        public const string HudCarryTip="Доставьте рыбу в грузовой контейнер";
        public const string HudCargo="КОНТЕЙНЕР / ЛЕБЁДКА";
        public const string HudCargoReady="Готов к загрузке";
        public const string HudCargoFull="КОНТЕЙНЕР ПОЛОН";
        public const string HudTerminalPrompt="E — терминал резервуара";
        public const string HudTerminalBaseHint="Улов, улучшения и выбор глубины";
        public const string HudTerminalDiveHint="Улов, кормление и возвращение на базу";
        public const string HudCargoPrompt="E — управление контейнером";
        public const string HudLoadPrompt="E — загрузить улов";
        public const string HudCargoBaseHint="Загрузка улова доступна во время экспедиции";
        public const string HudHoistReady="Улов загружен — можно начать подъём";
        public const string HudCargoEmpty="Доставьте улов, чтобы начать подъём";
        public const string HudLootPrompt="E — подобрать технический груз";
        public const string HudLootOccupied="Вы уже переносите технический груз";
        public const string HudAssistPrompt="Удерживать E — помочь напарнику с тросом";
        public const string HudFishPrompt="ЛКМ — выстрелить гарпуном";
        public const string HudReelPrompt="Удерживать ЛКМ — подтягивать рыбу";
        public const string HudFishOccupied="Рыба уже зацеплена";
        public const string HudWaitForRescue="Ожидайте помощи напарника";
        public const string HudOxygenCritical="КИСЛОРОД ИСЧЕРПАН";
        public const string HudOxygenLow="МАЛО КИСЛОРОДА";
        public const string HudOxygenLowHint="Вернитесь в герметичный отсек подлодки";
        public const string HudRescueHint="Напарник может спасти вас, удерживая F рядом";
        public const string HudTrapped="ВЫ ЗАСТРЯЛИ / НУЖНА ПОМОЩЬ";
        public const string HudPressureDanger="ОПАСНОЕ ДАВЛЕНИЕ";
        public const string HudPressureDangerHint="Вернитесь к безопасной глубине. Скорость снижена";
        public const string HudPressureWarning="ДАВЛЕНИЕ РАСТЁТ";
        public const string HudPressureWarningHint="Двигайтесь к безопасной глубине";
        public const string HudWave="НАПАДЕНИЕ НА РЕЗЕРВУАР";
        public const string HudWaveHint="Защитите необработанный улов гарпунами";
        public const string HudWaveEnded="Нападение завершено. Технологии и улучшения сохранены";
        public static string HudCrewCount(int value) => HudCrew+"  "+value+" / 5";
        public static string HudOxygen(float current,float capacity) => "Кислород  "+current.ToString("0")+" / "+capacity.ToString("0");
        public static string HudStress(float stress,bool inside) => "Нагрузка  "+stress.ToString("0")+"%"+(inside && stress>1?" · снижается":"");
        public static string HudDepth(float depth,int tier,bool atBase) => atBase?HudBase:"ГЛУБИНА "+tier+"   /   "+depth.ToString("0.0",CultureInfo.GetCultureInfo("ru-RU"))+" м";
        public static string HudDepthState(bool inside) => inside?"НА БОРТУ":"ПОД ВОДОЙ";
        public static string HudFood(int amount,int required) => "Пища  "+amount+" / "+required;
        public static string HudHungerTime(float seconds) => "До голода  "+Time(seconds);
        public static string HudHarpoonState(FishMode mode) => mode==FishMode.Hooked?"РЫБА ЗАЦЕПЛЕНА":mode==FishMode.Tired?"РЫБА ИСТОЩЕНА":"РЫБА СОПРОТИВЛЯЕТСЯ";
        public static string HudTensionValue(float value) => (value*100).ToString("0")+"%"+(value>=.85f?" · РИСК ОБРЫВА":value>=.6f?" · ОСЛАБЬТЕ":"");
        public static string HudStaminaValue(float current,float maximum) => current.ToString("0.0",CultureInfo.GetCultureInfo("ru-RU"))+" / "+maximum.ToString("0.0",CultureInfo.GetCultureInfo("ru-RU"));
        public static string HudCargoCount(int used,int capacity) => "Занято  "+used+" / "+capacity;
        public static string HudHoistTime(HoistPhase phase,float remaining) => Phase(phase)+" · осталось "+Time(remaining);
        public static string HudCargoBlocked(HoistPhase phase) => "Загрузка закрыта: "+Phase(phase).ToLowerInvariant();
        public static string HudCargoSpace(int size,int free) => "Нужно места: "+size+" · свободно: "+free;
        public static string HudRescuePrompt(DiverMotor target) => "Удерживать F — спасти "+PlayerName(target);
        public static string HudRescueProgress(int owner,float fraction) => "Спасение водолаза "+(owner+1).ToString("00")+"  "+(fraction*100).ToString("0")+"%";
        public static string HudRescueDuration(float seconds) => "Непрерывное удержание: "+seconds.ToString("0.0",CultureInfo.GetCultureInfo("ru-RU"))+" с";
        public static string HudStockLost(int count) => "Нападение: потеряно единиц улова — "+count+". Улучшения сохранены";
    }
}
