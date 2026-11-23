using UnityEngine;

namespace Raid
{
    // Owned-view feedback for server-confirmed fishing events. Reads replicated sequence
    // counters only; never fires, moves, damages or decides a catch.
    [DisallowMultipleComponent, DefaultExecutionOrder(930)]
    public sealed class FishingFeel : MonoBehaviour
    {
        DiverMotor owner;
        HarpoonTool tool;
        Nereid.NereidFeedingCameraImpulse camera;
        ThreatDirector threats;
        FishActor fish;
        int hookSequence, tugSequence, biteSequence;
        float nextLookup;

        public void Bind(DiverMotor diver)
        {
            owner = diver;
            tool = diver == null ? null : diver.GetComponent<HarpoonTool>();
            camera = diver == null ? null : diver.GetComponentInChildren<Nereid.NereidFeedingCameraImpulse>();
            if (camera == null && diver != null)
            {
                var view = diver.GetComponentInChildren<Camera>();
                if (view != null) camera = view.GetComponent<Nereid.NereidFeedingCameraImpulse>()
                    ?? view.gameObject.AddComponent<Nereid.NereidFeedingCameraImpulse>();
            }
            ResetCounters();
        }

        void ResetCounters()
        {
            fish = tool == null ? null : tool.HookedFish;
            hookSequence = fish == null ? 0 : fish.HookSequence;
            tugSequence = fish == null ? 0 : fish.TugSequence;
            biteSequence = threats == null ? 0 : threats.BiteSequence;
        }

        static float Setting(System.Func<UI.UiSettings, float> pick)
        {
            var ui = UI.UiRoot.Instance;
            return ui == null ? 1f : Mathf.Clamp01(pick(ui.Settings));
        }

        void LateUpdate()
        {
            var ui = UI.UiRoot.Instance;
            if (ui == null || owner == null || !owner.IsClientInitialized || !owner.IsOwner) return;
            if (Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + .4f;
                if (threats == null) threats = ThreatDirector.Instance;
            }
            var observed = owner.IsDead ? owner.Spectating : owner;
            if (observed != owner) return;
            var current = tool == null ? null : tool.HookedFish;
            if (current != fish)
            {
                fish = current;
                hookSequence = fish == null ? 0 : fish.HookSequence;
                tugSequence = fish == null ? 0 : fish.TugSequence;
            }
            bool controlling = ui.Page == UI.UiPage.None && !UI.UiInputGate.Modal && !owner.IsHelpless && !owner.IsDead;

            if (fish != null && fish.HookSequence != hookSequence)
            {
                hookSequence = fish.HookSequence;
                if (controlling)
                {
                    float hit = Setting(s => s.hitImpulse);
                    ui.PushFishingPunch(6.5f * hit);
                    if (camera != null) camera.Hit(.010f * Mathf.Clamp(fish.power,.5f,3f), 1.1f, 0f);
                }
            }
            if (fish != null && fish.TugSequence != tugSequence)
            {
                tugSequence = fish.TugSequence;
                if (controlling && fish.Surging)
                {
                    ui.PushFishingPunch(2.2f * Setting(s => s.fishingCamera));
                    if (camera != null) camera.Hit(.004f + .006f * Mathf.Clamp01(fish.TugStrength), .9f, RandomSign());
                }
            }
            if (threats != null && threats.BiteSequence != biteSequence)
            {
                biteSequence = threats.BiteSequence;
                bool mine = threats.BiteVictim == owner.OwnerId;
                if (mine && controlling)
                {
                    float hit = Setting(s => s.hitImpulse);
                    ui.PushFishingPunch(3.5f * hit);
                    if (camera != null) camera.Hit(.012f * hit, 1.6f * hit, Mathf.Sign(Vector3.Dot(owner.transform.right, threats.BiteDirection)));
                }
            }
        }

        static float RandomSign() => Time.unscaledTime % 2 < 1 ? -1 : 1;
    }
}
