using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        public enum HuntStage : byte { Watching, Display, Pursuit, Recovery }
        public struct ReefSnapshot
        {
            public bool Active,Deep,ClamClosed;
            public Vector3 Eel,EelHeading,Sweep;
            public byte EelPhase,KrakenPhase;
        }
        static readonly Vector3 EelHome=new(41,-9,-29);
        public static readonly Vector3 ClamBed=new(29,-15.9f,-33),Rift=new(48,-14,-50);
        readonly SyncVar<ReefSnapshot> reefState=new();
        public ReefSnapshot Reef => reefState.Value;
        const float ThinkPeriod=.12f;
        float nextThink,eelCooldown,eelDelay,krakenCharge,krakenClock,anglerClock;
        Vector3 eelDecoy;
        DiverMotor clampedDiver,eelTarget,anglerTarget;
        float clamCooldown;
        NereidFauna reefView;
        void ResetReef()
        {
            reefState.Value=new ReefSnapshot{Eel=EelHome,EelHeading=Vector3.forward};
            clampedDiver=null;eelTarget=null;anglerTarget=null;
            nextThink=eelCooldown=eelDelay=anglerClock=clamCooldown=krakenClock=krakenCharge=0;
        }

        void TickReef(ref ThreatSnapshot main,float delta)
        {
            var w=RaidWorld.Instance;
            if(w==null || w.Team==null)
            {
                if(reefState.Value.Active)ResetReef();
                return;
            }
            nextThink+=delta;
            if(nextThink<ThinkPeriod)return;
            float dt=nextThink;nextThink=0;
            var reef=reefState.Value;
            reef.Active=true;reef.Deep=depthTier>1;
            bool winch=w.Team.hoistPhase==HoistPhase.Raising || w.Team.hoistPhase==HoistPhase.Returning;
            bool movingWinch=winch&&w.container!=null;
            clamCooldown-=dt;
            if(clampedDiver!=null)
            {
                if(!CanHunt(clampedDiver)||!clampedDiver.IsTrapped||Vector3.Distance(clampedDiver.transform.position,ClamBed)>3.4f)
                {clampedDiver=null;clamCooldown=5;}
                else
                {
                    clampedDiver.DamageOxygen(1.4f*dt);
                    // Solo still gets the timed escape built into the diver. In co-op, a teammate can pry it open with F.
                    if(DiverMotor.ServerDivers.Count>1)clampedDiver.Trap();
                }
            }
            else if(clamCooldown<=0)
            {
                var target=Nearest(ClamBed,1.7f);
                if(target!=null)
                {target.Trap();clampedDiver=target;main.Message="Донный капкан захлопнулся. Напарник может разжать створки (F).";}
            }
            reef.ClamClosed=clampedDiver!=null;
            if(reef.Deep)
            {
                TickEel(ref reef,dt,ref main);
                TickKraken(ref reef,dt,movingWinch,ref main);
            }
            reefState.Value=reef;
        }

        void TickAngler(ref ThreatSnapshot main,float dt)
        {
            if(main.PredatorHealth<=0)
            {
                main.AnglerStage=HuntStage.Watching;anglerTarget=null;
                respawn-=dt;
                if(respawn<=0){main.PredatorHealth=tuning.predatorHealth*Strength;main.PredatorPosition=tuning.predatorSpawn;}
                return;
            }
            predatorCooldown-=dt;
            if(predatorCooldown>0)
            {
                main.AnglerStage=HuntStage.Recovery;
                main.PredatorPosition=Swim(main.PredatorPosition,tuning.predatorSpawn,2.2f*dt,null);
                return;
            }
            if(anglerTarget!=null && (!CanHunt(anglerTarget) || Vector3.Distance(main.PredatorPosition,anglerTarget.transform.position)>14 ||
                !ClearTo(main.PredatorPosition,anglerTarget) || (anglerClock-=dt)<=0 && main.AnglerStage==HuntStage.Pursuit))
            {
                anglerTarget=null;main.AnglerStage=HuntStage.Recovery;predatorCooldown=1.6f;return;
            }
            if(anglerTarget==null)
            {
                main.AnglerStage=HuntStage.Watching;
                var bait=Nearest(main.PredatorPosition,4.8f);
                if(bait==null || !ClearTo(main.PredatorPosition,bait))
                {
                    main.PredatorPosition=Swim(main.PredatorPosition,tuning.predatorSpawn,1.2f*dt,null);return;
                }
                anglerTarget=bait;anglerClock=.88f;main.AnglerStage=HuntStage.Display;
            }
            var offset=anglerTarget.transform.position-main.PredatorPosition;
            if(offset.sqrMagnitude>.01f)main.AnglerHeading=offset.normalized;
            if(main.AnglerStage==HuntStage.Display)
            {
                if(anglerClock>0)return;
                main.AnglerStage=HuntStage.Pursuit;anglerClock=5.5f;
            }
            main.PredatorPosition=Swim(main.PredatorPosition,anglerTarget.transform.position,5.9f*dt,anglerTarget);
            if(Vector3.Distance(main.PredatorPosition,anglerTarget.transform.position)>1.35f)return;
            anglerTarget.DamageOxygen(tuning.attackDamage*Strength);
            var anglerInventory=anglerTarget.GetComponent<HarpoonTool>();
            if(anglerInventory!=null && !anglerInventory.ResistPocketTheft())anglerInventory.DropOneCatch();
            predatorCooldown=Mathf.Max(3,tuning.attackInterval);anglerTarget=null;
            main.AnglerStage=HuntStage.Recovery;main.Message="Фонарник атаковал из засады.";
        }

        public float NearbyDanger(Vector3 eye)
        {
            var r=reefState.Value;
            var main=state.Value;
            float nearest=main.WaveActive?Vector3.Distance(eye,main.WavePosition):float.PositiveInfinity;
            if(r.Active)
            {
                if(main.PredatorHealth>0)nearest=Mathf.Min(nearest,Vector3.Distance(eye,main.PredatorPosition));
                if(r.Deep)nearest=Mathf.Min(nearest,Vector3.Distance(eye,r.Eel));
                if(r.KrakenPhase!=0)nearest=Mathf.Min(nearest,Vector3.Distance(eye,r.Sweep));
            }
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,13,nearest));
        }

        void TickEel(ref ReefSnapshot reef,float dt,ref ThreatSnapshot main)
        {
            eelCooldown-=dt;eelDelay-=dt;
            if(eelTarget!=null)
            {
                var creel=eelTarget.GetComponent<HarpoonTool>();
                if(!CanHunt(eelTarget) || creel==null || !creel.HasFullCreel || Vector3.Distance(eelTarget.transform.position,reef.Eel)>28)
                {
                    eelDecoy=eelTarget.transform.position;eelTarget=null;eelCooldown=6;reef.EelPhase=3;
                }
            }
            if(eelTarget==null && eelCooldown<=0)
            {
                reef.EelPhase=0;
                float radius=18;
                foreach(var diver in DiverMotor.ServerDivers)
                {
                    if(!CanHunt(diver) || !diver.TryGetComponent<HarpoonTool>(out var tool) || !tool.HasFullCreel)continue;
                    float distance=Vector3.Distance(reef.Eel,diver.transform.position);
                    if(distance<radius){radius=distance;eelTarget=diver;}
                }
                if(eelTarget!=null){reef.EelPhase=1;eelDelay=1.45f;}
            }
            Vector3 goal=reef.EelPhase==3?eelDecoy:EelHome+Vector3.right*Mathf.Sin(Time.time*.22f)*2;
            float speed=1.75f;
            if(eelTarget!=null)
            {
                goal=eelTarget.transform.position;
                if(eelDelay<=0){reef.EelPhase=2;speed=6.2f;}else speed=.45f;
            }
            var step=Swim(reef.Eel,goal,speed*dt,eelTarget);
            if((step-reef.Eel).sqrMagnitude>.0001f)reef.EelHeading=(step-reef.Eel).normalized;
            reef.Eel=step;
            if(eelTarget!=null && reef.EelPhase==2 && Vector3.Distance(step,goal)<1.5f && ClearTo(step,eelTarget))
            {
                var eelInventory=eelTarget.GetComponent<HarpoonTool>();
                if(eelInventory!=null && !eelInventory.ResistPocketTheft())eelInventory.DropOneCatch();
                eelTarget.DamageOxygen(7);
                eelTarget=null;eelDecoy=goal;eelCooldown=7;reef.EelPhase=3;
                main.Message="Сетевик сорвал рыбу и отвлёкся на добычу.";
            }
        }

        void TickKraken(ref ReefSnapshot reef,float dt,bool winch,ref ThreatSnapshot main)
        {
            if(reef.KrakenPhase==0)
            {
                krakenCharge=winch?krakenCharge+dt:Mathf.Max(0,krakenCharge-dt*.45f);
                if(krakenCharge<2.7f)return;
                var target=Nearest(Rift,22);
                if(target==null)return;
                // Lock the strike area when the shadow appears. Swimming away or behind cover remains effective.
                reef.Sweep=target.transform.position;
                reef.KrakenPhase=1;krakenClock=2;krakenCharge=0;return;
            }
            krakenClock-=dt;
            if(krakenClock>0)return;
            if(reef.KrakenPhase==1)
            {
                foreach(var diver in DiverMotor.ServerDivers)
                    if(CanHunt(diver) && Vector3.Distance(diver.transform.position,reef.Sweep)<3.7f && ClearTo(Rift+Vector3.up*2,diver))
                    {diver.DamageOxygen(26);diver.ApplyExternalMotion((diver.transform.position-Rift).normalized*1.5f);main.Message="Кракен ударил в открытом проходе.";}
                reef.KrakenPhase=2;krakenClock=.85f;
            }
            else if(reef.KrakenPhase==2){reef.KrakenPhase=3;krakenClock=6;}
            else reef.KrakenPhase=0;
        }

        bool StrikeReef(Vector3 origin,Vector3 direction)
        {
            var reef=reefState.Value;
            if(!reef.Active)return false;
            if(reef.Deep&&Hits(origin,direction,reef.Eel)){eelTarget=null;eelDecoy=EelHome;eelCooldown=6;reef.EelPhase=3;reefState.Value=reef;return true;}
            return false;
        }

        static bool CanHunt(DiverMotor diver) => diver!=null && diver.IsServerInitialized && !diver.IsInside && !diver.IsDead;
        static DiverMotor Nearest(Vector3 point,float radius)
        {
            DiverMotor best=null;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!CanHunt(diver))continue;
                float distance=Vector3.Distance(point,diver.transform.position);
                if(distance<radius){radius=distance;best=diver;}
            }
            return best;
        }

        static bool ClearTo(Vector3 point,DiverMotor target) =>
            !Physics.Linecast(point,target.transform.position,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) || hit.transform.IsChildOf(target.transform);

        static Vector3 Swim(Vector3 pos,Vector3 goal,float distance,DiverMotor target)
        {
            var step=Vector3.MoveTowards(pos,goal,distance);
            if(!Physics.Linecast(pos,step,out var hit,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
               (target!=null && hit.transform.IsChildOf(target.transform)))return step;
            var slide=Vector3.ProjectOnPlane(step-pos,hit.normal);
            return slide.sqrMagnitude>.00001f && !Physics.Linecast(pos,pos+slide,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?pos+slide:pos;
        }

        void ShowReef()
        {
            if(reefView==null && reefState.Value.Active)
            {
                var group=new GameObject("Fishing-ground encounters");
                group.transform.SetParent(transform,true);
                reefView=group.AddComponent<NereidFauna>();reefView.Build(predatorView);
            }
            if(reefView!=null)
            {
                reefView.gameObject.SetActive(reefState.Value.Active);
                if(reefState.Value.Active)reefView.Present(reefState.Value);
            }
        }
    }
}
