#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Raid
{
    // Feel lab: a repeatable owned-camera scenario for tuning fishing and the angler.
    // Stages positions only; every shot, reel and bite runs through the normal server rules.
    // Writes frames through FishingRecorder and a small status file for the lead to read.
    public sealed class FeelLab : MonoBehaviour
    {
        [Serializable] sealed class Status { public string scenario, phase = "idle", error = ""; public int frames; public string[] notes = Array.Empty<string>(); }
        public static FeelLab Active { get; private set; }
        public string Phase => status.phase;
        readonly Status status = new();
        string folder, species;
        float seconds;
        System.Collections.Generic.List<string> notes = new();

        public static FeelLab Begin(string scenario, string species, string folder, float seconds = 25)
        {
            if (Active != null) Destroy(Active.gameObject);
            var lab = new GameObject("Feel lab").AddComponent<FeelLab>();
            Active = lab;
            lab.folder = Path.GetFullPath(folder);
            lab.species = species;
            lab.seconds = seconds;
            lab.status.scenario = scenario;
            Directory.CreateDirectory(lab.folder);
            lab.StartCoroutine(lab.Run(scenario));
            return lab;
        }

        IEnumerator Run(string scenario)
        {
            IEnumerator body = scenario switch
            {
                "swim" => Swim(),
                "catch" => Catch(),
                "angler" => Angler(),
                _ => null
            };
            if (body == null) { Fail("unknown scenario " + scenario); yield break; }
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                object current;
                try
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator child) { stack.Push(child); continue; }
                    current = top.Current;
                }
                catch (Exception e) { Fail(e.Message); yield break; }
                yield return current;
            }
            FishingRecorder.Stop();
            SetPhase("done");
        }

        DiverMotor Host => DiverMotor.ServerDivers.FirstOrDefault(d => d != null && d.IsOwner);
        static readonly System.Reflection.FieldInfo ServerYaw = typeof(DiverMotor).GetField("_serverYaw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        static readonly System.Reflection.FieldInfo ServerPitch = typeof(DiverMotor).GetField("_serverPitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Owner look plus the server aim in one step: the lab runs on a host, so the hit ray
        // must not wait for the next input packet to catch up with the camera.
        void Aim(Vector3 point)
        {
            var host = Host;
            host.LookAtConsole(point);
            Vector3 direction = point - (host.transform.position + Vector3.up * .6f);
            if (direction.sqrMagnitude < .01f) return;
            var angles = Quaternion.LookRotation(direction).eulerAngles;
            ServerYaw?.SetValue(host, angles.y);
            ServerPitch?.SetValue(host, Mathf.Clamp(Mathf.DeltaAngle(0, angles.x), -85, 85));
        }
        HarpoonTool Tool => Host == null ? null : Host.GetComponent<HarpoonTool>();

        IEnumerator Ready()
        {
            SetPhase("waiting for host");
            float until = Time.unscaledTime + 30;
            while (RaidWorld.Instance == null || !RaidWorld.Instance.IsServerInitialized || Host == null || RaidWorld.Instance.Team == null)
            {
                if (Time.unscaledTime > until) throw new InvalidOperationException("host did not spawn");
                yield return null;
            }
            var world = RaidWorld.Instance;
            if (world.CurrentDepth != 1)
            {
                SetPhase("diving");
                var station = ShipConsole.Stations.First(s => s.kind == ShipConsoleKind.Depths);
                bool aligned = false;
                foreach (var side in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left })
                {
                    Host.Teleport(station.transform.position + side * 1.45f + Vector3.up * .5f);
                    Physics.SyncTransforms();
                    if (Host.IsInside && ShipConsole.CanUse(Host, WorldAction.DiveOne)) { aligned = true; break; }
                }
                if (!aligned) throw new InvalidOperationException("chart console unreachable");
                world.Command(WorldAction.DiveOne, 0);
                until = Time.unscaledTime + 40;
                while (world.CurrentDepth != 1 || world.TravelRemaining > 0)
                {
                    if (Time.unscaledTime > until) throw new InvalidOperationException("dive did not complete: " + world.Message);
                    yield return null;
                }
            }
            if (world.threats != null) world.threats.enabled = scenarioNeedsThreats;
        }
        bool scenarioNeedsThreats;

        FishActor FindFish(bool requirePartner)
        {
            foreach (var f in FindObjectsByType<FishActor>(FindObjectsSortMode.None))
                if (f.depthTier == 1 && f.InHabitat && f.Mode == FishMode.Roam && (species == "" || f.SpeciesId == species) && f.RequiresPartner == requirePartner) return f;
            return null;
        }

        IEnumerator WaitForFish(bool partner)
        {
            SetPhase("waiting for " + (species == "" ? "any fish" : species));
            float until = Time.unscaledTime + 90;
            while (FindFish(partner) == null)
            {
                if (Time.unscaledTime > until) throw new InvalidOperationException("no roaming " + species);
                yield return null;
            }
        }

        // Stand beside and slightly behind the fish, at its height, looking at its flank.
        void StandBy(FishActor fish, float distance, float sideSign)
        {
            Vector3 side = Vector3.Cross(Vector3.up, fish.transform.forward).normalized * sideSign;
            Vector3 eye = fish.transform.position + side * distance - fish.transform.forward * .4f + Vector3.up * .25f;
            // Back off along the side until the diver's capsule is clear of rock.
            for (int i = 0; i < 6 && Physics.CheckSphere(eye, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore); i++) eye += side * .5f;
            Host.Teleport(eye - Vector3.up * .6f);
            Physics.SyncTransforms();
            Aim(fish.transform.position + fish.transform.forward * .1f);
        }

        IEnumerator Swim()
        {
            scenarioNeedsThreats = false;
            yield return Ready();
            yield return WaitForFish(false);
            var fish = FindFish(false);
            Note("watching " + fish.SpeciesId + " in " + fish.HabitatName);
            FishingRecorder.Begin(folder, seconds, 20);
            float until = Time.unscaledTime + seconds;
            var behaviours = new System.Collections.Generic.HashSet<string>();
            while (Time.unscaledTime < until)
            {
                SetPhase("swim " + fish.Behaviour);
                behaviours.Add(fish.Behaviour.ToString());
                // Keep the camera on the fish from 4 m, outside its close distance; re-seat if it drifts far.
                Vector3 toFish = fish.transform.position - (Host.transform.position + Vector3.up * .6f);
                if (toFish.magnitude > 7f || toFish.magnitude < 2.2f) StandBy(fish, 4.2f, 1);
                else Aim(fish.transform.position);
                yield return null;
            }
            Note("behaviours: " + string.Join(",", behaviours));
        }

        IEnumerator Catch()
        {
            scenarioNeedsThreats = false;
            yield return Ready();
            var tool = Tool;
            tool.SelectSlot(0);
            bool partner = species == "Bristlefish" || species == "Sailray";
            yield return WaitForFish(partner);
            var fish = FindFish(partner);
            Note("hooking " + fish.SpeciesId + " in " + fish.HabitatName);
            FishingRecorder.Begin(folder, seconds, 20);
            // Approach from the side at 3.6 m, fire, then play the reel like a person: ease on surge warnings.
            StandBy(fish, 3.6f, 1);
            yield return new WaitForSeconds(.4f);
            Aim(fish.transform.position);
            yield return null;
            float until = Time.unscaledTime + 12;
            float nextShot = 0;
            while (tool.HookedFish != fish)
            {
                if (Time.unscaledTime > until) throw new InvalidOperationException("could not hook " + fish.SpeciesId + " (shots " + tool.ShotSequence + ")");
                if (fish.Mode != FishMode.Roam) { StandBy(fish, 3.6f, 1); yield return null; continue; }
                // Aim at the body and fire only when the server-side ray would actually land on this fish.
                Vector3 eye = Host.transform.position + Vector3.up * .6f;
                Vector3 target = fish.ImpactSurface(false).position;
                Aim(target);
                yield return new WaitForFixedUpdate();
                bool onFish = Physics.Raycast(eye, Host.AimDirection, out var hit, tool.tuning.maxRange, tool.tuning.hitMask, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<FishActor>() == fish;
                if (onFish && Time.unscaledTime >= nextShot && tool.QaFireOwned()) nextShot = Time.unscaledTime + tool.tuning.fireCooldown + .15f;
                if (!onFish && Vector3.Distance(eye, target) > 5f) StandBy(fish, 3.4f, 1);
                yield return null;
            }
            SetPhase("hooked");
            long id = fish.CargoId;
            var phases = new System.Collections.Generic.List<string>();
            until = Time.unscaledTime + seconds;
            bool reeling = true;
            while (Time.unscaledTime < until && tool.HookedFish == fish)
            {
                string phase = fish.Behaviour.ToString();
                if (phases.Count == 0 || phases[phases.Count - 1] != phase) phases.Add(phase);
                SetPhase("fight " + phase + " t=" + fish.Tension.ToString("F2"));
                // A human eases on the warning and when the gauge goes red, pulls otherwise.
                if (fish.SurgeWarning || fish.Surging || fish.Tension > .72f) reeling = false;
                else if (fish.Tension < .35f) reeling = true;
                tool.QaHoldOwned(false, reeling ? .5f : 0);
                // Keep the fish in view without moving the body: the tug itself moves the diver.
                Aim(fish.transform.position);
                yield return null;
            }
            tool.QaHoldOwned(false, 0);
            bool landed = Enumerable.Range(2, tool.SlotCount - 2).Any(s => tool.CatchIdInSlot(s) == id) || (fish.RequiresPartner && fish.Mode == FishMode.Captured);
            Note((landed ? "landed " : "not landed ") + fish.SpeciesId + " phases: " + string.Join(">", phases));
        }

        IEnumerator Angler()
        {
            scenarioNeedsThreats = true;
            yield return Ready();
            var threats = ThreatDirector.Instance;
            if (threats == null) throw new InvalidOperationException("no threat director");
            // Stand in open water 6 m from the angler, facing it, and let it come.
            Vector3 angler = threats.AnglerPosition;
            Vector3 away = (angler - Host.transform.position); away.y = 0; away.Normalize();
            Vector3 eye = angler - away * 6f + Vector3.up * .3f;
            for (int i = 0; i < 6 && Physics.CheckSphere(eye, .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore); i++) eye -= away * .5f;
            Host.Teleport(eye - Vector3.up * .6f);
            Physics.SyncTransforms();
            FishingRecorder.Begin(folder, seconds, 20);
            float until = Time.unscaledTime + seconds;
            var stages = new System.Collections.Generic.List<string>();
            int bites = threats.BiteSequence;
            while (Time.unscaledTime < until)
            {
                string stage = threats.AnglerStage.ToString();
                if (stages.Count == 0 || stages[stages.Count - 1] != stage) stages.Add(stage);
                SetPhase("angler " + stage + " d=" + Vector3.Distance(Host.transform.position, threats.AnglerPosition).ToString("F1"));
                Aim(threats.AnglerPosition + Vector3.up * .2f);
                yield return null;
            }
            Note("stages: " + string.Join(">", stages) + " bites=" + (threats.BiteSequence - bites));
        }

        void Note(string text) { notes.Add(text); status.notes = notes.ToArray(); Write(); }
        void SetPhase(string phase) { status.phase = phase; status.frames = FishingRecorder.Active == null ? status.frames : FishingRecorder.Active.Frames; Write(); }
        void Fail(string error) { status.error = error; status.phase = "failed"; FishingRecorder.Stop(); Write(); }
        void Write() { try { File.WriteAllText(Path.Combine(folder, "status.json"), JsonUtility.ToJson(status, true)); } catch (IOException) { } }
        void OnDestroy() { if (Active == this) Active = null; }
    }
}
#endif
