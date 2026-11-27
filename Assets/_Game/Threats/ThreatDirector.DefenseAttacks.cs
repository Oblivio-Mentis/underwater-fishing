using System.Collections.Generic;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
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
            public float vitality, progress, windAim, latchTick, life, rescuePull;
            public double until, nextRoute, captureUntil;
            public bool counter, attached, ramHit;
            public Vector3 goal, waypoint, exit;
            public DiverMotor target;
            public CatchItem catchItem;
            public SphereCollider collider;
            public readonly List<DefenseHook> hooks=new(4);
        }

        void SpawnAttacker(DefenseTuning.Species card,Vector3 point,int slot,int group,bool large)
        {
            float size=Mathf.Lerp(defenseTuning.small,defenseTuning.large,(float)raidRandom.NextDouble());
            if(large || AvailableBudget()>=card.cost+1 && activeRaid.budget>17 && raidRandom.NextDouble()<defenseTuning.titanChance)
                size=defenseTuning.large*1.16f;
            var orbit=slot>=0?DefenseAtmosphereView.SampleOrbit(Defense.seed,slot,SwarmAge,DefenseCenter):default;
            int cost=card.cost+(size>defenseTuning.large && AvailableBudget()>card.cost?1:0);
            var go=new GameObject("Defense target");
            go.transform.SetParent(transform,true);
            go.transform.position=point;
            go.layer=Mathf.Max(0,RaiderLayer);
            var hit=go.AddComponent<SphereCollider>();
            hit.radius=.70f*size*card.silhouetteScale;
            var a=new DefenseAttacker {
                data=new AttackerSnapshot{id=++actorSerial,kind=card.kind,position=point,
                    forward=slot>=0?orbit.Forward:(DefenseCenter-point).normalized,
                    scale=slot>=0 && size<=defenseTuning.large?orbit.Scale:size*card.silhouetteScale,
                    slot=slot,victim=-1,group=group},
                card=card,cost=cost,vitality=card.health*size,slot=slot,collider=hit,exit=point
            };
            attackers.Add(a);
            var s=Defense;if(slot>=0)s.promoted|=1UL<<slot;defenseState.Value=s;
        }

        void SetPose(DefenseAttacker a,AttackerPose pose,float seconds)
        {
            a.data.pose=pose;
            if(pose==AttackerPose.Strike)a.ramHit=false;
            a.until=DefenseClock+seconds;
            a.nextRoute=0;
        }

        bool EligibleVictim(DiverMotor diver) =>
            diver && diver.IsServerInitialized && !diver.IsDead && !diver.IsInside && !diver.IsHelpless &&
            diver.gameObject.scene==gameObject.scene && !diver.IsTrapped && !diver.CheatNoclip;

        DiverMotor ChooseDefenseVictim(DefenseAttacker a,float range)
        {
            DiverMotor choice=null;
            float best=range;
            foreach(var diver in DiverMotor.ServerDivers)
            {
                if(!EligibleVictim(diver))continue;
                Vector3 point=diver.transform.position+Vector3.up*.55f;
                float score=Vector3.Distance(a.data.position,point);
                if(a.data.kind==DefenseSpecies.Stalker || a.data.kind==DefenseSpecies.Abductor)
                {
                    var tool=diver.GetComponent<HarpoonTool>();
                    if(tool && (tool.HookedFish || tool.HasDefenseLine))score*=.67f;
                    if(Vector3.Dot(diver.AimDirection,(a.data.position-point).normalized)<.30f)score*=.79f;
                }
                if(score>=best || !ClearDefenseLine(a.data.position,point))continue;
                best=score;choice=diver;
            }
            return choice;
        }

        bool ClearDefenseLine(Vector3 from,Vector3 to)
        {
            var leg=to-from;
            return leg.sqrMagnitude<.01f || !Physics.Raycast(from,leg.normalized,Mathf.Max(0,leg.magnitude-.42f),
                obstacleMask,QueryTriggerInteraction.Ignore);
        }

        bool DefensePathClear(Vector3 from,Vector3 to)
        {
            var leg=to-from;
            return leg.sqrMagnitude<.015f || !Physics.SphereCast(from,defenseTuning.sceneryClearance,leg.normalized,
                out _,leg.magnitude,obstacleMask,QueryTriggerInteraction.Ignore);
        }

        bool FindPath(Vector3 from,Vector3 goal,out Vector3 next)
        {
            if(DefensePathClear(from,goal)){next=goal;return true;}
            var heading=(goal-from).normalized;
            for(int i=0;i<8;i++)
            {
                float angle=(i/2+1)*42*(i%2==0?1:-1);
                var detour=from+Quaternion.AngleAxis(angle,Vector3.up)*heading*4+
                    Vector3.up*(i>=4?1.65f:-.62f);
                if(DefensePathClear(from,detour) && MarineSpawn.Floor(depthTier,detour,out var bed) &&
                    detour.y>bed.point.y+defenseTuning.sceneryClearance)
                {next=detour;return true;}
            }
            next=from;return false;
        }

        bool Steer(DefenseAttacker a,Vector3 goal,float speed,float dt,bool mayCrossShield=false)
        {
            if(DefenseClock>=a.nextRoute || (a.data.position-a.waypoint).sqrMagnitude<.5f)
            {
                FindPath(a.data.position,goal,out a.waypoint);
                a.nextRoute=DefenseClock+.35;
            }
            var motion=Vector3.ClampMagnitude(a.waypoint-a.data.position,speed*dt);
            if(motion.sqrMagnitude>.00001f)
            {
                float span=motion.magnitude;
                var direction=motion/span;
                if(Physics.SphereCast(a.data.position,defenseTuning.sceneryClearance,direction,out var obstacle,
                    span+.033f,obstacleMask,QueryTriggerInteraction.Ignore))
                {
                    motion=direction*Mathf.Max(0,obstacle.distance-.033f);
                    a.nextRoute=0;
                }
                var candidate=a.data.position+motion;
                if(!mayCrossShield && ShieldContains(candidate))candidate=FieldContact(candidate);
                a.data.position=candidate;
                a.data.forward=Vector3.RotateTowards(a.data.forward,direction,dt*5.2f,1);
                a.collider.transform.position=candidate;
            }
            return (goal-a.data.position).sqrMagnitude<.62f*.62f;
        }

        void TickAttacker(int index,float dt)
        {
            var a=attackers[index];
            a.life+=dt;
            if(Defense.phase==DefensePhase.Assault && ((int)(a.life/.45f)!=(int)((a.life-dt)/.45f)))
                FishActor.ScatterFromDefense(a.data.position,3.8f,1.1f,depthTier);
            if(a.hooks.Count>0 && TickDefenseHooks(index,a,dt))return;
            if(a.data.victim>=0){TickCapture(a,dt);return;}
            if(Defense.phase==DefensePhase.Resolution)
            {
                a.attached=false;
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
                if(hunts && a.target==null)a.target=ChooseDefenseVictim(a,a.counter?11f:7f);
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
                Vector3 destination=a.target?a.target.transform.position+Vector3.up*.55f:contact+outwards*.54f;
                float trigger=a.data.kind==DefenseSpecies.Rammer?4.8f:a.data.kind==DefenseSpecies.Stalker?3.1f:1.65f;
                if((destination-a.data.position).sqrMagnitude>trigger*trigger)
                {Steer(a,destination,a.card.speed,dt);return;}
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
            bool close=Steer(a,a.goal,a.card.burstSpeed,dt);
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
                if(EligibleVictim(a.target) && (a.target.transform.position+Vector3.up*.55f-a.data.position).sqrMagnitude<2.8f && ClearDefenseLine(a.data.position,a.target.transform.position))
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
            foreach(var diver in DiverMotor.ServerDivers)if(EligibleVictim(diver))able++;
            foreach(var actor in attackers)if(actor.data.victim>=0)held++;
            if(able<2 || held>=2 || !victim.BeginRaidCarry(a.data.id))return false;
            a.data.victim=victim.OwnerId;
            a.target=victim;
            a.goal=a.exit;
            a.captureUntil=DefenseClock+defenseTuning.carrySeconds;
            a.rescuePull=0;
            SetPose(a,AttackerPose.Carry,defenseTuning.carrySeconds);
            return true;
        }

        void TickCapture(DefenseAttacker a,float dt)
        {
            var victim=a.target;
            if(!victim || !victim.IsServerInitialized || victim.IsDead || victim.IsInside || victim.RaidCaptive!=a.data.id ||
                DefenseClock>=a.captureUntil || Steer(a,a.exit,defenseTuning.captiveCarrySpeed,dt))
            {
                ReleaseVictim(a);
                a.goal=a.exit;SetPose(a,AttackerPose.Retreat,a.card.cooldown);
                return;
            }
            var at=a.data.position+a.data.forward*.55f-Vector3.up*.77f;
            var delta=at-victim.transform.position;
            if(delta.sqrMagnitude>12 || !ClearDefenseLine(a.data.position,victim.transform.position))
            {ReleaseVictim(a);SetPose(a,AttackerPose.Stagger,.8f);return;}
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

        void TickWithdrawal(int index,DefenseAttacker a,float dt)
        {
            bool canCarry=Defense.breached && a.data.kind is DefenseSpecies.Thief or DefenseSpecies.Stalker or DefenseSpecies.Abductor;
            if(canCarry && a.catchItem==null && a.data.pose!=AttackerPose.Retreat &&
                activeRaid.lost+activeRaid.inTransit.Count<activeRaid.lossLimit)
            {
                var at=DefenseCenter+Vector3.down*.32f;
                if(!Steer(a,at,a.card.speed,dt,true))return;
                a.catchItem=RaidWorld.Instance.TakeDefenseCatch();
                if(a.catchItem!=null)
                {
                    a.data.stolenId=a.catchItem.id;a.data.stolenSpecies=a.catchItem.species;
                    SetPose(a,AttackerPose.Carry,0);
                }
                else SetPose(a,AttackerPose.Retreat,0);
            }
            else if(a.catchItem==null)SetPose(a,AttackerPose.Retreat,0);
            if(Steer(a,a.exit,a.card.burstSpeed*.79f,dt,true))RemoveAttacker(index,false,true);
        }

        void RemoveAttacker(int index,bool loot,bool fled,HarpoonTool catcher=null,Vector3? blastOrigin=null,float blastRadius=0)
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
            if(world.ArchiveDefense() && loot)world.CreateDefenseCatch(a.data.kind,a.data.position,a.data.forward,catcher,blastOrigin,blastRadius);
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
