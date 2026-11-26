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
            public int granted,stocked;
            public float baselineMetresPerSecond,fastMetresPerSecond,restoredMetresPerSecond,blockedTravel,throughWallTravel,restoredBlockedTravel;
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
                await AwaitCheatUi(()=>Instance && Instance.IsServerInitialized && Instance.localDiver && Instance.LocalProgress!=null &&
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

        static async Awaitable ClickCheatButton(string name)
        {
            Canvas.ForceUpdateCanvases();
            var obj=GameObject.Find(name);
            if(!obj || !obj.TryGetComponent<Button>(out var button) || !button.IsInteractable())
                throw new InvalidOperationException("Unavailable mouse button "+name);
            var rect=(RectTransform)obj.transform;
            var canvas=button.GetComponentInParent<Canvas>().rootCanvas;
            var point=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,
                rect.TransformPoint(rect.rect.center));
            var events=EventSystem.current;
            var raycasts=new List<RaycastResult>();
            events.RaycastAll(new PointerEventData(events){position=point},raycasts);
            if(raycasts.Count==0 || raycasts[0].gameObject.GetComponentInParent<Button>()!=button)
                throw new InvalidOperationException(name+" is obscured at "+point+" hit="+(raycasts.Count>0?raycasts[0].gameObject.name:"nothing"));
            var mouse=Mouse.current??InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            await Awaitable.WaitForSecondsAsync(.08f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point,buttons=1});
            await Awaitable.WaitForSecondsAsync(.10f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});
            await Awaitable.WaitForSecondsAsync(.13f);
        }

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
            await ClickCheatButton("CheatResourceNext");
            await ClickCheatButton("CheatClear");
            await ClickCheatButton("CheatDigit1");
            string amount=CheatText("CheatAmountValue"),resource=CheatText("CheatResourceText"),fish=CheatText("CheatFishText");
            foreach(var key in new[]{Key.Digit6,Key.RightArrow,Key.DownArrow,Key.Enter,Key.NumpadEnter})await TapCheatKey(key);
            report.Check(CheatText("CheatAmountValue")==amount && CheatText("CheatResourceText")==resource && CheatText("CheatFishText")==fish &&
                CheatWallet().All(n=>n==0) && team.tank.Count==0,"Digits, arrows and both Enter keys left the pointer-only controls unchanged.");
            await ClickCheatButton("CheatDigit7");
            report.Check(CheatText("CheatAmountValue").Contains("17"),"Mouse digit buttons composed 17.");

            report.phase="mouse material and fish grants";
            var before=CheatWallet();
            await ClickCheatButton("CheatGrant");
            await AwaitCheatUi(()=>CheatWallet().Sum()-before.Sum()==17,"17 units credited");
            var diffs=CheatWallet().Zip(before,(a,b)=>a-b).ToArray();
            report.Check(diffs[1]==17 && diffs.Count(x=>x==0)==3,"A real mouse grant increased only the selected chitin by 17.");
            await ClickCheatButton("CheatResourceNext");
            await ClickCheatButton("CheatClear");
            await ClickCheatButton("CheatDigit2");await ClickCheatButton("CheatDigit3");
            await ClickCheatButton("CheatGrant");
            await AwaitCheatUi(()=>team.scrap==23,"23 scrap credited");
            report.Check(team.chitin==17 && team.scrap==23 && team.biomass==0 && team.electronics==0,"A second mouse selection credited 23 scrap while preserving the other resources.");
            report.resource=resource+", "+CheatText("CheatResourceText");report.granted=40;
            await ClickCheatButton("CheatFishNext");
            string selectedFish=CheatText("CheatFishText");
            report.Check(fish!=selectedFish,"Mouse advanced to a different fish species.");
            await ClickCheatButton("CheatClear");
            await ClickCheatButton("CheatDigit3");
            await ClickCheatButton("CheatStock");
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
            await ClickCheatButton("CheatFast");
            await AwaitCheatUi(()=>diver.CheatFast,"fast enabled");
            report.fastMetresPerSecond=await Walk(.85f);
            report.Check(report.fastMetresPerSecond>report.baselineMetresPerSecond*1.25f,"The fast switch measurably increased movement over the same clear lane.");
            await ClickCheatButton("CheatFast");
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
                await ClickCheatButton("CheatNoclip");
                await AwaitCheatUi(()=>diver.CheatNoclip,"noclip enabled");
                await Walk(duration,false);
                report.throughWallTravel=Vector3.Dot(diver.transform.position-origin,forward);
                report.Check(report.throughWallTravel>2.65f,"With noclip on, the same W input crossed the far face of the actual collider.");
                await ClickCheatButton("CheatNoclip");
                await AwaitCheatUi(()=>!diver.CheatNoclip,"noclip disabled");
                report.restoredBlockedTravel=(await Walk(duration))*duration;
                report.Check(report.restoredBlockedTravel<2.1f && report.restoredBlockedTravel>.25f,"After noclip was turned off, the same bulkhead blocked the diver again.");
            }
            finally{Destroy(barrier);}
            await TapCheatKey(Key.X);
            await AwaitCheatUi(()=>!CheatPanel.PointerActive && !GameObject.Find("CheatClose"),"X closes panel");
            report.Check(!UiInputGate.Blocked,"X closed the panel and restored the normal input context.");
            await TapCheatKey(Key.X);
            await AwaitCheatUi(()=>GameObject.Find("CheatClose") && CheatPanel.PointerActive,"reopen panel");
            await ClickCheatButton("CheatClose");
            await AwaitCheatUi(()=>!CheatPanel.PointerActive,"mouse closes panel");
            report.Check(!diver.CheatFast && !diver.CheatNoclip,"The mouse Close button worked and both movement switches ended disabled.");
        }
    }
}
#endif
