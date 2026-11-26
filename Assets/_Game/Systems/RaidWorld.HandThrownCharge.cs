using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public enum ChargeThrowAction : byte { WindUp, Release, Cancel }

    public sealed partial class DiverMotor
    {
        int windChargeSerial=-1,windChargeSlot=-1,windChargeDepth;
        float windChargeStarted;

        public void CmdChargeGesture(int serial,ChargeThrowAction action)
        {
            if(!NetworkObject || !IsClientInitialized || !IsOwner || !_camera)return;
            RequestChargeGesture(serial,action,_camera.transform.forward);
        }

        [ServerRpc(RequireOwnership=true)]
        void RequestChargeGesture(int serial,ChargeThrowAction action,Vector3 aim)
        {
            if(action==ChargeThrowAction.Cancel){windChargeSerial=-1;return;}
            var world=RaidWorld.Instance;
            var tool=GetComponent<HarpoonTool>();
            if(!world || !tool || !world.CanWindPocketCharge(this,tool,serial))
            {windChargeSerial=-1;return;}
            if(action==ChargeThrowAction.WindUp)
            {
                if(windChargeSerial==serial && windChargeSlot==tool.ActiveSlot && windChargeDepth==world.CurrentDepth)return;
                windChargeSerial=serial;
                windChargeSlot=tool.ActiveSlot;
                windChargeDepth=world.CurrentDepth;
                windChargeStarted=Time.time;
                return;
            }
            if(action!=ChargeThrowAction.Release || windChargeSerial!=serial)return;
            windChargeSerial=-1;
            if(tool.ActiveSlot!=windChargeSlot || world.CurrentDepth!=windChargeDepth)return;
            float length=aim.sqrMagnitude;
            if(!(length>.99f && length<1.01f))return;
            float strength=Mathf.Clamp01((Time.time-windChargeStarted)/Mathf.Max(.1f,world.tuning.ballWindSeconds));
            world.ThrowPocketCharge(this,tool,serial,aim.normalized,strength);
        }
    }

    public sealed partial class RaidWorld
    {
        const float ThrownChargeRadius=.13f;

        internal bool CanWindPocketCharge(DiverMotor diver,HarpoonTool tool,int serial)
        {
            if(!IsServerInitialized || team==null || saveFailed || transitionRemaining>0 ||
                serial<0 || diver.IsHelpless || diver.IsTrapped || GoshaKnocked(diver) ||
                diver.Owner?.IsAuthenticated!=true || diver.Owner.FirstObject!=diver.NetworkObject)return false;
            if(serial>0)
            {
                var carried=tool.CarriedInvention;
                return carried!=null && carried.kind=="charge" && carried.depth==CurrentDepth &&
                    carried.serial==serial && carried.operatorId==diver.OwnerId && carried.helperId<0;
            }
            if(Engaged(diver.OwnerId) || !tool.CanDrawPocketInvention)return false;
            var stack=tool.InventionInSlot(tool.ActiveSlot);
            return stack.kind=="charge" && stack.count>0;
        }

        internal void ThrowPocketCharge(DiverMotor diver,HarpoonTool tool,int serial,Vector3 aim,float wind)
        {
            var profile=team.Player(ProgressKey(diver));
            if(profile==null)return;
            int pocket=tool.ActiveSlot-FirstCargoSlot;
            if(serial==0 && (profile.pocketStacks==null || pocket<0 || pocket>=profile.pocketStacks.Length ||
                profile.pocketStacks[pocket].kind!="charge" || profile.pocketStacks[pocket].count<=0))return;
            var eye=diver.transform.position+Vector3.up*.6f;
            var hand=GoshaBallGrip.WorldCenter(diver,aim,wind,1);
            var arm=hand-eye;
            Physics.SyncTransforms();
            if(!ChargePathOpen(eye,arm,diver) || !ChargePathOpen(hand,aim*.06f,diver))
            {
                InventoryReceipt(diver,profile,false,"Для броска нужно свободное место перед рукой");
                return;
            }
            float fullSpeed=Mathf.Max(tuning.ballShortThrow,Fishery.chargeThrowSpeed*team.TechValue("charge","remote",1));
            float speed=Mathf.Lerp(tuning.ballShortThrow,fullSpeed,wind);
            var velocity=aim*speed+Vector3.up*tuning.ballThrowLift*Mathf.Lerp(.60f,1f,wind);
            void Launch(TechRig rig)
            {
                ClearGearHands(rig);
                rig.position=hand;
                rig.anchor=velocity;
                rig.timer=Mathf.Max(.1f,TechCatalog.Find("charge").interval);
                rig.mode="fuse";
            }
            bool ok;
            string reason;
            if(serial==0)
                ok=team.DeployTechFromSlot(profile,pocket,hand,diver.OwnerId,Launch,SaveTransaction,out reason);
            else
            {
                var previous=tool.CarriedInvention;
                if(previous==null || previous.serial!=serial)return;
                var before=(previous.operatorId,previous.helperId,previous.baitId,previous.position,previous.anchor,previous.timer,previous.mode);
                Launch(previous);
                ok=false;
                try{ok=SaveTransaction();}
                finally
                {
                    if(!ok)(previous.operatorId,previous.helperId,previous.baitId,previous.position,previous.anchor,previous.timer,previous.mode)=before;
                }
                reason=ok?null:"Не удалось сохранить бросок";
            }
            if(ok)
            {
                AnnounceRigPulse(hand,0);
                Publish();
            }
            InventoryReceipt(diver,profile,ok,ok?"Динамит брошен":reason);
        }

        bool ChargePathOpen(Vector3 origin,Vector3 segment,DiverMotor owner)
        {
            int overlaps=Physics.OverlapSphereNonAlloc(origin,ThrownChargeRadius,pulseObjects,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(overlaps==pulseObjects.Length)return false;
            for(int i=0;i<overlaps;i++)
                if(pulseObjects[i].GetComponentInParent<DiverMotor>()!=owner)return false;
            if(segment.sqrMagnitude<.000001f)return true;
            int hits=Physics.SphereCastNonAlloc(origin,ThrownChargeRadius,segment.normalized,gearHits,segment.magnitude,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(hits==gearHits.Length)return false;
            for(int i=0;i<hits;i++)
                if(gearHits[i].collider.GetComponentInParent<DiverMotor>()!=owner)return false;
            return true;
        }

        // Only the server sweeps the live scene. The usual rig snapshots carry position, velocity and fuse time.
        void FlyCharge(TechRig rig,float seconds)
        {
            while(seconds>.00001f && rig.anchor.sqrMagnitude>0)
            {
                float step=Mathf.Min(seconds,.04f);
                seconds-=step;
                bool dry=Nereid.NereidVessel.Active && Nereid.NereidVessel.Active.ContainsWalkingDeck(rig.position);
                var acceleration=dry?Physics.gravity:Vector3.down*Fishery.chargeDrop;
                var velocity=rig.anchor*Mathf.Exp(-(dry?.12f:Fishery.chargeDrag)*step)+acceleration*step;
                var travel=(rig.anchor+velocity)*(.5f*step);
                float distance=travel.magnitude;
                if(distance<.00001f){rig.anchor=velocity;continue;}
                var direction=travel/distance;
                int hits=Physics.SphereCastNonAlloc(rig.position,ThrownChargeRadius,direction,gearHits,distance,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                int nearest=-1;
                for(int i=0;i<hits;i++)
                {
                    var collider=gearHits[i].collider;
                    if(collider.GetComponentInParent<DiverMotor>() || collider.GetComponentInParent<FishActor>())continue;
                    if(nearest<0 || gearHits[i].distance<gearHits[nearest].distance)nearest=i;
                }
                if(nearest<0){rig.position+=travel;rig.anchor=velocity;continue;}
                var contact=gearHits[nearest];
                rig.position+=direction*Mathf.Max(0,contact.distance-.002f);
                rig.position+=contact.normal*.002f;
                var tangent=Vector3.ProjectOnPlane(velocity,contact.normal);
                float inward=Mathf.Min(0,Vector3.Dot(velocity,contact.normal));
                rig.anchor=tangent*.63f-contact.normal*(inward*(dry?.34f:.22f));
                if(contact.normal.y>.45f && rig.anchor.sqrMagnitude<.12f)rig.anchor=Vector3.zero;
            }
        }
    }
}
