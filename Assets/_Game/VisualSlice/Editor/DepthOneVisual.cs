using System.Collections.Generic;
using UnityEngine;

namespace Raid.VisualSlice.Editor
{
    /// <summary>Presentation geometry only. Existing seabed, gameplay objects and collisions are untouched.</summary>
    public static class DepthOneVisual
    {
        public static void Build(Transform parent)
        {
            var previous=parent.Find("DepthOneEnvironment");
            if(previous!=null) Object.DestroyImmediate(previous.gameObject);
            var root=VisualKit.Group("DepthOneEnvironment",parent);
            Shelf(root);
            Cliffs(root);
            Wreck(root);
            Vegetation(root);
            CoralGarden(root,new Vector3(-18,-14.3f,26),0);
            CoralGarden(root,new Vector3(16,-15.9f,20),1);
            CoralGarden(root,new Vector3(-15,-15.6f,39),2);
            // Subtle local fill under the shelf. No scene-wide fog, exposure or lighting changes.
            VisualKit.Lamp("D01 reflected cold light",root,new Vector3(0,-10,26),new Color(.16f,.56f,.65f),3f,27f);
        }

        private static void Shelf(Transform root)
        {
            var shelf=VisualKit.Group("Sediment shelf",root);
            // The existing collision floor is at -17.5. This veneer sits just above it.
            VisualKit.Part("Sand veneer",shelf,PrimitiveType.Cube,new Vector3(0,-17.46f,26),new Vector3(83,.035f,80),VisualKit.Mat("Hull"));
            for(int row=0;row<7;row++)
            {
                for(int side=0;side<2;side++)
                {
                    var ripple=VisualKit.MeshObject("D01_SedimentRipple",shelf,Ripple(),VisualKit.Mat("Trim"));
                    ripple.transform.localPosition=new Vector3(side==0?-8.5f:7.3f,-17.425f,15+row*3.9f);
                    ripple.transform.localRotation=Quaternion.Euler(0,side==0?-17:23,0);
                    ripple.transform.localScale=new Vector3(1.2f+(row%3)*.25f,.6f,1);
                }
            }
            // Broken ledges remain below the playable swimming corridor.
            Rock(shelf,"Port low shelf",new Vector3(-13.5f,-17.2f,18),new Vector3(8,2.2f,7),201);
            Rock(shelf,"Starboard low shelf",new Vector3(14,-17,25),new Vector3(9,2.5f,10),202);
            Rock(shelf,"Rear low shelf",new Vector3(-4,-18.6f,43),new Vector3(16,3.3f,9),203);
        }

        private static Mesh Ripple()
        {
            // A small raised sand wave; one reusable mesh rather than a texture or particle layer.
            var vertices=new List<Vector3>();
            var indices=new List<int>();
            const int steps=12;
            for(int i=0;i<=steps;i++)
            {
                float t=(float)i/steps;
                float x=(t-.5f)*9f;
                float z=Mathf.Sin(t*Mathf.PI)*.75f;
                vertices.Add(new Vector3(x,0,z-.26f));
                vertices.Add(new Vector3(x,.045f*Mathf.Sin(t*Mathf.PI),z));
                vertices.Add(new Vector3(x,0,z+.22f));
            }
            for(int i=0;i<steps;i++) for(int section=0;section<2;section++)
            {
                int a=i*3+section,b=a+1,c=a+3,d=b+3;
                indices.AddRange(new[]{a,b,c,b,d,c});
            }
            var mesh=new Mesh{name="Sand ripple"};
            mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }

        private static void Cliffs(Transform root)
        {
            var geology=VisualKit.Group("Grotto walls",root);
            // Large quiet silhouettes frame the forward view. Near surfaces stay outside x +/-13.
            Rock(geology,"Port buttress A",new Vector3(-28,-9,7),new Vector3(8,13,12),211);
            Rock(geology,"Port buttress B",new Vector3(-27,-6,25),new Vector3(7,14,13),212);
            Rock(geology,"Port buttress C",new Vector3(-28,-6,44),new Vector3(10,15,12),213);
            Rock(geology,"Starboard buttress A",new Vector3(30,-8,13),new Vector3(9,13,14),213);
            Rock(geology,"Starboard buttress B",new Vector3(31,-9,35),new Vector3(10,13,14),211);
            Rock(geology,"Starboard buttress C",new Vector3(27,-6,53),new Vector3(10,14,11),212);
            Rock(geology,"Far gate port",new Vector3(-14,-7,53),new Vector3(8,13,10),213);
            Rock(geology,"Far gate starboard",new Vector3(15,-9,55),new Vector3(9,13,11),211);
            Rock(geology,"Far gate lintel",new Vector3(0,5,55),new Vector3(17,4,9),212);
            Rock(geology,"Port broken overhang",new Vector3(-25,7,39),new Vector3(10,4,16),211);
            Rock(geology,"Rear grotto shadow",new Vector3(0,-9,69),new Vector3(30,15,10),213);
            Rock(geology,"Port stepping outcrop",new Vector3(-21,-13,35),new Vector3(5,5,8),212);
            Rock(geology,"Starboard submerged outcrop",new Vector3(23,-14,24),new Vector3(7,4,8),213);
        }

