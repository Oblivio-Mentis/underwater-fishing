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
            Phase("Route two selected catches; leave the other two in the aquarium");
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
            Require(ui.Page==UiPage.None && ui.QaClickReticle() && ui.Page==UiPage.Terminal && UiInputGate.MonitorLook,
                "The aquarium monitor did not open through the common physical reticle.");
            yield return Pause(.4f);
            host.Motor.LookAtConsole(aim.position);
            yield return Pause(.35f);
            yield return Shot("reservoir-select.png");
            Require(world.Team.tank.Count(x=>catches.Contains(x.id))==4 && !world.FishRoute.active,
                "A fish was routed without choosing a destination.");
            var food=world.Team.tank.Find(item=>item.id==catches[0]);
            int foodValue=world.Team.FoodValue(food,world.progression);
            ui.Send(WorldAction.Process,big.id);
            yield return Wait(()=>!ui.CommandPending && big.destination==CatchDestination.Process,5,"Processor draft was not confirmed.");
            var obsoleteQuote=FishRouteQuote.ComputeDraft(world.Team.tank);
            ui.Send(WorldAction.Feed,food.id);
            yield return Wait(()=>!ui.CommandPending && food.destination==CatchDestination.Feed,5,"Feeding draft was not confirmed.");
            yield return Pause(1f);
            Require(!world.FishRoute.active && catches.All(id=>world.Team.tank.Exists(item=>item.id==id)) &&
                world.Team.Treasury.Equals(beforeTreasury) && world.Team.offeringPoints==offered,
                "Choosing destinations moved fish or awarded resources before dispatch.");

            ui.Send(WorldAction.SendFish,obsoleteQuote);
            yield return Wait(()=>!ui.CommandPending,5,"Stale send quote was not acknowledged.");
            Require(!ui.LastCommandSucceeded && ui.LastCommandReason=="changed" && !world.FishRoute.active,"Stale batch was accepted.");
            Button send=null;
            yield return Wait(()=>
            {
                send=ui.GetComponentsInChildren<Button>().FirstOrDefault(button=>button.name=="SendFish");
                return send!=null && send.interactable;
            },5,"No enabled send button on the aquarium monitor.");
            host.Motor.LookAtConsole(send.transform.TransformPoint(((RectTransform)send.transform).rect.center));
            yield return Pause(.4f);
            Require(ui.QaClickReticle(),"The physical send button could not be clicked.");
            yield return Wait(()=>world.Team.tank.Where(item=>item.id==food.id || item.id==big.id).All(item=>item.dispatchCommitted) &&
                world.FishRoute.active,5,"The two drafts were not committed together.");
            ui.ClosePanel();
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==big.id),30,"Selected fish never reached the processor.");
            yield return Wait(()=>ui.ProcessingReceiptVisible && ui.Page==UiPage.None,3,"The confirmed processing receipt did not appear on the HUD.");
            yield return Pause(.65f);
            yield return Shot("processing-receipt.png");
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==food.id),30,"Selected fish never reached the creature.");
            var credited=world.Team.Treasury.Subtract(beforeTreasury);
            Require(credited.Equals(new MaterialCost(big.biomass,big.chitin,big.scrap,big.electronics)) &&
                world.Team.offeringPoints==offered+foodValue && catches.Skip(1).Take(2).All(id=>world.Team.tank.Exists(item=>item.id==id)),
                "The confirmed batch failed to credit the exact yield or consumed an unselected fish.");
            Check($"Selected heavy #{big.id}: {credited.biomass} B, {credited.chitin} C in the shared wallet. Feed #{food.id}: +{foodValue} hundredths. The other two fish stayed in the aquarium.");
        }
    }
}
#endif
