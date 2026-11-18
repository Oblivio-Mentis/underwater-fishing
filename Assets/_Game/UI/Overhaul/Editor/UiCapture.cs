using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
namespace Raid.UI.Editor
{
    public static class UiCapture
    {
        static int original=-1;
        static EditorWindow View => Resources.FindObjectsOfTypeAll<EditorWindow>().First(x=>x.GetType().Name=="GameView");
        public static void Resolution(int width,int height)
        {
            var view=View;var selected=view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(original<0)original=(int)selected.GetValue(view,null);
            var sizes=Type.GetType("UnityEditor.GameViewSizes,UnityEditor");
            var instance=sizes.BaseType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);
            var groupType=Type.GetType("UnityEditor.GameViewSizeGroupType,UnityEditor");
            var group=sizes.GetMethod("GetGroup").Invoke(instance,new[]{Enum.Parse(groupType,"Standalone")});
            string name="RAID011 QA "+width+"x"+height;
            var labels=(string[])group.GetType().GetMethod("GetDisplayTexts").Invoke(group,null);
            int index=Array.FindIndex(labels,x=>x.StartsWith(name));
            if(index<0)
            {
                var sizeType=Type.GetType("UnityEditor.GameViewSize,UnityEditor");var mode=Type.GetType("UnityEditor.GameViewSizeType,UnityEditor");
                var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.Parse(mode,"FixedResolution"),width,height,name},null);
                group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});index=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null)-1;
            }
            selected.SetValue(view,index,null);view.Repaint();
        }
        public static void RestoreResolution()
        {
            if(original<0)return;var view=View;view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,original,null);original=-1;view.Repaint();
        }
        public static void Capture(string filename)
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Capture requires Play Mode");
            ScreenCapture.CaptureScreenshot("Docs/RAID-011/Screenshots/"+System.IO.Path.GetFileName(filename));
        }
        public static string LayoutReport()
        {
            Canvas.ForceUpdateCanvases();var root=UiRoot.Instance;
            int escaped=0,visible=0,overflow=0;
            foreach(var text in root.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();visible++;
                if(text.isTextOverflowing && text.GetComponentInParent<UnityEngine.UI.ScrollRect>()==null)overflow++;
                if(text.GetComponentInParent<UnityEngine.UI.RectMask2D>()!=null)continue;
                var corners=new Vector3[4];text.rectTransform.GetWorldCorners(corners);
                if(corners[0].x<-.5f || corners[0].y<-.5f || corners[2].x>Screen.width+.5f || corners[2].y>Screen.height+.5f)escaped++;
            }
            string result="resolution="+Screen.width+"x"+Screen.height+" page="+root.Page+" scale="+root.Settings.scale+" visibleTexts="+visible+" textOutsideScreen="+escaped+" nonScrollTextOverflow="+overflow;
            Debug.Log("[RAID011 LAYOUT] "+result);return result;
        }
    }
}
