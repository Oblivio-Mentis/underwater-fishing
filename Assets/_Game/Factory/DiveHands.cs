using UnityEngine;
using Action = Raid.DiverAvatarRig.Action;

namespace Raid
{
    sealed class DiveHands
    {
        sealed class Arm
        {
            public Transform Upper,Sleeve,Wrist,Clavicle;
            public float UpperLength, ForearmLength;
        }
        readonly DiverAvatarRig avatar;
        readonly DiverStride stride = new DiverStride();
        readonly Transform root,fishAnchor;
        readonly HeldInvention invention;
        readonly Arm[] arms=new Arm[2];
        string species;
        long catchId;
        GameObject fish;
        Transform salvageModel;
        LootActor salvageSource;
        internal bool BallVisible { get; private set; }
        internal Vector3 BallCenter { get; private set; }
        Quaternion leftPalmFrame = Quaternion.identity;
        Vector3 leftPalmOffset = new(-.02f,.03f,.09f);
        DiverMotor viewed;
        int sequence,displaySlot,targetSlot,shotSequence;
        bool displayedHeavy,targetHeavy;
        float press,switchTime,releaseStroke,shownWind,shot;
        uint lastLaunch;

        public DiveHands(DiverAvatarRig template, Transform camera)
        {
            root=DiveOutfit.Pivot(camera,"Diver's hands",Vector3.zero);
            invention=new HeldInvention(root);
            avatar=Object.Instantiate(template,root,false);
            avatar.Prepare();
            for(int i=0;i<2;i++)
            {
                string side=i==0?"Left":"Right";
                var bones=avatar.Arm(i);
                var shoulder=root.InverseTransformPoint(bones.upper.position);
                var elbow=root.InverseTransformPoint(bones.lower.position);
                var wrist=root.InverseTransformPoint(bones.end.position);
                arms[i]=new Arm
                {
                    Upper=DiveOutfit.Pivot(root,side+" upper arm",Vector3.zero),
                    Sleeve=DiveOutfit.Pivot(root,side+" sleeve",Vector3.zero),
                    Wrist=DiveOutfit.Pivot(root,side+" glove",Vector3.zero),
                    Clavicle=bones.upper.parent!=avatar.Arm(1-i).upper.parent?bones.upper.parent:null,
                    UpperLength=Vector3.Distance(shoulder,elbow),
                    ForearmLength=Vector3.Distance(elbow,wrist)
                };
            }
            CalibrateLeftPalm(avatar.leftArm);
            fishAnchor=DiveOutfit.Pivot(root,"Presented catch",Vector3.zero);
        }

        // Bind the surface of the actual glove to the cap while preserving both arm lengths.
        void CalibrateLeftPalm(DiverAvatarRig.Chain arm)
        {
            Transform index=null,pinky=null,middle=null;
            foreach(Transform child in arm.end)
            {
                if (child.name.EndsWith("Index1")) index=child;
                if (child.name.EndsWith("Pinky1")) pinky=child;
                if (child.name.EndsWith("Middle1")) middle=child;
            }
            if (!index || !pinky || !middle) return;
            Quaternion toWrist = arm.endBind * Quaternion.Inverse(arm.end.rotation);
            var distal = toWrist*(middle.position-arm.end.position);
            var side = toWrist*(index.position-pinky.position);
            var normal = Vector3.Cross(side,distal).normalized;
            leftPalmFrame = Quaternion.LookRotation(normal, distal);
            leftPalmOffset = distal*.68f + normal*.021f;
        }

        public void SetVisible(bool value)
        {
            if(root==null||root.gameObject.activeSelf==value)return;
            root.gameObject.SetActive(value);
            if(!value){stride.Reset();BallVisible=false;}
        }

        public bool HasPanelPress(DiverMotor actor)
            => viewed==actor && (press>0||sequence!=actor.InteractionSequence);

