using System;
namespace Raid
{
    [Serializable]
    public struct FishRouteState
    {
        public int sequence,total,completed;
        public bool feeding,active,failed,stopping;
        public float elapsed;
        public float Duration => 8f+Math.Max(0,total-1)*.18f;
        public float Remaining => Math.Max(0,Duration-elapsed);
    }
}
