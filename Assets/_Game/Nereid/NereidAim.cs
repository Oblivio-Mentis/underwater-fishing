using Raid.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Raid.Nereid
{
    /// <summary>Owned-view aiming only. Firing, hit tests and movement remain server-owned.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(-9000)]
    public sealed class NereidAim : MonoBehaviour
    {
        public static NereidAim Active {get;private set;}
        public static float Blend => Active!=null?Active.amount:0;
        public static float SensitivityScale => Active!=null?Mathf.Lerp(1,Active.aimSensitivityRatio,Active.amount):1;
        [Range(.08f,.3f)] public float enterSeconds=.14f;
        [Range(.08f,.3f)] public float exitSeconds=.11f;
        [Range(.4f,.85f)] public float aimFovRatio=.65f;
        [Range(.2f,.8f)] public float aimSensitivityRatio=.42f;
        public bool IsAiming {get;private set;}
        float progress,amount;
        bool releaseRequired;
        DiverMotor player;

        void OnEnable()
        {
            if(Active!=null&&Active!=this){enabled=false;return;}
            Active=this;Clear();
        }

        void Update()
        {
            var ui=UiRoot.Instance;
            var owned=ui==null?null:ui.Context.Player;
            bool held=Mouse.current!=null&&Mouse.current.rightButton.isPressed;
            if(!held)releaseRequired=false;
            if(player!=owned){RestoreSensitivity(player);player=owned;Clear();releaseRequired=held;}
            bool available=ui!=null&&ui.Context.Connected&&player!=null&&player.NetworkObject!=null&&player.IsOwner&&player.IsClientInitialized&&!player.IsHelpless;
            bool permitted=available&&ui.Page==UiPage.None&&!UiInputGate.Blocked&&Application.isFocused&&Cursor.lockState==CursorLockMode.Locked;
            if(!permitted)
            {
                Clear();releaseRequired|=held;
            }
            else
            {
                IsAiming=held&&!releaseRequired;
                float duration=IsAiming?enterSeconds:exitSeconds;
                progress=Mathf.MoveTowards(progress,IsAiming?1:0,Mathf.Min(Time.unscaledDeltaTime,.05f)/Mathf.Max(.01f,duration));
                amount=progress*progress*(3-2*progress);
            }
            if(available)player.LocalLookMultiplier=ui.Settings.sensitivity*SensitivityScale;
            else RestoreSensitivity(player);
        }

        public float ResolveFieldOfView(float baseFov) => Mathf.Lerp(baseFov,baseFov*aimFovRatio,amount);
        void Clear(){progress=0;amount=0;IsAiming=false;}

        static void RestoreSensitivity(DiverMotor diver)
        {
            var ui=UiRoot.Instance;if(diver!=null&&ui!=null&&ui.Settings!=null)diver.LocalLookMultiplier=ui.Settings.sensitivity;
        }

        void OnApplicationFocus(bool focused)
        {
            if(focused)return;Clear();releaseRequired=true;RestoreSensitivity(player);
        }

        void OnDisable()
        {
            if(Active!=this)return;
            Clear();RestoreSensitivity(player);Active=null;
            var ui=UiRoot.Instance;
            if(ui!=null&&ui.Settings!=null&&ui.Page==UiPage.None&&player!=null)
            {var camera=player.GetComponentInChildren<Camera>();if(camera!=null)camera.fieldOfView=ui.Settings.fov;}
            player=null;
        }
    }
}
