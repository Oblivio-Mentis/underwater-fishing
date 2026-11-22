using UnityEngine;

namespace Raid.Nereid
{
    // Exists only in the server physics scene. The replicated view has no collider.
    internal sealed class GoshaBallBody : MonoBehaviour
    {
        RaidWorld world;
        Rigidbody body;
        SphereCollider sphere;
        PhysicsMaterial rubber;
        public Vector3 Position=>body.position;
        public Vector3 Velocity=>body.linearVelocity;
        public void Initialize(RaidWorld owner)
        {
            world=owner;
            sphere=gameObject.AddComponent<SphereCollider>();sphere.radius=.145f;
            rubber=new PhysicsMaterial("Gosha ball rubber"){bounciness=.53f,dynamicFriction=.55f,staticFriction=.6f};
            sphere.sharedMaterial=rubber;
            body=gameObject.AddComponent<Rigidbody>();body.mass=.24f;body.linearDamping=.37f;body.angularDamping=.42f;
            Hold();
        }
        public void Hold()
        {
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.isKinematic=true;sphere.enabled=false;
        }
        public void Release(Vector3 at,Vector3 speed)
        {
            body.position=at;body.rotation=Quaternion.identity;sphere.enabled=true;
            body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity=speed;body.angularVelocity=Vector3.Cross(Vector3.up,speed)*3;
            body.WakeUp();
        }
        void OnCollisionEnter(Collision hit)
        {
            if(world!=null && hit.collider.TryGetComponent<DiverMotor>(out var diver))world.BallTouchedDiver(diver);
        }
        void OnDestroy(){if(rubber!=null)Destroy(rubber);}
    }
}
