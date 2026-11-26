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
        private readonly SyncVar<bool> goshaCarried=new();
        public bool CarriedByGosha => goshaCarried.Value;
        /// <summary>Lying on the seabed: nobody and no pet holds it.</summary>
        public bool IsFreeOnSeabed => IsVisible && carrier.Value==null && !goshaCarried.Value;
        public int CarryWeight => tier==1 ? 1 : 2;
        private MeshFilter model;
        private Renderer body;
        private Collider shape;
        private Vector3 spawn;
        bool inGoshaBerth;

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
            spawn=positionOnBed;
            if(inGoshaBerth)return;
            transform.position=spawn;
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
            if(IsServerInitialized && carrier.Value==null && (!inGoshaBerth || !IsVisible))ResetStock(depth==tier);
        }

        private void ResetStock(bool available)
        {
            inGoshaBerth=false;
            transform.position=position.Value=spawn;
            cargoId.Value=NewId();
            Collector=null;
            carrier.Value=null;
            goshaCarried.Value=false;
            visible.Value=available;
            shape.enabled=available;
        }

        /// <summary>Server: the pet grabbed this item off the seabed. One holder at a time.</summary>
        internal bool TakeByGosha()
        {
            if(!IsServerInitialized || !IsFreeOnSeabed)return false;
            goshaCarried.Value=true;
            shape.enabled=false;
            return true;
        }

        internal void CarryByGosha(Vector3 at,Quaternion rotation)
        {
            if(!IsServerInitialized || !goshaCarried.Value)return;
            transform.SetPositionAndRotation(at,rotation);
            position.Value=at;
        }

        /// <summary>The pet leaves the item where it is; it stays a real pickup.</summary>
        internal bool PutDownByGosha(Vector3 at,bool berth=false)
        {
            if(!IsServerInitialized || !goshaCarried.Value)return false;
            float clearance=-MeshBounds.min.y*transform.lossyScale.y;
            if(Physics.Raycast(at+Vector3.up*.5f,Vector3.down,out var hit,6f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                at=hit.point+Vector3.up*clearance;
            inGoshaBerth=berth;
            transform.position=position.Value=at;
            transform.rotation=Quaternion.Euler(0,transform.eulerAngles.y,0);
            goshaCarried.Value=false;
            shape.enabled=IsVisible;
            return true;
        }

        /// <summary>Hands the carried item to a diver with free hands, as a normal pickup would.</summary>
        internal bool HandOverFromGosha(DiverMotor diver)
        {
            if(!IsServerInitialized || !goshaCarried.Value || diver==null || !diver.TryGetComponent<HarpoonTool>(out var tool) ||
               !tool.CanEquipSalvage || diver.IsHelpless || diver.IsTrapped)return false;
            tool.EquipSalvageHands();
            goshaCarried.Value=false;
            position.Value=transform.position;
            carrier.Value=diver.NetworkObject;
            shape.enabled=false;
            return true;
        }

        /// <summary>The dry hatch of the pet's receiver: the same manifest gate the hoist uses.</summary>
        internal bool DepositFromGosha(TeamProgress team,ProgressionTuning tuning)
        {
            if(!IsVisible || !IsServerInitialized || !goshaCarried.Value)return false;
            if(!team.Deposit(CatchItem.Salvage(CargoId,tier,scrap,electronics),tuning))return false;
            visible.Value=false;
            goshaCarried.Value=false;
            shape.enabled=false;
            return true;
        }

        /// <summary>Loads the carried item into the open cage through the team manifest.</summary>
        internal bool LoadFromGosha(TeamProgress team,ProgressionTuning tuning)
        {
            if(!IsVisible || !IsServerInitialized || !goshaCarried.Value)return false;
            var cargo=CatchItem.Salvage(CargoId,tier,scrap,electronics);
            if(!team.LoadCargo(cargo,tuning))return false;
            visible.Value=false;
            goshaCarried.Value=false;
            shape.enabled=false;
            return true;
        }

        internal bool FromEquipmentToHands(CatchItem item,DiverMotor diver)
        {
            if (!IsServerInitialized || IsVisible || !item.IsSalvage || item.id!=CargoId || tier!=item.depthTier ||
                !diver.TryGetComponent<HarpoonTool>(out var tool) || !tool.CanEquipSalvage) return false;
            tool.EquipSalvageHands();
            position.Value=diver.transform.position;
            carrier.Value=diver.NetworkObject;
            visible.Value=true;
            shape.enabled=false;
            return true;
        }

        internal bool PullWithMagnet(Vector3 towards, float step)
        {
            if (!IsServerInitialized || !IsFreeOnSeabed || !RaidWorld.IndustryCanPull(transform.position, towards, .12f)) return false;
            transform.position = position.Value = Vector3.MoveTowards(transform.position, towards, step);
            return true;
        }

        internal bool IntoEquipment(List<CatchItem> destination, int freeSpace, DiverMotor unloading = null)
        {
            if (!IsServerInitialized || !IsVisible || CarryWeight > freeSpace || destination.Exists(c => c.id == CargoId)) return false;
            if (unloading ? !IsCarriedBy(unloading) : !IsFreeOnSeabed) return false;
            var cargo = CatchItem.Salvage(CargoId, tier, scrap, electronics);
            cargo.cargoSize = CarryWeight;
            if (!string.IsNullOrEmpty(Collector)) cargo.caughtBy = new[] { Collector };
            destination.Add(cargo);
            visible.Value = false;
            carrier.Value = null;
            shape.enabled = false;
            return true;
        }

        internal bool CanPickUp(HarpoonTool tool) => IsFreeOnSeabed && tool!=null
            && tool.CanEquipSalvage && Vector3.Distance(tool.transform.position,transform.position)<=3f;

        public bool Pickup(DiverMotor diver)
        {
            if(!IsServerInitialized || diver==null || !diver.TryGetComponent<HarpoonTool>(out var tool) || !CanPickUp(tool))return false;
            tool.EquipSalvageHands();
            inGoshaBerth=false;
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
            shape.enabled=IsVisible && carrier.Value==null && !goshaCarried.Value;
            if(diver!=null)
            {
                transform.SetPositionAndRotation(diver.transform.TransformPoint(ObserverCenter),diver.transform.rotation);
            }
            else if(!IsServerInitialized)
                transform.position=Vector3.Lerp(transform.position,position.Value,1-Mathf.Exp(-Time.deltaTime*12));
        }
    }
}
