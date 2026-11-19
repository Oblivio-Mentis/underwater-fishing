using UnityEngine;
using static Raid.DiveOutfit.Surface;

namespace Raid
{
    // Observer rig driven solely by the replicated player transform, equipment and accepted actions.
    [DisallowMultipleComponent,DefaultExecutionOrder(260)]
    public sealed class DiverSuitVisual : MonoBehaviour
    {
        sealed class Limb { public Transform Upper,Lower,End; }
        readonly Limb[] arms=new Limb[2],legs=new Limb[2];
        DiveOutfit outfit;
        DiverMotor diver;
        HarpoonTool tool;
        Transform body,head,gun,fishAnchor;
        GameObject fishModel;
        string fishSpecies;
        int shotSequence,useSequence,previousSlot;
        float stride,speed,swimming,kick,reach,draw,movingPhase;
        Vector3 previous;
        Renderer legacyVisor,legacyCapsule;

        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);

        public void Initialize(HarpoonPresentation source)
        {
            diver=GetComponent<DiverMotor>();tool=GetComponent<HarpoonTool>();
            legacyVisor=transform.Find("Visor")?.GetComponent<Renderer>();
            legacyCapsule=transform.Find("Diver body")?.GetComponent<Renderer>();
            if(legacyVisor!=null)legacyVisor.enabled=false;if(legacyCapsule!=null)legacyCapsule.enabled=false;
            outfit=new DiveOutfit(source);
            body=DiveOutfit.Pivot(transform,"Expedition suit",Vector3.zero);
            outfit.Bind(body);
            outfit.Oval(Rubber,V(0,.18f,-.005f),V(.42f,.62f,.315f));
            outfit.Oval(Fabric,V(0,.252f,.023f),V(.442f,.515f,.345f));
            outfit.Block(Rubber,V(0,.253f,.17f),V(.269f,.27f,.064f),V(5,0,0));
            outfit.Block(Metal,V(0,.24f,.201f),V(.20f,.168f,.033f));
            outfit.Block(Glass,V(-.016f,.258f,.224f),V(.117f,.062f,.013f));
            outfit.Block(Lamp,V(-.024f,.252f,.233f),V(.074f,.009f,.008f));
            outfit.Oval(Rubber,V(0,-.092f,-.008f),V(.36f,.284f,.292f));
            outfit.Block(Rubber,V(0,-.008f,0),V(.367f,.089f,.3f));
            outfit.Block(Metal,V(0,-.01f,.15f),V(.09f,.055f,.02f));
            outfit.Block(Rubber,V(0,.245f,-.17f),V(.34f,.46f,.17f));
            for(int s=-1;s<=1;s+=2)
            {
                outfit.Oval(Enamel,V(s*.107f,.226f,-.27f),V(.177f,.443f,.178f));
                outfit.Pipe(Metal,V(s*.107f,.10f,-.271f),V(s*.107f,.14f,-.271f),.180f);
                outfit.Pipe(Rubber,V(s*.107f,.32f,-.271f),V(s*.107f,.356f,-.271f),.176f);
                outfit.Pipe(Metal,V(s*.107f,.423f,-.271f),V(s*.107f,.474f,-.271f),.056f);
                outfit.Block(Rubber,V(s*.132f,.25f,.145f),V(.045f,.31f,.041f),V(6,0,-s*7));
                outfit.Block(Enamel,V(s*.134f,.207f,.171f),V(.042f,.044f,.014f));
                outfit.Block(Fabric,V(s*.14f,-.03f,.13f),V(.078f,.094f,.068f));
            }
            outfit.Pipe(Rubber,V(.08f,.44f,-.25f),V(.196f,.48f,-.09f),.030f);
            outfit.Pipe(Rubber,V(.196f,.48f,-.09f),V(.183f,.414f,.096f),.032f);
            outfit.Pipe(Rubber,V(.183f,.414f,.096f),V(.087f,.469f,.127f),.030f);
            head=DiveOutfit.Pivot(body,"Pressure helmet",V(0,.49f,0));outfit.Bind(head);
            outfit.Pipe(Metal,V(0,-.059f,0),V(0,-.020f,0),.277f);
            outfit.Oval(Enamel,V(0,.09f,0),V(.336f,.331f,.324f));
            outfit.Oval(Rubber,V(0,.097f,.119f),V(.31f,.212f,.106f));
            outfit.Oval(Glass,V(0,.106f,.154f),V(.27f,.153f,.074f));
            outfit.Block(Metal,V(0,.009f,.145f),V(.09f,.05f,.057f));
            for(int i=-1;i<=1;i++)outfit.Block(Rubber,V(i*.022f,.006f,.175f),V(.008f,.027f,.005f));
            for(int s=-1;s<=1;s+=2)
            {
                outfit.Pipe(Metal,V(s*.145f,.099f,.002f),V(s*.177f,.099f,.002f),.092f);
                outfit.Pipe(Rubber,V(s*.111f,.232f,.038f),V(s*.111f,.232f,.102f),.052f);
                outfit.Pipe(Lamp,V(s*.111f,.232f,.099f),V(s*.111f,.232f,.108f),.043f);
            }
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;
                var arm=arms[i]=Chain("Arm "+s,V(s*.231f,.362f,0),.246f,.233f);
                outfit.Bind(arm.Upper);
                outfit.Oval(Fabric,V(0,-.105f,0),V(.14f,.257f,.15f));
                outfit.Oval(Enamel,V(s*.012f,-.026f,0),V(.163f,.11f,.167f));
                outfit.Pipe(Rubber,V(0,-.202f,0),V(0,-.246f,0),.113f);
                outfit.Bind(arm.Lower);
                outfit.Oval(Fabric,V(0,-.093f,0),V(.125f,.24f,.127f));
                outfit.Block(Metal,V(s*.051f,-.098f,-.006f),V(.033f,.101f,.084f));
                if(s<0)outfit.Block(Lamp,V(s*.066f,-.092f,-.006f),V(.01f,.064f,.04f));
                outfit.Pipe(Rubber,V(0,-.174f,0),V(0,-.219f,0),.108f);
                outfit.Glove(arm.End,s,1.12f);
                var leg=legs[i]=Chain("Leg "+s,V(s*.106f,-.153f,0),.343f,.328f);
                outfit.Bind(leg.Upper);outfit.Oval(Rubber,V(0,-.141f,0),V(.176f,.348f,.176f));
                outfit.Block(Fabric,V(s*.028f,-.134f,.045f),V(.134f,.194f,.117f));
                outfit.Oval(Metal,V(0,-.312f,.052f),V(.118f,.121f,.092f));
                outfit.Bind(leg.Lower);
                outfit.Oval(Rubber,V(0,-.14f,0),V(.146f,.312f,.15f));
                outfit.Pipe(Fabric,V(0,-.1f,0),V(0,-.16f,0),.144f);
                outfit.Bind(leg.End);
                outfit.Oval(Rubber,V(0,.018f,.035f),V(.143f,.118f,.234f));
                outfit.Blade(Fabric,V(0,-.012f,.168f),V(.15f,.037f,.20f),V(-4,0,0));
            }
            gun=DiveOutfit.Pivot(arms[1].End,"Service harpoon",V(0,.05f,.08f));outfit.Bind(gun);
            outfit.Block(Metal,V(0,.025f,0),V(.087f,.083f,.22f));
            outfit.Block(Enamel,V(0,-.042f,-.037f),V(.056f,.111f,.046f),V(-13,0,0));
            outfit.Pipe(Rubber,V(0,.042f,.077f),V(0,.042f,.32f),.033f);
            var bolt=DiveOutfit.Pivot(gun,"Loaded service spear",Vector3.zero);
            outfit.Bind(bolt);
            outfit.Pipe(Metal,V(0,.025f,.23f),V(0,.025f,.449f),.010f);
            outfit.Blade(Metal,V(0,.025f,.445f),V(.041f,.009f,.073f),V(0,180,0));
            var reel=DiveOutfit.Pivot(gun,"Line drum",V(-.08f,.018f,-.051f));
            outfit.Bind(reel);
            outfit.Pipe(Enamel,V(-.029f,0,0),V(.029f,0,0),.104f);
            outfit.Pipe(Metal,V(-.036f,0,0),V(-.029f,0,0),.108f);
            outfit.Pipe(Rubber,V(-.038f,0,0),V(-.039f,.039f,0),.009f);
            outfit.Pipe(Enamel,V(-.039f,.039f,0),V(-.055f,.039f,0),.012f);
            outfit.Flush();
            if(!diver.IsOwner)
            {
                var muzzle=DiveOutfit.Pivot(gun,"Service muzzle",V(0,.025f,.315f));
                var fx=DiveOutfit.Pivot(body,"Observed harpoon cable",Vector3.zero).gameObject.AddComponent<HarpoonShotFx>();
                fx.Initialize(tool,muzzle,bolt,reel,source.steel,true);
            }
            fishAnchor=DiveOutfit.Pivot(arms[1].End,"Pocket catch",V(-.032f,.058f,.06f));
            fishAnchor.localRotation=Quaternion.Euler(0,-65,-6);
            fishAnchor.localScale=Vector3.one*.27f;
            previous=transform.position;shotSequence=tool.ShotSequence;useSequence=diver.InteractionSequence;previousSlot=tool.ActiveSlot;
            body.gameObject.SetActive(false);
        }

        Limb Chain(string label,Vector3 start,float top,float bottom)
        {
            var limb=new Limb();
            limb.Upper=DiveOutfit.Pivot(body,label,start);
            limb.Lower=DiveOutfit.Pivot(limb.Upper,label+" hinge",Vector3.down*top);
            limb.End=DiveOutfit.Pivot(limb.Lower,label+" extremity",Vector3.down*bottom);
            return limb;
        }

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
            float rawSpeed=dt>.0001f?Vector3.Distance(transform.position,previous)/dt:0;previous=transform.position;
            if(tool.ShotSequence!=shotSequence){kick=1;shotSequence=tool.ShotSequence;}
            if(diver.InteractionSequence!=useSequence){reach=1;useSequence=diver.InteractionSequence;}
            if(tool.ActiveSlot!=previousSlot){draw=1;previousSlot=tool.ActiveSlot;}
            if(!visible)return;
            float blend=1-Mathf.Exp(-10*dt);
            speed=Mathf.Lerp(speed,Mathf.Min(rawSpeed,8),blend);stride=Mathf.Lerp(stride,Mathf.Clamp01(speed*.44f),blend);
            swimming=Mathf.Lerp(swimming,diver.IsWalking?0:1,blend);
            kick=Mathf.MoveTowards(kick,0,dt*5.3f);reach=Mathf.MoveTowards(reach,0,dt*2.1f);draw=Mathf.MoveTowards(draw,0,dt*3.2f);
            movingPhase+=dt*Mathf.Lerp(3.1f,diver.IsSprinting?12.3f:9.1f,stride);
            float beat=Mathf.Sin(movingPhase);
            float idle=Mathf.Sin(Time.time*1.85f+diver.OwnerId*.7f);
            bool held=tool.HandsFull,helper=tool.AssistTarget!=null;
            var pocket=tool.ActiveSlot>=2?tool.FishInSlot(tool.ActiveSlot):null;
            gun.gameObject.SetActive(!diver.IsDead&&!held&&tool.ActiveSlot==0);
            UpdateFish(pocket!=null&&!held&&!diver.IsDead?pocket.SpeciesId:null);
            if(diver.IsDead)
            {
                body.localPosition=Vector3.zero;body.localRotation=Quaternion.identity;
                head.localRotation=Quaternion.Euler(13,4,-9);
                for(int i=0;i<2;i++){int s=i==0?-1:1;Pose(arms[i],-11,0,s*(22+idle*2),-17,0,81);Pose(legs[i],s*7,0,s*6,9,0,-6);}return;
            }
            float lean=Mathf.Lerp(diver.IsSprinting?9:1,Mathf.Lerp(32,71,stride),swimming);
            body.localRotation=Quaternion.Euler(lean,0,beat*stride*(1-swimming)*2.5f);
            body.localPosition=V(0,(1-swimming)*Mathf.Abs(beat)*.027f*stride+idle*.006f,0);
            head.localRotation=Quaternion.Euler(Mathf.Clamp(diver.ViewPitch-lean*.68f,-48,46),0,0);
            for(int i=0;i<2;i++)
            {
                int s=i==0?-1:1;float cycle=s*beat;
                float thigh=Mathf.Lerp(-cycle*(diver.IsSprinting?42:30)*stride,-cycle*(12+23*stride),swimming);
                float knee=Mathf.Lerp(Mathf.Max(0,cycle)*40*stride,12+Mathf.Max(0,cycle)*38*stride,swimming);
                Pose(legs[i],thigh,0,s*4,knee,0,-knee*.48f+swimming*18);
                float shoulder=cycle*stride*27,elbow=-18-Mathf.Max(0,cycle)*17*stride;
                float roll=s*7,yaw=0,wrist=83;
                if(held||helper)
                {
                    shoulder=helper?-73:-42;elbow=helper?-13:-51;roll=s*(helper?4:19);yaw=-s*12;
                }
                else if(gun.gameObject.activeSelf||pocket!=null)
                {
                    shoulder=s>0?-48:-39;elbow=s>0?-40:-57;yaw=-s*(s>0?8:24);roll=s*(s>0?5:8);
                    shoulder-=15*kick;shoulder+=Mathf.Sin(draw*Mathf.PI)*30;
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
        }

        void UpdateFish(string species)
        {
            if(species==fishSpecies)return;
            fishSpecies=species;if(fishModel!=null){fishModel.SetActive(false);Destroy(fishModel);fishModel=null;}
            if(species!=null)fishModel=CatchFishModel.Create(fishAnchor,species);
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
            outfit?.Release();
        }
    }
}
