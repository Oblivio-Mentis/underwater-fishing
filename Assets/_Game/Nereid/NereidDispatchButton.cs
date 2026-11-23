using UnityEngine;

namespace Raid.Nereid
{
    [DisallowMultipleComponent]
    public sealed class NereidDispatchButton : MonoBehaviour
    {
        [Tooltip("The travelling cap. Its local +Z axis points into the housing.")]
        public Transform cap;
        public Transform palmContact;
        public Collider hitFace;
        [Min(.001f)] public float travel = .055f;
        [Min(.01f)] public float reachSeconds = .14f, pushSeconds = .075f, dwellSeconds = .10f, releaseSeconds = .13f, withdrawSeconds = .19f;

        public int PressCount { get; private set; }

        static NereidDispatchButton touched;
        DiverMotor pressingDiver;
        Vector3 neutral;
        float pressedAt = float.NegativeInfinity;
        float touchedAt = float.NegativeInfinity;

        void Awake()
        {
            if (cap) neutral = cap.localPosition;
        }

        // The command is owned by the station. A press also gives local tactile feedback
        // when the reservoir is empty or the other line is running.
        public void Press(DiverMotor diver)
        {
            if (!cap || !palmContact) return;
            PressCount++;
            if (diver)
            {
                touched = this;
                pressingDiver = diver;
                touchedAt = Time.unscaledTime;
            }
            pressedAt = Time.unscaledTime;
        }

        internal static bool TryPalm(DiverMotor diver, out Transform contact, out float reach)
        {
            contact = null;
            reach = 0;
            if (!touched || touched.pressingDiver != diver || !touched.isActiveAndEnabled) return false;
            float age = Time.unscaledTime - touched.touchedAt;
            float retract = touched.reachSeconds + touched.pushSeconds + touched.dwellSeconds;
            float duration = retract + touched.withdrawSeconds;
            if (age > duration) return false;
            contact = touched.palmContact;
            reach = age < retract ? Mathf.SmoothStep(0,1,age / touched.reachSeconds)
                : 1 - Mathf.SmoothStep(0,1,(age - retract) / touched.withdrawSeconds);
            return true;
        }

        void Update()
        {
            if (!cap || float.IsNegativeInfinity(pressedAt)) return;
            float elapsed = Time.unscaledTime - pressedAt - reachSeconds;
            float release = pushSeconds + dwellSeconds;
            float amount = elapsed < release ? Mathf.SmoothStep(0,1,elapsed / pushSeconds)
                : 1 - Mathf.SmoothStep(0,1,(elapsed - release) / releaseSeconds);
            cap.localPosition = neutral + cap.localRotation * Vector3.forward * (travel * amount);
            if (elapsed >= release + Mathf.Max(releaseSeconds,withdrawSeconds)) pressedAt = float.NegativeInfinity;
        }

        void OnDisable()
        {
            if (touched == this) touched = null;
            pressedAt = float.NegativeInfinity;
            if (cap) cap.localPosition = neutral;
        }
    }
}
