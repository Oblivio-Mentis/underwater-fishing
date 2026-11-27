using FishNet.Object.Synchronizing;
using Raid.Nereid;
using UnityEngine;

namespace Raid
{
    internal enum GoshaJunkLocation : byte { Seabed, Carried, Shown, Built }

    internal struct GoshaJunk
    {
        public GoshaJunkKind Kind;
        public GoshaJunkLocation Location;
        public Vector3 Position;
        public float Yaw;
    }

    public sealed partial class RaidWorld
    {
        const float DenSettleSeconds=.84f;
        readonly SyncList<GoshaJunk> goshaJunk=new();
        readonly System.Collections.Generic.Dictionary<int,float> unreachableJunk=new();
        int huntJunk=-1,usefulBeforeJunk=1;
        float denBuildTime,junkRetryAt;
        bool CanBuildDen => DeliveryRank>=3;

        // Loose finds belong to the current dive; placed pieces live in the campaign.
        internal static readonly Vector2[] JunkSpots={
            new(14,-14),new(13,-9),new(16,-5),new(10,5),new(17,1),new(11,-3),new(13,-20),
            new(19,-18),new(19,-12),new(12,12),new(7,16),new(-8,13),new(-11,-1),new(24,-9),
            new(19,8),new(10,-24),new(16,-27),new(-10,-9),new(-13,4),new(-9,-17)
        };

        internal int GoshaJunkCount=>goshaJunk.Count;
        internal GoshaJunk GoshaJunkAt(int index)=>goshaJunk[index];

        void SeedGoshaJunk()
        {
            var vessel=NereidVessel.Active;
            if(!vessel || goshaJunk.Count>0 || Time.time<junkRetryAt)return;
            junkRetryAt=Time.time+2;
            if(expeditionLayout && expeditionLayout.goshaJunkSites is {Length:>0} sites)
            {
                for(int i=0;i<sites.Length;i++)if(sites[i])AddJunkAt(sites[i].position,i);
                return;
            }
            var receiver=GoshaReceiver.Active;
            Vector3 lockExit=LockPoint(12.6f);
            Vector3 hullExit=receiver?receiver.TransferPosition:lockExit;
            float side=Mathf.Sign(vessel.transform.InverseTransformPoint(hullExit).x);
            var outward=vessel.transform.right*(side==0?1:side);
            for(int i=0;i<6;i++)
            {
                float fore=(i-2.5f)*2.7f;
                var step=outward*(2.5f+(i%3)*2.6f)+vessel.transform.forward*fore;
                AddJunkAt(hullExit+step,i);
                AddJunkAt(lockExit+vessel.transform.right*6.1f+vessel.transform.forward*fore,6+i);
            }
            for(int i=0;i<JunkSpots.Length;i++)
            {
                var spot=JunkSpots[i];
                AddJunkAt(vessel.transform.TransformPoint(new Vector3(spot.x,0,spot.y)),12+i);
            }
        }

        void AddJunkAt(Vector3 location,int variation)
        {
            if(!MarineSpawn.Floor(CurrentDepth,location,out var floor) || floor.normal.y<.58f)return;
            var at=floor.point+Vector3.up*.11f;
            if(!MarineSpawn.InOpenWater(CurrentDepth,at+Vector3.up*.54f,.38f) ||
                SwimZone(at)!=PetZone.Sea || GoshaSwimRoute.Risk(at)>.55f)return;
            foreach(var piece in goshaJunk)
                if((piece.Position-at).sqrMagnitude<2.6f)return;
            goshaJunk.Add(new GoshaJunk{Kind=(GoshaJunkKind)((variation+CurrentDepth*3)%6),
                Position=at,Yaw=(variation*77+CurrentDepth*19)%360});
        }

        void ResetGoshaJunk()
        {
            goshaJunk.Clear();
            unreachableJunk.Clear();
            junkRetryAt=0;
        }

