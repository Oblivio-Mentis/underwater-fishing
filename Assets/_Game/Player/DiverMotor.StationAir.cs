using UnityEngine;
namespace Raid
{
    public sealed partial class DiverMotor
    {
        internal float TakeStationAir(float offered)
        {
            if (!IsServerInitialized || IsDead || IsInside || offered <= 0) return 0;
            var state = _state.Value;
            float taken = Mathf.Clamp(AirCapacity - state.Oxygen, 0, offered);
            if (taken <= 0) return 0;
            state.Oxygen += taken;
            _state.Value = state;
            return taken;
        }
    }
}
