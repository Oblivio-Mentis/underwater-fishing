using FishNet.Connection;
using FishNet.Object;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public enum GoshaOrder : byte { Come, CatchFish, Follow }

    public sealed partial class RaidWorld
    {
        DiverMotor orderDiver;
        FishActor orderFish;
        long orderFishId;
        string orderProfile;
        bool followingOrder, clearingSeaDoor;
        float caution, lastOrderAt = float.NegativeInfinity, orderEnds, pocketWait;
        Vector3 fishWaterPosition;
        readonly GoshaSwimRoute swimRoute = new();

        static bool Training(GoshaMode mode) => mode is GoshaMode.Coming or GoshaMode.Fishing or GoshaMode.BringingFish or GoshaMode.GoingHome;
        bool PetAvailableForBall => pet.Mode!=GoshaMode.Cradled && !Training(pet.Mode);

        public string GoshaTrainingHint(DiverMotor diver)
        {
            if(diver==null || !Gosha.Active || View?.goshaTamed!=true || !diver.IsOwner || LocalProgress==null)return "";
            bool recall=SkillTreeDefinition.Rank(LocalProgress.skills,SkillLine.Recall)>0;
            bool fetch=SkillTreeDefinition.Rank(LocalProgress.skills,SkillLine.Fetch)>0;
            bool follow=SkillTreeDefinition.Rank(LocalProgress.skills,SkillLine.Faithful)>0;
            if(!recall && !fetch && !follow)return "";
            if(Training(Gosha.Mode) && Gosha.Mode!=GoshaMode.GoingHome)
            {
                if(Gosha.OrderOwner!=diver.OwnerId)return "Гоша выполняет поручение напарника";
                return Gosha.Mode switch
                {
                    GoshaMode.Coming => "Гоша идёт к вам",
                    GoshaMode.Fishing => "Гоша ищет рыбу",
                    _ => "Гоша несёт вашу рыбу"
                };
            }
            string hint=recall?"G — ко мне":"";
            if(fetch)hint+=(hint.Length>0?"  ·  ":"")+"H — принести рыбку";
            if(follow)hint+=(hint.Length>0?"  ·  ":"")+"J — сопровождение";
            return hint;
        }

        internal float OrderCooldown(DiverMotor diver) =>
            tuning.petOrderCooldown*(1-Mathf.Clamp(SkillFactor(diver,SkillLine.Recall)+.5f*SkillFactor(diver,SkillLine.Delivery),0,.45f));
        internal float OrderRadius(DiverMotor diver) => tuning.petSearchRadius*(1+SkillFactor(diver,SkillLine.Fetch));
        internal float OrderRemaining(DiverMotor diver) => Mathf.Max(0,lastOrderAt+OrderCooldown(diver)-Time.time);
        float OrderSpeed =>
            (1+SkillFactor(orderDiver,SkillLine.GoshaSpeed))*
            (1+(pet.Mode==GoshaMode.BringingFish?SkillFactor(orderDiver,SkillLine.Delivery):0))*
            (1+(followingOrder?SkillFactor(orderDiver,SkillLine.Faithful):0));

        [ServerRpc(RequireOwnership=false)]
        public void RequestGoshaOrder(GoshaOrder order,NetworkConnection sender=null)
        {
            if(sender?.FirstObject==null)return;
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(diver==null || diver.Owner!=sender || !Alive(diver) || diver.IsTrapped || transitionRemaining>0)return;
            if(!pet.Active || team?.goshaTamed!=true){OrderReply(sender,"Сначала подружитесь с Гошей и верните его на борт");return;}
            bool catchFish=order==GoshaOrder.CatchFish;
            bool follow=order==GoshaOrder.Follow;
            if(order!=GoshaOrder.Come && !catchFish && !follow)return;
            string profile=ProgressKey(diver);
            var required=catchFish?SkillLine.Fetch:follow?SkillLine.Faithful:SkillLine.Recall;
            if(string.IsNullOrEmpty(profile) || !HasSkill(diver,required))
            {OrderReply(sender,"Эта команда ещё не изучена");return;}
            if(follow && followingOrder && orderDiver==diver){EndOrder("Сопровождение завершено");return;}
            if((!RestingPet(pet.Mode) && pet.Mode!=GoshaMode.GoingHome) || pet.Ball==GoshaBallMode.Carried ||
                (pet.Ball==GoshaBallMode.Flying && pet.Thrower>=0))
            {OrderReply(sender,"Гоша занят");return;}
            float remaining=OrderRemaining(diver);
            if(remaining>0){OrderReply(sender,"Гоша отдыхает · ещё "+Mathf.CeilToInt(remaining)+" с");return;}
            var tool=diver.GetComponent<HarpoonTool>();
            if(catchFish && (tool==null || tool.HasFullCreel))
            {OrderReply(sender,"Освободите карман для улова");return;}
            float riskTolerance=SkillFactor(diver,SkillLine.SafeSwim);
            if(catchFish)
            {
                var origin=SwimZone(diver.transform.position)==PetZone.Sea?diver.transform.position:LockPoint(11.2f);
                orderFish=ClosestFish(origin,OrderRadius(diver),riskTolerance);
                if(orderFish==null || !orderFish.TryReserveForGosha())
                {orderFish=null;OrderReply(sender,"Поблизости нет доступной малой рыбы");return;}
                orderFishId=orderFish.CargoId;fishWaterPosition=orderFish.transform.position;
            }
            lastOrderAt=Time.time;orderEnds=Time.time+(follow?Mathf.Min(24f,tuning.petOrderTimeout):tuning.petOrderTimeout);pocketWait=0;
            orderDiver=diver;orderProfile=profile;caution=riskTolerance;followingOrder=follow;pet.OrderOwner=diver.OwnerId;
            swimRoute.Reset();SetMode(catchFish?GoshaMode.Fishing:GoshaMode.Coming);
            gosha.Value=pet;
            OrderReply(sender,catchFish?"Гоша отправился за рыбой":follow?"Гоша сопровождает вас":"Гоша идёт к вам");
        }

        FishActor ClosestFish(Vector3 origin,float radius,float riskTolerance)
        {
            FishActor nearest=null;float best=radius*radius;
            foreach(var f in FishActor.ServerSchool)
            {
                if(!f.IsAvailableForGosha)continue;
                Vector3 at=f.transform.position;float distance=(at-origin).sqrMagnitude;
                if(distance>best || SwimZone(at)!=PetZone.Sea ||
                    (riskTolerance>0 && GoshaSwimRoute.Risk(at)>GoshaSwimRoute.RiskLimit(riskTolerance)))continue;
                nearest=f;best=distance;
            }
            return nearest;
        }

        [TargetRpc] void OrderReply(NetworkConnection recipient,string message)
        {
            UI.UiRoot.Instance?.Toast(message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastGoshaOrderReply=message;
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string LastGoshaOrderReply {get;private set;}
        internal FishActor TrialReservedFish=>orderFish;
        internal void PreparePetOrderTrial(DiverMotor host,DiverMotor guest)
        {
            team.goshaMet=team.goshaTamed=true;ResetGosha();
            team.Player(ProgressKey(host)).skills.Add(SkillTreeDefinition.Id(SkillLine.Recall,1));
            var expert=team.Player(ProgressKey(guest)).skills;
            foreach(var line in new[]{SkillLine.Recall,SkillLine.Fetch,SkillLine.SafeSwim,SkillLine.Delivery})
                for(int rank=1;rank<=5;rank++)expert.Add(SkillTreeDefinition.Id(line,rank));
            PublishProfiles();Publish();
        }
#endif
        void ReleaseOrder()
        {
            if(orderFish!=null && orderFish.CargoId==orderFishId)
            {
                if(pet.Mode==GoshaMode.BringingFish && NereidAirlock.Active!=null && NereidVessel.Active!=null && SwimZone(orderFish.transform.position)!=PetZone.Sea)
                    orderFish.CarryForGosha(fishWaterPosition,orderFish.transform.rotation);
                orderFish.ReleaseGoshaReservation();
            }
            orderFish=null;orderDiver=null;orderProfile=null;orderFishId=0;pet.OrderOwner=-1;
            followingOrder=false;caution=0;
        }
        void EndOrder(string response)
        {
            if(orderDiver!=null && orderDiver.Owner!=null && orderDiver.Owner.IsActive)OrderReply(orderDiver.Owner,response);
            float returnCaution=caution;
            ReleaseOrder();swimRoute.Reset();
            if(SwimZone(pet.Position)==PetZone.Deck)ResumeDeck();
            else {caution=returnCaution;SetMode(GoshaMode.GoingHome);}
        }
        void ResumeDeck()
        {
            caution=0;
            if(pet.Ball==GoshaBallMode.Deck && pet.Thrower>=0 && deck.Interior(pet.BallPosition))SetMode(GoshaMode.Fetching,tuning.fetchTimeout);
            else if(pet.Ball==GoshaBallMode.Held && pet.Thrower>=0 && pet.Holder!=pet.Thrower && OnDeck(CrewMember(pet.Holder)))SetMode(GoshaMode.Chasing,20);
            else SetMode(GoshaMode.Resting,2);
        }
        bool TickOrder(float dt)
        {
            if(!Training(pet.Mode))return false;
            if(pet.Mode==GoshaMode.GoingHome)
            {
                if(TravelTo(deck.Mark(0),dt))ResumeDeck();
                return true;
            }
            if(!Alive(orderDiver) || orderDiver.IsTrapped || orderDiver.Owner==null || !orderDiver.Owner.IsActive ||
                Time.time>orderEnds || transitionRemaining>0)
            {EndOrder(followingOrder?"Сопровождение завершено":"Гоша вернулся без улова");return true;}
            if(pet.Mode==GoshaMode.Coming)
            {
                if(followingOrder)
                {
                    var caller=orderDiver.transform.position;
                    if((pet.Position-caller).sqrMagnitude>2.72f || SwimZone(pet.Position)!=SwimZone(caller))TravelTo(caller,dt);
                    return true;
                }
                if(TravelTo(orderDiver.transform.position,dt))EndOrder("Гоша рядом");
                return true;
            }
            if(orderFish==null || orderFish.CargoId!=orderFishId || !orderFish.InHabitat)
            {EndOrder("Гоша потерял след рыбы");return true;}
            if(pet.Mode==GoshaMode.Fishing)
            {
                if(!TravelTo(orderFish.transform.position,dt))return true;
                fishWaterPosition=orderFish.transform.position;
                SetMode(GoshaMode.BringingFish);swimRoute.Reset();
            }
            bool reached=TravelTo(orderDiver.transform.position,dt);
            Vector3 direction=pet.Heading.sqrMagnitude>.01f?pet.Heading:Vector3.forward;
            Vector3 jaws=pet.Position+direction*(GoshaView.JawSocket.z*tuning.petScale)+Vector3.up*(GoshaView.JawSocket.y*tuning.petScale);
            if(SwimZone(jaws)==PetZone.Sea)fishWaterPosition=jaws;
            orderFish.CarryForGosha(jaws,Quaternion.LookRotation(direction));
            if(!reached){pocketWait=0;return true;}
            var tool=orderDiver.GetComponent<HarpoonTool>();
            if(tool!=null && !tool.HasFullCreel && orderFish.CollectForGosha(tool,orderProfile))
            {orderFish=null;EndOrder("Гоша принёс рыбу · улов в вашем кармане");return true;}
            if(pocketWait<=0)OrderReply(orderDiver.Owner,"Гоша рядом с рыбой · освободите карман");
            pocketWait+=dt;
            if(pocketWait>=tuning.petPocketWait*(1+SkillFactor(orderDiver,SkillLine.Delivery)))
                EndOrder("Карманы заняты · Гоша отпустил рыбу");
            return true;
        }

        enum PetZone { Deck, Lock, Sea }
        PetZone SwimZone(Vector3 p)
        {
            var chamber=NereidAirlock.Active;
            if(chamber.Contains(p))return PetZone.Lock;
            // The authored room stops 10 cm before the inner gate center. The steel sill
            // in that seam is part of the doorway even before the chamber volume begins.
            var local=chamber.transform.InverseTransformPoint(p);
            if(local.x>=-.28f && local.x<0 && Mathf.Abs(local.z)<1.5f && local.y>-.16f && local.y<3.3f)return PetZone.Lock;
            return NereidVessel.Active.ContainsSealed(p)?PetZone.Deck:PetZone.Sea;
        }
        static Vector3 LockPoint(float x)=>NereidAirlock.Active.transform.TransformPoint(new Vector3(x,1,0));

        bool TravelTo(Vector3 goal,float dt)
        {
            var here=SwimZone(pet.Position);var there=SwimZone(goal);
            if(clearingSeaDoor && here==PetZone.Sea)
            {
                // Fully leave the sill before turning towards a shoal below or behind the hull.
                if(!SwimTo(LockPoint(11.5f),dt,false))return false;
                clearingSeaDoor=false;
            }
            if(here==there)
                return here==PetZone.Deck?Walk(goal,tuning.petFetchSpeed*OrderSpeed,dt):SwimTo(goal,dt,here==PetZone.Sea);
            var chamber=NereidAirlock.Active;
            if(here==PetZone.Lock)
            {
                bool sea=there==PetZone.Sea;
                if(!GateOpen(sea))
                {
                    if(SwimTo(LockPoint(4.25f),dt,false))RequestPetGate(sea);
                    return false;
                }
                clearingSeaDoor=sea;
                SwimTo(LockPoint(sea?11.5f:-1.8f),dt,false);return false;
            }
            bool outside=here==PetZone.Sea;
            var entrance=LockPoint(outside?11.2f:-1.8f);
            var local=chamber.transform.InverseTransformPoint(pet.Position);
            bool aligned=Mathf.Abs(local.z)<.74f &&
                (outside?local.x>8.5f && local.x<11.95f && Mathf.Abs(local.y-1)<.7f:local.x>-2.5f);
            if(!aligned)
            {
                if(outside)SwimTo(entrance,dt,true);
                else Walk(entrance,tuning.petFetchSpeed*OrderSpeed,dt);
                return false;
            }
            if(!GateOpen(outside)){RequestPetGate(outside);return false;}
            SwimTo(LockPoint(4.25f),dt,false);
            return false;
        }
        bool GateOpen(bool sea)=>airlock.Value.Phase==(sea?AirlockPhase.OuterOpen:AirlockPhase.InnerOpen);
        void RequestPetGate(bool sea)
        {
            var state=airlock.Value;
            if(!state.CanRequest(sea))return;
            var chamber=NereidAirlock.Active;
            foreach(var d in DiverMotor.ServerDivers)
                if(Alive(d) && chamber.Contains(d.transform.position))return;
            if(chamber.DoorOccupied(true) || chamber.DoorOccupied(false))return;
            if(state.Request(sea))airlock.Value=state;
        }
        bool SwimTo(Vector3 target,float dt,bool sea)
        {
            var from=pet.Position;
            if(Vector3.Distance(from,target)<.68f && swimRoute.Clear(from,target))return true;
            var next=swimRoute.Step(from,target,tuning.petCatchUpSpeed*OrderSpeed*dt,sea,sea?caution:0);
            Face(next-from);pet.Position=next;
            return false;
        }
        internal bool PetInDoor(Vector3 lockLocal,float doorX) => Training(pet.Mode) &&
            Mathf.Abs(lockLocal.x-doorX)<.65f && Mathf.Abs(lockLocal.z)<1.95f && lockLocal.y>-.4f && lockLocal.y<3.8f;
    }
}
