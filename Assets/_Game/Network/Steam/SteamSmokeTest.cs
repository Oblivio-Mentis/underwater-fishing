using System;
using System.Collections;
using UnderwaterFishing.Player;
using UnityEngine;

namespace UnderwaterFishing.Network.Steam
{
    /// <summary>Opt-in development check using real Steam lobbies and the actual selected transport.</summary>
    public sealed class SteamSmokeTest : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            if ((!Application.isEditor && !Debug.isDebugBuild) || Array.IndexOf(args, "--raid-steam-smoke") < 0) yield break;
            var steam = GetComponent<SteamLobbyService>();
            var session = GetComponent<NetworkSession>();
            float deadline = Time.realtimeSinceStartup + 10;
            while (!session.Manager.Initialized && Time.realtimeSinceStartup < deadline) yield return null;
            if (!steam.TryInitialize()) { Fail(steam.Status); yield break; }
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                steam.CreateLobby();
                deadline = Time.realtimeSinceStartup + 30;
                while ((!session.Manager.IsClientStarted || NetworkDiver.Local == null) && Time.realtimeSinceStartup < deadline) yield return null;
                var router = session.Manager.TransportManager.Transport as RoutingTransport;
                if (steam.CurrentLobby == 0 || !session.Manager.IsClientStarted || NetworkDiver.Local == null || router == null || router.ActiveTransport != steam.Transport)
                { Fail($"cycle={cycle} {steam.Status}"); yield break; }
                Debug.Log($"RAID_STEAM_HOST cycle={cycle} lobby={steam.CurrentLobby} players={session.PlayerCount} transport={router.ActiveTransport.GetType().Name}");
                yield return new WaitForSecondsRealtime(1);
                int capture = Array.IndexOf(args, "--raid-steam-capture");
                if (cycle == 1 && capture >= 0 && capture + 1 < args.Length)
                {
                    ScreenCapture.CaptureScreenshot(args[capture + 1]);
                    yield return new WaitForSecondsRealtime(.5f);
                }
                steam.LeaveLobby();
                deadline = Time.realtimeSinceStartup + 10;
                while ((session.Connected || NetworkDiver.All.Count != 0) && Time.realtimeSinceStartup < deadline) yield return null;
                if (steam.CurrentLobby != 0 || session.Connected || NetworkDiver.All.Count != 0)
                { Fail($"cleanup cycle={cycle}"); yield break; }
                yield return new WaitForSecondsRealtime(1);
            }
            Debug.Log("RAID_STEAM_SMOKE_PASS cycles=3 remoteAccountsTested=false");
            session.Disconnect();
            Application.Quit(0);
        }

        private static void Fail(string reason)
        {
            Debug.LogError("RAID_STEAM_SMOKE_FAIL " + reason);
            Application.Quit(1);
        }
    }
}
