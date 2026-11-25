using UnityEngine;

namespace Raid.Nereid
{
    // The pet's own small wet hatch on NEREID: a mouth in the outer skin, a clear pipe that ends in
    // the real reservoir, and a dry tray for materials. The server routes deliveries through
    // TeamProgress.Deposit; this component only marks the physical places and presents the flow.
    // Installed by NEREID / Install Gosha receiver; move the prefab or the pipe knots freely afterwards.
    [DefaultExecutionOrder(-20)]
    public sealed class GoshaReceiver : MonoBehaviour
    {
        public static GoshaReceiver Active { get; private set; }

        [Header("Authored in the prefab")]
        [SerializeField] Transform mouth;
        [SerializeField] Transform pipeStart;
        [SerializeField] Transform pipeEnd;
        [SerializeField] Transform dryTray;
        [SerializeField] Transform flapHinge;
        [SerializeField] Renderer readyLamp;
        [SerializeField, Range(10, 120)] float flapOpenAngle = 78f;
        [SerializeField, Min(.1f)] float flapSeconds = 1.6f;

        public Vector3 MouthPosition => mouth != null ? mouth.position : transform.position;
        public Vector3 DryPosition => dryTray != null ? dryTray.position : MouthPosition;
        public bool Ready => mouth != null;
        public bool DryReady => dryTray != null;
        public Transform PipeStart => pipeStart;
        public Transform PipeEnd => pipeEnd;
        public NereidPipeRoute Pipe => pipe != null ? pipe : pipe = GetComponentInChildren<NereidPipeRoute>();

        NereidPipeRoute pipe;
        float openUntil, flapAngle;
        MaterialPropertyBlock lampBlock;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Emission = Shader.PropertyToID("_EmissionColor");

        void OnEnable() => Active = this;
        void OnDisable() { if (Active == this) Active = null; }

        /// <summary>Client presentation: the flap swings while the catch passes the skin.</summary>
        public void PresentTransfer() => openUntil = Time.time + flapSeconds;

        void Update()
        {
            bool open = Time.time < openUntil;
            flapAngle = Mathf.MoveTowards(flapAngle, open ? flapOpenAngle : 0, Time.deltaTime * 160f);
            if (flapHinge != null) flapHinge.localRotation = Quaternion.Euler(-flapAngle, 0, 0);
            if (Pipe != null) Pipe.SetFlow(open ? 1 : 0);
            var world = RaidWorld.Instance;
            bool unlocked = world != null && (world.View?.TeamRank(SkillLine.GoshaDelivery) ?? 0) >= 5;
            if (readyLamp != null)
            {
                lampBlock ??= new MaterialPropertyBlock();
                var tone = unlocked ? (open ? new Color(.3f, 1f, .7f) : new Color(.12f, .55f, .5f)) : new Color(.08f, .09f, .1f);
                lampBlock.SetColor(BaseColor, tone);
                lampBlock.SetColor(Emission, tone * (unlocked ? 2.2f : 0f));
                readyLamp.SetPropertyBlock(lampBlock);
            }
        }
    }
}
