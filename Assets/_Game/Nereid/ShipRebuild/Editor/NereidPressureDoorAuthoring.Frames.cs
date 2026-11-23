using UnityEditor;
using UnityEngine;

namespace Raid.Nereid.Editor
{
    public static partial class NereidPressureDoorAuthoring
    {
        static Material Iron => Finish("Graphite forged frames");
        static Material Blue => Finish("Atlantic blue pressure steel");
        static Material Steel => Finish("Machined titanium");
        static Material Rubber => Finish("Reinforced hose rubber");
        static Material Enamel => Finish("Petrol marine enamel");
        static Material Ochre => Finish("Rescue ochre");

        static void FitDoorFrames()
        {
            using(var source=new PrefabUtility.EditPrefabContentsScope(Kit))
            {
                var rig=source.prefabContentsRoot.GetComponent<NereidPressureDoors>();
                if(!rig.cabinFrame) rig.cabinFrame=PressureCasing(rig,false);
                if(!rig.seaFrame) rig.seaFrame=PressureCasing(rig,true);
                foreach(var leaf in new[]{rig.innerPort,rig.innerStarboard,rig.outerPort,rig.outerStarboard})
                    FitDogs(leaf);
            }
            // The game draws rooms in half-scale hull coordinates and the operating doors in metres.
            FitBulkhead("EquipmentRoom","Machinery passage",new Vector3(0,0,3.89f),0,3.08f,5.44f,false);
            FitBulkhead("AftTransfer","Pump room passage",new Vector3(0,0,3.92f),0,3.76f,5.44f,false);
            FitBulkhead("CompanionBerth","Berth entrance",new Vector3(-.55f,0,.31f),90,1.47f,2.78f,true);
        }

        static Transform Group(Transform parent,string label)
        {
            var item=new GameObject(label).transform;item.SetParent(parent,false);return item;
        }

        static Material Pilot(bool sea)
            => Finish(sea?"Process key cyan":"Feed key amber");

        static Transform PressureCasing(NereidPressureDoors rig,bool sea)
        {
            var pivot=Group(rig.transform,sea?"Sea collar and leaf pockets":"Cabin collar and leaf pockets");
            pivot.localPosition=Vector3.right*(sea?8.65f:0);
            pivot.localRotation=Quaternion.Euler(0,90,0);
            var housing=new ShipSectionMesh(pivot,Vector3.zero,"Pressure casting and slide cassettes",Vector3.zero);
            const float half=1.48f, top=3.22f, edge=3.27f, crown=4.15f;
            var lamp=Pilot(sea);
            foreach (int sign in new[]{-1,1})
            {
                float sideX=sign*(half+edge)*.5f;
                // Independent skins on each side leave a continuous recess for the sliding slab.
                foreach (int face in new[]{-1,1})
                {
                    var mount=new Vector3(sideX,crown*.5f,face*.304f);
                    housing.Slab(Blue,mount,new(edge-half,crown,.13f));
                    housing.Box(Enamel,mount+new Vector3(sign*.13f,-.42f,face*.077f),new(1.31f,2.62f,.046f),default,.10f);
                    housing.Box(Iron,new Vector3(sign*2.76f,.61f,face*.39f),new(.67f,.39f,.056f));
                    for(int rib=0;rib<3;rib++)
                        housing.Box(Steel,new Vector3(sign*2.76f,.51f+rib*.094f,face*.432f),new(.46f,.036f,.042f));
                    housing.Box(Ochre,new Vector3(sign*2.61f,2.95f,face*.390f),new(.82f,.093f,.046f));
                    housing.Beam(Steel,new Vector3(sign*2.28f,3.10f,face*.40f),new Vector3(sign*2.83f,3.60f,face*.40f),.12f,.075f);
                }
                housing.Slab(Iron,new Vector3(sign*(edge-.075f),crown*.5f,0),new(.15f,crown,.65f));
            }
            housing.Slab(Blue,new(0,(crown+top)*.5f,0),new(half*2+.09f,crown-top,.68f));
            if(!sea) housing.Slab(Blue,new(0,5.18f,0),new(6.56f,2.1f,.36f));
            AddRim(housing,half,top,lamp,.265f);
            foreach(int face in new[]{-1,1})
            {
                housing.Box(Iron,new(0,top+.52f,face*.375f),new(1.33f,.26f,.085f));
                housing.Box(lamp,new(0,top+.52f,face*.425f),new(.82f,.063f,.018f));
            }
            Sill(housing,half);
            housing.Store(Root+"/Meshes/"+(sea?"Sea collar and double-sided cassettes":"Cabin collar and double-sided cassettes")+".asset");
            return pivot;
        }

