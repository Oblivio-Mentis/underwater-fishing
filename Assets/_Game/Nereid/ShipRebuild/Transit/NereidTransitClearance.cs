using UnityEngine;

namespace Raid.Nereid
{
    // A reserved walkable volume, evaluated by the pipe and cargo authoring tools.
    public sealed class NereidTransitClearance : MonoBehaviour
    {
        public Bounds walkway = new Bounds(Vector3.up*.55f, new Vector3(1.2f,1.1f,2.5f));
        public bool Intersects(Vector3 worldPoint, Vector3 worldHalfExtents)
        {
            var origin=transform.InverseTransformPoint(worldPoint);
            var x=transform.InverseTransformVector(Vector3.right*worldHalfExtents.x);
            var y=transform.InverseTransformVector(Vector3.up*worldHalfExtents.y);
            var z=transform.InverseTransformVector(Vector3.forward*worldHalfExtents.z);
            var extent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            return walkway.Intersects(new Bounds(origin,extent*2));
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=Color.yellow;
            Gizmos.DrawWireCube(walkway.center,walkway.size);
        }
    }
}

