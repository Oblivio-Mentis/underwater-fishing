#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Raid.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Raid
{
    public sealed partial class FishingTrial
    {
        IEnumerator ChooseReservoirRoutes(long[] catches)
        {
            var ui=UiRoot.Instance;
            Phase("Stage fish through the real monitor and use the physical processor");
            var first=world.Team.Player(world.ProgressKey(host.Motor));
            var second=world.Team.Player(world.ProgressKey(helper.Motor));
            Require(first!=null && second!=null && first!=second,"Two distinct host-owned profiles are required.");
            var beforeTreasury=world.Team.Treasury;
            var big=world.Team.tank.First(item=>item.id==catches[3]);
            int offered=world.Team.offeringPoints;
            var console=ShipConsole.Stations.First(s=>s.kind==ShipConsoleKind.Reservoir);
            var aim=console.screenAnchor;
            host.Motor.Teleport(aim.position-aim.forward*1.65f-Vector3.up*.60f);
            host.Motor.LookAtConsole(aim.position);
            yield return Wait(()=>ShipConsole.CanUse(host.Motor,WorldAction.Process) &&
                ShipConsole.ResolveSight(host.Motor,host.Motor.GetComponentInChildren<Camera>())==console,6,
                "Owned camera could not acquire the physical aquarium computer.");
            Require(ui.QaPointAtMonitor() && ui.Page==UiPage.Terminal && UiInputGate.MonitorLook,
                "The aquarium display did not become interactive on sight.");
            Require(Mathf.Abs(host.Motor.GetComponentInChildren<Camera>().fieldOfView-ui.Settings.fov)<.3f,
                "The monitor changed the gameplay field of view.");
            yield return Pause(.4f);
            host.Motor.LookAtConsole(aim.position);
            yield return Pause(.35f);
            yield return Shot("reservoir-select.png");
            Require(world.Team.tank.Count(x=>catches.Contains(x.id))==4 && !world.FishRoute.active,
                "A fish was routed without choosing a destination.");
            var food=world.Team.tank.Find(item=>item.id==catches[0]);
            int foodValue=world.Team.FoodValue(food,world.progression);
            yield return ClickReservoirControl("Process_"+big.id,0);
            yield return Wait(()=>!ui.CommandPending && big.destination==CatchDestination.Process,5,"Processor draft was not confirmed.");
            var obsoleteQuote=FishRouteQuote.ComputeDraft(world.Team.tank);
            yield return ClickReservoirControl("Feed_"+food.id,1);
            yield return Wait(()=>!ui.CommandPending && food.destination==CatchDestination.Feed,5,"Feeding draft was not confirmed.");
            yield return Pause(1f);
            Require(!world.FishRoute.active && catches.All(id=>world.Team.tank.Exists(item=>item.id==id)) &&
                world.Team.Treasury.Equals(beforeTreasury) && world.Team.offeringPoints==offered,
                "Choosing destinations moved fish or awarded resources before dispatch.");

            ui.Send(WorldAction.SendFish,obsoleteQuote);
            yield return Wait(()=>!ui.CommandPending,5,"Stale send quote was not acknowledged.");
            Require(!ui.LastCommandSucceeded && ui.LastCommandReason=="changed" && !world.FishRoute.active,"Stale batch was accepted.");
            var processConsole=ShipConsole.Stations.First(s=>s.kind==ShipConsoleKind.Process);
            var processFace=processConsole.screenAnchor;
            host.Motor.Teleport(processFace.position-processFace.forward*1.16f-Vector3.up*.60f);
            host.Motor.LookAtConsole(processFace.position);
            yield return Wait(()=>ShipConsole.ResolveSight(host.Motor,host.Motor.GetComponentInChildren<Camera>())==processConsole &&
                ShipConsole.CanUse(host.Motor,WorldAction.SendProcessingDraft),6,"Could not aim at the physical processor button.");
            Require(ui.QaClickReticle(),"The physical processor did not react to LMB.");
            yield return Wait(()=>big.dispatchCommitted && world.FishRoute.active && !world.FishRoute.feeding,
                5,"The processor asked for a new selection instead of using its existing draft.");
            Require(!food.dispatchCommitted && food.destination==CatchDestination.Feed,
                "The processor also dispatched the feeding draft.");
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==big.id),30,"Assigned fish never reached the processor.");
            yield return Wait(()=>ui.ProcessingReceiptVisible && ui.Page==UiPage.None,3,"The processing receipt did not appear on the HUD.");
            yield return Pause(.65f);
            yield return Shot("processing-receipt.png");
            Check("A selected fish left on LMB at the physical processor; the feeding draft remained in the tank.");

            host.Motor.Teleport(aim.position-aim.forward*1.65f-Vector3.up*.60f);
            host.Motor.LookAtConsole(aim.position);
            yield return Wait(()=>ui.QaPointAtMonitor() && ui.Page==UiPage.Terminal,5,"Could not return to the aquarium monitor.");
            var secondProcess=world.Team.tank.Find(item=>item.id==catches[1]);
            yield return ClickReservoirControl("Process_"+secondProcess.id,1);
            yield return Wait(()=>!ui.CommandPending && secondProcess.destination==CatchDestination.Process,5,"Second processing draft was not confirmed.");
            yield return ClickReservoirControl("SendFish");
            yield return Wait(()=>secondProcess.dispatchCommitted && food.dispatchCommitted && world.FishRoute.active,
                5,"The aquarium command did not commit both destinations.");
            ui.ClosePanel();
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==food.id || item.id==secondProcess.id),30,
                "Selected fish never reached their destinations.");
            var credited=world.Team.Treasury.Subtract(beforeTreasury);
            var combined=new MaterialCost(big.biomass+secondProcess.biomass,big.chitin+secondProcess.chitin,
                big.scrap+secondProcess.scrap,big.electronics+secondProcess.electronics);
            Require(credited.Equals(combined) && world.Team.offeringPoints==offered+foodValue &&
                world.Team.tank.Exists(item=>item.id==catches[2]),
                "The physical processor or mixed dispatch awarded the wrong amounts or touched an unassigned fish.");
            Check("Aquarium Send launches both draft routes once, and the unassigned fish remains in the tank.");
        }
        IEnumerator ClickReservoirControl(string name,float? listPosition=null)
        {
            var ui=UiRoot.Instance;
            if(listPosition.HasValue)
            {
                var list=ui.GetComponentInChildren<ScrollRect>();
                Require(list!=null,"Aquarium has no catch list.");
                list.StopMovement();
                list.verticalNormalizedPosition=listPosition.Value;
            }
            Button control=null;
            yield return Wait(()=>
            {
                control=ui.GetComponentsInChildren<Button>().FirstOrDefault(button=>button.name==name);
                return control!=null && control.interactable;
            },5,"No enabled aquarium control "+name);
            var rect=(RectTransform)control.transform;
            host.Motor.LookAtConsole(rect.TransformPoint(rect.rect.center));
            yield return Pause(.3f);
            Require(ui.QaClickReticle(),"The real reticle could not activate "+name);
        }
    }
}
#endif
