using System;
using System.Collections.Generic;
using OblivioMentis.UniversalFSM;

namespace UnderwaterFishing.Cargo
{
    internal sealed class WinchContext
    {
        public WinchState State;
        public float Elapsed;
    }

    internal abstract class WinchBehaviour : BaseUniversalFsmState<WinchContext>
    {
        public override void Enter(WinchContext context)
        { context.State = (WinchState)StateId; context.Elapsed = 0; }
        public override void Tick(WinchContext context, float dt) { context.Elapsed += dt; }
    }
    [UniversalFsmStateMetadata("Cargo")]
    internal sealed class WinchDeployed : WinchBehaviour { public override int StateId => (int)WinchState.Deployed; }
    [UniversalFsmStateMetadata("Cargo")]
    internal sealed class WinchAscending : WinchBehaviour { public override int StateId => (int)WinchState.Ascending; }
    [UniversalFsmStateMetadata("Cargo")]
    internal sealed class WinchUnloading : WinchBehaviour { public override int StateId => (int)WinchState.Unloading; }
    [UniversalFsmStateMetadata("Cargo")]
    internal sealed class WinchDescending : WinchBehaviour { public override int StateId => (int)WinchState.Descending; }

    /// <summary>Authority-independent UniversalFSM. Transitions are dispatched outside Tick.</summary>
    internal sealed class WinchMachine : IDisposable
    {
        private readonly CargoSettings settings;
        private readonly WinchContext context = new WinchContext();
        private readonly IUniversalFsmRuntimeHandle runtime;
        public event Action<WinchState, WinchState> Transitioned;
        public WinchState State => context.State;
        public float Progress => Math.Min(1, context.Elapsed / Duration);
        public float SpeedMultiplier { get; set; } = 1;
        private float Duration
        {
            get
            {
                float value = State == WinchState.Ascending ? settings.AscentSeconds
                    : State == WinchState.Unloading ? settings.UnloadSeconds : settings.DescentSeconds;
                return Math.Max(.01f, Finite(value) ? value : .01f) / Math.Max(.1f, Finite(SpeedMultiplier) ? SpeedMultiplier : 1);
            }
        }

        public WinchMachine(CargoSettings configuration)
        {
            settings = configuration ?? throw new ArgumentNullException(nameof(configuration));
            var states = new Dictionary<int, IUniversalFsmState<WinchContext>> {
                [(int)WinchState.Deployed] = new WinchDeployed(), [(int)WinchState.Ascending] = new WinchAscending(),
                [(int)WinchState.Unloading] = new WinchUnloading(), [(int)WinchState.Descending] = new WinchDescending()
            };
            var definitions = new UniversalStateDef[states.Count];
            int index = 0;
            foreach (var pair in states) definitions[index++] = new UniversalStateDef {
                id = pair.Key, name = ((WinchState)pair.Key).ToString(), layer_id = "Main",
                state_type_name = pair.Value.GetType().AssemblyQualifiedName
            };
            var definition = new UniversalFsmDefinition {
                version = 5, domain_id = "Cargo", context_type_name = typeof(WinchContext).AssemblyQualifiedName,
                start_state_id = (int)WinchState.Deployed, fallback_state_id = (int)WinchState.Deployed,
                layers = new[] { new UniversalLayerDef { id = "Main", name = "Main", start_state_id = (int)WinchState.Deployed, fallback_state_id = (int)WinchState.Deployed } },
                states = definitions
            };
            using (var composition = UniversalFsmRuntimeComposition<WinchContext>.CreateWithPrivateRegistry(context, states))
                runtime = composition.Build(definition);
            runtime.Initialize();
        }

        public bool StartLift()
        { return State == WinchState.Deployed && Transition(WinchState.Ascending); }

        public void Step(float dt)
        {
            if (!Finite(dt) || dt <= 0) return;
            // At most one externally initiated cycle can be in flight, even for a large server step.
            for (int transitions = 0; transitions < 4 && State != WinchState.Deployed; transitions++)
            {
                float remaining = Math.Max(0, Duration - context.Elapsed);
                float step = Math.Min(dt, remaining);
                runtime.Tick(step);
                dt -= step;
                if (step < remaining) break;
                WinchState next = State == WinchState.Ascending ? WinchState.Unloading
                    : State == WinchState.Unloading ? WinchState.Descending : WinchState.Deployed;
                if (!Transition(next)) throw new InvalidOperationException("Cargo FSM rejected " + next);
                if (dt <= 0) break;
            }
        }
        public void Reset() { if (State != WinchState.Deployed) Transition(WinchState.Deployed); }
        private bool Transition(WinchState next)
        {
            WinchState previous = State;
            if (!runtime.TryForceTransition("Main", (int)next)) return false;
            Transitioned?.Invoke(previous, next);
            return true;
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        public void Dispose() => runtime.Dispose();
    }
}
