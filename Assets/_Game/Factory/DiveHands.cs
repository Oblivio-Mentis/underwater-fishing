using UnityEngine;
using static Raid.DiveOutfit.Surface;

namespace Raid
{
    sealed class DiveHands
    {
        sealed class Arm
        {
            public Transform Sleeve,Wrist;
        }
        readonly DiveOutfit outfit;
        readonly Transform root,fishAnchor;
        readonly Arm[] arms=new Arm[2];
        string species;
        GameObject fish;
        DiverMotor viewed;
        int sequence,displaySlot,targetSlot;
        bool displayedHeavy,targetHeavy;
        float press,switchTime,clock;

        public DiveHands(HarpoonPresentation source,Transform camera)
        {
            outfit=new DiveOutfit(source);root=DiveOutfit.Pivot(camera,"Diver's hands",Vector3.zero);
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;var arm=arms[i]=new Arm();
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

        public void SetVisible(bool value){if(root!=null&&root.gameObject.activeSelf!=value)root.gameObject.SetActive(value);}

        public bool HasPanelPress(DiverMotor actor)
            => viewed==actor && (press>0||sequence!=actor.InteractionSequence);

        public bool Animate(DiverMotor actor,HarpoonTool equipment,Transform gun,float speed,float aim,float dt,bool panelPress)
        {
            if(viewed!=actor)
            {
                viewed=actor;sequence=actor.InteractionSequence;
                displaySlot=targetSlot=equipment.ActiveSlot;displayedHeavy=targetHeavy=equipment.HandsFull;
                press=switchTime=0;
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
            bool ranged=displaySlot==0&&!displayedHeavy;
            bool grip=equipment.AssistTarget!=null;
            var caught=displayedHeavy?equipment.CarriedFish:displaySlot>=2?equipment.FishInSlot(displaySlot):null;
            UpdateFish(caught!=null?caught.SpeciesId:null);
            float swim=actor.IsWalking?0:1;
            float stride=Mathf.Clamp01(speed*.35f);
            clock+=dt*Mathf.Lerp(2,actor.IsSprinting?12:8.5f,stride);
            float breath=Mathf.Sin(Time.time*1.7f)*.003f;
            Vector3 gait=new Vector3(Mathf.Sin(clock)*.011f,Mathf.Cos(clock*2)*.009f,0)*stride*(1-aim);
            Vector3 lower=new Vector3(0,-.20f*stow+breath,-.036f*stow);
            gun.localPosition+=lower+gait*.55f;
            gun.localRotation*=Quaternion.Euler(12*stow+stride*(actor.IsSprinting?6:1)*(1-aim),0,-stow*8);
            Vector3 fishPos=new Vector3(displayedHeavy?0:.11f,displayedHeavy?-.245f:-.207f,displayedHeavy?.67f:.51f)+gait+lower;
            fishAnchor.localPosition=fishPos;
            fishAnchor.localRotation=Quaternion.Euler(-8,displayedHeavy?77:57,displayedHeavy?-8:-19);
            fishAnchor.localScale=Vector3.one*(displayedHeavy?.63f:.29f);
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;var arm=arms[i];
                bool visible=panelPress?s<0:!ranged||s<0;
                arm.Wrist.gameObject.SetActive(visible);arm.Sleeve.gameObject.SetActive(visible);
                if(!visible)continue;
                var start=new Vector3(s*.325f,-.443f,.09f);
                Vector3 wrist,angles;
                if(ranged)
                {
                    wrist=gun.localPosition+gun.localRotation*new Vector3(-.038f,-.057f,.116f);
                    angles=new Vector3(-14,28,50);
                }
                else if(caught!=null)
                {
                    float spread=displayedHeavy?.214f:.108f;
                    wrist=fishPos+new Vector3(s*spread,-(displayedHeavy?.044f:.034f),-.048f);
                    angles=new Vector3(-20,-s*31,-s*56);
                }
                else
                {
                    float stroke=Mathf.Sin(clock+i*Mathf.PI)*stride;
                    wrist=new Vector3(s*(.24f+stroke*.021f),-.286f+stroke*.028f,.355f+stroke*(swim>.5f?.16f:.044f))+lower;
                    angles=new Vector3(8-stroke*14,-s*18,-s*(35+stroke*12));
                    if(grip)wrist=Vector3.Lerp(wrist,new Vector3(s*.14f,-.14f,.6f),.85f);
                }
                if(s<0&&push>0)
                {
                    wrist=Vector3.Lerp(wrist,new Vector3(-.059f,-.084f,.596f),push);
                    angles=Vector3.Lerp(angles,new Vector3(-12,1,12),push);
                }
                var axis=wrist-start;
                arm.Sleeve.localPosition=start;arm.Sleeve.localRotation=Quaternion.LookRotation(axis,Vector3.up);
                arm.Sleeve.localScale=new Vector3(1,1,axis.magnitude);
                arm.Wrist.localPosition=wrist;arm.Wrist.localRotation=Quaternion.Euler(angles);
            }
            return ranged&&!panelPress;
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
