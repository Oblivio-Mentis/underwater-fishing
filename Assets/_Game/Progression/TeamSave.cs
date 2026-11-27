using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Raid
{
    public static class TeamSave
    {
        [Serializable]
        private sealed class Envelope { public string format, payload, sha256; }

        [Serializable]
        sealed class LegacyTeamV6 { public LegacyPlayerV6[] players; }
        [Serializable]
        sealed class LegacyPlayerV6 { public string key; public TechSlot[] techSlots; }

        public static void Save(string path, TeamProgress progress)
        {
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            progress.Validate();
            string absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            string payload = JsonUtility.ToJson(progress);
            string document = JsonUtility.ToJson(new Envelope { format = "RAID.TeamProgress", payload = payload, sha256 = Hash(payload) });
            string temp = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(document);
                using (FileStream file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
                if (File.Exists(absolute)) File.Replace(temp, absolute, absolute + ".bak");
                else File.Move(temp, absolute);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        /// <summary>Missing/corrupt/unsupported saves throw; callers must explicitly choose recovery or a new team.</summary>
        public static TeamProgress Load(string path, ProgressionTuning tuning=null)
        {
            string document = File.ReadAllText(path, Encoding.UTF8);
            try
            {
                Envelope envelope = JsonUtility.FromJson<Envelope>(document);
                if (envelope == null || envelope.format != "RAID.TeamProgress" || string.IsNullOrEmpty(envelope.payload) ||
                    !string.Equals(envelope.sha256, Hash(envelope.payload), StringComparison.Ordinal))
                    throw new InvalidDataException("Save format or checksum mismatch.");
                TeamProgress progress = JsonUtility.FromJson<TeamProgress>(envelope.payload);
                if (progress == null) throw new InvalidDataException("Missing team progress.");
                if(progress.schemaVersion==4)
                {
                    progress.techPurchases=new System.Collections.Generic.List<TechPurchase>();
                    progress.rigs=new System.Collections.Generic.List<TechRig>();
                    progress.schemaVersion=5;
                }
                if(progress.schemaVersion==5)
                {
                    if(progress.players!=null)
                        foreach(var crew in progress.players)if(crew!=null)crew.pocketStacks=new TechSlot[TechSlot.PocketCount];
                    progress.schemaVersion=TeamProgress.CurrentSchema;
                }
                else if(progress.schemaVersion==6)
                {
                    MigrateV6Pockets(progress,envelope.payload);
                    progress.schemaVersion=TeamProgress.CurrentSchema;
                }
                if(progress.schemaVersion==7)progress.schemaVersion=TeamProgress.CurrentSchema;
                progress.Validate();
                return progress;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is InvalidDataException || error is OverflowException)
            { throw new InvalidDataException("Team save is corrupt or unsupported; original preserved: " + path, error); }
        }

        static void MigrateV6Pockets(TeamProgress progress,string payload)
        {
            var legacy=JsonUtility.FromJson<LegacyTeamV6>(payload);
            if(progress.players==null || legacy?.players?.Length!=progress.players.Count)
                throw new InvalidDataException("Missing crew inventory in schema 6.");
            for(int i=0;i<progress.players.Count;i++)
            {
                var player=progress.players[i];
                var previous=legacy.players[i];
                if(player==null || previous==null || previous.key!=player.key || previous.techSlots?.Length!=4)
                    throw new InvalidDataException("Invalid schema 6 equipment slots.");
                player.pocketStacks=new TechSlot[TechSlot.PocketCount];
                Array.Copy(previous.techSlots,player.pocketStacks,previous.techSlots.Length);
            }
        }

        private static string Hash(string payload)
        {
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }
    }
}
