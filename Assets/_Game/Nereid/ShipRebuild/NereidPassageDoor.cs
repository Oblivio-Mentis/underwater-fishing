using UnityEngine;

namespace Raid.Nereid
{
    public enum PassageId : byte { Machinery, Pump }

    // A single server transition describes both leaves, including to a newly connected observer.
    public struct PassageMotion
    {
        public double Started;
        public float From;
        public bool Opening;

        public float TravelAt(double time, float duration)
        {
            var elapsed = (float)System.Math.Max(0, time - Started);
            return Mathf.MoveTowards(From, Opening ? 1f : 0f, elapsed / Mathf.Max(.1f, duration));
        }
    }

    public sealed class NereidPassageDoor : MonoBehaviour
    {
        public PassageId passage;
        public Transform portLeaf, starboardLeaf;
        public BoxCollider portSeal, starboardSeal;
        [Min(.1f)] public float stroke = 3f;
        [Min(1f)] public float approachDistance = 5.2f;
        [Min(.1f)] public float openingSeconds = .62f, closingSeconds = 1.45f;
        [Min(0f)] public float lingerSeconds = 1.6f;

        Vector3 portClosed, starboardClosed;
        double lastApproach = double.NegativeInfinity;

        void Awake()
        {
            if (!portLeaf || !starboardLeaf || !portSeal || !starboardSeal) { enabled = false; return; }
            portClosed = portLeaf.localPosition;
            starboardClosed = starboardLeaf.localPosition;
        }

        bool Approaching(Vector3 position)
        {
            var p = transform.InverseTransformPoint(position);
            return Mathf.Abs(p.x) < stroke + .9f && p.y > -.9f && p.y < portSeal.size.y + .55f &&
                   Mathf.Abs(p.z) < approachDistance;
        }

        bool Occupied(RaidWorld world)
        {
            foreach (var diver in DiverMotor.ServerDivers)
                if (diver && Approaching(diver.transform.position)) return true;
            var pet = world.Gosha;
            if (pet.Active && Approaching(pet.Position)) return true;
            return world.threats && world.threats.WaveActive && Approaching(world.threats.WavePosition);
        }

        void Update()
        {
            var world = RaidWorld.Instance;
            if (!world || (!world.IsServerInitialized && !world.IsClientInitialized))
            {
                lastApproach = double.NegativeInfinity;
                return;
            }
            var time = world.TimeManager.TicksToTime(world.TimeManager.GetPreciseTick(world.TimeManager.Tick));
            var motion = world.PassageState(passage);
            float travelled = motion.TravelAt(time, motion.Opening ? openingSeconds : closingSeconds);
            if (world.IsServerInitialized)
            {
                if (Occupied(world)) lastApproach = time;
                bool opening = time - lastApproach <= lingerSeconds;
                if (opening != motion.Opening)
                {
                    motion = new PassageMotion { From = travelled, Started = time, Opening = opening };
                    world.SetPassageState(passage, motion);
                }
            }
            float offset = stroke * Mathf.SmoothStep(0, 1, travelled);
            portLeaf.localPosition = portClosed + Vector3.left * offset;
            starboardLeaf.localPosition = starboardClosed + Vector3.right * offset;
            // Release crossing immediately on approach. Re-engage only at rest with the zone empty.
            bool sealedDoor = !motion.Opening && travelled <= 0f;
            portSeal.enabled = sealedDoor;
            starboardSeal.enabled = sealedDoor;
        }
    }
}
