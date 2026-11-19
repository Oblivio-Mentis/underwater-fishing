#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
            public float x,y,z,oxygen,stress,depth,travel,rescueProgress,uiScale,fov,sensitivity,volume,aimYaw,aimPitch;
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-raid-ui-probe");
            if(index<0||index+1>=args.Length)return;
            var go=new GameObject("RAID011 explicit local QA probe");var probe=go.AddComponent<UiQaProbe>();probe.directory=Path.GetFullPath(args[index+1]);Directory.CreateDirectory(probe.directory);
        }
        void Update()
        {
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
                team=context.View==null?"":JsonUtility.ToJson(context.View),hookedFish=fish==null?"":fish.SpeciesId,hookMode=fish==null?"":fish.Mode.ToString(),tension=fish==null?0:fish.Tension,stamina=fish==null?0:fish.Stamina,
                rescueProgress=context.Rescue==null?0:context.Rescue.DisplayProgress,stockLoss=world==null?0:world.StockLossCount,wave=context.Threats!=null&&context.Threats.WaveActive};
            if(context.Harpoon!=null)
            {
                var tool=context.Harpoon;snapshot.tetherActive=tool.TetherActive;snapshot.isPulled=tool.IsPulled;
                snapshot.tetherOwner=tool.TetherOwner==null?-1:tool.TetherOwner.OwnerId;snapshot.tetherTarget=tool.TetherTarget==null?-1:tool.TetherTarget.OwnerId;
                snapshot.tetherRefusal=tool.TetherOptOutRemaining;
            }
            if(player!=null){snapshot.owner=player.OwnerId;snapshot.x=player.transform.position.x;snapshot.y=player.transform.position.y;snapshot.z=player.transform.position.z;snapshot.oxygen=player.Oxygen;snapshot.stress=player.PressureStress;snapshot.depth=player.Depth;}
            var camera=player==null?null:player.GetComponentInChildren<Camera>();if(camera!=null){snapshot.aimYaw=camera.transform.eulerAngles.y;snapshot.aimPitch=Mathf.DeltaAngle(0,camera.transform.eulerAngles.x);}
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
