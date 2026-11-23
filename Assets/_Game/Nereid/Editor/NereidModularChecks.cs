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
            var decks=Object.FindAnyObjectByType<NereidDeckNavigation>();
            Check(decks && decks.reservoirGlass && decks.reservoirGlass.enabled,"The reservoir floor has solid physical glass.");
            Check(decks.crewSpawns.All(s=>s && layout.ContainsSealed(s.position)),"Every crew spawn is in a dry compartment.");
            var hall=layout.Socket(NereidRoomRole.Reservoir);
            var rail=Object.FindAnyObjectByType<NereidCargoRail>();
            Check(rail && rail.Ready && rail.aquarium && !layout.ContainsSealed(rail.aquarium.position),"The aquarium below the floor is wet.");
            Check((rail.Point(0)-rail.Point(1)).sqrMagnitude>25,"The hoist connects distinct loading and unloading points.");
            var lines=Object.FindObjectsByType<NereidPipeRoute>();
            Check(lines.Length==3 && lines.Select(p=>p.line).Distinct().Count()==3,"Three uniquely assigned pipelines.");
            foreach(var line in lines)
            {
                Check(line.IsConnected && line.Length>1 && Vector3.Distance(line.WorldPoint(1),line.destination.transform.position)<.03f,
                    line.line+" follows its destination socket.");
            }
            var instruments=layout.GetComponentsInChildren<NereidInstrument>();
            Check(instruments.Length==6 && Object.FindObjectsByType<ShipConsole>().Length==6,"Six active consoles without overlapping legacy stations.");
            Physics.SyncTransforms();
            foreach(var instrument in instruments)
            {
                var console=instrument.console; var face=console.screenAnchor;
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
                var monitor=copy.GetComponentsInChildren<NereidInstrument>().First(s=>s.console.kind==ShipConsoleKind.Reservoir);
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
                monitor=Object.FindObjectsByType<NereidInstrument>().First(s=>s.console.kind==ShipConsoleKind.Reservoir);
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
