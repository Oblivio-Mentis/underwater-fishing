using System.Collections;
using UnderwaterFishing.Network;
using UnderwaterFishing.Player;
using UnderwaterFishing.Fishing.Tether;
using UnityEngine;
using UnityEngine.UI;

namespace UnderwaterFishing.Fishing.Gym
{
    public sealed class FishingGym : MonoBehaviour
    {
        private DiverInput input;
        private GameObject panel;
        private TetherSettings settings;
        private float originalReel, originalStiffness, originalBreak;
        private IEnumerator Start()
        {
            input = GetComponent<DiverInput>();
            yield return null;
            var session = GetComponent<NetworkSession>();
            if (!session.Connected) session.HostLan();
            while (NetworkDiver.Local == null) yield return null;
            settings = NetworkDiver.Local.Balance.Tether;
            originalReel = settings.ReelSpeed; originalStiffness = settings.Stiffness; originalBreak = settings.BreakTension;
            BuildPanel();
        }
        private void Update()
        {
            if (panel == null) return;
            if (input.MenuOpen && panel.activeSelf) Toggle(false);
            else if (input.InfoPressed) Toggle(!panel.activeSelf);
        }
        private void Toggle(bool show) { panel.SetActive(show); input.SetUiCapture(show); }
        private void BuildPanel()
        {
            if (!GetComponent<NetworkSession>().Manager.IsServerStarted) return;
            var canvas = new GameObject("Fishing Gym tuning", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay; canvas.GetComponent<Canvas>().sortingOrder = 20;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600, 900);
            panel = new GameObject("Tuning", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)); panel.transform.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(650, 360);
            panel.GetComponent<Image>().color = new Color(.02f, .08f, .1f, .98f);
            var layout = panel.GetComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(25, 25, 20, 20); layout.spacing = 12; layout.childForceExpandHeight = false;
            Label("ПОЛИГОН ОХОТЫ · Tab закрыть", 27, 45);
            AddSlider("Скорость катушки", settings.ReelSpeed, .5f, 5, v => settings.ReelSpeed = v);
            AddSlider("Жёсткость троса", settings.Stiffness, 3, 25, v => settings.Stiffness = v);
            AddSlider("Предел натяжения", settings.BreakTension, 15, 90, v => settings.BreakTension = v);
            Label("Настройки действуют на сервере до выхода из полигона.", 17, 30);
            panel.SetActive(false);
        }
        private Text Label(string text, int size, float height)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Text), typeof(LayoutElement)); go.transform.SetParent(panel.transform, false);
            var label = go.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = size; label.color = new Color(.7f, 1, .9f); label.text = text;
            go.GetComponent<LayoutElement>().preferredHeight = height; return label;
        }
        private void AddSlider(string caption, float value, float min, float max, System.Action<float> changed)
        {
            var text = Label(caption + ": " + value.ToString("0.0"), 20, 28);
            var go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Slider), typeof(LayoutElement)); go.transform.SetParent(panel.transform, false);
            go.GetComponent<Image>().color = new Color(.1f, .25f, .28f); go.GetComponent<LayoutElement>().preferredHeight = 24;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)); handle.transform.SetParent(go.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 24); handle.GetComponent<Image>().color = new Color(.3f, .9f, .8f);
            var slider = go.GetComponent<Slider>(); slider.handleRect = handle.GetComponent<RectTransform>(); slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            slider.onValueChanged.AddListener(v => { changed(v); text.text = caption + ": " + v.ToString("0.0"); });
        }
        private void OnDestroy()
        {
            if (settings != null) { settings.ReelSpeed = originalReel; settings.Stiffness = originalStiffness; settings.BreakTension = originalBreak; }
            if (input != null) input.SetUiCapture(false);
        }
    }
}
