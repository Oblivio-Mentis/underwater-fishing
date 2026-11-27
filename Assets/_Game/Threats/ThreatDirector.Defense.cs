using System;
using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        public DefenseTuning defenseTuning;

        public struct DefenseSnapshot
        {
            public DefensePhase phase;
            public FieldCondition condition;
            public int seed, budget, spent, wave, lost, returned, impact;
            public ulong promoted;
            public float integrity, maximum, severity, impactStrength;
            public double phaseAt, beginsAt, impactAt, assaultAt, blackoutFadeAt;
            public Vector3 impactPoint, approachStart, approachEnd;
            public bool breached, provoked;
            public float Ratio => maximum>0?Mathf.Clamp01(integrity/maximum):1;
        }

        public struct AttackerSnapshot
        {
            public int id, victim, group, slot;
            public DefenseSpecies kind;
            public AttackerPose pose;
            public FishBehaviour fightPhase;
            public int fightTug;
            public DefenseUrgency urgency;
            public Vector3 position, forward, impactOffset, snatchPoint, tongueAim, remoraPull;
            public Vector3 velocity, shieldContact;
            public bool shieldAttached;
            public float scale, effort, fightRemaining;
            public byte hookCount;
            public double poseAt, stolenAt, tongueGripAt, tongueReleasedAt, fightAt;
            public string stolenSpecies;
            public long stolenId;
        }

        readonly SyncVar<DefenseSnapshot> defenseState=new(new SyncTypeSettings(.08f));
        readonly SyncList<AttackerSnapshot> defenseActors=new(new SyncTypeSettings(.115f));
        readonly List<DefenseAttacker> attackers=new();
        readonly List<DefenseTuning.Species> eligibleAttacks=new();
        public struct InkCloud
        {
            public int id;
            public double at;
            public Vector3 center, forward;
        }
        readonly SyncList<InkCloud> inkClouds=new();
        int inkSerial;
        DefenseRecord activeRaid;
        System.Random raidRandom;
        bool ownsDefenseTuning;
        double nextGroupAt, nextActorSnapshot;
        int actorSerial, groupSerial;
        Vector3 fieldRadii=new(2.52f,1.28f,4.88f);
        Transform fieldAnchor;

        public DefenseSnapshot Defense => defenseState.Value;
        public bool DefenseActive => Defense.phase!=DefensePhase.Idle;
        public int DefenseActorCount => defenseActors.Count;
        public AttackerSnapshot DefenseActorAt(int index) => defenseActors[index];
        public float DefenseAge => Mathf.Max(0,(float)(DefenseClock-Defense.phaseAt));
        public float SwarmAge => Mathf.Max(0,(float)(DefenseClock-Defense.beginsAt));
        public float DefenseCountdown => Defense.phase is DefensePhase.Warning or DefensePhase.Encirclement
            ? Mathf.Max(0,(float)(Defense.assaultAt-DefenseClock)) : 0;
        public bool DefenseAlarm => Defense.phase is DefensePhase.Encirclement or DefensePhase.Assault or DefensePhase.Resolution;
        public float DefenseBlackout => Defense.phase switch
        {
            DefensePhase.Warning or DefensePhase.Encirclement=>Mathf.SmoothStep(0,1,SwarmAge/.85f),
            DefensePhase.Assault or DefensePhase.Resolution=>1,
            _=>Defense.blackoutFadeAt>0
                ?1-Mathf.SmoothStep(0,1,(float)(DefenseClock-Defense.blackoutFadeAt)/defenseTuning.blackoutFadeSeconds)
                :0
        };
        internal Vector3 DefenseApproachFocus => DefenseActive?SampleDefenseSlot(0).Position:DefenseCenter;
        public Vector3 DefenseCenter => fieldAnchor?fieldAnchor.position:
            NereidVessel.Active?NereidVessel.Active.transform.TransformPoint(new Vector3(0,-1.195f,-2)):tuning.reservoirPoint;
        public double DefenseClock => !NetworkObject || TimeManager==null?Time.timeAsDouble:
            TimeManager.TicksToTime(TimeManager.Tick)+TimeManager.GetTickElapsedAsDouble();
        public float DefenseConcealment => defenseTuning?DefenseBlackout*defenseTuning.concealment:0;

        DefenseAtmosphereView.OrbitPose SampleDefenseSlot(int slot)
        {
            var snapshot=Defense;
            float approachAge=(float)(DefenseClock-snapshot.assaultAt);
            return DefenseAtmosphereView.SampleSwimmer(snapshot.seed,slot,approachAge,defenseTuning.encirclementSeconds,
                DefenseCenter,snapshot.approachStart,snapshot.approachEnd);
        }

        void LoadDefenseSettings()
        {
            if(!defenseTuning)defenseTuning=Resources.Load<DefenseTuning>("ExpeditionDefense");
            if(!defenseTuning){defenseTuning=ScriptableObject.CreateInstance<DefenseTuning>();ownsDefenseTuning=true;}
        }

        void ResetDefense(double blackoutFadeAt=0)
        {
            foreach(var a in attackers){ReleaseVictim(a);ClearHooks(a,false);if(a.collider)Destroy(a.collider.gameObject);}
            attackers.Clear();defenseActors.Clear();inkClouds.Clear();activeRaid=null;
            defenseState.Value=new DefenseSnapshot{integrity=defenseTuning.maximumIntegrity,
                maximum=defenseTuning.maximumIntegrity,blackoutFadeAt=blackoutFadeAt};
        }

        internal bool BeginDefense(HungerReport report,bool provoked=false)
        {
            var world=RaidWorld.Instance;
            if(!IsServerInitialized || world?.Team==null || !NereidVessel.Active || WaveActive || !report.Attack ||
                SpawnLayout?.raiderEnabled==false)return false;
            int crew=0;
            foreach(var diver in DiverMotor.ServerDivers)if(diver && !diver.IsDead)crew++;
            int budget=defenseTuning.Budget(report.deficit,report.required,world.progression.foodRequired*TeamProgress.FoodScale,crew,report.mitigation);
            if(budget<=0)return false;
            var record=new DefenseRecord{
                seed=UnityEngine.Random.Range(1,int.MaxValue),budget=budget,appetite=report.required,deficit=report.deficit,
                mitigation=report.mitigation,integrity=defenseTuning.maximumIntegrity,provoked=provoked
            };
            world.Team.ongoingDefense=record;
            StartDefense(record);
            world.ArchiveDefense();
            return true;
        }

        internal void ResumeDefense(DefenseRecord record)
        {
            if(record==null || DefenseActive || !NereidVessel.Active)return;
            var world=RaidWorld.Instance;
            for(int i=record.inTransit.Count-1;i>=0;i--)world.RecoverDefenseCatch(record.inTransit[i],false);
            StartDefense(record);
            if(record.breached && !record.resolutionComplete)
            {
                var thief=defenseTuning.For(DefenseSpecies.Thief);
                for(int i=0;thief!=null && i<record.remainingCarriers;i++)
                {
                    if(!TryArrival(i*11,thief.kind,out var point,out var slot))continue;
                    if(!TrySpawnAttacker(thief,point,slot,1,false))continue;
                    SetPose(attackers[^1],AttackerPose.Carry,0);
                }
            }
            world.ArchiveDefense();
        }

        void StartDefense(DefenseRecord record)
        {
            activeRaid=record;
            raidRandom=new System.Random(record.seed ^ record.spent);
            double now=DefenseClock;
            SelectDefenseApproach(record.seed,out var approachStart,out var approachEnd);
            defenseState.Value=new DefenseSnapshot {
                seed=record.seed, budget=record.budget, spent=record.spent, lost=record.lost,
                integrity=Mathf.Clamp(record.integrity,0,defenseTuning.maximumIntegrity),maximum=defenseTuning.maximumIntegrity,
                severity=record.deficit, phase=record.resolutionComplete?DefensePhase.Recovery:record.breached?DefensePhase.Resolution:DefensePhase.Warning,
                breached=record.breached, provoked=record.provoked, beginsAt=now,phaseAt=now,
                blackoutFadeAt=record.resolutionComplete?now+defenseTuning.blackoutHoldSeconds:0,
                assaultAt=now+defenseTuning.warningSeconds+defenseTuning.encirclementSeconds,
                approachStart=approachStart,approachEnd=approachEnd,
                condition=record.breached?FieldCondition.Broken:FieldCondition.Normal
            };
            nextActorSnapshot=0;
            groupSerial=0;
            if(!record.breached && !record.resolutionComplete)FishActor.ScatterFromDefense(DefenseCenter,19f,3.5f,depthTier);
            SetDefenseMessage(record.provoked?"Рёв кракена. Стая идёт к подлодке!":"Кракен голоден. Возвращайтесь к подлодке!");
        }

        void SelectDefenseApproach(int seed,out Vector3 start,out Vector3 end)
        {
            var vessel=NereidVessel.Active;
            var center=DefenseCenter;
            Vector3 broadside=vessel.transform.right*((seed&1)==0?1:-1);
            Vector3 extent=fieldView?fieldView.transform.lossyScale:Vector3.one;
            float hullSide=fieldView?fieldView.radii.x*extent.x:10;
            Vector3 higher=center+Vector3.up*(defenseTuning.schoolHeight*extent.y);
            var school=DefenseAtmosphereView.Stock;
            float finishDistance=Mathf.Max(school.orbitRadius,Mathf.Max(hullSide+6,defenseTuning.arrivalDistance));
            float startDistance=Mathf.Max(finishDistance+48,defenseTuning.schoolTravelDistance+40);
            Vector3 bestStart=higher+broadside*startDistance;
            Vector3 bestEnd=higher+broadside*finishDistance;
            float bestScore=-1;
            var markers=SpawnLayout?.defenseApproaches;
            if(markers!=null)for(int i=0;i<markers.Length;i++)
            {
                var marker=markers[(i+(seed&int.MaxValue)%markers.Length)%markers.Length];
                if(!marker)continue;
                var horizontal=Vector3.ProjectOnPlane(marker.position-center,Vector3.up);
                if(horizontal.sqrMagnitude<1)continue;
                var near=higher+horizontal.normalized*finishDistance;
                var far=higher+horizontal.normalized*startDistance+Vector3.up*1.25f;
                Consider(far,near,.06f);
            }
            for(int height=0;height<3;height++)
            for(int i=0;i<18;i++)
            {
                int lane=i%9;
                float angle=((lane+1)/2)*13*(lane%2==0?-1:1);
                Vector3 heading=Quaternion.AngleAxis(angle,Vector3.up)*broadside*(i<9?1:-1);
                Vector3 altitude=higher+Vector3.up*(height*2.7f);
                Vector3 near=altitude+heading*finishDistance-Vector3.up*.12f;
                Vector3 far=altitude+heading*(startDistance-height*4f)+Vector3.up*1.25f;
                Consider(far,near,0);
            }
            start=bestStart;
            end=bestEnd;

            void Consider(Vector3 far,Vector3 near,float markerPreference)
            {
                if(!MarineSpawn.InOpenWater(depthTier,near,.9f) || !DefensePathClear(far,near))return;
                TryDefenseWindow(near,far,out _,out float opening);
                float score=opening+markerPreference;
                if(score<=bestScore)return;
                bestScore=score;
                bestStart=far;
                bestEnd=near;
            }
        }

        internal bool TryDefenseWindow(Vector3 near,Vector3 far,out Vector3 bestEye)
            => TryDefenseWindow(near,far,out bestEye,out _);

        bool TryDefenseWindow(Vector3 near,Vector3 far,out Vector3 bestEye,out float best)
        {
            bestEye=default;
            best=0;
            var vessel=NereidVessel.Active;
            var room=vessel?vessel.ObservationRoom():null;
            if(!room)return false;
            int side=Vector3.Dot(near-DefenseCenter,vessel.transform.right)<0?-1:1;
            var outward=vessel.transform.right*side;
            var forward=room.forward;
            for(int index=1;index<=3;index++)
            {
                var window=room.Find((side<0?"Port":"Starboard")+" observation window "+index);
                if(!window)continue;
                Vector3 aboveDeck=window.position-outward*1.22f+Vector3.up*.6f;
                int floors=Physics.RaycastNonAlloc(aboveDeck,Vector3.down,escapeSightHits,4,obstacleMask,QueryTriggerInteraction.Ignore);
                float nearest=float.PositiveInfinity;
                for(int floor=0;floor<floors;floor++)
                {
                    var hit=escapeSightHits[floor];
                    if(hit.normal.y>=.8f && hit.collider.transform.IsChildOf(room))
                        nearest=Mathf.Min(nearest,hit.distance);
                }
                if(!float.IsFinite(nearest))continue;
                // The standing view is 0.60 m above the diver's centred 1.80 m capsule.
                Vector3 eye=aboveDeck+Vector3.up*(1.51f-nearest);
                if(!vessel.ContainsSealed(eye) || !WindowRay(window,eye,outward,near,out float nearScore) ||
                    !WindowRay(window,eye,outward,far,out float farScore))continue;
                Vector3 centre=Vector3.Lerp(near,far,.45f);
                float roomToEitherSide=0;
                if(WindowRay(window,eye,outward,centre-forward*15,out _))roomToEitherSide+=.46f;
                if(WindowRay(window,eye,outward,centre+forward*15,out _))roomToEitherSide+=.46f;
                if(WindowRay(window,eye,outward,centre+Vector3.up*11,out _))roomToEitherSide+=.28f;
                float score=nearScore*.65f+farScore*.35f+roomToEitherSide;
                if(score<=best)continue;
                best=score;
                bestEye=eye;
            }
            return best>0;
        }

        bool WindowRay(Transform window,Vector3 eye,Vector3 outward,Vector3 target,out float score)
        {
            score=0;
            Vector3 ray=target-eye;
            float outwardTravel=Vector3.Dot(ray,outward);
            if(outwardTravel<4)return false;
            float intercept=Vector3.Dot(window.position-eye,outward)/outwardTravel;
            var local=window.InverseTransformPoint(eye+ray*intercept);
            if(Mathf.Abs(local.y)>1.26f || Mathf.Abs(local.z)>1.57f)return false;

            int count=Physics.RaycastNonAlloc(eye,ray.normalized,escapeSightHits,ray.magnitude-.45f,
                obstacleMask,QueryTriggerInteraction.Ignore);
            if(count==escapeSightHits.Length)return false;
            for(int index=0;index<count;index++)
            {
                var surface=escapeSightHits[index].collider;
                if(!IsObservationGlass(surface.transform) && !surface.GetComponentInParent<DiverMotor>())return false;
            }
            score=1-.36f*Mathf.Abs(local.y)-.23f*Mathf.Abs(local.z);
            return true;
        }

        void SetDefenseMessage(string text)
        {
            var current=state.Value;
            current.Message=text;
            state.Value=current;
        }

        void AdvanceDefense(DefensePhase phase,string text)
        {
            var current=Defense;
            current.phase=phase;
            current.phaseAt=DefenseClock;
            if(phase==DefensePhase.Recovery)current.blackoutFadeAt=current.phaseAt+defenseTuning.blackoutHoldSeconds;
            defenseState.Value=current;
            SetDefenseMessage(text);
        }

        void TickDefense(float dt)
        {
            var vessel=NereidVessel.Active;
            if(vessel && !fieldView)
            {
                LocateDefenseField();
                if(!fieldAnchor)fieldAnchor=vessel.authoredRail?vessel.authoredRail.aquarium:null;
            }
            if(!DefenseActive || activeRaid==null)return;
            double now=DefenseClock;
            if(inkClouds.Count>0 && now-inkClouds[0].at>=defenseTuning.inkSeconds)inkClouds.RemoveAt(0);
            var snapshot=Defense;
            float age=(float)(now-snapshot.phaseAt);
            switch(snapshot.phase)
            {
                case DefensePhase.Warning:
                    if(now>=snapshot.assaultAt-defenseTuning.encirclementSeconds)AdvanceDefense(DefensePhase.Encirclement,"Стая приближается. Следите за прожекторами!");
                    break;
                case DefensePhase.Encirclement:
                    if(now>=snapshot.assaultAt)
                    {
                        nextGroupAt=now;
                        AdvanceDefense(DefensePhase.Assault,"Защищайте поле с уловом!");
                    }
                    break;
                case DefensePhase.Assault:
                    if(age>=defenseTuning.maximumAssaultSeconds || activeRaid.spent>=activeRaid.budget && attackers.Count==0)
                        AdvanceDefense(DefensePhase.Resolution,"Стая отходит. Улов сохранён.");
                    else if(now>=nextGroupAt)
                    {
                        nextGroupAt=now+(LaunchGroup()?Mathf.Lerp(defenseTuning.firstGroupGap,defenseTuning.lateGroupGap,
                            Mathf.Clamp01((activeRaid.budget-5f)/29f)):1.25f);
                    }
                    break;
                case DefensePhase.Resolution:
                    if(attackers.Count==0)
                    {
                        activeRaid.resolutionComplete=true;
                        AdvanceDefense(DefensePhase.Recovery,snapshot.breached?"Генераторы восстанавливают поле.":"Улов защищён. Стая рассеивается.");
                        RaidWorld.Instance?.ArchiveDefense();
                    }
                    break;
                case DefensePhase.Recovery:
                    var recovering=Defense;
                    recovering.condition=FieldCondition.Restoring;
                    recovering.integrity=Mathf.Lerp(activeRaid.integrity,recovering.maximum,Mathf.Clamp01(age/defenseTuning.restorationSeconds));
                    defenseState.Value=recovering;
                    if(age>=defenseTuning.restorationSeconds)
                    {
                        var world=RaidWorld.Instance;
                        if(world?.Team!=null)world.Team.ongoingDefense=null;
                        ResetDefense(recovering.blackoutFadeAt);
                        SetDefenseMessage("Поле восстановлено. Экспедиция продолжается.");
                        world?.ArchiveDefense();
                    }
                    break;
            }
            for(int i=attackers.Count-1;i>=0;i--)
            {
                if(i>=attackers.Count)continue;
                var attacker=attackers[i];
                TickAttacker(i,dt);
                if(attacker.collider && attacker.data.forward.sqrMagnitude>.001f)
                    attacker.collider.transform.rotation=Quaternion.LookRotation(attacker.data.forward);
            }
            if(now>=nextActorSnapshot)
            {
                nextActorSnapshot=now+.1;
                PublishAttackers();
            }
            RefreshFieldCondition();
        }

        int AvailableBudget()
        {
            int planned=activeRaid.spent;
            foreach(var attacker in attackers)planned+=attacker.cost;
            return activeRaid.budget-planned;
        }

        bool LaunchGroup()
        {
            int crew=0;
            foreach(var diver in DiverMotor.ServerDivers)if(diver && !diver.IsDead && !diver.IsHelpless)crew++;
            int cap=Mathf.Clamp(defenseTuning.baseActive+Mathf.Max(0,crew-1)*defenseTuning.activePerExtraDiver,1,defenseTuning.maximumActive);
            int size=Mathf.Min(defenseTuning.maximumGroup,cap-attackers.Count,2+activeRaid.budget/6);
            if(size<=0)return false;
            bool any=false;
            int lane=raidRandom.Next(0,60);
            int group=groupSerial+1;
            for(int i=0;i<size && AvailableBudget()>0;i++)
            {
                var card=group==1 && i<2 && AvailableBudget()>=3
                    ?defenseTuning.For(i==0?DefenseSpecies.Rammer:DefenseSpecies.Remora)
                    :ChooseAttacker(AvailableBudget());
                if(card==null)break;
                if(!TryArrival(lane+i*7,card.kind,out var point,out var slot))continue;
                any|=TrySpawnAttacker(card,point,slot,group,false);
            }
            if(any)
            {
                groupSerial=group;
                var snapshot=Defense;snapshot.wave=1+(groupSerial-1)/3;defenseState.Value=snapshot;
            }
            return any;
        }

        DefenseTuning.Species ChooseAttacker(int remaining)
        {
            eligibleAttacks.Clear();
            foreach(var card in defenseTuning.species)
            {
                if(card==null || card.cost<=0 || card.cost>remaining)continue;
                int count=0;
                foreach(var actor in attackers)if(actor.data.kind==card.kind)count++;
                if(count<card.simultaneous && (activeRaid.budget>=8 || card.kind is DefenseSpecies.Thief or DefenseSpecies.Rammer or DefenseSpecies.Stalker))
                    eligibleAttacks.Add(card);
            }
            return eligibleAttacks.Count==0?null:eligibleAttacks[raidRandom.Next(eligibleAttacks.Count)];
        }

        bool TryArrival(int lane,DefenseSpecies species,out Vector3 point,out int slot)
        {
            Vector3 center=DefenseCenter;
            if(Defense.phase==DefensePhase.Assault)
            {
                int count=DefenseAtmosphereView.SwarmSlotCount;
                int lead=DefenseAtmosphereView.LeadSlots;
                for(int i=0;i<count;i++)
                {
                    // The opening group separates from the lead shoal in view of the crew.
                    // Later waves arise in the reserves wherever there is a clear route.
                    slot=groupSerial==0
                        ? i<lead ? (lane+i)%lead : lead+(lane+i-lead)%(count-lead)
                        : (lane+i)%count;
                    if((Defense.promoted&(1UL<<slot))!=0 || slot%DefenseAtmosphereView.SpeciesCount!=(int)species)continue;
                    var pose=SampleDefenseSlot(slot);
                    if(!OutsideAttackEntry(species,pose.Position,pose.Forward,pose.Scale) ||
                        !MarineSpawn.InOpenWater(depthTier,pose.Position,.70f) ||
                        !FindPath(pose.Position,FieldContact(pose.Position),out _))continue;
                    point=pose.Position;return true;
                }
            }
            var authored=SpawnLayout?.defenseApproaches;
            if(authored!=null)
            {
                for(int i=0;i<authored.Length;i++)
                {
                    var marker=authored[(i+lane)%authored.Length];
                    if(marker && Vector3.Distance(marker.position,center)>=7 && MarineSpawn.InOpenWater(depthTier,marker.position,.4f) &&
                        OutsideAttackEntry(species,marker.position,(center-marker.position).normalized,1.38f) &&
                        !CrewSeesEscape(marker.position,DefenseAtmosphereView.Stock.darkDrawingDistance) &&
                        ReefHabitats.CanArrive(marker.position,7,12) && FindPath(marker.position,FieldContact(marker.position),out _))
                    {point=marker.position;slot=-1;return true;}
                }
            }
            // A wave can outlast its available silhouettes. Search for covered approach lanes.
            for(int attempt=0;attempt<15;attempt++)
            {
                float angle=lane*29+attempt*137.5f;
                var direction=Quaternion.AngleAxis(angle,Vector3.up)*Vector3.forward;
                var candidate=center+direction*(27+attempt%3*9)+Vector3.up*(2+attempt%4*1.7f);
                if(!MarineSpawn.InOpenWater(depthTier,candidate,.7f) ||
                    !OutsideAttackEntry(species,candidate,(center-candidate).normalized,1.38f) ||
                    CrewSeesEscape(candidate,DefenseAtmosphereView.Stock.darkDrawingDistance) ||
                    !FindPath(candidate,FieldContact(candidate),out _))continue;
                point=candidate;slot=-1;return true;
            }
            point=default;slot=-1;
            return false;
        }

        void PublishAttackers()
        {
            for(int i=0;i<attackers.Count;i++)
            {
                var actor=attackers[i];
                var data=actor.data;
                data.shieldAttached=actor.attached;
                data.hookCount=(byte)actor.hooks.Count;
                data.fightRemaining=1-Mathf.Clamp01(actor.progress/FightWork(actor));
                data.urgency=actor.data.victim>=0?DefenseUrgency.Rescue:
                    actor.catchItem!=null || actor.seekingStock?DefenseUrgency.Stock:
                    actor.attached || Defense.phase==DefensePhase.Assault && !actor.target && data.hookCount==0 &&
                    data.pose is AttackerPose.Approach or AttackerPose.Windup or AttackerPose.Strike
                        ?DefenseUrgency.Shield:DefenseUrgency.None;
                if(i<defenseActors.Count)defenseActors[i]=data;
                else defenseActors.Add(data);
            }
            while(defenseActors.Count>attackers.Count)defenseActors.RemoveAt(defenseActors.Count-1);
        }

        internal bool TryDefenseFight(int id,out AttackerSnapshot target)
        {
            if(id>0)foreach(var actor in defenseActors)
                if(actor.id==id){target=actor;return true;}
            target=default;
            return false;
        }

        public bool TryDefenseAnchor(int id,out Vector3 position)
        {
            if(id>0)
            {
                if(IsServerInitialized)foreach(var a in attackers)if(a.data.id==id){position=a.data.position+Quaternion.LookRotation(a.data.forward)*a.data.impactOffset;return true;}
                foreach(var a in defenseActors)if(a.id==id){position=a.position+Quaternion.LookRotation(a.forward)*a.impactOffset;return true;}
            }
            position=default;return false;
        }

        bool ShieldContains(Vector3 point)
        {
            if(!NereidVessel.Active || Defense.breached && Defense.phase!=DefensePhase.Recovery)return false;
            if(fieldView)return fieldView.ContainsWorld(point);
            Vector3 local=fieldAnchor?fieldAnchor.InverseTransformPoint(point):point-DefenseCenter;
            return new Vector3(local.x/fieldRadii.x,local.y/fieldRadii.y,local.z/fieldRadii.z).sqrMagnitude<1;
        }

        Vector3 FieldContact(Vector3 from,float outwardOffset=0)
        {
            if(fieldView)
            {
                Vector3 localPoint=fieldView.transform.InverseTransformPoint(from);
                localPoint.y=Mathf.Min(localPoint.y,fieldView.RimHeight-.92f);
                return fieldView.ContactWorld(fieldView.transform.TransformPoint(localPoint),outwardOffset);
            }
            Vector3 center=DefenseCenter;
            Quaternion rotation=fieldAnchor?fieldAnchor.rotation:Quaternion.identity;
            var local=fieldAnchor?fieldAnchor.InverseTransformPoint(from):from-center;
            if(local.sqrMagnitude<.01f)local=Vector3.down;
            local.y=Mathf.Min(local.y,-.18f);
            var normalized=new Vector3(local.x/fieldRadii.x,local.y/fieldRadii.y,local.z/fieldRadii.z);
            var surface=local/Mathf.Max(.001f,normalized.magnitude);
            Vector3 contact=fieldAnchor?fieldAnchor.TransformPoint(surface):center+rotation*surface;
            return contact+(contact-center).normalized*outwardOffset;
        }

        void HitField(float amount,Vector3 impact)
        {
            if(Defense.phase!=DefensePhase.Assault || Defense.breached || !Finite(amount) || amount<=0)return;
            var value=Defense;
            value.integrity=Mathf.Max(0,value.integrity-amount);
            value.impact++;
            value.impactAt=DefenseClock;
            value.impactPoint=impact;
            value.impactStrength=Mathf.Clamp(Mathf.Sqrt(amount/8f),.13f,1.25f);
            activeRaid.integrity=value.integrity;
            defenseState.Value=value;
            if(value.integrity>0)return;
            activeRaid.breached=true;
            value.breached=true;value.condition=FieldCondition.Broken;
            var world=RaidWorld.Instance;
            activeRaid.lossLimit=world.DefenseLossLimit(value.severity,value.budget,defenseTuning);
            activeRaid.remainingCarriers=attackers.FindAll(a=>a.data.kind is DefenseSpecies.Thief or DefenseSpecies.Stalker or DefenseSpecies.Abductor).Count;
            defenseState.Value=value;
            foreach(var a in attackers)
            {
                if(a.hooks.Count==0 && a.data.victim<0)SetPose(a,AttackerPose.Carry,0);
            }
            AdvanceDefense(DefensePhase.Resolution,"Поле разрушено! Перехватите воров с рыбой.");
            world.ArchiveDefense();
        }

        void RefreshFieldCondition()
        {
            var value=Defense;
            var status=value.phase==DefensePhase.Recovery?FieldCondition.Restoring:
                value.breached?FieldCondition.Broken:
                value.Ratio<=defenseTuning.critical?FieldCondition.Critical:
                value.Ratio<=defenseTuning.damaged?FieldCondition.Damaged:
                value.impact>0 && DefenseClock-value.impactAt<.85?FieldCondition.UnderAttack:FieldCondition.Normal;
            if(value.condition==status)return;
            value.condition=status;defenseState.Value=value;
        }

        internal void StopDefense() => ResetDefense();
    }
}
