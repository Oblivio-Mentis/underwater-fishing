using UnityEngine;

namespace Raid.Nereid
{
    public enum PipePort { Receiver, ReservoirInlet, ReservoirProcessOutlet, ReservoirFeedOutlet, Processor, Parasite }

    [DisallowMultipleComponent]
    public sealed class PipeSocket : MonoBehaviour
    {
        public PipePort port;
        [Tooltip("Local bounds of the machine's connection chamber, expressed relative to its fitting.")]
        public Transform fitting;
        public Bounds connectionVolume = new Bounds(Vector3.zero, Vector3.one);
        public bool Fits => fitting && connectionVolume.Contains(fitting.InverseTransformPoint(transform.position));

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Fits ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(transform.position, .17f);
            if (!fitting) return;
            Gizmos.matrix = fitting.localToWorldMatrix;
            Gizmos.DrawWireCube(connectionVolume.center, connectionVolume.size);
        }
    }
}

