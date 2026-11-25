using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public enum GoshaOrder : byte { Come, Work, Follow, Materials }
    public enum GoshaWork : byte { None, Fish, Materials, Balanced }

    internal sealed class GoshaCatch
    {
        public FishActor Fish;
        public LootActor Loot;
        public long Id;
        public bool IsFish => Fish != null;
    }

    // One shared helper, one active job. The server owns selection, the chase, the grab and every
    // delivery; clients only see the snapshot. Nothing selected here is frozen or hidden.
    public sealed partial class RaidWorld
    {
        readonly GoshaHuntBrain hunt = new();
        readonly List<GoshaCatch> carried = new();
        readonly Dictionary<long, float> escapedUntil = new();
        readonly GoshaSwimRoute swimRoute = new();

        FishActor huntFish;
        long huntFishId;
        LootActor huntLoot;
        long huntLootId;
        GoshaWork work;
        DiverMotor orderDiver;
        string orderProfile;
        bool followingOrder, clearingSeaDoor, continuous, finishing;
        float caution, lastOrderAt = float.NegativeInfinity, orderEnds, pocketWait, scanAt, deliverWait, grabWait, huntStartedAt, escapedPruneAt;

        enum DeliverMode { Hands, Cage, Receptor }

        int Autonomy => team?.TeamRank(SkillLine.GoshaAutonomy) ?? 0;
        int SpeedRank => team?.TeamRank(SkillLine.GoshaSpeed) ?? 0;
        int ClawsRank => team?.TeamRank(SkillLine.GoshaClaws) ?? 0;
        int ScentRank => team?.TeamRank(SkillLine.GoshaScent) ?? 0;
        int DeliveryRank => team?.TeamRank(SkillLine.GoshaDelivery) ?? 0;
        GoshaHuntTuning HuntTuning => goshaHunt != null ? goshaHunt : GoshaHuntTuning.Default;
        GoshaHuntStats HuntStats => HuntTuning.Stats(SpeedRank, Autonomy, ClawsRank, carried.Count);

        static bool Training(GoshaMode mode) => mode is GoshaMode.Coming or GoshaMode.Fishing or GoshaMode.BringingFish or GoshaMode.GoingHome;
        bool PetAvailableForBall => pet.Mode != GoshaMode.Cradled && !Training(pet.Mode);

        public string GoshaTrainingHint(DiverMotor diver)
        {
            if (diver == null || !Gosha.Active || View?.goshaTamed != true || !diver.IsOwner) return "";
            if (Training(pet.Mode))
                return work switch
                {
                    GoshaWork.Fish => "Гоша на промысле · H — закончить",
                    GoshaWork.Materials => "Гоша ищет материалы · H — закончить",
                    GoshaWork.Balanced => "Гоша работает · H — закончить",
                    _ => followingOrder ? "Гоша сопровождает вас" : "Гоша занят"
                };
            string hint = "G — ко мне";
            if ((View?.TeamRank(SkillLine.GoshaAutonomy) ?? 0) > 0) hint += "  ·  H — работа";
            if ((View?.TeamRank(SkillLine.GoshaScent) ?? 0) > 0) hint += "  ·  K — материалы";
            hint += "  ·  J — сопровождение";
            return hint;
        }

        float OrderCooldown => tuning.petOrderCooldown * (1f - .4f * Mathf.Clamp01(Autonomy / 10f));
        internal float OrderRemaining() => Mathf.Max(0, lastOrderAt + OrderCooldown - Time.time);

        [ServerRpc(RequireOwnership = false)]
        public void RequestGoshaOrder(GoshaOrder order, NetworkConnection sender = null)
        {
            if (sender?.FirstObject == null) return;
            var diver = sender.FirstObject.GetComponent<DiverMotor>();
            if (diver == null || diver.Owner != sender || !Alive(diver) || diver.IsTrapped || transitionRemaining > 0) return;
            if (!pet.Active || team?.goshaTamed != true) { OrderReply(sender, "Сначала подружитесь с Гошей и верните его на борт"); return; }
            if (order == GoshaOrder.Come)
            {
                // A manual call ends the automatic session; a catch already in the jaws is kept and delivered first.
                StopWork();
                if (pet.Ball == GoshaBallMode.Carried) { OrderReply(sender, "Гоша занят мячом"); return; }
                orderDiver = diver; orderProfile = ProgressKey(diver); followingOrder = false;
                swimRoute.Reset(); SetMode(GoshaMode.Coming);
                orderEnds = Time.time + tuning.petOrderTimeout;
                OrderReply(sender, "Гоша идёт к вам"); gosha.Value = pet; return;
            }
            if (order == GoshaOrder.Follow)
            {
                if (followingOrder && orderDiver == diver) { EndOrder("Сопровождение завершено"); return; }
                if (!RestingPet(pet.Mode) && pet.Mode != GoshaMode.GoingHome && !Training(pet.Mode)) { OrderReply(sender, "Гоша занят"); return; }
                StopWork();
                orderDiver = diver; orderProfile = ProgressKey(diver);
                followingOrder = true; caution = 0; swimRoute.Reset();
                orderEnds = Time.time + Mathf.Min(24f, tuning.petOrderTimeout);
                SetMode(GoshaMode.Coming);
                OrderReply(sender, "Гоша сопровождает вас"); gosha.Value = pet; return;
            }
            if (Autonomy == 0) { OrderReply(sender, "Гоша ещё не умеет работать · откройте «Первый улов»"); return; }
            if (pet.Ball == GoshaBallMode.Carried || (pet.Ball == GoshaBallMode.Flying && pet.Thrower >= 0))
            { OrderReply(sender, "Гоша занят мячом"); return; }
            var mode = order == GoshaOrder.Materials ? GoshaWork.Materials : Autonomy >= 8 ? GoshaWork.Balanced : GoshaWork.Fish;
            if (mode == GoshaWork.Materials && ScentRank == 0) { OrderReply(sender, "Гоша ещё не ищет материалы · «Нюх на находки»"); return; }
            if (work == mode && Training(pet.Mode)) { StopWork(); OrderReply(sender, "Гоша закончил работу"); gosha.Value = pet; return; }
            float remaining = work == GoshaWork.None ? OrderRemaining() : 0;
            if (remaining > 0) { OrderReply(sender, "Гоша отдыхает · ещё " + Mathf.CeilToInt(remaining) + " с"); return; }
            lastOrderAt = Time.time;
            orderDiver = diver; orderProfile = ProgressKey(diver);
            followingOrder = false; finishing = false;
            work = mode;
            continuous = Autonomy >= 5;
            orderEnds = Time.time + tuning.petOrderTimeout * (continuous ? 6f : 2f);
            caution = 0; deliverWait = pocketWait = grabWait = 0; scanAt = 0;
            swimRoute.Reset(); hunt.Configure(HuntStats);
            SetMode(GoshaMode.Fishing);
            OrderReply(sender, mode switch
            {
                GoshaWork.Materials => "Гоша пошёл за материалами",
                GoshaWork.Balanced => "Гоша вышел на промысел",
                _ => "Гоша отправился на охоту"
            });
            gosha.Value = pet;
        }

        void StopWork()
        {
            if (work == GoshaWork.None && !finishing) return;
            work = GoshaWork.None;
            if (carried.Count > 0) { finishing = true; return; }
            huntFish = null; huntFishId = 0; huntLoot = null; huntLootId = 0; hunt.Reset();
            finishing = false;
        }

        void FinishWork(string reason)
        {
            huntFish = null; huntFishId = 0; huntLoot = null; huntLootId = 0; work = GoshaWork.None; finishing = false;
            hunt.Reset();
            EndOrder(reason);
        }

        void EndOrder(string response)
        {
            if (orderDiver != null && orderDiver.Owner != null && orderDiver.Owner.IsActive) OrderReply(orderDiver.Owner, response);
            float returnCaution = caution;
            ReleaseOrder(); swimRoute.Reset();
            if (SwimZone(pet.Position) == PetZone.Deck) ResumeDeck();
            else { caution = returnCaution; SetMode(GoshaMode.GoingHome); }
        }

        void ReleaseOrder()
        {
            DropCarried();
            huntFish = null; huntFishId = 0; huntLoot = null; huntLootId = 0; hunt.Reset();
            orderDiver = null; orderProfile = null;
            followingOrder = false; work = GoshaWork.None; finishing = false; continuous = false; caution = 0;
            pet.OrderOwner = -1;
        }

        // Anything in the jaws is set back into the water as the same real object, never deleted.
        void DropCarried()
        {
            var dir = pet.Heading.sqrMagnitude > .01f ? pet.Heading : Vector3.forward;
            for (int i = 0; i < carried.Count; i++)
            {
                var c = carried[i];
                var at = pet.Position + dir * (.5f + i * -.22f) - Vector3.up * (.1f + i * .05f);
                if (c.Fish != null) c.Fish.ReleaseGoshaCatch(at, Quaternion.LookRotation(dir));
                else if (c.Loot != null) c.Loot.PutDownByGosha(at);
            }
            carried.Clear();
        }

        void ResumeDeck()
        {
            caution = 0;
            if (pet.Ball == GoshaBallMode.Deck && pet.Thrower >= 0 && deck.Interior(pet.BallPosition)) SetMode(GoshaMode.Fetching, tuning.fetchTimeout);
            else if (pet.Ball == GoshaBallMode.Held && pet.Thrower >= 0 && pet.Holder != pet.Thrower && OnDeck(CrewMember(pet.Holder))) SetMode(GoshaMode.Chasing, 20);
            else SetMode(GoshaMode.Resting, 2);
        }

        bool TickOrder(float dt)
        {
            if (!Training(pet.Mode)) return false;
            hunt.Configure(HuntStats);
            PruneEscapes();
            if (pet.Mode == GoshaMode.GoingHome)
            {
                if (TravelTo(deck.Mark(0), dt)) ResumeDeck();
                return true;
            }
            if (pet.Mode == GoshaMode.Coming)
            {
                if (followingOrder)
                {
                    if (!Alive(orderDiver) || orderDiver.Owner == null || !orderDiver.Owner.IsActive || Time.time > orderEnds)
                    { EndOrder("Сопровождение завершено"); return true; }
                    var caller = orderDiver.transform.position;
                    if ((pet.Position - caller).sqrMagnitude > 2.72f || SwimZone(pet.Position) != SwimZone(caller)) TravelTo(caller, dt);
                    return true;
                }
                if (!Alive(orderDiver) || orderDiver.Owner == null || !orderDiver.Owner.IsActive || Time.time > orderEnds)
                { EndOrder("Гоша вернулся без добычи"); return true; }
                if (TravelTo(orderDiver.transform.position, dt)) EndOrder("Гоша рядом");
                return true;
            }
            if (work != GoshaWork.None || finishing) { TickWork(dt); return true; }
            EndOrder("Гоша вернулся");
            return true;
        }

        void TickWork(float dt)
        {
            if (team == null || !team.goshaTamed) { FinishWork("Гоша вернулся на палубу"); return; }
            if (Time.time > orderEnds) { FinishWork("Рабочая смена окончена"); return; }
            if (SwimZone(pet.Position) != PetZone.Sea)
            {
                // The helper leaves the hull through the real pressure lock; it never crosses a bulkhead.
                TravelTo(LockPoint(12.2f), dt);
                return;
            }
            // With nobody in the water the pet can still work into the open cage or its own hatch.
            bool canHandOver = AnyDiverAtSea() || (DeliveryRank >= 3 && CageLoadingOpen()) ||
                (DeliveryRank >= 5 && GoshaReceiver.Active != null && GoshaReceiver.Active.Ready);
            if (carried.Count == 0 && !canHandOver) { FinishWork("Экипаж на борту · Гоша возвращается"); return; }
            PlaceCarried();
            if (huntFish != null) { HuntFishTick(dt); return; }
            if (huntLoot != null) { HuntLootTick(dt); return; }
            bool mustDeliver = CarriedWeight >= HuntStats.Capacity || (finishing && carried.Count > 0);
            if (mustDeliver) { DeliverTick(dt); return; }
            if (Time.time >= scanAt)
            {
                scanAt = Time.time + HuntTuning.scanBackoff;
                FindTarget();
            }
            if (huntFish != null) { HuntFishTick(dt); return; }
            if (huntLoot != null) { HuntLootTick(dt); return; }
            // Nothing available: keep station near the crew instead of spamming scans.
            var near = NearestSeaDiver(pet.Position, 6.5f);
            if (near != null && !finishing) TravelTo(near.transform.position, dt);
            else if (carried.Count > 0) DeliverTick(dt);
        }

        void FindTarget()
        {
            if (finishing) return;
            if (work is GoshaWork.Fish or GoshaWork.Balanced) huntFish = SelectFish();
            if (huntFish != null)
            {
                huntFishId = huntFish.CargoId; huntStartedAt = Time.time; grabWait = 0;
                hunt.Begin(hunt.Velocity);
                return;
            }
            if (ScentRank > 0 && work is GoshaWork.Materials or GoshaWork.Balanced) huntLoot = SelectLoot();
            if (huntLoot != null)
            {
                huntLootId = huntLoot.CargoId; huntStartedAt = Time.time; grabWait = 0;
                hunt.Begin(hunt.Velocity);
            }
        }

        FishActor SelectFish()
        {
            float radius = tuning.petSearchRadius * (1f + .06f * Autonomy);
            float riskLimit = GoshaSwimRoute.RiskLimit(0);
            FishActor best = null; float bestScore = float.MaxValue;
            foreach (var fish in FishActor.ServerSchool)
            {
                if (fish == null || !fish.IsAvailableForGosha) continue;
                var at = fish.transform.position;
                if (SwimZone(at) != PetZone.Sea) continue;
                if (escapedUntil.TryGetValue(fish.CargoId, out float until) && until > Time.time) continue;
                float squared = (at - pet.Position).sqrMagnitude;
                if (squared > radius * radius) continue;
                float risk = GoshaSwimRoute.Risk(at);
                if (risk > riskLimit) continue;
                float score = Mathf.Sqrt(squared) + risk * 8f;
                if (Autonomy >= 2 && WatchedByCrew(at, out bool aimed))
                {
                    if (aimed) continue;
                    score += 14f;
                }
                float edge = HuntStats.MaxSpeed - fish.FleeSpeed;
                if (edge < 0) score += -edge * 6f;
                if (score < bestScore) { bestScore = score; best = fish; }
            }
            return best;
        }

        LootActor SelectLoot()
        {
            float radius = HuntTuning.searchRadius[Mathf.Clamp(ScentRank, 0, HuntTuning.searchRadius.Length - 1)];
            float riskLimit = GoshaSwimRoute.RiskLimit(0);
            LootActor best = null; float bestScore = float.MaxValue;
            foreach (var item in LootActor.VisibleStock)
            {
                if (item == null || !item.IsFreeOnSeabed || item.gameObject.scene != gameObject.scene) continue;
                if (item.tier > 1 && ScentRank < 2) continue;
                if (CarriedWeight + item.CarryWeight > HuntStats.Capacity) continue;
                var at = item.transform.position;
                if (SwimZone(at) != PetZone.Sea) continue;
                float squared = (at - pet.Position).sqrMagnitude;
                if (squared > radius * radius) continue;
                float risk = GoshaSwimRoute.Risk(at);
                if (risk > riskLimit) continue;
                float score = Mathf.Sqrt(squared) + risk * 8f + (item.tier == 1 ? 0 : 10f);
                if (Autonomy >= 2 && WatchedByCrew(at, out bool aimed))
                {
                    if (aimed) continue;
                    score += 12f;
                }
                if (score < bestScore) { bestScore = score; best = item; }
            }
            return best;
        }

        bool WatchedByCrew(Vector3 at, out bool aimed)
        {
            aimed = false;
            var cone = HuntTuning.aimCone; var range = HuntTuning.aimRange;
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (diver == null || !Alive(diver) || diver.IsInside) continue;
                Vector3 to = at - (diver.transform.position + Vector3.up * .6f);
                if (to.sqrMagnitude > range * range) continue;
                if (Vector3.Dot(diver.AimDirection, to.normalized) < cone) continue;
                aimed = true;
                return true;
            }
            return false;
        }

        void HuntFishTick(float dt)
        {
            var fish = huntFish;
            if (fish == null || fish.CargoId != huntFishId || !fish.IsFreeSwimming) { LoseFish(false); return; }
            if (fish.HookCount > 0 || fish.Mode != FishMode.Roam) { LoseFish(false); return; }
            var from = pet.Position;
            var fishAt = fish.transform.position;
            bool ClearOnly(Vector3 a, Vector3 b) => swimRoute.Clear(a, b);
            var desired = hunt.Step(dt, from, pet.Heading, tuning.petScale, fishAt, fish.SwimVelocity, ClearOnly, out bool grab);
            var next = swimRoute.Step(from, desired, Vector3.Distance(from, desired), true, caution);
            hunt.Confirm(from, next, dt);
            Face(next - from); pet.Position = next;
            if (grab && fish.CaptureForGosha())
            {
                carried.Add(new GoshaCatch { Fish = fish, Id = huntFishId });
                hunt.MarkCaught();
                huntFish = null; huntFishId = 0; deliverWait = 0;
                return;
            }
            if (hunt.Phase == GoshaHuntPhase.Failed) LoseFish(true);
        }

        void LoseFish(bool escaped)
        {
            if (escaped && huntFishId > 0) escapedUntil[huntFishId] = Time.time + HuntTuning.escapedCooldown;
            huntFish = null; huntFishId = 0;
            hunt.Reset();
            scanAt = Time.time + HuntTuning.scanBackoff;
        }

        void HuntLootTick(float dt)
        {
            var item = huntLoot;
            if (item == null || item.CargoId != huntLootId || !item.IsFreeOnSeabed) { huntLoot = null; huntLootId = 0; hunt.Reset(); return; }
            var at = item.transform.position;
            var from = pet.Position;
            if ((at - from).sqrMagnitude > 2.25f)
            {
                var desired = hunt.Travel(dt, from, at, HuntStats.MaxSpeed, 1.2f);
                var next = swimRoute.Step(from, desired, Vector3.Distance(from, desired), true, caution);
                hunt.Confirm(from, next, dt);
                Face(next - from); pet.Position = next; grabWait = 0;
                if (Time.time - huntStartedAt > HuntTuning.chaseTimeout * 1.5f) { huntLoot = null; huntLootId = 0; hunt.Reset(); }
                return;
            }
            grabWait += dt;
            if (grabWait < HuntTuning.grabSeconds) return;
            grabWait = 0;
            if (item.TakeByGosha())
            {
                carried.Add(new GoshaCatch { Loot = item, Id = huntLootId });
                huntLoot = null; huntLootId = 0; hunt.Reset(); deliverWait = 0;
            }
        }

        void PlaceCarried()
        {
            if (carried.Count == 0) return;
            var dir = pet.Heading.sqrMagnitude > .01f ? pet.Heading : Vector3.forward;
            for (int i = 0; i < carried.Count; i++)
            {
                var c = carried[i];
                var at = i == 0
                    ? GoshaHuntBrain.Jaws(pet.Position, dir, tuning.petScale)
                    : pet.Position - dir * (.2f + i * .18f) - Vector3.up * (i * .05f);
                if (c.Fish != null) c.Fish.CarryForGosha(at, Quaternion.LookRotation(dir));
                else if (c.Loot != null) c.Loot.CarryByGosha(at, Quaternion.LookRotation(dir));
            }
        }

        void DeliverTick(float dt)
        {
            bool onlyFish = true, onlyLoot = true;
            foreach (var c in carried) { if (c.IsFish) onlyLoot = false; else onlyFish = false; }
            if (carried.Count == 0) { finishing = false; hunt.Reset(); return; }

            // Rank V: the pet's own wet hatch on NEREID feeds the real aquarium.
            var receiver = GoshaReceiver.Active;
            if (DeliveryRank >= 5 && onlyFish && receiver != null && receiver.Ready && team.FitsReservoir(progression))
            {
                if (DeliverAt(dt, receiver.MouthPosition, 1.6f, DeliverMode.Receptor)) return;
            }
            // Rank III: the open recovery cage, through the team manifest only.
            if (DeliveryRank >= 3 && CageLoadingOpen() && CageMouth(out var cage) && CageHasRoom())
            {
                if (DeliverAt(dt, cage, 1.7f, DeliverMode.Cage)) return;
            }
            // Hands: anyone in the water with room. Materials can also go into the dry hatch later.
            var diver = NearestReceiver(pet.Position);
            if (diver != null)
            {
                if (DeliverAt(dt, diver.transform.position + Vector3.up * .3f, 1.6f, DeliverMode.Hands, diver)) return;
                if (ApproachBlocked) { skipReceiverUntil[diver.OwnerId] = Time.time + 8f; approachStall = 0; }
            }
            if (DeliveryRank >= 5 && onlyLoot && receiver != null && receiver.Ready && team != null && receiver.DryReady)
            {
                if (DeliverAt(dt, receiver.DryPosition, 1.6f, DeliverMode.Receptor)) return;
            }
            deliverWait += dt;
            if (deliverWait > HuntTuning.cageWait)
            {
                // Nothing can take the catch: keep it, stop the shift and let the crew sort it out.
                FinishWork("Гоша ждёт места для добычи");
            }
        }

        bool DeliverAt(float dt, Vector3 at, float reach, DeliverMode mode, DiverMotor diver = null)
        {
            if (!Approach(at, reach, dt)) return false;
            switch (mode)
            {
                case DeliverMode.Hands:
                    if (diver == null || !diver.TryGetComponent<HarpoonTool>(out var tool) || tool.HandsFull) return false;
                    var receiverKey = ProgressKey(diver) ?? orderProfile;
                    for (int i = carried.Count - 1; i >= 0; i--)
                    {
                        var c = carried[i];
                        bool taken = c.Fish != null ? c.Fish.CollectForGosha(tool, receiverKey) : c.Loot != null && c.Loot.HandOverFromGosha(diver);
                        if (taken) carried.RemoveAt(i);
                    }
                    break;
                case DeliverMode.Cage:
                    for (int i = carried.Count - 1; i >= 0; i--)
                    {
                        var c = carried[i];
                        bool stored = c.Fish != null ? c.Fish.LoadGoshaCatchInto(team, progression, Owners(receiverKey: orderProfile))
                            : c.Loot != null && c.Loot.LoadFromGosha(team, progression);
                        if (stored) carried.RemoveAt(i);
                    }
                    break;
                case DeliverMode.Receptor:
                    for (int i = carried.Count - 1; i >= 0; i--)
                    {
                        var c = carried[i];
                        bool stored = c.Fish != null ? c.Fish.DepositGoshaCatch(team)
                            : c.Loot != null && c.Loot.DepositFromGosha(team, progression);
                        if (stored) carried.RemoveAt(i);
                    }
                    break;
            }
            if (carried.Count == 0) DeliveryComplete();
            return true;
        }

        string[] Owners(string receiverKey) => string.IsNullOrEmpty(receiverKey) ? null : new[] { receiverKey };

        void DeliveryComplete()
        {
            deliverWait = 0; hunt.Reset();
            if (!continuous || finishing)
            {
                finishing = false;
                FinishWork("Гоша доставил добычу");
            }
        }

        Vector3 approachGoal; float approachBest, approachStall;

        // True once the pet is within reach of the goal. A goal that cannot be reached (a diver standing
        // in a rock seam, a cage mouth behind grating) is given up after a few seconds without progress.
        bool Approach(Vector3 goal, float arrive, float dt)
        {
            float distance = Vector3.Distance(goal, pet.Position);
            if (distance <= arrive && (distance <= arrive * .6f || swimRoute.Clear(pet.Position, goal))) { approachStall = 0; return true; }
            if ((goal - approachGoal).sqrMagnitude > 1f) { approachGoal = goal; approachBest = distance; approachStall = 0; }
            if (distance < approachBest - .05f) { approachBest = distance; approachStall = 0; }
            else approachStall += dt;
            var from = pet.Position;
            var desired = hunt.Travel(dt, from, goal, HuntStats.MaxSpeed, arrive);
            var next = swimRoute.Step(from, desired, Vector3.Distance(from, desired), true, caution);
            hunt.Confirm(from, next, dt);
            Face(next - from); pet.Position = next;
            return false;
        }

        bool ApproachBlocked => approachStall > 4f;

        bool CageLoadingOpen() => team != null && team.hoistPhase == HoistPhase.Loading;

        int CarriedWeight
        {
            get
            {
                int weight = 0;
                foreach (var c in carried) weight += c.Loot != null ? c.Loot.CarryWeight : 1;
                return weight;
            }
        }

        bool CageHasRoom() => team.CargoUsed + CarriedWeight <= team.Capacity(progression);

        bool CageMouth(out Vector3 at)
        {
            at = default;
            var basket = container != null ? container.GetComponentInChildren<CargoBasket>() : null;
            if (basket == null) return false;
            at = basket.transform.TransformPoint(new Vector3(0, .9f, 0));
            return true;
        }

        readonly Dictionary<int, float> skipReceiverUntil = new();

        DiverMotor NearestReceiver(Vector3 from)
        {
            DiverMotor best = null; float bestDistance = float.MaxValue;
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (diver == null || !Alive(diver) || diver.IsInside) continue;
                if (skipReceiverUntil.TryGetValue(diver.OwnerId, out float until) && until > Time.time) continue;
                if (!diver.TryGetComponent<HarpoonTool>(out var tool) || tool.HandsFull) continue;
                float distance = (diver.transform.position - from).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = diver; }
            }
            return best;
        }

        DiverMotor NearestSeaDiver(Vector3 from, float range)
        {
            DiverMotor best = null; float bestDistance = range * range;
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (diver == null || !Alive(diver) || diver.IsInside) continue;
                float distance = (diver.transform.position - from).sqrMagnitude;
                if (distance < bestDistance) { bestDistance = distance; best = diver; }
            }
            return best;
        }

        bool AnyDiverAtSea() => NearestSeaDiver(pet.Position, float.MaxValue) != null;

        void PruneEscapes()
        {
            if (Time.time < escapedPruneAt) return;
            escapedPruneAt = Time.time + 5f;
            var stale = new List<long>();
            foreach (var pair in escapedUntil) if (pair.Value <= Time.time) stale.Add(pair.Key);
            foreach (var id in stale) escapedUntil.Remove(id);
        }

        internal bool HunterAt(Vector3 point, float radius, out Vector3 at, out Vector3 velocity)
        {
            at = default; velocity = default;
            if (!pet.Active || team == null || !team.goshaTamed) return false;
            at = pet.Position;
            if (SwimZone(at) != PetZone.Sea) return false;
            if ((at - point).sqrMagnitude > radius * radius) return false;
            velocity = hunt.Velocity;
            return true;
        }

        [TargetRpc] void OrderReply(NetworkConnection recipient, string message)
        {
            UI.UiRoot.Instance?.Toast(message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastGoshaOrderReply = message;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string LastGoshaOrderReply { get; private set; }
        internal FishActor HuntTarget => huntFish;
        internal LootActor MaterialTarget => huntLoot;
        internal int CarriedCount => carried.Count;
        internal GoshaWork CurrentWork => work;
        internal void StageWorkForTrial(GoshaWork mode, int speed, int autonomy, int claws, int scent, int delivery)
        {
            team.goshaMet = team.goshaTamed = true; ResetGosha();
            void Grant(SkillLine line, int rank) { for (int i = 1; i <= rank; i++) team.teamSkills.Add(SkillTreeDefinition.Id(line, i)); }
            Grant(SkillLine.GoshaAutonomy, autonomy); Grant(SkillLine.GoshaSpeed, speed);
            Grant(SkillLine.GoshaClaws, claws); Grant(SkillLine.GoshaScent, scent); Grant(SkillLine.GoshaDelivery, delivery);
            lastOrderAt = float.NegativeInfinity;
            work = mode; continuous = true; finishing = false; orderEnds = Time.time + 900f;
            scanAt = 0; swimRoute.Reset(); SetMode(GoshaMode.Fishing);
            Publish();
        }
        internal void StopWorkForTrial() => FinishWork("trial");
#endif

        enum PetZone { Deck, Lock, Sea }
        PetZone SwimZone(Vector3 p)
        {
            var chamber = NereidAirlock.Active;
            if (chamber.Contains(p)) return PetZone.Lock;
            // The authored room stops 10 cm before the inner gate center. The steel sill
            // in that seam is part of the doorway even before the chamber volume begins.
            var local = chamber.transform.InverseTransformPoint(p);
            if (local.x >= -.28f && local.x < 0 && Mathf.Abs(local.z) < 1.5f && local.y > -.16f && local.y < 3.3f) return PetZone.Lock;
            return NereidVessel.Active.ContainsSealed(p) ? PetZone.Deck : PetZone.Sea;
        }
        static Vector3 LockPoint(float x) => NereidAirlock.Active.transform.TransformPoint(new Vector3(x, 1, 0));

        bool TravelTo(Vector3 goal, float dt)
        {
            var here = SwimZone(pet.Position); var there = SwimZone(goal);
            if (clearingSeaDoor && here == PetZone.Sea)
            {
                // Fully leave the sill before turning towards a shoal below or behind the hull.
                if (!SwimTo(LockPoint(11.5f), dt, false)) return false;
                clearingSeaDoor = false;
            }
            if (here == there)
                return here == PetZone.Deck ? Walk(goal, tuning.petFetchSpeed, dt) : SwimTo(goal, dt, here == PetZone.Sea);
            var chamber = NereidAirlock.Active;
            if (here == PetZone.Lock)
            {
                bool sea = there == PetZone.Sea;
                if (!GateOpen(sea))
                {
                    if (SwimTo(LockPoint(4.25f), dt, false)) RequestPetGate(sea);
                    return false;
                }
                clearingSeaDoor = sea;
                SwimTo(LockPoint(sea ? 11.5f : -1.8f), dt, false); return false;
            }
            bool outside = here == PetZone.Sea;
            var entrance = LockPoint(outside ? 11.2f : -1.8f);
            var local = chamber.transform.InverseTransformPoint(pet.Position);
            bool aligned = Mathf.Abs(local.z) < .74f &&
                (outside ? local.x > 8.5f && local.x < 11.95f && Mathf.Abs(local.y - 1) < .7f : local.x > -2.5f);
            if (!aligned)
            {
                if (outside) SwimTo(entrance, dt, true);
                else Walk(entrance, tuning.petFetchSpeed, dt);
                return false;
            }
            if (!GateOpen(outside)) { RequestPetGate(outside); return false; }
            SwimTo(LockPoint(4.25f), dt, false);
            return false;
        }
        bool GateOpen(bool sea) => airlock.Value.Phase == (sea ? AirlockPhase.OuterOpen : AirlockPhase.InnerOpen);
        void RequestPetGate(bool sea)
        {
            var state = airlock.Value;
            if (!state.CanRequest(sea)) return;
            var chamber = NereidAirlock.Active;
            foreach (var d in DiverMotor.ServerDivers)
                if (Alive(d) && chamber.Contains(d.transform.position)) return;
            if (chamber.DoorOccupied(true) || chamber.DoorOccupied(false)) return;
            if (state.Request(sea)) airlock.Value = state;
        }

        bool SwimTo(Vector3 target, float dt, bool sea)
        {
            var from = pet.Position;
            if (Vector3.Distance(from, target) < .68f && swimRoute.Clear(from, target)) { hunt.Stop(); return true; }
            var desired = hunt.Travel(dt, from, target, HuntStats.MaxSpeed * 1.05f, .65f);
            var next = swimRoute.Step(from, desired, Vector3.Distance(from, desired), sea, sea ? caution : 0);
            hunt.Confirm(from, next, dt);
            Face(next - from); pet.Position = next;
            return false;
        }

        internal bool PetInDoor(Vector3 lockLocal, float doorX) => Training(pet.Mode) &&
            Mathf.Abs(lockLocal.x - doorX) < .65f && Mathf.Abs(lockLocal.z) < 1.95f && lockLocal.y > -.4f && lockLocal.y < 3.8f;
    }
}
