#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class FishingTrial
    {
        IEnumerator RunBallHarpoon()
        {
            yield return Wait(()=>world.Gosha.Active && world.Gosha.Ball==GoshaBallMode.Deck,10,"Missing physical ball.");
            var ship=NereidVessel.Active.transform;
            var spawn=ship.TransformPoint(new Vector3(-3,.17f,1.6f));
            world.StageBallTrial(spawn);
            host.Motor.Teleport(ship.TransformPoint(new Vector3(.5f,1,2)));
            helper.Motor.Teleport(ship.TransformPoint(new Vector3(-3,1,-1.6f)));
            yield return Pause(.55f);
            yield return AimAtBall();

            Phase("First-hit occlusion on deck");
            Vector3 eye=host.transform.position+Vector3.up*.6f;
            var partition=new GameObject("Temporary solid test partition");
            partition.transform.SetPositionAndRotation(Vector3.Lerp(eye,world.Gosha.BallPosition,.55f),
                Quaternion.LookRotation(world.Gosha.BallPosition-eye));
            partition.AddComponent<BoxCollider>().size=new Vector3(.9f,1,.2f);
            Physics.SyncTransforms();
            int shots=host.ShotSequence;
            Require(host.QaFireOwned(),"Wall test could not dispatch the normal owned shot.");
            yield return Wait(()=>host.ShotSequence>shots,2,"Server did not process wall shot.");
            Require(!host.HasBallLine && world.Gosha.Ball==GoshaBallMode.Deck,"Wall did not occlude the server ball collider.");
            Check("The solid test partition took the first raycast hit. Ball stayed loose.");
            partition.SetActive(false);
            Destroy(partition);

            yield return ShootBall();
            yield return Wait(()=>ReadClient()?.gosha.Ball==GoshaBallMode.Tethered &&
                ReadClient()?.activeBoltOwners?.Contains(host.OwnerId)==true,5,"Independent client did not render attached bolt.");
            yield return Shot("ball-hook-deck.png");
            Require(host.QaReleaseOwned(),"R could not release the ball line.");
            yield return Wait(()=>!host.HasBallLine && world.Gosha.Ball==GoshaBallMode.Deck,3,"R left stale ball ownership.");
            Check("Both clients observed the ball tether. R released it onto the deck.");

            yield return ShootBall();
            var target=world.Gosha.BallPosition;
            var peerEye=helper.transform.position+Vector3.up*.6f;
            var look=Quaternion.LookRotation(target-peerEye).eulerAngles;
            // Remotely rotate the OTHER owned diver through the existing local view command.
            var req=new UI.UiQaProbe.Request {id=Guid.NewGuid().ToString("N"),action="view",number=look.y,
                number2=Mathf.DeltaAngle(0,look.x)};
            var requestFile=System.IO.Path.Combine(clientDir,"request.json");
            var temp=requestFile+".new";
            System.IO.File.WriteAllText(temp,JsonUtility.ToJson(req));
            System.IO.File.Replace(temp,requestFile,null);
            yield return Wait(()=>ReadClient()?.request==req.id && Vector3.Angle(helper.Motor.AimDirection,target-peerEye)<2,4,"Second owner did not aim at ball.");
            Physics.SyncTransforms();
            Require(Physics.Raycast(peerEye,helper.Motor.AimDirection,out var raceHit,helper.tuning.maxRange,
                helper.tuning.hitMask,QueryTriggerInteraction.Ignore) && raceHit.collider.TryGetComponent<GoshaBallBody>(out _),
                "Competing owner did not have the physical sphere as their first hit.");
            int peerShots=helper.ShotSequence;
            Send("fire",0);
            yield return Wait(()=>helper.ShotSequence>peerShots,3,"No second server shot.");
            Require(host.HasBallLine && !helper.HasBallLine && world.Gosha.Holder==host.OwnerId,"Second shooter stole the reserved ball.");
            Check("A second owner's shot at the reserved physical ball did not reassign it.");
            yield return ReelBall();
            Check("The deck ball travelled down the line into the shooter's temporary hand slot.");

            Phase("Reel the single physical ball in open water");
            // The vessel has a twofold scale; use world coordinates in the open cargo pool.
            var water=new Vector3(24,-6,-27);
            host.Motor.Teleport(water);
            helper.Motor.Teleport(water+Vector3.right*3);
            // Stage only the starting loose object and diver poses. All shots and reeling use normal RPCs.
            world.StageBallTrial(water+Vector3.up*.55f+Vector3.forward*3.4f);
            yield return Pause(.3f);
            Require(!host.Motor.IsInside,"Water fixture was not submerged.");
            yield return ShootBall();
            yield return Shot("ball-hook-water.png");
            yield return ReelBall();
            yield return Pause(.65f);
            Require(world.HasGoshaBall(host.Motor) && host.CatchCount==0,"Water retrieval interfered with pockets or dropped the ball.");
            Check("Submerged owner retrieved the same loose ball, and retained it underwater without using a fish pocket.");

            Phase("Drop the held ball, then drop a loaded harpoon");
            long gunId=host.HarpoonId;
            Require(gunId>0 && host.RequestDropSelected(),"Q did not accept the selected hand-held ball.");
            yield return Wait(()=>world.Gosha.Ball==GoshaBallMode.Deck && ReadClient()?.gosha.Ball==GoshaBallMode.Deck,
                3,"Q did not release the held ball on both peers.");
            Require(host.HarpoonId==gunId && world.DroppedCount==0 && !host.HasBallLine,"Dropping the ball also removed the gun.");
            host.Motor.Teleport(world.Gosha.BallPosition-Vector3.forward*3.4f-Vector3.up*.6f);
            yield return ShootBall();
            Require(host.RequestDropSelected(),"Q did not accept the gun with an active ball line.");
            yield return Wait(()=>!host.OwnsHarpoon && !host.HasBallLine && world.Gosha.Ball==GoshaBallMode.Deck &&
                ReadClient()?.droppedItems?.Count(item=>item.kind=="Harpoon" && item.instance==gunId.ToString())==1,
                4,"Dropping the gun left its line, ball, or instance in the wrong state.");
            Require(FindObjectsByType<GoshaBallBody>().Length==1,"Dropping the gun duplicated the ball.");
            Check("Q released the held ball underwater and preserved the original harpoon. Q on the loaded gun then freed the ball and replicated that same gun ID once.");
        }

        IEnumerator AimAtBall()
        {
            float until=Time.unscaledTime+4;
            while(true)
            {
                host.Motor.LookAtConsole(world.Gosha.BallPosition);
                yield return null;
                Physics.SyncTransforms();
                var eye=host.transform.position+Vector3.up*.6f;
                if(Physics.Raycast(eye,host.Motor.AimDirection,out var hit,host.tuning.maxRange,host.tuning.hitMask,
                    QueryTriggerInteraction.Ignore) && hit.collider.TryGetComponent<GoshaBallBody>(out _)) yield break;
                Require(Time.unscaledTime<until,"Owner's sightline did not reach the physical ball: "+hit.collider?.name);
            }
        }
        IEnumerator ShootBall()
        {
            yield return Pause(host.tuning.fireCooldown+.1f);
            yield return AimAtBall();
            Require(host.QaFireOwned(),"Harpoon input refused before shooting the ball.");
            yield return Wait(()=>host.HasBallLine && world.Gosha.Ball==GoshaBallMode.Tethered,3,"Server did not tether the first-hit ball.");
        }
        IEnumerator ReelBall()
        {
            var start=world.Gosha.BallPosition;
            Require(host.QaHoldOwned(false,5),"Owner could not reel.");
            yield return Wait(()=>Vector3.Distance(start,world.Gosha.BallPosition)>.2f,3,"Ball did not move.");
            Require(world.Gosha.Ball==GoshaBallMode.Tethered,"Distant shot teleported straight to hands.");
            yield return Wait(()=>world.HasGoshaBall(host.Motor) && ReadClient()?.gosha.Holder==host.OwnerId &&
                ReadClient()?.gosha.Ball==GoshaBallMode.Held,5,"Peers did not agree on the held ball.");
            host.QaHoldOwned(false,0);
            Require(!host.HasBallLine && !helper.HasBallLine && FindObjectsByType<GoshaBallBody>().Length==1,"A duplicate ball or cable survived pickup.");
        }
    }

    public sealed partial class RaidWorld
    {
        internal void StageBallTrial(Vector3 position)
        {
            if (!IsServerInitialized) throw new InvalidOperationException("Host required.");
            BeginBallRecovery();
            ClearBallLine();
            DropBall(position,Vector3.zero,false);
            gosha.Value=pet;
            Physics.SyncTransforms();
        }
    }
}
#endif
