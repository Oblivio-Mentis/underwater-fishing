using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static class TransitClearanceChecks
    {
        public static List<string> Pipe(NereidPipeRoute pipe)
        {
            var issues=new HashSet<string>();
            var entry=pipe.line==PipeLine.Inlet?PipePort.Receiver:pipe.line==PipeLine.Feed?PipePort.ReservoirFeedOutlet:PipePort.ReservoirProcessOutlet;
            var exit=pipe.line==PipeLine.Inlet?PipePort.ReservoirInlet:pipe.line==PipeLine.Feed?PipePort.Parasite:PipePort.Processor;
            Socket(pipe.source,entry,issues);Socket(pipe.destination,exit,issues);
            if(pipe.knots.Count<2)issues.Add("Place at least two knots.");
            if(!pipe.sightGlass || !pipe.metal || !pipe.enamel) issues.Add("A surface material is missing.");
            if(pipe.Length<.001f){issues.Add("The line has no length.");return new List<string>(issues);}
            Physics.SyncTransforms();
            var walks=Object.FindObjectsByType<NereidTransitClearance>();
            var scale=pipe.transform.lossyScale;
            float radius=pipe.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            int count=Mathf.Clamp(Mathf.CeilToInt(pipe.Length*Mathf.Max(scale.x,scale.z)/Mathf.Max(.15f,radius)),8,512);
            var last=pipe.WorldPoint(0);
            for(int i=1;i<=count;i++)
            {
                var current=pipe.WorldPoint((float)i/count);
                foreach(var c in Physics.OverlapCapsule(last,current,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    if(c.gameObject.scene!=pipe.gameObject.scene || c.transform.IsChildOf(pipe.transform) || MobileActor(c))continue;
                    if(AtSocket(c,pipe.source,current,radius) || AtSocket(c,pipe.destination,current,radius))continue;
                    issues.Add("Line crosses collider · "+ColliderName(c));
                }
                foreach(var walk in walks)
                    if(walk.gameObject.scene==pipe.gameObject.scene && walk.Intersects(current,Vector3.one*radius)) issues.Add("Line enters a crew passage · "+walk.name);
                last=current;
            }
            return new List<string>(issues);
        }

        static bool MobileActor(Collider c) => c.GetComponentInParent<FishActor>() || c.GetComponentInParent<DiverMotor>() || c.GetComponentInParent<LootActor>();

        static string ColliderName(Collider c) => c.transform.parent ? c.transform.parent.name+"/"+c.name : c.name;

        static void Socket(PipeSocket socket,PipePort expected,HashSet<string> issues)
        {
            if(!socket){issues.Add("Missing socket · "+expected);return;}
            if(socket.port!=expected)issues.Add("Expected "+expected+", linked to "+socket.port+".");
            if(!socket.Fits)issues.Add("Socket lies outside its fitting · "+socket.name);
        }

        static bool AtSocket(Collider c,PipeSocket s,Vector3 position,float radius) =>
            s && s.fitting && c.transform.IsChildOf(s.fitting) && (s.transform.position-position).sqrMagnitude<radius*radius*9;

        public static List<string> Cargo(NereidCargoRail rail)
        {
            var issues=new HashSet<string>();
            if(!rail.Ready){issues.Add("Assign the hatch and at least two complete stops.");return new List<string>(issues);}
            if(!rail.leftDoor || !rail.rightDoor)issues.Add("A pressure door is missing.");
            float roomX=rail.hatch.TransformVector(Vector3.right*rail.aperture.x).magnitude;
            float roomZ=rail.hatch.TransformVector(Vector3.forward*rail.aperture.y).magnitude;
            if(roomX < rail.cageDimensionsMetres.x || roomZ < rail.cageDimensionsMetres.z) issues.Add("The cage exceeds the hatch aperture.");
            Physics.SyncTransforms();
            var walks=Object.FindObjectsByType<NereidTransitClearance>();
            var world=Object.FindAnyObjectByType<RaidWorld>();
            var half=rail.cageDimensionsMetres*.5f;
            int count=Mathf.Clamp(Mathf.CeilToInt(rail.Length()/.45f),8,1024);
            for(int i=0;i<=count;i++)
            {
                var at=rail.Point((float)i/count)+rail.transform.rotation*rail.cageCentreMetres;
                if(rail.davit && !rail.davit.CanReach(rail.TrolleyPoint(rail.Point((float)i/count))))
                    issues.Add("The deck crane cannot reach this recovery path. Move the pedestal or revise the route.");
                var local=rail.hatch.InverseTransformPoint(at);
                if(Mathf.Abs(local.y)*Mathf.Abs(rail.hatch.lossyScale.y)<half.y && Mathf.Abs(local.x)<rail.aperture.x && Mathf.Abs(local.z)<rail.aperture.y)
                {
                    if(Mathf.Abs(local.x)*rail.hatch.lossyScale.x+half.x>roomX*.5f || Mathf.Abs(local.z)*rail.hatch.lossyScale.z+half.z>roomZ*.5f)
                        issues.Add("The cage clips the coaming at roof entry.");
                }
                // The terrain can be one mesh for an entire biome. Test the actual volume
                // above the landing feet rather than that terrain collider's global peak.
                const float footTolerance=.075f;
                var aboveFeet=i==0?Physics.OverlapBox(at+rail.transform.up*(footTolerance*.5f),
                    half-Vector3.up*(footTolerance*.5f),rail.transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore):null;
                foreach(var c in Physics.OverlapBox(at,half,rail.transform.rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                {
                    if(c.gameObject.scene!=rail.gameObject.scene || MobileActor(c) || c.transform.IsChildOf(rail.transform) ||
                       (world && world.container && c.transform.IsChildOf(world.container)) || c.transform.IsChildOf(rail.hatch))continue;
                    // The landing feet bear on the seabed at the first stop.
                    if(i==0 && System.Array.IndexOf(aboveFeet,c)<0)continue;
                    issues.Add("Cargo clearance intersects · "+ColliderName(c));
                }
                foreach(var walk in walks)
                    if(walk.gameObject.scene==rail.gameObject.scene && walk.Intersects(at,half)) issues.Add("Cargo enters a crew passage · "+walk.name);
            }
            return new List<string>(issues);
        }
    }
}