        public bool Animate(DiverMotor actor,HarpoonTool equipment,Transform gun,float aim,float dt,bool panelPress,float monitorRest)
        {
            if(viewed!=actor)
            {
                viewed=actor;sequence=actor.InteractionSequence;shotSequence=equipment.ShotSequence;shot=0;
                displaySlot=targetSlot=equipment.ActiveSlot;displayedHeavy=targetHeavy=equipment.HandsFull;
                press=switchTime=releaseStroke=shownWind=0;
                lastLaunch=RaidWorld.Instance!=null?RaidWorld.Instance.Gosha.BallLaunch:0;
                stride.Reset();
            }
            if(actor.RaidStrugglePose(out float brace,out float breakKick))
            {
                AnimateRaidGrip(brace,breakKick,dt);
                return false;
            }
            if(targetSlot!=equipment.ActiveSlot||targetHeavy!=equipment.HandsFull)
            {
                targetSlot=equipment.ActiveSlot;targetHeavy=equipment.HandsFull;switchTime=GoshaBallGrip.SwapDuration;
            }
            bool keyPress=Nereid.NereidDispatchButton.TryPalm(actor,out var contact,out float keyReach);
            if(sequence!=actor.InteractionSequence){sequence=actor.InteractionSequence;press=keyPress?0:.46f;}
            if(shotSequence!=equipment.ShotSequence){shotSequence=equipment.ShotSequence;shot=.16f;}
            shot=Mathf.Max(0,shot-dt);
            switchTime=Mathf.Max(0,switchTime-dt);press=Mathf.Max(0,press-dt);
            if(switchTime<GoshaBallGrip.SwapDuration*.5f){displaySlot=targetSlot;displayedHeavy=targetHeavy;}
            float stow=GoshaBallGrip.Stow(switchTime);
            float push=Mathf.Pow(Mathf.Max(0,Mathf.Sin(press/.46f*Mathf.PI)),.7f);
            bool grip=equipment.AssistTarget!=null;
            var world=RaidWorld.Instance;
            bool selectedBall=world?.GoshaBallInHands(actor)==true;
            if(world!=null && world.Gosha.BallLaunch!=lastLaunch && world.Gosha.Thrower==actor.OwnerId)
                releaseStroke=GoshaBallGrip.FollowThrough;
            if(world!=null)lastLaunch=world.Gosha.BallLaunch;
            releaseStroke=Mathf.Max(0,releaseStroke-dt);
            releaseStroke=Mathf.Max(releaseStroke,equipment.ChargeFollowThroughUntil-Time.unscaledTime);
            bool cuddling=world?.IsHoldingGosha(actor)==true;
            bool ball=BallVisible=!cuddling && !displayedHeavy && world?.HasGoshaBall(actor)==true && equipment.HasBallInSlot(displaySlot);
            float wind=shownWind=ball ? selectedBall?GoshaBallGrip.Charge(actor):shownWind : 0;
            Vector3 stowOffset=GoshaBallGrip.Lower(stow);
            float cast=world!=null && selectedBall?GoshaBallGrip.Stroke(world,world.Gosha):0;
            BallCenter=GoshaBallGrip.CameraCenter(avatar,wind,cast)+stowOffset;
            bool ranged=equipment.OwnsHarpoon && !ball && !cuddling && releaseStroke==0 && displaySlot==0 && !displayedHeavy && !grip;
            var salvage=displayedHeavy?equipment.CarriedSalvage:null;
            var inventionStack=equipment.InventionInSlot(displaySlot);
            string heldKind=ball||cuddling||displayedHeavy||grip||panelPress||releaseStroke>0||inventionStack.count<=0
                ?null:inventionStack.kind;
            var caught=ball||cuddling||salvage!=null||heldKind!=null?null:displayedHeavy?equipment.CarriedFish:equipment.FishInSlot(displaySlot);
            long nextId=caught==null?0:displayedHeavy?equipment.HeldCargoId:equipment.CatchIdInSlot(displaySlot);
            UpdateFish(caught?displayedHeavy?caught.SpeciesId:equipment.CatchSpeciesInSlot(displaySlot):null,nextId);
            stride.Advance(actor,Time.deltaTime);
            float freedom=(1f-aim)*(1f-push);
            float breath=Mathf.Sin(Time.time*1.7f)*Mathf.Lerp(.0024f,.00035f,aim);
            Vector3 lower=stowOffset+Vector3.up*breath+new Vector3(.03f,-.29f,0)*monitorRest;
            float chargeWind=heldKind=="charge"?equipment.ChargeWind:0f;
            var inventionRest=heldKind=="charge"?new Vector3(.075f,-.227f,.432f):new Vector3(.155f,-.245f,.465f);
            var inventionPos=Vector3.Lerp(inventionRest,avatar.ballWind,chargeWind)
                +lower+stride.Offset*.37f;
            var inventionGrip=Quaternion.Euler(Vector3.Lerp(new Vector3(-8,heldKind=="charge"?-37:-14,-6),
                avatar.ballWindRoll,chargeWind));
            invention.Present(heldKind,inventionPos,inventionGrip);
            gun.localPosition+=lower+stride.Offset*freedom;
            gun.localRotation*=Quaternion.Euler(stride.Tilt*freedom+new Vector3(12*stow+15*monitorRest,0,-stow*8));
            Vector3 fishPos=new Vector3(displayedHeavy?0:.11f,displayedHeavy?-.245f:-.207f,displayedHeavy?.47f:.39f)+stride.Offset*.55f+lower;
            fishAnchor.localPosition=fishPos;
            fishAnchor.localRotation=Quaternion.Euler(-8,displayedHeavy?77:57,displayedHeavy?-8:-19);
            fishAnchor.localScale=Vector3.one*(displayedHeavy?.63f:.29f);
            Vector3 salvagePos=salvage==null?Vector3.zero:LootActor.CameraCenter+lower+stride.Offset*.35f;
            float salvageScale=avatar.salvageScale;
            UpdateSalvage(salvage,salvagePos,salvageScale);
            avatar.BeginPose();
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;var arm=arms[i];
                bool visible=!panelPress||s<0;
                (i==0?avatar.leftSleeve:avatar.rightSleeve).enabled=visible;
                if(!visible)continue;
                Vector3 wrist,angles;
                if(ranged)
                {
                    // Both textured gloves track the moving receiver, including its recoil and line tension.
                    Vector3 socket=s<0?avatar.supportWrist:avatar.triggerWrist;
                    wrist=gun.localPosition+gun.localRotation*socket;
                    angles=(gun.localRotation*Quaternion.Euler(s<0?avatar.supportAngles:avatar.triggerAngles)).eulerAngles;
                }
                else if(ball)
                {
                    if(s>0)
                    {
                        var roll=Quaternion.Slerp(Quaternion.identity,Quaternion.Euler(avatar.ballWindRoll),wind*(1f-cast));
                        wrist=BallCenter+roll*avatar.ballThrowWrist;
                        angles=(roll*Quaternion.Euler(avatar.ballThrowAngles)).eulerAngles;
                    }
                    else
                    {
                        wrist=Vector3.Lerp(BallCenter+Vector3.Lerp(avatar.ballSupportWrist,GoshaBallGrip.GloveOffset(s,1),wind),
                            new Vector3(-.22f,-.22f,.30f),cast);
                        angles=Quaternion.Slerp(Quaternion.Euler(avatar.ballSupportAngles),Quaternion.Euler(-22,32,48),wind).eulerAngles;
                    }
                }
                else if(releaseStroke>0 && s>0)
                {
                    float swing=1-releaseStroke/GoshaBallGrip.FollowThrough;
                    var start=avatar.ballRelease+avatar.ballThrowWrist;
                    wrist=Vector3.Lerp(start,avatar.restingWrist,Mathf.SmoothStep(0,1,swing))+
                        new Vector3(-.065f,-.026f,.044f)*Mathf.Sin(swing*Mathf.PI);
                    angles=Quaternion.Slerp(Quaternion.Euler(avatar.ballThrowAngles),Quaternion.Euler(8,-18,-35),swing).eulerAngles;
                }
                else if(heldKind!=null)
                {
                    wrist=inventionPos+inventionGrip*HeldInvention.Grip(heldKind,s);
                    angles=(inventionGrip*Quaternion.Euler(-12,-s*27,-s*57)).eulerAngles;
                    if(heldKind=="charge" && s<0)wrist=Vector3.Lerp(wrist,new Vector3(-.22f,-.22f,.30f),chargeWind);
                }
                else if(cuddling)
                {
                    float stroke=.5f+.5f*Mathf.Sin(Time.time*5.6f);
                    wrist=new Vector3(-.18f,-.31f,.44f);
                    if(s>0)wrist=Vector3.Lerp(new Vector3(.17f,-.23f,.435f),new Vector3(.06f,-.14f,.48f),stroke);
                    angles=new Vector3(s>0?16:-20,-s*25,-s*52);
                }
                else if(salvage!=null)
                {
                    var offset=avatar.salvageWristOffset;
                    offset.x*=s;
                    wrist=salvagePos+salvage.GloveOffset(s,Vector3.one*salvageScale)+offset;
                    angles=new Vector3(-18,-s*31,-s*62);
                }
                else if(caught!=null)
                {
                    float spread=displayedHeavy?.214f:.108f;
                    wrist=fishPos+new Vector3(s*spread,-(displayedHeavy?.044f:.034f),-.048f);
                    angles=new Vector3(-20,-s*31,-s*56);
                }
                else
                {
                    stride.FreeHand(s,avatar,out wrist,out angles);
                    wrist+=lower;
                    if(grip)wrist=Vector3.Lerp(wrist,new Vector3(s*.14f,-.14f,.46f),.85f);
                }
                if(s<0 && keyPress && heldKind==null && !cuddling && !displayedHeavy)
                {
                    var point=root.InverseTransformPoint(contact.position);
                    var facing=Quaternion.Inverse(root.rotation)*contact.rotation*Quaternion.Inverse(leftPalmFrame);
                    // If the diver turns away mid-stroke, the glove slips off and returns to its grip.
                    float weight=keyReach*Mathf.InverseLerp(.05f,.35f,point.normalized.z);
                    wrist=Vector3.Lerp(wrist,point-facing*leftPalmOffset,weight);
                    angles=Quaternion.Slerp(Quaternion.Euler(angles),facing,weight).eulerAngles;
                }
                else if(s<0&&push>0&&heldKind==null&&!ball&&!cuddling&&!displayedHeavy)
                {
                    wrist=Vector3.Lerp(wrist,new Vector3(-.059f,-.084f,.46f),push);
                    angles=Quaternion.Slerp(Quaternion.Euler(angles),Quaternion.Euler(-12,1,12),push).eulerAngles;
                }
                if(arm.Clavicle)
                {
                    var back=avatar.shoulderReturn;
                    back.x*=s;
                    arm.Clavicle.position=root.TransformPoint(back);
                }
                bool support=ranged && s<0;
                // Keep the support elbow tucked even between steps and while sighting.
                float carry=support ? Mathf.Lerp(.85f,1f,stride.Weight)*(1f-push)*(1f-keyReach) : 0f;
                BendArm(arm,s,wrist,support,carry);
                arm.Wrist.localRotation=Quaternion.Euler(angles);
                avatar.Arm(i).Drive(arm.Upper,arm.Sleeve,arm.Wrist,Quaternion.identity);
            }
            var motion=keyPress||push>0||panelPress?Action.PressPanel:releaseStroke>0?Action.Throw:switchTime>0?Action.SwapEquipment:
                cuddling?Action.PetGosha:ball?(wind>.01f?Action.WindThrow:Action.HoldBall):
                heldKind!=null?(chargeWind>.01f?Action.WindThrow:Action.CarryFish):
                salvage!=null?Action.CarrySalvage:caught!=null?Action.CarryFish:
                shot>0?Action.Shoot:grip||equipment.HookedFish!=null?Action.Reel:ranged?(aim>.1f?Action.Aim:Action.Harpoon):
                !actor.IsWalking?Action.Swim:stride.Running>.15f?Action.Sprint:
                stride.Moving?Action.Walk:Action.Idle;
            avatar.FinishPose(motion,dt);
            return ranged&&!panelPress;
        }

