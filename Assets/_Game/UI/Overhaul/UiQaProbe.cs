#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.UI;
namespace Raid.UI
{
    // Explicit opt-in local QA driver. Only UI callbacks and owned-player actions;
    // no direct server state, save edits or code execution.
    // Omitted from non-development players; inactive unless -raid-ui-probe <directory> is provided.
    public sealed class UiQaProbe : MonoBehaviour
    {
        [Serializable] public sealed class Request { public string id,action,name,value;public float number,number2; }
        [Serializable] public sealed class Snapshot
        {
            public string request,page,error,cursor,worldMessage,scene;
            public bool connected,pending,inputBlocked,atBase,lastSuccess,wave;
            public int width,height,owner,players,events,canvases,audioListeners,stockLoss;
            public float x,y,z,oxygen,stress,depth,travel,rescueProgress,uiScale,fov,sensitivity,volume,aimYaw,aimPitch,actualFov,aimBlend,lookMultiplier;
            public bool aiming,aimQaActive,applicationFocused;
            public string team,hookedFish,hookMode;
            public float tension,stamina;
            public bool tetherActive,isPulled;
            public int tetherOwner=-1,tetherTarget=-1;
            public float tetherRefusal;
            public Control[] controls;
        }
        [Serializable] public sealed class Control { public string name,text;public bool interactable; public float x,y,w,h; }
        string directory,lastId="",error="";float nextPoll,keyboardReleaseAt,mouseReleaseAt;bool releaseKeyboard,releaseMouse;
        Vector2 mousePosition;
        [InputControlLayout(canRunInBackground=true)]
        public sealed class BackgroundAimMouse : Mouse { }
        const string AimMouseLayout="RAID011BackgroundAimMouse";
        BackgroundAimMouse aimMouse;
        InputSettings originalInputSettings,qaInputSettings;
        float aimReleaseAt;
        bool aimReleasePending;
        static UiQaProbe aimTestOwner;

        // Available to Editor MCP as well as the explicit file driver. Never compiled into a release player.
        public void BeginAimTest(float seconds=5)
        {
            EndAimTest();
            if(aimTestOwner!=null&&aimTestOwner!=this)throw new InvalidOperationException("Another aim QA scope is already active");
            var aim=Nereid.NereidAim.Active;if(aim==null)throw new InvalidOperationException("NereidAim is not installed");
            aimTestOwner=this;
            try
            {
                originalInputSettings=InputSystem.settings;qaInputSettings=Instantiate(originalInputSettings);
                qaInputSettings.name="Transient NEREID synthetic aim input";qaInputSettings.hideFlags=HideFlags.HideAndDontSave;
                qaInputSettings.backgroundBehavior=InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
#if UNITY_EDITOR
                qaInputSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
                InputSystem.settings=qaInputSettings;
                InputSystem.RegisterLayout<BackgroundAimMouse>(AimMouseLayout);aimMouse=InputSystem.AddDevice<BackgroundAimMouse>();
                aim.BeginBackgroundQa(aimMouse,seconds);QueueAimButton(false);
            }
            catch{EndAimTest();throw;}
        }

        public void QueueAimButton(bool held,float seconds=.35f)
        {
            if(aimMouse==null||!aimMouse.added)throw new InvalidOperationException("BeginAimTest first");
            InputSystem.QueueStateEvent(aimMouse,new MouseState{position=new Vector2(Screen.width*.5f,Screen.height*.5f),buttons=(ushort)(held?2:0)});
            aimReleasePending=held;aimReleaseAt=Time.unscaledTime+Mathf.Clamp(seconds,.05f,10);
        }

