using System;
using System.Collections.Generic;

namespace UnderwaterFishing.Expedition
{
    public enum ExpeditionPhase : byte { Base, Preparing, Expedition, Return, Debrief, Offering, Failure, Success }

    public struct ExpeditionCrewStatus
    {
        public int Id;
        public bool Connected, Alive, Aboard;
    }

    /// <summary>Server-agnostic expedition clock, consent and day rules. Inputs are confirmed crew/cargo facts.</summary>
    public sealed class ExpeditionModel
    {
        private readonly ExpeditionSettings settings;
        private readonly Dictionary<int, ExpeditionCrewStatus> crew = new Dictionary<int, ExpeditionCrewStatus>();
        private readonly HashSet<int> consents = new HashSet<int>();
        private readonly List<int> removedIds = new List<int>();
        private float phaseRemaining, cargoWaitElapsed;
        private bool dayInFlight, returnTravelStarted;
        public ExpeditionPhase Phase { get; private set; } = ExpeditionPhase.Base;
        public int CompletedDays { get; private set; }
        public int Day => Math.Min(Math.Max(1, settings.DaysPerOffering), Math.Max(1, CompletedDays +
            (Phase == ExpeditionPhase.Debrief || Phase == ExpeditionPhase.Offering || Phase == ExpeditionPhase.Failure ? 0 : 1)));
        public int CrewCount => crew.Count;
        public int RequiredCrew => DevelopmentMode ? 1 : 2;
        public bool DevelopmentMode { get; }
        public float ExpeditionDuration { get; }
        public float ExpeditionRemaining { get; private set; }
        public float PhaseRemaining => phaseRemaining;
        public bool ReturnRequested { get; private set; }
        public bool Emergency { get; private set; }
        public uint Revision { get; private set; }
        public event Action<ExpeditionPhase, ExpeditionPhase> PhaseChanged;
        public int PendingCrewCount
        {
            get
            {
                if (!ReturnRequested || Phase != ExpeditionPhase.Expedition) return 0;
                int count = 0;
                foreach (var member in crew.Values)
                    if (member.Alive && !member.Aboard && !consents.Contains(member.Id)) count++;
                return count;
            }
        }
        public int ConsentCount => consents.Count;
        public bool HasConsented(int crewId) => consents.Contains(crewId);

        public ExpeditionModel(ExpeditionSettings configuration, bool developmentMode, float expeditionSeconds)
        {
            settings = configuration ?? throw new ArgumentNullException(nameof(configuration));
            DevelopmentMode = developmentMode;
            ExpeditionDuration = Positive(expeditionSeconds, 420);
            ExpeditionRemaining = ExpeditionDuration;
        }

        public void UpdateCrew(IEnumerable<ExpeditionCrewStatus> confirmedCrew)
        {
            if (confirmedCrew == null) throw new ArgumentNullException(nameof(confirmedCrew));
            crew.Clear();
            foreach (var member in confirmedCrew) if (member.Connected && member.Id >= 0) crew[member.Id] = member;
            removedIds.Clear();
            foreach (int id in consents)
                if (!crew.TryGetValue(id, out var member) || !member.Alive) removedIds.Add(id);
            foreach (int id in removedIds) consents.Remove(id);
            if (removedIds.Count > 0) Revision++;
        }
        public void RemoveCrew(int id)
        { crew.Remove(id); if (consents.Remove(id)) Revision++; }

        public bool TryStart(int requestingCrewId)
        {
            if (Phase != ExpeditionPhase.Base || CrewCount < RequiredCrew || !Eligible(requestingCrewId)) return false;
            ClearReturn(); Emergency = false; dayInFlight = false;
            ExpeditionRemaining = ExpeditionDuration;
            phaseRemaining = Positive(settings.PreparingSeconds, 3);
            ChangePhase(ExpeditionPhase.Preparing);
            return true;
        }
        public bool TryRequestReturn(int requestingCrewId)
        {
            if (Phase != ExpeditionPhase.Expedition || !Eligible(requestingCrewId)) return false;
            ReturnRequested = true; consents.Add(requestingCrewId); Revision++;
            return true;
        }
        public bool TrySetReturnConsent(int requestingCrewId, bool consent)
        {
            if (Phase != ExpeditionPhase.Expedition || !ReturnRequested || !Eligible(requestingCrewId)) return false;
            bool changed = consent ? consents.Add(requestingCrewId) : consents.Remove(requestingCrewId);
            if (changed) Revision++;
            return true;
        }
        private bool Eligible(int id) => crew.TryGetValue(id, out var member) && member.Connected && member.Alive;

