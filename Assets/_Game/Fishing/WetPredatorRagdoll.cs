using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    // Presentation-only articulated bodies. Water drag and joint limits need no world colliders.
    sealed class WetPredatorRagdoll
    {
        static readonly int Glow=Shader.PropertyToID("_Glow");
        static MaterialPropertyBlock unlitEyes;

        sealed class Joint
        {
            readonly Transform bone;
            readonly Quaternion rest;
            readonly Vector3 lever;
            readonly float stiffness, limit;
            Vector3 bend, speed;

            internal Joint(Transform bone, Vector3 lever, float stiffness, float limit)
            {
                this.bone=bone;
                this.lever=lever;
                this.stiffness=stiffness;
                this.limit=limit;
                rest=bone.localRotation;
            }

            internal void Step(Vector3 force, float dt)
            {
                var axes=bone.parent.rotation*rest;
                var torque=Vector3.Cross(lever,Quaternion.Inverse(axes)*force);
                speed+=(torque*210f-bend*stiffness)*dt;
                speed*=Mathf.Exp(-4.7f*dt);
                var next=bend+speed*dt;
                bend=Vector3.ClampMagnitude(next,limit);
                if(next.sqrMagnitude>limit*limit)speed=Vector3.ProjectOnPlane(speed,bend.normalized)*.65f;
                bone.localRotation=rest*Quaternion.Euler(bend);
            }
        }

        readonly List<Joint> joints=new();
        readonly Transform body;
        readonly float phase, side;
        Transform volume;
        Vector3 velocity, angularVelocity, lastPosition, measuredVelocity;
        bool positioned;

        internal WetPredatorRagdoll(Transform model, float seed)
        {
            body=model;
            phase=seed*2*Mathf.PI;
            side=seed>=.5f?1:-1;
            var tail=model.Find("Tail");
            Add(tail,Vector3.back,16,37);
            if(tail)Add(tail.Find("TailTip"),Vector3.back,12,42);
            Add(model.Find("Jaw"),Vector3.forward,31,18);
            AddFin(model.Find("FinLeft"),-1);
            AddFin(model.Find("FinRight"),1);

            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=materials[i];
                    if(!material || !material.HasProperty(Glow) || material.GetFloat(Glow)<=0)continue;
                    unlitEyes??=new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(unlitEyes,i);
                    unlitEyes.SetFloat(Glow,0);
                    renderer.SetPropertyBlock(unlitEyes,i);
                }
            }
        }

        void AddFin(Transform fin, int side)
        {
            if(!fin)return;
            Add(fin,new Vector3(side,0,-.3f),20,30);
            for(int i=0;i<fin.childCount;i++)
                Add(fin.GetChild(i),new Vector3(side*.65f,-.15f,-.7f),13+i*2,38);
        }

        void Add(Transform bone, Vector3 lever, float stiffness, float limit)
        {
            if(bone)joints.Add(new Joint(bone,lever.normalized,stiffness,limit));
        }

        internal void Drift(Transform frame, Vector3 berth, Vector3 low, Vector3 high,
            float time, float dt, bool arriving)
        {
            var resting=Quaternion.Euler(10+17*Mathf.Sin(phase),phase*Mathf.Rad2Deg,side*88f);
            if(volume!=frame)
            {
                volume=frame;
                velocity=arriving?frame.InverseTransformVector(Vector3.ClampMagnitude(measuredVelocity,2.3f)):Vector3.zero;
                angularVelocity=arriving?new Vector3(26,side*22,side*32):Vector3.zero;
                if(!arriving)body.SetLocalPositionAndRotation(berth+Vector3.up*.37f,resting);
            }

            var position=body.localPosition;
            var current=new Vector3(Mathf.Sin(time*.93f+phase),.23f*Mathf.Sin(time*1.52f-phase),
                Mathf.Cos(time*.74f-phase));
            // The body descends to its low berth. Turbulence keeps the tail and fins moving.
            velocity+=((berth-position)*2.2f+current*.52f-Vector3.up*.67f-velocity*2.1f)*dt;
            position+=velocity*dt;
            if(KeepInside(ref position.x,low.x,high.x,ref velocity.x) |
                KeepInside(ref position.y,low.y,high.y,ref velocity.y) |
                KeepInside(ref position.z,low.z,high.z,ref velocity.z))
                angularVelocity+=new Vector3(-velocity.z,velocity.x,velocity.y*side)*12f;

            var righting=Quaternion.Inverse(body.localRotation)*resting;
            righting.ToAngleAxis(out float angle,out var axis);
            if(angle>180f)angle-=360f;
            angularVelocity+=(axis*(angle*2.8f)+current*27f-angularVelocity*2.3f)*dt;
            body.SetLocalPositionAndRotation(position,body.localRotation*Quaternion.Euler(angularVelocity*dt));
            PoseBody(frame.TransformVector(current*.22f),dt);
        }

        static bool KeepInside(ref float position, float low, float high, ref float speed)
        {
            if(position>=low && position<=high)return false;
            position=Mathf.Clamp(position,low,high);
            speed*=-.16f;
            return true;
        }

        internal void InTransit(float dt)
        {
            volume=null;
            PoseBody(Vector3.zero,dt);
        }

        void PoseBody(Vector3 current, float dt)
        {
            if(dt<=0)return;
            var position=body.position;
            var motion=positioned?Vector3.ClampMagnitude((position-lastPosition)/dt,5):Vector3.zero;
            var acceleration=(motion-measuredVelocity)*4.2f;
            measuredVelocity=Vector3.Lerp(measuredVelocity,motion,dt*4.2f);
            lastPosition=position;
            positioned=true;
            var load=Vector3.down*.77f+current-Vector3.ClampMagnitude(acceleration,3.3f)*.44f-measuredVelocity*.18f;
            foreach(var joint in joints)joint.Step(load,dt);
        }
    }
}
