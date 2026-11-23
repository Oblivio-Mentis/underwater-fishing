using UnityEngine;
using Action = Raid.DiverAvatarRig.Action;

namespace Raid
{
    // Observer rig driven solely by the replicated player transform, equipment and accepted actions.
    [DisallowMultipleComponent,DefaultExecutionOrder(260)]
    public sealed class DiverSuitVisual : MonoBehaviour
    {
        sealed class Limb { public Transform Upper,Lower,End; }
        readonly Limb[] arms=new Limb[2],legs=new Limb[2];
        DiverAvatarRig avatar;
        HarpoonPresentation equipment;
        readonly System.Collections.Generic.List<Mesh> weaponMeshes=new();
        Transform shuttle;
        bool weaponBuilt;
        DiverMotor diver;
        HarpoonTool tool;
        Transform body,head,gun,fishAnchor;
        internal Transform GoshaPerch {get;private set;}
        GameObject fishModel;
        string fishSpecies;
        long visibleCatchId;
        int shotSequence,useSequence,displaySlot,targetSlot;
        internal bool BallVisible { get; private set; }
        internal Vector3 BallCenter { get; private set; }
        internal bool Visible => body!=null && body.gameObject.activeSelf;
        float stride,speed,swimming,kick,reach,draw,movingPhase,shownWind,throwFollow;
        uint lastBallLaunch;
        readonly DiverStride gait=new();
        Renderer legacyVisor,legacyCapsule;

        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);

        public void Initialize(HarpoonPresentation source)
        {
            equipment=source;
            diver=GetComponent<DiverMotor>();tool=GetComponent<HarpoonTool>();
            legacyVisor=transform.Find("Visor")?.GetComponent<Renderer>();
            legacyCapsule=transform.Find("Diver body")?.GetComponent<Renderer>();
            if(legacyVisor!=null)legacyVisor.enabled=false;if(legacyCapsule!=null)legacyCapsule.enabled=false;
            body=DiveOutfit.Pivot(transform,"Expedition suit",Vector3.zero);
            avatar=Instantiate(diver.crewAppearance,body,false);
            avatar.Prepare();
            GoshaPerch=DiveOutfit.Pivot(body,"Gosha perch",RaidWorld.GoshaShoulderOffset);
            head=DiveOutfit.Pivot(body,"Helmet pose",body.InverseTransformPoint(avatar.head.position));
            for(int i=0;i<2;i++)
            {
                arms[i]=Chain("Arm "+i,avatar.Arm(i));
                legs[i]=Chain("Leg "+i,avatar.Leg(i));
            }
            gun=DiveOutfit.Pivot(body,"Service harpoon",Vector3.zero);
            fishAnchor=DiveOutfit.Pivot(arms[1].End,"Pocket catch",V(-.032f,.058f,.06f));
            fishAnchor.localRotation=Quaternion.Euler(0,-65,-6);
            fishAnchor.localScale=Vector3.one*.27f;
            shotSequence=tool.ShotSequence;useSequence=diver.InteractionSequence;displaySlot=targetSlot=tool.ActiveSlot;
            body.gameObject.SetActive(false);
        }

        Limb Chain(string label,DiverAvatarRig.Chain bones)
        {
            Vector3 start=body.InverseTransformPoint(bones.upper.position);
            float top=Vector3.Distance(start,body.InverseTransformPoint(bones.lower.position));
            float bottom=Vector3.Distance(body.InverseTransformPoint(bones.lower.position),body.InverseTransformPoint(bones.end.position));
            var limb=new Limb();
            limb.Upper=DiveOutfit.Pivot(body,label,start);
            limb.Lower=DiveOutfit.Pivot(limb.Upper,label+" hinge",Vector3.down*top);
            limb.End=DiveOutfit.Pivot(limb.Lower,label+" extremity",Vector3.down*bottom);
            return limb;
        }

