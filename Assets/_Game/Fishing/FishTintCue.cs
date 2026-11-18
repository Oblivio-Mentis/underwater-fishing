using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace Raid
{
    public sealed class FishTintCue : PresentationCue
    {
        public Color color = Color.cyan;
        public override void Execute(FsmPresentationDriver driver, in UniversalFsmSignal signal)
        {
            var actor = driver.GetComponent<FishActor>();
            if (actor != null) actor.SetTint(color);
        }
    }
}
