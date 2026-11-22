using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public enum GoshaMode : byte { Waiting, Swimming, Riding, Roaming, Resting, Petted, Fetching, Delivering, Chasing, Pouncing, Escaping, Cradled, Coming, Fishing, BringingFish, GoingHome }
    public enum GoshaBallMode : byte { Deck, Held, Flying, Carried, Tethered }
    public enum GoshaAction : byte { Pet, PickUp, Throw, WindUp, CancelWindUp }

    [System.Serializable]
    public struct GoshaSnapshot
    {
        public GoshaMode Mode;
        public GoshaBallMode Ball;
        public Vector3 Position,Heading,BallPosition;
        public int Rider,Holder,Thrower,Knocked,OrderOwner;
        public uint Pats;
        public byte BallWind;
        public bool Active;
    }

    public sealed partial class RaidWorld
    {
        readonly SyncVar<GoshaSnapshot> gosha=new();
        readonly GoshaDeck deck=new();
        GoshaBallBody ballBody;
        GoshaView goshaView;
        GoshaSnapshot pet;
        float moodTime,ballAge,knockTime,petCooldown,pathClock,blockedTime;
        int patrol;
        bool rodeThroughLock,relocatedThrow;
        Vector3 nextFootstep,lastRouteGoal,lastRiderPosition;
        float idleAtSea;
        float ballWindStarted;
        public GoshaSnapshot Gosha=>gosha.Value;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public uint QaMouthReturns {get;private set;}
        public bool QaBallRelocated=>relocatedThrow;
        // Reproduce the reported resting places without depending on an exact bounce.
        internal void QaStageLooseThrow(DiverMotor thrower,Vector3 origin,Vector3 velocity)
        {
            if(!IsServerInitialized || !HasGoshaBall(thrower))throw new System.InvalidOperationException("Thrower must own the single ball");
            relocatedThrow=false;DropBall(origin,velocity,true);pet.Thrower=thrower.OwnerId;gosha.Value=pet;
        }
#endif

        void StartGosha()
        {
            if(NereidVessel.Active==null)return;
            var go=new GameObject("Gosha's ball (server physics)");go.transform.SetParent(transform,true);
            ballBody=go.AddComponent<GoshaBallBody>();ballBody.Initialize(this);
            gosha.UpdateSettings(new SyncTypeSettings(.05f));
            lastOrderAt=float.NegativeInfinity;
            ResetGosha();
        }
        internal void ResetGosha()
        {
            if(ballBody==null)return;
            ReleaseOrder();clearingSeaDoor=false;
            ClearBallLine();
            pet=new GoshaSnapshot{Active=true,OrderOwner=-1,Rider=-1,Holder=-1,Thrower=-1,Knocked=-1,Heading=Vector3.forward,
                Mode=team.goshaTamed?GoshaMode.Roaming:GoshaMode.Waiting,Position=deck.Mark(7)};
            patrol=7;rodeThroughLock=false;knockTime=petCooldown=0;
            DropBall(deck.Mark(7)+Vector3.right,Vector3.zero,false);
            gosha.Value=pet;
        }
        void StopGosha()
        {
            ReleaseOrder();
            ClearBallLine();
            if(ballBody!=null){Destroy(ballBody.gameObject);ballBody=null;}
        }
        void PresentGosha()
        {
            if(!IsClientInitialized||!gosha.Value.Active)return;
            if(goshaView==null){var go=new GameObject("Гоша и мяч");go.transform.SetParent(transform,true);goshaView=go.AddComponent<GoshaView>();}
        }
        void HideGosha(){if(goshaView!=null){Destroy(goshaView.gameObject);goshaView=null;}}
        public bool HasGoshaBall(DiverMotor d)=>d!=null&&Gosha.Active&&Gosha.Ball==GoshaBallMode.Held&&Gosha.Holder==d.OwnerId;
        public bool IsHoldingGosha(DiverMotor d)=>d!=null&&Gosha.Active&&Gosha.Mode==GoshaMode.Cradled&&Gosha.Rider==d.OwnerId;
        public bool GoshaKnocked(DiverMotor d)=>d!=null&&Gosha.Active&&Gosha.Knocked==d.OwnerId;

        static DiverMotor CrewMember(int owner)
        {
            if(owner<0)return null;
            foreach(var diver in DiverMotor.ServerDivers)
                if(diver!=null && diver.OwnerId==owner && diver.IsServerInitialized)return diver;
            return null;
        }
        static bool Alive(DiverMotor d)=>d!=null && !d.IsDead && !d.IsHelpless;
        bool OnDeck(DiverMotor d)=>Alive(d)&&d.IsInside&&!d.IsTrapped&&deck.Interior(d.transform.position);
        static Vector3 Shoulder(DiverMotor d)=>d.transform.TransformPoint(new Vector3(.25f,.37f,-.31f));
        static Vector3 Hand(DiverMotor d)=>d.transform.position+Vector3.up*.35f+d.transform.right*.23f+d.transform.forward*.42f;
        void SetMode(GoshaMode mode,float duration=0)
        {pet.Mode=mode;moodTime=duration;blockedTime=0;pathClock=0;}
        void TickGosha()
        {
            if(ballBody==null)return;
            float dt=Time.fixedDeltaTime;
            if(pet.BallWind!=0)
                pet.BallWind=(byte)(1+Mathf.RoundToInt(254*ThrowCharge()));
            petCooldown=Mathf.Max(0,petCooldown-dt);
            if(knockTime>0 && (knockTime-=dt)<=0)pet.Knocked=-1;
            if(pet.Knocked>=0 && !OnDeck(CrewMember(pet.Knocked))){pet.Knocked=-1;knockTime=0;}
            TickBall(dt);
            if(!team.goshaTamed)TickMeeting(dt);
            else if(!TickOrder(dt))TickPetDeck(dt);
            if(pet.Ball==GoshaBallMode.Carried)
                pet.BallPosition=pet.Position+pet.Heading*(GoshaView.JawSocket.z*tuning.petScale)+Vector3.up*(GoshaView.JawSocket.y*tuning.petScale-GoshaView.DeckOffset);
            gosha.Value=pet;
        }

        void TickMeeting(float dt)
        {
            var chamber=NereidAirlock.Active;
            var diver=CrewMember(pet.Rider);
            if(!Alive(diver) || (pet.Mode==GoshaMode.Swimming && (diver.IsInside||chamber.Contains(diver.transform.position))))
            {pet.Rider=-1;pet.Mode=GoshaMode.Waiting;rodeThroughLock=false;diver=null;}
            if(diver==null)
            {
                foreach(var candidate in DiverMotor.ServerDivers)
                    if(Alive(candidate)&&!candidate.IsInside&&!chamber.Contains(candidate.transform.position)){diver=candidate;break;}
                if(diver==null)return;
                pet.Rider=diver.OwnerId;idleAtSea=0;lastRiderPosition=diver.transform.position;SetMode(GoshaMode.Swimming);
                // First reveal outside the sea door, in the same water as the expedition.
                pet.Position=chamber.transform.TransformPoint(new Vector3(11,1,-.7f));
                if(!team.goshaMet){team.goshaMet=true;Publish();Save();}
            }
            if(pet.Mode==GoshaMode.Riding)
            {
                pet.Position=Shoulder(diver);pet.Heading=diver.transform.forward;
                if(chamber.Contains(diver.transform.position))rodeThroughLock=true;
                if(!rodeThroughLock)
                {
                    if((diver.transform.position-lastRiderPosition).sqrMagnitude>1){idleAtSea=0;lastRiderPosition=diver.transform.position;}
                    else idleAtSea+=dt;
                    // A motionless rider can be left for an active returning diver nearby.
                    if(idleAtSea>tuning.lostCompanionSeconds)
                        foreach(var other in DiverMotor.ServerDivers)
                            if(other!=diver && Alive(other) && !other.IsInside && !chamber.Contains(other.transform.position) &&
                                Vector3.Distance(other.transform.position,pet.Position)<9 && deck.Clear(pet.Position,Shoulder(other)))
                            {pet.Rider=other.OwnerId;idleAtSea=0;lastRiderPosition=other.transform.position;SetMode(GoshaMode.Swimming);return;}
                }
                if(rodeThroughLock && OnDeck(diver) && chamber.transform.InverseTransformPoint(diver.transform.position).x<-.55f)
                {
                    team.goshaTamed=true;pet.Rider=-1;SetMode(GoshaMode.Roaming);patrol=1;
                    Publish();Save();
                }
                return;
            }
            var goal=Shoulder(diver);
            var old=pet.Position;
            var step=Vector3.MoveTowards(old,goal,dt*(Vector3.Distance(old,goal)>4?tuning.petCatchUpSpeed:tuning.petSwimSpeed));
            if(deck.Clear(old,step,.10f))pet.Position=step;
            else
            {
                // Rise over a ledge or swing around its side without crossing the hull.
                var tangent=Vector3.Cross(Vector3.up,(goal-old).normalized);
                var bypass=old+(Vector3.up+tangent*.8f).normalized*dt*2.8f;
                if(deck.Clear(old,bypass,.1f))pet.Position=bypass;
            }
            Face(pet.Position-old);
            if(Vector3.Distance(pet.Position,goal)<.18f){SetMode(GoshaMode.Riding);rodeThroughLock=false;}
        }

        void Face(Vector3 direction){if(direction.sqrMagnitude>.000001f)pet.Heading=direction.normalized;}
        bool Walk(Vector3 destination,float speed,float dt)
        {
            var ground=deck.Ground(destination);
            var old=pet.Position;
            float floor=deck.Ground(old).y;
            if(old.y>floor+.22f)
            {
                var down=old+Vector3.down*Mathf.Min(old.y-floor,dt*3.2f);
                if(deck.Clear(old,down,.12f)){pet.Position=down;return false;}
            }
            if((pathClock-=dt)<=0 || (lastRouteGoal-ground).sqrMagnitude>.36f || (nextFootstep-old).sqrMagnitude<.016f)
            {
                nextFootstep=deck.Next(old,ground);pathClock=.55f;lastRouteGoal=ground;
            }
            var step=Vector3.MoveTowards(old,nextFootstep,speed*dt);
            step.y=Mathf.MoveTowards(old.y,deck.Ground(step).y,4.5f*dt);
            if(deck.WalkClear(old,step,.13f))pet.Position=step;
            var travel=Vector3.ProjectOnPlane(pet.Position-old,Vector3.up);
            Face(travel);
            blockedTime=travel.sqrMagnitude<.0000001f?blockedTime+dt:0;
            return Vector3.Distance(pet.Position,ground)<.62f && deck.Clear(pet.Position,ground,.045f);
        }
        void PlacePetDown()
        {
            var origin=pet.Position;var forward=-pet.Heading;
            var feet=deck.Ground(origin+forward*.48f);
            if(!deck.Interior(feet)||!deck.Clear(origin,feet,.13f))feet=deck.RecoveryMark(origin);
            pet.Position=feet;pet.Rider=-1;SetMode(GoshaMode.Resting,.65f);
            if(pet.Ball==GoshaBallMode.Deck && pet.Thrower>=0)SetMode(GoshaMode.Fetching,tuning.fetchTimeout);
            if(pet.Ball==GoshaBallMode.Held && pet.Thrower>=0 && pet.Thrower!=pet.Holder)SetMode(GoshaMode.Chasing,20);
        }
        void TickPetDeck(float dt)
        {
            moodTime-=dt;
            if(pet.Mode==GoshaMode.Cradled)
            {
                var keeper=CrewMember(pet.Rider);
                if(!OnDeck(keeper)){PlacePetDown();return;}
                pet.Position=keeper.transform.TransformPoint(new Vector3(0,.18f,.48f));
                pet.Heading=keeper.transform.TransformDirection(Quaternion.Euler(0,145,0)*Vector3.forward);
                return;
            }
            if(pet.Mode==GoshaMode.Petted){if(moodTime<=0)SetMode(GoshaMode.Roaming);return;}
            if(pet.Mode==GoshaMode.Pouncing)
            {
                var victim=CrewMember(pet.Holder);
                if(!OnDeck(victim)||pet.Ball!=GoshaBallMode.Held){CancelFetch();return;}
                Vector3 target=victim.transform.position+Vector3.up*.13f;
                Face(target-pet.Position);pet.Position=Vector3.MoveTowards(pet.Position,target,dt*7.5f);
                if(moodTime>0)return;
                pet.Knocked=victim.OwnerId;knockTime=tuning.playfulFallSeconds;
                TakeBall();SetMode(GoshaMode.Escaping,9);patrol=(pet.Position-deck.Mark(7)).sqrMagnitude>16?7:5;return;
            }
            if(pet.Mode==GoshaMode.Fetching || pet.Mode==GoshaMode.Chasing)
            {
                bool chase=pet.Mode==GoshaMode.Chasing;
                var victim=CrewMember(pet.Holder);
                if((chase && (!OnDeck(victim)||pet.Ball!=GoshaBallMode.Held)) || (!chase && pet.Ball!=GoshaBallMode.Deck)){CancelFetch();return;}
                var target=chase?victim.transform.position:pet.BallPosition;
                if(!deck.Interior(target) || moodTime<=0 || blockedTime>2.2f)
                {
                    if(!chase && RecoverFetch())return;
                    CancelFetch();return;
                }
                if(!Walk(target,chase?tuning.petChaseSpeed:tuning.petFetchSpeed,dt))return;
                if(!chase && (Mathf.Abs(target.y-pet.Position.y)>.65f || !deck.Clear(pet.Position,target,.035f)))return;
                Face(Vector3.ProjectOnPlane(target-pet.Position,Vector3.up));
                if(chase)SetMode(GoshaMode.Pouncing,.22f);
                else{TakeBall();SetMode(GoshaMode.Delivering,20);}
                return;
            }
            if(pet.Mode==GoshaMode.Delivering || pet.Mode==GoshaMode.Escaping)
            {
                var recipient=CrewMember(pet.Thrower);
                bool returning=pet.Mode==GoshaMode.Delivering;
                if(returning && !OnDeck(recipient)){CancelFetch();return;}
                var target=returning?recipient.transform.position:deck.Mark(patrol);
                if(!Walk(target,returning?3.8f:5.1f,dt) && moodTime>0)return;
                if(returning&&Vector3.Distance(deck.Ground(recipient.transform.position),pet.Position)<1 && CanHold(recipient))
                    GiveBall(recipient);
                else DropBall(pet.Position+Vector3.up*.12f,Vector3.zero,false);
                SetMode(GoshaMode.Resting,2.2f);return;
            }
            if(pet.Mode==GoshaMode.Resting)
            {
                var nearby=NearestDeckCrew(3.2f);
                if(nearby!=null)Face(Vector3.ProjectOnPlane(nearby.transform.position-pet.Position,Vector3.up));
                if(moodTime>0)return;
                SetMode(GoshaMode.Roaming);
            }
            // Alternate the safe circuit with a short approach to a nearby crew member.
            var follower=patrol%3==0?NearestDeckCrew(3):null;
            if(Walk(follower!=null?follower.transform.position:deck.Mark(patrol),tuning.petCrawlSpeed,dt)||blockedTime>2)
            {patrol=(patrol+1)%deck.Count;SetMode(GoshaMode.Resting,patrol%2==0?2:1.2f);}
        }
        DiverMotor NearestDeckCrew(float range)
        {
            DiverMotor best=null;
            foreach(var d in DiverMotor.ServerDivers)
                if(OnDeck(d)){float dist=Vector3.Distance(d.transform.position,pet.Position);if(dist<range){best=d;range=dist;}}
            return best;
        }
        bool RecoverFetch()
        {
            if(!PetAvailableForBall || relocatedThrow || !team.goshaTamed || !OnDeck(CrewMember(pet.Thrower)))return false;
            int thrower=pet.Thrower;
            DropBall(deck.RecoveryMark(pet.BallPosition)+Vector3.up*.12f,Vector3.zero,false);
            relocatedThrow=true;pet.Thrower=thrower;SetMode(GoshaMode.Fetching,tuning.fetchTimeout);
            return true;
        }
        void BeginBallRecovery()
        {
            pet.Thrower=-1;relocatedThrow=false;
            if(team!=null && team.goshaTamed && PetAvailableForBall)
                SetMode(GoshaMode.Roaming);
        }
        void CancelFetch()
        {
            var at=deck.Interior(deck.Ground(pet.Position))?deck.Ground(pet.Position):deck.Mark(7);
            if(pet.Ball==GoshaBallMode.Carried || pet.Mode==GoshaMode.Chasing || pet.Mode==GoshaMode.Pouncing ||
                (pet.Mode==GoshaMode.Fetching && pet.Ball==GoshaBallMode.Deck))
                DropBall(at+Vector3.up*.18f,Vector3.zero,false);
            pet.Thrower=-1;SetMode(GoshaMode.Roaming);
        }
        void TakeBall(){pet.BallWind=0;ballBody.Hold();pet.Ball=GoshaBallMode.Carried;pet.Holder=-1;}
        void GiveBall(DiverMotor d){pet.BallWind=0;ballBody.Hold();pet.Ball=GoshaBallMode.Held;pet.Holder=d.OwnerId;pet.BallPosition=Hand(d);}
        void DropBall(Vector3 at,Vector3 speed,bool thrown)
        {
            pet.BallWind=0;
            pet.Ball=thrown?GoshaBallMode.Flying:GoshaBallMode.Deck;pet.Holder=-1;pet.BallPosition=at;ballAge=0;
            if(!thrown)pet.Thrower=-1;
            ballBody.Release(at,speed);
        }

        void TickBall(float dt)
        {
            ballAge+=dt;
            if(pet.Ball==GoshaBallMode.Tethered){TickBallLine(dt);return;}
            if(pet.Ball==GoshaBallMode.Held)
            {
                var holder=CrewMember(pet.Holder);
                if(Alive(holder)){pet.BallPosition=Hand(holder);return;}
                var last=deck.Ground(pet.BallPosition);
                DropBall(deck.InSea(pet.BallPosition)?pet.BallPosition:deck.Interior(last)?last:deck.RecoveryMark(last),Vector3.zero,false);return;
            }
            if(pet.Ball==GoshaBallMode.Carried)return;
            pet.BallPosition=ballBody.Position;
            if(deck.Mouth(pet.BallPosition))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                QaMouthReturns++;
#endif
                // The beast spits the same physical ball from its lips back into the main aisle.
                var hull=NereidVessel.Active.transform;
                var p=hull.InverseTransformPoint(pet.BallPosition);
                var lip=hull.TransformPoint(new Vector3(Mathf.Clamp(p.x,-.8f,.8f),1.15f,6.30f));
                int thrower=pet.Thrower;
                DropBall(lip,-hull.forward*4.8f+Vector3.up*2.4f,thrower>=0);
                pet.Thrower=thrower;return;
            }
            bool atSea=deck.InSea(pet.BallPosition);
            ballBody.SetWater(atSea);
            if(atSea || NereidAirlock.Active.Contains(pet.BallPosition))
            {
                // The open ocean and the pressure lock are usable places for a diver or a harpoon.
                // Only a lost ball below the world requires a final return to the deck.
                if(pet.BallPosition.y<NereidVessel.Active.transform.position.y-80)
                    DropBall(deck.Mark(7),Vector3.zero,false);
                else if(pet.Ball==GoshaBallMode.Flying && ballAge>2.4f)pet.Ball=GoshaBallMode.Deck;
                if(pet.Mode==GoshaMode.Fetching)BeginBallRecovery();
                return;
            }
            if(!deck.Interior(pet.BallPosition) && (pet.BallPosition.y<deck.Mark(0).y-.6f || ballAge>1.2f))
            {
                if(RecoverFetch())return;
                var safe=deck.RecoveryMark(pet.BallPosition);
                DropBall(safe+Vector3.up*.22f,Vector3.zero,false);BeginBallRecovery();return;
            }
            if(pet.Ball==GoshaBallMode.Flying && (ballAge>3.4f || (ballAge>.2f && ballBody.TouchedDeck)))
            {
                pet.Ball=GoshaBallMode.Deck;
                if(team.goshaTamed&&pet.Thrower>=0&&PetAvailableForBall)SetMode(GoshaMode.Fetching,tuning.fetchTimeout);
            }
        }
        internal void BallTouchedDiver(DiverMotor target)
        {
            if(!IsServerInitialized || pet.Ball!=GoshaBallMode.Flying || target.OwnerId==pet.Thrower)return;
            if(!OnDeck(target)||!CanHold(target)||pet.Knocked==target.OwnerId||IsHoldingGosha(target))
            {
                // Let the actual collision leave the ball beside an unavailable receiver.
                // The deck recovery and buoyancy rules continue to govern this same loose ball.
                pet.Ball=GoshaBallMode.Deck;
                BeginBallRecovery();
                return;
            }
            GiveBall(target);
            if(team.goshaTamed && PetAvailableForBall)SetMode(GoshaMode.Chasing,20);
        }
        static bool CanHold(DiverMotor d)=>d.TryGetComponent<HarpoonTool>(out var tool)&&!tool.HandsFull&&tool.HookedFish==null&&!tool.TetherActive&&!tool.HasBallLine;
        float ThrowCharge() => Mathf.Clamp01((Time.time-ballWindStarted)/Mathf.Max(.1f,tuning.ballWindSeconds));

        static bool RestingPet(GoshaMode mode)=>mode==GoshaMode.Roaming||mode==GoshaMode.Resting||mode==GoshaMode.Petted;
        public GoshaAction? GoshaContext(DiverMotor d,Vector3 eye,Vector3 forward)
        {
            if(!Gosha.Active || !Alive(d) || d.IsTrapped || GoshaKnocked(d))return null;
            if(IsHoldingGosha(d))return GoshaAction.Pet;
            var s=Gosha;
            bool ball=s.Ball==GoshaBallMode.Deck && CanHold(d) && InReach(d,s.BallPosition);
            bool touch=OnDeck(d)&&RestingPet(s.Mode)&&CanHold(d)&&!HasGoshaBall(d)&&InReach(d,s.Position);
            if(ball && (!touch || Vector3.Dot((s.BallPosition-eye).normalized,forward)>Vector3.Dot((s.Position-eye).normalized,forward)))return GoshaAction.PickUp;
            if(touch)return GoshaAction.Pet;
            return null;
        }
        bool InReach(DiverMotor d,Vector3 at)=>Vector3.Distance(d.transform.position,at)<tuning.petReach && deck.Clear(d.transform.position+Vector3.up*.25f,at,.035f);
        [ServerRpc(RequireOwnership=false)]
        public void RequestGosha(GoshaAction action,NetworkConnection sender=null)
        {
            if(sender?.FirstObject==null || !pet.Active || transitionRemaining>0)return;
            var d=sender.FirstObject.GetComponent<DiverMotor>();
            if(!Alive(d) || d.IsTrapped || pet.Knocked==d.OwnerId)return;
            if(action==GoshaAction.WindUp || action==GoshaAction.CancelWindUp || action==GoshaAction.Throw)
            {
                if(pet.Ball!=GoshaBallMode.Held || pet.Holder!=d.OwnerId ||
                    pet.Mode==GoshaMode.Chasing || pet.Mode==GoshaMode.Pouncing)return;
                if(action==GoshaAction.WindUp && pet.BallWind==0)
                {
                    ballWindStarted=Time.time;
                    pet.BallWind=1;
                }
                else if(action==GoshaAction.CancelWindUp)pet.BallWind=0;
                else if(action==GoshaAction.Throw && pet.BallWind!=0)
                {
                    float force=Mathf.Lerp(tuning.ballShortThrow,tuning.ballThrowSpeed,ThrowCharge());
                    pet.BallWind=0;
                    var direction=d.AimDirection.normalized;
                    var start=Hand(d)+direction*.20f;
                    if(deck.Clear(d.transform.position+Vector3.up*.32f,start,.15f))
                    {
                        relocatedThrow=false;
                        DropBall(start,direction*force+Vector3.up*tuning.ballThrowLift,true);
                        pet.Thrower=d.OwnerId;
                    }
                }
                gosha.Value=pet;
                return;
            }

            if(action==GoshaAction.Pet)
            {
                if(!team.goshaTamed || petCooldown>0)return;
                if(pet.Mode==GoshaMode.Cradled)
                {
                    if(pet.Rider!=d.OwnerId)return;
                    PlacePetDown();petCooldown=.32f;
                }
                else
                {
                    if(!OnDeck(d)||!RestingPet(pet.Mode)||!CanHold(d)||HasGoshaBall(d)||!InReach(d,pet.Position))return;
                    SetMode(GoshaMode.Cradled);pet.Rider=d.OwnerId;petCooldown=tuning.patCooldown;
                    pet.Position=d.transform.TransformPoint(new Vector3(0,.18f,.48f));
                    pet.Heading=d.transform.TransformDirection(Quaternion.Euler(0,145,0)*Vector3.forward);
                    pet.Pats++;d.ConfirmInteraction();
                }
            }
            else if(action==GoshaAction.PickUp)
            {
                if(IsHoldingGosha(d)||pet.Ball!=GoshaBallMode.Deck || !CanHold(d) || !InReach(d,pet.BallPosition))return;
                GiveBall(d);pet.Thrower=-1;
                if(team.goshaTamed && PetAvailableForBall)SetMode(GoshaMode.Resting,.5f);
                d.ConfirmInteraction();
            }
            gosha.Value=pet;
        }
    }
}