        public void EndAimTest()
        {
            if(aimTestOwner==this)aimTestOwner=null;
            aimReleasePending=false;
            if(aimMouse!=null)
            {
                var aim=Nereid.NereidAim.Active;if(aim!=null)aim.EndBackgroundQa();
                if(aimMouse.added)InputSystem.RemoveDevice(aimMouse);aimMouse=null;
            }
            if(qaInputSettings!=null)
            {
                if(InputSystem.settings==qaInputSettings&&originalInputSettings!=null)InputSystem.settings=originalInputSettings;
                Destroy(qaInputSettings);qaInputSettings=null;originalInputSettings=null;
            }
        }
        void OnDisable(){EndAimTest();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-raid-ui-probe");
            if(index<0||index+1>=args.Length)return;
            var go=new GameObject("RAID011 explicit local QA probe");var probe=go.AddComponent<UiQaProbe>();probe.directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(probe.directory);
        }
        void Update()
        {
            if(aimMouse!=null)
            {
                var aim=Nereid.NereidAim.Active;
                if(aim==null||!aim.BackgroundQaActive)EndAimTest();
                else if(aimReleasePending&&Time.unscaledTime>=aimReleaseAt)QueueAimButton(false);
            }
            if(releaseKeyboard && Time.unscaledTime>=keyboardReleaseAt){releaseKeyboard=false;InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());}
            if(releaseMouse && Time.unscaledTime>=mouseReleaseAt){releaseMouse=false;InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=mousePosition});}
            if(string.IsNullOrEmpty(directory)||UiRoot.Instance==null||Time.unscaledTime<nextPoll)return;nextPoll=Time.unscaledTime+.2f;
            string path=Path.Combine(directory,"request.json");
            try
            {
                if(File.Exists(path))
                {
                    var request=JsonUtility.FromJson<Request>(File.ReadAllText(path));
                    if(request!=null&&!string.IsNullOrEmpty(request.id)&&request.id!=lastId)
                    {lastId=request.id;error="";Execute(request);}
                }
            }
            catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;}
            WriteSnapshot();
        }
        void Execute(Request r)
        {
            if(r.action=="aim_scope"){if(r.value=="end")EndAimTest();else BeginAimTest(r.number>0?r.number:5);return;}
            if(r.action=="aim_button"){QueueAimButton(r.value!="release",r.number>0?r.number:.35f);return;}
            if(r.action=="console")
            {
                var station=ShipConsole.Stations.FirstOrDefault(s=>s!=null&&s.name==r.name);
                if(station==null)throw new InvalidOperationException("Physical console not found");
                UiRoot.Instance.UseConsole(station);return;
            }
            if(r.action=="cancel_tether")
            {
                var tool=UiRoot.Instance.Context.Harpoon;
                if(tool==null||!tool.IsOwner)throw new InvalidOperationException("Owned player is not connected");
                tool.CancelFriendTether();return;
            }
            if(r.action=="capture")
            {
                string leaf=Path.GetFileName(string.IsNullOrEmpty(r.value)?r.name:r.value);
                if(string.IsNullOrEmpty(leaf)||!leaf.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Capture requires a PNG filename");
                ScreenCapture.CaptureScreenshot(Path.Combine(directory,leaf));return;
            }
            if(r.action=="key")
            {
                Key key;if(!Enum.TryParse(r.value,true,out key))throw new ArgumentException("Unknown key");
                var keyboard=Keyboard.current;if(keyboard==null)keyboard=InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));releaseKeyboard=true;keyboardReleaseAt=Time.unscaledTime+Mathf.Clamp(r.number,.05f,10);return;
            }
            if(r.action=="mouse") {mousePosition=new Vector2(Screen.width*.5f,Screen.height*.5f);InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=mousePosition,buttons=(ushort)(r.value=="release"?0:1)});releaseMouse=r.value!="release";mouseReleaseAt=Time.unscaledTime+Mathf.Clamp(r.number,.05f,10);return;}
            if(r.action=="pointer") {mousePosition=new Vector2(r.number,r.number2);InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=mousePosition,buttons=(ushort)(r.value=="move"?0:1)});releaseMouse=r.value!="move";mouseReleaseAt=Time.unscaledTime+.12f;return;}
            if(r.action=="look") {InputSystem.QueueStateEvent(Mouse.current,new MouseState{delta=new Vector2(r.number,r.number2)});return;}
            if(r.action=="click")
            {
                var button=UnityEngine.Object.FindObjectsByType<Button>().FirstOrDefault(b=>b.name==r.name&&b.IsActive()&&b.IsInteractable());
                if(button==null)throw new InvalidOperationException("Active interactable button not found: "+r.name);
                ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);return;
            }
            if(r.action=="input")
            {
                var input=UnityEngine.Object.FindObjectsByType<TMP_InputField>().FirstOrDefault(t=>t.name==r.name&&t.isActiveAndEnabled);
                if(input==null)throw new InvalidOperationException("Input not found: "+r.name);input.text=r.value;input.onEndEdit.Invoke(r.value);return;
            }
            if(r.action=="slider")
            {
                var slider=UnityEngine.Object.FindObjectsByType<Slider>().FirstOrDefault(t=>t.name==r.name&&t.isActiveAndEnabled);
                if(slider==null)throw new InvalidOperationException("Slider not found: "+r.name);slider.value=r.number;return;
            }
            if(r.action!="snapshot")throw new InvalidOperationException("Unsupported UI-only QA action");
        }
        void WriteSnapshot()
        {
            var root=UiRoot.Instance;var context=root.Context;var player=context.Player;var world=context.World;var fish=context.Harpoon==null?null:context.Harpoon.HookedFish;
            var snapshot=new Snapshot{request=lastId,page=root.Page.ToString(),error=error,cursor=Cursor.lockState.ToString(),connected=context.Connected,pending=root.CommandPending,inputBlocked=UiInputGate.Blocked,
                width=Screen.width,height=Screen.height,players=context.Players.Count,atBase=context.AtBase,worldMessage=world==null?"":world.Message,travel=world==null?0:world.TravelRemaining,
                scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,events=UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.isActiveAndEnabled),
                canvases=UnityEngine.Object.FindObjectsByType<Canvas>().Count(c=>c.isActiveAndEnabled&&c.isRootCanvas),audioListeners=UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled),
                uiScale=root.Settings.scale,fov=root.Settings.fov,sensitivity=root.Settings.sensitivity,volume=root.Settings.volume,lastSuccess=root.LastCommandSucceeded,
                aimBlend=Nereid.NereidAim.Blend,aiming=Nereid.NereidAim.Active!=null&&Nereid.NereidAim.Active.IsAiming,aimQaActive=Nereid.NereidAim.Active!=null&&Nereid.NereidAim.Active.BackgroundQaActive,applicationFocused=Application.isFocused,
                team=context.View==null?"":JsonUtility.ToJson(context.View),hookedFish=fish==null?"":fish.SpeciesId,hookMode=fish==null?"":fish.Mode.ToString(),tension=fish==null?0:fish.Tension,stamina=fish==null?0:fish.Stamina,
                rescueProgress=context.Rescue==null?0:context.Rescue.DisplayProgress,stockLoss=world==null?0:world.StockLossCount,wave=context.Threats!=null&&context.Threats.WaveActive};
            if(context.Harpoon!=null)
            {
                var tool=context.Harpoon;snapshot.tetherActive=tool.TetherActive;snapshot.isPulled=tool.IsPulled;
                snapshot.tetherOwner=tool.TetherOwner==null?-1:tool.TetherOwner.OwnerId;snapshot.tetherTarget=tool.TetherTarget==null?-1:tool.TetherTarget.OwnerId;
                snapshot.tetherRefusal=tool.TetherOptOutRemaining;
            }
            if(player!=null){snapshot.owner=player.OwnerId;snapshot.x=player.transform.position.x;snapshot.y=player.transform.position.y;snapshot.z=player.transform.position.z;snapshot.oxygen=player.Oxygen;snapshot.stress=player.PressureStress;snapshot.depth=player.Depth;snapshot.lookMultiplier=player.LocalLookMultiplier;}
            var camera=player==null?null:player.GetComponentInChildren<Camera>();if(camera!=null){snapshot.actualFov=camera.fieldOfView;snapshot.aimYaw=camera.transform.eulerAngles.y;snapshot.aimPitch=Mathf.DeltaAngle(0,camera.transform.eulerAngles.x);}
            snapshot.controls=UnityEngine.Object.FindObjectsByType<Button>().Where(b=>b.IsActive()).Select(b=>
            {
                var rect=(RectTransform)b.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);var label=b.GetComponentInChildren<TMP_Text>();
                var canvas=b.GetComponentInParent<Canvas>();
                if(canvas!=null && canvas.renderMode==RenderMode.WorldSpace && canvas.worldCamera!=null)
                    for(int i=0;i<corners.Length;i++)corners[i]=canvas.worldCamera.WorldToScreenPoint(corners[i]);
                return new Control{name=b.name,text=label==null?"":label.text,interactable=b.IsInteractable(),x=corners[0].x,y=corners[0].y,w=corners[2].x-corners[0].x,h=corners[2].y-corners[0].y};
            }).ToArray();
            File.WriteAllText(Path.Combine(directory,"state.json.tmp"),JsonUtility.ToJson(snapshot,true));
            File.Copy(Path.Combine(directory,"state.json.tmp"),Path.Combine(directory,"state.json"),true);
        }
    }
}
#endif
