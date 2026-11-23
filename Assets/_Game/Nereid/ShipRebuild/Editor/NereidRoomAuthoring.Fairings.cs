using System;
using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidRoomAuthoring
    {
        // Incremental option for already installed RAID-020 ships. Only adds the new modular
        // fairing group to its source prefab. Original room meshes and instance overrides survive.
        [MenuItem("RAID/020/Fit NEREID stern window returns")]
        public static void CompletePressureFairings()
        {
            string path=HullPath+"/PressureHull.prefab";
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                throw new InvalidOperationException("Install the NEREID modular rooms before fitting the stern jackets.");
            Palette();
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                const string group="Hull coamings and stern inserts";
                if(prefab.transform.Find(group)==null)
                {
                    parts.Clear();
                    origin=new Vector3(0,0,-9);
                    room=new GameObject(group).transform;room.SetParent(prefab.transform,false);
                    foreach(int side in new[]{-1,1})
                    {
                        var s=Part(side<0?"Port machinery saddle":"Starboard machinery saddle",new(side*5.35f,3.40f,-20.80f));
                        // At each station the armored sill actually sweeps up to the elevated bridge.
                        // On the port side only, a short recessed port aligns with the glazed equipment bay.
                        Vector3 Edge(float z,bool high)
                        {
                            for(int i=0;i<frames.Length-1;i++)
                            {
                                Frame a=frames[i],b=frames[i+1];
                                if(z<a.z || z>b.z)continue;
                                float t=Mathf.InverseLerp(a.z,b.z,z);
                                return new Vector3(side*Mathf.Lerp(a.width,b.width,t),Mathf.Lerp(high?a.brow:a.sill,high?b.brow:b.sill,t),z);
                            }
                            throw new ArgumentOutOfRangeException(nameof(z));
                        }
                        void Skin(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material m)
                        {
                            if(Vector3.Cross(b-a,c-a).x*side<0)s.Face(m,a,d,c,b);
                            else s.Face(m,a,b,c,d);
                        }
                        float[] cuts={-24.76f,-23.02f,-21.80f,-21.2f,-20.65f,-19.04f,-17.04f,-16.12f};
                        for(int i=0;i<cuts.Length-1;i++)
                        {
                            float z=cuts[i],next=cuts[i+1];
                            var a=Edge(z,false);var b=Edge(next,false);var c=Edge(next,true);var d=Edge(z,true);
                            bool port=side<0 && z>=-21.80f && next<=-20.65f;
                            Material plate=i%3==0?enamel:hull;
                            if(port)
                            {
                                var e=new Vector3(a.x,1.61f,z);var f=new Vector3(b.x,1.61f,next);
                                var g=new Vector3(c.x,2.64f,next);var h=new Vector3(d.x,2.64f,z);
                                Skin(a,b,f,e,plate);Skin(h,g,c,d,plate);
                                foreach(var pair in new[]{(e,f),(h,g)})
                                {
                                    var innerA=new Vector3(side*4.29f,pair.Item1.y,pair.Item1.z);
                                    var innerB=new Vector3(side*4.29f,pair.Item2.y,pair.Item2.z);
                                    s.Face(dark,pair.Item1,pair.Item2,innerB,innerA);s.Face(dark,innerA,innerB,pair.Item2,pair.Item1);
                                    s.Beam(steel,pair.Item1,pair.Item2,.075f,.095f);
                                }
                                if(i==2||i==3)
                                {
                                    var j=i==2?e:f;var k=i==2?h:g;
                                    var l=new Vector3(side*4.29f,k.y,k.z);var m=new Vector3(side*4.29f,j.y,j.z);
                                    s.Face(dark,j,k,l,m);s.Face(dark,m,l,k,j);s.Beam(steel,j,k,.080f,.095f);
                                }
                            }
                            else
                            {
                                Skin(a,b,c,d,plate);
                                s.Beam(dark,a,d,.10f,.13f);
                                var p=Vector3.Lerp(a,d,.38f);var q=Vector3.Lerp(b,c,.38f);
                                s.Beam(steel,p,q,.056f,.062f);
                                if(i%2==0)s.Beam(ochre,Vector3.Lerp(p,q,.05f),Vector3.Lerp(p,q,.23f),.069f,.077f);
                            }
                        }
                        var rail=Part(side<0?"Port glazing returns":"Starboard glazing returns",new(side*5.85f,3,-3));
                        void Return(float za,float zb,float innerX,float lower,float upper)
                        {
                            var a=Edge(za,false);var b=Edge(zb,false);
                            var c=Edge(zb,true);var d=Edge(za,true);
                            foreach(var pair in new[]{(a,b,lower),(d,c,upper)})
                            {
                                var i=new Vector3(innerX*side,pair.Item3,za);var j=new Vector3(innerX*side,pair.Item3,zb);
                                rail.Face(enamel,pair.Item1,pair.Item2,j,i);
                                rail.Face(enamel,i,j,pair.Item2,pair.Item1);
                                rail.Beam(steel,pair.Item1,pair.Item2,.09f,.11f);
                            }
                        }
                        Return(-30.52f,-27.60f,4.37f,5.65f,6.83f);Return(-27.60f,-24.76f,4.37f,5.65f,6.83f);
                        foreach(var span in new[]{(-8.06f,-5.46f),(-5.46f,-.54f),(-.54f,4.52f),(4.52f,8.12f)})
                            Return(span.Item1,span.Item2,5.99f,.75f,3.97f);
                    }
                    var end=Part("Aft machinery pressure cap",new(0,2.2f,-34.4f));
                    var aEnd=frames[0].Profile(-1);var bEnd=frames[0].Profile(1);
                    for(int i=1;i<6;i++)end.Face(hull,aEnd[i],aEnd[i+1],bEnd[i+1],bEnd[i]);
                    end.Face(dark,aEnd[0],aEnd[1],bEnd[1]);
                    end.Face(dark,aEnd[6],aEnd[7],bEnd[6]);
                    foreach(var p in parts)p.Store(Root+"/Meshes/Fairing_"+p.node.name+".asset");
                    PrefabUtility.SaveAsPrefabAsset(prefab,path);
                }
            }
            finally { parts.Clear();PrefabUtility.UnloadPrefabContents(prefab); }
            // Old stock clearcoat values produced opaque-looking point-light discs in the glass.
            // Leave non-stock smoothness and any other artist modifications alone.
            foreach(var m in new[]{glass,water})
            {
                var baseColor=m.GetColor("_BaseColor");
                float stock=m==glass?.82f:.62f;
                float stockAlpha=m==glass?.13f:.10f;
                if(!Mathf.Approximately(m.GetFloat("_Smoothness"),stock)||!Mathf.Approximately(baseColor.a,stockAlpha))continue;
                m.SetFloat("_SpecularHighlights",0);m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                m.SetFloat("_EnvironmentReflections",0);m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
