using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class CargoRenderedInspection
    {
        // Pose the authored meshes directly and restore the exact editor state, including custom deck edits.
        public static List<string> Check(NereidCargoRail rail)
        {
            var issues=new List<string>();
            if(!rail || !rail.Ready || !rail.cageAnchor || !rail.leftDoor || !rail.rightDoor){issues.Add("Incomplete cargo rig.");return issues;}
            var cage=rail.cageAnchor.GetComponentInChildren<CargoBasket>();
            if(!cage){issues.Add("Missing physical cage.");return issues;}
            var saved=new Dictionary<Transform,(Vector3 p,Quaternion q,Vector3 s)>();
            void Save(Transform t){if(t)saved[t]=(t.localPosition,t.localRotation,t.localScale);}
            Save(rail.cageAnchor);Save(rail.leftDoor);Save(rail.rightDoor);
            foreach(var t in cage.GetComponentsInChildren<Transform>(true))Save(t);
            if(rail.davit)foreach(var t in rail.davit.GetComponentsInChildren<Transform>(true))Save(t);
            try
            {
                // Batch scene validation runs before ExecuteAlways.Update constructs the live pipe skin.
                foreach(var pipe in rail.GetComponentsInChildren<NereidPipeRoute>())
                    if(!pipe.GetComponent<MeshFilter>().sharedMesh)pipe.Rebuild();
                var inspector=new CargoMeshClearance(rail);
                bool lateGate=false;
                int steps=Mathf.Max(160,Mathf.CeilToInt(rail.Length()/.18f));
                foreach(var phase in new[]{HoistPhase.Raising,HoistPhase.Unloading,HoistPhase.Returning})
                {
                    int count=phase==HoistPhase.Unloading?36:steps;
                    for(int i=0;i<=count;i++)
                    {
                        float t=(float)i/count,f=NereidCargoRail.TravelFraction(phase,t);
                        var position=rail.Point(f);
                        rail.cageAnchor.position=position;
                        float open=rail.DoorAmount(position);
                        rail.leftDoor.localPosition=rail.DoorPosition(true,open);
                        rail.rightDoor.localPosition=rail.DoorPosition(false,open);
                        cage.PoseMechanism(phase,t);
                        if(rail.davit)rail.davit.Present(rail.davit.HeadTarget(rail.TrolleyPoint(cage.CableSocket.position),phase,t),cage.CableSocket.position,null);
                        if(!lateGate && open<1f && CargoMeshClearance.OccupiesAperture(rail,inspector.VisibleCargoBounds(),.65f,out _))
                        {issues.Add("The receiver leaves must clear the first cargo edge by at least 0.65 m and hold until the last edge leaves.");lateGate=true;}
                        inspector.Sample($"{phase} {t:F3} ({rail.transform.InverseTransformPoint(position):F2})");
                    }
                }
                issues.AddRange(inspector.contacts);issues.AddRange(inspector.unreadable);
                if(inspector.CargoMeshes==0 || inspector.StructuralMeshes==0)issues.Add("No visible cargo or ship surfaces.");
            }
            finally
            {
                foreach(var item in saved){var t=item.Key;if(!t)continue;t.localPosition=item.Value.p;t.localRotation=item.Value.q;t.localScale=item.Value.s;}
            }
            return issues;
        }
    }
}
