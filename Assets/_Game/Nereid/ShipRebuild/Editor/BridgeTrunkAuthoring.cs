using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Raid.Nereid.Editor
{
    public static class BridgeTrunkAuthoring
    {
        const string Root = "Assets/_Game/Nereid/ShipRebuild";
        const string Room = Root + "/Prefabs/Rooms/EquipmentRoom.prefab";
        const string Prefab = Root + "/Prefabs/Rooms/Bridge companionway lining.prefab";
        const float Port = -3.68f, Starboard = -1.46f, Knee = -19.46f, Landing = -24.93f;
        const float Soffit = 3.44f, Gradient = .672f;
        static float Ceiling(float z) => Soffit + Mathf.Max(0,Knee - z)*Gradient;
        static Vector3 At(float x, float z, float drop = 0) => new(x,Ceiling(z)-drop,z);
        static Material Finish(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+name+".mat");

        [MenuItem("Tools/NEREID/Fit bridge companionway lining")]
        public static void Install()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before fitting the stair trunk.");
            var room = PrefabUtility.LoadPrefabContents(Room);
            try
            {
                if (!Attach(room.transform)) return;
                PrefabUtility.SaveAsPrefabAsset(room,Room);
            }
            finally { PrefabUtility.UnloadPrefabContents(room); }
        }

        internal static bool Attach(Transform room)
        {
            var liner = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            if (!liner)
            {
                var host = new GameObject("Bridge companionway lining");
                try
                {
                    Build(host.transform);
                    liner = PrefabUtility.SaveAsPrefabAsset(host,Prefab);
                }
                finally { Object.DestroyImmediate(host); }
            }
            if (room.Cast<Transform>().Any(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == liner)) return false;
            var previous = room.Find("Sloped companionway overhead");
            if (previous) previous.gameObject.SetActive(false);
            PrefabUtility.InstantiatePrefab(liner,room);
            return true;
        }

        static void Build(Transform root)
        {
            Vector3 origin = new(0,0,-20);
            var paint = Finish("Petrol marine enamel");
            var plate = Finish("Atlantic blue pressure steel");
            var frame = Finish("Graphite forged frames");
            var metal = Finish("Machined titanium");
            var stripe = Finish("Rescue ochre");
            var lamp = Finish("Amber work light");
            var casing = new ShipSectionMesh(root,origin,"Companionway pressure trunk",new(-2.58f,3.52f,-20));
            var flanges = new ShipSectionMesh(root,origin,"Machinery ceiling coamings",new(-1.46f,3.49f,-20));
            var ribs = new ShipSectionMesh(root,origin,"Companionway frames and lighting",new(-2.58f,4.45f,-21.45f));

            void Deck(float lo,float hi,float aft,float forward)
            {
                casing.Box(plate,new((lo+hi)/2,Soffit+.11f,(aft+forward)/2),new(hi-lo,.22f,forward-aft),default,0);
            }
            // The level overhead closes the low end of the cut-out. From the knee aft,
            // the two cheeks and sloping crown meet at their outer surfaces.
            Deck(-4.34f,4.34f,Knee-.055f,-16.02f);
            Deck(-4.34f,Port+.045f,Landing,Knee+.03f);
            Deck(Starboard-.045f,4.34f,Landing,Knee+.03f);
            Cheek(casing,paint,Port-.095f,Port+.018f);
            Cheek(casing,paint,Starboard-.018f,Starboard+.095f);
            var centre=(Port+Starboard)/2;
            var low=At(centre,Knee+.025f);
            var high=At(centre,Landing-.07f);
            var axis=Quaternion.LookRotation(high-low);
            var normal=axis*Vector3.up;
            casing.Box(plate,(low+high)*.5f+normal*.105f,new(Starboard-Port+.25f,.22f,(high-low).magnitude+.16f),axis,0);
            // The deck above the aft bulkhead was previously visible edge-on with an
            // open strip underneath. The fascia ends below the landing tread.
            casing.Box(paint,new(0,4.085f,-25.015f),new(8.66f,1.41f,.22f),default,0);
            flanges.Box(frame,new(0,3.43f,-24.89f),new(8.55f,.16f,.18f));
            flanges.Box(metal,new(-2.57f,4.75f,-24.94f),new(2.29f,.10f,.19f));
            foreach (float x in new[]{Port,Starboard})
            {
                float sign = x == Port ? -1 : 1;
                flanges.Box(frame,new(x+sign*.035f,Soffit-.035f,(Knee+Landing)/2),new(.22f,.20f,Knee-Landing+.19f));
                flanges.Box(metal,new(x,Soffit-.078f,(Knee+Landing)/2),new(.25f,.046f,Knee-Landing+.22f));
                flanges.Beam(frame,At(x,Knee),At(x,Landing),.19f,.17f);
                flanges.Beam(metal,At(x-sign*.012f,Knee,.066f),At(x-sign*.012f,Landing,.066f),.094f,.045f);
            }
            flanges.Box(frame,new(centre,Soffit-.03f,Knee),new(Starboard-Port+.27f,.17f,.22f));
            // Closed transverse frames wrap down the sides and bear on the flat ceiling coaming.
            foreach (float z in new[]{-20.37f,-22.40f,-24.53f})
            {
                var top=At(centre,z,.077f);
                ribs.Box(frame,top,new(Starboard-Port+.12f,.145f,.20f),axis,.024f);
                foreach (float x in new[]{Port,Starboard})
                {
                    float side=x==Port?1:-1;
                    var end=At(x+side*.04f,z,.11f);
                    ribs.Beam(frame,new(x+side*.04f,Soffit-.025f,z),end,.128f,.205f);
                    ribs.Beam(metal,end+Vector3.up*.015f,end+new Vector3(side*.24f,-.18f,0),.078f,.165f);
                    ribs.Box(stripe,end+new Vector3(side*.060f,-.245f,0),new(.035f,.145f,.102f));
                }
                var housing=top-normal*.101f;
                ribs.Box(metal,housing,new(.78f,.085f,.172f),axis,.012f);
                ribs.Box(lamp,housing-normal*.047f,new(.62f,.018f,.115f),axis,.01f);
            }
            // Give the two flat machinery luminaires a visible bearing surface back to the shell.
            foreach (float z in new[]{-18.77f,-22.10f})
                ribs.Box(frame,new(.22f,3.438f,z),new(1.86f,.115f,.56f));
            ribs.Lamp("Lower stair worklight",new Color(.80f,.86f,.88f),new(-2.52f,3.22f,-19.92f),7.5f,6.5f);
            ribs.Lamp("Upper stair worklight",new Color(.97f,.81f,.59f),new(-2.58f,5.75f,-23.57f),8.5f,6.5f);
            foreach (var part in new[]{casing,flanges,ribs})
                part.Store(Root+"/Meshes/"+part.node.name+".asset");
            var solid=casing.node.gameObject.AddComponent<MeshCollider>();
            solid.sharedMesh=casing.node.GetComponent<MeshFilter>().sharedMesh;
        }

        static void Cheek(ShipSectionMesh shell,Material finish,float left,float right)
        {
            float front=Knee+.06f, aft=Landing-.045f, bottom=Soffit-.06f;
            Vector3[] Profile(float x) => new[]{new Vector3(x,bottom,front), new(x,bottom,aft),
                new(x,Ceiling(aft)+.16f,aft), new(x,Ceiling(front)+.16f,front)};
            var a=Profile(left);
            var b=Profile(right);
            shell.Face(finish,b);
            for(int i=0;i<4;i++)
            {
                int j=(i+1)%4;
                shell.Face(finish,a[i],a[j],b[j],b[i]);
            }
            Array.Reverse(a);
            shell.Face(finish,a);
        }
    }
}
