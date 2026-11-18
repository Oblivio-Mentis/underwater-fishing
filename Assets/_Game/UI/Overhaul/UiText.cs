using System;
namespace Raid.UI
{
    public static partial class UiText
    {
        public static string Phase(HoistPhase phase) => phase==HoistPhase.Loading?"Загрузка":phase==HoistPhase.Raising?"Подъём":phase==HoistPhase.Unloading?"Разгрузка":"Спуск";
        public static string UpgradeName(Upgrade value) => value==Upgrade.Reel?"Катушка":value==Upgrade.Fins?"Ласты":value==Upgrade.Tank?"Баллоны":value==Upgrade.Hull?"Корпус":"Лебёдка";
        public static string Materials(int b,int c,int s,int e) => "Биомасса "+b+"   Хитин "+c+"   Лом "+s+"   Электроника "+e;
        public static string Materials(MaterialCost cost) => Materials(cost.biomass,cost.chitin,cost.scrap,cost.electronics);
        public static string ItemName(CatchItem item) => (item.offering>0 || item.biomass>0 || item.chitin>0?"Рыба":"Технический груз")+" · глубина "+item.depthTier;
        public static string PlayerName(DiverMotor p) => "Водолаз "+(p.OwnerId+1).ToString("00")+(p.IsOwner?" · вы":"");
        public static string PlayerState(DiverMotor p) => p.IsHelpless?"НЕТ КИСЛОРОДА":p.IsTrapped?"НУЖНА ПОМОЩЬ":p.IsInside?"На борту":"Под водой";
        public static string Time(float value) => Math.Max(0,(int)Math.Ceiling(value))/60+":"+(Math.Max(0,(int)Math.Ceiling(value))%60).ToString("00");
        public static string WorldMessage(string raw)
        {
            if(string.IsNullOrEmpty(raw))return "";
            if(raw.StartsWith("Choose depth"))return "Выберите глубину у терминала резервуара";
            if(raw.StartsWith("Base:"))return "База: переработка улова и улучшения доступны";
            if(raw.StartsWith("Depth "))return "Экспедиция: ловите рыбу и доставляйте улов";
            if(raw.StartsWith("Submarine descending"))return "Погружение: переход выполняется";
            if(raw.StartsWith("Returning to base"))return "Возвращение на базу: переход выполняется";
            if(raw.StartsWith("Action unavailable"))return "Сервер отклонил действие: проверьте расстояние, груз и условия";
            if(raw.StartsWith("SAVE ERROR"))return "Ошибка сохранения. Исходный файл сохранён; прогресс может быть временным";
            if(raw.StartsWith("Feeding: "))
            {
                string outcome=raw.Contains("Underfed")?"НЕДОКОРМ":raw.Contains("Overfed")?"ПЕРЕКОРМ":raw.Contains("Enough")?"ДОСТАТОЧНО ПИЩИ":"НЕ КОРМИЛИ";
                int n=raw.IndexOf('(');return "Кормление: "+outcome+(n>=0?" "+raw.Substring(n):"");
            }
            return raw;
        }
        public const string QuoteChanged="Уровень или цена изменились. Проверьте улучшение и подтвердите заново";
        public static string CommandReason(string reason) => reason=="ok"?"Сервер подтвердил действие":reason=="range"?"Подойдите ближе к точке взаимодействия":reason=="travel"?"Дождитесь завершения перехода":reason=="cooldown"?"Предыдущее действие ещё обрабатывается":reason=="helpless"?"Сначала дождитесь спасения":reason=="route_busy"?RouteBusy:reason=="changed"?QuoteChanged:"Действие отклонено: условия изменились на сервере";
    }
}
