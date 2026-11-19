using UnityEngine;
namespace Raid.UI
{
    public static class UiTheme
    {
        public static readonly Color Steel = Hex("122024");
        public static readonly Color Panel = Hex("223438");
        public static readonly Color Edge = Hex("58706C");
        public static readonly Color Text = Hex("F0F3E9");
        public static readonly Color Muted = Hex("A8BEB5");
        public static readonly Color Cyan = Hex("9BD6C4");
        public static readonly Color Green = Hex("82D283");
        public static readonly Color Amber = Hex("F0BC68");
        public static readonly Color Red = Hex("F48065");
        static Color Hex(string value) { Color c; ColorUtility.TryParseHtmlString("#"+value,out c); return c; }
    }
    // Local presentation state only. The server continues validating every action.
    public static class UiInputGate
    {
        public static bool Active, Modal, AllowAssist, PointerReserved;
        public static float ResumeAt;
        public static bool Blocked => Active && (Modal || Time.unscaledTime < ResumeAt || !Application.isFocused);
        public static void Reset() { Active=false; Modal=false; AllowAssist=false; PointerReserved=false; ResumeAt=0; }
    }
    public enum UiPage { None, Main, Connection, Crew, Terminal, Cargo, Pause, Settings }
    public interface IUiController
    {
        UiContext Context { get; }
        UiSettings Settings { get; }
        bool CommandPending { get; }
        void Send(WorldAction action, long id=0);
        void Confirm(string title, string description, System.Action confirmed);
        void ShowPage(UiPage page);
        void ClosePanel();
        void Toast(string message, bool dangerous=false);
        void ApplySettings();
    }
    [System.Serializable]
    public sealed class UiSettings
    {
        public float volume=0.8f, scale=1f, fov=80f, sensitivity=1f;
        public static UiSettings Load() => new UiSettings {
            volume=Mathf.Clamp01(PlayerPrefs.GetFloat("RAID011.volume",.8f)),
            scale=Mathf.Clamp(PlayerPrefs.GetFloat("RAID011.scale",1f),.9f,1.2f),
            fov=Mathf.Clamp(PlayerPrefs.GetFloat("RAID011.fov",80f),65f,100f),
            sensitivity=Mathf.Clamp(PlayerPrefs.GetFloat("RAID011.sensitivity",1f),.4f,2f) };
        public void Save()
        {
            PlayerPrefs.SetFloat("RAID011.volume",volume); PlayerPrefs.SetFloat("RAID011.scale",scale);
            PlayerPrefs.SetFloat("RAID011.fov",fov); PlayerPrefs.SetFloat("RAID011.sensitivity",sensitivity); PlayerPrefs.Save();
        }
    }
}
