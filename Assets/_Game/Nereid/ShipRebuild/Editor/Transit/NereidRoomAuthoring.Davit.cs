using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        const string DavitParts=Root+"/Prefabs/Cargo/Deck recovery crane";
        const string DavitAsset=DavitParts+"/Articulated recovery crane.prefab";
        static readonly Vector3 DeckSocket=new(3.65f,4.83f,-8.80f);
        static Transform Joint(string label,Transform parent,Vector3 at)
        {
            var joint=new GameObject(label).transform;joint.SetParent(parent,false);joint.localPosition=at;return joint;
        }

        [MenuItem("NEREID/Install deck recovery crane in current scene")]
        public static void FitDeckCrane() => FitDeckCrane(Object.FindAnyObjectByType<NereidVessel>());

        internal static void FitDeckCrane(NereidVessel vessel)
        {
            if(Application.isPlaying || !vessel || !vessel.authoredRail || !vessel.authoredRail.Ready)
                throw new InvalidOperationException("Open the NEREID gameplay scene with its installed cargo rail.");
            var rail=vessel.authoredRail;
            if(rail.davit)return;
            Folder(DavitParts);Folder(DavitParts+"/Meshes");
            Palette();var atlas=CabinAtlas();
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(DavitAsset);
            if(!asset)
            {
                var root=new GameObject("Articulated recovery crane");
                try
                {
                    parts.Clear();origin=Vector3.zero;room=root.transform;
                    var drive=root.AddComponent<NereidCargoDavit>();
                    MakeCrane(drive);
                    foreach(var piece in parts)piece.StorePalette(DavitParts+"/Meshes/"+piece.node.name+".asset",atlas,cabinSwatches);
                    asset=PrefabUtility.SaveAsPrefabAsset(root,DavitAsset);
                }
                finally{parts.Clear();Object.DestroyImmediate(root);}
            }
            FitReceiver(atlas);
            FitCagePendant(atlas);
            var installed=(GameObject)PrefabUtility.InstantiatePrefab(asset,rail.transform);
            installed.transform.localPosition=DeckSocket;
            PrefabUtility.RecordPrefabInstancePropertyModifications(installed.transform);
            rail.davit=installed.GetComponent<NereidCargoDavit>();
            foreach(var web in rail.trackBeams)if(web)Object.DestroyImmediate(web.gameObject);
            foreach(var cap in rail.trackSupports)if(cap)Object.DestroyImmediate(cap.gameObject);
            for(int i=rail.transform.childCount-1;i>=0;i--)
            {
                var item=rail.transform.GetChild(i);
                if(item.name=="Gantry support frame")Object.DestroyImmediate(item.gameObject);
            }
            if(rail.traveller)Object.DestroyImmediate(rail.traveller.gameObject);
            rail.trackBeams=Array.Empty<Transform>();rail.trackSupports=Array.Empty<Transform>();rail.wheels=Array.Empty<Transform>();
            rail.traveller=rail.davit.head;
            rail.craneAboveHatch=4.05f;
            EditorUtility.SetDirty(rail);
            var cage=rail.cageAnchor.GetComponentInChildren<CargoBasket>();
            if(vessel.hoistCable)
            {
                vessel.hoistCable.startWidth=.092f;vessel.hoistCable.endWidth=.092f;
                vessel.hoistCable.numCornerVertices=2;
                EditorUtility.SetDirty(vessel.hoistCable);
            }
            rail.davit.Present(rail.TrolleyPoint(cage.CableSocket.position),cage.CableSocket.position,vessel.hoistCable);
            foreach(var node in installed.GetComponentsInChildren<Transform>())
                PrefabUtility.RecordPrefabInstancePropertyModifications(node);
            EditorSceneManager.MarkSceneDirty(vessel.gameObject.scene);AssetDatabase.SaveAssets();
        }

        public static void FitDeckCraneToMain()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Game/Scenes/InterfaceVerticalSlice.unity");
            FitDeckCrane();
            EditorSceneManager.SaveScene(scene);
        }

        static void FitCagePendant(Material atlas)
        {
            string path=Root+"/Prefabs/Cargo/RecoveryCage.prefab";
            var cage=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(cage.transform.Find("Pinned cage strongback"))return;
                foreach(string name in new[]{"Main lifting yoke","Guide block"})
                {
                    var old=cage.transform.Find(name);if(old)Object.DestroyImmediate(old.gameObject);
                }
                parts.Clear();room=cage.transform;origin=Vector3.zero;
                var yoke=Part("Pinned cage strongback",new(0,1.45f,0));
                foreach(int end in new[]{-1,1})
                {
                    var shoe=new Vector3(0,1.48f,end*1.50f);
                    yoke.Tube(steel,shoe-Vector3.right*.19f,shoe+Vector3.right*.19f,.10f,-1,8);
                    foreach(int side in new[]{-1,1})
                    {
                        var pin=new Vector3(side*.115f,1.66f,end*.10f);
                        yoke.Tube(ochre,shoe+Vector3.right*(side*.122f),pin,.064f,.079f,6);
                    }
                }
                var top=new Vector3(0,1.70f,0);
                Band(yoke,top-Vector3.right*.038f,Vector3.up,Vector3.forward,.152f,.077f,.078f,steel,ochre,0,360,12);
                yoke.Tube(hull,top-Vector3.right*.18f-Vector3.up*.09f,top+Vector3.right*.18f-Vector3.up*.09f,.065f,-1,8);
                var socket=cage.GetComponent<CargoBasket>().CableSocket;
                socket.localPosition=top+Vector3.up*.14f;
                yoke.StorePalette(DavitParts+"/Meshes/"+yoke.node.name+".asset",atlas,cabinSwatches);
                PrefabUtility.SaveAsPrefabAsset(cage,path);
            }
            finally{parts.Clear();PrefabUtility.UnloadPrefabContents(cage);}
        }

        static void FitReceiver(Material atlas)
        {
            string path=Root+"/Prefabs/Cargo/RoofReceiver.prefab";
            var host=PrefabUtility.LoadPrefabContents(path);
            try
            {
                if(host.transform.Find("Recessed hatch coaming"))return;
                foreach(var t in host.GetComponentsInChildren<Transform>(true))
                {
                    if(!t)continue;
                    switch(t.name)
                    {
                        case "Pressure guide":case "Longitudinal service coaming":case "Pressure seal":case "Armoured stiffener":
                            Object.DestroyImmediate(t.gameObject);break;
                        case "Laminated shutter":
                            // These original leaves are the physical seal tested by the rail sweep.
                            if(t.TryGetComponent<MeshRenderer>(out var skin))Object.DestroyImmediate(skin);
                            if(t.TryGetComponent<MeshFilter>(out var filter))Object.DestroyImmediate(filter);
                            break;
                    }
                }
                parts.Clear();room=host.transform;origin=Vector3.zero;
                var coaming=Part("Recessed hatch coaming",Vector3.zero);
                for(int s=-1;s<=1;s+=2)
                {
                    // A tapered curb follows the pressure hull. Keep the full 4.8 by 4.8 aperture open.
                    var axis=Vector3.forward;
                    var centre=Vector3.right*(s*2.57f);
                    coaming.Beam(enamel,centre-axis*2.40f+Vector3.up*.21f,centre+axis*2.40f+Vector3.up*.21f,.30f,.56f);
                    coaming.Beam(steel,centre-axis*2.39f+Vector3.up*.46f,centre+axis*2.39f+Vector3.up*.46f,.21f,.095f);
                    coaming.Beam(hull,new(-2.46f,.23f,s*2.53f),new(2.46f,.23f,s*2.53f),.20f,.47f);
                    foreach(int end in new[]{-1,1})
                    {
                        var corner=new Vector3(s*2.56f,.41f,end*2.46f);
                        coaming.Tube(deck,corner-Vector3.up*.33f,corner+Vector3.up*.12f,.23f,.19f,8);
                        coaming.Tube(ochre,corner+Vector3.up*.10f,corner+Vector3.up*.14f,.16f,-1,8);
                    }
                    var leaf=host.transform.Find(s<0?"Port pressure door":"Starboard pressure door");
                    room=leaf;
                    var lid=Part(s<0?"Port formed closure":"Starboard formed closure",Vector3.zero);
                    lid.Box(hull,new(0,.004f,0),new(2.38f,.187f,4.78f),default,.17f);
                    for(int j=0;j<3;j++)
                    {
                        var at=new Vector3(s*.06f,.105f,(j-1)*1.48f);
                        Casting(lid,at,Vector3.right,Vector3.back,Shield(2.08f,1.28f,.25f),.095f,ceramic,steel,deck,.94f);
                        lid.Beam(enamel,at-Vector3.right*.73f+Vector3.up*.14f,at+Vector3.right*.73f+Vector3.up*.14f,.085f,.12f);
                    }
                    lid.Beam(rubber,new(-s*1.14f,.097f,-2.28f),new(-s*1.14f,.097f,2.28f),.092f,.14f);
                }
                foreach(var p in parts)p.StorePalette(DavitParts+"/Meshes/"+p.node.name+".asset",atlas,cabinSwatches);
                PrefabUtility.SaveAsPrefabAsset(host,path);
            }
            finally{parts.Clear();PrefabUtility.UnloadPrefabContents(host);}
        }
    }
}