        void AnimateRaidGrip(float brace,float kick,float dt)
        {
            BallVisible=false;
            invention.Present(null,Vector3.zero,Quaternion.identity);
            UpdateFish(null,0);
            UpdateSalvage(null,Vector3.zero,1);
            avatar.BeginPose();
            for(int i=0;i<2;i++)
            {
                int side=i==0?-1:1;
                float strain=Mathf.Sin(Time.time*10.5f+i*1.7f);
                var wrist=new Vector3(side*(.20f+.065f*kick),-.20f+.071f*brace+.08f*kick+strain*.023f,
                    .33f+.18f*brace+.22f*kick+strain*.065f*(.38f+.62f*brace));
                (i==0?avatar.leftSleeve:avatar.rightSleeve).enabled=true;
                BendArm(arms[i],side,wrist,false,0);
                arms[i].Wrist.localRotation=Quaternion.Euler(-35-23*kick,side*(21-15*kick),-side*(32+23*kick));
                avatar.Arm(i).Drive(arms[i].Upper,arms[i].Sleeve,arms[i].Wrist,Quaternion.identity);
            }
            avatar.FinishPose(Action.Swim,dt);
        }

        void BendArm(Arm arm,int side,Vector3 target,bool support,float carry)
        {
            Vector3 shoulder=avatar.shoulderOrigin;
            shoulder.x*=side;
            shoulder+=avatar.movingSupportShoulder*carry;
            Vector3 reach=target-shoulder;
            Vector3 axis=reach.sqrMagnitude>.000001f?reach.normalized:Vector3.forward;
            float upper=arm.UpperLength, forearm=arm.ForearmLength;
            float maxReach=upper+forearm-.001f;
            float protraction=Mathf.Clamp(reach.magnitude-maxReach,0,avatar.shoulderTravel);
            shoulder+=axis*protraction;
            float distance=Mathf.Clamp(reach.magnitude-protraction,Mathf.Abs(upper-forearm)+.001f,maxReach);
            Vector3 wrist=shoulder+axis*distance;
            float along=(upper*upper-forearm*forearm+distance*distance)/(2f*distance);
            float bend=Mathf.Sqrt(Mathf.Max(0f,upper*upper-along*along));
            var restingPole=new Vector3(side*(support?.40f:.75f),support?-1.23f:-1f,-.22f);
            var elbowDirection=Vector3.ProjectOnPlane(Vector3.Lerp(restingPole,avatar.movingSupportPole,carry),axis).normalized;
            if (elbowDirection.sqrMagnitude<.01f) elbowDirection=Vector3.Cross(axis,Vector3.right).normalized;
            var elbow=shoulder+axis*along+elbowDirection*bend;
            PoseSleeve(arm.Upper,shoulder,elbow);
            PoseSleeve(arm.Sleeve,elbow,wrist);
            arm.Wrist.localPosition=wrist;
        }

