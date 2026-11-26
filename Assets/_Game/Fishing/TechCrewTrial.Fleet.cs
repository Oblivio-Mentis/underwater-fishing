#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Raid.UI;
using UnityEngine;

namespace Raid
{
    internal sealed partial class TechCrewTrial
    {
        [Serializable] sealed class Fleet
        {
            public int installations, last, catches, bytes;
            public string hash;
        }

        // Compare stable gameplay data. Ownership changes legitimately in the handoff and is checked separately.
        static string FleetDigest(TeamProgress team)
        {
            var text=new StringBuilder();
            void AddCatch(CatchItem item)=>text.Append(JsonUtility.ToJson(item.PublicCopy())).Append('|');
            foreach(var rig in team.rigs.OrderBy(r=>r.serial))
            {
                text.Append(rig.serial).Append('/').Append(rig.kind).Append('/').Append(rig.depth).Append(':');
                foreach(var item in rig.contents)AddCatch(item);
                text.Append('+');
                foreach(var item in rig.brood)AddCatch(item);
                text.AppendLine();
            }
            foreach(var item in team.tank)AddCatch(item);
            text.AppendLine();
            foreach(var item in team.cargo)AddCatch(item);
            text.AppendLine();
            foreach(long id in team.deliveredIds)text.Append(id).Append(',');
            text.AppendLine();
            foreach(var node in team.techPurchases.OrderBy(p=>p.item,StringComparer.Ordinal).ThenBy(p=>p.upgrade,StringComparer.Ordinal))
                text.Append(node.item).Append('/').Append(node.upgrade).Append('/').Append(node.rank).Append('|');
            text.Append(JsonUtility.ToJson(team.Treasury)).Append('/').Append(team.teamRevision);
            using var sha=SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        Fleet StageFleet()
        {
            var team=world.Team;
            void Own(SkillId skill)
            {
                if(team.teamSkills.Contains(skill))return;
                foreach(var parent in SkillTreeDefinition.Dependencies(skill))Own(parent);
                team.teamSkills.Add(skill);
            }
            Own(SkillTreeDefinition.Id(SkillLine.Hull,1));
            foreach(var definition in TechCatalog.Items)
            {
                foreach(var node in definition.upgrades.Where(n=>n.id=="unlock" || n.id=="limit" || n.id=="capacity"))
                    if(team.TechRank(definition.id,node.id)==0)
                        team.techPurchases.Add(new TechPurchase{item=definition.id,upgrade=node.id,rank=node.values.Length});
            }
            CatchItem Specimen(int depth)=>new(team.NewTechCatchId(),3,1,1,1,depth,species:"Needlefish");
            TechRig Park(string kind)
            {
                int number=team.rigs.Count;
                var at=world.CargoLoadingPosition+new Vector3((number%8)*2.4f+18,-3,number/8*2.4f+12);
                var rig=team.AddRig(kind,at,-1);
                rig.Detach();
                return rig;
            }
            int expected=world.tuning.maxSampleBaskets;
            foreach(var definition in TechCatalog.Items)
            {
                int limit=TechRig.TotalLimit(team,definition);
                expected+=limit;
                while(team.rigs.Count(r=>r.kind==definition.id)<limit)
                {
                    var rig=Park(definition.id);
                    if(rig.kind!="trap")continue;
                    rig.depth=2;
                    int capacity=Mathf.RoundToInt(definition.capacity*team.TechValue("trap","capacity",1));
                    for(int i=0;i<capacity;i++)rig.contents.Add(Specimen(2));
                }
            }
            for(int i=0;i<world.tuning.maxSampleBaskets;i++)
            {
                var basket=Park("basket");
                for(int j=0;j<world.tuning.sampleBasketCapacity;j++)basket.contents.Add(Specimen(1));
            }
            while(team.tank.Count<team.ReservoirCapacity(world.progression))
                if(!team.Deposit(Specimen(1),world.progression))throw new InvalidOperationException("Reservoir could not accept the stress fixture.");
            Assert(team.rigs.Count==expected && team.rigs.Where(r=>r.kind=="trap").All(r=>r.depth==2 && r.contents.Count>0),
                "host stages the licensed maximum for each invention, packed baskets and filled traps on the previous depth");
            TeamSave.Save(save,team);
            var view=team.PublicView();
            return new Fleet{installations=expected,last=team.lastRigSerial,
                catches=team.rigs.Sum(r=>r.contents.Count+r.brood.Count)+team.tank.Count+team.cargo.Count,
                bytes=Encoding.UTF8.GetByteCount(JsonUtility.ToJson(view)),hash=FleetDigest(view)};
        }

        IEnumerator VerifyFleet()
        {
            if(Host)Mark("fleet.json",JsonUtility.ToJson(StageFleet()));
            yield return Wait("server prepared a full loaded fleet",()=>Has("fleet.json"));
            var reference=JsonUtility.FromJson<Fleet>(File.ReadAllText(FileFor("fleet.json")));
            yield return Wait("largest serial and full fleet reached this network seat",()=>
                world.View.rigs.Count==reference.installations && world.View.lastRigSerial==reference.last,45);
            report.fleetCount=reference.installations;report.lastRigSerial=reference.last;
            report.snapshotBytes=reference.bytes;report.trackedCatchIds=reference.catches;report.fleetHash=reference.hash;
            Assert(FleetDigest(world.View)==reference.hash,
                "full fleet manifest matches host SHA256, including every catch, technology rank, wallet and revision");
            Mark("fleet-"+report.seat);
            yield return Wait("every client compared the full manifest",()=>All("fleet-"));
        }

        IEnumerator VerifyReconnection(int serial,string previousProfile)
        {
            if(report.seat==1)
            {
                var session=FindAnyObjectByType<RaidSession>();
                yield return Wait("remote transport fully stopped",()=>session.manager.IsOffline);
                session.JoinLocal();
                yield return Wait("same external player rejoined and received its private profile",()=>
                {
                    world=RaidWorld.Instance;
                    local=UiRoot.Instance?.Context?.Player;
                    return local && local.IsOwner && world && world.NetworkObject && world.IsClientInitialized && world.LocalProgress!=null &&
                        world.View!=null && world.View.rigs.Count==report.fleetCount;
                },60);
                Mark("returned-owner",local.OwnerId.ToString());
            }
            yield return Wait("peer reconnected using the original command-line profile",()=>Has("returned-owner"));
            if(Host)
            {
                int peer=int.Parse(File.ReadAllText(FileFor("returned-owner")));
                yield return Wait("server rebound the same persistent player key",()=>
                {
                    var connection=DiverMotor.ServerDivers.Find(d=>d && d.OwnerId==peer);
                    return connection && world.ProgressKey(connection)==previousProfile &&
                        world.Team.players.Count==report.players;
                });
                Mark("rebound");
            }
            yield return Wait("host confirmed the original crew record",()=>Has("rebound"));
            var state=world.View;
            Assert(state.rigs.Single(r=>r.serial==serial).CanRecall && state.rigs.All(r=>r.operatorId<0 && r.helperId<0 && r.baitId<0),
                "rejoining restores no departed ownership, second handles or voluntary tethers");
            Assert(state.TechRank("trap","unlock")==1 && state.TechRank("cage","unlock")==1 && FleetDigest(state)==report.fleetHash,
                "rejoining receives identical researched ranks, unique cargo IDs and treasury without a second purchase");
            Assert(state.players.Count==0,"replicated public snapshot contains no private profile or slot data");
            if(report.seat==1)
                Assert(InventoryDigest(world.LocalProgress)==File.ReadAllText(FileFor("private-stacks")),
                    "rejoined owner restored the exact two independent equipment stacks and profile revision");
            else
                Assert(world.LocalProgress.techSlots.All(slot=>slot.count==0),"another owner's two stacks never enter this private inventory");
            Mark("rejoined-"+report.seat);
            yield return Wait("all players observed the reconnect",()=>All("rejoined-"));
        }
    }
}
#endif
