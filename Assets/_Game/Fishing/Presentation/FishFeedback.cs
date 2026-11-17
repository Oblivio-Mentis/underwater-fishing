using System;
using OblivioMentis.Presentation;
using OblivioMentis.UniversalFSM;
using UnderwaterFishing.Fishing.Network;
using UnityEngine;

namespace UnderwaterFishing.Fishing.Presentation
{
    public sealed class FishFeedback : MonoBehaviour
    {
        private sealed class ReplicatedStateSource : IUniversalFsmStateChangeSource
        {
            public event Action<UniversalFsmLayerStateChange> LayerStateChanged;
            private int current;
            public void Apply(int next)
            {
                if (current == next) return;
                int previous = current; current = next;
                LayerStateChanged?.Invoke(new UniversalFsmLayerStateChange(UniversalFsmBlackboard.StableHash("Main"), "Main", previous, next));
            }
        }
        private NetworkFish fish;
        private FsmPresentationDriver driver;
        private ReplicatedStateSource source;
        private Renderer mesh;
        private Transform visual;
        private MaterialPropertyBlock block;
        private Vector3 baseScale;
        private Color baseColor, pulseColor;
        private float remaining, duration;
        private void Awake()
        {
            fish = GetComponent<NetworkFish>(); driver = GetComponent<FsmPresentationDriver>();
            visual = transform.Find("Visual"); mesh = visual.GetComponent<Renderer>();
            baseScale = visual.localScale; baseColor = mesh.sharedMaterial.color;
            block = new MaterialPropertyBlock();
        }
        private void Start()
        {
            source = new ReplicatedStateSource(); driver.Bind(source);
        }
        private void Update()
        {
            if (!fish.IsClientStarted || source == null) return;
            source.Apply((int)fish.State.State);
            remaining = Mathf.Max(0, remaining - Time.deltaTime);
            float t = duration <= 0 ? 0 : remaining / duration;
            mesh.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(baseColor, pulseColor, t));
            block.SetColor("_EmissionColor", Color.Lerp(baseColor * .2f, pulseColor * 2, t));
            mesh.SetPropertyBlock(block);
            visual.localScale = baseScale * (1 + .08f * t * Mathf.Sin(t * Mathf.PI));
        }
        public void Pulse(Color color, float seconds) { pulseColor = color; duration = remaining = Mathf.Max(.01f, seconds); }
        private void OnDestroy() { if (driver != null) driver.Unbind(); }
    }
}
