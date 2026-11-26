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
            internal int DeliveryRank;
            internal Vector3 ExitStart;
            internal Quaternion ExitRotation;
            // Entered through the pet hatch: the model travels its short glass line first.
            internal float HatchStartedAt = -1;
        }
        uint seenHatchDeliveries;
        long pendingHatchId;

        sealed class SalvageModel
        {
            internal Transform Model;
            internal Bounds MeshBounds;
            internal Vector3 Berth;
        }

        readonly Dictionary<long,Swimmer> swimmers=new();
        readonly Dictionary<long,SalvageModel> salvage=new();
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
            var authored=world.container.GetComponentInChildren<CargoBasket>();
            var basket=authored ? authored.transform : world.container.Find("RAID010 cargo basket");
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
                var rail=vessel.authoredRail;
                if(rail && rail.aquarium) reservoir.SetParent(rail.aquarium,false);
                else
                {
                    reservoir.SetParent(vessel.transform,false);
                    reservoir.localPosition=new Vector3(0,1.54f,-3);
                }
                factory=FindAnyObjectByType<Factory.FactoryVisuals>();
            }
            var state=world.View;
            if(state==null)return;
            var pet=world.Gosha;
            if(pet.HatchDeliveries!=seenHatchDeliveries)
            {
                seenHatchDeliveries=pet.HatchDeliveries;
                pendingHatchId=pet.HatchCargoId;
                Nereid.GoshaReceiver.Active?.PresentTransfer();
            }
            if(state!=manifest)Reconcile(state);
            bool unloading=state.hoistPhase==HoistPhase.Unloading && vessel!=null;
            // The host can already start the return while its replicated manifest still lists
            // these fish in the cage. Do not replay the entry path when that phase clock resets.
            deliveryProgress=unloading ? Mathf.Max(deliveryProgress,
                vessel.PresentedHoistPhase==HoistPhase.Unloading ? vessel.HoistAnimation : 1f) : 0;
            PresentSalvage(deliveryProgress);
            int unloadingCount=unloading?state.HoistFishCount(world.progression):0;
            var route=world.FishRoute;
            foreach(var entry in swimmers)
            {
                var fish=entry.Value;
                if(fish.InCage)
                {
                    if(unloading && fish.DeliveryRank<unloadingCount)FollowDelivery(fish,deliveryProgress);
                    else SwimInCage(fish);
                }
                else if(fish.HatchStartedAt>=0 && FollowHatch(fish)) {}
                else if(!FollowRoute(entry.Key,fish,route)) SwimInTank(fish);
            }
        }

        // Dense schools keep the same manifest while limiting animated bodies on screen.
        const int VisibleSchool=80;

        void Reconcile(TeamProgress state)
        {
            manifest=state;
            confirmed.Clear();
            cageCount=0;
            int equipmentCount=0;
            if(state!=null)
            {
                foreach(var item in state.cargo)
                {
                    if(item.IsSalvage)
                    {
                        PlaceSalvage(item,equipmentCount++);
                        confirmed.Add(item.id);
                        continue;
                    }
                    if(cageCount<VisibleSchool)
                    {
                        var staged=Slot(item);
                        staged.InCage=true;
                        staged.DeliveryRank=cageCount;
                        confirmed.Add(item.id);
                    }
                    cageCount++;
                }
                int displayed=0;
                if(reservoir!=null)
                    foreach(var item in state.tank)
                    {
                        if(item.IsSalvage)continue;
                        if(displayed++>=VisibleSchool && item.id!=pendingHatchId && !world.IsInFishPipe(item.id))continue;
                        bool fresh=!swimmers.ContainsKey(item.id);
                        var fish=Slot(item);
                        bool arrivedFromCage=fish.InCage;
                        fish.InCage=false;
                        if(arrivedFromCage){fish.Arriving=true;fish.ExitStart=fish.Model.position;}
                        else if(fresh && item.id==pendingHatchId && Nereid.GoshaReceiver.Active!=null)
                        {
                            fish.HatchStartedAt=Time.time;fish.Arriving=true;
                            fish.ExitStart=Nereid.GoshaReceiver.Active.MouthPosition;
                            fish.Model.position=fish.ExitStart;
                            pendingHatchId=0;
                        }
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
            removed.Clear();
            foreach(var entry in salvage)
                if(!confirmed.Contains(entry.Key))removed.Add(entry.Key);
            foreach(long id in removed)
            {
                var model=salvage[id].Model.gameObject;
                model.SetActive(false);
                Destroy(model);
                salvage.Remove(id);
            }
        }

        void PlaceSalvage(CatchItem item,int index)
        {
            int columns=vessel==null?3:4;
            float spacing=vessel==null?.51f:.73f;
            float scale=vessel==null?.26f:.38f;
            if(!salvage.TryGetValue(item.id,out var entry))
            {
                LootActor source=null;
                foreach(var actor in LootActor.VisibleStock)
                    if(actor.tier==item.depthTier && actor.gameObject.scene==gameObject.scene) {source=actor;break;}
                if(source==null)return;
                var model=source.CopyModel(transform,source.DisplayName+" "+item.id);
                model.localScale=Vector3.one*scale;
                entry=new SalvageModel{Model=model,MeshBounds=source.MeshBounds};
                salvage.Add(item.id,entry);
            }
            int layer=index/(columns*columns);
            float floor=vessel==null?-.46f:CargoBasket.Floor+.16f;
            var bounds=entry.MeshBounds;
            entry.Berth=new Vector3((index%columns-(columns-1)*.5f)*spacing,
                floor+scale*(-bounds.min.y+layer*bounds.size.y),
                (index/columns%columns-(columns-1)*.5f)*spacing);
        }

        void PresentSalvage(float unloading)
        {
            float offload=Mathf.InverseLerp(.36f,.81f,unloading);
            float slide=CargoBasket.Ease(offload);
            foreach(var entry in salvage.Values)
            {
                entry.Model.gameObject.SetActive(offload<1f);
                entry.Model.localPosition=entry.Berth+new Vector3(-slide*2.8f,-slide*slide*.58f,0);
                entry.Model.localRotation=Quaternion.Euler(0,0,slide*26f);
            }
        }

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
            var pipe=vessel.authoredRail ? vessel.authoredRail.inlet : null;
            bool authored=pipe && pipe.IsConnected;
            float start=authored ? .13f+.32f*fish.DeliveryRank/Mathf.Max(1,cageCount-1) : fish.Delay;
            float finish=authored ? start+.52f : .91f;
            if(elapsed<start){SwimInCage(fish);return;}
            // Keep the tail animation and material instance from the cage as the fish enters the water.
            if(!fish.Arriving)
            {
                fish.Arriving=true;
                fish.ExitStart=fish.Model.position;
                fish.Model.SetParent(null,true);
            }
            float t=Mathf.InverseLerp(start,finish,elapsed);
            if(authored)
            {
                Vector3 routePoint;
                if(t<.24f) routePoint=Vector3.Lerp(fish.ExitStart,pipe.WorldPoint(0),CargoBasket.Ease(t/.24f));
                else if(t<.84f) routePoint=pipe.WorldPoint(Mathf.InverseLerp(.24f,.84f,t));
                else routePoint=Vector3.Lerp(pipe.WorldPoint(1),TankPoint(fish,Time.time),CargoBasket.Ease(Mathf.InverseLerp(.84f,1,t)));
                Move(fish.Model,routePoint,25);
                return;
            }
            Vector3 center=vessel.transform.TransformPoint(new Vector3(0,4.16f,-3));
            Vector3 exit=vessel.transform.TransformPoint(new Vector3(0,2.45f,-3));
            Vector3 entry=Vector3.Lerp(fish.ExitStart,center,CargoBasket.Ease(t*3.75f));
            Vector3 outlet=Vector3.Lerp(center,exit,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.79f,t)));
            var target=TankPoint(fish,Time.time);
            Vector3 point=t<.27f?entry:t<.79f?outlet:Vector3.Lerp(exit,target,CargoBasket.Ease(Mathf.InverseLerp(.79f,1,t)));
            if(t>.27f && t<.79f)point+=new Vector3(Mathf.Sin(t*8+fish.Phase),0,Mathf.Cos(t*6+fish.Phase))*.13f;
            Move(fish.Model,point,25);
        }

        // The pet hatch: mouth, a beat inside the collar, the glass line, then the tank.
        bool FollowHatch(Swimmer fish)
        {
            var receiver=Nereid.GoshaReceiver.Active;
            var pipe=receiver!=null?receiver.Pipe:null;
            float age=Time.time-fish.HatchStartedAt;
            const float collar=.55f, travel=1.9f, settle=.8f;
            if(receiver==null || age>collar+travel+settle){fish.HatchStartedAt=-1;fish.Arriving=true;return false;}
            if(fish.Model.parent!=null)fish.Model.SetParent(null,true);
            bool routed=pipe!=null && pipe.IsConnected;
            Vector3 entry=routed?pipe.WorldPoint(0):receiver.PipeStart!=null?receiver.PipeStart.position:receiver.MouthPosition;
            Vector3 exit=routed?pipe.WorldPoint(1):receiver.PipeEnd!=null?receiver.PipeEnd.position:receiver.MouthPosition;
            Vector3 point;
            if(age<collar) point=Vector3.Lerp(receiver.MouthPosition,entry,CargoBasket.Ease(age/collar));
            else if(age<collar+travel)
            {
                float t=(age-collar)/travel;
                point=routed?pipe.WorldPoint(t):Vector3.Lerp(entry,exit,t);
            }
            else point=Vector3.Lerp(exit,TankPoint(fish,Time.time),CargoBasket.Ease((age-collar-travel)/settle));
            Move(fish.Model,point,25);
            fish.Model.localScale=Vector3.one*(fish.Size/Mathf.Max(.01f,reservoir!=null?reservoir.lossyScale.x:1));
            return true;
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
                float age=factory.Elapsed-FishFlow.LaunchAt(index);
                if(age<0)return false;
                age=Mathf.Min(Factory.FactoryVisuals.FishSeconds,age);
                if(!fish.Routing)
                {
                    fish.Routing=true;fish.Arriving=false;
                    fish.ExitStart=factory.transform.InverseTransformPoint(fish.Model.position);
                    fish.ExitRotation=fish.Model.rotation;
                }
                var pose=factory.SampleTransfer(index,age,fish.ExitStart,route.feeding);
                var rotation=factory.transform.rotation*pose.Rotation;
                float caught=Mathf.SmoothStep(0,1,age/.42f);
                fish.Model.SetPositionAndRotation(factory.transform.TransformPoint(pose.Position),
                    Quaternion.Slerp(fish.ExitRotation,rotation,caught));
                fish.Model.localScale=Vector3.one*(fish.Size/Mathf.Max(.01f,reservoir.lossyScale.x)*pose.Scale);
                return true;
            }
            if(fish.Routing){fish.Routing=false;fish.Arriving=true;}
            return false;
        }

        Vector3 TankPoint(Swimmer fish,float time)
        {
            float a=time*(fish.Speed*.49f)+fish.Phase;
            var rail=vessel.authoredRail;
            if(rail && rail.aquarium)
            {
                var radii=rail.aquariumSwimRadii;
                float ring=.45f+.55f*fish.Ring;
                return reservoir.TransformPoint(new Vector3(Mathf.Cos(a)*radii.x*ring,
                    ((fish.Depth-.5f)*1.6f+Mathf.Sin(a*1.7f)*.16f)*radii.y, Mathf.Sin(a)*radii.z*ring));
            }
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
