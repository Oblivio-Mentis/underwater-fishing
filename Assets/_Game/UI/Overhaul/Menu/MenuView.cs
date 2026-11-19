using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    /// <summary>Local presentation for connection, crew and settings. Network actions stay in RaidSession.</summary>
    public sealed class MenuView
    {
        public RectTransform Root { get; }
        readonly IUiController app;
        readonly RectTransform surface, body;
        readonly Dictionary<UiPage, RectTransform> pages = new Dictionary<UiPage, RectTransform>();
        readonly TMP_Text title, subtitle, footer;
        readonly Image headerSymbol;
        readonly Button back;
        UiPage page;
        bool createIntent = true, localTest;

        Button solo,createIntentButton, joinIntentButton, steamTab, localTab;
        RectTransform steamSection, localSection, steamCreateSection, steamJoinSection;
        TMP_Text mainStatus, steamState, connectionStatus, connectionBusy, crewCount, crewRole, crewLobby;
        TMP_InputField lobbyInput, addressInput;
        Button steamEnable, steamCreate, steamJoin, localHost, localJoin, cancel, invite, copyLobby;
        readonly List<CrewRow> crewRows = new List<CrewRow>();
        readonly List<SettingRow> settingRows = new List<SettingRow>();

        sealed class CrewRow
        {
            public Image Panel, Mark;
            public TMP_Text Name, Identity, State;
        }
        sealed class SettingRow
        {
            public Slider Slider;
            public TMP_Text Value;
            public Func<float> Read;
            public Func<float, string> Format;
        }

        public MenuView(Transform parent, IUiController app)
        {
            this.app = app;
            surface = UiKit.Frame(parent, "MenuView", 1180, 810);
            Root = (RectTransform)surface.parent;

            headerSymbol=UiKit.Symbol(surface,"Page symbol",DiveInstruments.Mark.Submarine,UiTheme.Cyan);
            SetArea(headerSymbol.rectTransform,new Vector2(30,-33),new Vector2(110,90));
            var micro = UiKit.Label(surface, "Service", UiText.MenuService, 18, UiTheme.Cyan);
            SetArea(micro.rectTransform, new Vector2(168, -19), new Vector2(950, 26));
            title = UiKit.Label(surface, "Title", UiText.MenuMainTitle, 40);
            SetArea(title.rectTransform, new Vector2(166, -52), new Vector2(970, 56));
            subtitle = UiKit.Label(surface, "Subtitle", "", 21, UiTheme.Muted);
            SetArea(subtitle.rectTransform, new Vector2(168, -112), new Vector2(950, 32));
            var rule = UiKit.Panel(surface, "HeaderRule", UiTheme.Edge);
            rule.rectTransform.anchorMin = new Vector2(0, 1); rule.rectTransform.anchorMax = Vector2.one;
            rule.rectTransform.pivot = new Vector2(.5f, 1); rule.rectTransform.anchoredPosition = new Vector2(0, -155); rule.rectTransform.sizeDelta = new Vector2(-64, 2);

            body = UiKit.Node(surface, "Pages"); UiKit.Fill(body);
            body.offsetMin = new Vector2(28, 96); body.offsetMax = new Vector2(-28, -172);
            BuildMain(); BuildConnection(); BuildCrew(); BuildPause(); BuildSettings();

            footer = UiKit.Label(surface, "Footer", "", 19, UiTheme.Muted);
            footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = Vector2.zero;
            footer.rectTransform.pivot = Vector2.zero; footer.rectTransform.anchoredPosition = new Vector2(32, 32); footer.rectTransform.sizeDelta = new Vector2(830, 46);
            back = UiKit.Button(surface, "Back", UiText.MenuBack, () => app.ClosePanel());
            UiKit.Place((RectTransform)back.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-32, 26), new Vector2(210, 52));
            Open(UiPage.Main);
        }

        public void Open(UiPage value)
        {
            page = value;
            bool landing = page == UiPage.Main;
            Root.sizeDelta = landing ? new Vector2(840, 642) : new Vector2(1180, 810);
            float headingWidth = Root.sizeDelta.x - 200;
            title.rectTransform.sizeDelta = new Vector2(headingWidth, 56);
            subtitle.rectTransform.sizeDelta = new Vector2(headingWidth, 32);
            headerSymbol.sprite=DiveInstruments.Art(value switch {
                UiPage.Crew=>DiveInstruments.Mark.Helmet,UiPage.Connection=>DiveInstruments.Mark.Compass,
                UiPage.Settings=>DiveInstruments.Mark.Spear,UiPage.Pause=>DiveInstruments.Mark.Helmet,
                _=>DiveInstruments.Mark.Submarine });
            foreach (var entry in pages) entry.Value.gameObject.SetActive(entry.Key == value);
            back.gameObject.SetActive(value != UiPage.Main);
            if (value == UiPage.Main) { UiKit.Set(title, UiText.MenuBrand); UiKit.Set(subtitle, UiText.MenuMainSubtitle); }
            else if (value == UiPage.Connection) { UiKit.Set(title, UiText.MenuConnectionTitle); UiKit.Set(subtitle, UiText.MenuConnectionSubtitle); RenderConnectionTabs(); }
            else if (value == UiPage.Crew) { UiKit.Set(title, UiText.MenuCrewTitle); UiKit.Set(subtitle, UiText.MenuCrewSubtitle); }
            else if (value == UiPage.Pause) { UiKit.Set(title, UiText.MenuPauseTitle); UiKit.Set(subtitle, UiText.MenuPauseSubtitle); }
            else if (value == UiPage.Settings)
            {
                UiKit.Set(title, UiText.MenuSettingsTitle); UiKit.Set(subtitle, UiText.MenuSettingsSubtitle);
                foreach (var row in settingRows) row.Slider.SetValueWithoutNotify(row.Read());
            }
            UiKit.Set(footer, value == UiPage.Main ? UiText.MenuBuild : UiText.MenuFooterOnline);
            Refresh();
        }

        public void Refresh()
        {
            var ctx = app.Context; var session = ctx.Session;
            bool busy = session != null && session.Busy;
            bool connected = session != null && session.Connected;
            bool offlineAvailable = session != null && !busy && !connected;
            solo.interactable=offlineAvailable;
            var status=session == null ? UiText.MenuConnectionMissing : session.Status;
            mainStatus.gameObject.SetActive(!string.IsNullOrWhiteSpace(status));
            UiKit.Set(mainStatus, status);
            if (page == UiPage.Connection)
            {
                var lobby = session == null ? null : session.lobby;
                UiKit.Set(steamState, lobby != null && lobby.OverlayActive ? UiText.MenuSteamOverlay : lobby != null && lobby.IsInitialized ? UiText.MenuSteamInitialized : UiText.MenuSteamNotInitialized);
                steamState.color = lobby != null && lobby.IsInitialized ? UiTheme.Green : UiTheme.Muted;
                UiKit.Set(connectionStatus, session == null ? UiText.MenuConnectionMissing : session.Status);
                UiKit.Set(connectionBusy, busy ? UiText.MenuConnectionBusy : UiText.MenuConnectionStatus);
                connectionBusy.color = busy ? UiTheme.Amber : UiTheme.Muted;
                steamEnable.interactable = offlineAvailable && lobby != null;
                steamCreate.interactable = offlineAvailable && lobby != null;
                steamJoin.interactable = offlineAvailable && lobby != null && !string.IsNullOrWhiteSpace(lobbyInput.text);
                localHost.interactable = offlineAvailable;
                localJoin.interactable = offlineAvailable && !string.IsNullOrWhiteSpace(addressInput.text);
                addressInput.interactable = !busy && !connected; lobbyInput.interactable = !busy && !connected;
                createIntentButton.interactable = joinIntentButton.interactable = steamTab.interactable = localTab.interactable = !busy;
                cancel.gameObject.SetActive(busy);
            }
            if (page == UiPage.Crew)
            {
                UiKit.Set(crewCount, UiText.MenuCount(ctx.Players.Count));
                UiKit.Set(crewRole, !connected ? UiText.MenuNoSession : session.manager.IsServerStarted ? UiText.MenuHostRole : UiText.MenuClientRole);
                for (int i = 0; i < crewRows.Count; i++)
                {
                    var row = crewRows[i]; var player = i < ctx.Players.Count ? ctx.Players[i] : null;
                    UiKit.Set(row.Name, player == null ? UiText.MenuCrewEmpty : UiText.PlayerName(player));
                    UiKit.Set(row.Identity, player == null ? "" : "ЗДР  " + Mathf.CeilToInt(player.Health) + "     O₂  " + Mathf.CeilToInt(player.Oxygen));
                    UiKit.Set(row.State, player == null ? UiText.MenuCrewWaiting : UiText.PlayerState(player));
                    Color signal = player == null ? UiTheme.Edge : player.IsDead||player.IsHelpless||player.IsTrapped ? UiTheme.Red : player.IsHypoxic ? UiTheme.Amber : player.IsInside ? UiTheme.Green : UiTheme.Cyan;
                    row.Mark.color = signal; row.State.color = player == null ? UiTheme.Muted : signal;
                    row.Name.color = player == null ? UiTheme.Muted : UiTheme.Text;
                }
                string id = session == null || session.lobby == null ? "" : session.lobby.LobbyId;
                bool hasLobby = connected && session.SteamMode && !string.IsNullOrEmpty(id);
                UiKit.Set(crewLobby, hasLobby ? UiText.MenuLobby(id) : connected && !session.SteamMode ? UiText.MenuLocalSession : UiText.MenuSteamLobbyInactive);
                copyLobby.gameObject.SetActive(hasLobby); invite.gameObject.SetActive(hasLobby);
                copyLobby.interactable = invite.interactable = hasLobby && !busy;
            }
            if (page == UiPage.Settings)
                foreach (var row in settingRows) UiKit.Set(row.Value, row.Format(row.Read()));
        }

        RectTransform Page(UiPage key, bool scroll, out RectTransform content)
        {
            var root = UiKit.Node(body, key.ToString()); UiKit.Fill(root); pages.Add(key, root);
            if (scroll) { var view = UiKit.Scroll(root, "Scroll", out content); UiKit.Fill((RectTransform)view.transform); }
            else { content = UiKit.Column(root, "Content", 14); UiKit.Fill(content); }
            return root;
        }

        static void SetArea(RectTransform rect, Vector2 position, Vector2 size) => UiKit.Place(rect, new Vector2(0, 1), new Vector2(0, 1), position, size);
        static TMP_Text Line(Transform parent, string name, string text, float height, float size = 22, Color? color = null)
        {
            var label = UiKit.Label(parent, name, text, size, color); UiKit.Size(label, height); return label;
        }

        void BuildMain()
        {
            Page(UiPage.Main, false, out var actions);
            solo=UiKit.Button(actions,"SoloDive","Играть одному",()=>StartConnection(s=>s.HostLocal()),true,82);
            var multiplayer = UiKit.Row(actions, "Multiplayer", 16);UiKit.Size(multiplayer, 70);
            UiKit.Button(multiplayer, "CreateSession", UiText.MenuCreate, () => { createIntent = true; localTest = false; app.ShowPage(UiPage.Connection); });
            UiKit.Button(multiplayer, "JoinSession", UiText.MenuJoin, () => { createIntent = false; localTest = false; app.ShowPage(UiPage.Connection); });
            var utility = UiKit.Row(actions, "Options", 16);UiKit.Size(utility, 60);
            UiKit.Button(utility, "MainSettings", UiText.MenuSettings, () => app.ShowPage(UiPage.Settings));
            UiKit.Button(utility, "MainQuit", UiText.MenuQuit, RequestQuit);
            mainStatus = Line(actions, "Session status", "", 88, 23, UiTheme.Amber);
        }

        void BuildConnection()
        {
            Page(UiPage.Connection, false, out var content);
            var intent = UiKit.Row(content, "ConnectionIntent", 10); UiKit.Size(intent, 48);
            createIntentButton = UiKit.Button(intent, "CreateIntent", UiText.MenuCreateIntent, () => { createIntent = true; RenderConnectionTabs(); }, height: 48);
            joinIntentButton = UiKit.Button(intent, "JoinIntent", UiText.MenuJoinIntent, () => { createIntent = false; RenderConnectionTabs(); }, height: 48);
            var transport = UiKit.Row(content, "Transport", 10); UiKit.Size(transport, 48);
            steamTab = UiKit.Button(transport, "SteamTab", UiText.MenuSteamTab, () => { localTest = false; RenderConnectionTabs(); }, height: 48);
            localTab = UiKit.Button(transport, "LocalTestTab", UiText.MenuLocalTab, () => { localTest = true; RenderConnectionTabs(); }, height: 48);

            var columns = UiKit.Row(content, "ConnectionColumns", 20); UiKit.Size(columns, 408);
            var controls = UiKit.Column(columns, "ConnectionControls", 0); UiKit.Size(controls, width: 660, flexibleWidth: 1);
            var statusColumn = UiKit.Column(columns, "ConnectionStatus", 12); UiKit.Size(statusColumn, width: 400, flexibleWidth: 1);

            steamSection = UiKit.Column(controls, "SteamSection", 12);
            Line(steamSection, "Heading", UiText.MenuSteamTitle, 30, 20, UiTheme.Cyan);
            steamState = Line(steamSection, "ServiceState", UiText.MenuSteamNotInitialized, 30, 22, UiTheme.Muted);
            steamEnable = UiKit.Button(steamSection, "InitializeSteam", UiText.MenuSteamEnable, () => { app.Context.Session?.lobby?.Enable(); Refresh(); }, height: 46);
            steamCreateSection = UiKit.Column(steamSection, "CreateLobbySection", 12);
            Line(steamCreateSection, "Help", UiText.MenuSteamCreateHelp, 56, 21, UiTheme.Muted);
            steamCreate = UiKit.Button(steamCreateSection, "SteamCreateLobby", UiText.MenuSteamCreate, () => StartConnection(s => s.lobby.Create()), true);
            steamJoinSection = UiKit.Column(steamSection, "JoinLobbySection", 12);
            Line(steamJoinSection, "Help", UiText.MenuSteamJoinHelp, 56, 21, UiTheme.Muted);
            lobbyInput = UiKit.Input(steamJoinSection, "LobbyIdInput", "", UiText.MenuSteamIdPlaceholder);
            lobbyInput.characterLimit = 24;
            lobbyInput.onValueChanged.AddListener(_ => Refresh());
            steamJoin = UiKit.Button(steamJoinSection, "SteamJoinLobby", UiText.MenuSteamJoin, () => StartConnection(s => s.lobby.Join(lobbyInput.text.Trim())), true);
            Line(steamSection, "DevelopmentNotice", UiText.MenuSteamDevelopment, 54, 18, UiTheme.Muted);

            localSection = UiKit.Column(controls, "LocalTestingSection", 12);
            Line(localSection, "Heading", UiText.MenuLocalTitle, 32, 20, UiTheme.Amber);
            Line(localSection, "Help", UiText.MenuLocalHelp, 64, 22, UiTheme.Muted);
            addressInput = UiKit.Input(localSection, "AddressInput", "127.0.0.1", UiText.MenuAddressPlaceholder);
            addressInput.onValueChanged.AddListener(_ => Refresh());
            localHost = UiKit.Button(localSection, "LocalHost", UiText.MenuLocalHost, () => StartConnection(s => s.HostLocal()), true);
            localJoin = UiKit.Button(localSection, "LocalJoin", UiText.MenuLocalJoin, () => StartConnection(s => { s.Address = addressInput.text; s.JoinLocal(); }), true);

            connectionBusy = Line(statusColumn, "ConnectionStateHeading", UiText.MenuConnectionStatus, 30, 20, UiTheme.Muted);
            var status = UiKit.Scroll(statusColumn, "ConnectionStatusScroll", out var statusContent); UiKit.Size(status, 288);
            connectionStatus = UiKit.Label(statusContent, "ConnectionStatus", UiText.MenuConnectionMissing, 21);
            cancel = UiKit.Button(statusColumn, "CancelConnection", UiText.MenuConnectionCancel, () => { app.Context.Session?.Disconnect(); Refresh(); });
        }

        void RenderConnectionTabs()
        {
            steamSection.gameObject.SetActive(!localTest); localSection.gameObject.SetActive(localTest);
            steamCreateSection.gameObject.SetActive(createIntent); steamJoinSection.gameObject.SetActive(!createIntent);
            localHost.gameObject.SetActive(createIntent); localJoin.gameObject.SetActive(!createIntent);
            addressInput.gameObject.SetActive(!createIntent);
            Select(createIntentButton, createIntent); Select(joinIntentButton, !createIntent);
            Select(steamTab, !localTest); Select(localTab, localTest);
            Refresh();
        }

        static void Select(Button button, bool selected)
        {
            button.image.color = selected ? UiTheme.Edge : UiTheme.Panel;
            var caption = button.GetComponentInChildren<TMP_Text>(); caption.color = selected ? UiTheme.Text : UiTheme.Muted;
        }

        void StartConnection(Action<RaidSession> connect)
        {
            var session = app.Context.Session;
            if (session == null || session.Busy || session.Connected) return;
            connect(session); Refresh();
        }

        void BuildCrew()
        {
            Page(UiPage.Crew, true, out var content);
            var summary = UiKit.Row(content, "CrewSummary", 18); UiKit.Size(summary, 34);
            crewCount = UiKit.Label(summary, "Count", UiText.MenuCount(0), 24, UiTheme.Cyan); UiKit.Size(crewCount, flexibleWidth: 1);
            crewRole = UiKit.Label(summary, "Role", "", 22, UiTheme.Muted); UiKit.Size(crewRole, width: 350);
            for (int i = 0; i < 5; i++)
            {
                var panel = UiKit.Panel(content, "CrewSlot" + (i + 1), UiTheme.Panel);UiKit.Tile(panel); UiKit.Size(panel, 62);
                var layout = UiKit.Row(panel.transform, "Row", 14, 9); UiKit.Fill(layout);
                var mark = UiKit.Panel(layout, "Signal", UiTheme.Edge); UiKit.Size(mark, width: 5);
                var helmet=UiKit.Symbol(layout,"Suit",DiveInstruments.Mark.Helmet,UiTheme.Cyan); UiKit.Size(helmet,40,40);
                var name = UiKit.Label(layout, "Name", UiText.MenuCrewEmpty, 25); UiKit.Size(name, flexibleWidth: 1);
                var identity = UiKit.Label(layout, "Identity", "", 19, UiTheme.Muted); UiKit.Size(identity, width: 170);
                var state = UiKit.Label(layout, "State", UiText.MenuCrewWaiting, 20, UiTheme.Muted); UiKit.Size(state, width: 245);
                crewRows.Add(new CrewRow { Panel = panel, Mark = mark, Name = name, Identity = identity, State = state });
            }
            crewLobby = Line(content, "LobbyNumber", UiText.MenuSteamLobbyInactive, 38, 22, UiTheme.Muted);
            var actions = UiKit.Row(content, "LobbyActions", 12); UiKit.Size(actions, 52);
            copyLobby = UiKit.Button(actions, "CopyLobbyId", UiText.MenuCopyLobby, () =>
            {
                var session = app.Context.Session;
                if (session?.lobby == null || string.IsNullOrEmpty(session.lobby.LobbyId)) return;
                GUIUtility.systemCopyBuffer = session.lobby.LobbyId; app.Toast(UiText.MenuLobbyCopied);
            });
            invite = UiKit.Button(actions, "SteamInvite", UiText.MenuInvite, () => app.Context.Session?.lobby?.Invite());
        }

        void BuildPause()
        {
            Page(UiPage.Pause, false, out var content);

            Line(content, "DiveControls", UiText.MenuDiveControls, 40, 21, UiTheme.Muted);
            UiKit.Button(content, "PauseResume", UiText.MenuResume, () => app.ShowPage(UiPage.None), true, 64);
            UiKit.Button(content, "PauseSettings", UiText.MenuSettings, () => app.ShowPage(UiPage.Settings), height: 60);
            UiKit.Button(content, "PauseCrew", UiText.MenuCrew, () => app.ShowPage(UiPage.Crew), height: 60);
            UiKit.Button(content, "Disconnect", UiText.MenuDisconnect, RequestDisconnect, height: 60);
            UiKit.Button(content, "PauseQuit", UiText.MenuQuit, RequestQuit, height: 60);
        }

        void BuildSettings()
        {
            Page(UiPage.Settings, true, out var content);
            AddSetting(content, "Scale", UiText.MenuScale, .9f, 1.2f, () => app.Settings.scale, value => app.Settings.scale = value, UiText.MenuPercent);
            AddSetting(content, "Fov", UiText.MenuFov, 65, 100, () => app.Settings.fov, value => app.Settings.fov = value, UiText.MenuDegrees);
            AddSetting(content, "Sensitivity", UiText.MenuSensitivity, .4f, 2, () => app.Settings.sensitivity, value => app.Settings.sensitivity = value, UiText.MenuPercent);
            Line(content, "LocalOnly", UiText.MenuSettingsLocalOnly, 62, 21, UiTheme.Muted);
            UiKit.Button(content, "RestoreDefaults", UiText.MenuRestore, () =>
            {
                app.Settings.scale = 1; app.Settings.fov = 80; app.Settings.sensitivity = 1;
                foreach (var row in settingRows) row.Slider.SetValueWithoutNotify(row.Read());
                app.ApplySettings(); Refresh(); app.Toast(UiText.MenuRestoreDone);
            });
        }

        void AddSetting(Transform parent, string name, string caption, float min, float max, Func<float> read, Action<float> write, Func<float, string> format)
        {
            var panel = UiKit.Panel(parent, name + "Setting", UiTheme.Panel);UiKit.Tile(panel); UiKit.Size(panel, 92);
            var column = UiKit.Column(panel.transform, "Content", 8, 12); UiKit.Fill(column);
            var heading = UiKit.Row(column, "Heading", 16); UiKit.Size(heading, 28);
            var label = UiKit.Label(heading, "Label", caption, 21, UiTheme.Muted); UiKit.Size(label, flexibleWidth: 1);
            var valueText = UiKit.Label(heading, "Value", format(read()), 23, UiTheme.Text); UiKit.Size(valueText, width: 120); valueText.alignment = TextAlignmentOptions.Right;
            var sliderRoot = UiKit.Panel(column, name + "Slider", new Color(0, 0, 0, 0), true); UiKit.Size(sliderRoot, 28);
            var slider = sliderRoot.gameObject.AddComponent<Slider>(); slider.direction = Slider.Direction.LeftToRight; slider.minValue = min; slider.maxValue = max;
            var background = UiKit.Panel(sliderRoot.transform, "Track", UiTheme.Edge);
            background.rectTransform.anchorMin = new Vector2(0, .5f); background.rectTransform.anchorMax = new Vector2(1, .5f); background.rectTransform.sizeDelta = new Vector2(-20, 6);
            var fillArea = UiKit.Node(sliderRoot.transform, "FillArea"); UiKit.Fill(fillArea); fillArea.offsetMin = new Vector2(10, 10); fillArea.offsetMax = new Vector2(-10, -10);
            var fill = UiKit.Panel(fillArea, "Fill", UiTheme.Cyan); UiKit.Fill(fill.rectTransform); slider.fillRect = fill.rectTransform;
            var handleArea = UiKit.Node(sliderRoot.transform, "HandleArea"); UiKit.Fill(handleArea); handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
            var handle = UiKit.Panel(handleArea, "Handle", UiTheme.Text, true); UiKit.Place(handle.rectTransform, new Vector2(0, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(20, 26));
            slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            var colors = slider.colors; colors.highlightedColor = UiTheme.Cyan; colors.selectedColor = UiTheme.Cyan; colors.pressedColor = UiTheme.Amber; colors.fadeDuration = .08f; slider.colors = colors;
            slider.SetValueWithoutNotify(read());
            slider.onValueChanged.AddListener(value => { write(value); UiKit.Set(valueText, format(value)); app.ApplySettings(); });
            settingRows.Add(new SettingRow { Slider = slider, Value = valueText, Read = read, Format = format });
        }

        void RequestDisconnect()
        {
            var session = app.Context.Session;
            bool host = session != null && session.manager != null && session.manager.IsServerStarted;
            app.Confirm(UiText.MenuDisconnectTitle, host ? UiText.MenuDisconnectHost : UiText.MenuDisconnectClient, () =>
            {
                app.Context.Session?.Disconnect(); app.ShowPage(UiPage.Main);
            });
        }

        void RequestQuit()
        {
            app.Confirm(UiText.MenuQuitTitle, UiText.MenuQuitDescription, () =>
            {
                app.Context.Session?.Disconnect();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        }
    }
}
