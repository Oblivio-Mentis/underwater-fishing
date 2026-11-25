#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using Raid.UI;
using UnityEngine;

namespace Raid.Nereid
{
    internal sealed partial class CargoReceiverTrial
    {
        CargoLandingSilt sand;
        float impactTime=-1;
        bool controls,inputStarted,inputDone;

        async Awaitable WaitInput(Func<bool> condition,string label)
        {
            var limit=Time.realtimeSinceStartup+20;
            while(!condition())
            {
                if(Time.realtimeSinceStartup>limit)throw new TimeoutException(label);
                await Awaitable.NextFrameAsync();
            }
        }

        async void StageInput(RaidWorld world,DiverMotor owner)
        {
            var tool=owner.GetComponent<HarpoonTool>();
            try
            {
                var berth=world.CargoLoadingPosition;
                if(host)
                {
                    var guest=DiverMotor.ServerDivers.First(d=>!d.IsOwner);
                    var fish=FindObjectsByType<FishActor>();
                    var small=fish.First(f=>!f.RequiresPartner && f.depthTier==world.CurrentDepth);
                    var large=fish.First(f=>f.RequiresPartner);
                    tool.StagePocketForTrial(small,First,2);
                    tool.StagePocketForTrial(small,First+20,3);
                    guest.GetComponent<HarpoonTool>().StageLiftedForTrial(large,Second);
                    if(tool.RequestLoadFish())throw new Exception("A diver on deck could load the cage.");
                    owner.Teleport(berth+new Vector3(2.14f,-.18f,-.4f));
                    await Awaitable.WaitForSecondsAsync(.35f);owner.LookAtConsole(berth);
                    if(!tool.SelectSlot(3))throw new Exception("Cannot select the Q control catch");
                    await WaitInput(()=>tool.HeldCargoId==First+20 && CargoDispatch.CanLoad(world,owner),"Own selected catch at open gate");
                    if(!tool.RequestDropSelected())throw new Exception("Q refused its selected catch");
                    await WaitInput(()=>world.DroppedCount==1 && tool.CatchIdInSlot(3)==0,"Q placed one physical catch");
                    if(world.Team.cargo.Count!=0 || !tool.SelectSlot(0))throw new Exception("Q loaded the catch, or the harpoon slot is unavailable");
                    await WaitInput(()=>tool.ActiveSlot==0 && tool.FishLoadTarget(out _,out long id) && id==First,"Pocket catch under equipped harpoon");
                }
                else
                {
                    await WaitInput(()=>File.Exists(Path.Combine(directory,"guest-turn")),"Waiting for host cargo controls");
                    await WaitInput(()=>tool.HoldingHeavyCatch && (owner.transform.position-berth).sqrMagnitude<10,"Guest received the heavy catch");
                    await Awaitable.WaitForSecondsAsync(.5f);
                    await WaitInput(()=>{owner.LookAtConsole(berth);return CargoDispatch.CanLoad(world,owner);},"Guest camera sees the open loading gate");
                }

                int shots=tool.ShotSequence;
                if(!tool.QaPrimaryOwned())throw new Exception("LMB did not consume the cargo context");
                long expected=host?First:Second;
                await WaitInput(()=>world.View.cargo.Any(x=>x.id==expected && !x.IsSalvage),"LMB transferred original fish "+expected);
                if(tool.ShotSequence!=shots)throw new Exception("The loading click also fired the harpoon.");
                if(NereidAim.Active.IsAiming || NereidAim.Blend>.01f)throw new Exception("The loading click also activated harpoon aim.");
                if(tool.RequestLoadFish())throw new Exception("A repeated press loaded the same fish.");
                if(host)
                {
                    owner.Teleport(berth+Vector3.right*6);
                    var guest=DiverMotor.ServerDivers.First(d=>!d.IsOwner);
                    guest.Teleport(berth+new Vector3(2.14f,-.18f,-.4f));
                    File.WriteAllText(Path.Combine(directory,"guest-turn"),"yes");
                    timeline.Add("Q at the open cage made a world drop. LMB with the harpoon selected deposited the intended pocket fish without a shot, zoom or duplicate.");
                }
                else timeline.Add("Remote owned LMB loaded the two-handed catch into the shared manifest exactly once.");
                File.WriteAllText(Path.Combine(folder,"input-pass"),"yes");
                inputDone=true;
            }
            catch(Exception e){timeline.Add(e.ToString());Finish("ERROR");}
        }
    }
}
#endif
