using UnityEngine;
using UnityEngine.Audio;

namespace Raid.Nereid
{
    public sealed partial class DefenseAtmosphereView
    {
        public enum CreatureCue : byte { Approach, Contact, Ability, Retreat }

        readonly AudioSource[] effects = new AudioSource[5];
        AudioSource roarWorld, roarWide, scoreLoop, fieldLoop;
        bool audioObserved, hasShieldState, shieldBroken, heardBroken, hasSceneRoarPoint, hasImpactSequence;
        int heardPhase, heardSeed, impactSequence, heardImpact;
        float integrity = 1, heardIntegrity = 1, heardImpactAge = float.PositiveInfinity, nextDarkCall;
        Vector3 sceneRoarPoint, heardImpactPoint;

        public void SetShieldState(float integrity01,bool broken)
        {
            hasShieldState = true;
            integrity = Mathf.Clamp01(integrity01);
            shieldBroken = broken;
        }

        public void SetImpactSequence(int sequence)
        {
            hasImpactSequence = true;
            impactSequence = sequence;
        }

        public void PlayCreatureCue(int kind,CreatureCue cue,Vector3 worldPoint,float importance = 1)
        {
            if (!isActiveAndEnabled || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var profile = Settings;
            EnsureAudio(profile);
            AudioClip clip = null;
            float gain = .60f;
            int priority = 100;
            switch (cue)
            {
                case CreatureCue.Contact:
                    clip = At(profile.creatureStrikes,kind);
                    gain = .72f;
                    priority = 84;
                    break;
                case CreatureCue.Ability:
                    clip = At(profile.creatureAbilities,kind);
                    break;
                case CreatureCue.Approach:
                    clip = At(profile.creatureAbilities,kind);
                    gain = .31f;
                    priority = 124;
                    break;
                case CreatureCue.Retreat:
                    clip = At(profile.darkCalls,kind % 3);
                    gain = .26f;
                    priority = 140;
                    break;
            }
            PlaySpatial(clip,worldPoint,profile.soundLevel*gain*Mathf.Clamp01(importance),priority,.97f);
        }

        static AudioClip At(AudioClip[] clips,int index)
            => clips != null && (uint)index < clips.Length ? clips[index] : null;

        void UpdateAudio(DefenseAtmosphereSettings profile,float elapsed,float orbitAge,Vector3 observer,float wet)
        {
            EnsureAudio(profile);
            bool eventChanged = !audioObserved || heardSeed != raidSeed;
            bool phaseChanged = eventChanged || heardPhase != phase;
            float distance = (observer-anchor).magnitude;
            float master = profile.soundLevel;
            if (phaseChanged && phase == 1 && elapsed < 2.25f)
            {
                Vector3 source = warningOrigin ? warningOrigin.position : hasSceneRoarPoint ? sceneRoarPoint : anchor;
                float local = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(7,29,(observer-source).magnitude));
                Play(roarWorld,profile.localRoar,source,master*.73f*local*wet,elapsed,1);
                Play(roarWide,profile.distantRoar,observer,master*Mathf.Lerp(.20f,.74f,1-local),elapsed,1);
            }
            if (phaseChanged && phase == 5 && elapsed < 1.1f)
                PlaySpatial(profile.shieldRestore,anchor,master*.68f*wet,60,1);

            if (!eventChanged && phase >= 3 && phase <= 4)
            {
                if (hasShieldState && shieldBroken && !heardBroken)
                    PlaySpatial(profile.shieldBreak,anchor,master*.88f*wet,32,1);
                if (hasShieldState && !shieldBroken && integrity <= .25f && heardIntegrity > .25f)
                    PlaySpatial(profile.shieldCritical,anchor,master*.66f*wet,55,1);
                bool recent = impactAge >= 0 && impactAge < .44f;
                bool freshHit = hasImpactSequence ? heardImpact != impactSequence
                    : impactAge+.025f < heardImpactAge || (impactPoint-heardImpactPoint).sqrMagnitude > .14f;
                if (recent && freshHit)
                    PlaySpatial(profile.shieldImpact,impactPoint,master*.67f*wet,68,Mathf.Lerp(.88f,1.10f,Unit((uint)(impactSequence+raidSeed))));
            }

            if (eventChanged) nextDarkCall = orbitAge + 1.3f;
            if (phase >= 2 && phase <= 4 && orbitAge >= nextDarkCall)
            {
                int tick = Mathf.FloorToInt(orbitAge*7);
                float random = Unit(unchecked((uint)(raidSeed + tick)));
                int index = (tick + (int)(random*700)) % Mathf.Clamp(profile.silhouetteCount,35,MaxSlots);
                OrbitPose caller = SampleOrbit(raidSeed,index,orbitAge,anchor,profile);
                PlaySpatial(At(profile.darkCalls,(int)(random*3)%3),caller.Position,
                    master*profile.swarmVolume*wet,162,Mathf.Lerp(.78f,1.16f,random));
                nextDarkCall = orbitAge + profile.swarmCallInterval*Mathf.Lerp(.72f,1.43f,random);
            }

            float score = phase switch
            {
                1 => Mathf.SmoothStep(0, .68f, Mathf.InverseLerp(.7f,5f,elapsed)),
                2 => .72f,
                3 or 4 => Mathf.Lerp(.76f,1,intensity),
                5 => Mathf.Lerp(.72f,0,Mathf.InverseLerp(0,4.6f,elapsed)),
                _ => 0
            };
            float stabilize = hasShieldState && shieldBroken ? 0 : Mathf.Lerp(.43f,1,integrity);
            float field = stabilize * Mathf.Lerp(.45f,1,(Mathf.Sin(Time.time*11)+1)*.5f*(1-integrity)) * .16f * wet;
            if (distance > 24) field = 0;
            TuneLoop(scoreLoop,profile.score,anchor,profile.scoreLevel*score,2.0f);
            TuneLoop(fieldLoop,profile.fieldHum,anchor,master*field,4.5f);
            audioObserved = true;
            heardPhase = phase;
            heardSeed = raidSeed;
            heardImpact = impactSequence;
            heardImpactAge = impactAge;
            heardImpactPoint = impactPoint;
            heardBroken = shieldBroken;
            heardIntegrity = integrity;
        }

