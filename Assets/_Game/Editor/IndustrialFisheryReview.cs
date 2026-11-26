using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Raid.Editor
{
    public static class IndustrialFisheryReview
    {
        const string Output="Docs/RAID-020/Technologies";
        [MenuItem("RAID/Technologies/Render equipment inspection")]
        public static void RenderInspection()
        {
            Directory.CreateDirectory(Output);
            Render(new[]{"cage","trap","charge","vacuum","net"}, "equipment-active.png");
            Render(new[]{"farm","lure","bubbles","winch","magnet","oxygen","robot","mail"}, "equipment-industry.png");
            const string icons="Assets/_Game/Progression/Resources/Technology/Icons";
            Directory.CreateDirectory(icons);
            foreach(var item in TechCatalog.Items)Render(new[]{item.id},Path.Combine(icons,item.id+".png"),true);
            AssetDatabase.Refresh();
        }

        static void Render(string[] kinds,string filename,bool icon=false)
        {
            var preview=new PreviewRenderUtility();
            Texture2D frame=null;
            try
            {
                var camera=preview.camera;
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.046f,.083f,.098f);
                camera.orthographic=true; camera.orthographicSize=kinds.Length<=5?2.18f:2.38f;
                var centre=Vector3.zero;
                camera.transform.position=centre+new Vector3(3.7f,4.9f,9f);
                camera.transform.LookAt(centre);camera.nearClipPlane=.1f;camera.farClipPlane=50;
                preview.lights[0].transform.rotation=Quaternion.Euler(34,-30,0);
                preview.lights[0].intensity=1.45f;preview.lights[0].color=new Color(1,.88f,.71f);
                preview.lights[1].transform.rotation=Quaternion.Euler(42,145,0);
                preview.lights[1].intensity=1.4f;preview.lights[1].color=new Color(.49f,.83f,1);
                preview.ambientColor=new Color(.39f,.49f,.51f);
                int cols=(kinds.Length+1)/2;
                for(int i=0;i<kinds.Length;i++)
                {
                    var go=new GameObject(kinds[i]);preview.AddSingleGO(go);
                    var shell=go.AddComponent<TechRigShell>();shell.Build(kinds[i]);
                    var rig=new TechRig{kind=kinds[i],position=icon?Vector3.zero:new Vector3(((cols-1)*.5f-i%cols)*1.7f,0,(.5f-i/cols)*2.7f)};
                    if(kinds[i]=="cage"){rig.mode="captured";rig.contents.Add(new CatchItem(1,1,1,1,1,1));}
                    shell.Display(rig,true);
                    go.transform.rotation=Quaternion.Euler(0,-18,0);
                    if(icon)
                    {
                        var parts=go.GetComponentsInChildren<Renderer>();
                        Bounds bounds=parts[0].bounds;
                        foreach(var part in parts)bounds.Encapsulate(part.bounds);
                        camera.transform.position+=bounds.center;
                        camera.orthographicSize=Mathf.Max(.16f,bounds.extents.magnitude*.96f);
                    }
                }
                var rect=icon?new Rect(0,0,128,128):new Rect(0,0,1400,720);
                preview.BeginStaticPreview(rect);preview.Render(true);
                frame=preview.EndStaticPreview();
                File.WriteAllBytes(icon?filename:Path.Combine(Output,filename),frame.EncodeToPNG());
            }
            finally {if(frame)UnityEngine.Object.DestroyImmediate(frame);preview.Cleanup();}
        }

        [MenuItem("RAID/Technologies/Build Windows QA")]
        public static void BuildQA()
        {
            BuildAt("Builds/RAID020-Technology-QA/UnderwaterFishing.exe");
        }
        public static void BuildCrew()
        {
            BuildAt("Builds/RAID020-Crew/UnderwaterFishing.exe");
        }
        static void BuildAt(string destination)
        {
            string path=Path.GetFullPath(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{"Assets/_Game/Scenes/InterfaceVerticalSlice.unity"},
                locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development
            });
            if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Technology QA build "+result.summary.result);
            Debug.Log("[Technologies] Windows QA ready: "+path);
        }
    }
}

