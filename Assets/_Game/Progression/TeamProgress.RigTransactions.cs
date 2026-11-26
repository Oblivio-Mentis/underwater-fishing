using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    public sealed partial class TeamProgress
    {
        // Both the manual hoist and direct courier arrivals settle a prefix of the same manifest.
        // A failed/throwing save restores the original IDs and every balance affected by Deposit.
        public int SettleRigCargo(TechRig source,ProgressionTuning tuning,bool direct,Func<bool> persist)
        {
            int slot=rigs.IndexOf(source);
            if(slot<0 || source.contents.Count==0)return 0;
            int cargoStart=cargo.Count, tankStart=tank.Count, ledgerStart=deliveredIds.Count;
            var wallet=Treasury;
            long lifetime=lifetimeCatch;
            int revision=teamRevision;
            string mode=source.mode, flight=source.flight;
            List<CatchItem> removed=null;
            bool removedRig=false,committed=false;
            try
            {
                int moved=0;
                while(moved<source.contents.Count)
                {
                    var item=source.contents[moved];
                    if(!(direct?Deposit(item,tuning):LoadCargo(item,tuning)))break;
                    moved++;
                }
                if(moved==0)return 0;
                removed=source.contents.GetRange(0,moved);
                source.contents.RemoveRange(0,moved);
                if(source.contents.Count==0)
                {
                    if(source.kind=="basket"){rigs.RemoveAt(slot);removedRig=true;}
                    if(source.mode=="captured")source.mode="placed";
                    if(source.kind=="mail" && source.flight=="transit")source.flight="";
                }
                committed=persist==null || persist();
                return committed?moved:0;
            }
            finally
            {
                if(!committed)
                {
                    if(removedRig)rigs.Insert(slot,source);
                    if(removed!=null)source.contents.InsertRange(0,removed);
                    cargo.RemoveRange(cargoStart,cargo.Count-cargoStart);
                    tank.RemoveRange(tankStart,tank.Count-tankStart);
                    deliveredIds.RemoveRange(ledgerStart,deliveredIds.Count-ledgerStart);
                    Treasury=wallet;lifetimeCatch=lifetime;teamRevision=revision;
                    source.mode=mode;source.flight=flight;
                }
            }
        }

        public bool PackRigBasket(TechRig source,int capacity,Vector3 position,int owner,Func<bool> persist)
        {
            int count=Math.Min(capacity,source.contents.Count);
            if(count<=0 || !rigs.Contains(source) || lastRigSerial==int.MaxValue)return false;
            int serial=lastRigSerial;
            var basket=AddRig("basket",position,owner);
            basket.contents.AddRange(source.contents.GetRange(0,count));
            source.contents.RemoveRange(0,count);
            bool committed=false;
            try{return committed=persist==null || persist();}
            finally
            {
                if(!committed)
                {source.contents.InsertRange(0,basket.contents);rigs.Remove(basket);lastRigSerial=serial;}
            }
        }

        public bool RepairRig(TechRig rig,int scrapCost,Func<bool> persist)
        {
            if(!rigs.Contains(rig) || rig.health>=1 || scrapCost<0 || scrap<scrapCost ||
                (scrapCost>0 && teamRevision==int.MaxValue))return false;
            int balance=scrap, revision=teamRevision;
            float health=rig.health;
            string mode=rig.mode;
            scrap-=scrapCost;
            if(scrapCost>0)teamRevision++;
            rig.health=1;
            if(rig.mode=="damaged")rig.mode="placed";
            bool committed=false;
            try{return committed=persist==null || persist();}
            finally
            {
                if(!committed){scrap=balance;teamRevision=revision;rig.health=health;rig.mode=mode;}
            }
        }
    }
}
