using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid.Editor
{
    public static partial class NereidPressureDoorAuthoring
    {
        const string Root="Assets/_Game/Nereid/ShipRebuild";
        const string Room=Root+"/Prefabs/Rooms/AirlockCollar.prefab";
        const string Kit=Root+"/Prefabs/Rooms/PressureDoors.prefab";
        const string FloodMaterial=Root+"/Materials/Pressure chamber water.mat";
        static Material Finish(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");

        [MenuItem("RAID/NEREID/Install pressure doors")]
        public static void Install()
        {
            var kit=AssetDatabase.LoadAssetAtPath<GameObject>(Kit);
            if(!kit)
            {
                var assembly=new GameObject("PressureDoors");
                try
                {
                    var rig=assembly.AddComponent<NereidPressureDoors>();
                    rig.innerPort=Leaf(assembly.transform,false,false);
                    rig.innerStarboard=Leaf(assembly.transform,false,true);
                    rig.outerPort=Leaf(assembly.transform,true,false);
                    rig.outerStarboard=Leaf(assembly.transform,true,true);
                    rig.controls=new AirlockPad[4];
                    for(byte i=0;i<4;i++) rig.controls[i]=Panel(assembly.transform,i);
                    var water=GameObject.CreatePrimitive(PrimitiveType.Cube);
                    water.name="Chamber waterline";
                    UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
                    rig.waterSurface=water.transform;
                    water.transform.SetParent(assembly.transform,false);
                    water.transform.localPosition=new Vector3(4.325f,rig.emptyLevel,0);
                    water.transform.localScale=new Vector3(8.53f,.012f,6.17f);
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(FloodMaterial);
                    if(!mat)
                    {
                        mat=new Material(Resources.Load<Shader>("PressureLockWater"));
                        AssetDatabase.CreateAsset(mat,FloodMaterial);
                    }
                    var renderer=water.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial=mat; renderer.shadowCastingMode=ShadowCastingMode.Off;
                    water.SetActive(false);
                    kit=PrefabUtility.SaveAsPrefabAsset(assembly,Kit);
                }
                finally { UnityEngine.Object.DestroyImmediate(assembly); }
            }
            FitDoorFrames();
            var room=PrefabUtility.LoadPrefabContents(Room);
            try
            {
                if(!room.GetComponentInChildren<NereidPressureDoors>(true))
                {
                    var mount=(GameObject)PrefabUtility.InstantiatePrefab(kit,room.transform);
                    // Room drawings use half-scale hull coordinates. Doors, sills and pump share world metres.
                    mount.transform.localScale=Vector3.one*.5f;
                    PrefabUtility.SaveAsPrefabAsset(room,Room);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
            AssetDatabase.SaveAssets();
        }

        static Transform Leaf(Transform parent,bool ocean,bool starboard)
        {
            const float half=.74f;
            var leaf=new ShipSectionMesh(parent,Vector3.zero,(ocean?"Sea":"Cabin")+(starboard?" starboard":" port")+" leaf",Vector3.zero);
            var armor=Finish("Atlantic blue pressure steel"); var seal=Finish("Reinforced hose rubber");
            var plate=Finish("Petrol marine enamel"); var steel=Finish("Machined titanium");
            var stripe=Finish("Rescue ochre"); var lamp=Finish(ocean?"Reservoir teal light":"Amber work light");
            leaf.Box(armor,Vector3.zero,new(.235f,3.22f,1.48f));
            leaf.Box(seal,new(0,0,.70f),new(.27f,3.19f,.075f));
            foreach(int face in new[]{-1,1})
            {
                float x=face*.137f;
                leaf.Box(plate,new(x,0,-.01f),new(.052f,2.96f,1.33f));
                // Two raised diagonal ribs meet across the centre seam when the door seals.
                leaf.Beam(steel,new(x*1.26f,-1.25f,-.56f),new(x*1.26f,-.63f,.48f),.094f,.14f);
                leaf.Beam(steel,new(x*1.26f,1.25f,-.56f),new(x*1.26f,.63f,.48f),.094f,.14f);
                leaf.Box(seal,new(x*1.23f,.25f,0),new(.054f,.28f,.97f));
                leaf.Box(lamp,new(x*1.49f,.25f,0),new(.02f,.080f,.78f));
                leaf.Box(stripe,new(x*1.27f,-.36f,0),new(.045f,.11f,1.29f));
            }
            leaf.Store(Root+"/Meshes/"+(ocean?"Sea":"Cabin")+" pressure gate leaf.asset");
            leaf.node.localPosition=new(ocean?8.65f:0,1.61f,starboard?half:-half);
            if(starboard)leaf.node.localRotation=Quaternion.Euler(0,180,0);
            var col=leaf.node.gameObject.AddComponent<BoxCollider>();
            col.size=new(.25f,3.22f,1.48f);
            return leaf.node;
        }

        static AirlockPad Panel(Transform parent,byte index)
        {
            bool ocean=index>=2;
            int face=index%2==0?-1:1;
            var panel=new ShipSectionMesh(parent,Vector3.zero,(ocean?"Flood":"Drain")+" control "+index,Vector3.zero);
            var dark=Finish("Graphite forged frames"); var plate=Finish("Machined titanium");
            var lamp=Finish(ocean?"Reservoir teal light":"Amber work light");
            panel.Box(dark,Vector3.zero,new(.17f,.94f,.74f));
            panel.Box(plate,new(.086f,0,0),new(.06f,.79f,.60f));
            panel.Box(dark,new(.123f,-.10f,0),new(.082f,.46f,.49f));
            panel.Box(lamp,new(.18f,-.10f,0),new(.065f,.34f,.39f));
            float direction=ocean?-1:1;
            for(int side=-1;side<=1;side+=2)
                panel.Beam(lamp,new(.14f,.22f,side*.16f),new(.14f,.22f+.11f*direction,0),.03f,.038f);
            panel.Store(Root+"/Meshes/"+(ocean?"Flood":"Drain")+" station.asset");
            panel.node.localPosition=new((ocean?8.65f:0)+face*.41f,1.53f,index%2==0?-1.92f:1.92f);
            if(face<0)panel.node.localRotation=Quaternion.Euler(0,180,0);
            var col=panel.node.gameObject.AddComponent<BoxCollider>();
            col.size=new(.33f,.85f,.70f); col.center=new(.066f,0,0);
            var pad=panel.node.gameObject.AddComponent<AirlockPad>();
            pad.Id=index; pad.Sea=ocean;
            return pad;
        }
    }
}
