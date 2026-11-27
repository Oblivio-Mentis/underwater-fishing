using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Raid
{
    public struct KrakenBreathState
    {
        public const float Ignite=.86f, Extinguish=2.23f, Roar=2.39f, RaidAt=3.18f, End=3.55f, Reach=12f;
        public int sequence;
        public double startedAt;
        public Vector3 origin, forward;

        public Vector3 Direction(float age)
        {
            float phase=Mathf.InverseLerp(Ignite,Extinguish,age);
            return Quaternion.AngleAxis(Mathf.Sin(phase*Mathf.PI*1.6f)*11-4,Vector3.up)*forward;
        }

        public float Flame(float age) =>
            Mathf.SmoothStep(0,1,(age-Ignite)/.18f)*(1-Mathf.SmoothStep(0,1,(age-Extinguish+.24f)/.24f));

        public float FlameReach(float age) => Reach*Mathf.SmoothStep(.11f,1,(age-Ignite)/.26f);

        public float Exposure(Vector3 point,float age)
        {
            if(Flame(age)<=.03f)return 0;
            var direction=Direction(age);
            var offset=point-origin;
            float along=Vector3.Dot(direction,offset);
            if(along<0 || along>FlameReach(age))return 0;
            float width=.72f+along*.165f;
            float radial=(offset-direction*along).magnitude;
            if(radial>width)return 0;
            return Mathf.Lerp(1,.72f,along/Reach)*Mathf.Lerp(1,.79f,radial/width);
        }
    }

    public sealed partial class RaidWorld
    {
        readonly SyncVar<KrakenBreathState> krakenBreath=new();
        readonly HashSet<DiverMotor> singedDivers=new();
        Factory.FactoryVisuals bowFactory;
        bool pendingKrakenRaid;
        int breathSequence;

        internal KrakenBreathState BowBreath => krakenBreath.Value;
        internal double BreathClock => TimeManager==null?Time.timeAsDouble:
            TimeManager.TicksToTime(TimeManager.Tick)+TimeManager.GetTickElapsedAsDouble();

        void ResetKrakenBreath()
        {
            krakenBreath.Value=default;
            pendingKrakenRaid=false;
            singedDivers.Clear();
        }

        bool TrySwallowCharge(TechRig charge,Vector3 travel,RaycastHit obstacle)
        {
            if(!Nereid.NereidVessel.Active || transitionRemaining>0 || pendingKrakenRaid || BowBreath.sequence!=0)return false;
            if(!bowFactory)bowFactory=FindAnyObjectByType<Factory.FactoryVisuals>();
            var mouth=bowFactory && bowFactory.upperJaw?bowFactory.upperJaw.parent:null;
            if(!mouth)return false;
            var aperture=expeditionLayout?expeditionLayout.chargeAperture:null;
            var outlet=expeditionLayout?expeditionLayout.flameOutlet:null;
            var frame=aperture?aperture:mouth;
            var start=frame.InverseTransformPoint(charge.position);
            var end=frame.InverseTransformPoint(charge.position+travel);
            float mouthPlane=aperture?0:-.84f;
            if(start.z>=mouthPlane || end.z<mouthPlane)return false;
            float fraction=(mouthPlane-start.z)/(end.z-start.z);
            var entry=Vector3.LerpUnclamped(start,end,fraction);
            float x=entry.x/Mathf.Max(.01f,expeditionLayout?expeditionLayout.apertureHalfWidth:1.04f);
            float y=(entry.y+(aperture?0:.12f))/Mathf.Max(.01f,expeditionLayout?expeditionLayout.apertureHalfHeight:.49f);
            if(x*x+y*y>1)return false;
            if(obstacle.collider && obstacle.distance+.008f<travel.magnitude*fraction &&
                (!obstacle.collider.transform.IsChildOf(mouth) || obstacle.distance+.16f<travel.magnitude*fraction))return false;

            charge.mode="spent";
            if(!SaveTransaction()){charge.mode="fuse";return false;}
            charge.anchor=Vector3.zero;
            charge.position=frame.TransformPoint(entry);
            singedDivers.Clear();
            krakenBreath.Value=new KrakenBreathState
            {
                sequence=++breathSequence,startedAt=BreathClock,
                origin=outlet?outlet.position:mouth.TransformPoint(new Vector3(0,-.12f,-1.03f)),
                forward=outlet?outlet.forward:(-mouth.forward-mouth.up*.25f).normalized
            };
            pendingKrakenRaid=true;
            return true;
        }

        float BreathExposure(KrakenBreathState state,float age,Vector3 target)
        {
            float exposed=state.Exposure(target,age);
            return exposed>0 && ClearGearPath(state.origin,target)?exposed:0;
        }

        void TickKrakenBreath(float dt)
        {
            var state=BowBreath;
            if(state.sequence!=0)
            {
                float age=(float)(BreathClock-state.startedAt);
                if(age>=KrakenBreathState.Ignite && age<KrakenBreathState.Extinguish)
                {
                    foreach(var diver in DiverMotor.ServerDivers)
                    {
                        if(!diver || diver.IsDead)continue;
                        var center=diver.transform.position;
                        float exposure=Mathf.Max(BreathExposure(state,age,center+Vector3.up*.62f),
                            BreathExposure(state,age,center+Vector3.up*1.10f));
                        if(exposure<=0)continue;
                        float damage=singedDivers.Add(diver)?(expeditionLayout?expeditionLayout.flameFirstDamage:86):(expeditionLayout?expeditionLayout.flameDamagePerSecond:24)*dt;
                        diver.TakeRigImpact(damage*exposure);
                    }
                }
                if(age>=KrakenBreathState.End)
                {
                    krakenBreath.Value=default;
                    singedDivers.Clear();
                }
                if(age<KrakenBreathState.RaidAt)return;
            }
            if(!pendingKrakenRaid || !threats || threats.WaveActive)return;
            waveLossBudget=team.StockLossLimit(progression);
            threats.StartWave(1,Mathf.Max(1,team.highestDepth-1),provoked:true);
            pendingKrakenRaid=false;
        }
    }
}
