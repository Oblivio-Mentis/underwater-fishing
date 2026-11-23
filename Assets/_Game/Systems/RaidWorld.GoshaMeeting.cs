using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        enum MeetingStage { Wait, Approach, Circle, Behind, Perch }
        const float CircleRadius=3.2f;
        const float CircleSeconds=3.65f;
        static readonly float[] RevealAngles={-19,25,-53,59,0};
        readonly GoshaSwimRoute meetingRoute=new();
        MeetingStage meetingStage;
        float meetingTime;
        Vector3 circleStart,previousDiverPosition;
        float circleTurn;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal bool QaMeetingCircles => meetingStage==MeetingStage.Circle && pet.Mode==GoshaMode.Swimming;
        internal bool QaMeetingPerches => meetingStage==MeetingStage.Perch && pet.Mode==GoshaMode.Swimming;
        internal Vector3 QaMeetingCenter(DiverMotor diver) => CircleCenter(diver,NereidAirlock.Active);
#endif

        void ResetMeeting()
        {
            meetingStage=MeetingStage.Wait;
            meetingTime=0;
            rodeThroughLock=false;
            meetingRoute.Reset();
        }

        static bool Outside(DiverMotor diver,NereidAirlock lockRoom)=>Alive(diver)&&!diver.IsInside&&
            !lockRoom.Contains(diver.transform.position);

        void TickMeeting(float dt)
        {
            var lockRoom=NereidAirlock.Active;
            var diver=CrewMember(pet.Rider);
            if(!Alive(diver) || (pet.Mode!=GoshaMode.Riding && !Outside(diver,lockRoom)))
            {
                pet.Rider=-1;
                SetMode(GoshaMode.Waiting);
                ResetMeeting();
                diver=null;
            }
            if(diver==null)
            {
                foreach(var candidate in DiverMotor.ServerDivers)
                    if(Outside(candidate,lockRoom)){diver=candidate;break;}
                if(diver==null)return;
                pet.Rider=diver.OwnerId;
                previousDiverPosition=diver.transform.position;
            }

            if(pet.Mode==GoshaMode.Riding)
            {
                TickShoulderRide(diver,lockRoom,dt);
                return;
            }

            meetingTime+=dt;
            var center=CircleCenter(diver,lockRoom);
            var diverMotion=Vector3.ClampMagnitude(diver.transform.position-previousDiverPosition,tuning.petCatchUpSpeed*dt);
            previousDiverPosition=diver.transform.position;
            var old=pet.Position;
            switch(meetingStage)
            {
                case MeetingStage.Wait:
                    if(meetingTime>=1.25f)RevealGosha(diver,lockRoom,center);
                    return;
                case MeetingStage.Approach:
                    var opening=center+circleStart*CircleRadius+Vector3.down*.48f;
                    SwimForMeeting(opening,Mathf.Lerp(2,tuning.petCatchUpSpeed,meetingTime/.85f),dt);
                    if(Vector3.Distance(pet.Position,opening)<.48f)
                    {
                        if(!team.goshaMet){team.goshaMet=true;Publish();Save();}
                        meetingStage=MeetingStage.Circle;
                        meetingTime=0;
                    }
                    break;
                case MeetingStage.Circle:
                    float phase=Mathf.Clamp01(meetingTime/CircleSeconds);
                    var radial=Quaternion.AngleAxis(circleTurn*phase,Vector3.up)*circleStart;
                    var arc=center+radial*CircleRadius+Vector3.up*(-.48f+Mathf.Sin(phase*Mathf.PI)*1.12f);
                    SwimForMeeting(arc,tuning.petCatchUpSpeed,dt);
                    if(phase>=1)
                    {
                        meetingStage=MeetingStage.Behind;
                        meetingTime=0;
                    }
                    break;
                case MeetingStage.Behind:
                    var behind=diver.transform.TransformPoint(new Vector3(.75f,.38f,-1.65f));
                    SwimForMeeting(behind,tuning.petCatchUpSpeed,dt);
                    if(Vector3.Distance(pet.Position,behind)<.25f ||
                        (meetingTime>1.4f && Vector3.Distance(pet.Position,diver.transform.position)<2.1f &&
                            meetingRoute.Clear(pet.Position,Shoulder(diver))))
                    {
                        meetingStage=MeetingStage.Perch;
                        meetingRoute.Reset();
                    }
                    break;
                case MeetingStage.Perch:
                    var perch=Shoulder(diver);
                    // While settling, match the diver's translation before closing the remaining gap.
                    // Otherwise the slower landing animation would trail a swimming diver indefinitely.
                    if(meetingRoute.Clear(pet.Position,pet.Position+diverMotion))pet.Position+=diverMotion;
                    float remaining=Vector3.Distance(pet.Position,perch);
                    float speed=Mathf.Min(tuning.petCatchUpSpeed,.65f+remaining*3.8f);
                    SwimForMeeting(perch,speed,dt,false);
                    if(Vector3.Distance(pet.Position,perch)<.065f)
                    {
                        pet.Position=perch;
                        SetMode(GoshaMode.Riding);
                        rodeThroughLock=false;
                        idleAtSea=0;
                        lastRiderPosition=diver.transform.position;
                    }
                    else if(remaining>2.5f || (pet.Position-old).sqrMagnitude<.000001f)
                    {
                        meetingStage=MeetingStage.Behind;
                        meetingTime=0;
                    }
                    break;
            }
            Face(pet.Position-old);
        }

        // Leave room for the circle across the outer hatch, even if the diver stops on the sill.
        static Vector3 CircleCenter(DiverMotor diver,NereidAirlock lockRoom)
        {
            var local=lockRoom.transform.InverseTransformPoint(diver.transform.position);
            if(local.x<12.7f && local.x>7.5f && Mathf.Abs(local.z)<6f)
                local.x+=Mathf.Max(0,12.7f-local.x)*Mathf.InverseLerp(6f,2.5f,Mathf.Abs(local.z));
            return lockRoom.transform.TransformPoint(local);
        }

        void RevealGosha(DiverMotor diver,NereidAirlock lockRoom,Vector3 center)
        {
            var outward=Vector3.ProjectOnPlane(lockRoom.transform.right,Vector3.up).normalized;
            var gaze=Vector3.ProjectOnPlane(diver.AimDirection,Vector3.up).normalized;
            float angle=gaze.sqrMagnitude>.01f?Mathf.Clamp(Vector3.SignedAngle(outward,gaze,Vector3.up),-60,60):0;
            // Start off-centre in the water. Prefer a flank with a clear approach from below.
            foreach(float offset in RevealAngles)
            {
                var radial=Quaternion.AngleAxis(Mathf.Clamp(angle+offset,-78,78),Vector3.up)*outward;
                var opening=center+radial*CircleRadius+Vector3.down*.48f;
                var start=center+radial*16f+Vector3.down*2.35f;
                if(!deck.InSea(start)||!deck.InSea(opening)||Physics.CheckSphere(start,.48f,Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)||!meetingRoute.Clear(start,opening))continue;
                pet.Position=start;
                Face(opening-start);
                circleStart=radial;
                circleTurn=Vector3.SignedAngle(outward,radial,Vector3.up)>0?-266:266;
                meetingStage=MeetingStage.Approach;
                meetingTime=0;
                SetMode(GoshaMode.Swimming);
                return;
            }
            // A diver might stop directly against rock. Try a new sightline without revealing a blocked spawn.
            meetingTime=.9f;
        }

        void SwimForMeeting(Vector3 goal,float speed,float dt,bool detour=true) =>
            pet.Position=meetingRoute.Step(pet.Position,goal,speed*dt,detour,0f);

        void TickShoulderRide(DiverMotor diver,NereidAirlock lockRoom,float dt)
        {
            pet.Position=Shoulder(diver);
            pet.Heading=diver.transform.forward;
            if(lockRoom.Contains(diver.transform.position))rodeThroughLock=true;
            if(!rodeThroughLock)
            {
                if((diver.transform.position-lastRiderPosition).sqrMagnitude>1)
                {idleAtSea=0;lastRiderPosition=diver.transform.position;}
                else idleAtSea+=dt;
                if(idleAtSea>tuning.lostCompanionSeconds)
                    foreach(var other in DiverMotor.ServerDivers)
                    {
                        if(other==diver || !Outside(other,lockRoom) ||
                            Vector3.Distance(other.transform.position,pet.Position)>=9 ||
                            !meetingRoute.Clear(pet.Position,Shoulder(other)))continue;
                        pet.Rider=other.OwnerId;
                        previousDiverPosition=other.transform.position;
                        meetingStage=MeetingStage.Behind;
                        meetingTime=0;
                        meetingRoute.Reset();
                        SetMode(GoshaMode.Swimming);
                        return;
                    }
            }
            if(!rodeThroughLock || !OnDeck(diver) || lockRoom.transform.InverseTransformPoint(diver.transform.position).x>=-.55f)return;
            team.goshaTamed=true;
            pet.Rider=-1;
            SetMode(GoshaMode.Roaming);
            patrol=1;
            Publish();
            Save();
        }
    }
}
