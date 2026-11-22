using UnityEngine;

namespace Raid
{
    // One server-side spool per confirmed impact. Slack is taken up by the fish swimming out.
    public sealed class FishingLine
    {
        public float PaidOut { get; private set; }
        public float Wound { get; private set; }
        public float WindingTime { get; private set; }

        public FishingLine(float distance) => PaidOut = Mathf.Max(0, distance);

        public bool Loaded(float distance, float slack) => distance + slack >= PaidOut;
        public void Slip(float distance) => PaidOut = Mathf.Max(PaidOut, distance);

        public void Wind(float distance, float slack, float speed, float dt)
        {
            if (!Loaded(distance, slack) || dt <= 0 || speed <= 0) return;
            float turn = Mathf.Min(PaidOut, speed * dt);
            PaidOut -= turn;
            Wound += turn;
            WindingTime += dt;
        }

        public bool AtHand(float distance, float reach, float minimumSeconds) =>
            Wound > 0 && WindingTime >= minimumSeconds && PaidOut <= reach && distance <= reach;
    }
}
