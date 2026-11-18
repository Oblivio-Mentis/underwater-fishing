using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.UI
{
    /// <summary>Keeps world signs from overlapping readable session and terminal panels.</summary>
    public sealed class WorldLabelVisibility : MonoBehaviour
    {
        private DiverInput input;
        private bool visible;
        private float nextScan;
        private void Start() { input = GetComponent<DiverInput>(); }
        private void Update()
        {
            bool show = input == null || (!input.MenuOpen && !input.UiCaptured);
            if (show == visible && Time.unscaledTime < nextScan) return;
            visible = show; nextScan = Time.unscaledTime + .25f;
            foreach (var label in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                var renderer = label.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = show;
            }
        }
    }
}
