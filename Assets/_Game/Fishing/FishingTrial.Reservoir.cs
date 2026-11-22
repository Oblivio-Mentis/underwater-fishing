#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Raid.UI;
using UnityEngine;

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
            ui.UseConsole(console);
            yield return Pause(.4f);
            host.Motor.LookAtConsole(aim.position);
            yield return Pause(.35f);
            yield return Shot("reservoir-select.png");
            Require(world.Team.tank.Count(x=>catches.Contains(x.id))==4 && !world.FishRoute.active,
                "A fish was routed without choosing a destination.");
            ui.Send(WorldAction.Process,big.id);
            yield return Wait(()=>world.FishRoute.active && world.ActiveRouteId(0)==big.id,5,"Selected fish did not enter its own route.");
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==big.id),16,"Selected fish never reached the processor.");
            yield return Pause(.8f);
            yield return Shot("processing-receipt.png");
            var credited=world.Team.Treasury.Subtract(beforeTreasury);
            Require(credited.Equals(new MaterialCost(big.biomass,big.chitin,big.scrap,big.electronics)) &&
                world.Team.offeringPoints==offered,
                "Processor failed to credit the crew exactly once.");
            var food=world.Team.tank.Find(item=>item.id==catches[0]);
            int foodValue=world.Team.FoodValue(food,world.progression);
            var beforeFeeding=world.Team.Treasury;
            ui.Send(WorldAction.Feed,food.id);
            yield return Wait(()=>world.FishRoute.active && world.ActiveRouteId(0)==food.id && world.FishRoute.feeding,5,
                "The selected fish did not enter the feeding pipe.");
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==food.id),16,"Selected fish never reached the creature.");
            Require(world.Team.offeringPoints==offered+foodValue && world.Team.Treasury.Equals(beforeFeeding) && catches.Skip(1).Take(2).All(id=>world.Team.tank.Exists(item=>item.id==id)),
                "Feeding awarded material or touched unselected fish.");
            Check($"Selected heavy #{big.id}: {credited.biomass} B, {credited.chitin} C in the shared wallet. Feed #{food.id}: +{foodValue} hundredths. The other two fish stayed in the aquarium.");
        }
    }
}
#endif
