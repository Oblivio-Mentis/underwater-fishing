using System;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    [DefaultExecutionOrder(280)]
    public sealed class LootActor : NetworkBehaviour
    {
        public int tier=1,scrap=2,electronics;
        public long CargoId => cargoId.Value;
        public string Collector { get; private set; }
        public int CarrierOwnerId => carrier.Value == null ? -1 : carrier.Value.OwnerId;
        public bool IsVisible => visible.Value;
        public string DisplayName => tier == 1 ? "Катушка" : "Ящик приборов";
        private static readonly List<LootActor> spawned = new();
        internal static IReadOnlyList<LootActor> VisibleStock => spawned;
        private readonly SyncVar<Vector3> position=new();
        private readonly SyncVar<bool> visible=new();
        private readonly SyncVar<long> cargoId=new();
        private readonly SyncVar<NetworkObject> carrier=new();
        private MeshFilter model;
        private Renderer body;
        private Collider shape;
        private Vector3 spawn;

        private void OnEnable() => spawned.Add(this);
        private void OnDisable() => spawned.Remove(this);

        internal static LootActor HeldBy(DiverMotor diver)
        {
            if (diver == null) return null;
            foreach (var item in spawned)
                if (item.IsVisible && item.IsCarriedBy(diver)) return item;
            return null;
        }

        internal void SetReefBerth(Vector3 positionOnBed)
        {
            transform.position=spawn=positionOnBed;
            if(NetworkObject?.IsServerInitialized==true)position.Value=spawn;
        }

        private void Awake()
        {
            model=GetComponent<MeshFilter>();
            body=GetComponent<Renderer>();
            shape=GetComponent<Collider>();
            spawn=transform.position;
        }

        private static long NewId()=>BitConverter.ToInt64(Guid.NewGuid().ToByteArray(),0)&long.MaxValue;

        public override void OnStartServer() => ResetStock(false);

        public void SetDepth(int depth)
        {
            if(IsServerInitialized && carrier.Value==null)ResetStock(depth==tier);
        }

        private void ResetStock(bool available)
        {
            transform.position=position.Value=spawn;
            cargoId.Value=NewId();
            Collector=null;
            carrier.Value=null;
            visible.Value=available;
            shape.enabled=available;
        }

        internal bool CanPickUp(HarpoonTool tool) => IsVisible && carrier.Value==null && tool!=null
            && tool.CanEquipSalvage && Vector3.Distance(tool.transform.position,transform.position)<=3f;

        public bool Pickup(DiverMotor diver)
        {
            if(!IsServerInitialized || diver==null || !diver.TryGetComponent<HarpoonTool>(out var tool) || !CanPickUp(tool))return false;
            tool.EquipSalvageHands();
            Collector??=RaidWorld.Instance?.ProgressKey(diver);
            position.Value=transform.position;
            carrier.Value=diver.NetworkObject;
            shape.enabled=false;
            return true;
        }

        internal bool PutDown(DiverMotor diver,Vector3 at)
        {
            if(!IsServerInitialized || !IsCarriedBy(diver) || !IsVisible)return false;
            transform.position=position.Value=at;
            carrier.Value=null;
            shape.enabled=true;
            return true;
        }

        internal bool DropAtFeet(DiverMotor diver)
        {
            if (RaidWorld.Instance==null || !IsCarriedBy(diver)) return false;
            Vector3 size=Vector3.Scale(MeshBounds.size,transform.lossyScale);
            float clearance=-MeshBounds.min.y*transform.lossyScale.y;
            Vector3 at=RaidWorld.Instance.PlaceItem(diver,Mathf.Max(size.x,size.z)*.5f,clearance);
            transform.rotation=diver.transform.rotation;
            return PutDown(diver,at);
        }

        public bool IsCarriedBy(DiverMotor diver) => diver!=null && carrier.Value==diver.NetworkObject;

        internal bool LoadInto(HarpoonTool tool,TeamProgress team,ProgressionTuning tuning,long expectedId)
        {
            if(!IsVisible || !IsServerInitialized || CargoId!=expectedId || !IsCarriedBy(tool.Motor))return false;
            var cargo=CatchItem.Salvage(CargoId,tier,scrap,electronics);
            if(Collector!=null)cargo.caughtBy=new[]{Collector};
            if(!team.LoadCargo(cargo,tuning))return false;
            visible.Value=false;
            carrier.Value=null;
            shape.enabled=false;
            return true;
        }

        internal static readonly Vector3 CameraCenter=new(0,-.25f,.48f);
        internal static readonly Vector3 ObserverCenter=new(0,.17f,.54f);
        internal Bounds MeshBounds => model.sharedMesh.bounds;
        internal Vector3 GloveOffset(int side,Vector3 scale)
        {
            var bounds=MeshBounds;
            var extents=bounds.extents;
            var grip=tier==1 ? new Vector3(side*extents.x*.79f,-extents.y*.43f,-extents.z*.63f)
                : new Vector3(side*extents.x*.66f,extents.y*.77f,-extents.z*.38f);
            return Vector3.Scale(bounds.center+grip,scale);
        }

        internal Transform CopyModel(Transform parent,string label)
        {
            var copy=new GameObject(label).transform;
            copy.SetParent(parent,false);
            copy.gameObject.AddComponent<MeshFilter>().sharedMesh=model.sharedMesh;
            copy.gameObject.AddComponent<MeshRenderer>().sharedMaterials=body.sharedMaterials;
            return copy;
        }

        private void FixedUpdate()
        {
            if(!IsServerInitialized || carrier.Value==null)return;
            var diver=carrier.Value.GetComponent<DiverMotor>();
            if(diver!=null && diver.IsServerInitialized && !diver.IsHelpless)
            {
                position.Value=diver.transform.TransformPoint(ObserverCenter);
                return;
            }
            if(DropAtFeet(diver))return;
            transform.position=position.Value;
            carrier.Value=null;
            shape.enabled=IsVisible;
        }

        private void LateUpdate()
        {
            if(!IsClientInitialized)return;
            var diver=carrier.Value==null ? null : carrier.Value.GetComponent<DiverMotor>();
            body.enabled=IsVisible && (diver==null || HarpoonPresentation.ActiveModel?.HasCameraOn(diver)!=true);
            shape.enabled=IsVisible && carrier.Value==null;
            if(diver!=null)
            {
                transform.SetPositionAndRotation(diver.transform.TransformPoint(ObserverCenter),diver.transform.rotation);
            }
            else if(!IsServerInitialized)
                transform.position=Vector3.Lerp(transform.position,position.Value,1-Mathf.Exp(-Time.deltaTime*12));
        }
    }
}
