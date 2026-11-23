using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class NereidModularChecks
    {
        const string ScenePath = "Assets/_Game/Scenes/InterfaceVerticalSlice.unity";

        public static void PrepareCandidate()
        {
            TransitFitAuthoring.ApplyToMain();
            NereidOperationsAuthoring.Install();
            Run();
        }

        [MenuItem("Tools/NEREID/Validate expedition")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save your scene and stop Play Mode before validating.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var layout=Object.FindAnyObjectByType<NereidShipLayout>();
            Check(layout && PrefabUtility.IsPartOfPrefabInstance(layout),"The modular vessel is a connected prefab.");
            var launcher=Object.FindAnyObjectByType<HarpoonPresentation>();
            Check(launcher && launcher.weaponAppearance && launcher.weaponAppearance.Ready &&
                Vector3.Distance(launcher.weaponAppearance.boltTipLocal,launcher.weaponAppearance.boltNockLocal)>.07f,
                "First-person and world displays are bound to a prefab with a detachable spear and cable drum.");
            var exhibit=layout.GetComponentsInChildren<Transform>().Single(t=>t.name=="Expedition equipment displays");
            var suits=exhibit.GetComponentsInChildren<SkinnedMeshRenderer>();
            var launchers=exhibit.GetComponentsInChildren<HarpoonVisualRig>();
            Check(suits.Length>=4 && launchers.Length>=6,"Four crew suits and six expedition harpoons in the equipment room.");
            Check(suits.All(s=>s.forceMeshLod>0 && s.sharedMesh.lodCount>s.forceMeshLod &&
                  s.sharedMesh.GetIndexCount(0,s.forceMeshLod)<s.sharedMesh.GetIndexCount(0,0)),
                "The displayed suits use a simplified rigged mesh.");
            var paints=suits.SelectMany(s=>s.sharedMaterials).Concat(launchers.SelectMany(h=>h.GetComponentsInChildren<MeshRenderer>())
                .SelectMany(r=>r.sharedMaterials)).Distinct();
            Check(paints.All(m=>m && m.shader.name=="RAID/Faceted Expedition Equipment" && !m.HasProperty("_BaseMap")),
                "Suit and harpoon surfaces are painted by solid part colours.");
            var lockRig=layout.GetComponentInChildren<NereidPressureDoors>();
            Check(lockRig && lockRig.controls.Length==4 && lockRig.controls.All(p=>p && p.GetComponent<Collider>()) &&
                lockRig.controls.Select(p=>p.Id).Distinct().Count()==4 && lockRig.waterSurface &&
                lockRig.waterSurface.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="RAID/Pressure Lock Water",
                "Editable pressure lock has four reach controls and a water surface.");
            Check(new[]{lockRig.innerPort,lockRig.innerStarboard,lockRig.outerPort,lockRig.outerStarboard}.All(
                t=>t && t.GetComponent<BoxCollider>() && t.GetComponent<MeshFilter>().sharedMesh),
                "All four sliding leaves have a saved mesh and a physical seal.");
            var passages=layout.GetComponentsInChildren<NereidPassageDoor>();
            Check(passages.Length==2 && passages.Select(p=>p.passage).Distinct().Count()==2 &&
                  passages.All(p=>p.portLeaf && p.starboardLeaf && p.portSeal && p.starboardSeal &&
                                  p.portSeal.enabled && p.starboardSeal.enabled),
                "The equipment room and pump room each have a pair of solid automatic doors.");
            var decks=Object.FindAnyObjectByType<NereidDeckNavigation>();
            Check(decks && decks.reservoirGlass && decks.reservoirGlass.enabled,"The reservoir floor has solid physical glass.");
            Check(decks.crewSpawns.All(s=>s && layout.ContainsSealed(s.position)),"Every crew spawn is in a dry compartment.");
            var hall=layout.Socket(NereidRoomRole.Reservoir);
            var rail=Object.FindAnyObjectByType<NereidCargoRail>();
            Check(rail && rail.Ready && rail.aquarium && !layout.ContainsSealed(rail.aquarium.position),"The aquarium below the floor is wet.");
            Check((rail.Point(0)-rail.Point(1)).sqrMagnitude>25,"The hoist connects distinct loading and unloading points.");
            Check(rail.davit && rail.davit.bearing && rail.davit.innerBoom && rail.davit.flyJib && rail.davit.head &&
                  rail.davit.drum && rail.davit.sheave, "The cargo basket is served by the articulated deck crane.");
            var cargoClearance=TransitClearanceChecks.Cargo(rail);
            Check(cargoClearance.Count==0,"Cargo route clearance: "+string.Join("; ",cargoClearance));
            var lines=Object.FindObjectsByType<NereidPipeRoute>();
            Check(lines.Length==3 && lines.Select(p=>p.line).Distinct().Count()==3,"Three uniquely assigned pipelines.");
            foreach(var line in lines)
            {
                Check(line.IsConnected && line.Length>1 && Vector3.Distance(line.WorldPoint(1),line.destination.transform.position)<.03f,
                    line.line+" follows its destination socket.");
            }
            var instruments=layout.GetComponentsInChildren<NereidInstrument>();
            var stations=Object.FindObjectsByType<ShipConsole>();
            Check(instruments.Length==3 && instruments.All(i=>i.console && i.console.IsMonitor) &&
                  stations.Length==6 && stations.Select(s=>s.kind).Distinct().Count()==6,
                "Three crew displays: reservoir, navigation and upgrades. Three action keys are independent.");
            var discharge=stations.Single(s=>s.kind==ShipConsoleKind.Process);
            var feeding=stations.Single(s=>s.kind==ShipConsoleKind.Feed);
            var hallRoom=layout.Socket(NereidRoomRole.Reservoir).parent;
            var separation=Vector3.Distance(discharge.screenAnchor.position,feeding.screenAnchor.position);
            Check(discharge.transform.IsChildOf(hallRoom) && feeding.transform.IsChildOf(hallRoom) &&
                  separation>.15f && separation<2f && !discharge.GetComponentInParent<NereidInstrument>() &&
                  !feeding.GetComponentInParent<NereidInstrument>(),
                "The two physical switches are adjacent in the aquarium hall, independent of display housings.");
            var cargoKey=stations.Single(s=>s.kind==ShipConsoleKind.CargoControl);
            Check(cargoKey.dispatchButton && cargoKey.dispatchButton.hitFace && !cargoKey.GetComponent<NereidInstrument>(),
                "The internal hoist control has a pressable cap without a screen.");
            Physics.SyncTransforms();
            var centre=(discharge.screenAnchor.position+feeding.screenAnchor.position)*.5f;
            var operatorEye=centre-(discharge.screenAnchor.forward+feeding.screenAnchor.forward).normalized*1.25f;
            foreach(var line in new[]{discharge,feeding})
            {
                var direction=line.screenAnchor.position-operatorEye;
                Check(Physics.Raycast(operatorEye,direction,out var hit,direction.magnitude+.15f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) && hit.collider==line.dispatchButton.hitFace,
                    "Both keys are independently reachable from the same standing position: "+line.kind);
            }
            foreach(var console in stations)
            {
                var face=console.screenAnchor;
                var eye=face.position-face.forward*1.65f;
                Check(layout.ContainsSealed(eye),"Dry operator position at "+console.kind);
                bool hit=Physics.Raycast(eye,face.forward,out var contact,1.85f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                Check(hit && contact.collider.GetComponentInParent<ShipConsole>()==console,
                    "Reticle reaches "+console.kind+"; first collider "+(hit?contact.collider.name:"none"));
            }
            string temporary=AssetDatabase.GenerateUniqueAssetPath("Assets/_Game/Scenes/ModularEditCheck.unity");
            try
            {
                if(!EditorSceneManager.SaveScene(scene,temporary,true)) throw new IOException(temporary);
                var clone=EditorSceneManager.OpenScene(temporary);
                var copy=Object.FindAnyObjectByType<NereidShipLayout>();
                var monitor=copy.GetComponentsInChildren<NereidInstrument>().First(s=>s.console && s.console.kind==ShipConsoleKind.Reservoir);
                monitor.transform.localPosition+=Vector3.right*.19f;
                var p=Object.FindObjectsByType<NereidPipeRoute>().First(l=>l.line==PipeLine.Process);
                var knot=p.knots[1];knot.position+=new Vector3(.13f,.27f,-.22f);p.knots[1]=knot;p.Rebuild();
                var r=Object.FindAnyObjectByType<NereidCargoRail>();
                var marker=r.stops[0].marker;
                marker.localPosition+=Vector3.right*.32f;
                var berth=r.Point(0);var monitorPosition=monitor.transform.position;
                foreach(var o in new Object[]{monitor.transform,p,marker}) {
                    EditorUtility.SetDirty(o);
                    if(PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o);
                }
                EditorSceneManager.MarkSceneDirty(clone);EditorSceneManager.SaveScene(clone);
                EditorSceneManager.OpenScene(temporary);
                r=Object.FindAnyObjectByType<NereidCargoRail>();
                p=Object.FindObjectsByType<NereidPipeRoute>().First(l=>l.line==PipeLine.Process);
                monitor=Object.FindObjectsByType<NereidInstrument>().First(s=>s.console && s.console.kind==ShipConsoleKind.Reservoir);
                Check(Vector3.Distance(monitor.transform.position,monitorPosition)<.001f &&
                    Vector3.Distance(p.knots[1].position,knot.position)<.001f && Vector3.Distance(r.Point(0),berth)<.001f,
                    "Moved monitor, pipe bend and loading berth survived saving and reopening a scene.");
            }
            finally { EditorSceneManager.OpenScene(ScenePath);AssetDatabase.DeleteAsset(temporary); }
            Debug.Log("[NEREID MODULAR QA] PASSED");
        }

        static void Check(bool condition,string message)
        {
            if(!condition)throw new InvalidOperationException("[NEREID MODULAR QA] "+message);
            Debug.Log("[NEREID MODULAR QA] "+message);
        }
    }
}
