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
using UnityEngine.Rendering;
using UnityEngine.UI;
namespace Raid.UI
{
    // Explicit opt-in local QA driver. Only UI callbacks and owned-player actions;
    // no direct server state, save edits or code execution.
    // Omitted from non-development players; inactive unless -raid-ui-probe <directory> is provided.
    [DefaultExecutionOrder(350)]
    public sealed class UiQaProbe : MonoBehaviour
    {
        [Serializable] public sealed class Request { public string id,action,name,value;public float number,number2;public Vector3 point; }
        [Serializable] public sealed class Snapshot
        {
            public bool dead,consumed,canReturn,hypoxic,pointerReserved;
            public float health,hypoxia,airlockWater,innerDoor,outerDoor;
            public string airlockPhase,reticleControl;
            public int processingPresses,feedingPresses;
            public GoshaSnapshot gosha;
            public bool goshaModelVisible;
            public int watchedOwner=-1,depthTier;
            public string request,page,error,cursor,worldMessage,scene;
            public bool connected,pending,inputBlocked,lastSuccess,wave;
            public int width,height,owner,players,events,canvases,audioListeners,audioSources,stockLoss;
            public float x,y,z,oxygen,stress,depth,travel,rescueProgress,uiScale,fov,sensitivity,aimYaw,aimPitch,actualFov,aimBlend,lookMultiplier;
            public bool aiming,aimQaActive,applicationFocused;
            public string team,hookedFish,hookMode;
            public PlayerProgress localProgress;
            public int skillTreeNodes;
            public string[] pockets;
            public string heldSpecies, harpoonInstance, selectedItem, selectedInstance;
            public DropReadout[] droppedItems;
            public bool handsFull;
            public int activeSlot, helperOwner=-1, assistedOwner=-1, cageSwimmers;
            public FishRouteState fishRoute;
            public int observedBurps;
            public long observedSwallows;
            public float tension,stamina;
            // RAID-018: replicated fight/predator readouts seen by this independent client.
            public string fishBehaviour, anglerStage;
            public int fishHookSequence, fishTugSequence, biteSequence, biteVictim;
            public float fishTugStrength, anglerDistance;
            public bool tetherActive,isPulled;
            public int tetherOwner=-1,tetherTarget=-1;
            public float tetherRefusal;
            public string harpoonPhase;
            public int harpoonShots;
            public bool harpoonAttached,harpoonSawFlight,harpoonSawAttach,harpoonSawRetract;
            public float harpoonShotAge,harpoonSpool,harpoonTipX,harpoonTipY,harpoonTipZ,harpoonEndX,harpoonEndY,harpoonEndZ;
            public int[] activeBoltOwners;
            public int anchorOwner=-1, anchorShot, anchorFrames;
            public float anchorTravel, anchorTipError, anchorKnotError;
            public Control[] controls;
        }
        [Serializable] public sealed class DropReadout { public string instance, kind, species; public Vector3 position; }
        [Serializable] public sealed class Control { public string name,text;public bool interactable; public float x,y,w,h; }
        string directory,lastId="",error="";float nextPoll,keyboardReleaseAt,mouseReleaseAt;bool releaseKeyboard,releaseMouse;
        Vector2 mousePosition;
        [InputControlLayout(canRunInBackground=true)]
        public sealed class BackgroundAimMouse : Mouse { }
        const string AimMouseLayout="RAID011BackgroundAimMouse";
        BackgroundAimMouse aimMouse;
        InputSettings originalInputSettings,qaInputSettings;
        HideFlags originalInputFlags;
        float aimReleaseAt;
        bool aimReleasePending;
        static UiQaProbe aimTestOwner;
        HarpoonTool observedTool;
        HarpoonShotFx observedFx;
        int observedShots=-1;
        bool sawFlight,sawAttach,sawRetract;

