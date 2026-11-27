using System.Collections.Generic;
using UnityEngine;

namespace Raid.Nereid
{
    internal sealed partial class GoshaView
    {
        readonly Mesh[] junkShapes=new Mesh[6];
        readonly Vector3[][] junkVertices=new Vector3[6][];
        readonly Dictionary<(GoshaJunkKind,int),float> supports=new();
        readonly List<Transform> looseJunk=new();
        readonly List<Transform> builtJunk=new();
        readonly Transform[] shelterParts=new Transform[3];
        Transform den;
        float placeBlend;

        void PresentDen(RaidWorld world,GoshaSnapshot s)
        {
            var vessel=NereidVessel.Active;
            if(!vessel)return;
            var pieces=world.View?.goshaDen;
            int count=pieces?.Count??0;
            if(count>0)
            {
                if(!den)den=Node(transform,"Гошин домик",Vector3.zero);
                den.SetPositionAndRotation(GoshaDenLayout.Origin(vessel),GoshaDenLayout.Rotation(vessel));
                for(int i=builtJunk.Count;i<count;i++)
                {
                    var piece=JunkModel(den,pieces[i],"Гошина находка");
                    var pose=GroundedJunkPose(vessel,pieces[i],i);
                    piece.SetPositionAndRotation(pose.position,pose.rotation);
                    builtJunk.Add(piece);
                }
                for(int stage=0;stage<shelterParts.Length;stage++)
                {
                    if(shelterParts[stage] || count<3+stage*3)continue;
                    shelterParts[stage]=Node(den,stage==0?"Стойки из обломков":stage==1?"Полог из старого брезента":"Рыбацкая сетка и подвески",Vector3.zero);
                    var mesh=ExpeditionLayout.At(world.expeditionLayout?.goshaShelterMeshes,stage);
                    if(!mesh)MeshPart(shelterParts[stage],GoshaDenArt.Shelter(stage));
                    else
                    {
                        shelterParts[stage].gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
                        shelterParts[stage].gameObject.AddComponent<MeshRenderer>().sharedMaterial=shell;
                    }
                }
            }
            placeBlend=s.DenPlacement==0?0:Mathf.MoveTowards(placeBlend,s.DenPlacement/255f,Time.deltaTime*3.3f);
            for(int i=0;i<world.GoshaJunkCount;i++)
            {
                GoshaJunk item=world.GoshaJunkAt(i);
                if(i==looseJunk.Count)looseJunk.Add(JunkModel(transform,item.Kind,"Находка на дне"));
                var view=looseJunk[i];
                view.GetComponent<MeshFilter>().sharedMesh=JunkMesh(item.Kind);
                bool held=item.Location==GoshaJunkLocation.Carried;
                view.gameObject.SetActive(item.Location!=GoshaJunkLocation.Built && (!held || creature.gameObject.activeSelf));
                if(!held)
                {
                    view.SetPositionAndRotation(item.Position,Quaternion.Euler(7,item.Yaw,-5));
                    view.localScale=Vector3.one;
                    continue;
                }

                var carryPoint=body.TransformPoint(JawSocket)+creature.forward*.12f;
                var carryRotation=creature.rotation*Quaternion.Euler(8,15,17);
                var placement=GroundedJunkPose(vessel,item.Kind,count);
                float place=Mathf.SmoothStep(0,1,placeBlend);
                Vector3 point=Vector3.Lerp(carryPoint,placement.position,place)+Vector3.up*(Mathf.Sin(place*Mathf.PI)*.17f);
                view.SetPositionAndRotation(point,Quaternion.Slerp(carryRotation,placement.rotation,place));
                view.localScale=Vector3.one*Mathf.Lerp(.75f,1,place);
            }
            for(int i=world.GoshaJunkCount;i<looseJunk.Count;i++)looseJunk[i].gameObject.SetActive(false);
        }

        Mesh JunkMesh(GoshaJunkKind kind)
        {
            int k=(int)kind;
            if(!junkShapes[k])
            {
                var authored=ExpeditionLayout.At(RaidWorld.Instance?.expeditionLayout?.goshaJunkMeshes,k);
                junkShapes[k]=authored?authored:GoshaDenArt.Found(kind).Mesh(kind.ToString());
                if(!authored)meshes.Add(junkShapes[k]);
                junkVertices[k]=junkShapes[k].vertices;
            }
            return junkShapes[k];
        }

        Pose GroundedJunkPose(NereidVessel vessel,GoshaJunkKind kind,int slot)
        {
            var pose=GoshaDenLayout.Piece(vessel,slot);
            var key=(kind,slot);
            if(!supports.TryGetValue(key,out float bottom))
            {
                JunkMesh(kind);
                Quaternion turn=Quaternion.Inverse(vessel.transform.rotation)*pose.rotation;
                bottom=float.PositiveInfinity;
                foreach(var point in junkVertices[(int)kind])bottom=Mathf.Min(bottom,(turn*point).y);
                supports[key]=bottom;
            }
            return new Pose(pose.position-vessel.transform.up*bottom,pose.rotation);
        }

        Transform JunkModel(Transform parent,GoshaJunkKind kind,string label)
        {
            var node=Node(parent,label,Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh=JunkMesh(kind);
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=shell;
            return node;
        }
    }
}
