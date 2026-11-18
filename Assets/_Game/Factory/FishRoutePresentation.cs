using UnityEngine;
namespace Raid.Factory
{
    // Reads the replicated route state on every observer, including late joiners.
    public sealed class FishRoutePresentation : MonoBehaviour
    {
        FactoryVisuals visuals;
        int observedSequence=-1;
        bool connected;
        float nextRead;
        void Awake() { visuals=GetComponent<FactoryVisuals>(); }
        void Update()
        {
            if(visuals==null || Time.unscaledTime<nextRead)return;nextRead=Time.unscaledTime+.1f;
            var world=RaidWorld.Instance;
            bool live=world!=null&&world.NetworkObject!=null&&world.IsClientInitialized;
            if(!live)
            {
                if(connected)visuals.StopBatch();connected=false;observedSequence=-1;return;
            }
            connected=true;var state=world.FishRoute;
            if(!state.active){if(state.sequence==0)observedSequence=-1;if(visuals.IsPlaying)visuals.StopBatch();return;}
            if(observedSequence==state.sequence)return;observedSequence=state.sequence;
            visuals.PlayBatch(state.feeding,state.total,state.sequence,state.elapsed);
            UnderwaterLook.Instance?.Valve();
        }
    }
}
