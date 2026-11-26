using UnityEngine;

namespace Raid.Factory
{
    public sealed partial class FactoryVisuals
    {
        internal readonly struct TransferPose
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly float Scale;
            internal TransferPose(Vector3 position,Quaternion rotation,float scale)
            { Position=position;Rotation=rotation;Scale=scale; }
        }

        internal float FishAtMouth
        {
            get
            {
                if(!batchActive || !batchFeeding)return 0;
                int next=FishFlow.Started(Elapsed-FishSeconds,batchCount);
                if(next==batchCount)return 0;
                float age=Elapsed-FishFlow.LaunchAt(next);
                return Mathf.SmoothStep(0,1,Mathf.InverseLerp(7.20f,7.75f,age));
            }
        }

        internal TransferPose SampleTransfer(int index,float age,Vector3 origin,bool feeding)
        {
            float seed=TransferSeed(index);
            var line=feeding?feedRoute:processRoute;
            var fallback=feeding?feedTube:processTube;
            var inlet=PipePoint(line,fallback,0,origin);
            var inletRotation=PipeRotation(line,fallback,0,origin);
            float exitAt=feeding?FishFlow.FeedOutlet:FishFlow.ProcessOutlet;
            if(age<FishFlow.EntrySeconds)
            {
                float t=Mathf.Clamp01(age/FishFlow.EntrySeconds);
                Vector3 point=SuctionPoint(origin,inlet,inletRotation,seed,t);
                Vector3 heading=SuctionPoint(origin,inlet,inletRotation,seed,Mathf.Min(1,t+.016f))-point;
                var rotation=Facing(heading,inletRotation)*Quaternion.Euler(0,0,Mathf.Sin(seed+9*t)*32*t);
                return new TransferPose(point,rotation,Mathf.Lerp(1,.62f,Smooth(t)));
            }
            if(age<=exitAt)
            {
                float t=Mathf.InverseLerp(FishFlow.EntrySeconds,exitAt,age);
                float phase=t+Mathf.Sin(t*2*Mathf.PI)*Mathf.Sin(seed)*.065f;
                var axis=PipeRotation(line,fallback,phase,origin);
                float room=line?Mathf.Min(.20f,line.radius*.59f):.15f;
                float swing=Mathf.Sin(t*Mathf.PI);
                float roll=seed+t*(7+2*Mathf.Cos(seed));
                var offset=axis*new Vector3(Mathf.Cos(roll)*room*swing,Mathf.Sin(roll)*room*.86f*swing,0);
                return new TransferPose(PipePoint(line,fallback,phase,origin)+offset,
                    axis*Quaternion.Euler(Mathf.Cos(roll)*14*swing,Mathf.Sin(roll)*20*swing,Mathf.Sin(roll*.71f)*50),
                    .62f+Mathf.Sin(roll)*.055f*swing);
            }
            var outlet=PipePoint(line,fallback,1,origin);
            var exit=PipeRotation(line,fallback,1,origin);
            return feeding?FeedPose(age,seed,outlet,exit):ProcessPose(index,age,seed,outlet,exit);
        }

