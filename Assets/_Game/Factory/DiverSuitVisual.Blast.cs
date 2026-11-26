using UnityEngine;

namespace Raid
{
    public sealed partial class DiverSuitVisual
    {
        sealed class BlastLimb
        {
            public Limb Visual;
            public Rigidbody Upper, Lower;

            public void Apply(float weight)
            {
                Visual.Upper.rotation=Quaternion.Slerp(Visual.Upper.rotation,Upper.rotation,weight);
                Visual.Lower.rotation=Quaternion.Slerp(Visual.Lower.rotation,Lower.rotation,weight);
            }
        }

        Transform blastPhysics;
        Rigidbody blastAnchor, blastTorso;
        readonly BlastLimb[] blastLimbs=new BlastLimb[4];
        uint blastSequence;
        bool blastActive;

        internal bool HasBlastPose => blastActive;

        void FixedUpdate()
        {
            if(blastActive && diver && blastAnchor)blastAnchor.MovePosition(diver.transform.position);
        }

        bool ApplyBlastPose()
        {
            var pulse=diver.ObservedBlast;
            float age=(float)(diver.BlastClock-pulse.StartedAt);
            if(pulse.Sequence!=blastSequence)
            {
                blastSequence=pulse.Sequence;
                EndBlastPose();
                if(pulse.Sequence!=0 && age<DiverMotor.BlastPoseDuration && age>-.2f)BeginBlastPose(pulse.Velocity);
            }
            if(!blastActive)return false;
            if(age>=DiverMotor.BlastPoseDuration){EndBlastPose();return false;}
            float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.76f,DiverMotor.BlastPoseDuration,age));
            body.SetPositionAndRotation(Vector3.Lerp(body.position,blastTorso.position,weight),
                Quaternion.Slerp(body.rotation,blastTorso.rotation,weight));
            foreach(var limb in blastLimbs)limb.Apply(weight);
            return weight>.25f;
        }

        void BeginBlastPose(Vector3 velocity)
        {
            blastPhysics=new GameObject("Diver · blast ragdoll").transform;
            if(gameObject.scene.IsValid())
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(blastPhysics.gameObject,gameObject.scene);
            blastAnchor=RagdollBody("Replicated body anchor",transform.position,Quaternion.identity,1,Vector3.zero);
            blastAnchor.isKinematic=true;
            // The replicated capsule resolves travel and collisions. The torso and limbs simulate the pose locally.
            blastTorso=RagdollBody("Torso",body.position,body.rotation,6,Vector3.up*.27f);
            Constrain(blastTorso,blastAnchor,100,65,100);
            Vector3 tumble=Vector3.Cross(Vector3.up,velocity.normalized);
            blastTorso.AddTorque(tumble*Mathf.Clamp(velocity.magnitude*.19f,1,5.3f),ForceMode.VelocityChange);
            for(int i=0;i<2;i++)
            {
                blastLimbs[i]=MakeBlastLimb(arms[i],false,i,velocity);
                blastLimbs[i+2]=MakeBlastLimb(legs[i],true,i,velocity);
            }
            blastActive=true;
        }

        BlastLimb MakeBlastLimb(Limb limb,bool leg,int side,Vector3 velocity)
        {
            var upper=RagdollBody(leg?"Thigh":"Upper arm",limb.Upper.position,limb.Upper.rotation,leg?1.55f:.9f,
                limb.Lower.localPosition*.5f);
            Constrain(upper,blastTorso,leg?70:95,48,leg?43:80);
            var lower=RagdollBody(leg?"Shin":"Forearm",limb.Lower.position,limb.Lower.rotation,leg?.9f:.52f,
                limb.End.localPosition*.48f);
            Constrain(lower,upper,leg?76:90,16,21);
            float s=side==0?-1:1;
            Vector3 outward=transform.right*s*(leg?.23f:.53f);
            Vector3 drag=-velocity.normalized;
            float kick=Mathf.Clamp(velocity.magnitude*.28f,.9f,6);
            upper.AddForce((outward+drag*.58f+Vector3.up*(leg?.09f:.42f))*kick,ForceMode.VelocityChange);
            lower.AddForce((outward+drag)*kick,ForceMode.VelocityChange);
            return new BlastLimb{Visual=limb,Upper=upper,Lower=lower};
        }

        Rigidbody RagdollBody(string label,Vector3 position,Quaternion rotation,float mass,Vector3 center)
        {
            var joint=new GameObject(label);
            joint.transform.SetParent(blastPhysics,false);
            joint.transform.SetPositionAndRotation(position,rotation);
            var rigid=joint.AddComponent<Rigidbody>();
            rigid.mass=mass;
            rigid.centerOfMass=center;
            rigid.inertiaTensor=Vector3.one*(mass*.033f);
            rigid.inertiaTensorRotation=Quaternion.identity;
            rigid.useGravity=false;
            rigid.linearDamping=1.55f;
            rigid.angularDamping=1.7f;
            rigid.maxAngularVelocity=13;
            rigid.solverIterations=12;
            return rigid;
        }

        static void Constrain(Rigidbody child,Rigidbody parent,float pitch,float yaw,float roll)
        {
            var joint=child.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody=parent;
            joint.xMotion=ConfigurableJointMotion.Locked;
            joint.yMotion=ConfigurableJointMotion.Locked;
            joint.zMotion=ConfigurableJointMotion.Locked;
            joint.angularXMotion=ConfigurableJointMotion.Limited;
            joint.angularYMotion=ConfigurableJointMotion.Limited;
            joint.angularZMotion=ConfigurableJointMotion.Limited;
            joint.lowAngularXLimit=new SoftJointLimit{limit=-pitch};
            joint.highAngularXLimit=new SoftJointLimit{limit=pitch};
            joint.angularYLimit=new SoftJointLimit{limit=yaw};
            joint.angularZLimit=new SoftJointLimit{limit=roll};
            joint.projectionMode=JointProjectionMode.PositionAndRotation;
            joint.projectionDistance=.035f;
            joint.projectionAngle=14;
        }

        void EndBlastPose()
        {
            if(!blastActive)return;
            blastActive=false;
            if(blastPhysics)Destroy(blastPhysics.gameObject);
        }
    }
}