        public void Step(float dt, bool currentCargoProtected)
        {
            if (!Finite(dt) || dt <= 0) return;
            // One call can cross timed phase boundaries, including a short fast-mode expedition.
            for (int transitions = 0; transitions < 8 && dt > 0; transitions++)
            {
                if (Phase == ExpeditionPhase.Preparing)
                {
                    if (CrewCount < RequiredCrew)
                    { ClearReturn(); ChangePhase(ExpeditionPhase.Base); break; }
                    if (!Consume(ref dt, ref phaseRemaining)) break;
                    dayInFlight = true;
                    ChangePhase(ExpeditionPhase.Expedition);
                    continue;
                }
                if (Phase == ExpeditionPhase.Expedition)
                {
                    if (ReturnRequested && PendingCrewCount == 0) { BeginReturn(false); continue; }
                    bool anyoneAlive = false;
                    foreach (var member in crew.Values) if (member.Alive) { anyoneAlive = true; break; }
                    if (!anyoneAlive) { BeginReturn(true); continue; }
                    float expeditionClock = ExpeditionRemaining;
                    bool expired = Consume(ref dt, ref expeditionClock);
                    ExpeditionRemaining = expeditionClock;
                    if (!expired) break;
                    BeginReturn(true); continue;
                }
                if (Phase == ExpeditionPhase.Return)
                {
                    if (!returnTravelStarted)
                    {
                        if (!Emergency && !currentCargoProtected)
                        {
                            float waitRemaining = Math.Max(0, Positive(settings.CargoWaitTimeoutSeconds, 45) - cargoWaitElapsed);
                            float waiting = Math.Min(dt, waitRemaining);
                            cargoWaitElapsed += waiting; dt -= waiting;
                            if (waiting < waitRemaining) break;
                            Emergency = true; Revision++;
                        }
                        returnTravelStarted = true;
                    }
                    if (!Consume(ref dt, ref phaseRemaining)) break;
                    if (dayInFlight) { CompletedDays++; dayInFlight = false; }
                    phaseRemaining = Positive(settings.DebriefSeconds, 4);
                    ChangePhase(ExpeditionPhase.Debrief);
                    continue;
                }
                if (Phase == ExpeditionPhase.Debrief)
                {
                    if (!Consume(ref dt, ref phaseRemaining)) break;
                    ClearReturn();
                    ChangePhase(CompletedDays >= Math.Max(1, settings.DaysPerOffering)
                        ? ExpeditionPhase.Offering : ExpeditionPhase.Base);
                    continue;
                }
                if (Phase == ExpeditionPhase.Success)
                {
                    if (!Consume(ref dt, ref phaseRemaining)) break;
                    ChangePhase(ExpeditionPhase.Base);
                    continue;
                }
                break;
            }
        }
        private void BeginReturn(bool emergency)
        {
            Emergency = emergency; cargoWaitElapsed = 0; returnTravelStarted = emergency;
            phaseRemaining = Positive(settings.ReturnSeconds, 3);
            ChangePhase(ExpeditionPhase.Return);
        }
        public bool CompleteOffering(bool success)
        {
            if (Phase != ExpeditionPhase.Offering) return false;
            ClearReturn();
            if (success) { CompletedDays = 0; Emergency = false; phaseRemaining = Positive(settings.SuccessSeconds, 3); }
            ChangePhase(success ? ExpeditionPhase.Success : ExpeditionPhase.Failure);
            return true;
        }
        public void ResetRun()
        {
            CompletedDays = 0; dayInFlight = false; Emergency = false; phaseRemaining = 0;
            ExpeditionRemaining = ExpeditionDuration; ClearReturn(); ChangePhase(ExpeditionPhase.Base); Revision++;
        }
        private void ClearReturn() { ReturnRequested = false; consents.Clear(); cargoWaitElapsed = 0; returnTravelStarted = false; }
        private void ChangePhase(ExpeditionPhase next)
        {
            if (Phase == next) return;
            ExpeditionPhase previous = Phase; Phase = next; Revision++;
            PhaseChanged?.Invoke(previous, next);
        }
        private static bool Consume(ref float dt, ref float remaining)
        {
            float used = Math.Min(dt, Math.Max(0, remaining)); remaining = Math.Max(0, remaining - used); dt -= used;
            return remaining <= 0;
        }
        private static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        private static float Positive(float n, float fallback) => Finite(n) && n > 0 ? n : fallback;
    }
}
