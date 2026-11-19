using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
namespace Raid.UI.Editor
{
    public static class UiAuthoring
    {
        public const string ScenePath="Assets/_Game/Scenes/InterfaceVerticalSlice.unity";
        const string FontPath="Assets/_Game/UI/Overhaul/Fonts/PTSans-SDF.asset";
        [MenuItem("RAID/011/Install UI in separate NEREID scene")]
        public static void Install()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play before authoring");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save current scene before switching");
            var scene=EditorSceneManager.OpenScene(System.IO.File.Exists(ScenePath)?ScenePath:"Assets/_Game/Scenes/VisualVerticalSlice.unity");
            if(scene.path!=ScenePath)EditorSceneManager.SaveScene(scene,ScenePath);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if(font==null)
            {
                var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Game/UI/Overhaul/Fonts/PTSans-Regular.ttf");
                font=TMP_FontAsset.CreateFontAsset(source,64,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);font.name="PT Sans UI Cyrillic";
                var chars=new List<uint>();for(uint i=32;i<127;i++)chars.Add(i);for(uint i=0x400;i<=0x45f;i++)chars.Add(i);
                chars.AddRange(new uint[]{0x00b7,0x00b0,0x00d7,0x2013,0x2014,0x2022,0x2026,0x2116,0x2190,0x2191,0x2192,0x2193,0x2264,0x2265});
                uint[] missing;font.TryAddCharacters(chars.ToArray(),out missing);font.atlasPopulationMode=AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font,FontPath);AssetDatabase.AddObjectToAsset(font.material,font);
                foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
                EditorUtility.SetDirty(font);
            }
            var root=Object.FindAnyObjectByType<UiRoot>();if(root==null)root=new GameObject("RAID011 UI Root",typeof(RectTransform)).AddComponent<UiRoot>();
            root.font=font;
            var session=Object.FindAnyObjectByType<RaidSession>();
            if(session!=null && session.menuCamera!=null)
            {
                var camera=session.menuCamera;camera.transform.position=new Vector3(-25,7,24);camera.transform.rotation=Quaternion.LookRotation(new Vector3(-2,1,-3)-camera.transform.position);camera.fieldOfView=64;
            }
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("[RAID011] Separate interface scene saved; NEREID assets, SceneIds and colliders retained.");
        }
    }
}
