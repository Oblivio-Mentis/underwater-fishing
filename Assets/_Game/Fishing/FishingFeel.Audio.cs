using UnityEngine;

namespace Raid
{
    public sealed partial class FishingFeel
    {
        AudioSource cueSource, reelSource;
        AudioClip shotClip, hookClip, coordinatedClip, soloClip, warningClip;
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
            shotClip = LoadFishingClip("shot");
            hookClip = LoadFishingClip("hook");
            coordinatedClip = LoadFishingClip("coordinated");
            soloClip = LoadFishingClip("solo");
            warningClip = LoadFishingClip("warning");
            reelSource.clip = LoadFishingClip("reel");
        }

        static AudioClip LoadFishingClip(string id)
        {
            var clip = Resources.Load<AudioClip>("FishingAudio/" + id);
            if (clip == null) Debug.LogError("Missing original fishing audio: " + id);
            return clip;
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

        void OnDestroy()
        {
            if (tool != null) tool.ShotVisual -= PlayShotAudio;
            if (reelSource != null) reelSource.Stop();
        }
    }
}
