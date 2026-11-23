using System;
using UnityEngine;

namespace Raid
{
    // These bindings live on ordinary Unity prefabs. The authoritative player never becomes
    // a child of the presentation, and neither the mesh nor a replacement clip moves the camera.
    public sealed class DiverAvatarRig : MonoBehaviour
    {
        public enum Action
        {
            Idle, Walk, Sprint, Swim, Harpoon, Aim, Reel, Shoot, CarryFish, CarrySalvage,
            HoldBall, WindThrow, Throw, SwapEquipment, PetGosha, PressPanel, Knockdown, Dead
        }

        [Serializable]
        public sealed class Motion
        {
            public Action action;
            public AnimationClip clip;
            [Min(.01f)] public float speed = 1;
        }

        [Serializable]
        public sealed class Chain
        {
            public Transform upper, lower, end;
            [Tooltip("Bone rotations relative to the shoulder/elbow/wrist or hip/knee/ankle target frames.")]
            public Quaternion upperBind = Quaternion.identity, lowerBind = Quaternion.identity, endBind = Quaternion.identity;

            internal void Drive(Transform a, Transform b, Transform c, Quaternion segmentFrame)
            {
                upper.SetPositionAndRotation(a.position, a.rotation * segmentFrame * upperBind);
                lower.SetPositionAndRotation(b.position, b.rotation * segmentFrame * lowerBind);
                end.SetPositionAndRotation(c.position, c.rotation * endBind);
            }
        }

        public Transform model, head;
        public Quaternion headBind = Quaternion.identity;
        public Chain leftArm, rightArm, leftLeg, rightLeg;
        public Renderer leftSleeve, rightSleeve;

        [Header("First-person hand targets")]
        [Tooltip("Camera-space resting position of the right shoulder. The left arm mirrors X.")]
        public Vector3 shoulderOrigin = new Vector3(.18f, -.19f, .045f);
        [Tooltip("Forward travel of the shoulder girdle when reaching for a distant target (metres).")]
        [Range(0, .08f)] public float shoulderTravel = .045f;
        [Tooltip("Camera-space resting position of the right wrist. The left wrist mirrors X.")]
        public Vector3 restingWrist = new Vector3(.22f, -.215f, .37f);
        [Tooltip("Weapon-space wrist position and rotation while supporting the barrel.")]
        public Vector3 supportWrist = new Vector3(-.038f, -.057f, .116f);
        public Vector3 supportAngles = new Vector3(-14, 28, 50);
        [Tooltip("Weapon-space wrist position and rotation while holding the trigger grip.")]
        public Vector3 triggerWrist = new Vector3(.055f, -.132f, -.097f);
        public Vector3 triggerAngles = new Vector3(-37, -39, -70);
        [Tooltip("Optional local-space animation clips on the model hierarchy. Unkeyed bones follow the gameplay pose.")]
        public Motion[] motions = Array.Empty<Motion>();

        Transform[] bones;
        Vector3[] restPositions, restScales;
        Quaternion[] restRotations;
        Motion playing;
        float elapsed;
        internal static readonly Quaternion DownwardSegment = Quaternion.LookRotation(Vector3.down, Vector3.forward);

        internal Chain Arm(int index) => index == 0 ? leftArm : rightArm;
        internal Chain Leg(int index) => index == 0 ? leftLeg : rightLeg;

        internal void Prepare()
        {
            if (motions.Length == 0 || !model) return;
            // Standalone Unity requires an Animator on the sampling root for native .anim clips.
            if (!model.TryGetComponent<Animator>(out _))
                model.gameObject.AddComponent<Animator>();
            bones = model.GetComponentsInChildren<Transform>(true);
            restPositions = new Vector3[bones.Length];
            restScales = new Vector3[bones.Length];
            restRotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                restPositions[i] = bones[i].localPosition;
                restScales[i] = bones[i].localScale;
                restRotations[i] = bones[i].localRotation;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal float SampledFingerAngle()
        {
            float angle = 0;
            if (playing == null || bones == null) return angle;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != rightArm.end && bones[i].IsChildOf(rightArm.end))
                    angle = Mathf.Max(angle, Quaternion.Angle(restRotations[i], bones[i].localRotation));
            }
            return angle;
        }
#endif

        internal void BeginPose()
        {
            if (playing == null) return;
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i].SetLocalPositionAndRotation(restPositions[i], restRotations[i]);
                bones[i].localScale = restScales[i];
            }
        }

        internal void FinishPose(Action action, float dt)
        {
            Motion next = null;
            foreach (var motion in motions)
                if (motion != null && motion.action == action && motion.clip != null) { next = motion; break; }
            elapsed = playing == next ? elapsed + dt : 0;
            playing = next;
            if (next == null) return;
            float duration = next.clip.length;
            float sample = elapsed * next.speed;
            if (duration > 0) sample = next.clip.isLooping ? Mathf.Repeat(sample, duration) : Mathf.Min(sample, duration);
            next.clip.SampleAnimation(model.gameObject, sample);
        }
    }
}
