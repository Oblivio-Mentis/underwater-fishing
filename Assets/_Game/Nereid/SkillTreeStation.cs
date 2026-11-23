using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Raid.Nereid
{
    // Shared scene geometry: a separate standing computer across the aisle from the reservoir.
    internal sealed class SkillTreeStation : MonoBehaviour
    {
        ShipConsole console;
        TMP_Text idle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= Install;
            SceneManager.sceneLoaded += Install;
            Install(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void Install(Scene scene, LoadSceneMode mode)
        {
            var vessel = NereidVessel.Active;
            if (vessel == null || vessel.gameObject.scene != scene ||
                vessel.GetComponentInChildren<SkillTreeStation>() != null) return;

            var station = new GameObject("SkillTreeConsole");
            station.SetActive(false);
            station.transform.SetParent(vessel.transform, false);
            station.transform.localPosition = new Vector3(4.48f, .015f, -2.3f);
            station.transform.localRotation = Quaternion.Euler(0, 90, 0);
            // Vessel coordinates are scaled; UI canvases and scene consoles use world-metre scale.
            Vector3 vesselScale = vessel.transform.lossyScale;
            station.transform.localScale = new Vector3(1 / vesselScale.z, 1 / vesselScale.y, 1 / vesselScale.x);
            var view = station.AddComponent<SkillTreeStation>();
            view.Build();
            station.SetActive(true);
        }

        void Build()
        {
            console = gameObject.AddComponent<ShipConsole>();
            console.kind = ShipConsoleKind.SkillTree;
            console.range = 3.65f;
            console.screenScale = .0018f;
            Material frame = FindSurface(ShipConsoleKind.Depths);
            Material baseMat = frame;
            Cube("Skill computer foot", new Vector3(0, .11f, .075f), new Vector3(1.4f, .16f, .66f), baseMat);
            Cube("Skill computer pedestal", new Vector3(0, .60f, .12f), new Vector3(.46f, .88f, .31f), frame);
            Cube("Skill computer frame", new Vector3(0, 1.33f, .06f), new Vector3(2.24f, 1.53f, .19f), baseMat);
            Cube("Skill computer face", new Vector3(0, 1.33f, -.052f), new Vector3(2.15f, 1.46f, .045f), frame);

            var anchor = new GameObject("ScreenAnchor").transform;
            anchor.SetParent(transform, false);
            anchor.localPosition = new Vector3(0, 1.33f, -.083f);
            var hit = anchor.gameObject.AddComponent<BoxCollider>();
            hit.size = new Vector3(2.17f, 1.48f, .016f);
            console.screenAnchor = anchor;
            if (UI.UiRoot.Instance == null) return;

            var label = new GameObject("Skill computer idle display");
            label.transform.SetParent(anchor, false);
            label.transform.localPosition = Vector3.back * .011f;
            idle = label.AddComponent<TextMeshPro>();
            idle.font = UI.UiRoot.Instance.font;
            idle.alignment = TextAlignmentOptions.Center;
            idle.rectTransform.sizeDelta = new Vector2(2.1f, 1.42f);
            idle.textWrappingMode = TextWrappingModes.NoWrap;
            idle.fontSize = .17f;
            idle.color = UI.UiTheme.Cyan;
            idle.text = "NEREID\nЛКМ · ПРОКАЧКА";
        }

        static Material FindSurface(ShipConsoleKind kind)
        {
            foreach (var other in ShipConsole.Stations)
            {
                if (other.kind != kind || other.screenAnchor == null) continue;
                Material surface = null;
                foreach (var part in other.GetComponentsInChildren<MeshRenderer>())
                {
                    if (part.sharedMaterial == null) continue;
                    surface = part.sharedMaterial;
                    if (surface.name.Contains("steel", System.StringComparison.OrdinalIgnoreCase)) return surface;
                }
                return surface;
            }
            return null;
        }

        void Cube(string label, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = label;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (material != null) part.GetComponent<Renderer>().sharedMaterial = material;
        }

        void LateUpdate()
        {
            if (idle == null) return;
            var ui = UI.UiRoot.Instance;
            bool interactive = ui != null && ui.Page == UI.UiPage.SkillTree && ui.Context.SelectedConsole == console;
            if (idle.gameObject.activeSelf == interactive) idle.gameObject.SetActive(!interactive);
        }
    }
}
