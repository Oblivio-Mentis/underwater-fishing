using System;
using UnityEngine;

namespace Raid.Nereid
{
    // The server's recovery rail positions the basket. The deck crane follows that published
    // load with two rigid links, rotating about their real trunnions like a marine knuckle boom.
    public sealed class NereidCargoDavit : MonoBehaviour
    {
        [Serializable] public struct Cylinder
        {
            public Transform foot, pin, barrel, rod;
        }

        public Transform bearing, innerBoom, flyJib, head, drum, sheave;
        [Min(.1f)] public float innerLength=6.7f, outerLength=6.0f;
        [Min(.01f)] public float drumRadius=.39f, sheaveRadius=.30f;
        public Cylinder[] cylinders=Array.Empty<Cylinder>();
        public Transform[] fairleads=Array.Empty<Transform>();

        public bool CanReach(Vector3 target)
        {
            var arm=transform.InverseTransformPoint(target)-bearing.localPosition;
            return arm.magnitude<=innerLength+outerLength-.015f && arm.magnitude>=Mathf.Abs(innerLength-outerLength)+.015f;
        }

        static Quaternion Hinge(Vector3 span) => Quaternion.AngleAxis(-Mathf.Atan2(span.y,span.z)*Mathf.Rad2Deg,Vector3.right);

        public void Present(Vector3 target,Vector3 cableEye,LineRenderer wire)
        {
            var aim=transform.InverseTransformPoint(target)-bearing.localPosition;
            float reach=new Vector2(aim.x,aim.z).magnitude;
            var axis=new Vector3(0,aim.y,reach);
            float distance=Mathf.Clamp(axis.magnitude,Mathf.Abs(innerLength-outerLength)+.001f,innerLength+outerLength-.001f);
            axis=axis.normalized;
            float chord=(distance*distance+innerLength*innerLength-outerLength*outerLength)/(2*distance);
            float rise=Mathf.Sqrt(Mathf.Max(0,innerLength*innerLength-chord*chord));
            Vector3 knuckle=axis*chord+new Vector3(0,axis.z,-axis.y)*rise;
            bearing.localRotation=Quaternion.Euler(0,Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg,0);
            innerBoom.localRotation=Hinge(knuckle);
            flyJib.localPosition=knuckle;
            flyJib.localRotation=Hinge(axis*distance-knuckle);
            head.position=flyJib.TransformPoint(Vector3.forward*outerLength);
            head.rotation=bearing.rotation;
            foreach(var cylinder in cylinders)
            {
                Vector3 span=cylinder.pin.position-cylinder.foot.position;
                if(span.sqrMagnitude<.000001f)continue;
                var direction=Quaternion.LookRotation(span,Mathf.Abs(Vector3.Dot(span.normalized,transform.up))>.98f?bearing.forward:transform.up);
                cylinder.barrel.SetPositionAndRotation(cylinder.foot.position,direction);
                cylinder.rod.SetPositionAndRotation(cylinder.pin.position,direction);
                // The polished rod runs back inside its sleeve as the joint retracts.
                float length=transform.InverseTransformVector(span).magnitude;
                cylinder.rod.localScale=new Vector3(1,1,Mathf.Max(.03f,length-cylinder.barrel.localScale.z+.045f));
            }
            float paid=0;
            Vector3 previous=fairleads[0].position;
            if(wire)wire.positionCount=fairleads.Length+1;
            for(int i=0;i<fairleads.Length;i++)
            {
                Vector3 p=fairleads[i].position;paid+=Vector3.Distance(previous,p);previous=p;
                if(wire)wire.SetPosition(i,p);
            }
            paid+=Vector3.Distance(previous,cableEye);
            if(wire)wire.SetPosition(fairleads.Length,cableEye);
            float localLength=paid/Mathf.Max(.001f,transform.lossyScale.y);
            drum.localRotation=Quaternion.AngleAxis(-localLength/drumRadius*Mathf.Rad2Deg,Vector3.right);
            sheave.localRotation=Quaternion.AngleAxis(-localLength/sheaveRadius*Mathf.Rad2Deg,Vector3.right);
        }
    }
}
