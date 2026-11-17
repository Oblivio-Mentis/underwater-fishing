using UnityEngine;

namespace UnderwaterFishing.Fishing.Creatures
{
    internal sealed class FishContext
    {
        public readonly FishSpecies Species;
        public readonly Vector3 Origin;
        private readonly System.Random random;
        public FishState? PendingState;
        public void Transition(FishState state) => PendingState = state;
        public FishState State;
        public Vector3 Position, Diver, Velocity, Target, StruggleDirection;
        public bool DiverNearby, Reeling;
        public float Stamina, StateSeconds, RetargetSeconds;
        public float Tension;
        public float MaximumStamina => Mathf.Max(0.01f, Species.Stamina);
        public bool ThreatWithin(float radius) => DiverNearby && (Position - Diver).sqrMagnitude <= radius * radius;

        public FishContext(FishSpecies species, Vector3 origin, int seed)
        {
            Species = species;
            Origin = Position = origin;
            random = new System.Random(seed);
            Stamina = MaximumStamina;
        }

        public float RandomSigned() => (float)(random.NextDouble() * 2.0 - 1.0);
        public Vector3 RandomDirection()
        {
            Vector3 direction = new Vector3(RandomSigned(), RandomSigned() * 0.4f, RandomSigned());
            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }
        public void ChooseRoamTarget()
        {
            Target = Origin + RandomDirection() * (Species.RoamRadius * (0.35f + (float)random.NextDouble() * 0.65f));
            RetargetSeconds = Mathf.Max(0.01f, Species.RoamRetargetSeconds);
        }
        public Vector3 AwayFromDiver()
        {
            Vector3 away = Position - Diver;
            return away.sqrMagnitude > 0.001f ? away.normalized : RandomDirection();
        }
        public void Recover(float dt, float multiplier = 1f)
        {
            Stamina = Mathf.Min(MaximumStamina, Stamina + Mathf.Max(0f, Species.StruggleRecoveryPerSecond) * multiplier * dt);
        }
    }
}
