using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public enum GoshaMode : byte { Waiting, Swimming, Riding, Roaming, Resting, Petted, Fetching, Delivering, Chasing, Pouncing, Escaping }
    public enum GoshaBallMode : byte { Deck, Held, Flying, Carried }
    public enum GoshaAction : byte { Pet, PickUp, Throw }

    public struct GoshaSnapshot
    {
        public GoshaMode Mode;
        public GoshaBallMode Ball;
        public Vector3 Position,Heading,BallPosition;
        public int Rider,Holder,Thrower,Knocked;
        public uint Pats;
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
        bool rodeThroughLock;
        Vector3 nextFootstep,lastRouteGoal;
        public GoshaSnapshot Gosha=>gosha.Value;

        void StartGosha()
        {
            if(NereidVessel.Active==null)return;
            var go=new GameObject("Gosha's ball (server physics)");go.transform.SetParent(transform,true);
            ballBody=go.AddComponent<GoshaBallBody>();ballBody.Initialize(this);
            gosha.UpdateSettings(new SyncTypeSettings(.05f));
            ResetGosha();
        }
        void ResetGosha()
        {
            if(ballBody==null)return;
            pet=new GoshaSnapshot{Active=true,Rider=-1,Holder=-1,Thrower=-1,Knocked=-1,Heading=Vector3.forward,
                Mode=team.goshaTamed?GoshaMode.Roaming:GoshaMode.Waiting,Position=deck.Mark(7)};
            patrol=7;rodeThroughLock=false;knockTime=petCooldown=0;
            DropBall(deck.Mark(7)+Vector3.right,Vector3.zero,false);
            gosha.Value=pet;
        }
        void StopGosha()
        {
            if(ballBody!=null){Destroy(ballBody.gameObject);ballBody=null;}
        }
        void PresentGosha()
        {
            if(!IsClientInitialized||!gosha.Value.Active)return;
            if(goshaView==null){var go=new GameObject("Гоша и мяч");go.transform.SetParent(transform,true);goshaView=go.AddComponent<GoshaView>();}
            goshaView.Show(gosha.Value);
        }
        void HideGosha(){if(goshaView!=null){Destroy(goshaView.gameObject);goshaView=null;}}
        public bool HasGoshaBall(DiverMotor d)=>d!=null&&Gosha.Active&&Gosha.Ball==GoshaBallMode.Held&&Gosha.Holder==d.OwnerId;
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
        static Vector3 Shoulder(DiverMotor d)=>d.transform.TransformPoint(new Vector3(.23f,.37f,-.22f));
        static Vector3 Hand(DiverMotor d)=>d.transform.position+Vector3.up*.35f+d.transform.right*.23f+d.transform.forward*.42f;
        void SetMode(GoshaMode mode,float duration=0)
        {pet.Mode=mode;moodTime=duration;blockedTime=0;pathClock=0;}
        void TickGosha()
        {
            if(ballBody==null)return;
            float dt=Time.fixedDeltaTime;
            petCooldown=Mathf.Max(0,petCooldown-dt);
            if(knockTime>0 && (knockTime-=dt)<=0)pet.Knocked=-1;
            if(pet.Knocked>=0 && !OnDeck(CrewMember(pet.Knocked))){pet.Knocked=-1;knockTime=0;}
            TickBall(dt);
            if(!team.goshaTamed)TickMeeting(dt);
            else TickPetDeck(dt);
            if(pet.Ball==GoshaBallMode.Carried)
                pet.BallPosition=pet.Position+pet.Heading*.27f+Vector3.up*.09f;
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
                pet.Rider=diver.OwnerId;SetMode(GoshaMode.Swimming);
                // First reveal outside the sea door, in the same water as the expedition.
                pet.Position=chamber.transform.TransformPoint(new Vector3(11,1,-.7f));
                if(!team.goshaMet){team.goshaMet=true;Publish();Save();}
            }
            if(pet.Mode==GoshaMode.Riding)
            {
                pet.Position=Shoulder(diver);pet.Heading=diver.transform.forward;
                if(chamber.Contains(diver.transform.position))rodeThroughLock=true;
                if(rodeThroughLock && OnDeck(diver) && chamber.transform.InverseTransformPoint(diver.transform.position).x<-.55f)
                {
                    team.goshaTamed=true;pet.Rider=-1;SetMode(GoshaMode.Roaming);patrol=1;
                    Publish();Save();
                }
                return;
            }
            var goal=Shoulder(diver);
            var old=pet.Position;
            var step=Vector3.MoveTowards(old,goal,dt*(Vector3.Distance(old,goal)>4?6.2f:3.8f));
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
            if((pathClock-=dt)<=0 || (lastRouteGoal-ground).sqrMagnitude>1)
            {
                nextFootstep=deck.Next(old,ground);pathClock=.18f;lastRouteGoal=ground;
            }
            var step=Vector3.MoveTowards(old,nextFootstep,speed*dt);
            if(deck.Clear(old,step,.13f))pet.Position=step;
            Face(Vector3.ProjectOnPlane(pet.Position-old,Vector3.up));
            blockedTime=(pet.Position-old).sqrMagnitude<.0000001f?blockedTime+dt:0;
            return Vector3.Distance(pet.Position,ground)<.46f;
        }
        void TickPetDeck(float dt)
        {
            moodTime-=dt;
            if(pet.Mode==GoshaMode.Petted){if(moodTime<=0)SetMode(GoshaMode.Roaming);return;}
            if(pet.Mode==GoshaMode.Pouncing)
            {
                var victim=CrewMember(pet.Holder);
                if(!OnDeck(victim)||pet.Ball!=GoshaBallMode.Held){CancelFetch();return;}
                Vector3 target=victim.transform.position+Vector3.up*.13f;
                Face(target-pet.Position);pet.Position=Vector3.MoveTowards(pet.Position,target,dt*7.5f);
                if(moodTime>0)return;
                pet.Knocked=victim.OwnerId;knockTime=1.2f;
                TakeBall();SetMode(GoshaMode.Escaping,9);patrol=(pet.Position-deck.Mark(7)).sqrMagnitude>16?7:5;return;
            }
            if(pet.Mode==GoshaMode.Fetching || pet.Mode==GoshaMode.Chasing)
            {
                bool chase=pet.Mode==GoshaMode.Chasing;
                var victim=CrewMember(pet.Holder);
                if((chase && (!OnDeck(victim)||pet.Ball!=GoshaBallMode.Held)) || (!chase && pet.Ball!=GoshaBallMode.Deck)){CancelFetch();return;}
                var target=chase?victim.transform.position:pet.BallPosition;
                if(!deck.Interior(target) || moodTime<=0){CancelFetch();return;}
                if(!Walk(target,chase?5.5f:4.3f,dt))return;
                if(!chase && Mathf.Abs(target.y-pet.Position.y)>.55f)return;
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
            if(Walk(follower!=null?follower.transform.position:deck.Mark(patrol),1.45f,dt)||blockedTime>2)
            {patrol=(patrol+1)%deck.Count;SetMode(GoshaMode.Resting,patrol%2==0?2:1.2f);}
        }
        DiverMotor NearestDeckCrew(float range)
        {
            DiverMotor best=null;
            foreach(var d in DiverMotor.ServerDivers)
                if(OnDeck(d)){float dist=Vector3.Distance(d.transform.position,pet.Position);if(dist<range){best=d;range=dist;}}
            return best;
        }
        void CancelFetch()
        {
            var at=deck.Interior(deck.Ground(pet.Position))?deck.Ground(pet.Position):deck.Mark(7);
            if(pet.Ball==GoshaBallMode.Carried || pet.Mode==GoshaMode.Chasing || pet.Mode==GoshaMode.Pouncing ||
                (pet.Mode==GoshaMode.Fetching && pet.Ball==GoshaBallMode.Deck))
                DropBall(at+Vector3.up*.18f,Vector3.zero,false);
            pet.Thrower=-1;SetMode(GoshaMode.Roaming);
        }
        void TakeBall(){ballBody.Hold();pet.Ball=GoshaBallMode.Carried;pet.Holder=-1;}
        void GiveBall(DiverMotor d){ballBody.Hold();pet.Ball=GoshaBallMode.Held;pet.Holder=d.OwnerId;pet.BallPosition=Hand(d);}
        void DropBall(Vector3 at,Vector3 speed,bool thrown)
        {
            pet.Ball=thrown?GoshaBallMode.Flying:GoshaBallMode.Deck;pet.Holder=-1;pet.BallPosition=at;ballAge=0;
            if(!thrown)pet.Thrower=-1;
            ballBody.Release(at,speed);
        }

        void TickBall(float dt)
        {
            ballAge+=dt;
            if(pet.Ball==GoshaBallMode.Held)
            {
                var holder=CrewMember(pet.Holder);
                if(OnDeck(holder)){pet.BallPosition=Hand(holder);return;}
                var last=deck.Ground(pet.BallPosition);
                DropBall(deck.Interior(last)?last:deck.Mark(7),Vector3.zero,false);return;
            }
            if(pet.Ball==GoshaBallMode.Carried)return;
            pet.BallPosition=ballBody.Position;
            if(!deck.Interior(pet.BallPosition) && (pet.BallPosition.y<deck.Mark(0).y-1 || ballAge>4))
            {DropBall(deck.Mark(7),Vector3.zero,false);if(team.goshaTamed)SetMode(GoshaMode.Roaming);return;}
            if(pet.Ball==GoshaBallMode.Flying && (ballAge>3.4f || (ballAge>.22f && ballBody.Velocity.magnitude<1.4f)))
            {
                pet.Ball=GoshaBallMode.Deck;
                if(team.goshaTamed)SetMode(GoshaMode.Fetching,24);
            }
        }
        internal void BallTouchedDiver(DiverMotor target)
        {
            if(!IsServerInitialized || pet.Ball!=GoshaBallMode.Flying || target.OwnerId==pet.Thrower)return;
            if(!OnDeck(target)||!CanHold(target)||pet.Knocked==target.OwnerId)
            {DropBall(deck.Interior(pet.BallPosition)?deck.Ground(pet.BallPosition):deck.Mark(7),Vector3.zero,false);return;}
            GiveBall(target);
            if(team.goshaTamed)SetMode(GoshaMode.Chasing,20);
        }
        static bool CanHold(DiverMotor d)=>d.TryGetComponent<HarpoonTool>(out var tool)&&!tool.HandsFull&&tool.HookedFish==null&&!tool.TetherActive;
        static bool RestingPet(GoshaMode mode)=>mode==GoshaMode.Roaming||mode==GoshaMode.Resting||mode==GoshaMode.Petted;
        public GoshaAction? GoshaContext(DiverMotor d,Vector3 eye,Vector3 forward)
        {
            if(!Gosha.Active || !OnDeck(d) || GoshaKnocked(d))return null;
            var s=Gosha;
            bool ball=s.Ball==GoshaBallMode.Deck && CanHold(d) && InReach(d,s.BallPosition);
            bool touch=RestingPet(s.Mode)&&InReach(d,s.Position);
            if(ball && (!touch || Vector3.Dot((s.BallPosition-eye).normalized,forward)>Vector3.Dot((s.Position-eye).normalized,forward)))return GoshaAction.PickUp;
            if(touch)return GoshaAction.Pet;
            return null;
        }
        bool InReach(DiverMotor d,Vector3 at)=>Vector3.Distance(d.transform.position,at)<2.05f && deck.Clear(d.transform.position+Vector3.up*.25f,at,.035f);
        [ServerRpc(RequireOwnership=false)]
        public void RequestGosha(GoshaAction action,NetworkConnection sender=null)
        {
            if(sender?.FirstObject==null || !pet.Active || transitionRemaining>0)return;
            var d=sender.FirstObject.GetComponent<DiverMotor>();
            if(!OnDeck(d)||pet.Knocked==d.OwnerId)return;
            if(action==GoshaAction.Pet)
            {
                if(!team.goshaTamed || !RestingPet(pet.Mode) || petCooldown>0 || !InReach(d,pet.Position))return;
                Face(Vector3.ProjectOnPlane(d.transform.position-pet.Position,Vector3.up));
                petCooldown=1.35f;SetMode(GoshaMode.Petted,1.25f);pet.Pats++;d.ConfirmInteraction();
            }
            else if(action==GoshaAction.PickUp)
            {
                if(pet.Ball!=GoshaBallMode.Deck || !CanHold(d) || !InReach(d,pet.BallPosition))return;
                GiveBall(d);pet.Thrower=-1;
                if(team.goshaTamed)SetMode(GoshaMode.Resting,.5f);
                d.ConfirmInteraction();
            }
            else if(action==GoshaAction.Throw)
            {
                if(pet.Ball!=GoshaBallMode.Held || pet.Holder!=d.OwnerId || pet.Mode==GoshaMode.Chasing || pet.Mode==GoshaMode.Pouncing)return;
                var direction=d.AimDirection.normalized;
                var start=Hand(d)+direction*.20f;
                if(!deck.Clear(d.transform.position+Vector3.up*.32f,start,.15f))return;
                DropBall(start,direction*8.4f+Vector3.up*1.65f,true);pet.Thrower=d.OwnerId;d.ConfirmInteraction();
            }
            gosha.Value=pet;
        }
    }
}
