using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderwaterFishing.Player
{
    public sealed class DiverInput : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        private InputActionAsset runtimeActions;
        private InputAction move, vertical, look, sprint, interact, help, fire, reel, release, sonar, menu;
        public bool MenuOpen { get; private set; } = true;
        public bool InteractPressed => !MenuOpen && interact.WasPressedThisFrame();
        public bool HelpHeld => !MenuOpen && help.IsPressed();
        public bool FirePressed => !MenuOpen && fire.WasPressedThisFrame();
        public bool ReelHeld => !MenuOpen && reel.IsPressed();
        public bool ReleasePressed => !MenuOpen && release.WasPressedThisFrame();
        public bool SonarPressed => !MenuOpen && sonar.WasPressedThisFrame();
        public bool Sprint => !MenuOpen && sprint.IsPressed();
        public Vector3 Movement => MenuOpen ? Vector3.zero : new Vector3(move.ReadValue<Vector2>().x, vertical.ReadValue<float>(), move.ReadValue<Vector2>().y);
        public Vector2 Look => MenuOpen ? Vector2.zero : look.ReadValue<Vector2>();
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
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
        private void OnDestroy() { if (runtimeActions != null) Destroy(runtimeActions); }
    }
}
