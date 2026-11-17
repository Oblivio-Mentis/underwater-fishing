using UnderwaterFishing.Fishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Presentation
{
    public sealed class DiverFishingView : MonoBehaviour
    {
        public Material RopeMaterial;
        public Transform Weapon;
        public AudioClip ShotClip;
        private DiverFishing fishing;
        private LineRenderer rope;
        private AudioSource audioSource;
        private uint lastShot;
        private float recoil;
        private Vector3 weaponOrigin;
        private MaterialPropertyBlock ropeProperties;
        private void Awake()
        {
            fishing = GetComponent<DiverFishing>();
            var line = new GameObject("Harpoon tether", typeof(LineRenderer)); line.transform.SetParent(transform);
            rope = line.GetComponent<LineRenderer>(); rope.sharedMaterial = RopeMaterial;
            rope.positionCount = 12; rope.startWidth = rope.endWidth = .025f; rope.useWorldSpace = true; rope.enabled = false;
            ropeProperties = new MaterialPropertyBlock();
            audioSource = gameObject.AddComponent<AudioSource>(); audioSource.spatialBlend = 1; audioSource.maxDistance = 25; audioSource.dopplerLevel = 0;
            if (Weapon != null) weaponOrigin = Weapon.localPosition;
        }
        private void Start() { lastShot = fishing.State.ShotSequence; }
        private void LateUpdate()
        {
            var state = fishing.State;
            if (lastShot != state.ShotSequence)
            {
                lastShot = state.ShotSequence; recoil = 1;
                if (ShotClip != null) audioSource.PlayOneShot(ShotClip, .3f);
            }
            recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 7);
            if (Weapon != null) Weapon.localPosition = weaponOrigin + Vector3.back * (.08f * recoil);
            if (fishing.IsOwner) fishing.Diver.Eyes.fieldOfView = 60 + 1.2f * recoil;
            var fish = fishing.CurrentFish;
            rope.enabled = state.Connected && fish != null;
            if (!rope.enabled) return;
            Vector3 start = Weapon != null ? Weapon.position + Weapon.forward * .38f : fishing.Diver.AimOrigin + fishing.Diver.transform.forward;
            Vector3 end = fish.transform.position;
            float slack = Mathf.Max(0, state.Length - Vector3.Distance(start, end));
            float ratio = state.NormalizedTension;
            Color color = Color.Lerp(new Color(.15f, 1f, .8f), new Color(1f, .2f, .05f), Mathf.Clamp01(ratio));
            rope.startColor = rope.endColor = color;
            ropeProperties.SetColor("_BaseColor", color); rope.SetPropertyBlock(ropeProperties);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                rope.SetPosition(i, Vector3.Lerp(start, end, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * slack * .25f));
            }
        }
    }
}