        static void FitBulkhead(string roomName,string label,Vector3 offset,float yaw,float half,float top,bool berth)
        {
            using var source=new PrefabUtility.EditPrefabContentsScope(Root+"/Prefabs/Rooms/"+roomName+".prefab");
            var room=source.prefabContentsRoot.transform;
            var existing=room.Find(label);
            if(existing)
            {
                if(berth) {existing.localPosition=offset; BerthCanopy(existing,half,top);}
                return;
            }
            var door=Group(room,label);door.SetLocalPositionAndRotation(offset,Quaternion.Euler(0,yaw,0));
            door.localScale=Vector3.one*.5f;
            var frame=new ShipSectionMesh(door,Vector3.zero,"Door jambs, sill and drive",Vector3.zero);
            var pilot=Pilot(berth);
            AddRim(frame,half,top,pilot,berth?.138f:.225f);
            Sill(frame,half);
            foreach(int side in new[]{-1,1})
            {
                float span=berth?.30f:half+.08f;
                float massX=side*(half+span*.5f);
                // Box the existing partition from both approaches. The open slot looks into a black return.
                foreach(int face in new[]{-1,1})
                {
                    frame.Box(Blue,new(massX,top*.5f,face*.347f),new(span,top,.21f),default,.083f);
                    frame.Box(Enamel,new(massX,top*.55f,face*.473f),new(span*.78f,top*.73f,.052f),default,.13f);
                    frame.Box(Steel,new(side*(half+span*.12f),top*.5f,face*.504f),new(.067f,top*.81f,.057f));
                    if(!berth)
                    {
                        frame.Box(Iron,new(massX,top*.46f,face*.52f),new(span*.70f,.67f,.085f));
                        for(int j=0;j<4;j++)
                            frame.Box(Steel,new(massX,top*.46f-.23f+j*.14f,face*.57f),new(span*.54f,.055f,.033f));
                        frame.Box(Ochre,new(massX,.63f,face*.52f),new(span*.62f,.11f,.052f));
                    }
                }
                frame.Box(Iron,new(side*(half+span),top*.51f,0),new(.12f,top*1.02f,.79f));
            }
            frame.Box(Blue,new(0,top+.35f,0),new(2*half+.38f,.7f,.85f),default,.17f);
            foreach(int face in new[]{-1,1})
            {
                frame.Box(Iron,new(0,top+.34f,face*.48f),new(half*1.50f,.28f,.15f));
                frame.Box(pilot,new(0,top+.35f,face*.57f),new(half*.57f,.057f,.04f));
            }
            frame.Store(Root+"/Meshes/"+label+" - coaming.asset");
            float rest=berth?half-.105f:0;
            var left=PassageLeaf(door,label,-1,rest,berth?.32f:half+.07f,top);
            var right=PassageLeaf(door,label,1,rest,berth?.32f:half+.07f,top);
            if(berth) { BerthCanopy(door,half,top);return; }
            var drive=door.gameObject.AddComponent<NereidPassageDoor>();
            drive.passage=roomName=="EquipmentRoom"?PassageId.Machinery:PassageId.Pump;
            drive.portLeaf=left; drive.starboardLeaf=right;
            drive.portSeal=LeafCollider(left,half+.07f,top);
            drive.starboardSeal=LeafCollider(right,half+.07f,top);
            drive.stroke=half-rest-.13f;
        }

