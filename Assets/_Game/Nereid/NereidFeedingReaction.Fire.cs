using UnityEngine;

namespace Raid.Nereid
{
    public sealed partial class NereidFeedingReaction
    {
        KrakenFlameFx fire;
        [SerializeField] bool playFieryBurpAudio;
        AudioSource throatAudio;
        AudioClip fireBelch, angryRoar;
        int fireSequence, firePhase;
        float fieryOpening, incomingFishGape;

        void PresentFieryBurp(RaidWorld world)
        {
            incomingFishGape=factory.FishAtMouth*.30f;
            var state=world.BowBreath;
            if(state.sequence==0){fieryOpening=0;if(fire)fire.Hide();return;}
            float age=Mathf.Max(0,(float)(world.BreathClock-state.startedAt));
            if(state.sequence!=fireSequence)
            {
                fireSequence=state.sequence;
                firePhase=0;
                if(age<.43f)
                {
                    if(playFieryBurpAudio)PlaySwallow();
                    if(creature)creature.PrepareBurp(KrakenBreathState.Ignite-age);
                    CameraImpulse?.Anticipate(mouth.position,KrakenBreathState.Ignite-age);
                }
            }

            float flame=state.Flame(age);
            float building=Mathf.Sin(Mathf.PI*Mathf.InverseLerp(.36f,KrakenBreathState.Ignite,age));
            float roarAge=age-KrakenBreathState.Roar;
            float roar=roarAge<0?0:Mathf.SmoothStep(0,1,roarAge/.13f)*Mathf.Exp(-roarAge*2.75f);
            fieryOpening=flame*.73f-building*.19f+roar*.71f;
            if(age>=KrakenBreathState.Ignite && firePhase<1)
            {
                firePhase=1;
                if(age<KrakenBreathState.Extinguish)
                {
                    if(creature)creature.ConfirmedBurp(2.3f);
                    CameraImpulse?.Release(mouth.position,2.3f);
                    PlayThroatSound(false,age-KrakenBreathState.Ignite);
                }
            }
            if(roarAge>=0 && firePhase<2)
            {
                firePhase=2;
                if(roarAge<.55f)
                {
                    if(creature)creature.ConfirmedBurp(1.6f);
                    CameraImpulse?.Release(mouth.position,1.5f);
                    PlayThroatSound(true,roarAge);
                }
            }
            if(flame>.01f && !fire)
            {
                var effect=new GameObject("Kraken fire breath");
                effect.transform.SetParent(mouth,false);
                fire=effect.AddComponent<KrakenFlameFx>();
            }
            if(fire)fire.Present(state,age);
        }

        void PlayThroatSound(bool roar,float offset)
        {
            if(!playFieryBurpAudio)return;
            if(!throatAudio)
            {
                throatAudio=mouth.gameObject.AddComponent<AudioSource>();
                throatAudio.playOnAwake=false;
                throatAudio.spatialBlend=1;
                throatAudio.minDistance=4;
                throatAudio.maxDistance=45;
                throatAudio.rolloffMode=AudioRolloffMode.Logarithmic;
            }
            if(roar && !angryRoar)angryRoar=Resources.Load<AudioClip>("KrakenRageRoar");
            if(!roar && !fireBelch)fireBelch=Resources.Load<AudioClip>("KrakenFireBelch");
            var voice=roar?angryRoar:fireBelch;
            if(!voice || offset>=voice.length)return;
            throatAudio.clip=voice;
            throatAudio.time=Mathf.Min(offset,throatAudio.clip.length-.01f);
            throatAudio.volume=roar?.86f:.72f;
            throatAudio.Play();
        }

        void ResetFieryBurp()
        {
            fieryOpening=incomingFishGape=0;
            fireSequence=firePhase=0;
            if(fire)fire.Hide();
            if(throatAudio)throatAudio.Stop();
        }

        void DestroyFieryBurp()
        {
            if(fire)Destroy(fire.gameObject);
            if(throatAudio)Destroy(throatAudio);
        }
    }
}
