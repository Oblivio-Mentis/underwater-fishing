using FishNet.Object;
using UnityEngine;

namespace Raid
{
    // Captured in the moving render surface's frame at the server physics hit.
    public struct HarpoonContact
    {
        public NetworkObject target;
        public bool tail, ball;
        public int defenseId;
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
            if(defenseId>0)
            {
                if(!target.TryGetComponent<ThreatDirector>(out var raid) || !raid.TryDefenseSurface(defenseId,out surface))return false;
                ResolveOn(surface,out point,out normal,out direction);
                return true;
            }
            if(ball)
            {
                if(!target.TryGetComponent<RaidWorld>(out var world) || !world.TryBallAnchor(out surface,out var center))return false;
                point=center+localPoint;
                normal=localNormal;
                direction=localDirection;
                return true;
            }
            surface=target.TryGetComponent<FishActor>(out var fish)?fish.ImpactSurface(tail):target.transform;
            if(surface==null)return false;
            ResolveOn(surface,out point,out normal,out direction);
            return true;
        }

        internal void ResolveOn(Transform surface,out Vector3 point,out Vector3 normal,out Vector3 direction)
        {
            // The bent body is a pure function of the network tick, so every peer lands on the same skin.
            point=surface.TransformPoint(surface.TryGetComponent<CatchFishModel>(out var spine)?spine.Displace(localPoint):localPoint);
            normal=surface.worldToLocalMatrix.transpose.MultiplyVector(localNormal).normalized;
            direction=surface.TransformVector(localDirection).normalized;
        }
    }
}
