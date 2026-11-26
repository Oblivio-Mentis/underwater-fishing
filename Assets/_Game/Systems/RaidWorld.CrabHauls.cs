using System.Collections.Generic;
using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    public struct CrabHaul
    {
        public long FishId;
        public Vector3 Burrow, Source, Den;
        public double Started;
        public byte Crew;
    }

    public sealed partial class RaidWorld
    {
        internal const float CrabDig=1.32f, CrabPick=3.2f, CrabFinish=8.7f;
        const int PileThreshold=5, ConcurrentHauls=4;
        const float PileRadius=2.8f;
        readonly SyncList<CrabHaul> crabHauls=new();
        readonly SyncList<Vector3> crabDens=new();
        readonly List<long> waitingCrabs=new();
        readonly List<long> nearbyPile=new();
        readonly Dictionary<long,CrabHaulVisual> crabViews=new();
        readonly Dictionary<Vector3,GameObject> denViews=new();
        readonly List<long> expiredCrabViews=new();
        readonly List<GameObject> denBlocks=new();
        Vector3 pileOrigin,pileDen;
        float nextCrabScan,pileAt;
        long pileLead;
        double CrabTime => TimeManager.TicksToTime(TimeManager.Tick)+TimeManager.GetTickElapsedAsDouble();

        bool PiledFish(DroppedItem item) => item.kind==DroppedKind.Catch && item.handDropped && !item.spoiled &&
            !item.crabCarried && item.depth==CurrentDepth && !CargoDispatch.ContainsDrop(this,item.position) &&
            (NereidVessel.Active==null || !NereidVessel.Active.ContainsSealed(item.position));

        bool CrabFloor(Vector3 point,out Vector3 floor)
        {
            floor=point;
            if(!Physics.Raycast(point+Vector3.up*1.5f,Vector3.down,out var hit,4.5f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ||
                hit.normal.y<.5f || hit.collider.GetComponentInParent<DiverMotor>() || hit.collider.GetComponentInParent<FishActor>())return false;
            floor=hit.point;
            return !CargoDispatch.ContainsDrop(this,floor) && (NereidVessel.Active==null || !NereidVessel.Active.ContainsSealed(floor));
        }

        void ScanFishPile()
        {
            bool found=false;
            foreach(var item in dropped)
            {
                if(!PiledFish(item) || !CrabFloor(item.position,out var floor) || item.position.y-floor.y>1.05f)continue;
                bool seen=false;
                foreach(var diver in DiverMotor.ServerDivers)
                    if(diver && !diver.IsDead && !diver.IsInside && (diver.transform.position-item.position).sqrMagnitude<22*22)
                    {seen=true;break;}
                if(!seen)continue;
                nearbyPile.Clear();
                foreach(var other in dropped)
                    if((other.position-item.position).sqrMagnitude<PileRadius*PileRadius && PiledFish(other))
                        nearbyPile.Add(other.id);
                if(nearbyPile.Count<PileThreshold)continue;
                found=true;
                if(pileLead==0 || (pileOrigin-item.position).sqrMagnitude>PileRadius*PileRadius)
                {pileOrigin=item.position;pileAt=Time.time;pileLead=item.id;}
                if(Time.time-pileAt<4f || !FindCrabDen(floor,out pileDen))break;
                waitingCrabs.AddRange(nearbyPile);
                pileLead=0;
                break;
            }
            if(!found)pileLead=0;
        }

        bool FindCrabDen(Vector3 at,out Vector3 den)
        {
            den=default;
            foreach(var site in crabDens)
                if((site-at).sqrMagnitude<54){den=site;return true;}
            if(crabDens.Count>=12)return false;
            for(int i=0;i<22;i++)
            {
                float angle=i*2.4f;
                var outward=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                Vector3 probe=at+outward*(4.4f+(i%4)*.48f);
                if(!CrabFloor(probe,out var floor) || Mathf.Abs(floor.y-at.y)>1.25f)continue;
                den=floor;
                crabDens.Add(den);
                var backing=new GameObject("Rock backing · crab fissure");
                backing.transform.SetParent(transform,false);
                backing.transform.position=den+Vector3.up*.18f;
                backing.AddComponent<BoxCollider>().size=new Vector3(.72f,.38f,.62f);
                denBlocks.Add(backing);
                return true;
            }
            return false;
        }

        void TickCrabs()
        {
            double now=CrabTime;
            for(int i=crabHauls.Count-1;i>=0;i--)
            {
                var haul=crabHauls[i];
                int slot=DropIndex(haul.FishId);
                if(slot<0){crabHauls.RemoveAt(i);continue;}
                float age=(float)(now-haul.Started);
                if(age<CrabPick)continue;
                var item=dropped[slot];
                if(age>=CrabFinish)
                {
                    RemoveDrop(haul.FishId);
                    crabHauls.RemoveAt(i);
                    continue;
                }
                if(!item.crabCarried)
                {
                    item.crabCarried=true;
                    looseFishMotion.Remove(item.id);
                }
                var desired=CrabHaulVisual.Payload(haul,age);
                if(age<CrabFinish-.65f && CrabFloor(desired,out var floor))
                    desired.y=floor.y+.19f;
                if((item.position-desired).sqrMagnitude<.006f && dropped[slot].crabCarried)continue;
                item.position=desired;
                var forward=Vector3.ProjectOnPlane(haul.Den-haul.Source,Vector3.up);
                item.yaw=forward.sqrMagnitude>.001f?Quaternion.LookRotation(forward).eulerAngles.y:0;
                dropped[slot]=item;
            }
            if(Time.time<nextCrabScan)return;
            nextCrabScan=Time.time+.48f;
            if(waitingCrabs.Count==0 && crabHauls.Count==0)ScanFishPile();
            if(waitingCrabs.Count==0 || crabHauls.Count>=ConcurrentHauls)return;
            long id=waitingCrabs[0];
            waitingCrabs.RemoveAt(0);
            int fishSlot=DropIndex(id);
            if(fishSlot<0 || !PiledFish(dropped[fishSlot]))return;
            var fish=dropped[fishSlot];
            if(!CrabFloor(fish.position,out var ground) || fish.position.y-ground.y>1.12f)return;
            Vector3 first=Vector3.Lerp(ground,pileDen,.23f);
            if(!CrabFloor(first,out var burrow))burrow=ground;
            crabHauls.Add(new CrabHaul{FishId=id,Burrow=burrow,Source=fish.position,Den=pileDen,Started=now,Crew=(byte)(2+id%3)});
        }

        void ResetCrabs()
        {
            foreach(var haul in crabHauls)
            {
                int slot=DropIndex(haul.FishId);
                if(slot<0 || !dropped[slot].crabCarried)continue;
                var fish=dropped[slot];
                fish.crabCarried=false;
                dropped[slot]=fish;
                TrackDeadFish(fish);
            }
            crabHauls.Clear();
            crabDens.Clear();
            waitingCrabs.Clear();
            pileLead=0;
            nextCrabScan=0;
            foreach(var block in denBlocks)if(block)Destroy(block);
            denBlocks.Clear();
        }

        void PresentCrabs()
        {
            if(!IsClientInitialized)return;
            foreach(var den in crabDens)
                if(!denViews.ContainsKey(den))denViews.Add(den,CrabHaulVisual.BuildDen(transform,den));
            if(denViews.Count>0 && crabDens.Count==0)
            {
                foreach(var den in denViews.Values)Destroy(den);
                denViews.Clear();
            }
            expiredCrabViews.Clear();
            expiredCrabViews.AddRange(crabViews.Keys);
            double now=CrabTime;
            foreach(var haul in crabHauls)
            {
                expiredCrabViews.Remove(haul.FishId);
                if(!crabViews.TryGetValue(haul.FishId,out var group))
                {
                    var graphic=new GameObject("Reef crabs · "+haul.FishId);
                    graphic.transform.SetParent(transform,false);
                    group=graphic.AddComponent<CrabHaulVisual>();
                    group.Build(haul);
                    crabViews.Add(haul.FishId,group);
                }
                group.Present(haul,(float)(now-haul.Started));
            }
            foreach(long id in expiredCrabViews)
            {
                Destroy(crabViews[id].gameObject);
                crabViews.Remove(id);
            }
        }

        void StopCrabViews()
        {
            foreach(var visual in crabViews.Values)if(visual)Destroy(visual.gameObject);
            foreach(var den in denViews.Values)if(den)Destroy(den);
            crabViews.Clear();
            denViews.Clear();
        }
    }
}
