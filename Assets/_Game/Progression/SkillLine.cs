namespace Raid
{
    // Wire IDs reserve 16 slots for each line, including the unique module in each sector.
    public enum SkillLine
    {
        Reel = 0, Cable, Damper, Tip, SyncPull, HarpoonModule,
        Oxygen = 6, Seal, Pressure, Armor, EmergencyAir, ExpeditionSuit,
        Fins = 12, SwimStart, SwimmingTurn, CargoCarry, TugStability, HydroDash,
        Hull = 18, Searchlights, Airlock, Scanner, Medical, DeepVessel,
        Cage = 24, Winch, Reservoir, CargoDefense, Pockets, Industrial,
        Separator = 30, Biomass, Chitin, Scrap, Electronics, WasteFree,
        Nutrition = 36, SmallRation, LargeRation, Satiety, Carryover, PreparedRation,
        GoshaSpeed = 42, Recall, Fetch, SafeSwim, Delivery, Faithful
    }
}
