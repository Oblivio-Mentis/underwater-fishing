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
        int defenseTarget;
        int carriedCount;
        Camera eye;
        float nextLookup;
        bool wasSoloTow, wasCoordinated;

        public void Bind(DiverMotor diver)
        {
            owner = diver;
            tool = diver == null ? null : diver.GetComponent<HarpoonTool>();
            camera = diver == null ? null : diver.CameraImpulse;
            eye = diver ? diver.GetComponentInChildren<Camera>() : null;
            carriedCount = tool ? tool.CatchCount : 0;
            ResetCounters();
        }

        void ResetCounters()
        {
            fish = tool == null ? null : tool.HookedFish;
            hookSequence = fish == null ? 0 : fish.HookSequence;
            tugSequence = fish == null ? 0 : fish.TugSequence;
            biteSequence = threats == null ? 0 : threats.BiteSequence;
            wasSoloTow = fish != null && fish.SoloTow;
            wasCoordinated = fish != null && fish.Coordinated;
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
            if (observed != owner) { ui.PullEdge(Vector2.zero, 0); return; }
            var current = tool == null ? null : tool.HookedFish;
            bool attached = current != null && current != fish;
            if (current != fish)
            {
                fish = current;
                hookSequence = fish == null ? 0 : fish.HookSequence;
                tugSequence = fish == null ? 0 : fish.TugSequence;
                wasSoloTow = fish != null && fish.SoloTow;
                wasCoordinated = fish != null && fish.Coordinated;
            }
            bool controlling = ui.Page == UI.UiPage.None && !UI.UiInputGate.Modal && !owner.IsHelpless && !owner.IsDead;
            if(tool && tool.TryHookedDefense(out var predator) && controlling)
            {
                Vector3 toward=predator.position-owner.transform.position;
                Vector3 local=eye?eye.transform.InverseTransformDirection(toward.normalized):Vector3.forward;
                float strain=Mathf.Clamp01(tool.LineTension*.9f+predator.effort*.68f);
                ui.PullEdge(new Vector2(local.x,local.y).normalized,strain*.62f);
                if(defenseTarget!=predator.id)
                {
                    ui.HitMark(UI.UiTheme.Cyan,1.15f);
                    ui.PushFishingPunch(6.5f*Setting(s=>s.hitImpulse));
                }
                if(camera)camera.SetStrain(strain*Setting(s=>s.cameraShake));
                defenseTarget=predator.id;
                return;
            }
            defenseTarget=0;

            bool fighting = fish != null && fish.Mode >= FishMode.Hooked && fish.Mode <= FishMode.Tired;
            if (fighting && controlling && camera != null)
            {
                float strain = Mathf.Clamp01(tool.LineTension * .9f + fish.TugStrength * .8f + (fish.SurgeWarning ? .25f : 0));
                camera.SetStrain(strain * Setting(s => s.cameraShake));
            }
            // Pull edge: the screen side the line drags toward lights up with the pull.
            if (fighting && controlling)
            {
                Vector3 local = eye == null ? Vector3.forward : eye.transform.InverseTransformDirection((fish.transform.position - eye.transform.position).normalized);
                Vector2 dir = new Vector2(local.x, local.y);
                float pull = Mathf.Clamp01(fish.TugStrength * 1.1f + (tool.Reeling ? tool.LineTension * .35f : 0));
                ui.PullEdge(dir.sqrMagnitude > .0001f ? dir.normalized : Vector2.zero, pull * (0.35f + 0.65f * Mathf.Clamp01(dir.magnitude * 2f)));
            }
            else ui.PullEdge(Vector2.zero, 0);

            bool solo = fighting && fish.SoloTow;
            if (solo && !wasSoloTow && controlling)
            {
                ui.PushFishingPunch(fish.tuning.soloHeavyCameraImpulse * Setting(s => s.fishingCamera));
                Vector3 toFish = fish.transform.position - owner.transform.position;
                if (camera != null) camera.Hit(fish.tuning.soloHeavyCameraHit, 1.6f, Mathf.Sign(Vector3.Dot(owner.transform.right, toFish)));
            }
            wasSoloTow = solo;
            bool coordinated = fighting && fish.Coordinated;
            if (coordinated && !wasCoordinated && controlling)
            {
                ui.HitMark(UI.UiTheme.Green, .9f);
            }
            else if (!coordinated && wasCoordinated && fighting && fish.RequiresPartner && controlling)
                ui.Toast("Согласованная тяга потеряна");
            wasCoordinated = coordinated;

            // Landing: the catch reaches the hand. One white mark, one punch.
            int count = tool ? tool.CatchCount : 0;
            if (count > carriedCount && controlling)
            {
                ui.HitMark(Color.white, 1.4f);
                ui.PushFishingPunch(4f * Setting(s => s.hitImpulse));
            }
            carriedCount = count;
            if (fish != null && (attached || fish.HookSequence != hookSequence))
            {
                hookSequence = fish.HookSequence;
                if (controlling)
                {
                    float hit = Setting(s => s.hitImpulse);
                    ui.PushFishingPunch(6.5f * hit);
                    ui.HitMark(UI.UiTheme.Cyan, 1f);
                    if (camera != null) camera.Hit(.010f * Mathf.Clamp(fish.power,.5f,3f), 1.1f, 0f);
                }
            }
            if (fish != null && fish.TugSequence != tugSequence)
            {
                tugSequence = fish.TugSequence;
                if (controlling && fish.Surging)
                {
                    ui.PushFishingPunch(3.5f * Setting(s => s.fishingCamera));
                    // The kick follows the fish: it yanks the view toward the side the fish bolts to.
                    Vector3 toFish = fish.transform.position - owner.transform.position;
                    float side = Mathf.Sign(Vector3.Dot(owner.transform.right, toFish));
                    if (camera != null) camera.Hit(.018f + .02f * Mathf.Clamp(fish.power, .5f, 3f) * .5f, 2.4f, side);
                }
            }
            if (threats != null && threats.BiteSequence != biteSequence)
            {
                biteSequence = threats.BiteSequence;
                bool mine = threats.BiteVictim == owner.OwnerId;
                if (mine && controlling)
                {
                    float hit = Setting(s => s.hitImpulse);
                    ui.PushFishingPunch(7f * hit);
                    if (camera != null) camera.Hit(.05f * hit, 5.5f * hit, Mathf.Sign(Vector3.Dot(owner.transform.right, threats.BiteDirection)));
                }
            }
        }
    }
}
