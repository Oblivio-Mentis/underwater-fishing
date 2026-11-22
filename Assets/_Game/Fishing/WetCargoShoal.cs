using System.Collections.Generic;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    // A cargo ID owns one model while it swims in the cage, passes the loading pipe and joins the tank.
    [DefaultExecutionOrder(510)]
    public sealed class WetCargoShoal : MonoBehaviour
    {
        sealed class Swimmer
        {
            internal Transform Model;
            internal float Phase, Ring, Depth, Speed, Size, Delay;
            internal bool InCage, Arriving, Routing;
            internal Vector3 ExitStart;
        }

        readonly Dictionary<long,Swimmer> swimmers=new();
        readonly HashSet<long> confirmed=new();
        readonly List<long> removed=new();
        RaidWorld world;
        NereidVessel vessel;
        Factory.FactoryVisuals factory;
        Transform reservoir;
        TeamProgress manifest;
        int cageCount;
        float deliveryProgress;
        public int Count => cageCount;
        public int TankCount => swimmers.Count-cageCount;
        public Transform ModelFor(long id) => swimmers.TryGetValue(id,out var fish)?fish.Model:null;

        public static void Attach(RaidWorld world)
        {
            if(world.container==null)return;
            var basket=world.container.Find("RAID010 cargo basket");
            if(basket==null)return;
            var shoal=basket.GetComponent<WetCargoShoal>();
            if(shoal==null)shoal=basket.gameObject.AddComponent<WetCargoShoal>();
            shoal.world=world;
            shoal.vessel=NereidVessel.Active;
        }

        void LateUpdate()
        {
            if (world==null || !world.IsClientInitialized)
            {
                if(manifest!=null)Reconcile(null);
                return;
            }
            if(vessel!=null && reservoir==null)
            {
                reservoir=new GameObject("Aquarium · delivered catches").transform;
                reservoir.SetParent(vessel.transform,false);
                reservoir.localPosition=new Vector3(0,1.54f,-3);
                factory=FindAnyObjectByType<Factory.FactoryVisuals>();
            }
            var state=world.View;
            if(state==null)return;
            if(state!=manifest)Reconcile(state);
            bool unloading=state.hoistPhase==HoistPhase.Unloading && vessel!=null;
            // The host can already start the return while its replicated manifest still lists
            // these fish in the cage. Do not replay the entry path when that phase clock resets.
            deliveryProgress=unloading ? Mathf.Max(deliveryProgress,
                vessel.PresentedHoistPhase==HoistPhase.Unloading ? vessel.HoistAnimation : 1f) : 0;
            var route=world.FishRoute;
            foreach(var entry in swimmers)
            {
                var fish=entry.Value;
                if(fish.InCage)
                {
                    if(unloading)FollowDelivery(fish,deliveryProgress);
                    else SwimInCage(fish);
                }
                else if(!FollowRoute(entry.Key,fish,route)) SwimInTank(fish);
            }
        }

        void Reconcile(TeamProgress state)
        {
            manifest=state;
            confirmed.Clear();
            cageCount=0;
            if(state!=null)
            {
                foreach(var item in state.cargo)
                {
                    if(!VisibleFish(item))continue;
                    Slot(item).InCage=true;
                    confirmed.Add(item.id);
                    cageCount++;
                }
                if(reservoir!=null)
                    foreach(var item in state.tank)
                    {
                        if(!VisibleFish(item))continue;
                        var fish=Slot(item);
                        bool arrivedFromCage=fish.InCage;
                        fish.InCage=false;
                        if(arrivedFromCage){fish.Arriving=true;fish.ExitStart=fish.Model.position;}
                        if(fish.Model.parent!=reservoir)fish.Model.SetParent(reservoir,true);
                        confirmed.Add(item.id);
                    }
            }
            removed.Clear();
            foreach(var entry in swimmers)
                if(!confirmed.Contains(entry.Key))removed.Add(entry.Key);
            foreach(long id in removed)
            {
                var model=swimmers[id].Model;
                model.gameObject.SetActive(false);
                Destroy(model.gameObject);
                swimmers.Remove(id);
            }
        }

        static bool VisibleFish(CatchItem item) => item.species!=null || item.offering>0;

        Swimmer Slot(CatchItem item)
        {
            if(swimmers.TryGetValue(item.id,out var fish))return fish;
            bool large=item.cargoSize>1;
            string species=string.IsNullOrEmpty(item.species)?large?"Bristlefish":"Lanternfish":item.species;
            var model=CatchFishModel.Create(transform,species,item.id).transform;
            float seed=HabitatNoise((ulong)item.id);
            fish=new Swimmer
            {
                Model=model,
                Phase=seed*Mathf.PI*2,
                Ring=seed,
                Depth=HabitatNoise((ulong)item.id^0x9e3779b9U),
                Speed=.62f+seed*.6f,
                Size=large?1.07f:.65f,
                Delay=.29f+seed*.20f
            };
            swimmers.Add(item.id,fish);
            return fish;
        }

        static float HabitatNoise(ulong id)
        {
            // Sequential IDs still need statistically separate positions and cadence.
            uint bits=unchecked((uint)(id^(id>>32)));
            bits=unchecked((bits^(bits>>16))*0x7feb352dU);
            bits=unchecked((bits^(bits>>15))*0x846ca68bU);
            return ((bits^(bits>>16)) & 0x00ffffffU)/16777216f;
        }

        void SwimInCage(Swimmer fish)
        {
            if(fish.Model.parent!=transform)fish.Model.SetParent(transform,false);
            bool upgraded=vessel!=null;
            float a=Time.time*fish.Speed+fish.Phase;
            float radius=upgraded?.43f+.54f*fish.Ring:.32f+.20f*fish.Ring;
            float y=upgraded ? -.45f+.9f*fish.Depth : -.25f+.52f*fish.Depth;
            fish.Model.localPosition=new Vector3(Mathf.Cos(a)*radius,y+Mathf.Sin(a*2)*.075f,Mathf.Sin(a)*radius*.89f);
            fish.Model.localRotation=Quaternion.LookRotation(new Vector3(-Mathf.Sin(a),.13f*Mathf.Cos(a*2),.89f*Mathf.Cos(a)));
            fish.Model.localScale=Vector3.one*(upgraded?fish.Size:fish.Size*.53f);
            fish.Arriving=false;
        }

        void FollowDelivery(Swimmer fish,float elapsed)
        {
            if(elapsed<fish.Delay){SwimInCage(fish);return;}
            // Keep the tail animation and material instance from the cage as the fish enters the water.
            if(!fish.Arriving)
            {
                fish.Arriving=true;
                fish.ExitStart=fish.Model.position;
                fish.Model.SetParent(null,true);
            }
            float t=Mathf.InverseLerp(fish.Delay,.91f,elapsed);
            Vector3 center=vessel.transform.TransformPoint(new Vector3(0,4.16f,-3));
            Vector3 exit=vessel.transform.TransformPoint(new Vector3(0,2.45f,-3));
            Vector3 entry=Vector3.Lerp(fish.ExitStart,center,CargoBasket.Ease(t*3.75f));
            Vector3 outlet=Vector3.Lerp(center,exit,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.79f,t)));
            var target=TankPoint(fish,Time.time);
            Vector3 point=t<.27f?entry:t<.79f?outlet:Vector3.Lerp(exit,target,CargoBasket.Ease(Mathf.InverseLerp(.79f,1,t)));
            if(t>.27f && t<.79f)point+=new Vector3(Mathf.Sin(t*8+fish.Phase),0,Mathf.Cos(t*6+fish.Phase))*.13f;
            Move(fish.Model,point,25);
        }

        bool FollowRoute(long id,Swimmer fish, FishRouteState route)
        {
            if(factory==null || !route.active || !factory.IsPlaying)
            {
                if(fish.Routing){fish.Routing=false;fish.Arriving=true;}
                return false;
            }
            for(int index=route.completed;index<route.total;index++)
            {
                if(world.ActiveRouteId(index)!=id)continue;
                float age=factory.Elapsed-index*Factory.FactoryVisuals.FishInterval;
                if(age<0)return false;
                age=Mathf.Min(Factory.FactoryVisuals.FishSeconds,age);
                if(!fish.Routing)
                {
                    fish.Routing=true;fish.Arriving=false;
                    fish.ExitStart=factory.transform.InverseTransformPoint(fish.Model.position);
                }
                var point=factory.PathPosition(index,age,fish.ExitStart,route.feeding);
                Move(fish.Model,factory.transform.TransformPoint(point),23);
                float consumed=Mathf.InverseLerp(7.55f,8,age);
                fish.Model.localScale=Vector3.one*(fish.Size/Mathf.Max(.01f,reservoir.lossyScale.x))*Mathf.Lerp(1,.09f,consumed);
                return true;
            }
            if(fish.Routing){fish.Routing=false;fish.Arriving=true;}
            return false;
        }

        Vector3 TankPoint(Swimmer fish,float time)
        {
            float a=time*(fish.Speed*.49f)+fish.Phase;
            float radius=1.08f+1.10f*fish.Ring;
            Quaternion hull=vessel.transform.rotation;
            return reservoir.position+hull*new Vector3(radius*Mathf.Cos(a),(fish.Depth-.5f)*2.72f+Mathf.Sin(a*1.7f)*.24f,radius*Mathf.Sin(a));
        }

        void SwimInTank(Swimmer fish)
        {
            Vector3 point=TankPoint(fish,Time.time);
            Vector3 tangent=(TankPoint(fish,Time.time+.12f)-point).normalized;
            if(fish.Arriving)
            {
                Vector3 next=Vector3.MoveTowards(fish.Model.position,point,Time.deltaTime*2.9f);
                Move(fish.Model,next,9);
                if((point-next).sqrMagnitude<.0025f)fish.Arriving=false;
            }
            else fish.Model.SetPositionAndRotation(point,Quaternion.LookRotation(tangent));
            float support=reservoir.lossyScale.x;
            fish.Model.localScale=Vector3.one*(fish.Size/Mathf.Max(.01f,support));
        }

        static void Move(Transform model,Vector3 point,float rate)
        {
            Vector3 direction=point-model.position;
            Quaternion rotation=model.rotation;
            if(direction.sqrMagnitude>.00002f)
                rotation=Quaternion.Slerp(rotation,Quaternion.LookRotation(direction),1-Mathf.Exp(-rate*Time.deltaTime));
            model.SetPositionAndRotation(point,rotation);
        }

        void OnDestroy()
        {
            foreach(var fish in swimmers.Values)if(fish.Model!=null)Destroy(fish.Model.gameObject);
            if(reservoir!=null)Destroy(reservoir.gameObject);
        }
    }
}
