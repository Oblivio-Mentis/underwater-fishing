using UnityEngine;

namespace Raid
{
    public sealed partial class FishingFeel
    {
        AudioSource cueSource, reelSource;
        AudioClip shotClip, hookClip, coordinatedClip, soloClip, warningClip, reelClip;
        bool audioReady;
        float nextListenerCheck, nextPreBreakWarning;

        internal int AudioCueCount { get; private set; }
        internal bool ReelAudioPlaying => reelSource != null && reelSource.isPlaying;

        void EnsureFishingAudio()
        {
            if (audioReady) return;
            audioReady = true;
            cueSource = gameObject.AddComponent<AudioSource>();
            cueSource.playOnAwake = false;
            cueSource.spatialBlend = 0;
            cueSource.dopplerLevel = 0;
            reelSource = gameObject.AddComponent<AudioSource>();
            reelSource.playOnAwake = false;
            reelSource.spatialBlend = 0;
            reelSource.dopplerLevel = 0;
            reelSource.loop = true;
            const int rate = 22050;
            shotClip = CreateClip("RAID shot / original synthesis", rate, .16f, (i, t) =>
                (Noise(i, 3) * .55f + Mathf.Sin(Mathf.PI * 2 * (150 * t - 240 * t * t)) * .45f) * Mathf.Exp(-t * 22));
            hookClip = CreateClip("RAID hook / original synthesis", rate, .24f, (i, t) =>
                (Mathf.Sin(Mathf.PI * 2 * 840 * t) * .55f + Mathf.Sin(Mathf.PI * 2 * 1260 * t) * .25f) * Mathf.Exp(-t * 23)
                + Noise(i, 7) * .22f * Mathf.Exp(-t * 55));
            coordinatedClip = CreateClip("RAID coordinated pull / original synthesis", rate, .30f, (i, t) =>
            {
                float local = t < .13f ? t : t - .13f;
                float frequency = t < .13f ? 520 : 780;
                return Mathf.Sin(Mathf.PI * 2 * frequency * local) * Mathf.Sin(Mathf.Clamp01(t / .30f) * Mathf.PI) * .65f;
            });
            soloClip = CreateClip("RAID solo tow / original synthesis", rate, .38f, (i, t) =>
                (Mathf.Sin(Mathf.PI * 2 * (95 * t - 45 * t * t)) * .65f + Noise(i, 11) * .12f) * Mathf.Exp(-t * 7));
            warningClip = CreateClip("RAID line warning / original synthesis", rate, .22f, (i, t) =>
                Mathf.Sin(Mathf.PI * 2 * 230 * t) * (Mathf.Sin(Mathf.PI * 2 * 7 * t) > 0 ? .55f : .18f) * Mathf.Exp(-t * 2));
            reelClip = CreateClip("RAID reel loop / original synthesis", rate, .12f, (i, t) =>
            {
                float phase = Mathf.PI * 2 * i / (rate * .12f);
                return (Mathf.Sin(phase * 18) * .32f + Mathf.Sin(phase * 90) * .09f + (Mathf.Sin(phase * 36) > .82f ? .18f : 0)) * .55f;
            });
        }

        void PlayShotAudio(Vector3 origin, Vector3 endpoint)
        {
            EnsureFishingAudio();
            PlayFishingCue(shotClip, tool.tuning.shotVolume);
        }

        void PlayFishingCue(AudioClip clip, float volume)
        {
            if (clip == null || cueSource == null) return;
            cueSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            AudioCueCount++;
        }

        void UpdateFishingAudio(bool fighting, bool controlling)
        {
            EnsureFishingAudio();
            if (Time.unscaledTime >= nextListenerCheck)
            {
                nextListenerCheck = Time.unscaledTime + .5f;
                bool hasListener = false;
                foreach (var listener in Object.FindObjectsByType<AudioListener>())
                    if (listener.isActiveAndEnabled) { hasListener = true; break; }
                if (!hasListener && eye != null && eye.GetComponent<AudioListener>() == null)
                    eye.gameObject.AddComponent<AudioListener>();
            }

            bool winding = fighting && controlling && tool.Reeling;
            if (winding)
            {
                reelSource.pitch = .85f + .45f * tool.LineTension;
                reelSource.volume = Mathf.Clamp01(tool.tuning.reelVolume * (.65f + .35f * tool.LineTension));
                if (!reelSource.isPlaying) reelSource.Play();
            }
            else if (reelSource.isPlaying) reelSource.Stop();

            if (fighting && controlling && tool.LineTension >= tool.tuning.preBreakWarningThreshold && Time.unscaledTime >= nextPreBreakWarning)
            {
                nextPreBreakWarning = Time.unscaledTime + tool.tuning.preBreakWarningInterval;
                PlayFishingCue(warningClip, tool.tuning.cueVolume);
            }
        }

        static AudioClip CreateClip(string name, int rate, float seconds, System.Func<int, float, float> sample)
        {
            int count = Mathf.CeilToInt(rate * seconds);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(sample(i, i / (float)rate), -1, 1);
            var clip = AudioClip.Create(name, count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Noise(int index, int seed)
        {
            float value = Mathf.Sin((index + seed * 17) * 12.9898f) * 43758.5453f;
            return (value - Mathf.Floor(value)) * 2 - 1;
        }

        void OnDestroy()
        {
            if (tool != null) tool.ShotVisual -= PlayShotAudio;
            if (reelSource != null) reelSource.Stop();
            foreach (var clip in new[] { shotClip, hookClip, coordinatedClip, soloClip, warningClip, reelClip })
                if (clip != null) Destroy(clip);
        }
    }
}
