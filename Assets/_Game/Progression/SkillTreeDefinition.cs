using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public enum SkillId { None, H1, H2, H3, H4, S1, S2, S3, S4, F1, F2, F3, F4, R1, R2, R3, R4, C1, C2, C3, C4, G1, G2, G3, G4, K1, K2, K3, K4, P1, P2, P3, P4, N1, N2, N3, N4 }

    [Serializable]
    public sealed class SkillNode
    {
        public SkillId Id;
        public string Name;
        public string Effect;
        [Min(0)] public int Magnitude;
        public MaterialCost Cost;
        public bool Team => Id >= SkillId.K1;
        public SkillId Prerequisite => (Id==SkillId.None || ((int)Id-1)%4==0)?SkillId.None:Id-((int)Id-1)%4;
        public SkillNode(SkillId id, string name, string effect, int magnitude, MaterialCost cost)
        {
            Id=id; Name=name; Effect=effect; Magnitude=magnitude; Cost=cost;
        }
    }

    public static class SkillTreeDefinition
    {
        static readonly SkillNode[] Defaults =
        {
            new(SkillId.H1, "Сервокатушка", "Смотка троса быстрее", 12, new(8, 0, 0, 0)),
            new(SkillId.H2, "Плетёный трос", "Выше предел натяжения", 20, new(12, 3, 0, 0)),
            new(SkillId.H3, "Гаситель рывков", "Меньше смещение при рывке рыбы", 18, new(12, 3, 0, 0)),
            new(SkillId.H4, "Общий такт", "Сильнее согласованная тяга двух водолазов", 15, new(20, 7, 5, 0)),
            new(SkillId.S1, "Увеличенные баллоны", "Больше воздуха", 20, new(8, 0, 0, 0)),
            new(SkillId.S2, "Компенсатор давления", "Глубже безопасный предел, м", 10, new(12, 4, 0, 0)),
            new(SkillId.S3, "Защитные пластины", "Меньше урон хищников", 12, new(12, 5, 0, 0)),
            new(SkillId.S4, "Аварийный резерв", "Запас воздуха после истощения, с", 12, new(18, 7, 4, 2)),
            new(SkillId.F1, "Усиленные ласты", "Быстрее плавание", 10, new(8, 0, 0, 0)),
            new(SkillId.F2, "Уверенный старт", "Быстрее набор скорости под водой", 15, new(12, 0, 3, 0)),
            new(SkillId.F3, "Работа с грузом", "Меньше штраф от тяжёлой рыбы в воде", 20, new(12, 4, 0, 0)),
            new(SkillId.F4, "Гидрорывок", "Ускорение на 1 с · B · откат 12 с", 30, new(19, 5, 5, 2)),
            new(SkillId.R1, "Ручной сканер", "Краткий поиск рыбы, V · радиус, м", 12, new(9, 0, 2, 0)),
            new(SkillId.R2, "Биосигнатуры", "Сведения о ценности обнаруженной рыбы", 1, new(11, 3, 0, 0)),
            new(SkillId.R3, "Метки утиля", "Метки близкого утиля · радиус, м", 12, new(11, 0, 4, 0)),
            new(SkillId.R4, "Командный маяк", "V отмечает находку для экипажа · откат, с", 20, new(18, 4, 5, 2)),
            new(SkillId.C1, "Дополнительный подсумок", "Дополнительный карман", 1, new(10, 0, 2, 0)),
            new(SkillId.C2, "Крепления трофея", "Меньше штраф от тяжёлой рыбы", 15, new(12, 4, 0, 0)),
            new(SkillId.C3, "Фиксаторы улова", "Защита одного улова от кражи за вылазку", 1, new(12, 0, 4, 0)),
            new(SkillId.C4, "Раскладной подсумок", "Ещё один карман", 1, new(20, 7, 5, 2)),
            new(SkillId.G1, "Знакомый сигнал", "Гоша выполняет команду «Ко мне»", 1, new(7, 2, 0, 0)),
            new(SkillId.G2, "Принеси рыбку", "Гоша приносит одну небольшую рыбу", 1, new(12, 4, 0, 0)),
            new(SkillId.G3, "Осторожный пловец", "Гоша избегает опасных участков", 1, new(12, 4, 0, 0)),
            new(SkillId.G4, "Лучший рыбак", "Меньше откат, больше радиус поиска", 25, new(18, 7, 4, 2)),
            new(SkillId.K1, "Дополнительный отсек", "Дополнительная вместимость контейнера", 4, new(12, 0, 6, 0)),
            new(SkillId.K2, "Плотная укладка", "Дополнительная вместимость контейнера", 4, new(16, 4, 10, 0)),
            new(SkillId.K3, "Усиленная клеть", "Дополнительная вместимость контейнера", 4, new(16, 6, 8, 2)),
            new(SkillId.K4, "Расширенный трюм", "Дополнительная вместимость контейнера", 6, new(28, 10, 14, 5)),
            new(SkillId.P1, "Калибровка сепаратора", "Больше всех исходных материалов", 10, new(10, 0, 6, 1)),
            new(SkillId.P2, "Биофильтр", "Больше биомассы и хитина", 15, new(14, 5, 6, 2)),
            new(SkillId.P3, "Магнитный сепаратор", "Больше лома и электроники", 15, new(14, 0, 9, 4)),
            new(SkillId.P4, "Промышленная линия", "Дополнительный выход всех материалов", 10, new(24, 8, 12, 8)),
            new(SkillId.N1, "Подготовка корма", "Больше насыщение от каждой рыбы", 10, new(12, 5, 3, 0)),
            new(SkillId.N2, "Питательная смесь", "Больше насыщение от порций до 2 включительно", 20, new(16, 7, 6, 1)),
            new(SkillId.N3, "Уплотнение порций", "Больше насыщение от порций от 3", 20, new(16, 7, 6, 1)),
            new(SkillId.N4, "Улучшенная подача", "Больше насыщение от каждой рыбы", 15, new(28, 12, 10, 4)),
        };

        public static IReadOnlyList<SkillNode> All => Defaults;
        public static readonly float[] Angles = { 0, 40, 80, 120, 160, 200, 240, 280, 320 };
        public static SkillNode Get(SkillId id) => id > SkillId.None && (int)id <= Defaults.Length ? Defaults[(int)id-1] : null;
        public static IReadOnlyList<SkillId> Dependencies(SkillId id)
        {
            if(Get(id)==null)return Array.Empty<SkillId>();
            int step=((int)id-1)%4;
            if(step==0)return Array.Empty<SkillId>();
            if(step==3)return new[]{id-2,id-1};
            return new[]{id-step};
        }

        public static bool IsAvailable(SkillId id, IReadOnlyCollection<SkillId> owned)
        {
            if(Get(id)==null || owned==null)return false;
            var dependencies=Dependencies(id);
            foreach(var requirement in dependencies)
                if(System.Linq.Enumerable.Contains(owned,requirement))return true;
            return dependencies.Count==0;
        }
    }
}
