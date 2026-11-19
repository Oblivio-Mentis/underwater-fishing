using UnityEngine;

namespace Raid
{
    public enum AirlockPhase : byte { InnerOpen, ClosingInner, Flooding, OpeningOuter, OuterOpen, ClosingOuter, Draining, OpeningInner }

    public struct AirlockSnapshot
    {
        public AirlockPhase Phase;
        public float Inner, Outer, Water;
        public bool Busy => Phase!=AirlockPhase.InnerOpen && Phase!=AirlockPhase.OuterOpen;
        public bool CanRequest(bool sea) => sea?Phase==AirlockPhase.InnerOpen:Phase==AirlockPhase.OuterOpen;
        public string Caption => Phase switch
        {
            AirlockPhase.InnerOpen => "Сухо · проход на борт",
            AirlockPhase.OuterOpen => "Затоплен · проход в море",
            AirlockPhase.Flooding => "Заполнение камеры",
            AirlockPhase.Draining => "Откачка воды",
            AirlockPhase.ClosingInner or AirlockPhase.ClosingOuter => "Гермодверь закрывается",
            _ => "Гермодверь открывается"
        };
        public static AirlockSnapshot Dry => new AirlockSnapshot{Inner=1,Phase=AirlockPhase.InnerOpen};

        public bool Request(bool sea)
        {
            if(!CanRequest(sea))return false;
            Phase=sea?AirlockPhase.ClosingInner:AirlockPhase.ClosingOuter;
            return true;
        }

        public void Step(float dt,bool innerOccupied,bool outerOccupied)
        {
            float door=dt/1.25f;
            switch(Phase)
            {
                case AirlockPhase.ClosingInner:
                    Inner=Mathf.MoveTowards(Inner,innerOccupied?1:0,door);
                    if(Inner==0)Phase=AirlockPhase.Flooding;
                    break;
                case AirlockPhase.Flooding:
                    Water=Mathf.MoveTowards(Water,1,dt/5f);
                    if(Water==1)Phase=AirlockPhase.OpeningOuter;
                    break;
                case AirlockPhase.OpeningOuter:
                    Outer=Mathf.MoveTowards(Outer,1,door);
                    if(Outer==1)Phase=AirlockPhase.OuterOpen;
                    break;
                case AirlockPhase.ClosingOuter:
                    Outer=Mathf.MoveTowards(Outer,outerOccupied?1:0,door);
                    if(Outer==0)Phase=AirlockPhase.Draining;
                    break;
                case AirlockPhase.Draining:
                    Water=Mathf.MoveTowards(Water,0,dt/5.4f);
                    if(Water==0)Phase=AirlockPhase.OpeningInner;
                    break;
                case AirlockPhase.OpeningInner:
                    Inner=Mathf.MoveTowards(Inner,1,door);
                    if(Inner==1)Phase=AirlockPhase.InnerOpen;
                    break;
            }
        }
    }
}