        static void PoseSleeve(Transform sleeve,Vector3 start,Vector3 end)
        {
            Vector3 axis=end-start;
            sleeve.localPosition=start;
            sleeve.localRotation=Quaternion.LookRotation(axis,Vector3.up);

        }

        void UpdateSalvage(LootActor next,Vector3 at,float scale)
        {
            if(salvageSource!=next)
            {
                if(salvageModel!=null){salvageModel.gameObject.SetActive(false);Object.Destroy(salvageModel.gameObject);}
                salvageSource=next;
                salvageModel=next==null?null:next.CopyModel(root,next.DisplayName);
            }
            if(salvageModel==null)return;
            salvageModel.localPosition=at;
            salvageModel.localScale=Vector3.one*scale;
        }

        void UpdateFish(string next,long id)
        {
            if(next==species && id==catchId)return;
            species=next;catchId=id;
            if(fish!=null){fish.SetActive(false);Object.Destroy(fish);fish=null;}
            if(species!=null)
            {
                if(DefenseCreatureAssets.ReadKey(species,out var kind))fish=DefenseCreatureAssets.Model(kind,fishAnchor);
                if(!fish)fish=CatchFishModel.Create(fishAnchor,species,catchId);
            }
        }

        public void Release(){if(root!=null)Object.Destroy(root.gameObject);}
    }
}
