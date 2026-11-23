using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public sealed class PipeRouteEditor : EditorWindow
    {
        NereidPipeRoute route;
        Vector2 scroll;
        int selected;
        bool handles=true;
        List<string> issues;

        [MenuItem("NEREID/Pipe Route Editor")]
        public static void Open()=>GetWindow<PipeRouteEditor>("Pipe Route Editor");

        void OnEnable(){SceneView.duringSceneGui+=SceneGUI;Undo.undoRedoPerformed+=AfterUndo;}
        void OnDisable(){SceneView.duringSceneGui-=SceneGUI;Undo.undoRedoPerformed-=AfterUndo;}
        void AfterUndo(){if(route)route.Rebuild();Repaint();SceneView.RepaintAll();}

        void OnGUI()
        {
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.BeginHorizontal();
            foreach(PipeLine kind in (PipeLine[])System.Enum.GetValues(typeof(PipeLine)))
                if(GUILayout.Toggle(route && route.line==kind,kind.ToString(),"Button") && (!route || route.line!=kind))
                    foreach(var candidate in Object.FindObjectsByType<NereidPipeRoute>())
                        if(candidate.line==kind){SelectRoute(candidate);break;}
            EditorGUILayout.EndHorizontal();
            var choice=(NereidPipeRoute)EditorGUILayout.ObjectField("Route",route,typeof(NereidPipeRoute),true);
            if(choice!=route)SelectRoute(choice);
            if(!route){EditorGUILayout.HelpBox("Select an installed line.",MessageType.Info);EditorGUILayout.EndScrollView();return;}
            var data=new SerializedObject(route);
            data.Update();
            foreach(string field in new[]{"source","destination","radius","smoothing","curveSteps","circumference","sightGlass","metal","enamel"})
                EditorGUILayout.PropertyField(data.FindProperty(field));
            int count=route.knots.Count;
            selected=Mathf.Clamp(selected,0,Mathf.Max(0,count-1));
            if(count>0)
            {
                selected=EditorGUILayout.IntSlider("Knot",selected,0,count-1);
                var knot=data.FindProperty("knots").GetArrayElementAtIndex(selected);
                EditorGUILayout.PropertyField(knot.FindPropertyRelative("manualTangents"));
                if(knot.FindPropertyRelative("manualTangents").boolValue)
                {
                    EditorGUILayout.PropertyField(knot.FindPropertyRelative("incoming"));
                    EditorGUILayout.PropertyField(knot.FindPropertyRelative("outgoing"));
                }
                if(selected+1<count)
                {
                    EditorGUILayout.PropertyField(knot.FindPropertyRelative("section"),new GUIContent("Outgoing section"));
                    EditorGUILayout.PropertyField(knot.FindPropertyRelative("materialOverride"));
                }
            }
            if(data.ApplyModifiedProperties())Changed();
            handles=EditorGUILayout.Toggle("Scene handles",handles);
            EditorGUILayout.BeginHorizontal();
            using(new EditorGUI.DisabledScope(count<2))
            {
                if(GUILayout.Button("Insert bend")) Insert();
                if(GUILayout.Button(route.destination?"Extend and unbind end":"Extend end")) Extend();
            }
            using(new EditorGUI.DisabledScope(count<=2 || selected==0 || selected==count-1))
                if(GUILayout.Button("Remove knot")){Record(); route.knots.RemoveAt(selected);Changed();}
            EditorGUILayout.EndHorizontal();
            if(GUILayout.Button("Rebuild Selected Pipe")){route.Rebuild();SceneView.RepaintAll();}
            if(GUILayout.Button("Check sockets and clearance"))issues=TransitClearanceChecks.Pipe(route);
            if(issues!=null)
            {
                if(issues.Count==0)EditorGUILayout.HelpBox("Sockets and clearance OK.",MessageType.Info);
                foreach(var issue in issues) EditorGUILayout.HelpBox(issue,MessageType.Warning);
            }
            EditorGUILayout.EndScrollView();
        }

        void SelectRoute(NereidPipeRoute target)
        {
            route=target;selected=0;issues=null;
            if(route)Selection.activeGameObject=route.gameObject;
            SceneView.RepaintAll();
        }

        void Record()=>Undo.RecordObject(route,"Edit pressure line");
        void Changed()
        {
            issues=null;route.Rebuild();EditorUtility.SetDirty(route);PrefabUtility.RecordPrefabInstancePropertyModifications(route);
            EditorSceneManager.MarkSceneDirty(route.gameObject.scene);SceneView.RepaintAll();Repaint();
        }

        void Insert()
        {
            int i=Mathf.Min(selected,route.knots.Count-2);
            Vector3 a=route.LocalPoint(i),d=route.LocalPoint(i+1);
            Vector3 b=a+route.Tangent(i,true),c=d+route.Tangent(i+1,false);
            Vector3 ab=(a+b)*.5f,bc=(b+c)*.5f,cd=(c+d)*.5f,abc=(ab+bc)*.5f,bcd=(bc+cd)*.5f,p=(abc+bcd)*.5f;
            var left=route.knots[i];var right=route.knots[i+1];
            left.incoming=route.Tangent(i,false);left.outgoing=ab-a;left.manualTangents=true;
            right.incoming=cd-d;right.outgoing=route.Tangent(i+1,true);right.manualTangents=true;
            Record();route.knots[i]=left;route.knots[i+1]=right;
            route.knots.Insert(i+1,new PipeKnot{position=p,manualTangents=true,incoming=abc-p,outgoing=bcd-p,section=left.section,materialOverride=left.materialOverride});
            selected=i+1;Changed();
        }

        void Extend()
        {
            int last=route.knots.Count-1;
            Vector3 p=route.LocalPoint(last);
            Vector3 heading=(p-route.LocalPoint(last-1)).normalized;
            if(heading.sqrMagnitude<.1f)heading=Vector3.forward;
            Vector3 next=p+heading*Mathf.Max(1,route.radius*4);
            Record();var knot=route.knots[last];knot.position=p;route.knots[last]=knot;
            route.knots.Add(new PipeKnot{position=next});
            route.destination=null;
            selected=last+1;Changed();
        }

        void MoveSocket(PipeSocket socket,Vector3 point)
        {
            Undo.RecordObject(socket.transform,"Move pipe socket");
            socket.transform.position=point;
            PrefabUtility.RecordPrefabInstancePropertyModifications(socket.transform);
        }

        void SceneGUI(SceneView scene)
        {
            if(!route || !handles || Application.isPlaying || route.knots.Count==0) return;
            Color tint=route.line==PipeLine.Feed ? new Color(1,.48f,.13f) : route.line==PipeLine.Process ? new Color(.47f,.91f,.50f) : Color.cyan;
            Handles.color=tint;
            var previous=route.WorldPoint(0);
            for(int i=1;i<=72;i++)
            {
                var p=route.WorldPoint(i/72f);
                Handles.DrawAAPolyLine(3,previous,p);
                if(i%10==0 && (p-previous).sqrMagnitude>.0001f)
                    Handles.ConeHandleCap(0,p,Quaternion.LookRotation(p-previous),HandleUtility.GetHandleSize(p)*.14f,EventType.Repaint);
                previous=p;
            }
            for(int i=0;i<route.knots.Count;i++)
            {
                var p=route.transform.TransformPoint(route.LocalPoint(i));
                float size=HandleUtility.GetHandleSize(p)*.075f;
                if(Handles.Button(p,Quaternion.identity,size,size*1.3f,Handles.SphereHandleCap)){selected=i;Repaint();}
            }
            selected=Mathf.Clamp(selected,0,route.knots.Count-1);
            var origin=route.transform.TransformPoint(route.LocalPoint(selected));
            EditorGUI.BeginChangeCheck();var moved=Handles.PositionHandle(origin,route.transform.rotation);
            if(EditorGUI.EndChangeCheck())
            {
                Record();var knot=route.knots[selected];knot.position=route.transform.InverseTransformPoint(moved);route.knots[selected]=knot;
                if(selected==0 && route.source) MoveSocket(route.source,moved);
                if(selected==route.knots.Count-1 && route.destination)MoveSocket(route.destination,moved);
                Changed();
            }
            if(selected>0) TangentHandle(origin,false);
            if(selected<route.knots.Count-1) TangentHandle(origin,true);
        }

        void TangentHandle(Vector3 origin,bool outgoing)
        {
            var tip=origin+route.transform.TransformVector(route.Tangent(selected,outgoing));
            Handles.color=Color.yellow;Handles.DrawDottedLine(origin,tip,3);
            var size=HandleUtility.GetHandleSize(tip)*.06f;
            EditorGUI.BeginChangeCheck();var p=Handles.FreeMoveHandle(tip,size,Vector3.zero,Handles.CubeHandleCap);
            if(!EditorGUI.EndChangeCheck())return;
            Record();
            var knot=route.knots[selected];
            knot.incoming=route.Tangent(selected,false);knot.outgoing=route.Tangent(selected,true);
            var tangent=route.transform.InverseTransformVector(p-origin);
            if(outgoing)knot.outgoing=tangent;else knot.incoming=tangent;
            knot.manualTangents=true;route.knots[selected]=knot;Changed();
        }
    }
}

