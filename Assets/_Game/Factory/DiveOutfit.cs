using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    // Rigid suit panels share one mesh per material on each moving joint.
    // The visual rig never creates a collider or modifies the authoritative diver skeleton.
    sealed class DiveOutfit
    {
        public enum Surface { Rubber, Canvas, Enamel, Metal, Glass, Lamp }
        readonly Material[] materials;
        readonly Dictionary<Surface,List<CombineInstance>> parts=new Dictionary<Surface,List<CombineInstance>>();
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        readonly Mesh pebble,plate,round,fin;
        Transform joint;

        public DiveOutfit(HarpoonPresentation source)
        {
            materials=new[]{
                Tint(source.dark,"Neoprene",new Color(.075f,.101f,.115f),.25f),
                Tint(source.amber,"Dive canvas",new Color(.79f,.47f,.13f),.29f),
                Tint(source.amber,"Ceramic yellow",new Color(.99f,.69f,.21f),.49f),
                Tint(source.steel,"Gunmetal",new Color(.35f,.43f,.46f),.6f),
                Tint(source.steel,"Polarised glass",new Color(.055f,.19f,.24f),.91f),
                Tint(source.cyan??source.steel,"Instrument phosphor",new Color(.55f,.95f,.97f),.5f)
            };
            pebble=RingMesh(new[]{-.5f,-.445f,-.30f,0,.30f,.445f,.50f},new[]{.04f,.25f,.42f,.50f,.42f,.25f,.04f},12);
            plate=RingMesh(new[]{-.5f,-.39f,.39f,.5f},new[]{.40f,.50f,.50f,.40f},8,true);
            round=RingMesh(new[]{-.5f,-.45f,.45f,.5f},new[]{.43f,.50f,.50f,.43f},12);
            fin=Wing();
        }

        static Material Tint(Material basis,string name,Color color,float smoothness)
        {
            var material=new Material(basis){name=name,hideFlags=HideFlags.DontSave};
            if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",null);
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            else material.color=color;
            if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",smoothness);
            return material;
        }

        Mesh RingMesh(float[] y,float[] radii,int sides,bool squared=false)
        {
            var v=new List<Vector3>();var triangles=new List<int>();
            for(int j=0;j<y.Length;j++)for(int i=0;i<sides;i++)
            {
                float angle=(i+.5f)*Mathf.PI*2/sides;
                float x=Mathf.Cos(angle),z=Mathf.Sin(angle);
                if(squared){float max=Mathf.Max(Mathf.Abs(x),Mathf.Abs(z));x/=max;z/=max;}
                v.Add(new Vector3(x*radii[j],y[j],z*radii[j]));
                if(j==0)continue;
                int a=(j-1)*sides+i,b=(j-1)*sides+(i+1)%sides,c=a+sides,d=b+sides;
                triangles.Add(a);triangles.Add(c);triangles.Add(b);triangles.Add(b);triangles.Add(c);triangles.Add(d);
            }
            int end=(y.Length-1)*sides;
            for(int i=1;i<sides-1;i++){triangles.Add(0);triangles.Add(i);triangles.Add(i+1);triangles.Add(end);triangles.Add(end+i+1);triangles.Add(end+i);}
            return MeshFrom("Joint profile",v.ToArray(),triangles.ToArray());
        }
        Mesh Wing()
        {
            var pts=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.25f,0,.5f),new Vector3(.25f,0,.5f)};
            var v=new Vector3[8];
            for(int i=0;i<4;i++){v[i]=pts[i]+Vector3.up*.5f;v[i+4]=pts[i]-Vector3.up*.5f;}
            return MeshFrom("Tapered blade",v,new[]{0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5});
        }
        Mesh MeshFrom(string name,Vector3[] vertices,int[] triangles)
        {
            var m=new Mesh{name=name,vertices=vertices,triangles=triangles};m.RecalculateNormals();m.RecalculateBounds();ownedMeshes.Add(m);return m;
        }

        public static Transform Pivot(Transform parent,string name,Vector3 p)
        {
            var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=p;return t;
        }
        public void Bind(Transform target){Flush();joint=target;}

        public void Oval(Surface mat,Vector3 pos,Vector3 scale,Vector3 rotation=default)
            =>Part(pebble,mat,pos,scale,Quaternion.Euler(rotation));
        public void Block(Surface mat,Vector3 pos,Vector3 scale,Vector3 rotation=default)
            =>Part(plate,mat,pos,scale,Quaternion.Euler(rotation));
        public void Blade(Surface mat,Vector3 pos,Vector3 scale,Vector3 rotation=default)
            =>Part(fin,mat,pos,scale,Quaternion.Euler(rotation));
        public void Pipe(Surface mat,Vector3 start,Vector3 end,float diameter)
        {
            Vector3 axis=end-start;
            if(axis.sqrMagnitude>.000001f)Part(round,mat,(start+end)*.5f,new Vector3(diameter,axis.magnitude,diameter),Quaternion.FromToRotation(Vector3.up,axis));
        }
        void Part(Mesh mesh,Surface surface,Vector3 pos,Vector3 scale,Quaternion rot)
        {
            if(!parts.TryGetValue(surface,out var list)){list=new List<CombineInstance>();parts.Add(surface,list);}
            list.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(pos,rot,scale)});
        }
        public void Flush()
        {
            if(joint==null)return;
            foreach(var entry in parts)
            {
                if(entry.Value.Count==0)continue;
                var mesh=new Mesh{name=joint.name+" / "+entry.Key};mesh.CombineMeshes(entry.Value.ToArray(),true,true);
                ownedMeshes.Add(mesh);
                var child=Pivot(joint,entry.Key.ToString(),Vector3.zero);child.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=child.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[(int)entry.Key];
                entry.Value.Clear();
            }
            joint=null;
        }
        public void Release()
        {
            foreach(var mesh in ownedMeshes)if(mesh!=null)Object.Destroy(mesh);
            foreach(var material in materials)if(material!=null)Object.Destroy(material);
        }

        // All hand pieces attach to an independently posed wrist. Local fingers point forwards.
        public void Glove(Transform wrist,int side,float scale)
        {
            Bind(wrist);
            Oval(Surface.Rubber,new Vector3(0,0,.025f)*scale,new Vector3(.092f,.055f,.12f)*scale);
            Block(Surface.Canvas,new Vector3(0,.022f,.018f)*scale,new Vector3(.074f,.016f,.079f)*scale);
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.021f*side;
                float len=(.058f-Mathf.Abs(i-1)*.007f)*scale;
                var knuckle=new Vector3(x,0,.067f)*scale;
                var bend=knuckle+new Vector3(.001f*side,-.012f,.78f*len/scale)*scale;
                Pipe(Surface.Rubber,knuckle,bend,.021f*scale);
                Pipe(Surface.Rubber,bend,bend+new Vector3(0,-.017f,.010f)*scale,.018f*scale);
            }
            Pipe(Surface.Rubber,new Vector3(-side*.036f,0,.01f)*scale,new Vector3(-side*.061f,-.01f,.059f)*scale,.028f*scale);
            Pipe(Surface.Metal,new Vector3(0,0,-.032f)*scale,new Vector3(0,0,-.046f)*scale,.091f*scale);Flush();
        }

    }
}