        static Vector3 SuctionPoint(Vector3 origin,Vector3 inlet,Quaternion axis,float seed,float t)
        {
            float draw=t*t*(2-t);
            float radius=Mathf.Sin(t*Mathf.PI)*(.31f+.10f*Mathf.Sin(seed))*(1-.64f*t);
            float angle=seed+(t+t*t)*4.4f;
            return Vector3.LerpUnclamped(origin,inlet,draw)+axis*new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0);
        }

        TransferPose FeedPose(float age,float seed,Vector3 outlet,Quaternion exit)
        {
            const float tongueAt=7.62f;
            var throat=feedThroat?transform.InverseTransformPoint(feedThroat.position):new Vector3(0,1.75f,8.7f);
            var forward=feedThroat && upperJaw?transform.InverseTransformDirection(upperJaw.parent.forward):exit*Vector3.forward;
            float side=Mathf.Sin(seed)*.23f;
            var tongue=livingTongue?transform.InverseTransformPoint(livingTongue.TransformPoint(new Vector3(.08f+side,-.43f,.51f)))
                :throat-forward*.48f+Vector3.right*side-Vector3.up*.13f;
            var facing=Facing(throat-tongue,exit)*Quaternion.Euler(0,Mathf.Sin(seed)*12,75*Mathf.Cos(seed));
            if(age<tongueAt)
            {
                float t=Mathf.InverseLerp(FishFlow.FeedOutlet,tongueAt,age);
                var point=Vector3.Lerp(outlet,tongue,t)+exit*Vector3.forward*(Mathf.Sin(t*Mathf.PI)*.25f);
                point+=Vector3.up*(Mathf.Sin(t*Mathf.PI)*.27f);
                return new TransferPose(point,Quaternion.Slerp(exit*Quaternion.Euler(48*Mathf.Sin(t*Mathf.PI),0,Mathf.Sin(seed)*36*t),facing,t),Mathf.Lerp(.62f,1,t));
            }
            float bite=Mathf.InverseLerp(tongueAt,FishSeconds,age);
            var intoThroat=Vector3.Lerp(tongue,throat,bite*bite);
            intoThroat+=Vector3.up*Mathf.Sin(bite*Mathf.PI*1.7f)*.16f;
            return new TransferPose(intoThroat,facing*Quaternion.Euler(-37*bite,0,Mathf.Sin(bite*11+seed)*17*bite),
                Mathf.Lerp(1,.06f,Smooth(Mathf.InverseLerp(.28f,1,bite))));
        }

        TransferPose ProcessPose(int index,float age,float seed,Vector3 outlet,Quaternion exit)
        {
            GrinderLane(index,out var drop,out var intake,out var grinder);
            float height=Mathf.Max(.02f,transform.TransformVector(outlet-drop).y);
            float landedAt=Mathf.Min(7.32f,FishFlow.ProcessOutlet+Mathf.Sqrt(2*height/9.81f));
            var belt=Facing(intake-drop,exit);
            float turn=Mathf.Sin(seed)*68+(index%3==0?105:0);
            float roll=(index%3==0?-1:1)*(74+Mathf.Cos(seed)*26);
            var resting=belt*Quaternion.Euler(Mathf.Cos(seed)*10,turn,roll);
            if(age<landedAt)
            {
                float fall=Mathf.InverseLerp(FishFlow.ProcessOutlet,landedAt,age);
                var point=Vector3.Lerp(outlet,drop,fall);
                point.y=Mathf.Lerp(outlet.y,drop.y,fall*fall);
                var spin=exit*Quaternion.Euler(fall*200,Mathf.Sin(seed)*fall*72,Mathf.Cos(seed)*fall*158);
                return new TransferPose(point,Quaternion.Slerp(spin,resting,Smooth(fall)),Mathf.Lerp(.62f,1,fall));
            }
            if(age<FishFlow.GrindAt)
            {
                float since=age-landedAt, settle=Mathf.Exp(-since*12)*Mathf.Abs(Mathf.Sin(since*21));
                var point=Vector3.Lerp(drop,intake,Mathf.InverseLerp(landedAt,FishFlow.GrindAt,age))+Vector3.up*(settle*.12f);
                return new TransferPose(point,resting*Quaternion.Euler(settle*14,0,settle*20),1);
            }
            float contact=Mathf.InverseLerp(FishFlow.GrindAt,FishSeconds,age);
            float jerk=Mathf.Sin(contact*31+seed)*Mathf.Sin(contact*Mathf.PI);
            var pulled=Vector3.Lerp(intake,grinder,contact*contact)+belt*new Vector3(jerk*.11f,Mathf.Abs(jerk)*.23f,0);
            return new TransferPose(pulled,resting*Quaternion.Euler(jerk*43,contact*81,jerk*38),
                Mathf.Lerp(1,.08f,Smooth(contact)));
        }

        void GrinderLane(int index,out Vector3 drop,out Vector3 intake,out Vector3 grinder)
        {
            int lane=(int)((uint)(index+batchSeed)%4);
            float x=beltX[lane]+Mathf.Sin(TransferSeed(index))*.18f;
            if(processorDrop && processorIntake && processorGrinder)
            {
                var offset=Vector3.right*(x-(beltX[0]+beltX[3])*.5f);
                drop=transform.InverseTransformPoint(processorDrop.TransformPoint(offset));
                intake=transform.InverseTransformPoint(processorIntake.TransformPoint(offset));
                grinder=transform.InverseTransformPoint(processorGrinder.TransformPoint(offset));
                return;
            }
            drop=ProcessorPoint(new Vector3(x,beltY,-4.95f));
            intake=ProcessorPoint(new Vector3(x,beltY,-6.42f));
            grinder=ProcessorPoint(new Vector3(x,.65f,-6.94f));
        }

        float TransferSeed(int index) => Mathf.Repeat(index*2.399963f+(batchSeed%23)*.84f,2*Mathf.PI);
        Vector3 PipePoint(Nereid.NereidPipeRoute pipe,Vector3[] path,float phase,Vector3 origin) =>
            pipe && pipe.IsConnected?transform.InverseTransformPoint(pipe.WorldPoint(phase)):
                path!=null && path.Length>=2?Along(path,phase):origin;
        Quaternion PipeRotation(Nereid.NereidPipeRoute pipe,Vector3[] path,float phase,Vector3 origin)
        {
            var tangent=PipePoint(pipe,path,Mathf.Min(1,phase+.006f),origin)-PipePoint(pipe,path,Mathf.Max(0,phase-.006f),origin);
            return Facing(tangent,Quaternion.identity);
        }
        static Quaternion Facing(Vector3 direction,Quaternion fallback)
        {
            if(direction.sqrMagnitude<.00001f)return fallback;
            var up=Mathf.Abs(Vector3.Dot(direction.normalized,Vector3.up))>.94f?Vector3.right:Vector3.up;
            return Quaternion.LookRotation(direction,up);
        }
    }
}
