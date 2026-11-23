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

        [Header("Park and recovery")]
        [Tooltip("Park the jib outboard of the boarding collar while the cage is on the seabed.")]
        [Range(-65, 65)] public float parkedSlew = -35f;
        [Range(.5f, 1f)] public float parkedReach = .97f;
        [Header("Work lights")]
        public Light workFlood, deckWash;
        public Renderer floodGlass, portGlass, starboardGlass, warningLens;
        [Min(0)] public float floodIntensity = 420f, washIntensity = 64f;
        MaterialPropertyBlock glassBlock;
        float lastPower = -1f, lastFlash = -1f;
        static readonly int LensColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Off = new(.033f, .052f, .056f);

        public Vector3 HeadTarget(Vector3 plumb, HoistPhase phase, float progress)
        {
            // Take up slack while the cage closes. Park only after its feet have landed.
            float deployed = phase switch
            {
                HoistPhase.Raising => Mathf.SmoothStep(0, 1, progress / .15f),
                HoistPhase.Returning => 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.88f, 1f, progress)),
                HoistPhase.Unloading => 1f,
                _ => 0f
            };
            if (deployed >= 1f) return plumb;
            Vector3 arm = transform.InverseTransformPoint(plumb) - bearing.localPosition;
            Vector3 horizontal = new Vector3(arm.x, 0, arm.z) * Mathf.Lerp(parkedReach, 1f, deployed);
            horizontal = Quaternion.AngleAxis(parkedSlew * (1f - deployed), Vector3.up) * horizontal;
            return transform.TransformPoint(bearing.localPosition + horizontal + Vector3.up * arm.y);
        }

        public void PresentCycle(Vector3 plumb, Vector3 cableEye, LineRenderer wire, HoistPhase phase, float progress)
        {
            Present(HeadTarget(plumb, phase, progress), cableEye, wire);
            float power = phase switch
            {
                HoistPhase.Raising => Mathf.SmoothStep(0, 1, progress / .045f),
                HoistPhase.Unloading => 1f,
                HoistPhase.Returning => 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.96f, 1f, progress)),
                _ => 0f
            };
            if (workFlood)
            {
                Vector3 aim = cableEye - workFlood.transform.position;
                if (aim.sqrMagnitude > .01f)
                    workFlood.transform.rotation = Quaternion.LookRotation(aim, bearing.forward);
                workFlood.intensity = floodIntensity * power;
                workFlood.enabled = power > .001f;
            }
            if (deckWash)
            {
                deckWash.intensity = washIntensity * power;
                deckWash.enabled = power > .001f;
            }
            float pulse = phase == HoistPhase.Unloading ? 1f :
                .18f + .82f * Mathf.Pow(.5f + .5f * Mathf.Cos(progress * Mathf.PI * 12f), 2);
            if (!Application.isPlaying || power != lastPower)
            {
                Lens(floodGlass, new Color(1.18f, 1.42f, 1.57f), power);
                Lens(portGlass, new Color(1.56f, 1.17f, .63f), power);
                Lens(starboardGlass, new Color(1.56f, 1.17f, .63f), power);
                lastPower = power;
            }
            float flash = pulse * power;
            if (!Application.isPlaying || Mathf.Abs(flash - lastFlash) > .001f || (flash == 0 && lastFlash != 0))
            {
                Lens(warningLens, new Color(2.35f, .76f, .09f), flash);
                lastFlash = flash;
            }
        }

        void Lens(Renderer glass, Color lit, float power)
        {
            if (!glass) return;
            glassBlock ??= new MaterialPropertyBlock();
            glass.GetPropertyBlock(glassBlock);
            glassBlock.SetColor(LensColor, Color.Lerp(Off, lit, power));
            glass.SetPropertyBlock(glassBlock);
        }

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
