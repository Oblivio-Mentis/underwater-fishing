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
        float railFraction=-1;

        void Awake()
        {
            Active=this;
            airlock=NereidAirlock.Install(transform);
            NereidServiceFittings.Install(transform,airlock);
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
            float speed=1+state.winchLevel*world.progression.winchSpeedPerLevel;
            float fraction=0;
            if(state.hoistPhase==HoistPhase.Raising)fraction=1-state.hoistRemaining/(world.progression.raisingSeconds/speed);
            else if(state.hoistPhase==HoistPhase.Unloading)fraction=1;
            else if(state.hoistPhase==HoistPhase.Returning)fraction=state.hoistRemaining/(world.progression.returningSeconds/speed);
            fraction=Mathf.Clamp01(fraction);
            railFraction=railFraction<0||state.hoistPhase==HoistPhase.Loading?fraction:Mathf.Lerp(railFraction,fraction,1-Mathf.Exp(-Time.deltaTime*14));
            world.container.position=transform.TransformPoint(AlongRail(railFraction));
            var collider=world.container.GetComponent<Collider>();if(collider!=null)collider.enabled=state.hoistPhase==HoistPhase.Loading;
            if(hoistCable!=null)
            {
                var point=world.container.position;
                hoistCable.positionCount=3;
                float railY=transform.TransformPoint(new Vector3(0,6.9f,-14)).y;
                hoistCable.SetPosition(0,transform.TransformPoint(new Vector3(0,6.9f,-14)));
                hoistCable.SetPosition(1,new Vector3(point.x,railY,point.z));
                hoistCable.SetPosition(2,point+Vector3.up*.8f);
            }
        }

        Vector3 AlongRail(float fraction)
        {
            float length=0;for(int i=1;i<cargoRail.Length;i++)length+=Vector3.Distance(cargoRail[i-1],cargoRail[i]);
            float remaining=length*fraction;
            for(int i=1;i<cargoRail.Length;i++)
            {
                float segment=Vector3.Distance(cargoRail[i-1],cargoRail[i]);
                if(remaining<=segment)return Vector3.Lerp(cargoRail[i-1],cargoRail[i],segment>.001f?remaining/segment:0);
                remaining-=segment;
            }
            return cargoRail[cargoRail.Length-1];
        }
    }
}
