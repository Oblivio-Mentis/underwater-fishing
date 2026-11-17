using UnderwaterFishing.Network;
using UnderwaterFishing.Network.Steam;
using UnderwaterFishing.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UnderwaterFishing.UI
{
    public sealed class SessionUI : MonoBehaviour
    {
        private NetworkSession session;
        private SteamLobbyService steam;
        private DiverInput input;
        private Font font;
        private GameObject menu;
        private Text hud, status;
        private RectTransform lobbyList;
        private InputField address;

        private void Start()
        {
            session = GetComponent<NetworkSession>();
            steam = GetComponent<SteamLobbyService>();
            input = GetComponent<DiverInput>();
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = new GameObject("Crew UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            var events = new GameObject("UI Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            hud = Label(canvas.transform, "", 21);
            var hr = hud.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0, 1);
            hr.pivot = new Vector2(0, 1);
            hr.anchoredPosition = new Vector2(24, -24);
            hr.sizeDelta = new Vector2(650, 250);
            var cross = Label(canvas.transform, "+", 26);
            cross.alignment = TextAnchor.MiddleCenter;
            cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(.5f, .5f);
            cross.rectTransform.sizeDelta = new Vector2(50, 50);
            var hint = Label(canvas.transform, "WASD — плыть    Space / Ctrl — вверх / вниз    Shift — ускорение    Esc — меню", 17);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 0);
            hint.rectTransform.pivot = new Vector2(.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 20);
            hint.rectTransform.sizeDelta = new Vector2(1100, 35);
            hint.alignment = TextAnchor.MiddleCenter;

            menu = new GameObject("Session menu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            menu.transform.SetParent(canvas.transform, false);
            var rect = menu.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(650, 840);
            menu.GetComponent<Image>().color = new Color(.015f, .06f, .08f, .97f);
            var layout = menu.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 20, 20);
            layout.spacing = 6;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            Fixed(Label(menu.transform, "UNDERWATER FISHING", 30).gameObject, 48);
            Fixed(Label(menu.transform, "Экипаж подлодки · 2–5 игроков", 19).gameObject, 30);
            status = Label(menu.transform, "", 16);
            Fixed(status.gameObject, 65);
            Button(menu.transform, "Создать Steam-лобби", () => steam.CreateLobby());
            Button(menu.transform, "Найти Steam-лобби", () => steam.FindLobbies());
            Button(menu.transform, "Пригласить друзей через Steam", () => steam.InviteFriends());
            lobbyList = new GameObject("Lobbies", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            lobbyList.SetParent(menu.transform, false);
            Fixed(lobbyList.gameObject, 110);
            Fixed(Label(menu.transform, "Локальная проверка по IP", 16).gameObject, 25);
            address = CreateAddress(menu.transform);
            Button(menu.transform, "LAN: создать хоста", () => session.HostLan());
            Button(menu.transform, "LAN: подключиться", () => session.JoinLan(address.text.Trim()));
            Button(menu.transform, "Продолжить", () => { if (session.Connected) input.SetMenu(false); });
            Button(menu.transform, "Завершить сессию", () => { session.Disconnect(); steam.LeaveLobby(); });
            steam.Changed += RefreshLobbies;
            string[] args = System.Environment.GetCommandLineArgs();
            if (System.Array.IndexOf(args, "--raid-host") < 0 && System.Array.IndexOf(args, "--raid-join") < 0)
                steam.TryInitialize();
        }

        private void Update()
        {
            if (menu == null) return;
            menu.SetActive(input.MenuOpen || !session.Connected);
            status.text = $"{session.Status} · Экипаж: {session.PlayerCount}/5\nSteam: {steam.Status}";
            var diver = NetworkDiver.Local;
            if (diver == null) { hud.text = "ПЕЩЕРА · БАЗА ЭКИПАЖА"; return; }
            var state = diver.State;
            var d = state.Diving;
            hud.text = $"O₂  {d.Oxygen:0}   |   Глубина {d.Depth:0.0} м\nДавление {d.AmbientPressure:0.0} атм   |   Нагрузка {d.Stress:0}%\n{(state.Sheltered ? "ПОДЛОДКА · кислород восстанавливается" : "В ОТКРЫТОЙ ВОДЕ")}\nСостояние: {d.Life}  ·  Экипаж {session.PlayerCount}/5";
            hud.color = d.Stress > 60 || d.Oxygen < 25 ? new Color(1f, .65f, .3f) : new Color(.7f, 1f, .95f);
        }
        private void RefreshLobbies()
        {
            foreach (Transform child in lobbyList) Destroy(child.gameObject);
            int count = 0;
            foreach (var lobby in steam.Lobbies)
            {
                if (count++ >= 2) break;
                ulong id = lobby.Id;
                Button(lobbyList, $"{lobby.Name}  ({lobby.Members}/{lobby.Capacity})", () => steam.JoinLobby(id));
            }
        }
        private Text Label(Transform parent, string text, int size)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = font; label.fontSize = size; label.text = text;
            label.color = new Color(.7f, 1f, .95f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return label;
        }
        private void Button(Transform parent, string caption, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(caption, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(.07f, .25f, .29f);
            go.GetComponent<Button>().onClick.AddListener(action);
            var label = Label(go.transform, caption, 19);
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            Fixed(go, 40);
        }
        private InputField CreateAddress(Transform parent)
        {
            var go = new GameObject("Address", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(.1f, .17f, .2f);
            var text = Label(go.transform, "127.0.0.1", 19);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(10, 0); text.rectTransform.offsetMax = new Vector2(-10, 0);
            text.alignment = TextAnchor.MiddleLeft;
            var field = go.GetComponent<InputField>(); field.textComponent = text; field.text = "127.0.0.1";
            Fixed(go, 36);
            return field;
        }
        private static void Fixed(GameObject go, float height) => go.AddComponent<LayoutElement>().preferredHeight = height;
        private void OnDestroy() { if (steam != null) steam.Changed -= RefreshLobbies; }
    }
}