        // Available to Editor MCP as well as the explicit file driver. Never compiled into a release player.
        public void BeginAimTest(float seconds=5)
        {
            EndAimTest();
            if(aimTestOwner!=null&&aimTestOwner!=this)throw new InvalidOperationException("Another aim QA scope is already active");
            var aim=Nereid.NereidAim.Active;if(aim==null)throw new InvalidOperationException("NereidAim is not installed");
            aimTestOwner=this;
            try
            {
                originalInputSettings=InputSystem.settings;originalInputFlags=originalInputSettings.hideFlags;
                // InputManager.settings destroys the previous object when its flags equal HideAndDontSave.
                // Preserve an engine-created default settings object until the scope returns ownership.
                if(originalInputFlags==HideFlags.HideAndDontSave)originalInputSettings.hideFlags=HideFlags.DontSave;
                qaInputSettings=Instantiate(originalInputSettings);
                qaInputSettings.name="Transient NEREID synthetic aim input";qaInputSettings.hideFlags=HideFlags.DontSave|HideFlags.HideInInspector;
#if UNITY_EDITOR
                UnityEditor.AssemblyReloadEvents.beforeAssemblyReload+=EndAimTest;
                UnityEditor.EditorApplication.playModeStateChanged+=OnEditorPlayMode;
#endif
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
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload-=EndAimTest;
            UnityEditor.EditorApplication.playModeStateChanged-=OnEditorPlayMode;
#endif
            if(aimTestOwner==this)aimTestOwner=null;
            aimReleasePending=false;
            // Restore first: device callbacks must see live settings, even during shutdown/reload.
            if(originalInputSettings!=null)
            {
                InputSystem.settings=originalInputSettings;
                originalInputSettings.hideFlags=originalInputFlags;
                originalInputSettings=null;
            }
            if(aimMouse!=null)
            {
                var aim=Nereid.NereidAim.Active;if(aim!=null)aim.EndBackgroundQa();
                if(aimMouse.added)InputSystem.RemoveDevice(aimMouse);aimMouse=null;
            }
            if(qaInputSettings!=null)
            {
                // Never destroy an object still owned by InputManager if an external tool invalidated the original.
                if(!ReferenceEquals(InputSystem.settings,qaInputSettings))Destroy(qaInputSettings);
                qaInputSettings=null;
            }
        }
#if UNITY_EDITOR
        void OnEditorPlayMode(UnityEditor.PlayModeStateChange state)
        {if(state==UnityEditor.PlayModeStateChange.ExitingPlayMode)EndAimTest();}
#endif
        void OnDisable(){EndAimTest();}
        void LateUpdate()
        {
            var root=UiRoot.Instance;var tool=root==null?null:root.Context.Harpoon;
            if(observedTool!=tool){observedTool=tool;observedFx=null;observedShots=-1;sawFlight=sawAttach=sawRetract=false;}
            if(tool==null)return;
            if(observedFx==null)observedFx=tool.GetComponentInChildren<HarpoonShotFx>(true);
            if(observedFx==null)return;
            if(observedShots!=observedFx.AcceptedShotCount){observedShots=observedFx.AcceptedShotCount;sawFlight=sawAttach=sawRetract=false;}
            sawFlight|=observedFx.Phase==HarpoonShotFx.CablePhase.Flying;
            sawAttach|=observedFx.Phase==HarpoonShotFx.CablePhase.Attached&&observedFx.HasConfirmedAttachment;
            sawRetract|=observedFx.Phase==HarpoonShotFx.CablePhase.Retracting;
        }
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
                    using var requestFile=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                    using var reader=new StreamReader(requestFile);
                    var request=JsonUtility.FromJson<Request>(reader.ReadToEnd());
                    if(request!=null&&!string.IsNullOrEmpty(request.id)&&request.id!=lastId)
                    {lastId=request.id;error="";Execute(request);}
                }
            }
            catch(Exception exception){error=exception.GetType().Name+": "+exception.Message;}
            WriteSnapshot();
        }
        void Execute(Request r)
        {
            if(r.action=="view" || r.action=="spectate" || r.action=="return_to_ship")
            {
                var diver=UiRoot.Instance.Context.Player;
                if(diver==null || !diver.IsOwner || UiRoot.Instance.Page!=UiPage.None)throw new InvalidOperationException("Local HUD required");
                if(r.action=="spectate")diver.CycleSpectator();
                else if(r.action=="return_to_ship")diver.RequestRespawn();
                else diver.LookAtConsole(diver.transform.position+Vector3.up*.6f+Quaternion.Euler(r.number2,r.number,0)*Vector3.forward*10f);
                return;
            }
            if(r.action=="reticle_aim")
            {
                var root=UiRoot.Instance;var diver=root.Context.Player;
                if(diver==null || !diver.IsOwner || (root.Page!=UiPage.None && root.Page!=UiPage.Terminal && root.Page!=UiPage.SkillTree))
                    throw new InvalidOperationException("Local gameplay view required");
                var station=ShipConsole.Stations.FirstOrDefault(target=>target!=null && target.name==r.name);
                if(station!=null && station.screenAnchor!=null)diver.LookAtConsole(station.screenAnchor.position);
                else
                {
                    var button=root.GetComponentsInChildren<Button>().FirstOrDefault(target=>target.name==r.name && target.IsActive());
                    if(button==null || (root.Page!=UiPage.Terminal && root.Page!=UiPage.SkillTree))
                        throw new InvalidOperationException("Active physical display control not found");
                    diver.LookAtConsole(ReticleTarget(button));
                }
                return;
            }
            if(r.action=="reticle_hover"){UiRoot.Instance.QaPointAtMonitor();return;}
            if(r.action=="reticle_click")
            {
                if(!UiRoot.Instance.QaClickReticle())throw new InvalidOperationException("No available control under the physical reticle");
                return;
            }
            if(r.action=="reticle_buy")
            {
                if(!UiRoot.Instance.QaBuyReticle())throw new InvalidOperationException("No skill under the reticle");
                return;
            }
            if(r.action=="reticle_scroll"){UiRoot.Instance.QaScrollReticle(r.number);return;}
            if(r.action=="aim_scope"){if(r.value=="end")EndAimTest();else BeginAimTest(r.number>0?r.number:5);return;}
            if(r.action=="aim_button"){QueueAimButton(r.value!="release",r.number>0?r.number:.35f);return;}
            if (r.action == "slot" || r.action == "fish_reel" || r.action == "fish_grip")
            {
                var tool = UiRoot.Instance.Context.Harpoon;
                bool accepted = tool != null && (r.action == "slot" ? tool.SelectSlot(Mathf.RoundToInt(r.number))
                    : tool.QaHoldOwned(r.action == "fish_grip", r.number));
                if (!accepted) throw new InvalidOperationException("Owned fishing controls are currently unavailable");
                if (r.action == "fish_reel") tool.Motor.QaSwimToward(r.point, r.value == "follow" ? 1 : 0);
                return;
            }
            if(r.action=="drop_selected" || r.action=="pickup_drop")
            {
                var tool=UiRoot.Instance.Context.Harpoon;
                bool dispatched=tool!=null && (r.action=="drop_selected" ? tool.RequestDropSelected()
                    : long.TryParse(r.value,out var id) && tool.RequestPickup(id));
                if(!dispatched)throw new InvalidOperationException("Item action unavailable.");
                return;
            }
            if(r.action=="rejoin_local")
            {
                var session=UiRoot.Instance.Context.Session;
                if(session==null || session.SteamMode || !session.Connected || session.manager.IsServerStarted)
                    throw new InvalidOperationException("A connected non-host LAN peer is required.");
                StartCoroutine(RejoinLocal(session));
                return;
            }
            if(r.action=="fire"||r.action=="harpoon_release")
            {
                var tool=UiRoot.Instance.Context.Harpoon;
                if(tool==null||!(r.action=="fire"?tool.QaFireOwned():tool.QaReleaseOwned()))
                    throw new InvalidOperationException("Owned harpoon action blocked by connection, UI or current cargo/tether state");
                return; // This acknowledges dispatch only. harpoonShots changes only after the server accepts a shot.
            }
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
            if(r.action=="ball_press" || r.action=="ball_release")
            {
                UiRoot.Instance.HandleBallPointer(r.action=="ball_press",r.action=="ball_release");
                return;
            }
            if(r.action=="gosha_pet" || r.action=="gosha_pickup")
            {
                var player=UiRoot.Instance.Context.Player;var world=RaidWorld.Instance;
                if(player==null || !player.IsOwner || world==null)throw new InvalidOperationException("Owned player required");
                world.RequestGosha(r.action=="gosha_pet"?GoshaAction.Pet:GoshaAction.PickUp);
                return;
            }
            if(r.action=="record")
            {
                if(r.value=="stop")FishingRecorder.Stop();
                else FishingRecorder.Begin(Path.Combine(directory,string.IsNullOrEmpty(r.name)?"clip":r.name),r.number>0?r.number:20,r.number2>0?(int)r.number2:20);
                return;
            }
            if(r.action=="capture")
            {
                string leaf=Path.GetFileName(string.IsNullOrEmpty(r.value)?r.name:r.value);
                if(string.IsNullOrEmpty(leaf)||!leaf.EndsWith(".png",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Capture requires a PNG filename");
                string path=Path.Combine(directory,leaf);
                if(Application.isBatchMode)CapturePeerCamera(path);
                else ScreenCapture.CaptureScreenshot(path);
                return;
            }
            if(r.action=="key")
            {
                Key key;if(!Enum.TryParse(r.value,true,out key))throw new ArgumentException("Unknown key");
                if (key==Key.F) UiRoot.Instance.Context.Rescue?.QaPressF(r.number);
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
        static System.Collections.IEnumerator RejoinLocal(RaidSession session)
        {
            session.Disconnect();
            float until=Time.unscaledTime+8;
            while(!session.manager.IsOffline && Time.unscaledTime<until)yield return null;
            if(session.manager.IsOffline)session.JoinLocal();
        }

        // Batch players have no presented swap-chain image. Render the owned diver's actual camera stack on the GPU.
        internal static Vector3 ReticleTarget(Button button)
        {
            var rect=(RectTransform)button.transform;
            Vector2 location=rect.rect.center;
            if(button is SkillSectorButton && int.TryParse(button.name.Substring("Sector".Length),out int sector))
                location+=SkillBoardMesh.At(sector*45)*216;
            return rect.TransformPoint(location);
        }

        public static void CapturePeerCamera(string path)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)
                throw new InvalidOperationException("A graphics device is required for a peer camera capture");
            var diver=UiRoot.Instance.Context.Player;
            var camera=diver!=null && diver.IsOwner?diver.GetComponentInChildren<Camera>():null;
            if(camera==null || !camera.isActiveAndEnabled)
                throw new InvalidOperationException("An active camera on the local owned diver is required");

            var overlays=UnityEngine.Object.FindObjectsByType<Canvas>()
                .Where(c=>c.isActiveAndEnabled && c.isRootCanvas && c.renderMode==RenderMode.ScreenSpaceOverlay)
                .Select(c=>(canvas:c,previousCamera:c.worldCamera,distance:c.planeDistance,rotation:c.transform.localRotation,
                    scale:c.transform.localScale,position:c.transform.localPosition)).ToArray();
            int width=Screen.width>1?Screen.width:1280, height=Screen.height>1?Screen.height:720;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previousTarget=camera.targetTexture;
            var previousActive=RenderTexture.active;
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=target;
                foreach(var overlay in overlays)
                {
                    overlay.canvas.renderMode=RenderMode.ScreenSpaceCamera;
                    overlay.canvas.worldCamera=camera;
                    overlay.canvas.planeDistance=camera.nearClipPlane+.02f;
                }
                Canvas.ForceUpdateCanvases();
                var frame=new RenderPipeline.StandardRequest{destination=target};
                if(!RenderPipeline.SupportsRenderRequest(camera,frame))
                    throw new InvalidOperationException("The active render pipeline does not support camera-stack capture");
                RenderPipeline.SubmitRenderRequest(camera,frame);
                RenderTexture.active=target;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0,false);
                pixels.Apply(false,false);
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previousTarget;
                RenderTexture.active=previousActive;
                foreach(var overlay in overlays)
                {
                    var canvas=overlay.canvas;
                    if(canvas==null)continue;
                    canvas.planeDistance=overlay.distance;
                    canvas.worldCamera=overlay.previousCamera;
                    canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                    canvas.transform.SetLocalPositionAndRotation(overlay.position,overlay.rotation);
                    canvas.transform.localScale=overlay.scale;
                }
                Canvas.ForceUpdateCanvases();
                if(pixels!=null)Destroy(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        void WriteSnapshot()
        {
            var root=UiRoot.Instance;var context=root.Context;var player=context.Player;var world=context.World;var fish=context.Harpoon==null?null:context.Harpoon.HookedFish;
            var snapshot=new Snapshot{request=lastId,page=root.Page.ToString(),error=error,cursor=Cursor.lockState.ToString(),connected=context.Connected,pending=root.CommandPending,inputBlocked=UiInputGate.Blocked,
                width=Screen.width,height=Screen.height,players=context.Players.Count,localProgress=world?.LocalProgress,
                skillTreeNodes=root.Page==UiPage.SkillTree?root.GetComponentsInChildren<SkillPurchaseButton>().Length:0,depthTier=world==null?0:world.CurrentDepth,worldMessage=world==null?"":world.Message,travel=world==null?0:world.TravelRemaining,
                scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,events=UnityEngine.Object.FindObjectsByType<EventSystem>().Count(e=>e.isActiveAndEnabled),
                canvases=UnityEngine.Object.FindObjectsByType<Canvas>().Count(c=>c.isActiveAndEnabled&&c.isRootCanvas),audioSources=UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include).Length,audioListeners=UnityEngine.Object.FindObjectsByType<AudioListener>().Count(a=>a.isActiveAndEnabled),
                uiScale=root.Settings.scale,fov=root.Settings.fov,sensitivity=root.Settings.sensitivity,lastSuccess=root.LastCommandSucceeded,
                aimBlend=Nereid.NereidAim.Blend,aiming=Nereid.NereidAim.Active!=null&&Nereid.NereidAim.Active.IsAiming,aimQaActive=Nereid.NereidAim.Active!=null&&Nereid.NereidAim.Active.BackgroundQaActive,applicationFocused=Application.isFocused,
                team=context.View==null?"":JsonUtility.ToJson(context.View),hookedFish=fish==null?"":fish.SpeciesId,hookMode=fish==null?"":fish.Mode.ToString(),tension=fish==null?0:fish.Tension,stamina=fish==null?0:fish.Stamina,
                fishBehaviour=fish==null?"":fish.Behaviour.ToString(),fishHookSequence=fish==null?0:fish.HookSequence,fishTugSequence=fish==null?0:fish.TugSequence,fishTugStrength=fish==null?0:fish.TugStrength,
                anglerStage=ThreatDirector.Instance==null?"":ThreatDirector.Instance.AnglerStage.ToString(),biteSequence=ThreatDirector.Instance==null?0:ThreatDirector.Instance.BiteSequence,biteVictim=ThreatDirector.Instance==null?-1:ThreatDirector.Instance.BiteVictim,
                anglerDistance=ThreatDirector.Instance==null||player==null?-1:Vector3.Distance(player.transform.position,ThreatDirector.Instance.AnglerPosition),
                rescueProgress=context.Rescue==null?0:context.Rescue.DisplayProgress,stockLoss=world==null?0:world.StockLossCount,wave=context.Threats!=null&&context.Threats.WaveActive};
            if(context.Harpoon!=null)
            {
                var tool=context.Harpoon;snapshot.tetherActive=tool.TetherActive;snapshot.isPulled=tool.IsPulled;
                snapshot.tetherOwner=tool.TetherOwner==null?-1:tool.TetherOwner.OwnerId;snapshot.tetherTarget=tool.TetherTarget==null?-1:tool.TetherTarget.OwnerId;
                snapshot.tetherRefusal=tool.TetherOptOutRemaining;
                snapshot.harpoonInstance = tool.HarpoonId.ToString();
                snapshot.selectedItem = tool.SelectedItem(out long instance).ToString();
                snapshot.selectedInstance = instance.ToString();
                snapshot.activeSlot = tool.ActiveSlot;
                snapshot.handsFull = tool.HandsFull;
                snapshot.heldSpecies = tool.CarriedFish == null ? "" : tool.CarriedFish.SpeciesId;
                snapshot.pockets = Enumerable.Range(2,tool.PocketCapacity).Select(i => tool.HasBallInSlot(i) ? "ball" : tool.FishInSlot(i)?.SpeciesId ?? "").ToArray();
                snapshot.helperOwner = tool.AssistingDiver == null ? -1 : tool.AssistingDiver.OwnerId;
                snapshot.assistedOwner = tool.AssistTarget == null ? -1 : tool.AssistTarget.OwnerId;
            }
            if(world!=null)
            {
                foreach(var station in ShipConsole.Stations)
                {
                    var button=station.GetComponent<Nereid.NereidDispatchButton>();
                    if(!button) continue;
                    if(station.kind==ShipConsoleKind.Process) snapshot.processingPresses=button.PressCount;
                    if(station.kind==ShipConsoleKind.Feed) snapshot.feedingPresses=button.PressCount;
                }
                snapshot.droppedItems=Enumerable.Range(0,world.DroppedCount).Select(i=>{
                    var item=world.DroppedAt(i);
                    return new DropReadout {instance=item.id.ToString(),kind=item.kind.ToString(),species=item.Fish?.SpeciesId,position=item.position};
                }).ToArray();
                snapshot.fishRoute=world.FishRoute;snapshot.gosha=world.Gosha;
                snapshot.goshaModelVisible=world.QaGoshaVisible;
                var chamber=world.Airlock;snapshot.airlockPhase=chamber.Phase.ToString();snapshot.airlockWater=chamber.Water;snapshot.innerDoor=chamber.Inner;snapshot.outerDoor=chamber.Outer;
                if(player!=null)
                {
                    snapshot.health=player.Health;snapshot.hypoxia=player.HypoxiaSeverity;snapshot.hypoxic=player.IsHypoxic;
                    snapshot.dead=player.IsDead;snapshot.consumed=player.IsConsumed;snapshot.canReturn=player.CanRespawn;
                    snapshot.watchedOwner=player.Spectating==null?-1:player.Spectating.OwnerId;
                }
                var shoal = world.container == null ? null : world.container.GetComponentInChildren<WetCargoShoal>();
                snapshot.cageSwimmers = shoal == null ? 0 : shoal.Count;
            }
            var mouth=UnityEngine.Object.FindAnyObjectByType<Nereid.NereidFeedingReaction>();
            if(mouth!=null){snapshot.observedBurps=mouth.BurpCount;snapshot.observedSwallows=mouth.SwallowedFishObserved;}
            // Report the latest attached bolt visible to this observer, including crew weapons.
            var measured=UnityEngine.Object.FindObjectsByType<HarpoonShotFx>()
                .Where(fx=>fx.VerifiedFrames>0 && fx.ShotAge<90)
                .OrderByDescending(fx=>fx.Phase==HarpoonShotFx.CablePhase.Attached)
                .ThenBy(fx=>fx.ShotAge).FirstOrDefault();
            snapshot.activeBoltOwners=FindObjectsByType<HarpoonShotFx>().Where(fx=>
                fx.HasConfirmedAttachment && fx.Phase==HarpoonShotFx.CablePhase.Attached)
                .Select(fx=>fx.VerifiedOwner).Distinct().ToArray();
            if(measured!=null)
            {
                snapshot.anchorOwner=measured.VerifiedOwner;snapshot.anchorShot=measured.VerifiedShot;
                snapshot.anchorFrames=measured.VerifiedFrames;snapshot.anchorTravel=measured.AnchorTravel;
                snapshot.anchorTipError=measured.MaxTipError;snapshot.anchorKnotError=measured.MaxKnotError;
            }
            if(observedFx!=null)
            {
                var fx=observedFx;var tip=fx.VisualTip;var end=fx.ServerShotEndpoint;
                snapshot.harpoonPhase=fx.Phase.ToString();snapshot.harpoonShots=fx.AcceptedShotCount;snapshot.harpoonAttached=fx.HasConfirmedAttachment;
                snapshot.harpoonSawFlight=sawFlight;snapshot.harpoonSawAttach=sawAttach;snapshot.harpoonSawRetract=sawRetract;
                snapshot.harpoonShotAge=fx.AcceptedShotCount>0?fx.ShotAge:-1;snapshot.harpoonSpool=fx.SpoolDegrees;
                snapshot.harpoonTipX=tip.x;snapshot.harpoonTipY=tip.y;snapshot.harpoonTipZ=tip.z;snapshot.harpoonEndX=end.x;snapshot.harpoonEndY=end.y;snapshot.harpoonEndZ=end.z;
            }
            if(player!=null){snapshot.owner=player.OwnerId;snapshot.x=player.transform.position.x;snapshot.y=player.transform.position.y;snapshot.z=player.transform.position.z;snapshot.oxygen=player.Oxygen;snapshot.stress=player.PressureStress;snapshot.depth=player.Depth;snapshot.lookMultiplier=player.LocalLookMultiplier;}
            var camera=player==null?null:player.GetComponentInChildren<Camera>();if(camera!=null){snapshot.actualFov=camera.fieldOfView;snapshot.aimYaw=camera.transform.eulerAngles.y;snapshot.aimPitch=Mathf.DeltaAngle(0,camera.transform.eulerAngles.x);}
            snapshot.reticleControl=root.ReticleControl;snapshot.pointerReserved=UiInputGate.PointerReserved;
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
