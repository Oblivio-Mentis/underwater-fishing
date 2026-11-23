using UnityEngine;

namespace Raid.Nereid
{
    // Door leaves are saved in the sealed pose. The left and right tracks use chamber-local metres.
    public sealed class NereidPressureDoors : MonoBehaviour
    {
        public Transform cabinFrame, seaFrame;
        public Transform innerPort, innerStarboard, outerPort, outerStarboard;
        [Min(0)] public float stroke = 1.59f;
        public Transform waterSurface;
        public float emptyLevel = -.12f, fullLevel = 4.20f;
        public AirlockPad[] controls;
        readonly Vector3[] closed = new Vector3[4];

        internal void Bind()
        {
            closed[0] = innerPort.localPosition;
            closed[1] = innerStarboard.localPosition;
            closed[2] = outerPort.localPosition;
            closed[3] = outerStarboard.localPosition;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal float MeasuredOpening(bool sea)
        {
            var panel=sea ? outerPort : innerPort;
            return Vector3.Distance(panel.localPosition,closed[sea ? 2 : 0])/Mathf.Max(.001f,stroke);
        }
#endif

        internal void MoveGate(int gate, float open)
        {
            var travel = Vector3.forward * (stroke * open);
            if (gate == 0)
            {
                innerPort.localPosition = closed[0] - travel;
                innerStarboard.localPosition = closed[1] + travel;
            }
            else
            {
                outerPort.localPosition = closed[2] - travel;
                outerStarboard.localPosition = closed[3] + travel;
            }
        }
    }
}
