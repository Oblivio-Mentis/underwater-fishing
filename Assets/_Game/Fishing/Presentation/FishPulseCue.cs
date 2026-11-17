using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Presentation
{
    public sealed class FishPulseCue : PresentationCue
    {
        public Color Color = UnityEngine.Color.white;
        public float Duration = .25f;
        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        { driver.GetComponent<FishFeedback>().Pulse(Color, Duration); }
    }
}
