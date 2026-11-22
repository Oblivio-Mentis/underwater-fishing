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
        const string LanProfilePreference="RAID.profile.v1";
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

        public bool HasSkill(DiverMotor diver, SkillId id) =>
            team?.Player(ProgressKey(diver))?.skills.Contains(id)==true;

        public int SkillValue(DiverMotor diver, SkillId id) =>
            HasSkill(diver,id) ? progression.Skill(id).Magnitude : 0;

        public float SkillFactor(DiverMotor diver, SkillId id) => SkillValue(diver,id)*.01f;

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
                { wallet=profile.wallet, skills=profile.skills, revision=profile.revision }));
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
        public void RequestSkill(SkillId id,int revision,NetworkConnection sender=null)
        {
            if(sender==null)return;
            var diver=sender.FirstObject==null?null:sender.FirstObject.GetComponent<DiverMotor>();
            var player=team?.Player(ProgressKey(diver));
            var node=progression.Skill(id);
            if(player==null || diver.IsHelpless || transitionRemaining>0 ||
                !ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree))
            {SkillReply(sender,false,"Подойдите к монитору навыков живым водолазом");return;}
            if(node==null){SkillReply(sender,false,"Неизвестный навык");return;}
            var owned=node.Team ? team.teamSkills : player.skills;
            if(owned.Contains(id))
            {SkillReply(sender,false,node.Team?"Уже улучшено экипажем":"Навык уже открыт");return;}
            if(revision!=(node.Team?team.teamRevision:player.revision))
            {SendProfile(sender,player);SkillReply(sender,false,"Данные обновились. Выберите узел снова");return;}
            if(!SkillTreeDefinition.IsAvailable(id,owned) || !player.wallet.Covers(node.Cost))
            {SkillReply(sender,false,"Не хватает материалов или предыдущего навыка");return;}
            var before=player.wallet;
            int previousPlayer=player.revision, previousTeam=team.teamRevision;
            if(previousPlayer==int.MaxValue || node.Team && previousTeam==int.MaxValue)
            {SkillReply(sender,false,"Достигнут предел версии сохранения");return;}
            owned.Add(id);
            player.wallet=before.Subtract(node.Cost);
            player.revision++;
            if(node.Team)team.teamRevision++;
            if(!SaveTransaction())
            {
                owned.Remove(id);
                player.wallet=before;
                player.revision=previousPlayer;
                team.teamRevision=previousTeam;
                SkillReply(sender,false,"Сохранение недоступно. Материалы остались у вас");
                return;
            }
            Publish();
            SendProfile(sender,player);
            SkillReply(sender,true,node.Name);
        }

        [ServerRpc(RequireOwnership=false)]
        public void RequestTreasury(MaterialCost amount,int revision,NetworkConnection sender=null)
        {
            if(sender==null)return;
            var diver=sender.FirstObject==null?null:sender.FirstObject.GetComponent<DiverMotor>();
            var player=team?.Player(ProgressKey(diver));
            if(player==null || diver.IsHelpless || transitionRemaining>0 ||
                (!ShipConsole.CanUseKind(diver,ShipConsoleKind.SkillTree) && !ShipConsole.CanUseKind(diver,ShipConsoleKind.Upgrades)))
            {SkillReply(sender,false,"Казна доступна у бортового компьютера");return;}
            if(revision!=player.revision || player.revision==int.MaxValue || amount.IsEmpty || !player.wallet.Covers(amount))
            {SkillReply(sender,false,"Проверьте баланс и сумму взноса");return;}
            var treasury=team.Treasury;
            var balance=player.wallet;
            try { team.Treasury=treasury.Add(amount); }
            catch(OverflowException){SkillReply(sender,false,"Казна достигла предела");return;}
            player.wallet=balance.Subtract(amount);
            player.revision++;
            if(!SaveTransaction())
            {
                team.Treasury=treasury;
                player.wallet=balance;
                player.revision--;
                SkillReply(sender,false,"Сохранение недоступно. Взнос отменён");
                return;
            }
            Publish();
            SendProfile(sender,player);
            SkillReply(sender,true,"Взнос поступил в казну NEREID");
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
