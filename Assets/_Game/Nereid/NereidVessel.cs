using System.Collections.Generic;
using UnityEngine;
using Raid.VisualSlice;

namespace Raid.Nereid
{
    // Only the rebuilt scene supplies this component. Previous scenes keep their original bounds/hoist presentation.
    [DefaultExecutionOrder(500)]
    public sealed class NereidVessel : MonoBehaviour
    {
        public static NereidVessel Active { get; private set; }
        public const float GalleryAftFoot=-6.75f, GalleryAftTop=-4.5f, GalleryBowTop=3f, GalleryBowFoot=5.45f;
        public Bounds[] sealedRooms;
        [Min(.1f)] public float indoorGravity=18f;
        [Min(0)] public float indoorJumpHeight=.8f;
        [Min(0)] public float indoorWalkSpeed=4.5f;
        public Vector3[] cargoRail;
        public NereidCargoRail authoredRail;
        public LineRenderer hoistCable;
        readonly Dictionary<Material, Material> cladding = new();
        RaidWorld world;
        NereidAirlock airlock;
        internal NereidDeckExtension DeckExtension { get; private set; }
        CargoBasket basket;
        CargoLandingSilt landingSilt;
        NereidReef grounds;
        CageReceivingHopper hopper;
        TeamProgress observed;
        float seenAt,railVisual=-1;
        internal float HoistAnimation {get;private set;}
        internal HoistPhase PresentedHoistPhase {get;private set;}


        void Awake()
        {
            Active=this;
            airlock=NereidAirlock.Install(transform);
            DeckExtension=NereidDeckExtension.Install(this);
            if (!authoredRail) InstallLegacyDressing();
            var reef=new GameObject("NEREID fishing grounds");
            reef.transform.SetParent(transform,true);
            grounds=reef.AddComponent<NereidReef>();
        }

        void InstallLegacyDressing()
        {
            NereidServiceFittings.Install(transform,airlock);
            gameObject.AddComponent<NereidHullLighting>();
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                NereidSurfaceFinish.Clad(transform, transform.Find("Nereid_Exterior"), cladding);
                var factory = FindAnyObjectByType<Factory.FactoryVisuals>();
                if (factory != null) NereidSurfaceFinish.Clad(factory.transform, null, cladding);
            }
        }
        void OnDestroy()
        {
            if (Active == this) Active = null;
            foreach (var material in cladding.Values) Destroy(material);
        }

        public bool ContainsSealed(Vector3 point)
        {
            if(airlock!=null&&airlock.Contains(point))return !airlock.IsSubmerged(point+Vector3.up*.6f);
            var navigation=NereidDeckNavigation.Active;
            if(navigation && navigation.rooms) return navigation.rooms.ContainsSealed(point);
            point=transform.InverseTransformPoint(point);
            if(DeckExtension!=null && NereidDeckExtension.TryGround(point,out _))return true;
            if(sealedRooms==null)return false;
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
            bool hasRail=authoredRail && authoredRail.Ready;
            if(state==null || world.container==null || (!hasRail && (cargoRail==null || cargoRail.Length<2)))return;
            if(basket==null)
            {
                basket=CargoBasket.Install(world);
                if(basket==null)return;
                if(world.IsClientInitialized)WetCargoShoal.Attach(world);
                if(SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null)
                    landingSilt=basket.GetComponent<CargoLandingSilt>();
                if(!hasRail)hopper=CageReceivingHopper.Install(this,world);
            }
            if(state!=observed){observed=state;seenAt=Time.time;}
            float duration=state.HoistDuration(state.hoistPhase,world.progression);
            float estimate=world.IsServerInitialized?0:Mathf.Min(world.tuning.snapshotInterval,Time.time-seenAt);
            PresentedHoistPhase=state.hoistPhase;
            HoistAnimation=state.hoistPhase==HoistPhase.Loading?0:Mathf.Clamp01(1-(state.hoistRemaining-estimate)/duration);
            float fraction=NereidCargoRail.TravelFraction(state.hoistPhase,HoistAnimation);
            railVisual=railVisual<0||state.hoistPhase==HoistPhase.Loading?fraction:
                Mathf.Lerp(railVisual,fraction,1-Mathf.Exp(-Time.deltaTime*19));
            world.container.position=hasRail ? authoredRail.Point(railVisual) : transform.TransformPoint(AlongRail(railVisual));
            if(hasRail) authoredRail.Present(railVisual,state.hoistPhase,HoistAnimation);
            basket.Meter(state.CargoUsed,state.Capacity(world.progression));
            basket.Present(state,HoistAnimation);
            if(world.IsClientInitialized && landingSilt && grounds)
                landingSilt.Present(state.hoistPhase,world.CargoLoadingPosition,grounds.Seabed);
            if(hopper!=null)hopper.Present(state,HoistAnimation);
            if(hasRail && authoredRail.davit)
                authoredRail.davit.PresentCycle(authoredRail.TrolleyPoint(basket.CableSocket.position),basket.CableSocket.position,
                    hoistCable,PresentedHoistPhase,HoistAnimation);
            else if(hoistCable!=null)
            {
                var liftingEye=basket.CableSocket.position;
                hoistCable.positionCount=3;
                var anchor=hasRail ? authoredRail.TrolleyPoint(liftingEye) : new Vector3(liftingEye.x,transform.TransformPoint(new Vector3(0,7.14f,-14)).y,liftingEye.z);
                hoistCable.SetPosition(0,anchor+Vector3.up*.05f);
                hoistCable.SetPosition(1,anchor-Vector3.up*.11f);
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
