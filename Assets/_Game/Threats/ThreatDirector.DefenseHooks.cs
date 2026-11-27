using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        static FishFightProfile predatorFightProfile;
        const float PredatorHandReach=1.15f;

        internal bool HookDefense(HarpoonTool tool,RaycastHit hit,out HarpoonContact contact)
        {
            contact=default;
            if(!IsServerInitialized || !DefenseActive || !hit.collider || !tool || tool.HasDefenseLine || tool.Motor.IsTrapped)return false;
            foreach(var a in attackers)
            {
                if(hit.collider.transform!=a.collider.transform || a.hooks.Count>=4)continue;
                contact=HarpoonContact.FromHit(NetworkObject,a.collider.transform,false,hit,tool.Motor.AimDirection);
                contact.defenseId=a.data.id;
                float distance=Vector3.Distance(tool.FishingAnchor,hit.point);
                if(a.hooks.Count==0)
                {
                    if(!predatorFightProfile)predatorFightProfile=Resources.Load<FishFightProfile>("ExpeditionDefenseFight");
                    var profile=predatorFightProfile?predatorFightProfile:tool.tuning.FightProfile("Needlefish");
                    a.fight=new FishFightBrain(profile,unchecked(Defense.seed*397^a.data.id));
                    a.data.fightPhase=a.data.victim<0?FishBehaviour.HookShock:FishBehaviour.None;
                    a.data.fightAt=DefenseClock;
                }
                else a.fight.Shock();
                a.hooks.Add(new DefenseHook{tool=tool,contact=contact,line=new FishingLine(distance),tension=.1f});
                if(a.data.victim<0)a.target=null;
                a.data.impactOffset=Quaternion.Inverse(Quaternion.LookRotation(a.data.forward))*(hit.point-a.data.position);
                tool.AttachDefenseLine(a.data.id);
                tool.UpdateFishingLine(distance,.1f);
                var s=state.Value;s.WaveHits++;state.Value=s;
                return true;
            }
            return false;
        }

        internal void ReleaseDefenseHook(HarpoonTool tool)
        {
            foreach(var a in attackers)
            {
                int index=a.hooks.FindIndex(x=>x.tool==tool);
                if(index<0)continue;
                a.hooks.RemoveAt(index);
                tool.ClearDefenseLine();
                if(a.hooks.Count==0)OnDefenseEscape(a,tool.Motor);
                return;
            }
            tool.ClearDefenseLine();
        }

        void ClearHooks(DefenseAttacker a,bool escaped)
        {
            var victim=a.hooks.Count>0 && a.hooks[0].tool?a.hooks[0].tool.Motor:null;
            foreach(var hook in a.hooks)if(hook.tool)hook.tool.ClearDefenseLine();
            a.hooks.Clear();
            if(escaped)OnDefenseEscape(a,victim);
        }

        void OnDefenseEscape(DefenseAttacker a,DiverMotor diver)
        {
            a.progress*=.56f;
            a.data.remoraPull=Vector3.zero;
            a.data.fightPhase=FishBehaviour.None;
            a.data.effort=0;
            a.fight=null;
            if(a.data.victim>=0)
            {
                SetPose(a,AttackerPose.Carry,Mathf.Max(0,(float)(a.captureUntil-DefenseClock)));
                return;
            }
            if(Defense.phase!=DefensePhase.Assault){SetPose(a,AttackerPose.Carry,0);return;}
            if(a.attached && a.data.kind==DefenseSpecies.Remora)
            {
                SetPose(a,AttackerPose.Latch,0);
                return;
            }
            a.counter=EligibleVictim(diver) && raidRandom.NextDouble()<a.card.counterChance;
            a.target=a.counter?diver:null;
            SetPose(a,AttackerPose.Approach,0);
        }

        bool TickDefenseHooks(int index,DefenseAttacker a,float dt)
        {
            DiverMotor escapedFrom=null;
            if(a.attached && a.latchTick>=.32f)
            {HitField(a.card.shieldDamage*a.latchTick*a.data.scale*.68f,a.goal);a.latchTick=0;}
            if(a.attached)a.latchTick+=dt;

            float nearest=float.MaxValue, range=0;
            bool windingInput=false, obstructed=false, assisted=false;
            for(int j=a.hooks.Count-1;j>=0;j--)
            {
                var hook=a.hooks[j];
                var tool=hook.tool;
                if(!tool || !tool.IsServerInitialized || !tool.HarpoonInHands || tool.Motor.IsHelpless || tool.Motor.IsTrapped)
                {
                    if(tool){escapedFrom=tool.Motor;tool.ClearDefenseLine();}
                    a.hooks.RemoveAt(j);continue;
                }
                var position=tool.FishingAnchor;
                hook.anchor=DefenseLineAnchor(a,hook);
                hook.distance=Vector3.Distance(hook.anchor,position);
                bool clear=ClearDefenseLine(position,hook.anchor);
                hook.obstructed=clear?0:hook.obstructed+dt;
                if(hook.distance>tool.tuning.maxRange+defenseTuning.harpoonRangeMargin || hook.obstructed>.7f)
                {
                    escapedFrom=tool.Motor;tool.ClearDefenseLine();a.hooks.RemoveAt(j);continue;
                }
                nearest=Mathf.Min(nearest,hook.distance);
                range=Mathf.Max(range,tool.tuning.maxRange);
                windingInput|=tool.Reeling && clear;
                obstructed|=!clear;
                assisted|=tool.IsAssisted;
            }
            if(a.hooks.Count==0)
            {
                OnDefenseEscape(a,escapedFrom);
                return true;
            }

            float needed=FightWork(a);
            var brain=a.fight;
            float surge=0;
            bool rescuing=a.data.victim>=0;
            if(!rescuing)
            {
                brain.Tick(new FishFightInput {
                    Dt=dt, Stamina01=1-Mathf.Clamp01(a.progress/needed),
                    Distance01=Mathf.Clamp01(nearest/Mathf.Max(1,range)),
                    Hooks=a.hooks.Count, Coordinated=assisted || a.hooks.Count>1,
                    Obstructed=obstructed, Reeling=windingInput, NearHand=nearest<=PredatorHandReach,
                    Exhausted=a.progress>=needed
                });
                a.data.fightPhase=brain.CurrentPhase;
                if(brain.PhaseChanged)a.data.fightAt=DefenseClock;
                surge=brain.TugStrength;
                if(brain.SurgeStarted)a.data.fightTug++;
            }
            else a.data.fightPhase=FishBehaviour.None;
            a.data.effort=surge;
            if(a.data.pose!=AttackerPose.Fight)SetPose(a,AttackerPose.Fight,0);

            DefenseHook strongest=null;
            float totalWinding=0, loadedTension=0;
            int engaged=0;
            Vector3 pull=Vector3.zero;
            var world=RaidWorld.Instance;
            for(int j=a.hooks.Count-1;j>=0;j--)
            {
                var hook=a.hooks[j];
                var tool=hook.tool;
                float cable=1+world.SkillFactor(tool.Motor,SkillLine.Cable);
                float teamwork=(a.hooks.Count>1 || tool.IsAssisted)?1.28f+world.SkillFactor(tool.Motor,SkillLine.SyncPull):1;
                float jawLoad=a.attached?1.27f:1;
                bool winding=tool.Reeling && hook.obstructed<=0 && hook.line.Loaded(hook.distance,tool.tuning.slackAllowance);
                if(!winding)hook.line.Slip(hook.distance);
                float resistance=rescuing?.065f:brain.Weakened?.055f:.105f+.89f*surge;
                hook.tension=Mathf.Clamp01(hook.tension+(winding
                    ? resistance*jawLoad*a.data.scale*dt/(cable*teamwork)
                    : -tool.tuning.tensionRelease*dt));
                if(hook.tension>=1 && tool.TryStabilizeDeepLine())hook.tension=.58f;
                if(hook.tension>=1)
                {escapedFrom=tool.Motor;tool.ClearDefenseLine();a.hooks.RemoveAt(j);continue;}
                if(!winding){tool.UpdateFishingLine(hook.line.PaidOut,hook.tension);continue;}
                float reel=Mathf.Clamp(tool.ReelMultiplier,.5f,2.2f);
                hook.line.Wind(hook.distance,tool.tuning.slackAllowance,tool.tuning.reelSpeed*1.58f*reel*teamwork,dt);
                tool.UpdateFishingLine(hook.line.PaidOut,hook.tension);
                Vector3 lineDirection=(tool.FishingAnchor-hook.anchor).normalized;
                if(surge>.1f && a.data.kind is DefenseSpecies.Rammer or DefenseSpecies.Abductor)
                {
                    float damp=Mathf.Clamp(world.SkillFactor(tool.Motor,SkillLine.Damper),0,.65f);
                    tool.Motor.ApplyTetherMotion(-lineDirection*dt*surge*(1-damp)*(tool.IsAssisted?1.2f:2.35f));
                }
                float strength=reel*teamwork*(1+world.SkillFactor(tool.Motor,SkillLine.Tip)*.55f);
                totalWinding+=strength;
                loadedTension+=hook.tension*strength;
                pull+=lineDirection*strength;
                engaged++;
                if(strongest==null || hook.distance<strongest.distance)strongest=hook;
            }
            if(a.hooks.Count==0)
            {
                OnDefenseEscape(a,escapedFrom);
                return true;
            }
            if(a.attached)
            {
                Vector3 stretch=Vector3.zero;
                if(totalWinding>.01f && pull.sqrMagnitude>.01f)
                {
                    float strain=Mathf.Clamp01(.28f+.46f*(loadedTension/totalWinding)+.55f*a.progress/needed);
                    stretch=(pull/totalWinding)*(strain*1.24f*a.data.scale);
                }
                a.data.remoraPull=Vector3.MoveTowards(a.data.remoraPull,stretch,dt*5.5f);
            }
            if(strongest==null)
            {
                if(!a.attached && !rescuing && !brain.Weakened)SwimAgainstLine(a,a.hooks[0],brain,0,dt);
                return true;
            }
            float agreement=Mathf.Clamp01(pull.magnitude/Mathf.Max(.01f,totalWinding));
            float combined=(engaged<=1?totalWinding:Mathf.Min(totalWinding,2.7f))*Mathf.Lerp(.66f,1,agreement);
            if(rescuing)
            {
                a.rescuePull+=dt*combined;
                if(a.rescuePull<defenseTuning.rescuePullSeconds)return true;
                a.target?.PresentRaidReleasePose();
                ReleaseVictim(a);
            }
            a.progress+=dt*combined*Mathf.Lerp(1,.71f,surge);
            if(!a.attached)SwimAgainstLine(a,strongest,brain,combined,dt);
            if(a.progress<needed)return true;
            float reach=Mathf.Max(PredatorHandReach,strongest.tool.tuning.captureRange);
            if(!a.attached && (brain.Surging || !strongest.line.AtHand(
                Vector3.Distance(strongest.tool.FishingAnchor,DefenseLineAnchor(a,strongest)),reach,.48f)))return true;
            if(a.attached)TearRemora(a);
            RemoveAttacker(index,true,false,strongest.tool);
            return true;
        }

        static Vector3 DefenseLineAnchor(DefenseAttacker a,DefenseHook hook)
        {
            Vector3 point=a.collider.transform.TransformPoint(hook.contact.localPoint);
            if(!a.attached || a.data.remoraPull.sqrMagnitude<.00001f)return point;
            return new RemoraStretch {
                direction=a.data.forward, mouth=a.goal, displacement=a.data.remoraPull,
                inverseLength=1/Mathf.Max(.2f,1.68f*a.data.scale)
            }.Point(point);
        }

        void SwimAgainstLine(DefenseAttacker a,DefenseHook hook,FishFightBrain brain,float winding,float dt)
        {
            var tool=hook.tool;
            Vector3 eye=tool.FishingAnchor;
            Vector3 away=(hook.anchor-eye).normalized;
            if(away.sqrMagnitude<.01f)away=a.data.forward;
            Vector3 side=Vector3.Cross(Vector3.up,away).normalized;
            Vector3 climb=Vector3.Cross(away,side);
            float sway=brain.Lateral*brain.Profile.lateralStrength;
            Vector3 motion=(side+climb*.30f)*sway*dt;
            if(brain.Surging || winding<=0)
            {
                float escape=Mathf.Lerp(.48f,Mathf.Min(5.8f,a.card.burstSpeed*.56f),brain.TugStrength);
                if(winding>0)
                    escape=Mathf.Min(escape,Mathf.Max(0,hook.line.PaidOut+.36f-hook.distance)/Mathf.Max(.001f,dt));
                motion+=away*(escape*dt);
            }
            else
            {
                float excess=Mathf.Max(0,hook.distance-hook.line.PaidOut);
                float maxStep=Mathf.Min(tool.tuning.reelSpeed*winding*1.48f,7.5f)*dt;
                motion-=away*Mathf.Min(excess,maxStep);
                float length=Mathf.Max(hook.distance,hook.line.PaidOut+tool.tuning.slackAllowance);
                motion=Vector3.ClampMagnitude(hook.anchor+motion-eye,length)+eye-hook.anchor;
                float minBodyGap=.76f*a.data.scale;
                if(Vector3.Distance(a.data.position+motion,eye)<minBodyGap)
                    motion=Vector3.ProjectOnPlane(motion,(a.data.position-eye).normalized);
            }
            if(motion.sqrMagnitude<.00001f)return;
            var before=a.data.position;
            a.nextRoute=0;
            Steer(a,before+motion,motion.magnitude/Mathf.Max(.001f,dt),dt,Defense.breached,tool.Motor);
            if((a.data.position-before).sqrMagnitude<motion.sqrMagnitude*.1f && DefenseClock>=a.nextFightDeflection)
            {
                brain.Deflect();
                a.nextFightDeflection=DefenseClock+.45f;
            }
        }

        static float FightWork(DefenseAttacker a) =>
            a.card.fightSeconds*Mathf.Lerp(.41f,.59f,Mathf.InverseLerp(.78f,1.34f,a.data.scale));
    }
}
