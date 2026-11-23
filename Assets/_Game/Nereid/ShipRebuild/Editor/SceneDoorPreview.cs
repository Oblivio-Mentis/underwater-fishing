using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Raid.Nereid.Editor
{
    // Scene View inspection uses the editor animation preview so the authored rest pose survives save, Play and reload.
    [InitializeOnLoad]
    public static class SceneDoorPreview
    {
        const string Command="RAID/NEREID/Preview dry lock and open passages";
        static readonly List<AnimationClip> clips=new();
        static bool active;

        static SceneDoorPreview()
        {
            EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.ExitingEditMode) Stop();};
            EditorSceneManager.sceneSaving+=(scene,path)=>Stop();
            EditorSceneManager.activeSceneChangedInEditMode+=(before,after)=>Stop();
            PrefabStage.prefabSaving+=prefab=>Stop();
            AssemblyReloadEvents.beforeAssemblyReload+=Stop;
        }

        [MenuItem(Command,true)]
        static bool Available()
        {
            Menu.SetChecked(Command,active&&AnimationMode.InAnimationMode());
            return !EditorApplication.isPlayingOrWillChangePlaymode && (active||!AnimationMode.InAnimationMode());
        }

        [MenuItem(Command)]
        public static void Toggle()
        {
            if(active){Stop();return;}
            if(EditorApplication.isPlayingOrWillChangePlaymode||AnimationMode.InAnimationMode())return;
            var stage=PrefabStageUtility.GetCurrentPrefabStage();
            var roots=stage?new[]{stage.prefabContentsRoot}:SceneManager.GetActiveScene().GetRootGameObjects();
            var poses=new List<(Transform target,Vector3 offset)>();
            foreach(var root in roots)
            {
                foreach(var door in root.GetComponentsInChildren<NereidPassageDoor>())
                {
                    poses.Add((door.portLeaf,Vector3.left*door.stroke));
                    poses.Add((door.starboardLeaf,Vector3.right*door.stroke));
                }
                foreach(var rig in root.GetComponentsInChildren<NereidPressureDoors>())
                {
                    // The sea-facing pair stays sealed in the dry, docked pose.
                    poses.Add((rig.innerPort,Vector3.back*rig.stroke));
                    poses.Add((rig.innerStarboard,Vector3.forward*rig.stroke));
                }
            }
            if(poses.Count==0)return;
            active=true;
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                try
                {
                    foreach(var (target,offset) in poses)
                    {
                        if(!target)continue;
                        var pose=target.localPosition+offset;
                        var clip=new AnimationClip{hideFlags=HideFlags.HideAndDontSave};
                        clips.Add(clip);
                        for(int i=0;i<3;i++)
                            clip.SetCurve("",typeof(Transform),"m_LocalPosition."+"xyz"[i],AnimationCurve.Constant(0,1,pose[i]));
                        AnimationMode.SampleAnimationClip(target.gameObject,clip,0);
                    }
                }
                finally { AnimationMode.EndSampling(); }
            }
            catch {Stop();throw;}
            SceneView.RepaintAll();
        }

        static void Stop()
        {
            if(!active)return;
            active=false;
            if(AnimationMode.InAnimationMode())AnimationMode.StopAnimationMode();
            foreach(var clip in clips)if(clip)Object.DestroyImmediate(clip);
            clips.Clear();
            SceneView.RepaintAll();
        }
    }
}
