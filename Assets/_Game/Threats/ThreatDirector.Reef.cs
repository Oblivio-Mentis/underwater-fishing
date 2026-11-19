using System.Collections.Generic;
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
            public Vector3 Clicker,ClickerHeading,Eel,EelHeading,Sweep;
            public HuntStage ClickerStage;
            public byte EelPhase,KrakenPhase;
        }
        static readonly Vector3 ClickerHome=new(36,-11,-37),EelHome=new(41,-9,-29);
        public static readonly Vector3 ClamBed=new(29,-15.9f,-33),Rift=new(48,-14,-50);
        readonly SyncVar<ReefSnapshot> reefState=new();
        public ReefSnapshot Reef => reefState.Value;
        const float ThinkPeriod=.12f;
        float nextThink,clickerCooldown,eelCooldown,eelDelay,krakenCharge,krakenClock,anglerClock;
        Vector3 clickerGoal,eelDecoy;
        DiverMotor clampedDiver,eelTarget,anglerTarget;
        float clickerWarn;
        float clamCooldown;
        NereidFauna reefView;
        sealed class Hearing
        {
            public Vector3 lastPosition,noiseAt;
            public float noiseLeft,lastSpeed;
            public bool hadCargo;
        }
        readonly Dictionary<DiverMotor,Hearing> sounds=new();
        readonly List<DiverMotor> staleSounds=new();

        void ResetReef()
        {
            reefState.Value=new ReefSnapshot{Clicker=ClickerHome,Eel=EelHome,EelHeading=Vector3.forward};
            sounds.Clear();clampedDiver=null;eelTarget=null;anglerTarget=null;clickerWarn=0;
            clickerGoal=ClickerHome;nextThink=clickerCooldown=eelCooldown=eelDelay=anglerClock=clamCooldown=krakenClock=krakenCharge=0;
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
            Vector3 noise=movingWinch?w.container.position:ClickerHome;
            float best=movingWinch?27:0;
            DiverMotor noisy=null;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!CanHunt(diver))continue;
                if(!sounds.TryGetValue(diver,out var sound))
                {
                    sound=new Hearing{lastPosition=diver.transform.position};
                    sounds.Add(diver,sound);
                }
                var pos=diver.transform.position;
                float speed=Vector3.Distance(sound.lastPosition,pos)/dt;
                var tool=diver.GetComponent<HarpoonTool>();
                bool struggle=tool!=null&&tool.HookedFish!=null&&tool.Reeling;
                bool cargo=tool!=null&&tool.CarriedFish!=null;
                bool drop=sound.hadCargo&&!cargo;
                if(struggle || drop || speed>4.7f || (speed>2.8f && speed-sound.lastSpeed>2.2f))
                {
                    sound.noiseLeft=drop?5f:2.6f;
                    sound.noiseAt=pos;
                }
                sound.lastSpeed=speed;sound.lastPosition=pos;sound.hadCargo=cargo;
                sound.noiseLeft-=dt;
                float intensity=32-Vector3.Distance(sound.noiseAt,reef.Clicker)+(struggle?6:0);
                if(sound.noiseLeft>0 && intensity>best){best=intensity;noise=sound.noiseAt;noisy=diver;}
            }
            staleSounds.Clear();
            foreach(var entry in sounds)if(!CanHunt(entry.Key))staleSounds.Add(entry.Key);
            foreach(var diver in staleSounds)sounds.Remove(diver);
            clickerCooldown-=dt;
            clickerGoal=best>0?noise:ClickerHome+new Vector3(Mathf.Sin(Time.time*.14f)*2.4f,0,Mathf.Cos(Time.time*.14f)*2.4f);
            if(clickerCooldown<=0)
            {
                if(best<=0)reef.ClickerStage=HuntStage.Watching;
                else if(reef.ClickerStage is HuntStage.Watching or HuntStage.Recovery)
                {reef.ClickerStage=HuntStage.Display;clickerWarn=.66f;}
                else if(reef.ClickerStage==HuntStage.Display && (clickerWarn-=dt)<=0)reef.ClickerStage=HuntStage.Pursuit;
                var aim=clickerGoal-reef.Clicker;
                if(aim.sqrMagnitude>.05f)reef.ClickerHeading=aim.normalized;
                float pace=reef.ClickerStage==HuntStage.Display?.16f:best>0?4.25f:1.15f;
                reef.Clicker=Swim(reef.Clicker,clickerGoal,pace*dt,noisy);
                if(reef.ClickerStage==HuntStage.Pursuit && noisy!=null && Vector3.Distance(noisy.transform.position,reef.Clicker)<1.8f && ClearTo(reef.Clicker,noisy))
                {noisy.DamageOxygen(14);clickerCooldown=4;reef.ClickerStage=HuntStage.Recovery;main.Message="Щелкун ударил по источнику шума.";}
            }

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
            anglerTarget.GetComponent<HarpoonTool>()?.DropOneCatch();
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
                nearest=Mathf.Min(nearest,Vector3.Distance(eye,r.Clicker));
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
                eelTarget.GetComponent<HarpoonTool>().DropOneCatch();eelTarget.DamageOxygen(7);
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
            if(Hits(origin,direction,reef.Clicker)){clickerCooldown=4.5f;return true;}
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