        private static void Rock(Transform parent,string name,Vector3 position,Vector3 scale,int seed)
        {
            VisualKit.Rock(name,parent,position,scale,seed);
        }

        private static void Wreck(Transform root)
        {
            var wreck=VisualKit.Group("Survey wreck / salvage landmark",root,new Vector3(19,-14.5f,35));
            wreck.localRotation=Quaternion.Euler(0,-18,7);
            var rust=VisualKit.Mat("Rust");
            var trim=VisualKit.Mat("Trim");
            var navy=VisualKit.Mat("Navy");
            VisualKit.Part("Broken deck",wreck,PrimitiveType.Cube,new Vector3(0,.2f,0),new Vector3(6.6f,.22f,10),rust);
            for(int i=0;i<4;i++)
            {
                float z=-4+i*2.5f;
                Vector3[] points={new Vector3(-3.3f,.2f,z),new Vector3(-3.3f,2.7f,z),new Vector3(-2.1f,5.2f,z),
                    new Vector3(0,6,z),new Vector3(2.1f,5.2f,z),new Vector3(3.3f,2.7f,z),new Vector3(3.3f,.2f,z)};
                VisualKit.Tube("D01_WreckRib"+i,wreck,points,new[]{.13f,.13f,.15f,.15f,.15f,.13f,.13f},trim,6);
            }
            // Half a hull: missing plates leave an open, legible ribbed silhouette.
            VisualKit.Part("Torn starboard plate",wreck,PrimitiveType.Cube,new Vector3(3.3f,1.5f,-1.6f),new Vector3(.16f,2.7f,5.1f),rust,Quaternion.Euler(0,0,-13));
            VisualKit.Part("Port fragment",wreck,PrimitiveType.Cube,new Vector3(-3.25f,1,-3.2f),new Vector3(.15f,1.8f,2.6f),rust,Quaternion.Euler(4,0,8));
            VisualKit.Part("Aft dark bulkhead",wreck,PrimitiveType.Cube,new Vector3(0,1.5f,4.6f),new Vector3(6.2f,2.7f,.15f),navy);
            for(int i=0;i<3;i++)
                VisualKit.Part("Wreck deck longitudinal",wreck,PrimitiveType.Cube,new Vector3((i-1)*2.4f,.35f,0),new Vector3(.1f,.1f,9.8f),trim);
            VisualKit.Part("Detached forward plate",wreck,PrimitiveType.Cube,new Vector3(-.5f,-.1f,-6.8f),new Vector3(3.5f,.15f,3.8f),rust,Quaternion.Euler(0,13,-8));
            VisualKit.Part("Salvage case",wreck,PrimitiveType.Cube,new Vector3(.8f,.85f,-3),new Vector3(1.7f,1.2f,1.1f),VisualKit.Mat("Amber"));
            VisualKit.Part("Case strap A",wreck,PrimitiveType.Cube,new Vector3(.2f,.86f,-3),new Vector3(.12f,1.25f,1.14f),navy);
            VisualKit.Part("Case strap B",wreck,PrimitiveType.Cube,new Vector3(1.4f,.86f,-3),new Vector3(.12f,1.25f,1.14f),navy);
            VisualKit.Part("Salvage beacon mast",wreck,PrimitiveType.Cylinder,new Vector3(-2.5f,3.3f,-4.2f),new Vector3(.13f,3,.13f),trim);
            VisualKit.Part("Beacon housing",wreck,PrimitiveType.Cube,new Vector3(-2.5f,6.3f,-4.2f),new Vector3(.65f,.45f,.6f),navy);
            VisualKit.Part("Beacon lens",wreck,PrimitiveType.Cube,new Vector3(-2.5f,6.3f,-4.54f),new Vector3(.48f,.24f,.08f),VisualKit.Mat("Light"));
            VisualKit.Lamp("Salvage warm pool",wreck,new Vector3(-2.5f,5.8f,-4.6f),new Color(1,.53f,.19f),8f,14);
            VisualKit.Label("SURVEY  /  01",wreck,new Vector3(0,2.2f,-4.12f),.055f,new Color(.9f,.68f,.4f));
        }