        int SelectJunk()
        {
            SeedGoshaJunk();
            if(CanBuildDen && team.goshaDen.Count>=TeamProgress.GoshaDenCapacity)return -1;
            float range=HuntTuning.searchRadius[Mathf.Clamp(ScentRank,0,HuntTuning.searchRadius.Length-1)];
            float best=float.PositiveInfinity;
            int selected=-1;
            for(int i=0;i<goshaJunk.Count;i++)
            {
                var item=goshaJunk[i];
                if(item.Location!=GoshaJunkLocation.Seabed && !(CanBuildDen && item.Location==GoshaJunkLocation.Shown))continue;
                if(SwimZone(item.Position)!=PetZone.Sea ||
                    (unreachableJunk.TryGetValue(i,out float nearbyAgain) && Time.time<nearbyAgain))continue;
                float risk=GoshaSwimRoute.Risk(item.Position);
                if(risk>GoshaSwimRoute.RiskLimit(0))continue;
                var offset=item.Position-pet.Position;
                float horizontal=new Vector2(offset.x,offset.z).magnitude;
                if(horizontal>range || Mathf.Abs(offset.y)>range*1.8f)continue;
                float score=horizontal+Mathf.Abs(offset.y)*.34f+risk*9;
                if(score>=best || (Autonomy>=2 && WatchedByCrew(item.Position,out _)))continue;
                best=score;selected=i;
            }
            return selected;
        }

        void HuntJunkTick(float dt)
        {
            var item=goshaJunk[huntJunk];
            Vector3 approach=item.Position+Vector3.up*.46f;
            if(!Approach(approach,1.10f,dt))
            {
                grabWait=0;
                if(Time.time-huntStartedAt>HuntTuning.chaseTimeout*1.5f)
                {
                    unreachableJunk[huntJunk]=Time.time+17;
                    huntJunk=-1;hunt.Reset();scanAt=Time.time+1.4f;
                }
                return;
            }
            Face(Vector3.ProjectOnPlane(item.Position-pet.Position,Vector3.up));
            grabWait+=dt;
            if(grabWait<HuntTuning.grabSeconds)return;
            item.Location=GoshaJunkLocation.Carried;
            goshaJunk[huntJunk]=item;
            carried.Add(new GoshaCatch{JunkSlot=huntJunk});
            huntJunk=-1;grabWait=0;denBuildTime=0;deliverWait=0;hunt.Reset();
        }

        void PutJunkDown(int index,Vector3 at)
        {
            if(index<0 || index>=goshaJunk.Count)return;
            var item=goshaJunk[index];
            if(item.Location!=GoshaJunkLocation.Carried)return;
            item.Location=GoshaJunkLocation.Shown;
            if(NereidVessel.Active && NereidAirlock.Active && SwimZone(at)==PetZone.Sea && MarineSpawn.Floor(CurrentDepth,at,out var floor))
                at=floor.point+Vector3.up*.11f;
            item.Position=at;
            goshaJunk[index]=item;
        }

        void DeliverJunk(int slot,float dt)
        {
            int index=carried[slot].JunkSlot;
            var vessel=NereidVessel.Active;
            if(!vessel)return;
            if(!CanBuildDen || team.goshaDen.Count>=TeamProgress.GoshaDenCapacity)
            {
                var diver=NearestSeaDiver(pet.Position,30);
                Vector3 meeting=diver?diver.transform.position+Vector3.up*.42f:LockPoint(12.6f);
                if(!Approach(meeting,1.95f,dt) && (deliverWait+=dt)<HuntTuning.cageWait)return;
                PutJunkDown(index,pet.Position+pet.Heading*.7f);
                FinishJunkDelivery(slot);
                return;
            }

            if(TryEnterServiceHatch(false,dt) || !TravelTo(GoshaDenLayout.Approach(vessel),dt))return;
            var placement=GoshaDenLayout.Piece(vessel,team.goshaDen.Count);
            Face(Vector3.ProjectOnPlane(placement.position-pet.Position,Vector3.up));
            denBuildTime+=dt;
            pet.DenPlacement=(byte)Mathf.Clamp(Mathf.RoundToInt(denBuildTime/DenSettleSeconds*255),1,255);
            if(denBuildTime<DenSettleSeconds)return;
            var junk=goshaJunk[index];
            team.goshaDen.Add(junk.Kind);
            if(!SaveTransaction())
            {
                team.goshaDen.RemoveAt(team.goshaDen.Count-1);
                PutJunkDown(index,pet.Position+pet.Heading*.45f);
                FinishJunkDelivery(slot);
                return;
            }
            junk.Location=GoshaJunkLocation.Built;
            goshaJunk[index]=junk;
            Publish();
            FinishJunkDelivery(slot);
        }

        void FinishJunkDelivery(int slot)
        {
            carried.RemoveAt(slot);
            usefulBeforeJunk=2;
            denBuildTime=0;pet.DenPlacement=0;
            DeliveryComplete();
        }
    }
}
