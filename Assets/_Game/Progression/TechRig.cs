using System;
using System.Collections.Generic;
using UnityEngine;

namespace Raid
{
    [Serializable]
    public sealed class TechRig
    {
        public int serial, depth=1, operatorId=-1, helperId=-1, baitId=-1;
        public string kind, mode="placed", flight="";
        public Vector3 position, anchor, destination;
        public float timer, progress, health=1, air=-1, care;
        public int targetSerial, selection, cycle, towDiver=-1;
        public long towCargo;
        public bool CanRecall => operatorId<0 && helperId<0 && baitId<0 && contents.Count==0 && brood.Count==0 &&
            towCargo==0 && string.IsNullOrEmpty(flight) && (mode=="placed" || mode=="damaged");
        public CatchItem localStock;
        public List<CatchItem> contents=new(), brood=new();
        public bool Carrying(int player) => mode=="carried" && (operatorId==player || helperId==player);
        public static int TotalLimit(TeamProgress team,TechItem item) =>
            item==null?0:Mathf.Clamp(Mathf.RoundToInt(item.limit*(item.id is "charge" or "trap" or "bubbles" or "farm" or "mail" ? team.TechValue(item.id,"limit",1f) : 1f)),1,24);
        public void Detach()
        {
            operatorId=helperId=baitId=towDiver=-1;
            if(mode=="carried" || mode=="netting" || mode=="pumping" || mode=="armed")
                mode=kind=="cage" && contents.Count>0?"captured":"placed";
        }

        public TechRig PublicCopy()
        {
            var r=(TechRig)MemberwiseClone();
            r.contents=contents.ConvertAll(x=>x.PublicCopy());
            r.brood=brood.ConvertAll(x=>x.PublicCopy());
            r.localStock=null;
            return r;
        }
    }

    public sealed partial class TeamProgress
    {
        public List<TechRig> rigs=new();
        public long techCatchSequence;
        public int lastRigSerial;
        const long TechIdPrefix=1L<<60;
        [NonSerialized] HashSet<long> occupiedFutureIds;
        public long NewTechCatchId()
        {
            long next=techCatchSequence;
            do
            {
                if(next>=TechIdPrefix-1)throw new InvalidOperationException("No remaining industrial catch IDs.");
                next++;
            }while(occupiedFutureIds!=null && occupiedFutureIds.Contains(TechIdPrefix+next));
            techCatchSequence=next;
            return TechIdPrefix+next;
        }
        public TechRig AddRig(string kind,Vector3 position,int owner)
        {
            var rig=new TechRig{ serial=checked(++lastRigSerial),kind=kind,depth=expeditionDepth,
                operatorId=owner,mode="carried",position=position,anchor=position };
            rigs.Add(rig);
            return rig;
        }

        void ValidateTechRigs()
        {
            rigs??=new List<TechRig>();
            if(rigs.Count>400 || techCatchSequence<0 || techCatchSequence>=TechIdPrefix || lastRigSerial<0)
                throw new InvalidOperationException("Invalid equipment ledger.");
            var serials=new HashSet<int>();
            var used=new HashSet<long>(deliveredIds);
            foreach(var item in cargo) if(item!=null)used.Add(item.id);
            foreach(var rig in rigs)
            {
                if(rig==null || rig.serial<=0 || !serials.Add(rig.serial) || rig.depth<1 || rig.depth>2 ||
                    (rig.kind!="basket" && TechCatalog.Find(rig.kind)==null) || string.IsNullOrEmpty(rig.mode) ||
                    !FiniteRig(rig.position) || !FiniteRig(rig.anchor) || !FiniteRig(rig.destination) ||
                    !float.IsFinite(rig.health) || !float.IsFinite(rig.timer) || !float.IsFinite(rig.progress) ||
                    !float.IsFinite(rig.care) || !float.IsFinite(rig.air) || rig.contents==null || rig.brood==null)
                    throw new InvalidOperationException("Invalid installation.");
                lastRigSerial=Math.Max(lastRigSerial,rig.serial);
                foreach(var group in new[]{rig.contents,rig.brood})
                    foreach(var fish in group)
                        if(fish==null || !fish.IsValid || !used.Add(fish.id))throw new InvalidOperationException("Invalid equipment cargo.");
            }
            // Legacy manifests accepted arbitrary positive IDs. Preserve any that fall ahead of
            // the industrial allocator without letting one large imported ID exhaust the sequence.
            occupiedFutureIds?.Clear();
            foreach(long id in used)
                if(id>TechIdPrefix+techCatchSequence && id<TechIdPrefix*2)
                    (occupiedFutureIds??=new HashSet<long>()).Add(id);
        }
        static bool FiniteRig(Vector3 p) => float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
    }
}
