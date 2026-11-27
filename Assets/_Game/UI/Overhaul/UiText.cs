using System;
namespace Raid.UI
{
    public static partial class UiText
    {
        public static string Phase(HoistPhase phase) => phase==HoistPhase.Loading?"Загрузка":phase==HoistPhase.Raising?"Подъём":phase==HoistPhase.Unloading?"Разгрузка":"Спуск";
        public static string Materials(int b,int c,int s,int e) => "Биомасса "+b+"   Хитин "+c+"   Лом "+s+"   Электроника "+e;
        public static string Materials(MaterialCost cost) => Materials(cost.biomass,cost.chitin,cost.scrap,cost.electronics);
        public static string CargoName(CatchItem item) => item.offering>0 || item.biomass>0 || item.chitin>0 ?
            string.IsNullOrEmpty(item.species)?"Рыба":ReelReadout.Species(item.species) : "Технический груз";
        public static string ItemName(CatchItem item) => CargoName(item)+" · горизонт "+item.depthTier;
        public static string PlayerName(DiverMotor p) => "Водолаз "+(p.OwnerId+1).ToString("00")+(p.IsOwner?" · вы":"");
        public static string PlayerState(DiverMotor p) => p.IsDead?"Погиб":p.IsTrapped?"Нужна помощь":p.IsHypoxic?"Гипоксия":p.IsHelpless?"Нет кислорода":p.IsInside?"На борту":"Под водой";
        public static string Time(float value) => Math.Max(0,(int)Math.Ceiling(value))/60+":"+(Math.Max(0,(int)Math.Ceiling(value))%60).ToString("00");
        public static string WorldMessage(string raw)
        {
            if(string.IsNullOrEmpty(raw))return "";
            if(raw.StartsWith("Depth "))return "Экспедиция: ловите рыбу и доставляйте улов";
            if(raw.StartsWith("Changing depth"))return "Подлодка меняет глубину";
            if(raw.StartsWith("Action unavailable"))return "Сервер отклонил действие: проверьте расстояние, груз и условия";
            if(raw.StartsWith("SAVE ERROR"))return "Ошибка сохранения. Исходный файл сохранён; прогресс может быть временным";
            if(raw.StartsWith("Feeding: "))
            {
                string outcome=raw.Contains("Underfed")?"НЕДОКОРМ":raw.Contains("Overfed")?"ПЕРЕКОРМ":raw.Contains("Enough")?"ДОСТАТОЧНО ПИЩИ":"НЕ КОРМИЛИ";
                int n=raw.IndexOf('(');return "Кормление: "+outcome+(n>=0?" "+raw.Substring(n):"");
            }
            return raw;
        }
        public const string QuoteChanged="Состав улова, уровень или цена изменились. Повторите действие";
        public static string CommandReason(string reason) => reason=="resource_full"?"Запас ресурса заполнен. Груз сохранён.":reason=="ok"?"Сервер подтвердил действие":reason=="onboard"?"Войдите в подлодку и подойдите к пульту":reason=="range"?"Подойдите ближе к точке взаимодействия":reason=="defense"?"Дождитесь окончания обороны":reason=="travel"?"Дождитесь завершения перехода":reason=="cooldown"?"Предыдущее действие ещё обрабатывается":reason=="helpless"?"Сначала дождитесь спасения":reason=="reservoir_full"?"Освободите место в резервуаре":reason=="route_busy"?RouteBusy:reason=="use_route"?UseFishRoute:reason=="changed"?QuoteChanged:"Действие отклонено: условия изменились на сервере";
    }
}