        void EnsureAudio(DefenseAtmosphereSettings profile)
        {
            if (roarWorld) return;
            roarWorld = Source("Mouth roar",true,profile.effectsOutput,3,42,35);
            roarWide = Source("Long range roar",false,profile.effectsOutput,1,1,35);
            fieldLoop = Source("Field conduits",true,profile.effectsOutput,3,23,132);
            scoreLoop = Source("Defense score",false,profile.scoreOutput,1,1,148);
            for (int i = 0; i < effects.Length; i++)
                effects[i] = Source("Defense sound " + (i+1),true,profile.effectsOutput,3,39,128);
        }

        AudioSource Source(string label,bool local,AudioMixerGroup group,float minimum,float maximum,int importance)
        {
            var node = new GameObject(label);
            node.transform.SetParent(transform,false);
            var source = node.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.volume = 0;
            source.spatialBlend = local ? 1 : 0;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = minimum;
            source.maxDistance = maximum;
            source.dopplerLevel = 0;
            source.priority = importance;
            source.outputAudioMixerGroup = group;
            return source;
        }

        void PlaySpatial(AudioClip clip,Vector3 position,float gain,int priority,float pitch)
        {
            if (!clip || gain < .015f) return;
            AudioSource available = null;
            foreach (var voice in effects)
            {
                if (!voice.isPlaying)
                {
                    available = voice;
                    break;
                }
                if (voice.priority > priority && (!available || voice.priority > available.priority))
                    available = voice;
            }
            if (!available) return;
            available.priority = priority;
            Play(available,clip,position,gain,0,pitch);
        }

        static void Play(AudioSource source,AudioClip clip,Vector3 point,float volume,float age,float pitch)
        {
            if (!clip || volume < .01f || age >= clip.length) return;
            source.Stop();
            source.transform.position = point;
            source.clip = clip;
            source.pitch = pitch;
            source.volume = Mathf.Clamp01(volume);
            source.time = age;
            source.Play();
        }

        static void TuneLoop(AudioSource source,AudioClip clip,Vector3 point,float target,float response)
        {
            if (!clip) return;
            source.transform.position = point;
            float blend = 1-Mathf.Exp(-Time.unscaledDeltaTime*response);
            source.volume = Mathf.Lerp(source.volume,target,blend);
            if (source.volume < .003f && target <= .001f)
            {
                source.Stop();
                return;
            }
            if (source.isPlaying) return;
            source.loop = true;
            source.clip = clip;
            source.Play();
        }
    }
}
