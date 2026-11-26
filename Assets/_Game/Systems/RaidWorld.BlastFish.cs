using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        struct LooseFishMotion
        {
            public Vector3 Position, Velocity;
            public float Age, NextSample, Yaw, Turn, Side, Away;
            public bool Settled;
        }

        readonly Dictionary<long,LooseFishMotion> looseFishMotion=new();
        readonly HashSet<long> blastCatches=new();
        const float CorpseRadius=.082f, CorpseNoticeDistance=28f, CorpseCleanUpDelay=12f;
        readonly RaycastHit[] corpseHits=new RaycastHit[8];

        void TrackDeadFish(DroppedItem item)
        {
            if(item.dead && !looseFishMotion.ContainsKey(item.id))
                looseFishMotion[item.id]=new LooseFishMotion
                {
                    Position=item.position,Yaw=item.yaw,Side=Mathf.Sign(item.roll),Age=.5f
                };
        }

        void RegisterBlastedFish(long id,FishActor fish,Vector3 position,float yaw,Vector3 origin,float radius,bool spoiled)
        {
            var corpse=new DroppedItem
            {
                id=id, species=fish.NetworkObject, kind=DroppedKind.Catch, position=position, yaw=yaw,
                dead=true, spoiled=spoiled
            };
            StartFishDrift(ref corpse,origin,radius);
            RegisterDrop(corpse);
        }

        void PushLooseFish(Vector3 origin,float radius)
        {
            for(int i=0;i<dropped.Count;i++)
            {
                var item=dropped[i];
                if(item.kind!=DroppedKind.Catch || item.spoiled || item.crabCarried || item.depth!=CurrentDepth || !item.Fish ||
                    Vector3.Distance(item.position,origin)>radius || !ClearGearPath(origin,item.position))continue;
                StartFishDrift(ref item,origin,radius);
                dropped[i]=item;
            }
        }

        void StartFishDrift(ref DroppedItem item,Vector3 origin,float radius)
        {
            var offset=item.position-origin;
            var outward=Vector3.ProjectOnPlane(offset,Vector3.up);
            if(outward.sqrMagnitude<.01f)outward=Vector3.forward;
            var current=Quaternion.AngleAxis(Random.Range(-24f,24f),Vector3.up)*outward.normalized;
            var launch=(current+Vector3.up*Random.Range(.28f,.66f)).normalized;
            float side=Random.value<.5f?-1:1;
            float force=ActiveFisheryTuning.Get.chargeCorpseSpeed*Mathf.Lerp(.52f,1,1-Mathf.Clamp01(offset.magnitude/radius));
            item.dead=true;
            if(!item.spoiled)blastCatches.Add(item.id);
            item.roll=side*55;
            looseFishMotion[item.id]=new LooseFishMotion
            {
                Position=item.position,Velocity=launch*force,Yaw=item.yaw,Side=side,Turn=Random.Range(-27f,27f)
            };
        }

        void TickLooseFish(float dt)
        {
            if(looseFishMotion.Count==0)return;
            for(int i=dropped.Count-1;i>=0;i--)
            {
                var item=dropped[i];
                if(!looseFishMotion.TryGetValue(item.id,out var motion))continue;
                motion.Age+=dt;
                bool nearby=false;
                foreach(var diver in DiverMotor.ServerDivers)
                    if(diver && !diver.IsDead && (diver.transform.position-motion.Position).sqrMagnitude<CorpseNoticeDistance*CorpseNoticeDistance)
                    {nearby=true;break;}
                motion.Away=nearby?0:motion.Away+dt;
                if(motion.Away>=CorpseCleanUpDelay)
                {
                    RemoveDrop(item.id);
                    continue;
                }
                if(!motion.Settled)
                {
                    motion.Velocity*=Mathf.Exp(-2.12f*dt);
                    motion.Velocity.y-=.94f*dt;
                    Vector3 step=motion.Velocity*dt;
                    float distance=step.magnitude;
                    if(distance>=.0001f)
                    {
                        var direction=step/distance;
                        int hits=Physics.SphereCastNonAlloc(motion.Position,CorpseRadius,direction,corpseHits,distance,
                            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                        float nearest=distance;
                        Vector3 normal=Vector3.zero;
                        for(int h=0;h<hits;h++)
                        {
                            var hit=corpseHits[h];
                            if(hit.distance>nearest || hit.collider.GetComponentInParent<DiverMotor>() ||
                                hit.collider.GetComponentInParent<FishActor>())continue;
                            nearest=hit.distance;
                            normal=hit.normal;
                        }
                        motion.Position+=direction*Mathf.Max(0,nearest-(normal==Vector3.zero?0:.003f));
                        if(normal!=Vector3.zero)
                        {
                            motion.Velocity=Vector3.ProjectOnPlane(motion.Velocity,normal)*.53f;
                            motion.Settled=normal.y>.55f && motion.Age>.5f && motion.Velocity.sqrMagnitude<.04f;
                        }
                    }
                    if(motion.Age>=motion.NextSample || motion.Settled)
                    {
                        item.position=motion.Position;
                        item.roll=motion.Side*Mathf.SmoothStep(55,91,Mathf.InverseLerp(0,.46f,motion.Age));
                        item.pitch=motion.Side*11*Mathf.Sin(motion.Age*5.5f)*Mathf.Exp(-2.2f*motion.Age);
                        item.yaw=motion.Yaw+motion.Turn*(1-Mathf.Exp(-2*motion.Age));
                        motion.NextSample=motion.Age+.085f;
                        dropped[i]=item;
                    }
                }
                looseFishMotion[item.id]=motion;
            }
        }
    }

    sealed partial class DroppedItemView
    {
        static Material outlineMaterial;
        MeshRenderer[] corpseOutline;
        MaterialPropertyBlock outlineParameters;
        static readonly int OutlineOpacity=Shader.PropertyToID("_Opacity");

        void PresentDeadFish()
        {
            fishModel.enabled=false;
            var surfaces=fishModel.GetComponentsInChildren<MeshRenderer>();
            var ink=new MaterialPropertyBlock();
            foreach(var skin in surfaces)
            {
                var materials=skin.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    if(!materials[i])continue;
                    var color=materials[i].GetColor("_BaseColor");
                    var grey=new Color(color.grayscale,color.grayscale,color.grayscale);
                    ink.SetColor("_BaseColor",Color.Lerp(color,grey,.66f)*.77f);
                    ink.SetColor("_EmissionColor",Color.Lerp(grey,new Color(.13f,.19f,.23f),.7f)*.22f);
                    skin.SetPropertyBlock(ink,i);
                }
            }

            if(!outlineMaterial)
            {
                var shader=Resources.Load<Shader>("RaidFishOutline");
                if(!shader)return;
                outlineMaterial=new Material(shader){name="Catch · white proximity outline",hideFlags=HideFlags.DontSave};
            }
            outlineParameters=new MaterialPropertyBlock();
            corpseOutline=new MeshRenderer[surfaces.Length];
            for(int i=0;i<surfaces.Length;i++)
            {
                var skin=surfaces[i];
                var shell=new GameObject("Proximity outline");
                shell.transform.SetParent(skin.transform,false);
                var mesh=skin.GetComponent<MeshFilter>().sharedMesh;
                shell.AddComponent<MeshFilter>().sharedMesh=mesh;
                var outline=shell.AddComponent<MeshRenderer>();
                var materials=new Material[mesh.subMeshCount];
                System.Array.Fill(materials,outlineMaterial);
                outline.sharedMaterials=materials;
                outline.shadowCastingMode=ShadowCastingMode.Off;
                outline.receiveShadows=false;
                outline.enabled=false;
                corpseOutline[i]=outline;
            }
        }

        void LateUpdate()
        {
            if(!dead && !beingCarried)return;
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*24);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position,targetPosition,blend),
                Quaternion.Slerp(transform.rotation,targetRotation,blend));
            if(corpseOutline==null)return;
            var diver=UI.UiRoot.Instance?.Context.Player;
            float distance=diver && !diver.IsDead?Vector3.Distance(diver.transform.position,transform.position):10;
            float intensity=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(6.5f,12.5f,distance));
            outlineParameters.SetFloat(OutlineOpacity,intensity);
            foreach(var outline in corpseOutline)
            {
                outline.enabled=intensity>.01f;
                if(outline.enabled)outline.SetPropertyBlock(outlineParameters);
            }
        }
    }
}
