#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid.Nereid
{
    internal sealed partial class CargoReceiverTrial
    {
        readonly MaterialPropertyBlock beaconSample = new();
        readonly List<string> filmTimes = new();
        Transform cableSocket;
        float parkedYaw, maxYaw, initialSlew, beamError, initialLight, maxWorkLight;
        float minimumFlash = float.PositiveInfinity, maximumFlash;
        bool observedPark, observedDock, recordCrane;
        float nextFilmAt, filmStart;

        void ObserveDavit(HoistPhase phase, float progress)
        {
            var arm = rail.davit;
            if (!arm || !arm.workFlood || !arm.deckWash || !arm.warningLens || !arm.portGlass || !arm.floodGlass)
                throw new InvalidOperationException("Missing authored crane or lighting hardware.");
            float yaw = arm.bearing.localEulerAngles.y;
            if (!observedPark)
            {
                if (phase != HoistPhase.Loading) throw new InvalidOperationException("Expected a grounded basket at the start.");
                parkedYaw = yaw;
                initialLight = arm.workFlood.enabled || arm.deckWash.enabled ? 1 : 0;
                cableSocket = rail.cageAnchor.GetComponentInChildren<CargoBasket>().CableSocket;
                recordCrane = host && Array.IndexOf(Environment.GetCommandLineArgs(), "-raid-crane-film") >= 0;
                if (recordCrane) Directory.CreateDirectory(Path.Combine(folder, "crane-film"));
                CraneFrame("crane-01-parked.jpg");
                observedPark = true;
            }
            float angle = Mathf.Abs(Mathf.DeltaAngle(parkedYaw, yaw));
            maxYaw = Mathf.Max(maxYaw, angle);
            if (phase == HoistPhase.Raising && progress <= .15f) initialSlew = Mathf.Max(initialSlew, angle);
            if (phase == HoistPhase.Raising && progress > .18f && progress < .91f)
            {
                if (!arm.workFlood.enabled || !arm.deckWash.enabled) throw new InvalidOperationException("Working crane lights are off.");
                maxWorkLight = Mathf.Max(maxWorkLight, arm.workFlood.intensity);
                beamError = Mathf.Max(beamError, Vector3.Angle(arm.workFlood.transform.forward,
                    cableSocket.position - arm.workFlood.transform.position));
                arm.warningLens.GetPropertyBlock(beaconSample);
                float brightness = beaconSample.GetColor("_BaseColor").r;
                minimumFlash = Mathf.Min(minimumFlash, brightness);
                maximumFlash = Mathf.Max(maximumFlash, brightness);
            }
            if (phase == HoistPhase.Raising && progress > .19f && photos.Add("crane pickup"))
                CraneFrame("crane-02-lifting.jpg");
            if (phase == HoistPhase.Unloading && !observedDock)
            {
                CraneFrame("crane-03-inboard.jpg");
                observedDock = true;
            }
            if (recordCrane && (phase != HoistPhase.Loading || seenRaise) && Time.unscaledTime >= nextFilmAt)
            {
                if (filmTimes.Count == 0) filmStart = Time.unscaledTime;
                filmTimes.Add((Time.unscaledTime - filmStart).ToString("F4", CultureInfo.InvariantCulture));
                CraneFrame("crane-film/" + filmTimes.Count.ToString("D4") + ".jpg");
                nextFilmAt = Time.unscaledTime + 1f / 16;
            }
            if (seenRaise && phase == HoistPhase.Loading)
            {
                CraneFrame("crane-04-returned.jpg");
                if (recordCrane) File.WriteAllLines(Path.Combine(folder, "crane-film", "times.txt"), filmTimes);
                timeline.Add($"Crane slew before lift {Number(initialSlew)} deg; maximum from park {Number(maxYaw)} deg; " +
                    $"returned bearing error {Number(angle)} deg; flood aiming error {Number(beamError)} deg; peak intensity {Number(maxWorkLight)}; " +
                    $"beacon red channel {Number(minimumFlash)}..{Number(maximumFlash)}; work lights off at both ends " +
                    $"{initialLight==0 && !arm.workFlood.enabled && !arm.deckWash.enabled}.");
                if (initialLight != 0 || arm.workFlood.enabled || arm.deckWash.enabled || !observedDock ||
                    initialSlew < Mathf.Abs(arm.parkedSlew) * .80f || maxYaw < 60f || angle > 1f ||
                    maxWorkLight < .9f * arm.floodIntensity || beamError > 2f || maximumFlash - minimumFlash < .7f)
                    throw new InvalidOperationException(timeline[timeline.Count - 1]);
            }
        }

        void CraneFrame(string name)
        {
            var hull = rail.transform;
            view.transform.position = hull.TransformPoint(new Vector3(17f, 11.1f, -22f));
            view.transform.LookAt(hull.TransformPoint(new Vector3(4.6f, 7.7f, -9.9f)));
            var previous = RenderTexture.active;
            try
            {
                RenderPipeline.SubmitRenderRequest(view, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                photo.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                photo.Apply();
                File.WriteAllBytes(Path.Combine(folder, name), photo.EncodeToJPG(87));
            }
            finally { RenderTexture.active = previous; }
        }
    }
}
#endif
