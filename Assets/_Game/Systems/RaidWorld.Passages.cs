using FishNet.Object.Synchronizing;
using Raid.Nereid;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly SyncVar<PassageMotion> machineryDoor = new();
        readonly SyncVar<PassageMotion> pumpDoor = new();

        internal PassageMotion PassageState(PassageId id)
            => id == PassageId.Machinery ? machineryDoor.Value : pumpDoor.Value;

        internal void SetPassageState(PassageId id, PassageMotion motion)
        {
            if (!IsServerInitialized) return;
            if (id == PassageId.Machinery) machineryDoor.Value = motion;
            else pumpDoor.Value = motion;
        }
    }
}