        static BoxCollider LeafCollider(Transform leaf,float width,float top)
        {
            var seal=leaf.gameObject.AddComponent<BoxCollider>();
            seal.center=new(width*.5f,top*.5f,0);
            seal.size=new(width,top-.12f,.23f);
            return seal;
        }

        static Transform PassageLeaf(Transform parent,string label,int side,float rest,float width,float top)
        {
            var lamina=new ShipSectionMesh(parent,Vector3.zero,side<0?"Port sliding leaf":"Starboard sliding leaf",Vector3.zero);
            var p=new Vector3(width*.5f,top*.5f,0);
            lamina.Box(Blue,p,new(width,top-.13f,.22f));
            lamina.Box(Rubber,new(.046f,top*.5f,0),new(.092f,top-.11f,.28f));
            foreach(int face in new[]{-1,1})
            {
                lamina.Box(Enamel,p+Vector3.forward*(face*.125f),new(width*.84f,top-.48f,.044f));
                lamina.Box(Steel,new(.13f,top*.50f,face*.163f),new(.074f,top*.82f,.05f));
                lamina.Box(Ochre,new(width*.51f,.70f,face*.16f),new(width*.83f,.12f,.04f));
                if(width>1)
                {
                    lamina.Beam(Steel,new(.25f,top-.66f,face*.16f),new(width-.18f,top-1.42f,face*.16f),.125f,.075f);
                    lamina.Beam(Steel,new(.25f,1.12f,face*.16f),new(width-.18f,1.76f,face*.16f),.125f,.075f);
                }
            }
            lamina.Store(Root+"/Meshes/"+label+" - retracting leaf.asset");
            lamina.node.localPosition=Vector3.right*(side*rest);
            if(side<0)lamina.node.localRotation=Quaternion.Euler(0,180,0);
            return lamina.node;
        }

        static void BerthCanopy(Transform entry,float half,float top)
        {
            const string name="Berth shelter return";
            var fitted=entry.Find(name);
            if(fitted) { fitted.localScale=new Vector3(1,1,3.52f/3.04f); return; }
            var c=new ShipSectionMesh(entry,Vector3.zero,name,Vector3.zero);
            const float depth=3.04f;
            c.node.localScale=new Vector3(1,1,3.52f/depth);
            c.Box(Blue,new(0,top+.14f,depth*.5f),new(half*2+.24f,.20f,depth),default,.16f);
            foreach(int side in new[]{-1,1})
            {
                float x=side*(half+.075f);
                c.Box(Enamel,new(x,.38f,depth*.5f),new(.14f,.72f,depth),default,.13f);
                c.Beam(Steel,new(x,.77f,.15f),new(x,.77f,depth),.10f,.095f);
                c.Beam(Steel,new(x,top-.10f,.18f),new(x,top-.10f,depth),.085f,.12f);
                c.Box(Iron,new(x,top*.5f,depth-.12f),new(.18f,top,.22f));
                c.Box(Ochre,new(x,top*.48f,depth-.22f),new(.115f,.32f,.09f));
            }
            c.Box(Iron,new(0,top-.012f,depth*.56f),new(.92f,.16f,.37f));
            c.Box(Finish("Dispatch key inlay"),new(0,top-.104f,depth*.56f),new(.69f,.036f,.21f));
            c.Lamp("Berth service light",new Color(1f,.79f,.56f),new(0,top-.21f,depth*.56f),.95f,3.4f);
            c.Store(Root+"/Meshes/Berth shelter return.asset");
        }

