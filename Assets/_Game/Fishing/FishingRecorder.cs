#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Raid
{
    // Development-only frame recorder for the owned diver camera. Writes numbered PNG frames
    // (HUD excluded, URP stack included) that Tools/RAID018/Make-Clip.ps1 stitches with ffmpeg.
    // Never touches gameplay or network state. Start with the QA probe action "record" or
    // -raid-record <folder> <seconds>.
    [DefaultExecutionOrder(980)]
    public sealed class FishingRecorder : MonoBehaviour
    {
        public static FishingRecorder Active { get; private set; }
        public bool Recording => until > Time.unscaledTime;
        public int Frames { get; private set; }
        string folder;
        float until, nextFrame, interval = 1f / 20f;
        RenderTexture target;
        Camera owned;
        readonly RenderPipeline.StandardRequest request = new();
        readonly Queue<(int index, AsyncGPUReadbackRequest readback)> pending = new();
        int width = 1280, height = 720;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-raid-record");
            if (i < 0 || i + 2 >= args.Length) return;
            Begin(args[i + 1], float.Parse(args[i + 2], System.Globalization.CultureInfo.InvariantCulture));
        }

        public static FishingRecorder Begin(string directory, float seconds, int fps = 20)
        {
            if (Active == null) Active = new GameObject("RAID-018 owned camera recorder").AddComponent<FishingRecorder>();
            Active.folder = Path.GetFullPath(directory);
            Directory.CreateDirectory(Active.folder);
            Active.interval = 1f / Mathf.Clamp(fps, 5, 30);
            Active.until = Time.unscaledTime + Mathf.Clamp(seconds, 1, 300);
            Active.Frames = 0;
            return Active;
        }

        public static void Stop() { if (Active != null) Active.until = 0; }

        void LateUpdate()
        {
            Drain();
            if (!Recording || Time.unscaledTime < nextFrame) return;
            var ui = UI.UiRoot.Instance;
            var diver = ui == null ? null : ui.Context.Player;
            if (owned == null || !owned.isActiveAndEnabled)
                owned = diver != null && diver.IsOwner ? diver.GetComponentInChildren<Camera>() : null;
            if (owned == null) return;
            nextFrame = Time.unscaledTime + interval;
            if (target == null) target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "RAID-018 recorder" };
            var previous = owned.targetTexture;
            try
            {
                owned.targetTexture = target;
                RenderPipeline.SubmitRenderRequest(owned, request);
            }
            finally { owned.targetTexture = previous; }
            pending.Enqueue((Frames++, AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32)));
        }

        void Drain()
        {
            while (pending.Count > 0 && (pending.Peek().readback.done || pending.Count > 8))
            {
                var (index, readback) = pending.Dequeue();
                if (!readback.done) readback.WaitForCompletion();
                if (readback.hasError) continue;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(readback.GetData<byte>());
                tex.Apply(false);
                File.WriteAllBytes(Path.Combine(folder, $"frame-{index:D5}.png"), tex.EncodeToPNG());
                Destroy(tex);
            }
        }

        void OnDestroy()
        {
            while (pending.Count > 0) { var p = pending.Dequeue(); p.readback.WaitForCompletion(); }
            if (target != null) Destroy(target);
            if (Active == this) Active = null;
        }
    }
}
#endif
