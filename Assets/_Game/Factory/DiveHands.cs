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
        GameObject fish;
        DiverMotor viewed;
        int sequence,displaySlot,targetSlot;
        bool displayedHeavy,targetHeavy;
        float press,switchTime;

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
            if(!value)stride.Reset();
        }

        public bool HasPanelPress(DiverMotor actor)
            => viewed==actor && (press>0||sequence!=actor.InteractionSequence);

        public bool Animate(DiverMotor actor,HarpoonTool equipment,Transform gun,float aim,float dt,bool panelPress)
        {
            if(viewed!=actor)
            {
                viewed=actor;sequence=actor.InteractionSequence;
                displaySlot=targetSlot=equipment.ActiveSlot;displayedHeavy=targetHeavy=equipment.HandsFull;
                press=switchTime=0;
                stride.Reset();
            }
            if(targetSlot!=equipment.ActiveSlot||targetHeavy!=equipment.HandsFull)
            {
                targetSlot=equipment.ActiveSlot;targetHeavy=equipment.HandsFull;switchTime=.34f;
            }
            if(sequence!=actor.InteractionSequence){sequence=actor.InteractionSequence;press=.46f;}
            switchTime=Mathf.Max(0,switchTime-dt);press=Mathf.Max(0,press-dt);
            if(switchTime<.17f){displaySlot=targetSlot;displayedHeavy=targetHeavy;}
            float stow=Mathf.Sin(Mathf.Clamp01(switchTime/.34f)*Mathf.PI);
            float push=Mathf.Pow(Mathf.Max(0,Mathf.Sin(press/.46f*Mathf.PI)),.7f);
            bool grip=equipment.AssistTarget!=null;
            bool ranged=displaySlot==0 && !displayedHeavy && !grip;
            var caught=displayedHeavy?equipment.CarriedFish:displaySlot>=2?equipment.FishInSlot(displaySlot):null;
            UpdateFish(caught!=null?caught.SpeciesId:null);
            stride.Advance(actor,Time.deltaTime);
            float freedom=(1f-aim)*(1f-push);
            float breath=Mathf.Sin(Time.time*1.7f)*Mathf.Lerp(.0024f,.00035f,aim);
            Vector3 lower=new Vector3(0,-.20f*stow+breath,-.036f*stow);
            gun.localPosition+=lower+stride.Offset*freedom;
            gun.localRotation*=Quaternion.Euler(stride.Tilt*freedom+new Vector3(12*stow,0,-stow*8));
            Vector3 fishPos=new Vector3(displayedHeavy?0:.11f,displayedHeavy?-.245f:-.207f,displayedHeavy?.67f:.51f)+stride.Offset*.55f+lower;
            fishAnchor.localPosition=fishPos;
            fishAnchor.localRotation=Quaternion.Euler(-8,displayedHeavy?77:57,displayedHeavy?-8:-19);
            fishAnchor.localScale=Vector3.one*(displayedHeavy?.63f:.29f);
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
                if(s<0&&push>0)
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

        void UpdateFish(string next)
        {
            if(next==species)return;
            species=next;
            if(fish!=null){fish.SetActive(false);Object.Destroy(fish);fish=null;}
            if(species!=null)fish=CatchFishModel.Create(fishAnchor,species);
        }

        public void Release(){if(root!=null)Object.Destroy(root.gameObject);outfit.Release();}
    }
}
