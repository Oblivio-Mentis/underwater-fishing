using System;
using System.Collections.Generic;
using System.IO;
using Raid.Factory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        const string PressFolder=Root+"/Prefabs/Processor";
        const string MillPrefab=PressFolder+"/Nereid separation press.prefab";

        [MenuItem("NEREID/Install separation press in current scene")]
        public static void FitProcessor()
        {
            var factory=Object.FindAnyObjectByType<FactoryVisuals>();
            if(!factory || Application.isPlaying)throw new InvalidOperationException("Open the stopped NEREID gameplay scene.");
            var workshop=factory.transform.Find("Aft processing workshop");
            if(!workshop || !factory.processorGrinder || !factory.processorDrop || !factory.processRoute)
                throw new InvalidOperationException("The working conveyors and their pressure route must be installed first.");

            var existing=workshop.GetComponentInChildren<ProcessorDriveVisual>(true);
            if(existing)return;

            Folder(PressFolder);Folder(PressFolder+"/Meshes");
            Palette();var finish=CabinAtlas();
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(MillPrefab);
            if(!asset)
            {
                var assembly=new GameObject("Nereid separation press");
                try
                {
                    parts.Clear();origin=Vector3.zero;room=assembly.transform;
                    var drive=assembly.AddComponent<ProcessorDriveVisual>();
                    ReceivingHead(drive);CutterAndBonnet(drive);SeparationDrawers(drive);
                    var lamp=Part("Cantilever belt inspection lamp",new(-1.70f,1.40f,.67f));
                    var start=new Vector3(-1.78f,1.13f,.53f);var column=start+new Vector3(0,.56f,.31f);
                    var end=column+new Vector3(.99f,.11f,.25f);
                    lamp.Beam(hull,start,column,.146f,.133f);
                    lamp.Beam(enamel,column,end,.170f,.153f);
                    HoodedLamp(lamp,end+Vector3.down*.095f,Vector3.right,Vector3.forward,.49f,.20f);
                    lamp.Lamp("Inspection wash",new Color(.89f,.85f,.73f),end+new Vector3(.19f,-.23f,.05f),25f,7.0f);
                    foreach(var piece in parts)
                        piece.StorePalette(PressFolder+"/Meshes/"+piece.node.name+".asset",finish,cabinSwatches);
                    drive.batchLamp=room.Find("Press working light").GetComponent<Renderer>();
                    drive.beltFinish=PressBelt();drive.rollerFinish=deck;
                    asset=PrefabUtility.SaveAsPrefabAsset(assembly,MillPrefab);
                }
                finally{parts.Clear();Object.DestroyImmediate(assembly);}
            }

            var installed=(GameObject)PrefabUtility.InstantiatePrefab(asset,workshop);
            var anchor=factory.processorGrinder.localPosition;
            installed.transform.localPosition=new Vector3(anchor.x,0,anchor.z);
            // Workshop transforms use half-height geometry. The new replaceable model has uniform vessel units.
            installed.transform.localScale=new Vector3(1,2,1);
            PrefabUtility.RecordPrefabInstancePropertyModifications(installed.transform);
            var mill=installed.GetComponent<ProcessorDriveVisual>();
            var line=factory.processRoute;
            mill.intakeCollar.position=line.WorldPoint(1);
            mill.intakeCollar.rotation=Quaternion.LookRotation((line.WorldPoint(1)-line.WorldPoint(.994f)).normalized,workshop.up);
            PrefabUtility.RecordPrefabInstancePropertyModifications(mill.intakeCollar);

            factory.processorLamp=mill.batchLamp;
            foreach(var belt in factory.belts)if(belt)belt.sharedMaterial=mill.beltFinish;
            foreach(var roller in factory.rollers)if(roller && roller.TryGetComponent<Renderer>(out var surface))surface.sharedMaterial=mill.rollerFinish;
            RetireFactoryShell(workshop);
            RetireCabinMill();
            EditorUtility.SetDirty(factory);EditorSceneManager.MarkSceneDirty(factory.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        public static void FitProcessorToMain()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/InterfaceVerticalSlice.unity");
            FitProcessor();EditorSceneManager.SaveScene(scene);
        }

        static Material PressBelt()
        {
            string path=PressFolder+"/Tread matte.mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(existing)return existing;
            string pattern=PressFolder+"/Conveyor register.png";
            if(!File.Exists(pattern))
            {
                // One broad registration band travels along the factory's actual scrolling tread.
                var texture=new Texture2D(2,8,TextureFormat.RGB24,false);
                for(int v=0;v<8;v++)for(int u=0;u<2;u++)
                    texture.SetPixel(u,v,v==0?new Color(.24f,.29f,.28f):new Color(.15f,.20f,.20f));
                texture.Apply();File.WriteAllBytes(pattern,texture.EncodeToPNG());Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(pattern);
                var importer=(TextureImporter)AssetImporter.GetAtPath(pattern);
                importer.mipmapEnabled=false;importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Repeat;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            }
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Tread matte",enableInstancing=true};
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(pattern));
            mat.SetFloat("_Metallic",.045f);mat.SetFloat("_Smoothness",.23f);
            AssetDatabase.CreateAsset(mat,path);return mat;
        }

        static readonly HashSet<string> OldPressParts=new(StringComparer.Ordinal){
            "Belt underframe","Conveyor guard rail","Conveyor leg","Workshop rear service panel",
            "Processor front armor","Processor intakes shadow","Intake upper guide","Intake lower guide",
            "Machine pressure casing","Processor active batch lamp","Material output cradle","Labeled material outlet",
            "Hopper back wall","Hopper upright","Hopper safety bracket","Hopper top rail"
        };

        static void RetireFactoryShell(Transform workshop)
        {
            foreach(var renderer in workshop.GetComponentsInChildren<Renderer>(true))
            {
                if(renderer.GetComponentInParent<ProcessorDriveVisual>())continue;
                string label=renderer.name;
                bool oldLabel=label.StartsWith("Label ",StringComparison.Ordinal) && (
                    renderer.transform.parent==workshop || renderer.transform.parent.name=="Biomaterial processor" ||
                    renderer.transform.parent.name.StartsWith("Material output bin ",StringComparison.Ordinal));
                if(!oldLabel && !OldPressParts.Contains(label))continue;
                if(renderer.TryGetComponent<Collider>(out _))
                {
                    // These nine boxes are the existing player collision for belts, drawers and casing.
                    var filter=renderer.GetComponent<MeshFilter>();Object.DestroyImmediate(renderer);
                    if(filter)Object.DestroyImmediate(filter);
                }
                else if(renderer.transform.childCount==0)Object.DestroyImmediate(renderer.gameObject);
                else Object.DestroyImmediate(renderer);
            }
            var hopper=workshop.Find("Transparent distribution hopper");
            if(hopper && hopper.childCount==0)Object.DestroyImmediate(hopper.gameObject);
        }

        static void RetireCabinMill()
        {
            string path=CabinFolder+"/AftTransfer fittings.prefab";
            var host=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(string id in new[]{"Twin induction mill jacket","Cyclone extraction cowl"})
                {
                    var legacy=host.transform.Find(id);
                    if(legacy)Object.DestroyImmediate(legacy.gameObject);
                }
                PrefabUtility.SaveAsPrefabAsset(host,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(host);}
        }
    }
}
