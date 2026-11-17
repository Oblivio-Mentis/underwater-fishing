using System;

namespace UnderwaterFishing.Cargo
{
    [Serializable]
    public struct CargoYield
    {
        public int Bio, Rare, Scrap, Electronics;
        public bool IsValid => Bio >= 0 && Rare >= 0 && Scrap >= 0 && Electronics >= 0;
    }

    [Serializable]
    public struct CargoItem
    {
        public ulong Id;
        public string Name;
        public int Size, OfferingValue;
        public bool IsFish, Reserved;
        public CargoYield Yield;
        public bool IsValid => Id != 0 && Size > 0 && OfferingValue >= 0 && Yield.IsValid
            && !string.IsNullOrWhiteSpace(Name) && (!Reserved || IsFish);
    }

    public enum CargoDisposition : byte { Process, Offering }
    public enum WinchState : byte { Deployed = 1, Ascending, Unloading, Descending }
}
