using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        // Watching = patrol. Investigate/Circle/Overshoot were added for the full hunt; Display/Pursuit/Recovery keep their meaning.
        public enum HuntStage : byte { Watching, Display, Pursuit, Recovery, Investigate, Circle, Overshoot }
        [System.Serializable]
        public struct ReefSnapshot
        {
            public bool Active,Deep,ClamClosed,HasClam,HasKraken,HasEel;
            public Vector3 Eel,EelHeading,Sweep,Clam,Kraken;
            public byte EelPhase,KrakenPhase;
        }
        internal static readonly Vector3[] DefaultEelPatrol = {new(45,-8.5f,-36),new(48,-8.8f,-43),new(43,-8,-45),new(38,-8.3f,-34),new(41,-9,-29)};
        int EelPatrolCount=>SpawnLayout?.eelPatrol is {Length:>0} patrol?patrol.Length:DefaultEelPatrol.Length;
        Vector3 EelPoint(int index)
        {
            var marker=ExpeditionLayout.At(SpawnLayout?.eelPatrol,index);
            return marker?marker.position:DefaultEelPatrol[Mathf.Clamp(index,0,DefaultEelPatrol.Length-1)];
        }
        readonly RaycastHit[] huntHits = new RaycastHit[16];
        public static Vector3 ClamBed => Instance && Instance.Reef.HasClam ? Instance.Reef.Clam : Instance && Instance.tuning ? Instance.ClamArrival : new Vector3(29,-15.9f,-33);
        public static Vector3 Rift => Instance && Instance.Reef.HasKraken ? Instance.Reef.Kraken : Instance && Instance.tuning ? Instance.KrakenArrival : new Vector3(48,-14,-50);
        readonly SyncVar<ReefSnapshot> reefState=new();
        public ReefSnapshot Reef => reefState.Value;
        const float ThinkPeriod=.12f;
        float reefArrivalAt, nextThink,eelCooldown,eelDelay,krakenCharge,krakenClock;
        Vector3 eelDecoy, eelSeen, eelPreviousTarget;
        float eelBlindTime, eelRouteAt;
        int eelWaypoint;
        DiverMotor clampedDiver,eelTarget;
        float clamCooldown;
        NereidFauna reefView;
        void ResetReef()
        {
            reefState.Value=default;
            clampedDiver=null;eelTarget=null;ResetAngler();
            nextThink=eelCooldown=eelDelay=clamCooldown=krakenClock=krakenCharge=0;
            reefArrivalAt=0; eelBlindTime=eelRouteAt=0;
            eelWaypoint=0;
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
            reef.Active = true;
            reef.Deep = depthTier > 1;
            bool seekClam=SpawnLayout?.clamEnabled!=false && !reef.HasClam;
            bool seekEel=(SpawnLayout?.eelEnabled??reef.Deep) && !reef.HasEel;
            bool seekKraken=(SpawnLayout?.krakenEnabled??reef.Deep) && !reef.HasKraken;
            if (Time.time >= reefArrivalAt && (seekClam || seekEel || seekKraken))
            {
                reefArrivalAt = Time.time + 1;
                if (seekClam) reef.HasClam = TryBottomSite(ClamArrival, tuning.clamArrivalRadius, 1.15f, out reef.Clam);
                if (seekKraken) reef.HasKraken = TryBottomSite(KrakenArrival, tuning.krakenArrivalRadius, 1.8f, out reef.Kraken);
                if (seekEel && Time.time >= nextCagedEelArrival) reef.HasEel = TryEelEntry(ref reef);
            }
            bool winch=w.Team.hoistPhase==HoistPhase.Raising || w.Team.hoistPhase==HoistPhase.Returning;
            bool movingWinch=winch&&w.container!=null;
            clamCooldown-=dt;
            if(clampedDiver!=null)
            {
                if(!CanHunt(clampedDiver)||!clampedDiver.IsTrapped||Vector3.Distance(clampedDiver.transform.position,reef.Clam)>3.4f)
                {clampedDiver=null;clamCooldown=5;}
                else
                {
                    clampedDiver.DamageFromCreature(1.4f*dt);
                    // Solo still gets the timed escape built into the diver. In co-op, a teammate can pry it open with F.
                    if(DiverMotor.ServerDivers.Count>1)clampedDiver.Trap();
                }
            }
            else if(reef.HasClam && clamCooldown<=0)
            {
                var target=Nearest(reef.Clam,1.7f);
                if(target!=null)
                {target.Trap();clampedDiver=target;main.Message="Донный капкан захлопнулся. Напарник может разжать створки (F).";}
            }
            reef.ClamClosed=clampedDiver!=null;
            if (reef.HasEel) TickEel(ref reef,dt,ref main);
            if (reef.HasKraken) TickKraken(ref reef,dt,movingWinch,ref main);
            reefState.Value=reef;
        }

        bool TryEelEntry(ref ReefSnapshot reef)
        {
            int start = anglerRandom.Next(EelPatrolCount);
            for (int i = 0; i < 20; i++)
            {
                int from = (start + i) % EelPatrolCount;
                int to = (from + 1) % EelPatrolCount;
                var point = Vector3.Lerp(EelPoint(from), EelPoint(to), Rand(.05f, .95f)) +
                            MarineSpawn.Scatter(anglerRandom, tuning.eelArrivalRadius) + Vector3.up * Rand(-1.1f,1.1f);
                if (!MarineSpawn.InOpenWater(depthTier, point, .65f) ||
                    !ReefHabitats.CanArrive(point, 9, 14) || !MarineSpawn.ClearLeg(point, EelPoint(to), .45f, 2.7f)) continue;
                reef.Eel = point;
                reef.EelHeading = (EelPoint(to) - point).normalized;
                eelWaypoint = to;
                return true;
            }
            return false;
        }

        bool TryBottomSite(Vector3 anchor, float spread, float radius, out Vector3 site)
        {
            site = default;
            for (int i = 0; i < 20; i++)
            {
                var candidate = anchor + MarineSpawn.Scatter(anglerRandom, spread);
                if (!MarineSpawn.Floor(depthTier, candidate, out var floor) || floor.normal.y < .88f) continue;
                var centre = floor.point + Vector3.up * (radius + .25f);
                if (!ReefHabitats.CanArrive(centre, 7, 12) || FishActor.InCage(centre) ||
                    Physics.CheckSphere(centre, radius, obstacleMask, QueryTriggerInteraction.Ignore)) continue;
                site = floor.point;
                return true;
            }
            return false;
        }

        public float NearbyDanger(Vector3 eye)
        {
            var r=reefState.Value;
            var main=state.Value;
            float nearest=main.WaveActive && !main.WaveAwaitingEntry?Vector3.Distance(eye,main.WavePosition):float.PositiveInfinity;
            if(r.Active)
            {
                if(main.PredatorHealth>0)nearest=Mathf.Min(nearest,Vector3.Distance(eye,main.PredatorPosition));
                if(r.HasEel)nearest=Mathf.Min(nearest,Vector3.Distance(eye,r.Eel));
                if(r.KrakenPhase!=0)nearest=Mathf.Min(nearest,Vector3.Distance(eye,r.Sweep));
            }
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2,13,nearest));
        }

        void TickEel(ref ReefSnapshot reef,float dt,ref ThreatSnapshot main)
        {
            eelCooldown -= dt;
            if (reef.EelPhase == 3 && eelCooldown <= 0) reef.EelPhase = 0;
            if (reef.EelPhase == 0 && eelCooldown <= 0)
            {
                eelTarget = NearestVisible(reef.Eel,15,needsFish:true);
                if (eelTarget != null)
                {
                    eelDelay = 1.38f;
                    eelBlindTime = 0;
                    eelSeen = eelPreviousTarget = BodyPoint(eelTarget);
                    reef.EelPhase = 1;
                }
                else eelCooldown = .34f;
            }

            if (eelTarget != null)
            {
                if (!CanPursue(eelTarget))
                    DistractEel(ref reef,EelPoint(eelWaypoint),4);
                else if (!CarriesFish(eelTarget) && !(RaidWorld.Instance && RaidWorld.Instance.IsCageBait(eelTarget)))
                    DistractEel(ref reef,eelTarget.transform.position,5);
            }

            if (eelTarget == null)
            {
                if (reef.EelPhase == 3 && eelCooldown > 3)
                {
                    SwimEel(ref reef,eelDecoy,2.8f,dt);
                    return;
                }
                Vector3 goal = EelPoint(eelWaypoint);
                if (Time.time >= eelRouteAt || (goal - reef.Eel).sqrMagnitude < 1.5f)
                {
                    if (eelRouteAt > 0) eelWaypoint = (eelWaypoint + 1) % EelPatrolCount;
                    goal = EelPoint(eelWaypoint);
                    eelRouteAt = Time.time + 11;
                }
                SwimEel(ref reef,goal,2.2f,dt);
                return;
            }

            eelDelay -= dt;
            bool visible = ClearTo(reef.Eel,eelTarget);
            Vector3 point = BodyPoint(eelTarget);
            if (visible)
            {
                eelBlindTime = 0;
                eelSeen = point;
            }
            else eelBlindTime += dt;
            if (eelBlindTime > .65f || (point - reef.Eel).sqrMagnitude > 19 * 19)
            {
                DistractEel(ref reef,EelPoint(eelWaypoint),4.6f);
                return;
            }

            if (reef.EelPhase == 1)
            {
                SwimEel(ref reef,eelSeen,.55f,dt);
                eelPreviousTarget = point;
                if (eelDelay <= 0) { reef.EelPhase = 2; eelDelay = 2.8f; }
                return;
            }
            if (eelDelay <= 0)
            {
                DistractEel(ref reef,EelPoint(eelWaypoint),5);
                return;
            }

            Vector3 lead = visible ? Vector3.ClampMagnitude((point - eelPreviousTarget) / dt * .32f,1.65f) : Vector3.zero;
            eelPreviousTarget = point;
            SwimEel(ref reef,eelSeen + lead,5.9f,dt);
            if ((reef.Eel - point).sqrMagnitude > 1.5f * 1.5f || !ClearTo(reef.Eel,eelTarget)) return;
            var inventory = eelTarget.GetComponent<HarpoonTool>();
            bool stoleFish = inventory != null && !inventory.ResistPocketTheft() && inventory.DropPocketCatch();
            eelTarget.DamageFromCreature(7);
            DistractEel(ref reef,point,7);
            main.Message = stoleFish ? "Сетевик сорвал рыбу и отвлёкся на добычу." : "Сетевик наскочил на водолаза.";
        }

        void DistractEel(ref ReefSnapshot reef,Vector3 point,float seconds)
        {
            eelTarget = null;
            eelDecoy = point;
            eelCooldown = seconds;
            reef.EelPhase = 3;
        }

        void SwimEel(ref ReefSnapshot reef,Vector3 goal,float speed,float dt)
        {
            var step = SwimToward(reef.Eel,goal,speed * dt,.36f);
            var travel = step - reef.Eel;
            if (travel.sqrMagnitude > .00001f)
                reef.EelHeading = Vector3.RotateTowards(reef.EelHeading,travel.normalized,dt * 4.7f,0);
            reef.Eel = step;
        }

        void TickKraken(ref ReefSnapshot reef,float dt,bool winch,ref ThreatSnapshot main)
        {
            if(reef.KrakenPhase==0)
            {
                krakenCharge=winch?krakenCharge+dt:Mathf.Max(0,krakenCharge-dt*.45f);
                if(krakenCharge<2.7f)return;
                var target=NearestVisible(reef.Kraken+Vector3.up*2,22);
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
                    if(CanPursue(diver) && Vector3.Distance(diver.transform.position,reef.Sweep)<3.7f && ClearTo(reef.Kraken+Vector3.up*2,diver))
                    {diver.DamageFromCreature(26);diver.ApplyExternalMotion((diver.transform.position-reef.Kraken).normalized*1.5f);main.Message="Кракен ударил в открытом проходе.";}
                reef.KrakenPhase=2;krakenClock=.85f;
            }
            else if(reef.KrakenPhase==2){reef.KrakenPhase=3;krakenClock=6;}
            else reef.KrakenPhase=0;
        }

        bool StrikeReef(Vector3 origin,Vector3 direction)
        {
            var reef=reefState.Value;
            if(!reef.Active)return false;
            if(reef.HasEel&&Hits(origin,direction,reef.Eel)){DistractEel(ref reef,EelPoint(EelPatrolCount-1),6);reefState.Value=reef;return true;}
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

        static Vector3 BodyPoint(DiverMotor diver) => diver.transform.position + Vector3.up * .27f;

        static bool CanPursue(DiverMotor diver) => CanHunt(diver) && !ReefHabitats.InShelter(BodyPoint(diver));

        static bool CarriesFish(DiverMotor diver)
        {
            if (!diver.TryGetComponent<HarpoonTool>(out var inventory)) return false;
            int count = 0;
            for (int slot = 2; slot < inventory.SlotCount; slot++)
                if (inventory.CatchIdInSlot(slot) > 0 && ++count >= 3) return true;
            return false;
        }

        DiverMotor NearestVisible(Vector3 point,float radius,bool needsFish=false)
        {
            DiverMotor best = null;
            float bestDistance = radius * radius;
            foreach (var diver in DiverMotor.ServerDivers)
            {
                if (!CanPursue(diver) || needsFish && !CarriesFish(diver)) continue;
                float distance = (BodyPoint(diver) - point).sqrMagnitude;
                if (distance >= bestDistance || !ClearTo(point,diver)) continue;
                best = diver;
                bestDistance = distance;
            }
            return best;
        }

        bool ClearTo(Vector3 start,DiverMotor diver) => PredatorClear(start,BodyPoint(diver),0);

        bool PredatorClear(Vector3 start,Vector3 end,float radius)
        {
            if (ReefHabitats.InShelter(end)) return false;
            Vector3 path = end - start;
            if (path.sqrMagnitude < .00001f) return true;
            int count = radius <= 0
                ? Physics.RaycastNonAlloc(start,path.normalized,huntHits,path.magnitude,obstacleMask,QueryTriggerInteraction.Ignore)
                : Physics.SphereCastNonAlloc(start,radius,path.normalized,huntHits,path.magnitude,obstacleMask,QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = huntHits[i].collider;
                if (collider.GetComponentInParent<DiverMotor>() == null && collider.GetComponentInParent<FishActor>() == null) return false;
            }
            return true;
        }

        Vector3 SwimToward(Vector3 position,Vector3 goal,float distance,float radius)
        {
            Vector3 direct = Vector3.ClampMagnitude(goal - position,distance);
            if (direct.sqrMagnitude < .00001f) return position;
            Vector3 head = direct.normalized;
            float look = direct.magnitude + radius;
            if (PredatorClear(position,position + head * look,radius)) return position + direct;
            for (int i = 0; i < 6; i++)
            {
                float turn = (i % 2 == 0 ? 1 : -1) * (38 + (i / 2) * 38);
                var side = Quaternion.AngleAxis(turn,Vector3.up) * head;
                if (PredatorClear(position,position + side * look,radius)) return position + side * direct.magnitude;
            }
            return position;
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
