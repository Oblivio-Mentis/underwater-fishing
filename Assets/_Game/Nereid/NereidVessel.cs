using UnityEngine;
using Raid.VisualSlice;

namespace Raid.Nereid
{
    // Only the rebuilt scene supplies this component. Previous scenes keep their original bounds/hoist presentation.
    [DefaultExecutionOrder(500)]
    public sealed class NereidVessel : MonoBehaviour
    {
        public static NereidVessel Active { get; private set; }
        public Bounds[] sealedRooms;
        [Min(.1f)] public float indoorGravity=18f;
        [Min(0)] public float indoorJumpHeight=.8f;
        [Min(0)] public float indoorWalkSpeed=4.5f;
        public Vector3[] cargoRail;
        public LineRenderer hoistCable;
        RaidWorld world;
        NereidAirlock airlock;
        CargoBasket basket;
        CageReceivingHopper hopper;
        TeamProgress observed;
        float seenAt,railVisual=-1;
        internal float HoistAnimation {get;private set;}
        internal HoistPhase PresentedHoistPhase {get;private set;}


        void Awake()
        {
            Active=this;
            airlock=NereidAirlock.Install(transform);
            gameObject.AddComponent<NereidHullLighting>();
            var reef=new GameObject("NEREID fishing grounds");
            reef.transform.SetParent(transform,true);
            reef.AddComponent<NereidReef>();
        }
        void OnDestroy() { if(Active==this)Active=null; }

        public bool ContainsSealed(Vector3 point)
        {
            if(airlock!=null&&airlock.Contains(point))return !airlock.IsSubmerged(point+Vector3.up*.6f);
            if(sealedRooms==null)return false;
            point=transform.InverseTransformPoint(point);
            foreach(var room in sealedRooms)if(room.Contains(point))return true;
            return false;
        }
        public bool ContainsWalkingDeck(Vector3 point)
        {
            return airlock!=null&&airlock.Contains(point)?!airlock.IsSubmerged(point):ContainsSealed(point);
        }

        public bool InsideJaws(Vector3 chest)
        {
            Vector3 local=transform.InverseTransformPoint(chest);
            if(local.z<7.72f || local.z>9.42f)return false;
            float lateral=local.x/1.54f;
            float vertical=(local.y-1.45f)/1.49f;
            return lateral*lateral+vertical*vertical<1f;
        }

        void LateUpdate()
        {
            if(world==null)world=RaidWorld.Instance;
            if(world==null||world.NetworkObject==null||!world.IsClientInitialized&&!world.IsServerInitialized)return;
            var state=world.IsServerInitialized?world.Team:world.View;
            if(state==null||world.container==null||cargoRail==null||cargoRail.Length<2)return;
            if(basket==null)
            {
                basket=CargoBasket.Install(world);
                if(basket==null)return;
                if(world.IsClientInitialized)WetCargoShoal.Attach(world);
                hopper=CageReceivingHopper.Install(this,world);
            }
            if(state!=observed){observed=state;seenAt=Time.time;}
            var duration=state.hoistPhase switch {
                HoistPhase.Raising=>world.progression.raisingSeconds,
                HoistPhase.Unloading=>world.progression.unloadingSeconds,
                HoistPhase.Returning=>world.progression.returningSeconds,
                _=>1
            };
            duration=Mathf.Max(.01f,duration/Mathf.Max(1f,1+state.winchLevel*world.progression.winchSpeedPerLevel));
            float estimate=world.IsServerInitialized?0:Mathf.Min(world.tuning.snapshotInterval,Time.time-seenAt);
            PresentedHoistPhase=state.hoistPhase;
            HoistAnimation=state.hoistPhase==HoistPhase.Loading?0:Mathf.Clamp01(1-(state.hoistRemaining-estimate)/duration);
            float fraction=state.hoistPhase switch {
                HoistPhase.Raising=>Mathf.InverseLerp(.15f,.96f,HoistAnimation),
                HoistPhase.Unloading=>1,
                HoistPhase.Returning=>1-Mathf.InverseLerp(.07f,.88f,HoistAnimation),
                _=>0
            };
            railVisual=railVisual<0||state.hoistPhase==HoistPhase.Loading?fraction:
                Mathf.Lerp(railVisual,fraction,1-Mathf.Exp(-Time.deltaTime*19));
            world.container.position=transform.TransformPoint(AlongRail(railVisual));
            basket.Meter(state.CargoUsed,state.Capacity(world.progression));
            basket.Present(state,HoistAnimation);
            if(hopper!=null)hopper.Present(state,HoistAnimation);
            if(hoistCable!=null)
            {
                var liftingEye=basket.CableSocket.position;
                hoistCable.positionCount=3;
                float railY=transform.TransformPoint(new Vector3(0,7.14f,-14)).y;
                hoistCable.SetPosition(0,new Vector3(liftingEye.x,railY+.05f,liftingEye.z));
                hoistCable.SetPosition(1,new Vector3(liftingEye.x,railY-.11f,liftingEye.z));
                hoistCable.SetPosition(2,liftingEye);
            }
        }

        Vector3 AlongRail(float fraction)
        {
            float length=0;for(int i=1;i<cargoRail.Length;i++)length+=Vector3.Distance(cargoRail[i-1],cargoRail[i]);
            float remaining=length*fraction;
            for(int i=1;i<cargoRail.Length;i++)
            {
                float segment=Vector3.Distance(cargoRail[i-1],cargoRail[i]);
                if(remaining<=segment)return Vector3.Lerp(cargoRail[i-1],cargoRail[i],segment>.001f?CargoBasket.Ease(remaining/segment):0);
                remaining-=segment;
            }
            return cargoRail[cargoRail.Length-1];
        }
    }
}
