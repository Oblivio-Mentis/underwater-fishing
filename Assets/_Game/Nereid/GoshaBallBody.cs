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
        public bool TouchedDeck { get; private set; }
        bool underwater;
        public void SetWater(bool wet)
        {
            if(wet==underwater)return;
            underwater=wet;body.useGravity=!wet;
            body.linearDamping=wet?2.4f:.37f;
            body.angularDamping=wet?2.3f:.42f;
        }
        public void Initialize(RaidWorld owner)
        {
            world=owner;
            sphere=gameObject.AddComponent<SphereCollider>();sphere.radius=.145f;
            rubber=new PhysicsMaterial("Gosha ball rubber"){bounciness=.53f,dynamicFriction=.55f,staticFriction=.6f};
            sphere.sharedMaterial=rubber;
            body=gameObject.AddComponent<Rigidbody>();body.mass=.24f;body.linearDamping=.37f;body.angularDamping=.42f;
            Hold();
        }
        public float Radius=>sphere.radius;
        public void Tether()
        {
            Hold();
            sphere.enabled=true;
        }
        public void ReelTo(Vector3 position) => body.MovePosition(position);

        public void Hold()
        {
            if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
            body.isKinematic=true;sphere.enabled=false;
        }
        public void Release(Vector3 at,Vector3 speed)
        {
            TouchedDeck=false;SetWater(false);body.position=at;body.rotation=Quaternion.identity;sphere.enabled=true;
            body.isKinematic=false;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            body.linearDamping=.37f;body.angularDamping=.42f;
            body.linearVelocity=speed;body.angularVelocity=Vector3.Cross(Vector3.up,speed)*3;
            body.WakeUp();
        }
        void OnCollisionEnter(Collision hit)
        {
            var diver=hit.collider.GetComponentInParent<DiverMotor>();
            if(world!=null && diver!=null){world.BallTouchedDiver(diver);return;}
            if(hit.contactCount>0 && hit.GetContact(0).normal.y>.65f)TouchedDeck=true;
        }
        void OnCollisionStay(Collision contact)
        {
            // Rubber loses speed on the grated deck while the airborne throw retains its arc.
            if(body.isKinematic || contact.contactCount==0 || contact.GetContact(0).normal.y<.65f)return;
            body.linearDamping=1.75f;body.angularDamping=2.3f;
        }
        void OnDestroy(){if(rubber!=null)Destroy(rubber);}
    }
}
