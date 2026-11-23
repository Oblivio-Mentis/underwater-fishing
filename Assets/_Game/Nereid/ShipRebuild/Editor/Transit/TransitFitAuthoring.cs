using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static class TransitFitAuthoring
    {
        [MenuItem("NEREID/Fit cargo clearances")]
        public static void FitScene()=>Fit(Object.FindAnyObjectByType<NereidVessel>());

        public static void ApplyToMain()
        {
            if(!Application.isBatchMode)throw new System.InvalidOperationException("Use NEREID/Fit cargo clearances in the open scene.");
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/InterfaceVerticalSlice.unity");
            var vessel=Object.FindAnyObjectByType<NereidVessel>();
            if(!vessel || !vessel.authoredRail)throw new System.InvalidOperationException("No installed NEREID recovery rail.");
            Fit(vessel);
            var issues=TransitClearanceChecks.Cargo(vessel.authoredRail);
            foreach(var pipe in vessel.authoredRail.GetComponentsInChildren<NereidPipeRoute>())
                issues.AddRange(TransitClearanceChecks.Pipe(pipe));
            if(issues.Count>0)throw new System.InvalidOperationException("Clearance meshing: "+string.Join(" | ",issues));
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("NEREID TRANSIT CLEAR · three pressure circuits and roof recovery sweep verified.");
        }

        internal static void Fit(NereidVessel vessel)
        {
            if(!vessel || !vessel.authoredRail || Application.isPlaying)return;
            var rail=vessel.authoredRail;
            foreach(var node in vessel.GetComponentsInChildren<Transform>(true))
                if(node.name=="ObservationHall"){DeckPenetrations.Install(node);break;}

            // Existing authored levels can be upgraded without moving a user's revised control points.
            // Only the identified factory-default path templates qualify for the conversion.
            foreach(var route in rail.GetComponentsInChildren<NereidPipeRoute>())
            {
                switch(route.line)
                {
                    case PipeLine.Inlet:
                        ReplaceDefaults(route,new[]{new Vector3(.1f,2.55f,-3),new(1.05f,2.23f,-3),new(2.96f,2.23f,-3),new(3.18f,1.66f,-3),new(3.18f,-.45f,-3)},
                            new[]{new Vector3(.1f,2.55f,-3),new(1.05f,2.23f,-3),new(2.83f,2.23f,-3),new(3.18f,1.55f,-3),new(3.18f,.60f,-3),new(3.18f,-.65f,-3),new(2.80f,-.82f,-3)});
                        break;
                    case PipeLine.Process:
                        ReplaceDefaults(route,new[]{new Vector3(-2.93f,-1.03f,-5.86f),new(-3.31f,-.60f,-6.09f),new(-3.31f,2.78f,-6.43f),new(-3.31f,2.88f,-9.64f),new(-2.55f,2.6f,-10.55f)},
                            new[]{new Vector3(-2.96f,-1.03f,-5.59f),new(-3.43f,-.78f,-5.57f),new(-3.43f,.91f,-5.57f),new(-3.35f,1.82f,-5.69f),new(-1.22f,2.05f,-7.18f),new(-1.22f,2.05f,-8.90f),new(-2.32f,2.58f,-10.29f)});
                        break;
                    case PipeLine.Feed:
                        ReplaceDefaults(route,new[]{new Vector3(3.12f,-.95f,1.91f),new(3.39f,-.50f,2.17f),new(3.39f,2.79f,2.69f),new(3.39f,2.89f,5.69f),new(2.38f,2.56f,6.34f),new(1.14f,1.97f,7.16f)},
                            new[]{new Vector3(2.95f,-.96f,1.65f),new(3.43f,-.76f,1.63f),new(3.43f,.88f,1.63f),new(3.43f,2.65f,1.78f),new(3.39f,2.89f,5.69f),new(2.38f,2.56f,6.34f),new(1.14f,1.97f,7.16f)});
                        break;
                }
            }
            if(rail.stops.Length>1 && rail.stops[1].marker)
            {
                var lift=rail.stops[1].marker;
                if((lift.localPosition-new Vector3(11.5f,6.1f,-14)).sqrMagnitude<.00001f)
                {
                    lift.localPosition=new Vector3(14.3f,6.1f,-14);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lift);
                }
            }
            foreach(var leaf in new[]{rail.leftDoor,rail.rightDoor})
            {
                var panel=leaf ? leaf.Find("Laminated shutter") : null;
                if(panel && !panel.TryGetComponent<BoxCollider>(out _)) panel.gameObject.AddComponent<BoxCollider>();
            }
            EditorSceneManager.MarkSceneDirty(vessel.gameObject.scene);
        }

        static void ReplaceDefaults(NereidPipeRoute pipe,Vector3[] baseline,Vector3[] revised)
        {
            if(pipe.knots.Count!=baseline.Length+2)return;
            for(int i=0;i<baseline.Length;i++)
                if(pipe.knots[i+1].manualTangents || (pipe.knots[i+1].position-baseline[i]).sqrMagnitude>.00001f)return;
            var previous=pipe.knots.ToArray();
            pipe.knots.Clear();pipe.knots.Add(previous[0]);
            for(int i=0;i<revised.Length;i++)
            {
                var k=previous[1+Mathf.RoundToInt(i*(baseline.Length-1f)/(revised.Length-1f))];
                k.position=revised[i];pipe.knots.Add(k);
            }
            pipe.knots.Add(previous[^1]);pipe.Rebuild();
            EditorUtility.SetDirty(pipe);PrefabUtility.RecordPrefabInstancePropertyModifications(pipe);
        }
    }
}

