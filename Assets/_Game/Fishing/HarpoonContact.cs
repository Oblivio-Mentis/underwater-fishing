using FishNet.Object;
using UnityEngine;

namespace Raid
{
    // Captured in the moving render surface's frame at the server physics hit.
    public struct HarpoonContact
    {
        public NetworkObject target;
        public bool tail;
        public Vector3 localPoint, localNormal, localDirection;

        public static HarpoonContact FromHit(NetworkObject target, Transform surface, bool tail, RaycastHit hit, Vector3 direction)
        {
            return new HarpoonContact
            {
                target=target, tail=tail,
                localPoint=surface.InverseTransformPoint(hit.point),
                localNormal=surface.localToWorldMatrix.transpose.MultiplyVector(hit.normal).normalized,
                localDirection=surface.InverseTransformVector(direction).normalized
            };
        }

        public bool Resolve(out Transform surface, out Vector3 point, out Vector3 normal, out Vector3 direction)
        {
            surface=null;point=normal=direction=Vector3.zero;
            if(target==null)return false;
            surface=target.TryGetComponent<FishActor>(out var fish)?fish.ImpactSurface(tail):target.transform;
            if(surface==null)return false;
            ResolveOn(surface,out point,out normal,out direction);
            return true;
        }

        internal void ResolveOn(Transform surface,out Vector3 point,out Vector3 normal,out Vector3 direction)
        {
            point=surface.TransformPoint(localPoint);
            normal=surface.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;
            direction=surface.TransformVector(localDirection).normalized;
        }
    }
}
