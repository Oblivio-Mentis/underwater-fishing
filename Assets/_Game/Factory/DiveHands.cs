using UnityEngine;
using static Raid.DiveOutfit.Surface;

namespace Raid
{
    sealed class DiveHands
    {
        sealed class Arm
        {
            public Transform Upper,Sleeve,Wrist;
        }
        readonly DiveOutfit outfit;
        readonly FirstPersonStride stride = new FirstPersonStride();
        readonly Transform root,fishAnchor;
        readonly Arm[] arms=new Arm[2];
        string species;
        long catchId;
        GameObject fish;
        Transform salvageModel;
        LootActor salvageSource;
        internal bool BallVisible { get; private set; }
        internal Vector3 BallCenter { get; private set; }
        DiverMotor viewed;
        int sequence,displaySlot,targetSlot;
        bool displayedHeavy,targetHeavy;
        float press,switchTime,releaseStroke,shownWind;
        bool hadBall;

        public DiveHands(HarpoonPresentation source,Transform camera)
        {
            outfit=new DiveOutfit(source);root=DiveOutfit.Pivot(camera,"Diver's hands",Vector3.zero);
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;var arm=arms[i]=new Arm();
                arm.Upper=DiveOutfit.Pivot(root,s<0?"Left upper arm":"Right upper arm",Vector3.zero);
                outfit.Bind(arm.Upper);
                outfit.Oval(Fabric,new Vector3(0,0,.51f),new Vector3(.147f,.143f,1.05f));
                outfit.Oval(Rubber,new Vector3(0,0,.91f),new Vector3(.119f,.122f,.25f));
                arm.Sleeve=DiveOutfit.Pivot(root,s<0?"Left sleeve":"Right sleeve",Vector3.zero);
                outfit.Bind(arm.Sleeve);
                outfit.Oval(Fabric,new Vector3(0,0,.4f),new Vector3(.119f,.115f,1.03f));
                outfit.Pipe(Rubber,new Vector3(0,0,.70f),new Vector3(0,0,.94f),.097f);
                outfit.Pipe(Metal,new Vector3(0,0,.851f),new Vector3(0,0,.885f),.099f);
                if(s<0)
                {
                    outfit.Block(Metal,new Vector3(0,.044f,.67f),new Vector3(.061f,.025f,.17f));
                    outfit.Block(Glass,new Vector3(0,.059f,.67f),new Vector3(.048f,.009f,.12f));
                    for(int j=0;j<3;j++)outfit.Block(Lamp,new Vector3(-.012f+j*.012f,.065f,.668f),new Vector3(.006f,.003f,.055f));
                }
                arm.Wrist=DiveOutfit.Pivot(root,s<0?"Left glove":"Right glove",Vector3.zero);
                outfit.Glove(arm.Wrist,s,1.05f);
            }
            fishAnchor=DiveOutfit.Pivot(root,"Presented catch",Vector3.zero);
            outfit.Flush();
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
                viewed=actor;sequence=actor.InteractionSequence;
                displaySlot=targetSlot=equipment.ActiveSlot;displayedHeavy=targetHeavy=equipment.HandsFull;
                press=switchTime=releaseStroke=shownWind=0;hadBall=false;
                stride.Reset();
            }
            if(targetSlot!=equipment.ActiveSlot||targetHeavy!=equipment.HandsFull)
            {
                targetSlot=equipment.ActiveSlot;targetHeavy=equipment.HandsFull;switchTime=GoshaBallGrip.SwapDuration;
            }
            if(sequence!=actor.InteractionSequence){sequence=actor.InteractionSequence;press=.46f;}
            switchTime=Mathf.Max(0,switchTime-dt);press=Mathf.Max(0,press-dt);
            if(switchTime<GoshaBallGrip.SwapDuration*.5f){displaySlot=targetSlot;displayedHeavy=targetHeavy;}
            float stow=GoshaBallGrip.Stow(switchTime);
            float push=Mathf.Pow(Mathf.Max(0,Mathf.Sin(press/.46f*Mathf.PI)),.7f);
            bool grip=equipment.AssistTarget!=null;
            var world=RaidWorld.Instance;
            bool selectedBall=world?.GoshaBallInHands(actor)==true;
            if(hadBall && !selectedBall && world!=null && world.Gosha.Ball==GoshaBallMode.Flying &&
                world.Gosha.Thrower==actor.OwnerId)releaseStroke=.23f;
            hadBall=selectedBall;
            releaseStroke=Mathf.Max(0,releaseStroke-dt);
            bool cuddling=world?.IsHoldingGosha(actor)==true;
            bool ball=BallVisible=!cuddling && !displayedHeavy && world?.HasGoshaBall(actor)==true && equipment.HasBallInSlot(displaySlot);
            float wind=shownWind=ball ? selectedBall?GoshaBallGrip.Charge(actor):shownWind : 0;
            Vector3 stowOffset=GoshaBallGrip.Lower(stow);
            BallCenter=GoshaBallGrip.CameraCenter(wind)+stowOffset;
            bool ranged=equipment.OwnsHarpoon && !ball && !cuddling && releaseStroke==0 && displaySlot==0 && !displayedHeavy && !grip;
            var salvage=displayedHeavy?equipment.CarriedSalvage:null;
            var caught=ball||cuddling||salvage!=null?null:displayedHeavy?equipment.CarriedFish:equipment.FishInSlot(displaySlot);
            long nextId=caught==null?0:displayedHeavy?equipment.HeldCargoId:equipment.CatchIdInSlot(displaySlot);
            UpdateFish(caught?.SpeciesId,nextId);
            stride.Advance(actor,Time.deltaTime);
            float freedom=(1f-aim)*(1f-push);
            float breath=Mathf.Sin(Time.time*1.7f)*Mathf.Lerp(.0024f,.00035f,aim);
            Vector3 lower=stowOffset+Vector3.up*breath+new Vector3(.03f,-.29f,0)*monitorRest;
            gun.localPosition+=lower+stride.Offset*freedom;
            gun.localRotation*=Quaternion.Euler(stride.Tilt*freedom+new Vector3(12*stow+15*monitorRest,0,-stow*8));
            Vector3 fishPos=new Vector3(displayedHeavy?0:.11f,displayedHeavy?-.245f:-.207f,displayedHeavy?.67f:.51f)+stride.Offset*.55f+lower;
            fishAnchor.localPosition=fishPos;
            fishAnchor.localRotation=Quaternion.Euler(-8,displayedHeavy?77:57,displayedHeavy?-8:-19);
            fishAnchor.localScale=Vector3.one*(displayedHeavy?.63f:.29f);
            Vector3 salvagePos=salvage==null?Vector3.zero:LootActor.CameraCenter+lower+stride.Offset*.35f;
            const float salvageScale=.42f;
            UpdateSalvage(salvage,salvagePos,salvageScale);
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;var arm=arms[i];
                bool visible=!panelPress||s<0;
                arm.Upper.gameObject.SetActive(visible);arm.Sleeve.gameObject.SetActive(visible);
                arm.Wrist.gameObject.SetActive(visible&&(!ranged||s<0));
                if(!visible)continue;
                Vector3 wrist,angles;
                if(ranged)
                {
                    // The trigger glove is built around the receiver. Both sleeves follow their grips.
                    Vector3 socket=s<0?new Vector3(-.038f,-.057f,.116f):new Vector3(.055f,-.132f,-.097f);
                    wrist=gun.localPosition+gun.localRotation*socket;
                    angles=(gun.localRotation*Quaternion.Euler(-14,28,50)).eulerAngles;
                }
                else if(ball)
                {
                    wrist=BallCenter+GoshaBallGrip.GloveOffset(s,wind);
                    angles=new Vector3(-22,-s*32,-s*48);
                }
                else if(releaseStroke>0 && s>0)
                {
                    float swing=1-releaseStroke/.23f;
                    wrist=Vector3.Lerp(new Vector3(.28f,-.08f,.36f),new Vector3(.09f,-.16f,.70f),Mathf.Sin(swing*Mathf.PI*.65f));
                    angles=new Vector3(-8,-17,-54);
                }
                else if(cuddling)
                {
                    float stroke=.5f+.5f*Mathf.Sin(Time.time*5.6f);
                    wrist=new Vector3(-.19f,-.28f,.76f);
                    if(s>0)wrist=Vector3.Lerp(new Vector3(.18f,-.16f,.73f),new Vector3(.065f,-.055f,.81f),stroke);
                    angles=new Vector3(s>0?16:-20,-s*25,-s*52);
                }
                else if(salvage!=null)
                {
                    wrist=salvagePos+salvage.GloveOffset(s,Vector3.one*salvageScale);
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
                    stride.FreeHand(s,out var offset,out var tilt);
                    wrist=new Vector3(s*.24f,-.286f,.355f)+lower+offset;
                    angles=new Vector3(8,-s*18,-s*35)+tilt;
                    if(grip)wrist=Vector3.Lerp(wrist,new Vector3(s*.14f,-.14f,.6f),.85f);
                }
                if(s<0&&push>0&&!ball&&!cuddling&&!displayedHeavy)
                {
                    wrist=Vector3.Lerp(wrist,new Vector3(-.059f,-.084f,.596f),push);
                    angles=Quaternion.Slerp(Quaternion.Euler(angles),Quaternion.Euler(-12,1,12),push).eulerAngles;
                }
                BendArm(arm,s,wrist);
                arm.Wrist.localPosition=wrist;arm.Wrist.localRotation=Quaternion.Euler(angles);
            }
            return ranged&&!panelPress;
        }

        static void BendArm(Arm arm,int side,Vector3 wrist)
        {
            var shoulder=new Vector3(side*.22f,-.32f,-.065f);
            var reach=wrist-shoulder;
            float distance=Mathf.Max(.001f,reach.magnitude);
            Vector3 axis=reach/distance;
            // Keep the forearm, cuff and wrist computer at a constant length through the stride.
            const float forearm=.37f;
            float upper=Mathf.Max(.46f,distance-forearm+.012f);
            float along=(upper*upper-forearm*forearm+distance*distance)/(2f*distance);
            float bend=Mathf.Sqrt(Mathf.Max(0f,upper*upper-along*along));
            var elbowDirection=Vector3.ProjectOnPlane(new Vector3(side*.75f,-1f,-.22f),axis).normalized;
            var elbow=shoulder+axis*along+elbowDirection*bend;
            PoseSleeve(arm.Upper,shoulder,elbow);
            PoseSleeve(arm.Sleeve,elbow,wrist);
        }

        static void PoseSleeve(Transform sleeve,Vector3 start,Vector3 end)
        {
            Vector3 axis=end-start;
            sleeve.localPosition=start;
            sleeve.localRotation=Quaternion.LookRotation(axis,Vector3.up);
            sleeve.localScale=new Vector3(1,1,axis.magnitude);
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
            if(species!=null)fish=CatchFishModel.Create(fishAnchor,species,catchId);
        }

        public void Release(){if(root!=null)Object.Destroy(root.gameObject);outfit.Release();}
    }
}
