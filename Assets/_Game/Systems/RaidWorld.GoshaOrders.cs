using FishNet.Connection;
using FishNet.Object;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public enum GoshaOrder : byte { Come, CatchFish }

    public sealed partial class RaidWorld
    {
        DiverMotor orderDiver;
        FishActor orderFish;
        long orderFishId;
        string orderProfile;
        bool carefulSwimmer, clearingSeaDoor;
        float lastOrderAt = float.NegativeInfinity, orderEnds, pocketWait;
        Vector3 fishWaterPosition;
        readonly GoshaSwimRoute swimRoute = new();

        static bool Training(GoshaMode mode) => mode is GoshaMode.Coming or GoshaMode.Fishing or GoshaMode.BringingFish or GoshaMode.GoingHome;
        bool PetAvailableForBall => pet.Mode!=GoshaMode.Cradled && !Training(pet.Mode);

        public string GoshaTrainingHint(DiverMotor diver)
        {
            if(diver==null || !Gosha.Active || View?.goshaTamed!=true || !diver.IsOwner || LocalProgress?.skills.Contains(SkillId.G1)!=true)return "";
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
            return LocalProgress.skills.Contains(SkillId.G2)?"G — ко мне  ·  H — принести рыбку":"G — Гоша, ко мне";
        }

        internal float OrderCooldown(DiverMotor diver) => tuning.petOrderCooldown*(1-Mathf.Clamp(SkillValue(diver,SkillId.G4),0,99)*.01f);
        internal float OrderRadius(DiverMotor diver) => tuning.petSearchRadius*(HasSkill(diver,SkillId.G4)?1.2f:1);
        internal float OrderRemaining(DiverMotor diver) => Mathf.Max(0,lastOrderAt+OrderCooldown(diver)-Time.time);

        [ServerRpc(RequireOwnership=false)]
        public void RequestGoshaOrder(GoshaOrder order,NetworkConnection sender=null)
        {
            if(sender?.FirstObject==null)return;
            var diver=sender.FirstObject.GetComponent<DiverMotor>();
            if(diver==null || diver.Owner!=sender || !Alive(diver) || diver.IsTrapped || transitionRemaining>0)return;
            if(!pet.Active || team?.goshaTamed!=true){OrderReply(sender,"Сначала подружитесь с Гошей и верните его на борт");return;}
            bool catchFish=order==GoshaOrder.CatchFish;
            if(order!=GoshaOrder.Come && !catchFish)return;
            string profile=ProgressKey(diver);
            if(string.IsNullOrEmpty(profile) || !HasSkill(diver,SkillId.G1) || (catchFish&&!HasSkill(diver,SkillId.G2)))
            {OrderReply(sender,"Эта команда ещё не изучена");return;}
            if((!RestingPet(pet.Mode) && pet.Mode!=GoshaMode.GoingHome) || pet.Ball==GoshaBallMode.Carried ||
                (pet.Ball==GoshaBallMode.Flying && pet.Thrower>=0))
            {OrderReply(sender,"Гоша занят");return;}
            float remaining=OrderRemaining(diver);
            if(remaining>0){OrderReply(sender,"Гоша отдыхает · ещё "+Mathf.CeilToInt(remaining)+" с");return;}
            var tool=diver.GetComponent<HarpoonTool>();
            if(catchFish && (tool==null || tool.HasFullCreel))
            {OrderReply(sender,"Освободите карман для улова");return;}
            bool careful=HasSkill(diver,SkillId.G3);
            if(catchFish)
            {
                var origin=SwimZone(diver.transform.position)==PetZone.Sea?diver.transform.position:LockPoint(11.2f);
                orderFish=ClosestFish(origin,OrderRadius(diver),careful);
                if(orderFish==null || !orderFish.TryReserveForGosha())
                {orderFish=null;OrderReply(sender,"Поблизости нет доступной малой рыбы");return;}
                orderFishId=orderFish.CargoId;fishWaterPosition=orderFish.transform.position;
            }
            lastOrderAt=Time.time;orderEnds=Time.time+tuning.petOrderTimeout;pocketWait=0;
            orderDiver=diver;orderProfile=profile;carefulSwimmer=careful;pet.OrderOwner=diver.OwnerId;
            swimRoute.Reset();SetMode(catchFish?GoshaMode.Fishing:GoshaMode.Coming);
            gosha.Value=pet;
            OrderReply(sender,catchFish?"Гоша отправился за рыбой":"Гоша идёт к вам");
        }

        FishActor ClosestFish(Vector3 origin,float radius,bool careful)
        {
            FishActor nearest=null;float best=radius*radius;
            foreach(var f in FishActor.ServerSchool)
            {
                if(!f.IsAvailableForGosha)continue;
                Vector3 at=f.transform.position;float distance=(at-origin).sqrMagnitude;
                if(distance>best || SwimZone(at)!=PetZone.Sea || (careful&&GoshaSwimRoute.Risk(at)>.68f))continue;
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
            team.Player(ProgressKey(host)).skills.Add(SkillId.G1);
            team.Player(ProgressKey(guest)).skills.AddRange(new[]{SkillId.G1,SkillId.G2,SkillId.G3,SkillId.G4});
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
        }
        void EndOrder(string response)
        {
            if(orderDiver!=null && orderDiver.Owner!=null && orderDiver.Owner.IsActive)OrderReply(orderDiver.Owner,response);
            ReleaseOrder();swimRoute.Reset();
            if(SwimZone(pet.Position)==PetZone.Deck)ResumeDeck();
            else SetMode(GoshaMode.GoingHome);
        }
        void ResumeDeck()
        {
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
            {EndOrder("Гоша вернулся без улова");return true;}
            if(pet.Mode==GoshaMode.Coming)
            {
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
            if(pocketWait>=tuning.petPocketWait)EndOrder("Карманы заняты · Гоша отпустил рыбу");
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
                return here==PetZone.Deck?Walk(goal,tuning.petFetchSpeed,dt):SwimTo(goal,dt,here==PetZone.Sea);
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
            bool aligned=Mathf.Abs(local.z)<.74f && (outside?local.x<11.95f:local.x>-2.5f);
            if(!aligned)
            {
                if(outside)SwimTo(entrance,dt,true);
                else Walk(entrance,tuning.petFetchSpeed,dt);
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
            var next=swimRoute.Step(from,target,tuning.petCatchUpSpeed*dt,sea,sea&&carefulSwimmer);
            Face(next-from);pet.Position=next;
            return false;
        }
        internal bool PetInDoor(Vector3 lockLocal,float doorX) => Training(pet.Mode) &&
            Mathf.Abs(lockLocal.x-doorX)<.65f && Mathf.Abs(lockLocal.z)<1.95f && lockLocal.y>-.4f && lockLocal.y<3.8f;
    }
}
