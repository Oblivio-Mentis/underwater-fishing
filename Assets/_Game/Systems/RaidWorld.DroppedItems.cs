using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public struct DroppedItem
    {
        public long id;
        public DroppedKind kind;
        public NetworkObject species;
        public string invention;
        public bool handDropped,dead,spoiled,crabCarried;
        public byte defenseSpecies;
        public int depth;
        public Vector3 position;
        public float yaw,pitch,roll;
        public FishActor Fish => species == null ? null : species.GetComponent<FishActor>();
        public string Label => kind==DroppedKind.Invention ? TechCatalog.Find(invention)?.title ?? invention :
            kind==DroppedKind.Harpoon ? "гарпун" : defenseSpecies>0?DefenseCreatureAssets.Name((DefenseSpecies)(defenseSpecies-1)):UI.ReelReadout.Species(Fish?.SpeciesId);
        public static DroppedItem Gun(long id, Vector3 position, float yaw) =>
            new DroppedItem { kind = DroppedKind.Harpoon, id = id, position = position, yaw = yaw };
        // Normal gear and catches have positive instance IDs. A packed invention retains its unique rig serial.
        internal static DroppedItem Packed(TechRig rig) => new DroppedItem
            {kind=DroppedKind.Invention,id=-(long)rig.serial,invention=rig.kind,position=rig.position,yaw=rig.yaw};
    }

    public sealed partial class RaidWorld
    {
        readonly SyncList<DroppedItem> dropped = new();
        readonly Dictionary<long, DroppedItemView> dropViews = new();
        readonly List<long> vanished = new();
        bool dropsDirty;
        TeamProgress lastDropView;
        TeamProgress DropState => NetworkObject && IsServerInitialized?team:view;
        bool VisiblePackedItem(TechRig rig) => rig.mode=="dropped" && rig.depth==CurrentDepth;
        public int DroppedCount
        {
            get
            {
                int count=dropped.Count;
                if(DropState?.rigs!=null)foreach(var rig in DropState.rigs)if(VisiblePackedItem(rig))count++;
                return count;
            }
        }
        public DroppedItem DroppedAt(int index)
        {
            if(index<dropped.Count)return dropped[index];
            index-=dropped.Count;
            if(DropState?.rigs!=null)
                foreach(var rig in DropState.rigs)
                    if(VisiblePackedItem(rig) && index--==0)return DroppedItem.Packed(rig);
            throw new System.ArgumentOutOfRangeException(nameof(index));
        }

        internal bool DropOwnedInvention(HarpoonTool tool,long itemId,string kind,Vector3 at)
        {
            if(!IsServerInitialized || saveFailed || transitionRemaining>0 || !tool)return false;
            var diver=tool.Motor;
            var profile=team?.Player(ProgressKey(diver));
            if(profile==null)return false;
            float yaw=diver.transform.eulerAngles.y;
            string reason;
            bool ok;
            if(itemId<0)
            {
                var rig=tool.CarriedInvention;
                if(rig==null || rig.depth!=CurrentDepth || itemId!=-(long)rig.serial || rig.kind!=kind)return false;
                ok=team.DropCarriedInvention(rig,diver.OwnerId,at,yaw,SaveTransaction,out reason);
            }
            else
            {
                if(itemId!=tool.ActiveSlot)return false;
                ok=team.DropPocketInvention(profile,tool.ActiveSlot-FirstCargoSlot,kind,at,yaw,SaveTransaction,out reason);
            }
            if(ok){dropsDirty=true;Publish();}
            InventoryReceipt(diver,profile,ok,reason);
            return ok;
        }

        internal int TakeLooseInvention(HarpoonTool tool,DroppedItem item)
        {
            if(!IsServerInitialized || saveFailed || transitionRemaining>0 || item.kind!=DroppedKind.Invention || !tool)return -1;
            var diver=tool.Motor;
            var profile=team?.Player(ProgressKey(diver));
            if(profile==null)return -1;
            var rig=team.rigs.Find(r=>-(long)r.serial==item.id && r.kind==item.invention && VisiblePackedItem(r));
            if(rig==null)return -1;
            int stackLimit=team.TechStackLimit(rig.kind);
            int slot=tool.CanFitInvention(tool.ActiveSlot,rig.kind,stackLimit)
                ?tool.ActiveSlot:tool.FindInventionPocket(rig.kind,stackLimit);
            if(slot<FirstCargoSlot)return -1;
            bool ok=team.StoreTechInSlot(profile,rig.serial,slot-FirstCargoSlot,diver.OwnerId,SaveTransaction,out var reason);
            if(ok){dropsDirty=true;Publish();}
            InventoryReceipt(diver,profile,ok,reason);
            return ok?slot:-1;
        }

        int DropIndex(long id)
        {
            for (int i = 0; i < dropped.Count; i++) if (dropped[i].id == id) return i;
            return -1;
        }

        internal bool CanRegisterDrop(long id) => IsServerInitialized && id > 0 && DropIndex(id) < 0;
        internal void RegisterDrop(DroppedItem item)
        {
            item.depth=CurrentDepth;
            if(defenseLoot.TryGetValue(item.id,out var specimen) && DefenseCreatureAssets.ReadKey(specimen.species,out var kind))
            {
                item.defenseSpecies=(byte)((int)kind+1);item.dead=true;item.roll=78;
            }
            if(item.kind==DroppedKind.Catch && blastCatches.Contains(item.id) && !item.dead)
            {
                item.dead=true;
                item.roll=90;
            }
            dropped.Add(item);
            TrackDeadFish(item);
        }
        internal void RemoveDrop(long id)
        {
            int index = DropIndex(id);
            if (index < 0)return;
            dropped.RemoveAt(index);
            looseFishMotion.Remove(id);
        }

        internal Vector3 PlaceItem(DiverMotor diver,float radius=.17f,float clearance=.17f)
        {
            Vector3 eye = diver.transform.position + Vector3.up * .52f;
            Vector3 forward = diver.transform.forward;
            float reach = 1.13f;
            if (Physics.SphereCast(eye, radius, forward, out var wall, reach, ~0, QueryTriggerInteraction.Ignore))
                reach = Mathf.Max(.05f, wall.distance - .08f);
            Vector3 point = eye + forward * reach;
            if (Physics.Raycast(point, Vector3.down, out var floor, 1.85f, ~0, QueryTriggerInteraction.Ignore))
                return floor.point + floor.normal * clearance;
            return point + Vector3.down * .28f;
        }

        public bool TryAimDrop(DiverMotor diver, out DroppedItem item)
        {
            item = default;
            if (diver == null || !diver.TryGetComponent<HarpoonTool>(out var tool)) return false;
            var eye = diver.transform.position + Vector3.up * .6f;
            var aim = diver.AimDirection;
            if (!IsServerInitialized && diver.IsOwner && diver.GetComponentInChildren<Camera>() is Camera camera)
                aim = camera.transform.forward;
            float best = float.PositiveInfinity;
            DroppedItem closest=default;
            void Consider(DroppedItem candidate)
            {
                if(candidate.spoiled || candidate.crabCarried || candidate.depth!=0 && candidate.depth!=CurrentDepth)return;
                Vector3 offset = candidate.position - eye;
                float distance = offset.magnitude;
                if (distance > 3 || distance < .02f || !tool.CanPickUp(candidate)) return;
                float along = Vector3.Dot(offset, aim);
                float radius = candidate.Fish != null && candidate.Fish.RequiresPartner ? .62f : .39f;
                float cross = (offset - aim * along).magnitude;
                if (along <= 0 || cross > radius) return;
                float score = cross + distance * .1f;
                if (score >= best || Physics.Raycast(eye, offset / distance, Mathf.Max(0,distance - .08f), ~0, QueryTriggerInteraction.Ignore)) return;
                best = score;
                closest = candidate;
            }
            foreach(var candidate in dropped)Consider(candidate);
            if(DropState?.rigs!=null)foreach(var rig in DropState.rigs)if(VisiblePackedItem(rig))Consider(DroppedItem.Packed(rig));
            item=closest;
            return item.id != 0;
        }

        internal bool PutDownHeldBall(DiverMotor diver, Vector3 at)
        {
            if (!IsServerInitialized || !GoshaBallInHands(diver) || pet.Holder != diver.OwnerId) return false;
            BeginBallRecovery();
            DropBall(at, Vector3.zero, false);
            gosha.Value = pet;
            return true;
        }

        void StartDroppedViews()
        {
            dropped.OnChange += OnDropsChanged;
            dropsDirty = true;
        }

        void OnDropsChanged(SyncListOperation operation, int index, DroppedItem before, DroppedItem after, bool asServer)
        {
            dropsDirty = true;
        }

        void StopDroppedViews()
        {
            dropped.OnChange -= OnDropsChanged;
            foreach (var view in dropViews.Values) if (view != null) Destroy(view.gameObject);
            dropViews.Clear();
            lastDropView=null;
        }

        void PresentDrops()
        {
            if (!IsClientInitialized || (!dropsDirty && ReferenceEquals(lastDropView,view))) return;
            lastDropView=view;
            dropsDirty = false;
            vanished.Clear();
            vanished.AddRange(dropViews.Keys);
            foreach(var item in dropped)if(item.depth==0 || item.depth==CurrentDepth)ShowDrop(item);
            if(DropState?.rigs!=null)foreach(var rig in DropState.rigs)if(VisiblePackedItem(rig))ShowDrop(DroppedItem.Packed(rig));
            foreach (var id in vanished)
            {
                var old = dropViews[id];
                if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
                dropViews.Remove(id);
            }
        }
        void ShowDrop(DroppedItem item)
        {
            vanished.Remove(item.id);
            if (!dropViews.TryGetValue(item.id, out var model))
            {
                model=new GameObject("Dropped "+item.kind).AddComponent<DroppedItemView>();
                model.transform.SetParent(transform,true);
                dropViews.Add(item.id,model);
            }
            if(!model.Show(item))dropsDirty=true;
        }
    }

    sealed partial class DroppedItemView : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new();
        CatchFishModel fishModel;
        GameObject carcass;
        bool built,dead,beingCarried;
        Vector3 targetPosition;
        Quaternion targetRotation;

        internal bool Show(DroppedItem item)
        {
            targetPosition=item.position;
            beingCarried=item.crabCarried;
            float roll=item.dead?item.roll:item.kind==DroppedKind.Harpoon?76:12;
            targetRotation=Quaternion.Euler(item.pitch,item.yaw,roll);
            if(!built || !item.dead && !beingCarried)transform.SetPositionAndRotation(targetPosition,targetRotation);
            if(!built)
            {
                if(item.kind==DroppedKind.Harpoon)
                {
                    var source=HarpoonPresentation.ActiveModel;
                    if(source==null || !source.MakeWorldModel(transform,ownedMeshes,out _,out _,out _,out _))return false;
                    transform.localScale=Vector3.one*1.48f;
                }
                else if(item.kind==DroppedKind.Invention)
                {
                    if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                        TechRigShell.BuildSaleSample(gameObject,item.invention,.39f);
                }
                else
                {
                    if(!item.Fish)return false;
                    if(item.defenseSpecies>0)carcass=DefenseCreatureAssets.Model((DefenseSpecies)(item.defenseSpecies-1),transform);
                    if(!carcass)fishModel=CatchFishModel.Create(transform,item.Fish.SpeciesId,item.dead?item.id:0).GetComponent<CatchFishModel>();
                    transform.localScale=Vector3.one*(carcass?.82f:item.Fish.RequiresPartner?1.25f:.68f);
                }
                built=true;
            }
            if(item.dead && !dead && (fishModel || carcass))
            {
                dead=true;
                PresentDeadFish();
            }
            return true;
        }

        void OnDestroy()
        {
            foreach(var mesh in ownedMeshes)if(mesh)Destroy(mesh);
        }
    }
}
