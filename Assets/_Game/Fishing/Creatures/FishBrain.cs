using System;
using System.Collections.Generic;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Creatures
{
    /// <summary>Own on the simulation authority. Feed actual positions after collision resolution.</summary>
    public sealed class FishBrain : IDisposable
    {
        private readonly FishContext context;
        private readonly IUniversalFsmRuntimeHandle runtime;
        private bool disposed;
        public FishState CurrentState => context.State;
        public float Stamina => context.Stamina;
        public bool IsExhausted => CurrentState == FishState.Exhausted;
        public IUniversalFsmStateChangeSource StateSource => (IUniversalFsmStateChangeSource)runtime;

        public FishBrain(FishSpecies species, Vector3 origin, int seed)
        {
            if (species == null) throw new ArgumentNullException(nameof(species));
            context = new FishContext(species, origin, seed);
            var states = new Dictionary<int, IUniversalFsmState<FishContext>>
            {
                [(int)FishState.Idle] = new FishIdle(),
                [(int)FishState.Roam] = new FishRoam(),
                [(int)FishState.Alerted] = new FishAlerted(),
                [(int)FishState.Flee] = new FishFlee(),
                [(int)FishState.Hooked] = new FishHooked(),
                [(int)FishState.Struggling] = new FishStruggling(),
                [(int)FishState.Exhausted] = new FishExhausted(),
                [(int)FishState.Captured] = new FishCaptured(),
                [(int)FishState.Escaped] = new FishEscaped()
            };
            var definitions = new UniversalStateDef[states.Count];
            int index = 0;
            foreach (var pair in states)
                definitions[index++] = new UniversalStateDef
                {
                    id = pair.Key, name = ((FishState)pair.Key).ToString(), layer_id = "Main",
                    state_type_name = pair.Value.GetType().AssemblyQualifiedName
                };
            var definition = new UniversalFsmDefinition
            {
                version = 5, domain_id = "Fishing", context_type_name = typeof(FishContext).AssemblyQualifiedName,
                start_state_id = (int)FishState.Idle, fallback_state_id = (int)FishState.Idle,
                layers = new[] { new UniversalLayerDef { id = "Main", name = "Main", start_state_id = (int)FishState.Idle, fallback_state_id = (int)FishState.Idle } },
                states = definitions
            };
            using (var composition = UniversalFsmRuntimeComposition<FishContext>.CreateWithPrivateRegistry(context, states))
                runtime = composition.Build(definition);
            runtime.Initialize();
        }

        public Vector3 Step(float dt, Vector3 position, Vector3 nearestDiver, bool diverNearby, bool reeling, float normalizedTension)
        {
            ThrowIfDisposed();
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
                throw new ArgumentOutOfRangeException(nameof(dt));
            if (!Finite(position) || !Finite(nearestDiver))
                throw new ArgumentException("Fish simulation positions must be finite.");
            if (dt == 0f) return context.Velocity;
            context.Position = position;
            context.Diver = nearestDiver;
            context.DiverNearby = diverNearby;
            context.Reeling = reeling;
            context.Tension = float.IsNaN(normalizedTension) || float.IsInfinity(normalizedTension) ? 0f : Mathf.Clamp01(normalizedTension);
            context.PendingState = null;
            runtime.Tick(dt);
            // UniversalFSM rejects force transitions while Tick is dispatching a state.
            if (context.PendingState.HasValue)
            {
                FishState next = context.PendingState.Value;
                context.PendingState = null;
                if (!runtime.TryForceTransition("Main", (int)next))
                    throw new InvalidOperationException("Fish FSM rejected its requested transition to " + next);
            }
            return context.Velocity;
        }

        public bool TryHook()
        {
            ThrowIfDisposed();
            switch (CurrentState)
            {
                case FishState.Idle:
                case FishState.Roam:
                case FishState.Alerted:
                case FishState.Flee:
                    return runtime.TryForceTransition("Main", (int)FishState.Hooked);
                default:
                    return false;
            }
        }

        public void Release()
        {
            ThrowIfDisposed();
            if (CurrentState == FishState.Hooked || CurrentState == FishState.Struggling || CurrentState == FishState.Exhausted)
                runtime.TryForceTransition("Main", (int)FishState.Escaped);
        }

        public bool Capture()
        {
            ThrowIfDisposed();
            return IsExhausted && runtime.TryForceTransition("Main", (int)FishState.Captured);
        }

        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x)
            && !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(FishBrain)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            runtime.Dispose();
        }
    }
}