        static void FitDogs(Transform leaf)
        {
            const string name="Dog carriers and gasket keepers";
            if(leaf.Find(name)) return;
            var l=new ShipSectionMesh(leaf,Vector3.zero,name,Vector3.zero);
            foreach(int face in new[]{-1,1})
            {
                // Closing-edge dogs, captive tracks and recessed keeper plates move with each leaf.
                l.Box(Iron,new(face*.162f,0,.54f),new(.061f,2.91f,.225f));
                foreach(float height in new[]{-.91f,.86f})
                {
                    l.Box(Steel,new(face*.200f,height,.53f),new(.056f,.37f,.195f));
                    l.Box(Ochre,new(face*.226f,height,.535f),new(.016f,.127f,.134f));
                }
                l.Box(Blue,new(face*.171f,.57f,-.16f),new(.071f,.32f,.91f));
                l.Box(Iron,new(face*.209f,.57f,-.16f),new(.023f,.185f,.64f));
                for(int n=0;n<3;n++)
                    l.Box(Steel,new(face*.228f,.569f,-.35f+n*.17f),new(.014f,.120f,.076f));
                l.Box(Iron,new(face*.171f,-1.22f,-.19f),new(.085f,.24f,.77f));
                l.Box(Steel,new(face*.212f,-1.22f,-.19f),new(.022f,.061f,.54f));
            }
            l.Store(Root+"/Meshes/Pressure gate keeper hardware.asset");
        }

        // Concentric sections form the front bevel, dark gasket and full-depth return on all faces.
        static void AddRim(ShipSectionMesh shell,float half,float top,Material pilot,float width)
        {
            Vector2[] Path(float margin)=>new[]{
                new Vector2(-half-margin,.045f),new Vector2(-half-margin,top-.39f),
                new Vector2(-half+.39f-margin*.4f,top+margin),new Vector2(half-.39f+margin*.4f,top+margin),
                new Vector2(half+margin,top-.39f),new Vector2(half+margin,.045f)};
            Vector3 Point(Vector2 point,float z)=>new(point.x,point.y,z);
            // The left and right rear flanges meet at the midplane; the mirror reverses the face winding.
            var inner=Path(0);var bead=Path(.052f);var shoulder=Path(width);var back=Path(width+.095f);
            for(int i=0;i<inner.Length-1;i++)
            {
                int j=i+1;
                foreach(int face in new[]{-1,1})
                {
                    void Band(Material m,Vector2[] a,float z0,Vector2[] b,float z1)
                    {
                        Vector3 p=Point(a[i],face*z0),q=Point(a[j],face*z0),r=Point(b[j],face*z1),s=Point(b[i],face*z1);
                        if(face>0)shell.Face(m,p,q,r,s);else shell.Face(m,s,r,q,p);
                    }
                    Band(Rubber,inner,.202f,inner,.424f);
                    Band(Steel,inner,.424f,bead,.493f);
                    Band(Iron,bead,.493f,shoulder,.493f);
                    Band(Steel,shoulder,.493f,back,.382f);
                    Band(Blue,back,.382f,back,.245f);
                }
            }
            foreach(int side in new[]{-1,1}) foreach(int face in new[]{-1,1})
            {
                float x=side*(half+width*.65f);
                shell.Box(Ochre,new(x,1.65f,face*.512f),new(width*.61f,.72f,.043f));
                shell.Box(pilot,new(x,1.64f,face*.542f),new(width*.23f,.50f,.026f));
            }
        }

        static void Sill(ShipSectionMesh shell,float half)
        {
            shell.Box(Steel,new(0,.012f,0),new(half*2+.27f,.029f,.86f));
            for(int z=-1;z<=1;z+=2)
            {
                shell.Box(Rubber,new(0,.029f,z*.26f),new(half*2+.16f,.008f,.039f));
                foreach(int side in new[]{-1,1})
                    shell.Box(Ochre,new(side*(half-.16f),.035f,z*.365f),new(.32f,.01f,.084f));
            }
        }
    }
}
