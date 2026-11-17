using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderwaterFishing.Player
{
    public sealed class DiverInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionAsset runtimeActions;
        private InputAction move, vertical, look, sprint, interact, help, fire, reel, release, sonar, menu, info;
        private bool uiCaptured;
        public bool MenuOpen { get; private set; } = true;
        private bool Captured => MenuOpen || uiCaptured;
        public bool InteractPressed => !Captured && interact.WasPressedThisFrame();
        public bool HelpHeld => !Captured && help.IsPressed();
        public bool FirePressed => !Captured && fire.WasPressedThisFrame();
        public bool ReelHeld => !Captured && reel.IsPressed();
        public bool ReleasePressed => !Captured && release.WasPressedThisFrame();
        public bool SonarPressed => !Captured && sonar.WasPressedThisFrame();
        public bool InfoPressed => !MenuOpen && info.WasPressedThisFrame();
        public bool Sprint => !Captured && sprint.IsPressed();
        public Vector3 Movement => Captured ? Vector3.zero : new Vector3(move.ReadValue<Vector2>().x, vertical.ReadValue<float>(), move.ReadValue<Vector2>().y);
        public Vector2 Look => Captured ? Vector2.zero : look.ReadValue<Vector2>();
        public void Configure(InputActionAsset asset) => actions = asset;

        private void Awake()
        {
            runtimeActions = Instantiate(actions);
            move = runtimeActions.FindAction("Diver/Move", true);
            vertical = runtimeActions.FindAction("Diver/Vertical", true);
            look = runtimeActions.FindAction("Diver/Look", true);
            sprint = runtimeActions.FindAction("Diver/Sprint", true);
            interact = runtimeActions.FindAction("Diver/Interact", true);
            help = runtimeActions.FindAction("Diver/Help", true);
            fire = runtimeActions.FindAction("Diver/Fire", true);
            reel = runtimeActions.FindAction("Diver/Reel", true);
            release = runtimeActions.FindAction("Diver/Release", true);
            sonar = runtimeActions.FindAction("Diver/Sonar", true);
            menu = runtimeActions.FindAction("Diver/Menu", true);
            info = runtimeActions.FindAction("Diver/Info", true);
        }

        private void OnEnable() { if (runtimeActions != null) runtimeActions.Enable(); }
        private void OnDisable() { if (runtimeActions != null) runtimeActions.Disable(); }
        private void Update()
        {
            if (NetworkDiver.Local == null) { if (!MenuOpen) SetMenu(true); return; }
            if (menu.WasPressedThisFrame()) SetMenu(!MenuOpen);
        }
        public void SetMenu(bool open)
        {
            MenuOpen = open;
            UpdateCursor();
        }
        public void SetUiCapture(bool captured) { uiCaptured = captured; UpdateCursor(); }
        private void UpdateCursor() { Cursor.lockState = Captured ? CursorLockMode.None : CursorLockMode.Locked; Cursor.visible = Captured; }
        private void OnDestroy() { if (runtimeActions != null) Destroy(runtimeActions); }
    }
}
