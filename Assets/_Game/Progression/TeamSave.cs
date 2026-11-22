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
        [Serializable] private sealed class LegacyFood { public int offering=0; }
        [Serializable] private sealed class LegacyWallet { public MaterialCost wallet=default; }
        [Serializable] private sealed class LegacyCrew { public LegacyWallet[] players; }

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
                if(progress.schemaVersion==1)
                    progress.MigrateV1(JsonUtility.FromJson<LegacyFood>(envelope.payload).offering,tuning==null?4:tuning.cargoCapacityPerWinch);
                else if(progress.schemaVersion==2)
                {
                    var combined=new MaterialCost();
                    var legacy=JsonUtility.FromJson<LegacyCrew>(envelope.payload);
                    if(legacy?.players!=null)
                        foreach(var profile in legacy.players)
                        {
                            if(profile==null || !profile.wallet.IsValid)throw new InvalidDataException("Invalid former wallet.");
                            combined=combined.Add(profile.wallet);
                        }
                    progress.MigrateV2(combined);
                }
                progress.Validate();
                progress.RecoverLegacySalvage();
                return progress;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is InvalidDataException || error is OverflowException)
            { throw new InvalidDataException("Team save is corrupt or unsupported; original preserved: " + path, error); }
        }

        private static string Hash(string payload)
        {
            using (SHA256 sha = SHA256.Create())
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        }
    }
}
