using UnityEngine;

namespace Raid.Nereid
{
    // The pet's own small wet hatch on NEREID: a mouth in the outer skin, a clear pipe that ends in
    // the real reservoir, and a dry tray for materials. The server routes deliveries through
    // TeamProgress.Deposit; this component only marks the physical places and presents the flow.
    [DefaultExecutionOrder(-20)]
    public sealed class GoshaReceiver : MonoBehaviour
    {
        public static GoshaReceiver Active { get; private set; }

        [Header("Authored in the prefab")]
        [SerializeField] Transform mouth;
        [SerializeField] Transform pipeStart;
        [SerializeField] Transform pipeEnd;
        [SerializeField] Transform dryTray;
        [SerializeField] Animator hatch;
        [SerializeField] string hatchOpenState = "Open";
        [SerializeField] string hatchCloseState = "Close";

        public Vector3 MouthPosition => mouth != null ? mouth.position : transform.position;
        public Vector3 DryPosition => dryTray != null ? dryTray.position : MouthPosition;
        public bool Ready => mouth != null;
        public bool DryReady => dryTray != null;
        public Transform PipeStart => pipeStart;
        public Transform PipeEnd => pipeEnd;

        float openUntil;

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>Client presentation: the hatch swings while the catch passes the skin.</summary>
        public void PresentTransfer()
        {
            openUntil = Time.time + 1.2f;
            if (hatch != null && !string.IsNullOrEmpty(hatchOpenState)) hatch.CrossFade(hatchOpenState, .12f);
        }

        void Update()
        {
            if (hatch == null || openUntil <= 0f || Time.time < openUntil) return;
            openUntil = 0f;
            if (!string.IsNullOrEmpty(hatchCloseState)) hatch.CrossFade(hatchCloseState, .25f);
        }
    }
}
