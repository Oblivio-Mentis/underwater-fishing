#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Raid.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        [Serializable] sealed class CheatUiReport
        {
            public string status="running",phase,error,resource,fish;
            public int granted,stocked,inputSystemEdits,focusedTmpEdits;
            public float focusedTravel;
            public float baselineMetresPerSecond,fastMetresPerSecond,restoredMetresPerSecond,blockedTravel,throughWallTravel,restoredBlockedTravel;
            public float embeddedDepth,observedClearOffset,restoredClearOffset,reverseTravel;
            public List<string> checks=new();
            public void Check(bool ok,string description)
            {
                if(!ok)throw new InvalidOperationException(description);
                checks.Add(description);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void BeginCheatUiTrial()
        {
            string[] args=Environment.GetCommandLineArgs();
            int flag=Array.IndexOf(args,"-raid-cheat-ui");
            if(flag<0 || flag+1>=args.Length)return;
            string folder=Path.GetFullPath(args[flag+1]);
            Directory.CreateDirectory(folder);
            var report=new CheatUiReport();
            try
            {
                int save=Array.IndexOf(args,"-raid-save"),profile=Array.IndexOf(args,"-raid-profile");
                if(!args.Contains("-raid-host") || save<0 || save+1>=args.Length || profile<0 || profile+1>=args.Length ||
                   !Guid.TryParse(args[profile+1],out _) || !string.Equals(Path.GetFullPath(args[save+1]),
                   Path.Combine(folder,"cheat-ui-campaign.json"),StringComparison.OrdinalIgnoreCase) || File.Exists(args[save+1]))
                    throw new InvalidOperationException("Cheat UI trial requires a new cheat-ui-campaign.json in its output folder, a GUID fixture profile and a local host.");
                await AwaitCheatUi(()=>Instance && Instance.team!=null && Instance.localDiver && Instance.LocalProgress!=null && Instance.NetworkObject && Instance.IsServerInitialized &&
                    UiRoot.Instance && UiRoot.Instance.Context.Connected && UiRoot.Instance.Page==UiPage.None &&
                    Instance.transitionRemaining<=0 && !UiInputGate.Blocked,"host, private profile and unblocked player input",85);
                await Instance.RunCheatUiTrial(report,folder);
                report.status="passed";
            }
            catch(Exception ex){report.status="failed";report.error=ex.ToString();}
            File.WriteAllText(Path.Combine(folder,"cheat-ui-report.json"),JsonUtility.ToJson(report,true));
            UnityEngine.Debug.Log("[CheatUiTrial] "+report.status+" "+report.phase+" "+report.error);
            if(!Application.isEditor)Application.Quit(report.status=="passed"?0:1);
        }

        static async Awaitable AwaitCheatUi(Func<bool> ready,string purpose,float timeout=7)
        {
            float deadline=Time.realtimeSinceStartup+timeout;
            while(!ready())
            {
                if(Time.realtimeSinceStartup>deadline)
                    throw new TimeoutException(purpose+" | focus="+Application.isFocused+" cursor="+Cursor.lockState+
                        " menu="+UiRoot.Instance?.Page+" blocked="+UiInputGate.Blocked+" pointer="+CheatPanel.PointerActive);
                await Awaitable.NextFrameAsync();
            }
        }

        static Keyboard TrialKeyboard => Keyboard.current??InputSystem.AddDevice<Keyboard>();
        static async Awaitable TapCheatKey(Key key)
        {
            InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState(key));
            await Awaitable.WaitForSecondsAsync(.11f);
            InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState());
            await Awaitable.WaitForSecondsAsync(.13f);
        }

        static string CheatText(string name)
        {
            var obj=GameObject.Find(name);
            if(!obj)throw new InvalidOperationException("Missing panel label "+name);
            var tmp=obj.GetComponent<TMP_Text>()??obj.GetComponentInChildren<TMP_Text>();
            if(tmp)return tmp.text;
            var text=obj.GetComponent<Text>()??obj.GetComponentInChildren<Text>();
            if(text)return text.text;
            throw new InvalidOperationException("Missing text on "+name);
        }

        static async Awaitable ClickCheatControl(string name)
        {
            Canvas.ForceUpdateCanvases();
            var obj=GameObject.Find(name);
            if(!obj || !obj.TryGetComponent<Selectable>(out var button) || !button.IsInteractable())
                throw new InvalidOperationException("Unavailable mouse control "+name);
            var rect=(RectTransform)obj.transform;
            var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
            var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var events=EventSystem.current;
            var raycasts=new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events){position=point},raycasts);
            if(raycasts.Count==0 || raycasts[0].gameObject.GetComponentInParent<Selectable>()!=button)
                throw new InvalidOperationException(name+" is obscured at "+point+" hit="+(raycasts.Count>0?raycasts[0].gameObject.name:"nothing"));
            var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            await Awaitable.WaitForSecondsAsync(.08f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});
            await Awaitable.WaitForSecondsAsync(.10f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            await Awaitable.WaitForSecondsAsync(.13f);
        }

        static bool CheatFieldFocused(TMP_InputField field) =>
            field && field.isFocused && EventSystem.current && EventSystem.current.currentSelectedGameObject==field.gameObject;

        static async Awaitable EditCheatField(CheatUiReport report,TMP_InputField field,Key key,KeyCode keyCode,
            char character,Func<bool> expected,bool control=false)
        {
            if(!CheatFieldFocused(field))throw new InvalidOperationException("Quantity lost pointer focus before "+key);
            bool already=expected();
            try
            {
                InputSystem.QueueStateEvent(TrialKeyboard,control?new KeyboardState(Key.LeftCtrl,key):new KeyboardState(key));
                if(character!='\0')InputSystem.QueueTextEvent(TrialKeyboard,character);
                await Awaitable.WaitForSecondsAsync(.10f);
            }
            finally{InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState());}
            await Awaitable.WaitForSecondsAsync(.10f);
            if(!expected())
            {
                // Batch players can process movement without receiving a desktop text event.
                if(!CheatFieldFocused(field))throw new InvalidOperationException("Lost keyboard focus on "+key);
                field.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=keyCode,character=character,
                    modifiers=control?EventModifiers.Control:EventModifiers.None});
                await AwaitCheatUi(expected,"TMP handles "+keyCode+" (current amount '"+field.text+"')",2);
                report.focusedTmpEdits++;
            }
            else if(!already)report.inputSystemEdits++;
            if(!CheatFieldFocused(field))throw new InvalidOperationException("Quantity lost keyboard focus on "+key);
        }

        static Awaitable SelectCheatAmount(CheatUiReport report,TMP_InputField field) =>
            EditCheatField(report,field,Key.A,KeyCode.A,'\0',
                ()=>Math.Abs(field.selectionAnchorPosition-field.selectionFocusPosition)==field.text.Length,true);

        int[] CheatWallet() => new[]{team.biomass,team.chitin,team.scrap,team.electronics};

        async Awaitable RunCheatUiTrial(CheatUiReport report,string folder)
        {
            var diver=localDiver;
            report.phase="open panel and keyboard isolation";
            report.Check(team.tank.Count==0 && CheatWallet().All(x=>x==0),"Isolated campaign begins with an empty aquarium and treasury.");
            if(threats)threats.enabled=false;
            team.goshaMet=team.goshaTamed=true;
            Vector3 origin=default,forward=default;
            bool found=false;
            foreach(float rise in new[]{2.5f,6f,10f})
            {
                foreach(var axis in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
                {
                    var point=CargoLoadingPosition+axis*17+Vector3.up*rise;
                    var middle=point+Vector3.up*.6f;
                    if(Physics.CheckSphere(middle,.85f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                        Physics.SphereCast(middle,.65f,axis,out _,24,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                    diver.Teleport(point);
                    if(diver.IsInside)continue;
                    origin=point;forward=axis;found=true;break;
                }
                if(found)break;
            }
            report.Check(found,"Located a clear underwater movement lane in the running game.");
            await TapCheatKey(Key.X);
            await AwaitCheatUi(()=>GameObject.Find("CheatGrant") && CheatPanel.PointerActive,"X opens the cheat panel");
            report.Check(!UiInputGate.Blocked && UiRoot.Instance.Page==UiPage.None,"X opened the panel while the gameplay input gate remained open.");
            await ClickCheatControl("CheatResourceNext");
            var field=GameObject.Find("CheatAmountField")?.GetComponent<TMP_InputField>();
            report.Check(field && field.text=="1","The quantity is a TMP input field starting at 1.");
            string resource=CheatText("CheatResourceText"),fish=CheatText("CheatFishText");
            foreach(var key in new[]{Key.Digit6,Key.RightArrow,Key.DownArrow,Key.Enter,Key.NumpadEnter})await TapCheatKey(key);
            report.Check(!field.isFocused && field.text=="1" && CheatText("CheatResourceText")==resource && CheatText("CheatFishText")==fish &&
                CheatWallet().All(n=>n==0) && team.tank.Count==0,"With quantity unfocused, digits, arrows and Enter did not change the panel or dispatch a grant.");
            var tool=diver.GetComponent<HarpoonTool>();
            await TapCheatKey(Key.Digit2);
            await AwaitCheatUi(()=>tool.ActiveSlot==1,"number 2 selects the actual second equipment slot outside the amount field");
            await ClickCheatControl("CheatAmountField");
            await AwaitCheatUi(()=>CheatFieldFocused(field),"mouse focuses the quantity input");
            await SelectCheatAmount(report,field);
            await EditCheatField(report,field,Key.Digit1,KeyCode.Alpha1,'1',()=>field.text=="1" && field.selectionFocusPosition==field.selectionAnchorPosition);
            await EditCheatField(report,field,Key.Digit8,KeyCode.Alpha8,'8',()=>field.text=="18");
            await EditCheatField(report,field,Key.Backspace,KeyCode.Backspace,'\0',()=>field.text=="1");
            await EditCheatField(report,field,Key.Digit7,KeyCode.Alpha7,'7',()=>field.text=="17");
            report.Check(tool.ActiveSlot==1 && field.text=="17","Physical digit presses edited 18 to 17 with Backspace and kept the second equipment slot selected.");
            diver.LookAtConsole(origin+forward*20+Vector3.up*.65f);
            await Awaitable.WaitForSecondsAsync(.2f);
            var focusedStart=diver.transform.position;
            try
            {
                InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState(Key.W));
                InputSystem.QueueTextEvent(TrialKeyboard,'w');
                await Awaitable.WaitForSecondsAsync(.6f);
            }
            finally{InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState());}
            report.focusedTravel=Vector3.Dot(diver.transform.position-focusedStart,forward);
            report.Check(report.focusedTravel>.3f && CheatFieldFocused(field) && field.text=="17" && !UiInputGate.Blocked,
                "W moved the owned diver with the quantity field still focused and its integer value unchanged.");

            report.phase="mouse material and fish grants";
            var before=CheatWallet();
            await ClickCheatControl("CheatGrant");
            await AwaitCheatUi(()=>CheatWallet().Sum()-before.Sum()==17,"17 units credited");
            var diffs=CheatWallet().Zip(before,(a,b)=>a-b).ToArray();
            report.Check(diffs[1]==17 && diffs.Count(x=>x==0)==3,"A real mouse grant increased only the selected chitin by 17.");
            await ClickCheatControl("CheatResourceNext");
            await ClickCheatControl("CheatAmountField");
            await SelectCheatAmount(report,field);
            await EditCheatField(report,field,Key.Digit2,KeyCode.Alpha2,'2',()=>field.text=="2");
            await EditCheatField(report,field,Key.Digit3,KeyCode.Alpha3,'3',()=>field.text=="23");
            report.Check(field.text=="23" && tool.ActiveSlot==1,"Selection replacement entered 23 without selecting another equipment slot.");
            await ClickCheatControl("CheatGrant");
            await AwaitCheatUi(()=>team.scrap==23,"23 scrap credited");
            report.Check(team.chitin==17 && team.scrap==23 && team.biomass==0 && team.electronics==0,"A second mouse selection credited 23 scrap while preserving the other resources.");
            report.resource=resource+", "+CheatText("CheatResourceText");report.granted=40;
            await ClickCheatControl("CheatFishNext");
            string selectedFish=CheatText("CheatFishText");
            report.Check(fish!=selectedFish,"Mouse advanced to a different fish species.");
            await ClickCheatControl("CheatAmountField");
            await SelectCheatAmount(report,field);
            await EditCheatField(report,field,Key.Digit3,KeyCode.Alpha3,'3',()=>field.text=="3");
            await ClickCheatControl("CheatStock");
            await AwaitCheatUi(()=>team.tank.Count==3,"three fish appear in the aquarium");
            var ids=team.tank.Select(x=>x.id).ToArray();
            report.Check(ids.Distinct().Count()==3 && ids.All(id=>id>0 && team.deliveredIds.Contains(id)) && team.tank.All(x=>x.species==team.tank[0].species) && selectedFish==ReelReadout.Species(team.tank[0].species) &&
                CheatWallet().Sum()==40,"Mouse stocked three distinct catches of a single species without changing the materials.");
            report.fish=team.tank[0].species;report.stocked=3;
            UiQaProbe.CapturePeerCamera(Path.Combine(folder,"cheat-panel.png"));

            report.phase="movement and collision from real input";
            async Awaitable<float> Walk(float duration,bool fromStart=true)
            {
                if(fromStart)diver.Teleport(origin);
                diver.LookAtConsole(diver.transform.position+forward*22+Vector3.up*.65f);
                await Awaitable.WaitForSecondsAsync(.27f);
                var start=diver.transform.position;
                float startTime=Time.time;
                try
                {
                    InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState(Key.W));
                    await Awaitable.WaitForSecondsAsync(duration);
                    return Vector3.Dot(diver.transform.position-start,forward)/Mathf.Max(.001f,Time.time-startTime);
                }
                finally
                {
                    InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState());
                }
            }

            report.baselineMetresPerSecond=await Walk(.85f);
            report.Check(report.baselineMetresPerSecond>.5f && CheatPanel.PointerActive && !UiInputGate.Blocked,"W moved the owned diver with the panel open.");
            await ClickCheatControl("CheatFast");
            await AwaitCheatUi(()=>diver.CheatFast,"fast enabled");
            report.fastMetresPerSecond=await Walk(.85f);
            report.Check(report.fastMetresPerSecond>report.baselineMetresPerSecond*1.25f,"The fast switch measurably increased movement over the same clear lane.");
            await ClickCheatControl("CheatFast");
            await AwaitCheatUi(()=>!diver.CheatFast,"fast disabled");
            report.restoredMetresPerSecond=await Walk(.85f);
            report.Check(Mathf.Abs(report.restoredMetresPerSecond-report.baselineMetresPerSecond)<report.baselineMetresPerSecond*.4f,"Switching fast off restored the ordinary movement speed in the same lane.");
            var barrier=new GameObject("QA collision bulkhead");
            try
            {
                barrier.transform.SetPositionAndRotation(origin+forward*2.1f+Vector3.up*.6f,Quaternion.LookRotation(forward));
                barrier.AddComponent<BoxCollider>().size=new Vector3(8,8,.4f);
                Physics.SyncTransforms();
                float duration=Mathf.Clamp(5/report.baselineMetresPerSecond,1.1f,3.5f);
                report.blockedTravel=(await Walk(duration))*duration;
                report.Check(report.blockedTravel>.25f && report.blockedTravel<2.1f,"Normal movement stopped at the physical fixture bulkhead.");
                await ClickCheatControl("CheatNoclip");
                await AwaitCheatUi(()=>diver.CheatNoclip,"noclip enabled");
                await Walk(duration,false);
                report.throughWallTravel=Vector3.Dot(diver.transform.position-origin,forward);
                report.Check(report.throughWallTravel>2.65f,"With noclip on, the same W input crossed the far face of the actual collider.");
                await ClickCheatControl("CheatNoclip");
                await AwaitCheatUi(()=>!diver.CheatNoclip,"noclip disabled");
                report.restoredBlockedTravel=(await Walk(duration))*duration;
                report.Check(report.restoredBlockedTravel<2.1f && report.restoredBlockedTravel>.25f,"After noclip was turned off, the same bulkhead blocked the diver again.");

                report.phase="disable noclip from inside the solid wall";
                diver.Teleport(origin);
                var collider=barrier.GetComponent<BoxCollider>();
                collider.size=new Vector3(8,8,2.5f);
                Physics.SyncTransforms();
                var controller=diver.GetComponent<CharacterController>();
                float nearFace=2.1f-collider.size.z*.5f;
                float radius=controller.radius*diver.transform.lossyScale.x;
                await ClickCheatControl("CheatNoclip");
                await AwaitCheatUi(()=>diver.CheatNoclip && !controller.enabled,"noclip disabled the normal controller");
                diver.LookAtConsole(origin+forward*20+Vector3.up*.65f);
                await Awaitable.WaitForSecondsAsync(.22f);
                float deadline=Time.realtimeSinceStartup+4;
                try
                {
                    InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState(Key.W));
                    while(true)
                    {
                        float offset=Vector3.Dot(diver.transform.position-origin,forward);
                        if(offset+radius<nearFace)report.observedClearOffset=offset;
                        if(offset>=1.94f)break;
                        if(Time.realtimeSinceStartup>=deadline)throw new TimeoutException("Fly to the middle of the wall using W.");
                        await Awaitable.NextFrameAsync();
                    }
                }
                finally{InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState());}
                await Awaitable.WaitForSecondsAsync(.18f);
                var inside=diver.transform.position;
                report.embeddedDepth=Vector3.Dot(inside-origin,forward);
                report.Check(collider.bounds.Contains(inside+Vector3.up*.6f) && report.embeddedDepth>nearFace+radius &&
                    report.embeddedDepth<nearFace+collider.size.z-radius,"The diver actually stopped inside the thicker solid while noclip was active.");
                await ClickCheatControl("CheatNoclip");
                await AwaitCheatUi(()=>!diver.CheatNoclip && controller.enabled,"exit from inside the bulkhead");
                report.restoredClearOffset=Vector3.Dot(diver.transform.position-origin,forward);
                report.Check(report.restoredClearOffset+radius<=nearFace+controller.skinWidth+.04f &&
                    Mathf.Abs(report.restoredClearOffset-report.observedClearOffset)<.65f &&
                    Vector3.Distance(diver.transform.position,inside)>radius,"Disabling noclip inside the solid restored the controller at the last clear side of the same wall.");
                Vector3 escape=diver.transform.position;
                try
                {
                    InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState(Key.S));
                    await Awaitable.WaitForSecondsAsync(.55f);
                    report.reverseTravel=Vector3.Dot(escape-diver.transform.position,forward);
                }
                finally{InputSystem.QueueStateEvent(TrialKeyboard,new KeyboardState());}
                report.Check(report.reverseTravel>.3f && controller.enabled,"Normal reverse movement worked immediately after recovery from the wall.");
            }
            finally{Destroy(barrier);}
            await TapCheatKey(Key.X);
            await AwaitCheatUi(()=>!CheatPanel.PointerActive && !GameObject.Find("CheatClose"),"X closes panel");
            report.Check(!UiInputGate.Blocked,"X closed the panel and restored the normal input context.");
            await TapCheatKey(Key.X);
            await AwaitCheatUi(()=>GameObject.Find("CheatClose") && CheatPanel.PointerActive,"reopen panel");
            await ClickCheatControl("CheatClose");
            await AwaitCheatUi(()=>!CheatPanel.PointerActive,"mouse closes panel");
            report.Check(!diver.CheatFast && !diver.CheatNoclip,"The mouse Close button worked and both movement switches ended disabled.");
        }
    }
}
#endif
