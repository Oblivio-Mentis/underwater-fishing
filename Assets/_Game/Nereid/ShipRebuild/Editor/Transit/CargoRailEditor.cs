using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public sealed class CargoRailEditor : EditorWindow
    {
        NereidCargoRail rail;
        int selected;
        bool preview,playing;
        float clock,duration;
        double previous;
        List<string> issues;
        readonly List<(Transform target,AnimationClip clip)> clips=new();

        [MenuItem("NEREID/Cargo Rail & Hatch Editor")]
        public static void Open()=>GetWindow<CargoRailEditor>("Cargo Rail & Hatch Editor");

        void OnEnable()
        {
            SceneView.duringSceneGui+=SceneGUI;
            EditorApplication.update+=UpdatePreview;
            EditorApplication.playModeStateChanged+=ModeChanged;
            Undo.undoRedoPerformed+=Repaint;
            rail=UnityEngine.Object.FindAnyObjectByType<NereidCargoRail>();
        }

        void ModeChanged(PlayModeStateChange mode)
        {
            if(mode==PlayModeStateChange.ExitingEditMode) StopPreview();
        }

        void OnDisable()
        {
            StopPreview();
            SceneView.duringSceneGui-=SceneGUI;
            EditorApplication.update-=UpdatePreview;
            EditorApplication.playModeStateChanged-=ModeChanged;
            Undo.undoRedoPerformed-=Repaint;
        }

        void OnGUI()
        {
            using(new EditorGUI.DisabledScope(preview))
                rail=(NereidCargoRail)EditorGUILayout.ObjectField("Recovery rail",rail,typeof(NereidCargoRail),true);
            if(!rail)return;
            var data=new SerializedObject(rail);data.Update();
            using(new EditorGUI.DisabledScope(preview))
            {
                EditorGUILayout.PropertyField(data.FindProperty("stops"),true);
                EditorGUILayout.PropertyField(data.FindProperty("aperture"));
                EditorGUILayout.PropertyField(data.FindProperty("cageDimensionsMetres"));
                EditorGUILayout.PropertyField(data.FindProperty("craneAboveHatch"));
                if(data.ApplyModifiedProperties())SceneView.RepaintAll();
                if(GUILayout.Button("Select roof receiver") && rail.hatch){selected=-1;Selection.activeTransform=rail.hatch;SceneView.RepaintAll();}
            }
            if(!rail.Ready){EditorGUILayout.HelpBox("Assign a receiver and complete stop markers.",MessageType.Warning);return;}
            using(new EditorGUI.DisabledScope(Application.isPlaying || (!preview && AnimationMode.InAnimationMode())))
            {
                EditorGUILayout.BeginHorizontal();
                if(!preview)
                {
                    if(GUILayout.Button("Preview recovery cycle")) StartPreview();
                }
                else
                {
                    if(GUILayout.Button(playing?"Pause":"Play")){playing=!playing;previous=EditorApplication.timeSinceStartup;}
                    if(GUILayout.Button("Stop preview"))StopPreview();
                }
                EditorGUILayout.EndHorizontal();
                if(preview)
                {
                    EditorGUI.BeginChangeCheck();clock=EditorGUILayout.Slider("Cycle (s)",clock,0,duration);
                    if(EditorGUI.EndChangeCheck()){playing=false;Sample();}
                }
            }
            if(GUILayout.Button("Check aperture and route clearance")) issues=TransitClearanceChecks.Cargo(rail);
            if(issues!=null)
            {
                if(issues.Count==0)EditorGUILayout.HelpBox("Hatch and route clearance OK.",MessageType.Info);
                foreach(var issue in issues)EditorGUILayout.HelpBox(issue,MessageType.Warning);
            }
        }

        void SceneGUI(SceneView scene)
        {
            if(!rail || !rail.Ready || Application.isPlaying)return;
            Handles.color=new Color(1,.68f,.18f);
            for(int i=0;i<rail.stops.Length;i++)
            {
                var p=rail.stops[i].marker.position;
                if(i>0)Handles.DrawAAPolyLine(3,rail.stops[i-1].marker.position,p);
                Handles.Label(p+Vector3.up*.33f,rail.stops[i].label);
                if(preview)continue;
                float size=HandleUtility.GetHandleSize(p)*.09f;
                if(Handles.Button(p,Quaternion.identity,size,size*1.25f,Handles.SphereHandleCap)){selected=i;Repaint();}
            }
            if(preview)return;
            var marker=selected==-1?rail.hatch:rail.stops[Mathf.Clamp(selected,0,rail.stops.Length-1)].marker;
            EditorGUI.BeginChangeCheck();
            var position=Handles.PositionHandle(marker.position,marker.rotation);
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(marker,"Move recovery marker");marker.position=position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);issues=null;Repaint();
            }
        }

        void UpdatePreview()
        {
            if(!preview)return;
            if(!rail || !AnimationMode.InAnimationMode()){StopPreview();return;}
            if(!playing)return;
            double now=EditorApplication.timeSinceStartup;
            clock=Mathf.Min(duration,clock+(float)(now-previous));previous=now;
            if(clock>=duration)playing=false;
            Sample(); Repaint();
        }

        void StartPreview()
        {
            if(AnimationMode.InAnimationMode())return;
            float up=rail.timings ? rail.timings.raisingSeconds : 5;
            float unload=rail.timings ? rail.timings.unloadingSeconds : 3;
            float back=rail.timings ? rail.timings.returningSeconds : 4;
            duration=up+unload+back;
            float Travel(float t)=>t<=up?NereidCargoRail.TravelFraction(HoistPhase.Raising,t/up):
                t<up+unload?1:NereidCargoRail.TravelFraction(HoistPhase.Returning,(t-up-unload)/back);
            float Hatch(float t)=>rail.DoorAmount(rail.Point(Travel(t)));
            Vector3 Local(Transform target,Vector3 point)=>target.parent?target.parent.InverseTransformPoint(point):point;
            Animate(rail.cageAnchor,"m_LocalPosition",t=>Local(rail.cageAnchor,rail.Point(Travel(t))));
            Animate(rail.traveller,"m_LocalPosition",t=>Local(rail.traveller,rail.TrolleyPoint(rail.Point(Travel(t)))));
            Animate(rail.leftDoor,"m_LocalPosition",t=>rail.DoorPosition(true,Hatch(t)));
            Animate(rail.rightDoor,"m_LocalPosition",t=>rail.DoorPosition(false,Hatch(t)));
            if(rail.cageAnchor && rail.cageAnchor.GetComponentInChildren<CargoBasket>() is CargoBasket cage)
            {
                var data=new SerializedObject(cage);
                float Closure(float t)=>t<=up?Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,.145f,t/up)):
                    t<=up+unload?1:1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.87f,1,(t-up-unload)/back));
                float Floor(float t)
                {
                    float s=(t-up)/unload;
                    return Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.32f,s))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.83f,1,s)));
                }
                for(int i=0;i<2;i++)
                {
                    float sign=i==0?1:-1;
                    Animate((Transform)data.FindProperty("lids").GetArrayElementAtIndex(i).objectReferenceValue,"localEulerAnglesRaw",
                        t=>Vector3.forward*(sign*62*(1-Closure(t))));
                    Animate((Transform)data.FindProperty("trapdoors").GetArrayElementAtIndex(i).objectReferenceValue,"localEulerAnglesRaw",
                        t=>Vector3.back*(sign*71*Floor(t)));
                }
                var gate=(Transform)data.FindProperty("loadingGate").objectReferenceValue;
                if(gate) Animate(gate,"m_LocalPosition",t=>new Vector3(gate.localPosition.x,2.02f*(1-Closure(t)),0));
            }
            AnimationMode.StartAnimationMode();
            preview=playing=true;clock=0;previous=EditorApplication.timeSinceStartup;Sample();
        }

        void Animate(Transform target,string property,Func<float,Vector3> pose)
        {
            if(!target)return;
            var curves=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
            const int intervals=160;
            for(int i=0;i<=intervals;i++)
            {
                float t=duration*i/intervals;
                var v=pose(t);
                for(int j=0;j<3;j++) curves[j].AddKey(t,v[j]);
            }
            var clip=new AnimationClip{hideFlags=HideFlags.HideAndDontSave};
            for(int j=0;j<3;j++) clip.SetCurve("",typeof(Transform),property+"."+"xyz"[j],curves[j]);
            clips.Add((target,clip));
        }

        void Sample()
        {
            AnimationMode.BeginSampling();
            try
            {
                foreach(var pair in clips)
                    if(pair.target)AnimationMode.SampleAnimationClip(pair.target.gameObject,pair.clip,clock);
            }
            finally {AnimationMode.EndSampling();}
            SceneView.RepaintAll();
        }

        void StopPreview()
        {
            if(preview && AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
            preview=playing=false;
            foreach(var pair in clips)if(pair.clip)DestroyImmediate(pair.clip);
            clips.Clear();SceneView.RepaintAll();
        }
    }
}

