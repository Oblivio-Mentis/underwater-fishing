using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class WatchRoomAuthoring
    {
        const string Rooms="Assets/_Game/Nereid/ShipRebuild/Prefabs/Rooms";
        const string Kit=Rooms+"/Dive watch";
        const string HullFinishes="Assets/_Game/Nereid/ShipRebuild/OceanEnvelope/Materials/";
        static Material frame, metal, shell, seat, ochre, enamel, lamp, ink, ice, mineral;
        static readonly List<ShipSectionMesh> groups=new();
        static Transform root;
        static Vector3 origin;

        [MenuItem("NEREID/Fit the science watch and bridge crew stations")]
        public static void Install()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Install the fittings with Play Mode stopped.");
            Ensure(Kit);Ensure(Kit+"/Meshes");
            Material Hull(string name)=>AssetDatabase.LoadAssetAtPath<Material>(HullFinishes+name+".mat");
            frame=Hull("Keel bituminous coat");metal=Hull("Brushed clamp steel");shell=Hull("Oxidised pressure plate");
            seat=Hull("Recessed acoustic rubber");ochre=Hull("Faded safety ochre");enamel=Hull("Weathered petrol enamel");
            lamp=Hull("Amber flood lens");ink=Solid("Chart mineral blue",new Color(.18f,.40f,.46f));
            ice=Solid("Sounding enamel",new Color(.49f,.66f,.63f));mineral=Solid("Sample clay",new Color(.64f,.45f,.33f));
            Fit("ObservationHall",new(0,0,-2),Observation);
            Fit("NavigationBridge",new(0,4.85f,-27.5f),Bridge);
            AssetDatabase.SaveAssets();
        }

        static void Ensure(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }

        static Material Solid(string name,Color tint)
        {
            string path=Kit+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",.19f);m.SetFloat("_Metallic",.09f);
            AssetDatabase.CreateAsset(m,path);return m;
        }

        static void Fit(string roomName,Vector3 center,Action build)
        {
            string label=roomName+" dive watch",kitPath=Kit+"/"+label+".prefab";
            var kit=AssetDatabase.LoadAssetAtPath<GameObject>(kitPath);
            if(!kit)
            {
                var host=new GameObject(label);
                try
                {
                    root=host.transform;origin=center;groups.Clear();build();
                    foreach(var g in groups)g.Store(Kit+"/Meshes/"+g.node.name+".asset");
                    kit=PrefabUtility.SaveAsPrefabAsset(host,kitPath);
                }
                finally {groups.Clear();UnityEngine.Object.DestroyImmediate(host);}
            }
            string roomPath=Rooms+"/"+roomName+".prefab";
            var room=PrefabUtility.LoadPrefabContents(roomPath);
            try
            {
                foreach(Transform child in room.transform)
                    if(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)==kit)return;
                PrefabUtility.InstantiatePrefab(kit,room.transform);
                PrefabUtility.SaveAsPrefabAsset(room,roomPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(room);}
        }

        static ShipSectionMesh Group(string name,Vector3 pivot)
        {
            var mesh=new ShipSectionMesh(root,origin,name,pivot);groups.Add(mesh);return mesh;
        }

        // An extruded cut plate. Face colour and edge colour are chosen independently,
        // so the silhouette reads even under the ship's emergency lighting.
        static void Cut(ShipSectionMesh g,Vector3 c,Vector3 u,Vector3 v,Vector2[] contour,
            float depth,Material face,Material edge)
        {
            var n=Vector3.Cross(u,v).normalized*depth*.5f;
            var a=new Vector3[contour.Length];var b=new Vector3[contour.Length];
            float winding=0;
            for(int i=0;i<contour.Length;i++)
            {
                Vector2 q=contour[i],q1=contour[(i+1)%contour.Length];
                winding+=q.x*q1.y-q1.x*q.y;
                var p=c+u*q.x+v*q.y;a[i]=p-n;b[i]=p+n;
            }
            if(winding<0){Array.Reverse(a);Array.Reverse(b);}
            g.Face(face,b);
            for(int i=0;i<a.Length;i++){int j=(i+1)%a.Length;g.Face(edge,a[i],a[j],b[j],b[i]);}
            Array.Reverse(a);g.Face(face,a);
        }

        static Vector2[] Outline(float width,float height,float chamfer)
        {
            float x=width*.5f,y=height*.5f,k=Mathf.Min(chamfer,Mathf.Min(x,y)*.8f);
            return new Vector2[]{new(-x+k,-y),new(x-k,-y),new(x,-y+k),new(x,y-k),new(x-k,y),new(-x+k,y),new(-x,y-k),new(-x,-y+k)};
        }

        static void WatchChair(ShipSectionMesh g,Vector3 foot,Vector3 forward,bool wide)
        {
            var up=Vector3.up;var f=forward.normalized;var r=Vector3.Cross(up,f);
            var yaw=Quaternion.LookRotation(f);float width=wide?.83f:.68f;
            Vector3 P(float x,float y,float z)=>foot+r*x+up*y+f*z;
            if(wide)
            {
                g.Box(frame,P(0,.075f,0),new(.66f,.15f,.67f),yaw,.15f);
                g.Tube(metal,P(0,.14f,-.03f),P(0,.40f,-.03f),.105f,-1,8);
                g.Box(frame,P(0,.40f,-.005f),new(width,.12f,.64f),yaw,.12f);
                foreach(int sign in new[]{-1,1})
                {
                    g.Beam(frame,P(sign*.34f,.44f,-.12f),P(sign*.40f,.73f,-.05f),.06f,.07f);
                    g.Box(ochre,P(sign*.398f,.73f,.035f),new(.10f,.075f,.34f),yaw,.04f);
                }
            }
            float cushionDepth=wide?.59f:.38f;
            g.Box(seat,P(0,wide?.49f:.47f,0),new(width,.13f,cushionDepth),yaw,.12f);
            var c=P(0,wide?.91f:.75f,-cushionDepth*.48f);
            Vector2[] shape={new(-.32f,-.30f),new(.32f,-.30f),new(.405f,.095f),new(.30f,.28f),new(-.30f,.28f),new(-.405f,.095f)};
            var lift=(up-f*.12f).normalized;
            Cut(g,c,r,lift,shape,.088f,wide?ochre:enamel,frame);
            Cut(g,c+f*.061f,r*.83f,lift*.80f,shape,.054f,seat,shell);
            if(wide)
            {
                g.Box(frame,P(0,1.245f,-.32f),new(.27f,.16f,.11f),yaw);
                g.Box(seat,P(0,1.32f,-.30f),new(.53f,.15f,.17f),yaw,.07f);
                g.Box(ice,P(0,.97f,-.348f),new(.28f,.067f,.026f),yaw,.02f);
                g.Collision(P(0,.57f,0),new(width+.03f,1.12f,.67f),yaw);
            }
        }
    }
}