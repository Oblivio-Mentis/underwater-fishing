using System.Collections.Generic;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace Raid
{
    public enum FishMode { Roam = 1, Hooked, Struggling, Tired, Captured, Transported, Escaped }

    public sealed class FishContext
    {
        public FishActor Actor;
    }

    public abstract class FishFsmState : BaseUniversalFsmState<FishContext>
    {
        public override void Enter(FishContext context) => context.Actor.EnterState((FishMode)StateId);
        public override void FixedTick(FishContext context, float dt) => context.Actor.SimulateState((FishMode)StateId, dt);
    }

    [UniversalFsmStateMetadata("RaidFishing", "Roam", "Fish")]
    public sealed class FishRoamState : FishFsmState { public override int StateId => (int)FishMode.Roam; }
    [UniversalFsmStateMetadata("RaidFishing", "Hooked", "Fish")]
    public sealed class FishHookedState : FishFsmState { public override int StateId => (int)FishMode.Hooked; }
    [UniversalFsmStateMetadata("RaidFishing", "Struggling", "Fish")]
    public sealed class FishStrugglingState : FishFsmState { public override int StateId => (int)FishMode.Struggling; }
    [UniversalFsmStateMetadata("RaidFishing", "Tired", "Fish")]
    public sealed class FishTiredState : FishFsmState { public override int StateId => (int)FishMode.Tired; }
    [UniversalFsmStateMetadata("RaidFishing", "Captured", "Fish")]
    public sealed class FishCapturedState : FishFsmState { public override int StateId => (int)FishMode.Captured; }
    [UniversalFsmStateMetadata("RaidFishing", "Transported", "Fish")]
    public sealed class FishTransportedState : FishFsmState { public override int StateId => (int)FishMode.Transported; }
    [UniversalFsmStateMetadata("RaidFishing", "Escaped", "Fish")]
    public sealed class FishEscapedState : FishFsmState { public override int StateId => (int)FishMode.Escaped; }

    [CreateAssetMenu(menuName = "RAID/Fish FSM profile")]
    public sealed class FishFsmProfile : UniversalFsmDomainProfile<FishContext>
    {
        public override string DomainId => "RaidFishing";

        protected override UniversalFsmRuntimeComposition<FishContext> CreateComposition(GameObject owner, UniversalFsmDefinition definition)
        {
            var actor = owner.GetComponent<FishActor>();
            if (actor == null) throw new System.InvalidOperationException("Fish FSM requires FishActor.");
            var board = new UniversalFsmBlackboard();
            var states = new Dictionary<int, IUniversalFsmState<FishContext>>
            {
                [1] = new FishRoamState(), [2] = new FishHookedState(), [3] = new FishStrugglingState(),
                [4] = new FishTiredState(), [5] = new FishCapturedState(), [6] = new FishTransportedState(), [7] = new FishEscapedState()
            };
            return UniversalFsmRuntimeComposition<FishContext>.CreateWithPrivateRegistry(
                new FishContext { Actor = actor }, states, blackboard: board,
                beforeTick: (context, dt) => board.SetInt(UniversalFsmBlackboard.StableHash("DesiredState"), (int)actor.DesiredState));
        }
    }
}
