using System.Collections.Generic;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        readonly RaycastHit[] pursuitSurfaceHits=new RaycastHit[16];
        readonly Collider[] pursuitOverlaps=new Collider[16];
        sealed class DefenseHook
        {
            public HarpoonTool tool;
            public FishingLine line;
            public float obstructed, tension;
        }

        sealed class DefenseAttacker
        {
            public AttackerSnapshot data;
            public DefenseTuning.Species card;
            public int cost, slot;
            public float vitality, progress, windAim, latchTick, life, rescuePull, gripObstruction;
            public double until, nextRoute, captureUntil, recoilUntil, nextFleeSearch, nextSightTest, unseenSince=-1;
            public bool hasFleeRoute;
            public bool counter, attached, ramHit, breachPursuit, breachPursuitUsed;
            public Vector3 goal, waypoint, exit, fleeGoal;
            public Vector3? stockApproach;
            public DiverMotor target;
            public CatchItem catchItem;
            public CapsuleCollider collider;
            public readonly List<DefenseHook> hooks=new(4);
        }

        void SpawnAttacker(DefenseTuning.Species card,Vector3 point,int slot,int group,bool large)
        {
            float size=Mathf.Lerp(defenseTuning.small,defenseTuning.large,(float)raidRandom.NextDouble());
            if(large || AvailableBudget()>=card.cost+1 && activeRaid.budget>17 && raidRandom.NextDouble()<defenseTuning.titanChance)
                size=defenseTuning.large*1.16f;
            var orbit=slot>=0?DefenseAtmosphereView.SampleOrbit(Defense.seed,slot,SwarmAge,DefenseCenter):default;
            if(slot>=0 && group==1 && Defense.phase==DefensePhase.Assault && Defense.approachEnd!=Vector3.zero)
            {
                var arrival=DefenseAtmosphereView.SampleApproach(Defense.seed,slot,SwarmAge,Defense.approachStart,Defense.approachEnd,1);
                if((point-arrival.Position).sqrMagnitude<1)orbit=arrival;
            }
            int cost=card.cost+(size>defenseTuning.large && AvailableBudget()>card.cost?1:0);
            var go=new GameObject("Defense target");
            go.transform.SetParent(transform,true);
            go.transform.position=point;
            go.layer=Mathf.Max(0,RaiderLayer);
            var hit=go.AddComponent<CapsuleCollider>();
            hit.direction=2;
            hit.radius=card.kind==DefenseSpecies.Rammer?.60f:.47f;
            hit.height=card.kind switch {DefenseSpecies.Remora=>2.47f,DefenseSpecies.Stalker=>2.86f,_=>2.56f};
            if(card.kind is DefenseSpecies.Abductor or DefenseSpecies.Rammer)
            {
                var fins=go.AddComponent<CapsuleCollider>();
                fins.direction=0;
                fins.height=card.kind==DefenseSpecies.Abductor?2.18f:1.66f;
                fins.radius=card.kind==DefenseSpecies.Abductor?.23f:.26f;
                fins.center=new Vector3(0,card.kind==DefenseSpecies.Abductor?.04f:-.14f,-.23f);
            }
            var a=new DefenseAttacker {
                data=new AttackerSnapshot{id=++actorSerial,kind=card.kind,position=point,
                    forward=slot>=0?orbit.Forward:(DefenseCenter-point).normalized,
                    scale=slot>=0 && size<=defenseTuning.large?orbit.Scale:size*card.silhouetteScale,
                    slot=slot,victim=-1,group=group},
                card=card,cost=cost,vitality=card.health*size,slot=slot,collider=hit,exit=point
            };
            go.transform.localScale=Vector3.one*a.data.scale;
            go.transform.rotation=Quaternion.LookRotation(a.data.forward);
            attackers.Add(a);
            var s=Defense;if(slot>=0)s.promoted|=1UL<<slot;defenseState.Value=s;
        }

        void SetPose(DefenseAttacker a,AttackerPose pose,float seconds)
        {
            a.data.pose=pose;
            a.data.poseAt=DefenseClock;
            if(pose==AttackerPose.Strike)a.ramHit=false;
            a.until=DefenseClock+seconds;
            a.nextRoute=0;
        }

        bool EligibleVictim(DiverMotor diver) =>
            diver && diver.IsServerInitialized && !diver.IsDead && !diver.IsInside && !diver.IsHelpless &&
            !diver.IsTrapped && !diver.CheatNoclip;

        DiverMotor ChooseDefenseVictim(DefenseAttacker a,float range)
        {
            DiverMotor choice=null;
            float best=range;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!EligibleVictim(diver) || a.data.kind==DefenseSpecies.Abductor && !diver.CanBeRaidCaptive)continue;
                Vector3 point=diver.transform.position+Vector3.up*.55f;
                float score=Vector3.Distance(a.data.position,point);
                if(a.data.kind==DefenseSpecies.Stalker || a.data.kind==DefenseSpecies.Abductor)
                {
                    var tool=diver.GetComponent<HarpoonTool>();
                    if(tool && (tool.HookedFish || tool.HasDefenseLine))score*=.67f;
                    if(Vector3.Dot(diver.AimDirection,(a.data.position-point).normalized)<.30f)score*=.79f;
                }
                if(score>=best || !ClearDefenseLine(a.data.position,point,diver))continue;
                best=score;choice=diver;
            }
            return choice;
        }

        bool ClearDefenseLine(Vector3 from,Vector3 to,DiverMotor endpoint=null)
        {
            var leg=to-from;
            if(leg.sqrMagnitude<.01f || !Physics.Raycast(from,leg.normalized,out var hit,Mathf.Max(0,leg.magnitude-.42f),
                obstacleMask,QueryTriggerInteraction.Ignore))return true;
            return endpoint && hit.collider.GetComponentInParent<DiverMotor>()==endpoint;
        }

        bool DefensePathClear(Vector3 from,Vector3 to,DiverMotor prey=null)
        {
            var leg=to-from;
            if(leg.sqrMagnitude<.015f)return true;
            int steps=Mathf.Max(1,Mathf.CeilToInt(leg.magnitude/1.2f));
            var vessel=NereidVessel.Active;
            if(vessel)for(int i=1;i<=steps;i++)if(vessel.ContainsSealed(from+leg*((float)i/steps)))return false;
            return !DefenseObstacle(from,leg.normalized,leg.magnitude,prey,out _);
        }

        bool DefenseObstacle(Vector3 from,Vector3 direction,float distance,DiverMotor prey,out RaycastHit nearest)
        {
            var radius=defenseTuning.sceneryClearance;
            if(!prey)return Physics.SphereCast(from,radius,direction,out nearest,distance,obstacleMask,QueryTriggerInteraction.Ignore);
            int count=Physics.SphereCastNonAlloc(from,radius,direction,pursuitSurfaceHits,distance,obstacleMask,QueryTriggerInteraction.Ignore);
            if(count==pursuitSurfaceHits.Length)
                return Physics.SphereCast(from,radius,direction,out nearest,distance,obstacleMask,QueryTriggerInteraction.Ignore);
            nearest=default;
            float closest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=pursuitSurfaceHits[i];
                if(hit.distance>=closest || hit.collider.GetComponentInParent<DiverMotor>()==prey)continue;
                closest=hit.distance;
                nearest=hit;
            }
            return nearest.collider;
        }

        bool DefenseOccupied(Vector3 point,DiverMotor prey)
        {
            float radius=defenseTuning.sceneryClearance;
            if(!prey)return Physics.CheckSphere(point,radius,obstacleMask,QueryTriggerInteraction.Ignore);
            int count=Physics.OverlapSphereNonAlloc(point,radius,pursuitOverlaps,obstacleMask,QueryTriggerInteraction.Ignore);
            if(count==pursuitOverlaps.Length)return true;
            for(int i=0;i<count;i++)
                if(pursuitOverlaps[i].GetComponentInParent<DiverMotor>()!=prey)return true;
            return false;
        }

        bool FindPath(Vector3 from,Vector3 goal,out Vector3 next,DiverMotor prey=null)
        {
            if(DefensePathClear(from,goal,prey)){next=goal;return true;}
            var heading=(goal-from).normalized;
            for(int i=0;i<8;i++)
            {
                float angle=(i/2+1)*42*(i%2==0?1:-1);
                var detour=from+Quaternion.AngleAxis(angle,Vector3.up)*heading*4+
                    Vector3.up*(i>=4?1.65f:-.62f);
                if(DefensePathClear(from,detour,prey) && MarineSpawn.Floor(depthTier,detour,out var bed) &&
                    detour.y>bed.point.y+defenseTuning.sceneryClearance)
                {next=detour;return true;}
            }
            next=from;return false;
        }

        bool Steer(DefenseAttacker a,Vector3 goal,float speed,float dt,bool mayCrossShield=false,DiverMotor prey=null)
        {
            if(DefenseClock>=a.nextRoute || (a.data.position-a.waypoint).sqrMagnitude<.5f)
            {
                FindPath(a.data.position,goal,out a.waypoint,prey);
                a.nextRoute=DefenseClock+.35;
            }
            var motion=Vector3.ClampMagnitude(a.waypoint-a.data.position,speed*dt);
            if(motion.sqrMagnitude>.00001f)
            {
                float span=motion.magnitude;
                var direction=motion/span;
                if(DefenseObstacle(a.data.position,direction,span+.033f,prey,out var obstacle))
                {
                    motion=direction*Mathf.Max(0,obstacle.distance-.033f);
                    a.nextRoute=0;
                }
                var candidate=a.data.position+motion;
                if(!mayCrossShield && ShieldContains(candidate))
                {
                    candidate=fieldView?fieldView.ContactWorld(candidate,.06f):FieldContact(candidate,.06f);
                    if(!DefensePathClear(a.data.position,candidate,prey)){a.nextRoute=0;return false;}
                }
                var facing=Vector3.RotateTowards(a.data.forward,direction,dt*5.2f,1);
                var vessel=NereidVessel.Active;
                if((vessel && !vessel.OutsideDefenseHull(candidate,facing,a.data.scale)) ||
                    DefenseOccupied(candidate,prey))
                {a.nextRoute=0;return false;}
                a.data.position=candidate;
                a.data.forward=facing;
                a.collider.transform.SetPositionAndRotation(candidate,Quaternion.LookRotation(a.data.forward));
            }
            return (goal-a.data.position).sqrMagnitude<.62f*.62f;
        }

        void TickAttacker(int index,float dt)
        {
            var a=attackers[index];
            a.life+=dt;
            if(Defense.breached && a.attached)
            {
                a.attached=false;
                RemoveAttacker(index,true,false,fallFromShield:true);
                return;
            }
            if(Defense.phase==DefensePhase.Assault && ((int)(a.life/.45f)!=(int)((a.life-dt)/.45f)))
                FishActor.ScatterFromDefense(a.data.position,3.8f,1.1f,depthTier);
            if(a.hooks.Count>0 && TickDefenseHooks(index,a,dt))return;
            if(a.data.victim>=0){TickCapture(a,dt);return;}
            if(DefenseClock<a.recoilUntil)
            {
                Steer(a,a.goal,(float)(a.recoilUntil-DefenseClock)*8,dt,Defense.breached);
                return;
            }
            if(Defense.phase==DefensePhase.Resolution)
            {
                if(Defense.breached && TickBreachPursuit(a,dt))return;
                TickWithdrawal(index,a,dt);
                return;
            }
            if(a.life>43+Defense.budget*.45f && a.data.pose!=AttackerPose.Retreat)SetPose(a,AttackerPose.Retreat,4);
            if(a.data.pose==AttackerPose.Retreat && a.life>43+Defense.budget*.45f)
            {
                if(Steer(a,a.exit,a.card.burstSpeed,dt) || DefenseClock>=a.until)RemoveAttacker(index,false,false);
                return;
            }
            if(a.data.pose==AttackerPose.Stagger)
            {
                if(DefenseClock>=a.until)SetPose(a,AttackerPose.Approach,0);
                return;
            }

            var contact=a.data.kind==DefenseSpecies.Remora?FreeLatchPoint(a):FieldContact(a.data.position);
            var outwards=(contact-DefenseCenter).normalized;
            if(a.data.pose==AttackerPose.Retreat)
            {
                Steer(a,a.goal,a.card.speed,dt);
                if(DefenseClock>=a.until)SetPose(a,AttackerPose.Approach,0);
                return;
            }
            if(a.data.pose==AttackerPose.Latch)
            {
                a.latchTick+=dt;
                if(a.latchTick>=.32f){HitField(a.card.shieldDamage*a.latchTick*a.data.scale,a.goal);a.latchTick=0;}
                return;
            }
            TickStrike(a,contact,outwards,dt);
        }

        bool TickBreachPursuit(DefenseAttacker a,float dt)
        {
            if(a.breachPursuitUsed || a.catchItem!=null || a.data.kind!=DefenseSpecies.Abductor ||
                !a.breachPursuit && DefenseAge>8)return false;
            if(!EligibleVictim(a.target) || !a.target.CanBeRaidCaptive)
                a.target=ChooseDefenseVictim(a,16f);
            if(!a.target)return false;
            if(!a.breachPursuit)
            {
                a.breachPursuit=true;
                SetPose(a,AttackerPose.Approach,0);
            }
            var contact=FieldContact(a.data.position);
            TickStrike(a,contact,(contact-DefenseCenter).normalized,dt);
            if(a.data.pose is AttackerPose.Carry or AttackerPose.Retreat || DefenseAge>=11)
                a.breachPursuitUsed=true;
            return true;
        }

        Vector3 FreeLatchPoint(DefenseAttacker a)
        {
            var along=a.data.position-DefenseCenter;
            const float spacing=1.30f;
            for(int i=0;i<7;i++)
            {
                float angle=i==0?0:(i+1)/2*(i%2==0?-28:28);
                var at=FieldContact(DefenseCenter+Quaternion.AngleAxis(angle,Vector3.up)*along);
                bool taken=false;
                foreach(var other in attackers)
                    if(other!=a && other.attached && (other.goal-at).sqrMagnitude<spacing*spacing){taken=true;break;}
                if(!taken)return at;
            }
            return FieldContact(a.data.position);
        }

        void TickStrike(DefenseAttacker a,Vector3 contact,Vector3 outwards,float dt)
        {
            double now=DefenseClock;
            if(a.data.pose==AttackerPose.Approach)
            {
                bool hunts=a.counter || a.data.kind is DefenseSpecies.Abductor or DefenseSpecies.Stalker;
                if(hunts && a.target==null)a.target=ChooseDefenseVictim(a,a.counter?15f:a.data.kind==DefenseSpecies.Abductor?15f:10f);
                if(!EligibleVictim(a.target))a.target=null;
                if(a.data.kind==DefenseSpecies.Stalker && a.target)
                {
                    var view=a.target.AimDirection;
                    var line=(a.data.position-a.target.transform.position).normalized;
                    if(Vector3.Dot(view,line)>.86f && (a.data.position-a.target.transform.position).sqrMagnitude<45)
                    {
                        var flank=Vector3.Cross(Vector3.up,line).normalized;
                        a.target=null;
                        a.goal=a.data.position+flank*(a.data.id%2==0?6:-6)+Vector3.down*1.25f;
                        SetPose(a,AttackerPose.Retreat,1.65f);
                        return;
                    }
                }
                Vector3 destination=a.target?a.target.transform.position+Vector3.up*.55f:FieldContact(contact,.70f*a.data.scale);
                float trigger=a.data.kind==DefenseSpecies.Rammer?4.8f:a.data.kind==DefenseSpecies.Stalker?3.1f:a.target?3.1f:1.65f;
                if((destination-a.data.position).sqrMagnitude>trigger*trigger)
                {Steer(a,destination,a.card.speed,dt,prey:a.target);return;}
                a.goal=destination;
                a.windAim=a.data.kind==DefenseSpecies.Rammer?1:.62f;
                a.data.forward=(a.goal-a.data.position).normalized;
                SetPose(a,AttackerPose.Windup,a.card.windup);
                return;
            }
            if(a.data.pose==AttackerPose.Windup)
            {
                if(now<a.until)
                {
                    if(a.target && a.windAim<1)a.goal=Vector3.Lerp(a.goal,a.target.transform.position+Vector3.up*.55f,dt*3f);
                    return;
                }
                SetPose(a,AttackerPose.Strike,1.1f);
            }
            var prior=a.data.position;
            if(a.data.kind==DefenseSpecies.Abductor && EligibleVictim(a.target))
            {
                var chest=a.target.transform.position+Vector3.up*.58f;
                a.goal=Vector3.MoveTowards(a.goal,chest,dt*6f);
            }
            bool close=Steer(a,a.goal,a.card.burstSpeed,dt,prey:a.target);
            if(a.data.kind==DefenseSpecies.Abductor && EligibleVictim(a.target))
            {
                var chest=a.target.transform.position+Vector3.up*.58f;
                var leg=a.data.position-prior;
                float t=Mathf.Clamp01(Vector3.Dot(chest-prior,leg)/Mathf.Max(.0001f,leg.sqrMagnitude));
                if((chest-prior-leg*t).sqrMagnitude<2.20f && ClearDefenseLine(a.data.position,chest,a.target) &&
                    TryCapture(a,a.target))return;
            }
            if(a.data.kind==DefenseSpecies.Rammer && a.target==null && !a.ramHit)
            {
                var sweep=a.data.position-prior;
                if(sweep.sqrMagnitude>.0001f)
                    foreach(var diver in DiverMotor.ServerDivers)
                    {
                        if(!EligibleVictim(diver))continue;
                        var chest=diver.transform.position+Vector3.up*.66f;
                        float along=Mathf.Clamp01(Vector3.Dot(chest-prior,sweep)/sweep.sqrMagnitude);
                        if((prior+sweep*along-chest).sqrMagnitude>1.21f)continue;
                        a.ramHit=true;
                        diver.DamageFromCreature(a.card.diverDamage*a.data.scale);
                        diver.ApplyTetherMotion(a.data.forward*.86f);
                        break;
                    }
            }
            if(!close && now<a.until)return;
            if(a.target)
            {
                if(EligibleVictim(a.target) && (a.target.transform.position+Vector3.up*.55f-a.data.position).sqrMagnitude<2.8f && ClearDefenseLine(a.data.position,a.target.transform.position+Vector3.up*.55f,a.target))
                {
                    if(a.data.kind==DefenseSpecies.Abductor && TryCapture(a,a.target))return;
                    a.target.DamageFromCreature(a.card.diverDamage*a.data.scale);
                    a.target.ApplyTetherMotion(a.data.forward*.52f);
                }
            }
            else if(close)
            {
                if(a.data.kind==DefenseSpecies.Remora)
                {
                    a.goal=contact;a.latchTick=0;a.attached=true;
                    SetPose(a,AttackerPose.Latch,0);
                    return;
                }
                HitField(a.card.shieldDamage*a.data.scale,contact);
                if(Defense.breached)return;
            }
            a.counter=false;a.target=null;
            a.goal=a.data.position+outwards*(a.data.kind==DefenseSpecies.Stalker?8f:4.4f)+
                (a.data.kind==DefenseSpecies.Stalker?Vector3.down*1.4f:Vector3.zero);
            SetPose(a,AttackerPose.Retreat,a.card.cooldown);
        }

        bool TryCapture(DefenseAttacker a,DiverMotor victim)
        {
            int able=0,held=0;
            foreach(var diver in DiverMotor.ServerDivers)
                if(diver && diver.IsServerInitialized && !diver.IsDead)able++;
            foreach(var actor in attackers)if(actor.data.victim>=0)held++;
            bool solo=able==1;
            if(held>=Mathf.Clamp(able/2,1,2) || !victim.BeginRaidCarry(a.data.id,solo))return false;
            a.data.victim=victim.OwnerId;
            a.target=victim;
            Vector3 outward=Vector3.ProjectOnPlane(a.data.position-DefenseCenter,Vector3.up).normalized;
            if(outward.sqrMagnitude<.01f)outward=a.data.forward;
            Vector3 toward=a.data.position+outward*17+Vector3.up*1.4f;
            a.goal=FindPath(a.data.position,toward,out var leg,victim) && (leg-a.data.position).sqrMagnitude>4?leg:a.exit;
            float carry=solo?defenseTuning.soloCarrySeconds:defenseTuning.carrySeconds;
            a.captureUntil=DefenseClock+carry;
            a.rescuePull=0;
            a.gripObstruction=0;
            SetPose(a,AttackerPose.Carry,carry);
            return true;
        }

        void TickCapture(DefenseAttacker a,float dt)
        {
            var victim=a.target;
            if(!victim || !victim.IsServerInitialized || victim.IsDead || victim.IsInside || victim.RaidCaptive!=a.data.id ||
                DefenseClock>=a.captureUntil)
            {
                ReleaseVictim(a);
                a.goal=a.exit;SetPose(a,AttackerPose.Retreat,a.card.cooldown);
                return;
            }
            Vector3 chest=victim.transform.position+Vector3.up*.55f;
            bool linked=ClearDefenseLine(a.data.position,chest,victim);
            a.gripObstruction=linked?Mathf.Max(0,a.gripObstruction-dt*2):a.gripObstruction+dt;
            if(a.gripObstruction>=1.05f || (a.data.position-chest).sqrMagnitude>23)
            {ReleaseVictim(a);SetPose(a,AttackerPose.Stagger,.8f);return;}
            if(!linked)return;
            float speed=defenseTuning.captiveCarrySpeed;
            if(victim.TryGetComponent<HarpoonTool>(out var harpoon) && harpoon.TeammateReelsThisDiver)
                speed*=.27f;
            else if(victim.CanBreakRaidGrip && victim.TryGetComponent<RescueTool>(out var rescue) &&
                rescue.DisplayTargetOwnerId==victim.OwnerId)
                speed*=Mathf.Lerp(1,.39f,rescue.DisplayProgress);
            if(Steer(a,a.goal,speed,dt,prey:victim))
            {
                Vector3 outward=Vector3.ProjectOnPlane(a.data.position-DefenseCenter,Vector3.up).normalized;
                if(FindPath(a.data.position,a.data.position+outward*11+Vector3.up,out var next,victim))
                    a.goal=next;
            }
            var at=a.data.position+a.data.forward*(.57f*a.data.scale)-Vector3.up*(1.12f*a.data.scale);
            var delta=at-victim.transform.position;
            victim.ApplyTetherMotion(Vector3.ClampMagnitude(delta,dt*(defenseTuning.captiveCarrySpeed+3f)));
            victim.DamageFromCreature(a.card.diverDamage*.085f*dt);
        }

        void ReleaseVictim(DefenseAttacker a)
        {
            if(a.data.victim<0)return;
            if(a.target)a.target.EndRaidCarry(a.data.id);
            a.target=null;a.data.victim=-1;a.rescuePull=0;
        }

        internal bool RescueDefenseVictim(DiverMotor victim)
        {
            if(!IsServerInitialized || !victim || victim.RaidCaptive<=0)return false;
            foreach(var a in attackers)
                if(a.data.id==victim.RaidCaptive && a.target==victim)
                {ReleaseVictim(a);SetPose(a,AttackerPose.Stagger,1.4f);return true;}
            return false;
        }

        internal bool BreakSoloGrip(DiverMotor victim)
        {
            if(!IsServerInitialized || !victim || !victim.CanBreakRaidGrip)return false;
            foreach(var a in attackers)
            {
                if(a.data.id!=victim.RaidCaptive || a.target!=victim)continue;
                Vector3 away=(victim.transform.position-a.data.position).normalized;
                if(away.sqrMagnitude<.001f)away=-a.data.forward;
                ReleaseVictim(a);
                victim.PresentRaidBreak((Vector3.ProjectOnPlane(away,Vector3.up)+Vector3.up*.27f).normalized);
                a.goal=a.data.position-away*2.6f+Vector3.up*.5f;
                a.recoilUntil=DefenseClock+.58;
                SetPose(a,AttackerPose.Stagger,1.25f);
                return true;
            }
            return false;
        }

        void TickWithdrawal(int index,DefenseAttacker a,float dt)
        {
            if(a.catchItem!=null)
            {
                TickDefenseEscape(index,a,dt,true);
                return;
            }
            bool canCarry=Defense.breached && a.data.kind is DefenseSpecies.Thief or DefenseSpecies.Stalker or DefenseSpecies.Abductor;
            if(canCarry && a.data.pose!=AttackerPose.Retreat &&
                (DefenseAge<defenseTuning.theftSeconds || a.data.pose==AttackerPose.Snatch) &&
                activeRaid.lost+activeRaid.inTransit.Count<activeRaid.lossLimit)
            {
                var pickup=TheftPickup();
                if(a.data.pose==AttackerPose.Snatch)
                {
                    float age=(float)(DefenseClock-a.data.poseAt);
                    if(age<defenseTuning.snatchSeconds)
                    {
                        if(age>=defenseTuning.snatchSeconds*.4f)
                            Steer(a,a.goal,a.card.burstSpeed*.55f,dt,true);
                        return;
                    }
                    a.catchItem=RaidWorld.Instance.TakeDefenseCatch();
                    if(a.catchItem==null){SetPose(a,AttackerPose.Retreat,0);return;}
                    a.data.stolenId=a.catchItem.id;
                    a.data.stolenSpecies=a.catchItem.species;
                    a.data.stolenAt=DefenseClock;
                    SetPose(a,AttackerPose.Carry,0);
                    return;
                }
                if(a.stockApproach==null && !DefensePathClear(a.data.position,pickup) &&
                    TryStockChannel(a.data.position,pickup,a.exit,out var opening))a.stockApproach=opening;
                Vector3 facing=(pickup-a.data.position).normalized;
                float mouthReach=.8f*a.data.scale;
                Vector3 poised=pickup-facing*(mouthReach+.53f);
                if(!Steer(a,a.stockApproach??poised,a.card.speed,dt,true))return;
                if(a.stockApproach.HasValue){a.stockApproach=null;a.nextRoute=0;return;}
                a.data.snatchPoint=pickup;
                a.goal=pickup-facing*mouthReach;
                SetPose(a,AttackerPose.Snatch,defenseTuning.snatchSeconds);
                return;
            }
            TickDefenseEscape(index,a,dt,false);
        }

        Vector3 TheftPickup()
        {
            float depth=fieldView?fieldView.DepthBelowCenter*.68f:.32f;
            var pickup=DefenseCenter+Vector3.down*depth;
            if(MarineSpawn.Floor(depthTier,pickup,out var floor))pickup.y=Mathf.Max(pickup.y,floor.point.y+.55f);
            return pickup;
        }

        bool TryStockChannel(Vector3 from,Vector3 pickup,Vector3 preferred,out Vector3 point)
        {
            Vector3 broadside=fieldAnchor?fieldAnchor.right:Vector3.right;
            float side=Vector3.Dot(preferred-pickup,broadside)<0?-1:1;
            float halfWidth=fieldView?fieldView.radii.x:fieldRadii.x;
            float distance=halfWidth+1.25f;
            for(int i=0;i<2;i++)
            for(int angle=0;angle<5;angle++)
            {
                float yaw=((angle+1)/2)*(angle%2==0?-20:20);
                var lane=Quaternion.AngleAxis(yaw,Vector3.up)*(broadside*side*(i==0?1:-1));
                var candidate=pickup+lane*distance;
                if(!MarineSpawn.InOpenWater(depthTier,candidate,defenseTuning.sceneryClearance+.1f) ||
                    !DefensePathClear(from,candidate) || !DefensePathClear(candidate,pickup))continue;
                point=candidate;
                return true;
            }
            point=default;
            return false;
        }

        void RemoveAttacker(int index,bool loot,bool fled,HarpoonTool catcher=null,Vector3? blastOrigin=null,float blastRadius=0,
            bool fallFromShield=false)
        {
            var a=attackers[index];
            var world=RaidWorld.Instance;
            ReleaseVictim(a);
            ClearHooks(a,false);
            if(a.catchItem!=null)
            {
                if(fled)
                {
                    world.LoseDefenseCatch(a.catchItem);
                    var lost=Defense;lost.lost=activeRaid.lost;defenseState.Value=lost;
                }
                else
                {
                    world.RecoverDefenseCatch(a.catchItem,true);
                    var recovered=Defense;recovered.returned++;defenseState.Value=recovered;
                }
            }
            if(activeRaid.breached && (a.data.kind is DefenseSpecies.Thief or DefenseSpecies.Stalker or DefenseSpecies.Abductor))
                activeRaid.remainingCarriers=Mathf.Max(0,activeRaid.remainingCarriers-1);
            activeRaid.spent=Mathf.Min(activeRaid.budget,activeRaid.spent+a.cost);
            var stateCopy=Defense;stateCopy.spent=activeRaid.spent;
            if(a.slot>=0)stateCopy.promoted&=~(1UL<<a.slot);
            defenseState.Value=stateCopy;
            if(a.collider)Destroy(a.collider.gameObject);
            attackers.RemoveAt(index);
            if(world.ArchiveDefense() && loot)world.CreateDefenseCatch(a.data.kind,a.data.position,a.data.forward,catcher,
                blastOrigin,blastRadius,fallFromShield);
        }

        void TearRemora(DefenseAttacker a)
        {
            if(inkClouds.Count>=4)inkClouds.RemoveAt(0);
            inkClouds.Add(new InkCloud{id=++inkSerial,at=DefenseClock,center=a.data.position,forward=a.data.forward});
            a.attached=false;
        }

        internal void BlastDefense(Vector3 origin,float radius,float damage)
        {
            if(!IsServerInitialized || !DefenseActive || radius<=0 || damage<=0)return;
            for(int i=attackers.Count-1;i>=0;i--)
            {
                var a=attackers[i];
                float intensity=1-Mathf.Clamp01(Vector3.Distance(a.data.position,origin)/radius);
                if(intensity<=0 || !ClearDefenseLine(origin,a.data.position))continue;
                a.vitality-=damage*Mathf.Lerp(.40f,1f,intensity);
                ReleaseVictim(a);
                if(a.vitality<=0){if(a.attached)TearRemora(a);RemoveAttacker(i,true,false,blastOrigin:origin,blastRadius:radius);}
                else if(a.hooks.Count==0)SetPose(a,AttackerPose.Stagger,1.7f);
            }
        }
    }
}
