using System;
using UnityEngine;

namespace Raid.Nereid
{
    [Serializable]
    public struct RailStop
    {
        public string label;
        public Transform marker;
    }

    [ExecuteAlways]
    public sealed class NereidCargoRail : MonoBehaviour
    {
        public RailStop[] stops = Array.Empty<RailStop>();
        public Transform hatch, leftDoor, rightDoor, traveller, cageAnchor;
        public ProgressionTuning timings;
        public NereidCargoDavit davit;
        public Transform[] trackBeams = Array.Empty<Transform>();
        public Transform[] trackSupports = Array.Empty<Transform>();
        public Transform[] wheels = Array.Empty<Transform>();
        public Renderer[] signalLights = Array.Empty<Renderer>();
        public Vector2 aperture = new(4.8f,4.8f);
        public Vector3 cageCentreMetres = new(.12f,.38f,0);
        public Vector3 cageDimensionsMetres = new(4.2f,2.97f,3.7f);
        [Tooltip("Distance in metres between the leading cage edge and the hatch opening where the shutters start moving.")]
        [Min(.1f)] public float hatchWarningDistance=13f;
        [Tooltip("The aperture is fully open while any part of the cargo footprint is inside this approach margin in metres.")]
        [Min(0)] public float hatchHoldDistance=2.4f;
        [Min(.2f)] public float craneAboveHatch = 2.4f;
        [Min(.01f)] public float beamWidth = .15f;
        public Vector3 leftClosed, rightClosed;
        public NereidPipeRoute inlet;
        public Transform aquarium;
        [Tooltip("Water enclosed by the reservoir walls, in aquarium socket coordinates.")]
        public Bounds aquariumVolume = new(Vector3.zero,new Vector3(4.84f,2.11f,9.40f));
        public bool InsideAquarium(Vector3 worldPoint) => aquarium && aquariumVolume.Contains(aquarium.InverseTransformPoint(worldPoint));
        public Vector3 aquariumSwimRadii = new(2f,.75f,4f);
        public bool Ready
        {
            get
            {
                if(!hatch || stops.Length < 2) return false;
                foreach(var stop in stops) if(!stop.marker) return false;
                return true;
            }
        }

        MaterialPropertyBlock block;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        public Vector3 Point(float fraction)
        {
            if (!Ready) return transform.position;
            float remaining = Mathf.Clamp01(fraction)*Length();
            for (int i=1;i<stops.Length;i++)
            {
                Vector3 a=stops[i-1].marker.position, b=stops[i].marker.position;
                float length=Vector3.Distance(a,b);
                if (length>.001f && remaining<=length) return Vector3.Lerp(a,b,remaining/length);
                remaining-=length;
            }
            return stops[^1].marker.position;
        }

        public float Length()
        {
            float length=0;
            for(int i=1;i<stops.Length;i++)
                if(stops[i-1].marker && stops[i].marker) length+=Vector3.Distance(stops[i-1].marker.position,stops[i].marker.position);
            return length;
        }

        public static float TravelFraction(HoistPhase phase,float progress) => phase switch
        {
            HoistPhase.Raising=>Mathf.InverseLerp(.15f,.96f,progress),
            HoistPhase.Unloading=>1,
            HoistPhase.Returning=>1-Mathf.InverseLerp(.07f,.88f,progress),
            _=>0
        };

        public Vector3 TrolleyPoint(Vector3 cage)
        {
            Vector3 local=hatch.InverseTransformPoint(cage);
            local.y=craneAboveHatch;
            return hatch.TransformPoint(local);
        }

