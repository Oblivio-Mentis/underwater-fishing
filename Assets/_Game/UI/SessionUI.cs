using UnderwaterFishing.Network;
using UnderwaterFishing.Network.Steam;
using UnderwaterFishing.Player;
using UnderwaterFishing.Fishing.Network;
using UnderwaterFishing.Fishing.Creatures;
using UnderwaterFishing.Cooperation;
using UnderwaterFishing.Diving;
using UnderwaterFishing.Cargo.Network;
using UnderwaterFishing.Expedition.Network;
using UnderwaterFishing.Progression.Network;
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
        private Text hud, status, fishingHud;
        private RectTransform lobbyList;
        private InputField address, lobbyId;

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
            fishingHud = Label(canvas.transform, "", 20);
            fishingHud.rectTransform.anchorMin = fishingHud.rectTransform.anchorMax = new Vector2(1, 0);
            fishingHud.rectTransform.pivot = new Vector2(1, 0); fishingHud.rectTransform.anchoredPosition = new Vector2(-24, 65);
            fishingHud.rectTransform.sizeDelta = new Vector2(600, 170); fishingHud.alignment = TextAnchor.LowerRight;
            var hr = hud.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0, 1);
            hr.pivot = new Vector2(0, 1);
            hr.anchoredPosition = new Vector2(24, -24);
            hr.sizeDelta = new Vector2(650, 250);
            var cross = Label(canvas.transform, "+", 26);
            cross.alignment = TextAnchor.MiddleCenter;
            cross.rectTransform.anchorMin = cross.rectTransform.anchorMax = new Vector2(.5f, .5f);
            cross.rectTransform.sizeDelta = new Vector2(50, 50);
            var hint = Label(canvas.transform, "WASD · Space/Ctrl · Shift    ЛКМ — гарпун    ПКМ — катушка    R — отпустить    E — взять рыбу    F — помощь    Esc — меню", 17);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(.5f, 0);
            hint.rectTransform.pivot = new Vector2(.5f, 0);
            hint.rectTransform.anchoredPosition = new Vector2(0, 20);
            hint.rectTransform.sizeDelta = new Vector2(1100, 35);
            hint.alignment = TextAnchor.MiddleCenter;

            menu = new GameObject("Session menu", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            menu.transform.SetParent(canvas.transform, false);
            var rect = menu.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(650, session.AllowsLanDebug ? 870 : 710);
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
            lobbyList = CreateLobbyList(menu.transform);
            lobbyId = CreateAddress(menu.transform, "Steam Lobby ID", "");
            Button(menu.transform, "Войти по Steam Lobby ID", () => { if (ulong.TryParse(lobbyId.text.Trim(), out ulong id)) steam.JoinLobby(id); else steam.JoinLobby(0); });
            if (session.AllowsLanDebug)
            {
                Fixed(Label(menu.transform, "Отладка · LAN по IP", 16).gameObject, 25);
                address = CreateAddress(menu.transform);
                Button(menu.transform, "LAN: создать хоста", () => session.HostLan());
                Button(menu.transform, "LAN: подключиться", () => session.JoinLan(address.text.Trim()));
            }
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
            if (diver == null) { hud.text = "ПЕЩЕРА · БАЗА ЭКИПАЖА"; fishingHud.text = ""; return; }
            var state = diver.State;
            var d = state.Diving;
            hud.text = $"O₂  {d.Oxygen:0}   |   Глубина {d.Depth:0.0} м\nДавление {d.AmbientPressure:0.0} атм   |   Нагрузка {d.Stress:0}%\n{(state.Sheltered ? "ПОДЛОДКА · кислород восстанавливается" : "В ОТКРЫТОЙ ВОДЕ")}\nСостояние: {d.Life}  ·  Экипаж {session.PlayerCount}/5";
            hud.color = d.Stress > 60 || d.Oxygen < 25 ? new Color(1f, .65f, .3f) : new Color(.7f, 1f, .95f);
            var rescue = diver.GetComponent<DiverRescue>();
            if (state.PinnedBy >= 0) hud.text += "\nПРИДАВЛЕН · F — подать сигнал. Напарник: удерживать E.";
            if (d.Life == DiverLife.Incapacitated) hud.text += $"\nБЕЗ СОЗНАНИЯ · до гибели {d.IncapacitatedRemaining:0} с. Напарник: удерживать F.";
            if (d.Life == DiverLife.Dead) hud.text += "\nНАБЛЮДЕНИЕ · до возвращения экипажа";
            if (rescue != null)
            {
                hud.text += $"\nАварийный кислород: {rescue.State.EmergencyCharges}";
                if (rescue.State.TargetId >= 0) hud.text += $" · Спасение {rescue.State.Progress:P0}";
            }
            var fishing = DiverFishing.Local;
            if (fishing == null) return;
            var f = fishing.State; var fish = fishing.CurrentFish;
            if (f.Connected && fish != null)
            {
                fishingHud.text = $"{fish.Species.DisplayName} · {fish.State.State}\nТрос {f.Length:0.0} м · Натяжение {f.NormalizedTension:P0} · Помощь {f.Helpers}\nСилы рыбы {fish.State.Stamina:0.0}\n" +
                    (fish.State.State == FishState.Exhausted ? "Подтяните рыбу ближе · E взять" : "ПКМ — подтягивать · сбрасывайте нагрузку на рывках");
            }
            else fishingHud.text = f.Status == HarpoonStatus.Carried ? "Улов в руках · R отпустить" :
                f.Status == HarpoonStatus.Broken ? "Трос оборвался · ищите рыбу снова" : "Найдите рыбу и наведите прицел · ЛКМ";
            var cargo = DiverCargo.Local;
            var station = NetworkCargoStation.Instance;
            if (station != null) fishingHud.text += $"\nКонтейнер: {station.State.State} · {station.State.UsedCapacity}/{station.State.Capacity} · Отсек {station.State.HoldCount}";
            if (cargo != null && cargo.InteractionHint.Length > 0) fishingHud.text += "\n" + cargo.InteractionHint;
            if (DiverExpedition.Local != null && DiverExpedition.Local.InteractionHint.Length > 0) fishingHud.text += "\n" + DiverExpedition.Local.InteractionHint;
            if (DiverProgression.Local != null && DiverProgression.Local.InteractionHint.Length > 0) fishingHud.text += "\n" + DiverProgression.Local.InteractionHint;
            if (f.HelpTargetId >= 0) fishingHud.text += "\nПомогаете удерживать напарника";
            fishingHud.color = f.NormalizedTension > .8f ? new Color(1, .5f, .2f) : new Color(.7f, 1, .9f);
        }
        private void RefreshLobbies()
        {
            if (lobbyList == null || steam == null) return;
            foreach (Transform child in lobbyList) Destroy(child.gameObject);
            foreach (var lobby in steam.Lobbies)
            {
                ulong id = lobby.Id;
                Button(lobbyList, $"{lobby.Name}  ({lobby.Members}/{lobby.Capacity})", () => steam.JoinLobby(id));
            }
        }
        private RectTransform CreateLobbyList(Transform parent)
        {
            var scroll = new GameObject("Steam lobbies", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scroll.transform.SetParent(parent, false); Fixed(scroll, 100);
            scroll.GetComponent<Image>().color = new Color(.025f, .1f, .12f);
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(scroll.transform, false); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            var content = new GameObject("Lobby results", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var list = content.GetComponent<VerticalLayoutGroup>(); list.spacing = 4; list.childControlWidth = list.childControlHeight = true;
            list.childForceExpandWidth = true; list.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var view = scroll.GetComponent<ScrollRect>(); view.viewport = viewport; view.content = content;
            view.horizontal = false; view.scrollSensitivity = 22; view.movementType = ScrollRect.MovementType.Clamped;
            return content;
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
        private InputField CreateAddress(Transform parent, string name = "Address", string initial = "127.0.0.1")
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(.1f, .17f, .2f);
            var text = Label(go.transform, initial, 19);
            text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(10, 0); text.rectTransform.offsetMax = new Vector2(-10, 0);
            text.alignment = TextAnchor.MiddleLeft;
            var field = go.GetComponent<InputField>(); field.textComponent = text; field.text = initial;
            if (initial.Length == 0)
            {
                var placeholder = Label(go.transform, name, 17);
                placeholder.rectTransform.anchorMin = Vector2.zero; placeholder.rectTransform.anchorMax = Vector2.one;
                placeholder.rectTransform.offsetMin = new Vector2(10, 0); placeholder.rectTransform.offsetMax = new Vector2(-10, 0);
                placeholder.alignment = TextAnchor.MiddleLeft; placeholder.color = new Color(.4f, .7f, .7f);
                field.placeholder = placeholder;
            }
            Fixed(go, 36);
            return field;
        }
        private static void Fixed(GameObject go, float height) => go.AddComponent<LayoutElement>().preferredHeight = height;
        private void OnDestroy() { if (steam != null) steam.Changed -= RefreshLobbies; }
        private void OnEnable() { if (steam != null) steam.Changed += RefreshLobbies; }
        private void OnDisable() { if (steam != null) steam.Changed -= RefreshLobbies; }
    }
}