        private static void Vegetation(Transform root)
        {
            var plants=VisualKit.Group("Sparse eelgrass stands",root);
            KelpCluster(plants,new Vector3(-19,-15.9f,17),4.3f,.2f);
            KelpCluster(plants,new Vector3(-21,-15.2f,28),5.8f,1.6f);
            KelpCluster(plants,new Vector3(-18.5f,-15.8f,38),4.7f,3.2f);
            KelpCluster(plants,new Vector3(20,-16.4f,16),4.1f,4.6f);
            KelpCluster(plants,new Vector3(24,-15.8f,27),5.1f,6.2f);
            KelpCluster(plants,new Vector3(16.5f,-17.1f,43),4.2f,7.5f);
            KelpCluster(plants,new Vector3(-8,-17.35f,46),3.3f,8.9f);
        }

        private static void KelpCluster(Transform parent,Vector3 position,float height,float phase)
        {
            var patch=VisualKit.Group("Eelgrass",parent,position);
            var blades=new Transform[5];
            for(int i=0;i<blades.Length;i++)
            {
                var blade=VisualKit.MeshObject("D01_KelpBlade"+(i%3),patch,KelpBlade(i%3),VisualKit.Mat("Kelp"));
                blade.transform.localPosition=new Vector3(Mathf.Cos(i*2.4f)*.45f,0,Mathf.Sin(i*2.4f)*.45f);
                blade.transform.localRotation=Quaternion.Euler(0,i*139+phase*15,0);
                blade.transform.localScale=new Vector3(1,height*(.75f+i*.06f),1);
                blades[i]=blade.transform;
            }
            var sway=patch.gameObject.AddComponent<KelpSway>();sway.blades=blades;sway.phase=phase;sway.amplitudeDegrees=4;sway.frequency=.32f;
        }

        private static Mesh KelpBlade(int variant)
        {
            const int rings=9;
            var vertices=new List<Vector3>();
            var indices=new List<int>();
            for(int side=0;side<2;side++) for(int i=0;i<rings;i++)
            {
                float t=(float)i/(rings-1),width=Mathf.Sin(t*Mathf.PI)*.22f+.012f;
                float bend=Mathf.Sin(t*Mathf.PI*.8f+variant*.3f)*t*.9f;
                float forward=t*t*(.4f+variant*.12f);
                vertices.Add(new Vector3(bend-width,t,forward+(side==0?.005f:-.005f)));
                vertices.Add(new Vector3(bend+width,t,forward+(side==0?.005f:-.005f)));
            }
            for(int side=0;side<2;side++) for(int i=0;i<rings-1;i++)
            {
                int a=side*rings*2+i*2,b=a+1,c=a+2,d=a+3;
                if(side==0)indices.AddRange(new[]{a,b,c,b,d,c});
                else indices.AddRange(new[]{a,c,b,b,c,d});
            }
            var mesh=new Mesh{name="Eelgrass blade"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        private static void CoralGarden(Transform root,Vector3 position,int variant)
        {
            var garden=VisualKit.Group("Bioluminescent coral / "+variant,root,position);
            Rock(garden,"Coral footing",new Vector3(0,-.3f,0),new Vector3(1.9f,.75f,1.5f),220);
            for(int i=0;i<5;i++)
            {
                float angle=i*1.25f;
                Vector3 direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                Vector3[] points={direction*.2f,direction*.5f+Vector3.up*.65f,direction*.8f+Vector3.up*(1.1f+(i%2)*.4f)};
                VisualKit.Tube("D01_CoralBranch"+i,garden,points,new[]{.12f,.08f,.025f},VisualKit.Mat("CreatureLight"),5);
                VisualKit.Part("Coral luminous tip",garden,PrimitiveType.Cube,points[2],new Vector3(.16f,.25f,.16f),VisualKit.Mat("Glow"),Quaternion.Euler(0,i*43,20));
            }
            VisualKit.Lamp("Coral faint spill",garden,new Vector3(0,1.2f,0),new Color(.16f,.8f,.72f),1.3f,6);
        }
    }
}