        public float DoorAmount(Vector3 cage)
        {
            // Use metres in hatch space: the first corner of a wide load triggers the lock.
            // Both shutters and the visible cage use the same interpolated pose on each peer.
            var centre=cage+transform.rotation*cageCentreMetres-hatch.position;
            var half=cageDimensionsMetres*.5f;
            float Gap(Vector3 axis,float opening)
            {
                float loadExtent=half.x*Mathf.Abs(Vector3.Dot(axis,transform.right))+
                    half.y*Mathf.Abs(Vector3.Dot(axis,transform.up))+half.z*Mathf.Abs(Vector3.Dot(axis,transform.forward));
                return Mathf.Abs(Vector3.Dot(centre,axis))-opening*.5f-loadExtent;
            }
            float fore=Gap(hatch.forward,hatch.TransformVector(Vector3.forward*aperture.y).magnitude);
            float beam=Gap(hatch.right,hatch.TransformVector(Vector3.right*aperture.x).magnitude);
            float horizontal=Mathf.Max(fore,beam);
            float height=Gap(hatch.up,0);
            float approach=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(hatchHoldDistance,Mathf.Max(hatchHoldDistance+.1f,hatchWarningDistance),horizontal));
            float vertical=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,10f,height));
            return approach*vertical;
        }

        public Vector3 DoorPosition(bool left,float open) =>
            (left?leftClosed:rightClosed)+Vector3.right*((left?-1:1)*aperture.x*.51f*open);

        public void Present(float fraction, HoistPhase phase, float progress)
        {
            if(!Ready) return;
            Vector3 at=Point(fraction);
            float opening=DoorAmount(at);
            if(leftDoor) leftDoor.localPosition=DoorPosition(true,opening);
            if(rightDoor) rightDoor.localPosition=DoorPosition(false,opening);
            if(traveller) traveller.position=TrolleyPoint(at);
            foreach(var wheel in wheels)
                if(wheel) wheel.localRotation=Quaternion.Euler(fraction*Length()*150f,0,90);
            float flow=phase==HoistPhase.Unloading ? Mathf.Sin(Mathf.PI*progress) : 0;
            if(inlet) inlet.SetFlow(flow);
            block??=new MaterialPropertyBlock();
            Color signal=opening>=.998f ? new Color(.12f,.82f,.53f) :
                opening<=.001f ? new Color(.08f,.23f,.27f) :
                Color.Lerp(new Color(1,.27f,.025f),new Color(1,.65f,.13f),.5f+.5f*Mathf.Sin(Time.time*7));
            foreach(var lamp in signalLights)
                if(lamp){lamp.GetPropertyBlock(block);block.SetColor(BaseColor,signal);block.SetColor(Emission,signal*2.4f);lamp.SetPropertyBlock(block);}
        }

        void Update()
        {
            if(!Ready) return;
            for(int i=0;i+1<stops.Length && i<trackBeams.Length;i++)
            {
                if(!trackBeams[i])continue;
                var beam=trackBeams[i];
                var a=transform.InverseTransformPoint(TrolleyPoint(stops[i].marker.position));
                var b=transform.InverseTransformPoint(TrolleyPoint(stops[i+1].marker.position));
                bool span=(b-a).sqrMagnitude>.0025f;
                if(beam.gameObject.activeSelf!=span)beam.gameObject.SetActive(span);
                if(i<trackSupports.Length && trackSupports[i] && trackSupports[i].gameObject.activeSelf!=span)
                    trackSupports[i].gameObject.SetActive(span);
                if(!span) continue;
                // The beam roots are direct children of the rail, with +Z along their web.
                beam.localPosition=(a+b)*.5f+Vector3.up*.18f;
                beam.localRotation=Quaternion.LookRotation(b-a,Vector3.up);
                beam.localScale=new Vector3(beamWidth,.25f,(b-a).magnitude);
                if(i<trackSupports.Length && trackSupports[i])
                {
                    var support=trackSupports[i];
                    support.localPosition=beam.localPosition+Vector3.up*.16f;
                    support.localRotation=beam.localRotation;
                    support.localScale=new Vector3(beamWidth*2.7f,.09f,(b-a).magnitude);
                }
            }
        }
    }
}

