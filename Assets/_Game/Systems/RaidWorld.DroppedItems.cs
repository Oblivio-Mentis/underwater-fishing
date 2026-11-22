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
        public Vector3 position;
        public float yaw;
        public FishActor Fish => species == null ? null : species.GetComponent<FishActor>();
        public string Label => kind == DroppedKind.Harpoon ? "гарпун" : UI.ReelReadout.Species(Fish?.SpeciesId);
        public static DroppedItem Gun(long id, Vector3 position, float yaw) =>
            new DroppedItem { kind = DroppedKind.Harpoon, id = id, position = position, yaw = yaw };
    }

    public sealed partial class RaidWorld
    {
        readonly SyncList<DroppedItem> dropped = new();
        readonly Dictionary<long, DroppedItemView> dropViews = new();
        readonly List<long> vanished = new();
        bool dropsDirty;
        public int DroppedCount => dropped.Count;
        public DroppedItem DroppedAt(int index) => dropped[index];

        int DropIndex(long id)
        {
            for (int i = 0; i < dropped.Count; i++) if (dropped[i].id == id) return i;
            return -1;
        }

        internal bool CanRegisterDrop(long id) => IsServerInitialized && id > 0 && DropIndex(id) < 0;
        internal void RegisterDrop(DroppedItem item) => dropped.Add(item);
        internal void RemoveDrop(long id)
        {
            int index = DropIndex(id);
            if (index >= 0) dropped.RemoveAt(index);
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
            if (diver == null || dropped.Count == 0 || !diver.TryGetComponent<HarpoonTool>(out var tool)) return false;
            var eye = diver.transform.position + Vector3.up * .6f;
            var aim = diver.AimDirection;
            if (!IsServerInitialized && diver.IsOwner && diver.GetComponentInChildren<Camera>() is Camera camera)
                aim = camera.transform.forward;
            float best = float.PositiveInfinity;
            foreach (var candidate in dropped)
            {
                Vector3 offset = candidate.position - eye;
                float distance = offset.magnitude;
                if (distance > 3 || distance < .02f || !tool.CanPickUp(candidate)) continue;
                float along = Vector3.Dot(offset, aim);
                float radius = candidate.Fish != null && candidate.Fish.RequiresPartner ? .62f : .39f;
                float cross = (offset - aim * along).magnitude;
                if (along <= 0 || cross > radius) continue;
                float score = cross + distance * .1f;
                if (score >= best || Physics.Raycast(eye, offset / distance, Mathf.Max(0,distance - .08f), ~0, QueryTriggerInteraction.Ignore)) continue;
                best = score;
                item = candidate;
            }
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
        }

        void PresentDrops()
        {
            if (!IsClientInitialized || !dropsDirty) return;
            dropsDirty = false;
            vanished.Clear();
            foreach (var id in dropViews.Keys) if (DropIndex(id) < 0) vanished.Add(id);
            foreach (var id in vanished)
            {
                var old = dropViews[id];
                if (old != null) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
                dropViews.Remove(id);
            }
            foreach (var item in dropped)
            {
                if (!dropViews.TryGetValue(item.id, out var view))
                {
                    view = new GameObject("Dropped " + item.kind).AddComponent<DroppedItemView>();
                    view.transform.SetParent(transform, true);
                    dropViews.Add(item.id, view);
                }
                if (!view.Show(item)) dropsDirty = true;
            }
        }
    }

    sealed class DroppedItemView : MonoBehaviour
    {
        readonly List<Mesh> ownedMeshes = new();
        bool built;

        internal bool Show(DroppedItem item)
        {
            transform.SetPositionAndRotation(item.position, Quaternion.Euler(0, item.yaw, item.kind == DroppedKind.Harpoon ? 76 : 12));
            if (built) return true;
            if (item.kind == DroppedKind.Harpoon)
            {
                var source = HarpoonPresentation.ActiveModel;
                if (source == null || !source.MakeWorldModel(transform, ownedMeshes, out _, out _, out _, out _)) return false;
                transform.localScale = Vector3.one * 1.48f;
            }
            else
            {
                if (item.Fish == null) return false;
                CatchFishModel.Create(transform, item.Fish.SpeciesId);
                transform.localScale = Vector3.one * (item.Fish.RequiresPartner ? 1.25f : .68f);
            }
            return built = true;
        }

        void OnDestroy()
        {
            foreach (var mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
        }
    }
}