        float fall;
        void LateUpdate()
        {
            if(body==null||diver==null||tool==null)return;
            // FishNet can reuse the diver object between sessions, and the motor restores the prefab renderers on pool return.
            if(legacyVisor!=null&&legacyVisor.enabled)legacyVisor.enabled=false;
            if(legacyCapsule!=null&&legacyCapsule.enabled)legacyCapsule.enabled=false;
            var ui=UI.UiRoot.Instance;var local=ui==null?null:ui.Context.Player;
            bool visible=diver.IsClientInitialized&&!diver.IsConsumed&&local!=null&&local.Spectating!=diver&&(!diver.IsOwner||local.Spectating!=null);
            if(body.gameObject.activeSelf!=visible)body.gameObject.SetActive(visible);
            float dt=Mathf.Clamp(Time.deltaTime,0,.06f);
            gait.Advance(diver,Time.deltaTime);
            if(tool.ShotSequence!=shotSequence){kick=1;shotSequence=tool.ShotSequence;}
            if(diver.InteractionSequence!=useSequence){reach=1;useSequence=diver.InteractionSequence;}
            if(!visible)
            {
                BallVisible=false;
                draw=shownWind=0;
                displaySlot=targetSlot=tool.ActiveSlot;
                return;
            }
            if(tool.ActiveSlot!=targetSlot){draw=GoshaBallGrip.SwapDuration;targetSlot=tool.ActiveSlot;}
            if(!weaponBuilt && equipment!=null && equipment.MakeWorldModel(gun,weaponMeshes,out var bolt,out var drum,out var muzzle,out shuttle))
            {
                weaponBuilt=true;
                if(!diver.IsOwner)
                {
                    var cable=DiveOutfit.Pivot(body,"Observed harpoon cable",Vector3.zero).gameObject.AddComponent<HarpoonShotFx>();
                    cable.Initialize(tool,muzzle,bolt,drum,equipment.steel,true);
                }
            }
            speed=gait.Speed;stride=gait.Weight;swimming=gait.Water;
            kick=Mathf.MoveTowards(kick,0,dt*5.3f);reach=Mathf.MoveTowards(reach,0,dt*2.1f);draw=Mathf.Max(0,draw-dt);
            if(draw<GoshaBallGrip.SwapDuration*.5f)displaySlot=targetSlot;
            float run=gait.Running*(1f-swimming);
            movingPhase+=dt*Mathf.Lerp(3.1f,9.1f,stride);
            float phase=Mathf.LerpAngle(gait.Phase*Mathf.Rad2Deg,movingPhase*Mathf.Rad2Deg,swimming)*Mathf.Deg2Rad;
            float beat=Mathf.Sin(phase);
            float idle=Mathf.Sin(Time.time*1.85f+diver.OwnerId*.7f);
            bool held=tool.HandsFull,helper=tool.AssistTarget!=null;
            var salvage=tool.CarriedSalvage;
            var world=RaidWorld.Instance;
            bool cuddling=world?.IsHoldingGosha(diver)==true;
            bool ball=BallVisible=!diver.IsDead && !held && !cuddling && world?.HasGoshaBall(diver)==true && tool.HasBallInSlot(displaySlot);
            float wind=shownWind=ball ? displaySlot==tool.ActiveSlot?GoshaBallGrip.Charge(diver):shownWind : 0;
            var look=Quaternion.Euler(diver.ViewPitch,diver.transform.eulerAngles.y,0);
            float cast=world!=null && ball?GoshaBallGrip.Stroke(world,world.Gosha):0f;
            BallCenter=transform.InverseTransformPoint(GoshaBallGrip.WorldCenter(diver,look*Vector3.forward,wind,cast))+
                GoshaBallGrip.Lower(GoshaBallGrip.Stow(draw));
            if(world && lastBallLaunch!=world.Gosha.BallLaunch && world.Gosha.Thrower==diver.OwnerId)throwFollow=GoshaBallGrip.FollowThrough;
            if(world)lastBallLaunch=world.Gosha.BallLaunch;
            throwFollow=Mathf.Max(0,throwFollow-dt);
            var pocket=!ball&&!cuddling&&!held?tool.FishInSlot(displaySlot):null;
            gun.gameObject.SetActive(!diver.IsDead && !cuddling && !held && !helper && throwFollow<=0 && tool.OwnsHarpoon && displaySlot==0);
            var shownFish=tool.HoldingHeavyCatch?tool.LiftedHeavy:pocket;
            long shownId=tool.HoldingHeavyCatch?tool.HeldCargoId:tool.CatchIdInSlot(displaySlot);
            UpdateFish(!diver.IsDead?shownFish?.SpeciesId:null,shownFish!=null?shownId:0);
            avatar.BeginPose();
            if(diver.IsDead)
            {
                body.localPosition=Vector3.zero;body.localRotation=Quaternion.identity;
                head.localRotation=Quaternion.Euler(13,4,-9);
                for(int i=0;i<2;i++){int s=i==0?-1:1;Pose(arms[i],-11,0,s*(22+idle*2),-17,0,81);Pose(legs[i],s*7,0,s*6,9,0,-6);}
                ApplyAppearance(Action.Dead,dt);return;
            }
            float lean=Mathf.Lerp(1+run*13f,Mathf.Lerp(32,71,stride),swimming);
            fall=Mathf.MoveTowards(fall,(RaidWorld.Instance?.GoshaKnocked(diver)??false)?1:0,dt*5.5f);
            body.localRotation=Quaternion.Euler(lean-fall*79,beat*run*4f,fall*13+beat*stride*(1-swimming)*2.5f-gait.Lateral*run*4f);
            body.localPosition=V(0,-fall*.53f+(1-swimming)*Mathf.Abs(beat)*(.025f+.045f*run)*stride+idle*.006f,0);
            head.localRotation=Quaternion.Euler(Mathf.Clamp(diver.ViewPitch-lean*.68f,-48,46),0,0);
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;float cycle=s*beat;
                float lift=Mathf.Max(0,s*Mathf.Cos(phase));
                float thigh=Mathf.Lerp(-cycle*(30f+run*23f)*stride,-cycle*(12+23*stride),swimming);
                float knee=Mathf.Lerp((6f+lift*(35f+run*43f))*stride,12+Mathf.Max(0,cycle)*38*stride,swimming);
                float ankle=Mathf.Lerp(-knee*.47f+cycle*stride*(7f+run*8f),18-knee*.48f,swimming);
                Pose(legs[i],thigh,0,s*4+gait.Lateral*cycle*stride*11f*(1-swimming),knee,0,ankle);
                float shoulder=cycle*stride*(27f+run*16f),elbow=-18-run*36-Mathf.Max(0,cycle)*17*stride;
                float roll=s*7,yaw=0,wrist=83;
                if(held||helper||ball||cuddling)
                {
                    shoulder=helper?-73:-42;elbow=helper?-13:-51;roll=s*(helper?4:19);yaw=-s*12;
                }
                else if(gun.gameObject.activeSelf||pocket!=null)
                {
                    shoulder=s>0?-48:-39;elbow=s>0?-40:-57;yaw=-s*(s>0?8:24);roll=s*(s>0?5:8);
                    shoulder-=15*kick;shoulder+=GoshaBallGrip.Stow(draw)*30;
                }
                else
                {
                    shoulder=Mathf.Lerp(shoulder,-90+cycle*30*stride,swimming);
                    elbow=Mathf.Lerp(elbow,-25-(1-cycle)*13*stride,swimming);
                }
                if(s<0&&reach>0)
                {
                    float press=Mathf.Sin(reach*Mathf.PI);
                    shoulder=Mathf.Lerp(shoulder,-89,press);elbow=Mathf.Lerp(elbow,-6,press);roll*=1-press;yaw*=1-press;
                }
                if(swimming>.01f&&(held||helper||gun.gameObject.activeSelf||pocket!=null))shoulder-=lean*.73f;
                Pose(arms[i],shoulder,yaw,roll,elbow,0,wrist);
            }
            if(ball || cuddling || salvage!=null)
            {
                var center=ball?BallCenter:salvage!=null?LootActor.ObserverCenter:new Vector3(0,.18f,.48f);
                for(int i=0;i<2;i++)
                {
                    int side=i==0?-1:1;
                    var grip=ball?GoshaBallGrip.GloveOffset(side,wind):salvage!=null
                        ?salvage.GloveOffset(side,salvage.transform.lossyScale):new Vector3(side*.19f,-.04f,-.035f);
                    if(cuddling && side>0)grip+=new Vector3(-.075f,.13f,.07f)*(.5f+.5f*Mathf.Sin(Time.time*5.6f));
                    ReachGrip(arms[i],transform.TransformPoint(center+grip),transform.rotation*Quaternion.Euler(-16,side*24,-side*48),
                        transform.rotation*new Vector3(side*.8f,-.65f,-.3f));
                }
            }
            else if(throwFollow>0f)
            {
                float t=1f-throwFollow/GoshaBallGrip.FollowThrough;
                var rig=diver.firstPersonAppearance;
                var grip=Vector3.Lerp(rig.ballRelease+rig.ballThrowWrist,rig.restingWrist,Mathf.SmoothStep(0,1,t));
                ReachGrip(arms[1],diver.transform.position+Vector3.up*.6f+look*grip,look*Quaternion.Euler(rig.ballThrowAngles),
                    transform.TransformDirection(new Vector3(.55f,-1,-.22f)));
            }
            else if(gun.gameObject.activeSelf && weaponBuilt)HoldHarpoon(beat,idle);
            var motion=fall>.1f?Action.Knockdown:throwFollow>0?Action.Throw:reach>.01f?Action.PressPanel:draw>0?Action.SwapEquipment:
                cuddling?Action.PetGosha:ball?(wind>.01f?Action.WindThrow:Action.HoldBall):
                salvage!=null?Action.CarrySalvage:shownFish!=null?Action.CarryFish:kick>.01f?Action.Shoot:
                helper||tool.HookedFish!=null?Action.Reel:gun.gameObject.activeSelf?Action.Harpoon:
                !diver.IsWalking?Action.Swim:run>.15f?Action.Sprint:speed>.1f?Action.Walk:Action.Idle;
            ApplyAppearance(motion,dt);
        }

        void ApplyAppearance(Action motion,float dt)
        {
            avatar.head.rotation=head.rotation*avatar.headBind;
            for(int i=0;i<2;i++)
            {
                var arm=arms[i];var leg=legs[i];
                avatar.Arm(i).Drive(arm.Upper,arm.Lower,arm.End,DiverAvatarRig.DownwardSegment);
                avatar.Leg(i).Drive(leg.Upper,leg.Lower,leg.End,DiverAvatarRig.DownwardSegment);
            }
            avatar.FinishPose(motion,dt);
        }

        void HoldHarpoon(float beat,float idle)
        {
            var yaw=Quaternion.Euler(0,diver.transform.eulerAngles.y,0);
            float run=gait.Running*(1-swimming);
            float lowered=GoshaBallGrip.Stow(draw)*32+run*13f;
            var aim=yaw*Quaternion.Euler(Mathf.Clamp(diver.ViewPitch,-80,80)+lowered-kick*5,run*-16f,run*(8f+beat*3f));
            var shoulders=(arms[0].Upper.position+arms[1].Upper.position)*.5f;
            gun.SetPositionAndRotation(shoulders+yaw*(Vector3.right*(.095f-run*.026f)-Vector3.up*(.115f+run*.048f))
                +aim*new Vector3(0,beat*stride*.008f+idle*.002f,.143f-kick*.035f),aim);
            shuttle.localPosition=Vector3.back*kick*.028f;
            ReachGrip(arms[1],gun.TransformPoint(V(.022f,-.103f,-.087f)),aim*Quaternion.Euler(0,-12,-66),
                yaw*V(.78f,-.75f,-.42f));
            if(reach<.015f)
                ReachGrip(arms[0],gun.TransformPoint(V(-.034f,-.047f,.117f)),aim*Quaternion.Euler(-9,9,64),
                    yaw*V(-.41f,-1.17f,-.4f));
        }

        static void ReachGrip(Limb arm,Vector3 grip,Quaternion handRotation,Vector3 elbowPole)
        {
            var shoulder=arm.Upper.position;
            float upper=arm.Lower.localPosition.magnitude,lower=arm.End.localPosition.magnitude;
            var target=grip-handRotation*V(0,0,.042f);
            var axis=(target-shoulder).normalized;
            float reach=Mathf.Clamp(Vector3.Distance(shoulder,target),Mathf.Abs(upper-lower)+.001f,upper+lower-.001f);
            float along=(upper*upper-lower*lower+reach*reach)/(2*reach);
            float away=Mathf.Sqrt(Mathf.Max(0,upper*upper-along*along));
            var elbow=shoulder+axis*along+Vector3.ProjectOnPlane(elbowPole,axis).normalized*away;
            arm.Upper.rotation=Quaternion.FromToRotation(arm.Lower.position-shoulder,elbow-shoulder)*arm.Upper.rotation;
            arm.Lower.rotation=Quaternion.FromToRotation(arm.End.position-arm.Lower.position,shoulder+axis*reach-arm.Lower.position)*arm.Lower.rotation;
            arm.End.rotation=handRotation;
        }

        void UpdateFish(string species,long id)
        {
            if(species==fishSpecies && id==visibleCatchId)return;
            fishSpecies=species;visibleCatchId=id;if(fishModel!=null){fishModel.SetActive(false);Destroy(fishModel);fishModel=null;}
            if(species!=null)fishModel=CatchFishModel.Create(fishAnchor,species,id);
        }

        static void Pose(Limb chain,float pitch,float yaw,float roll,float hinge,float twist,float wrist)
        {
            chain.Upper.localRotation=Quaternion.Euler(pitch,yaw,roll);
            chain.Lower.localRotation=Quaternion.Euler(hinge,twist,0);
            chain.End.localRotation=Quaternion.Euler(wrist,0,0);
        }

        void OnDestroy()
        {
            if(body!=null)Destroy(body.gameObject);
            foreach(var mesh in weaponMeshes)if(mesh!=null)Destroy(mesh);
        }
    }
}
