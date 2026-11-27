using UnityEngine;

namespace Raid
{
    public sealed partial class ThreatDirector
    {
        internal bool HookDefense(HarpoonTool tool,RaycastHit hit)
        {
            if(!IsServerInitialized || !DefenseActive || !hit.collider || !tool || tool.HasDefenseLine || tool.Motor.IsTrapped)return false;
            foreach(var a in attackers)
            {
                if(hit.collider!=a.collider || a.hooks.Count>=4)continue;
                a.hooks.Add(new DefenseHook{tool=tool,line=new FishingLine(Vector3.Distance(tool.FishingAnchor,a.data.position))});
                if(a.data.victim<0)a.target=null;
                a.data.impactOffset=Quaternion.Inverse(Quaternion.LookRotation(a.data.forward))*(hit.point-a.data.position);
                tool.AttachDefenseLine(a.data.id);
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
            HarpoonTool strongest=null;
            DiverMotor escapedFrom=null;
            float nearest=float.MaxValue,totalWinding=0;
            int engaged=0;
            Vector3 pull=Vector3.zero;
            if(a.data.victim>=0 && DefenseClock>=a.captureUntil)ReleaseVictim(a);
            // A short readable strain window twice per fight. Slack or a partner makes it safe to wind.
            float agitation=Mathf.Max(0,Mathf.Sin((a.life+a.data.id*.57f)*3.3f));
            float surge=agitation>.65f?Mathf.InverseLerp(.65f,1,agitation):0;
            if(a.attached && a.latchTick>=.32f)
            {HitField(a.card.shieldDamage*a.latchTick*a.data.scale*.68f,a.goal);a.latchTick=0;}
            if(a.attached)a.latchTick+=dt;
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
                float distance=Vector3.Distance(a.data.position,position);
                bool clear=ClearDefenseLine(position,a.data.position);
                hook.obstructed=clear?0:hook.obstructed+dt;
                if(distance>tool.tuning.maxRange+defenseTuning.harpoonRangeMargin || hook.obstructed>.7f)
                {
                    escapedFrom=tool.Motor;tool.ClearDefenseLine();a.hooks.RemoveAt(j);continue;
                }

                var world=RaidWorld.Instance;
                float cable=1+world.SkillFactor(tool.Motor,SkillLine.Cable);
                float teamwork=(a.hooks.Count>1 || tool.IsAssisted)?1.28f+world.SkillFactor(tool.Motor,SkillLine.SyncPull):1;
                float jawLoad=a.attached?1.27f:1;
                bool taut=hook.line.Loaded(distance,tool.tuning.slackAllowance);
                bool winding=tool.Reeling && clear && taut;
                hook.tension=Mathf.Clamp01(hook.tension+(winding
                    ? (.075f+surge*.36f)*jawLoad*a.data.scale*dt/(cable*teamwork)
                    : -tool.tuning.tensionRelease*dt));
                if(hook.tension>=1 && tool.TryStabilizeDeepLine())hook.tension=.58f;
                if(hook.tension>=1)
                {escapedFrom=tool.Motor;tool.ClearDefenseLine();a.hooks.RemoveAt(j);continue;}
                tool.UpdateFishingLine(hook.line.PaidOut,hook.tension);
                if(!winding)continue;
                float reel=Mathf.Clamp(tool.ReelMultiplier,.5f,2.2f);
                hook.line.Wind(distance,tool.tuning.slackAllowance,tool.tuning.reelSpeed*reel*teamwork,dt);
                if(surge>.1f && a.data.kind is DefenseSpecies.Rammer or DefenseSpecies.Abductor)
                {
                    float damp=Mathf.Clamp(world.SkillFactor(tool.Motor,SkillLine.Damper),0,.65f);
                    tool.Motor.ApplyTetherMotion((a.data.position-position).normalized*dt*surge*(1-damp)*(tool.IsAssisted?1.2f:2.35f));
                }
                float strength=reel*teamwork*(1+world.SkillFactor(tool.Motor,SkillLine.Tip)*.55f);
                totalWinding+=strength;
                pull+=(position-a.data.position).normalized*strength;
                engaged++;
                if(distance<nearest){nearest=distance;strongest=tool;}
            }
            if(a.hooks.Count==0)
            {
                OnDefenseEscape(a,escapedFrom);
                return true;
            }
            a.data.pose=AttackerPose.Fight;
            a.data.effort=surge;
            if(!strongest)return true;
            float agreement=Mathf.Clamp01(pull.magnitude/Mathf.Max(.01f,totalWinding));
            float combined=(engaged<=1?totalWinding:Mathf.Min(totalWinding,2.7f))*Mathf.Lerp(.66f,1,agreement);
            if(a.data.victim>=0)
            {
                a.rescuePull+=dt*combined;
                if(a.rescuePull>=defenseTuning.rescuePullSeconds)ReleaseVictim(a);
                else return true;
            }
            float needed=a.card.fightSeconds*Mathf.Lerp(.81f,1.18f,Mathf.InverseLerp(.78f,1.34f,a.data.scale))*(a.attached?1.12f:1);
            a.progress+=dt*combined*Mathf.Lerp(1,.60f,surge);
            if(!a.attached && nearest>1.55f)
            {
                var hand=strongest.FishingAnchor+Vector3.up*Mathf.Sin(a.life*5)*.19f+strongest.transform.right*Mathf.Sin(a.life*3.8f)*.37f;
                if(engaged>1 && pull.sqrMagnitude>.04f)hand=Vector3.Lerp(hand,a.data.position+pull.normalized*nearest,.55f);
                Steer(a,hand,Mathf.Min(strongest.tuning.reelSpeed*combined,4.6f)*(1-surge*.72f),dt);
            }
            else if(a.attached)
            {
                a.data.forward=Vector3.RotateTowards(a.data.forward,
                    (DefenseCenter-a.data.position).normalized,dt*3,1);
            }
            a.data.effort=Mathf.Max(surge,.22f);
            if(a.progress<needed || !a.attached && nearest>Mathf.Max(2.45f,strongest.tuning.captureRange))return true;
            if(a.attached)TearRemora(a);
            RemoveAttacker(index,true,false,strongest);
            return true;
        }
    }
}
