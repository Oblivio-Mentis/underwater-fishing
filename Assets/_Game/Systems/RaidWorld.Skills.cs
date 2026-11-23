using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Raid
{
    public sealed partial class RaidWorld
    {
        readonly Dictionary<NetworkConnection,string> crewProfiles = new();
        readonly Dictionary<long,string[]> catchOwners = new();
        const string LanProfilePreference="RAID.profile.radial";
        public PlayerProgress LocalProgress { get; private set; }
        public event Action<bool,string> SkillResponded;

        void UnbindProfile(NetworkConnection sender,FishNet.Transporting.RemoteConnectionStateArgs state)
        {
            if(state.ConnectionState==FishNet.Transporting.RemoteConnectionState.Stopped)
                crewProfiles.Remove(sender);
        }

        public string ProgressKey(DiverMotor diver)
        {
            if(diver==null || diver.Owner==null)return null;
            return crewProfiles.TryGetValue(diver.Owner,out var key)?key:null;
        }

        public int SkillRank(DiverMotor diver, SkillLine line) =>
            SkillTreeDefinition.Rank(team?.Player(ProgressKey(diver))?.skills,line);

        public bool HasSkill(DiverMotor diver, SkillLine line) => SkillRank(diver,line)>0;

        public int SkillValue(DiverMotor diver, SkillLine line) =>
            SkillTreeDefinition.Value(team?.Player(ProgressKey(diver))?.skills,line);

        public float SkillFactor(DiverMotor diver, SkillLine line) => SkillValue(diver,line)*.01f;
        public float TeamSkillFactor(SkillLine line) =>
            NetworkObject==null ? 0 : (IsServerInitialized ? team : view)?.TeamFactor(line) ?? 0;
        public int LocalRank(SkillLine line) => SkillTreeDefinition.Rank(LocalProgress?.skills,line);

        internal void RecordCatch(long id,IEnumerable<string> keys)
        {
            var owners=new SortedSet<string>(StringComparer.Ordinal);
            foreach(var key in keys)
                if(team.Player(key)!=null)owners.Add(key);
            if(owners.Count==0)return;
            var array=new string[owners.Count];
            owners.CopyTo(array);
            catchOwners[id]=array;
        }

        internal CatchItem AttributeCatch(CatchItem item)
        {
            if(catchOwners.TryGetValue(item.id,out var owners))item.caughtBy=owners;
            return item;
        }

        void ConnectProfile()
        {
            string key=PlayerPrefs.GetString(LanProfilePreference,"");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var args=Environment.GetCommandLineArgs();
            int fixture=Array.IndexOf(args,"-raid-profile");
            if(fixture>=0 && fixture+1<args.Length && Guid.TryParse(args[fixture+1],out var fixtureKey))
            {
                BindProfile(fixtureKey.ToString("N"));
                return;
            }
#endif
            if(!Guid.TryParseExact(key,"N",out _))
            {
                key=Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(LanProfilePreference,key);
                PlayerPrefs.Save();
            }
            BindProfile(key);
        }

        [ServerRpc(RequireOwnership=false)]
        void BindProfile(string localKey, NetworkConnection sender=null)
        {
            if(team==null || sender?.FirstObject==null || !sender.IsAuthenticated)return;
            if(crewProfiles.TryGetValue(sender,out var existing)){SendProfile(sender,team.Player(existing));return;}
            string key;
            if(sender.TransportIndex==1)
            {
                string address=sender.GetAddress();
                if(!ulong.TryParse(address,out var steamId) || steamId==0)return;
                key="steam:"+steamId;
            }
            else
            {
                if(!Guid.TryParseExact(localKey,"N",out var lanId))return;
                key="lan:"+lanId.ToString("N");
            }
            foreach(var pair in crewProfiles)
                if(pair.Value==key && pair.Key!=sender && pair.Key.IsActive && pair.Key.FirstObject!=null)
                {
                    SkillReply(sender,false,"Этот профиль уже в экспедиции");
                    return;
                }
            crewProfiles.Add(sender,key);
            SendProfile(sender,team.RegisterPlayer(key));
        }

        void SendProfile(NetworkConnection connection,PlayerProgress profile)
        {
            if(profile==null)return;
            ApplyProfile(connection,JsonUtility.ToJson(new PlayerProgress
                { skills=profile.skills, revision=profile.revision }));
        }

        [TargetRpc]
        void ApplyProfile(NetworkConnection target,string data) => LocalProgress=JsonUtility.FromJson<PlayerProgress>(data);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal void RefreshCrewProfilesForTrial()=>PublishProfiles();
#endif

        void PublishProfiles()
        {
            foreach(var pair in crewProfiles)
                if(pair.Key.IsActive && pair.Key.FirstObject!=null)SendProfile(pair.Key,team.Player(pair.Value));
        }

        [ServerRpc(RequireOwnership=false)]
        public void RequestSkill(SkillId id,int revision,int profileRevision,NetworkConnection sender=null)
        {
            if(sender?.IsAuthenticated!=true)return;
            var diver=sender.FirstObject==null?null:sender.FirstObject.GetComponent<DiverMotor>();
            var player=team?.Player(ProgressKey(diver));
            if(player==null || diver.IsHelpless || transitionRemaining>0 ||
                !ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree))
            {SkillReply(sender,false,"Подойдите к монитору прокачки");return;}
            if(!team.TryPurchase(player,id,revision,profileRevision,progression,SaveTransaction,out var reason))
            {
                SendProfile(sender,player);
                SkillReply(sender,false,reason);
                return;
            }
            Publish();
            SendProfile(sender,player);
            diver.ConfirmInteraction();
            SkillReply(sender,true,reason);
        }

        bool SaveTransaction()
        {
            if(saveFailed || team==null || string.IsNullOrEmpty(savePath))return false;
            try { TeamSave.Save(savePath,team);return true; }
            catch(Exception error)
            {
                Debug.LogError("[RAID Save] "+error.Message);
                return false;
            }
        }

        [TargetRpc]
        void SkillReply(NetworkConnection target,bool accepted,string explanation) => SkillResponded?.Invoke(accepted,explanation);
    }
}
