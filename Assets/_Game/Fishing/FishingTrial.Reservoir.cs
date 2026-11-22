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
            Phase("Route two selected catches; leave the other two in the aquarium");
            var first=world.Team.Player(world.ProgressKey(host.Motor));
            var second=world.Team.Player(world.ProgressKey(helper.Motor));
            Require(first!=null && second!=null && first!=second,"Two distinct host-owned profiles are required.");
            var beforeFirst=first.wallet;
            var beforeSecond=second.wallet;
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
            yield return Shot("reservoir-select.png");
            Require(world.Team.tank.Count(x=>catches.Contains(x.id))==4 && !world.FishRoute.active,
                "A fish was routed without choosing a destination.");
            ui.Send(WorldAction.Process,big.id);
            yield return Wait(()=>world.FishRoute.active && world.ActiveRouteId(0)==big.id,5,"Selected fish did not enter its own route.");
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==big.id),16,"Selected fish never reached the processor.");
            yield return Pause(.8f);
            yield return Shot("processing-receipt.png");
            var yieldA=first.wallet.Subtract(beforeFirst);
            var yieldB=second.wallet.Subtract(beforeSecond);
            Require(yieldA.Add(yieldB).Equals(new MaterialCost(big.biomass,big.chitin,big.scrap,big.electronics)) &&
                world.Team.Treasury.Equals(beforeTreasury) && world.Team.offeringPoints==offered &&
                first.wallet.biomass>0 && second.wallet.biomass>0,
                "Processor failed to credit personal shares exactly once.");
            var food=world.Team.tank.Find(item=>item.id==catches[0]);
            int foodValue=world.Team.FoodValue(food,world.progression);
            var preFeedA=first.wallet;var preFeedB=second.wallet;
            ui.Send(WorldAction.Feed,food.id);
            yield return Wait(()=>world.FishRoute.active && world.ActiveRouteId(0)==food.id && world.FishRoute.feeding,5,
                "The selected fish did not enter the feeding pipe.");
            yield return Wait(()=>!world.Team.tank.Exists(item=>item.id==food.id),16,"Selected fish never reached the creature.");
            Require(world.Team.offeringPoints==offered+foodValue && first.wallet.Equals(preFeedA) &&
                second.wallet.Equals(preFeedB) && catches.Skip(1).Take(2).All(id=>world.Team.tank.Exists(item=>item.id==id)),
                "Feeding awarded material or touched unselected fish.");
            Check($"Selected heavy #{big.id}: {yieldA.biomass}+{yieldB.biomass} B, {yieldA.chitin}+{yieldB.chitin} C. Feed #{food.id}: +{foodValue} hundredths. The other two fish stayed in the aquarium.");
        }
    }
}
#endif
