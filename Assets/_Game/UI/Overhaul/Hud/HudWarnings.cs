using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mark = Raid.UI.DiveInstruments.Mark;

namespace Raid.UI
{
    internal sealed class HudWarnings
    {
        internal enum Signal { None, Oxygen, CriticalAir, Hypoxia, Reserve, Pressure, CriticalPressure, Attack, Impact, Wave, Trap, Down, Dead }
        readonly float[] nextPulse=new float[(int)Signal.Dead+1];
        readonly RectTransform safe,card;
        readonly Canvas canvas;
        readonly HudWarningEdges edges;
        readonly Image glyph,rule;
        readonly Image escapeTrack,escapeFill;
        readonly TMP_Text title,detail;
        Signal signal;
        DiverMotor observed;
        RaidWorld observedWorld;
        int bite,airBand;
        float lastHealth,lastAir,hurtUntil,hurtSide,opened,pulseStarted;
        Color tint;
        bool critical;
        internal HudWarnings(RectTransform safe)
        {
            this.safe=safe;canvas=safe.GetComponentInParent<Canvas>();
            var frame=UiKit.Node(safe,"Peripheral warning light");
            UiKit.Fill(frame);edges=frame.gameObject.AddComponent<HudWarningEdges>();edges.raycastTarget=false;edges.enabled=false;
            var body=UiKit.Panel(safe,"HUD Alert",HudEdgePanel.Surface);UiKit.Tile(body);card=body.rectTransform;
            rule=UiKit.Panel(card,"Alert edge",UiTheme.Amber);
            rule.rectTransform.anchorMin=new(0,.15f);rule.rectTransform.anchorMax=new(0,.85f);
            rule.rectTransform.offsetMin=Vector2.zero;rule.rectTransform.offsetMax=new(3,0);
            glyph=UiKit.Symbol(card,"Alert symbol",Mark.Bottle,UiTheme.Amber);
            title=UiKit.Label(card,"Alert Title","",21);title.fontStyle=FontStyles.Bold;
            title.alignment=TextAlignmentOptions.MidlineLeft;title.textWrappingMode=TextWrappingModes.NoWrap;
            detail=UiKit.Label(card,"Alert Detail","",17,UiTheme.Text);detail.alignment=TextAlignmentOptions.MidlineLeft;
            escapeTrack=UiKit.Panel(card,"Escape track",UiTheme.Edge);
            escapeFill=UiKit.Panel(escapeTrack.rectTransform,"Escape effort",UiTheme.Cyan);
            card.gameObject.SetActive(false);
        }
        internal void Refresh(UiContext c,float air)
        {
            var now=Time.unscaledTime;var p=c.Player;var threats=c.Threats;
            if(observed!=p || observedWorld!=c.World)
            {
                observed=p;observedWorld=c.World;
                bite=threats?threats.BiteSequence:0;lastHealth=p.Health;lastAir=p.Oxygen;hurtUntil=0;airBand=0;signal=Signal.None;
                System.Array.Clear(nextPulse,0,nextPulse.Length);
            }
            if(threats && bite!=threats.BiteSequence)
            {
                bite=threats.BiteSequence;
                if(threats.BiteVictim==p.OwnerId){hurtUntil=now+1.9f;hurtSide=-Vector3.Dot(p.transform.right,threats.BiteDirection.normalized);}
            }
            if(p.Health<lastHealth-1 && !p.IsHypoxic && !p.IsTrapped && !p.IsDead && hurtUntil<now)
            {hurtUntil=now+1.7f;hurtSide=0;}
            if(threats && !p.IsInside && lastAir-p.Oxygen>=3 && !p.IsTrapped && !p.IsDead && hurtUntil<now)
            {
                var reef=threats.Reef;
                float facing=0;
                bool eel=reef.Deep && reef.EelPhase>=2 && (reef.Eel-p.transform.position).sqrMagnitude<4*4;
                bool kraken=reef.Deep && reef.KrakenPhase==2 && (reef.Sweep-p.transform.position).sqrMagnitude<5*5;
                if(eel)facing=Vector3.Dot(p.transform.right,(reef.Eel-p.transform.position).normalized);
                if(eel || kraken){hurtUntil=now+1.7f;hurtSide=facing;}
            }
            lastAir=p.Oxygen;lastHealth=p.Health;
            if(p.IsInside)airBand=0;
            else if(air<=.10f)airBand=2;
            else if(air<=.20f || (airBand>0&&air<.24f))airBand=airBand==2 && air<.13f?2:1;
            else airBand=0;
            Signal next=Choose(c,now);
            if(next==Signal.None)
            {
                signal=next;card.gameObject.SetActive(false);edges.enabled=false;return;
            }
            UiText.Warning(next,c,air,out var name,out var advice,out var mark,out critical);
            if(next!=signal)
            {
                signal=next;
                bool fresh=now>=nextPulse[(int)next];
                opened=fresh?now:now-5;
                pulseStarted=fresh?now:now-2;
                if(fresh)nextPulse[(int)next]=now+9;
            }
            bool expanded=now-opened<4.5f || next is Signal.Down or Signal.Dead or Signal.Trap or Signal.Hypoxia;
            tint=critical?UiTheme.Red:UiTheme.Amber;
            if(next==Signal.Dead)tint=UiTheme.Muted;
            bool sub=expanded && advice.Length>0;
            float scale=HudEdgePanel.ReadableScale(canvas);card.localScale=Vector3.one*scale;
            float width=Mathf.Min(350,Mathf.Max(202,safe.rect.width*.39f/scale));
            card.gameObject.SetActive(true);
            bool breaking=next==Signal.Trap && p.CanBreakRaidGrip;
            UiKit.Place(card,Vector2.zero,Vector2.zero,new(27,162),new(width,breaking?87:sub?76:44));
            HudEdgePanel.AtTop(glyph.rectTransform,12,sub?-13:-7,30,30);
            HudEdgePanel.AtTop(title.rectTransform,49,sub?-7:-6,width-59,32);
            HudEdgePanel.AtTop(detail.rectTransform,49,-35,width-58,36);
            glyph.color=rule.color=title.color=tint;glyph.sprite=DiveInstruments.Art(mark);
            UiKit.Set(title,name);detail.gameObject.SetActive(sub);if(sub)UiKit.Set(detail,advice);
            escapeTrack.gameObject.SetActive(breaking);
            if(breaking)
            {
                float progress=p.TryGetComponent<RescueTool>(out var rescue)?rescue.DisplayProgress:0;
                HudEdgePanel.AtTop(escapeTrack.rectTransform,49,-73,width-65,5);
                escapeFill.rectTransform.anchorMin=Vector2.zero;
                escapeFill.rectTransform.anchorMax=new Vector2(progress,1);
                escapeFill.rectTransform.offsetMin=escapeFill.rectTransform.offsetMax=Vector2.zero;
            }
            edges.enabled=next!=Signal.Dead;Animate();
        }
        Signal Choose(UiContext c,float now)
        {
            var p=c.Player;
            if(p.IsDead)return Signal.Dead;
            if(p.IsTrapped)return Signal.Trap;
            if(p.IsHelpless)return Signal.Down;
            if(p.IsHypoxic)return Signal.Hypoxia;
            if(now<hurtUntil)return Signal.Impact;
            if(p.AirReserveRemaining>0)return Signal.Reserve;
            if(airBand==2)return Signal.CriticalAir;
            if(!p.IsInside && AttackNearby(c))return Signal.Attack;
            if(DiverMotor.PressureEnabled && !p.IsInside && p.PressureStress>=p.tuning.stressDanger)return Signal.CriticalPressure;
            if(c.DefendingReservoir && (!c.Threats.DefenseActive || c.Threats.DefenseAlarm))return Signal.Wave;
            if(airBand==1)return Signal.Oxygen;
            if(DiverMotor.PressureEnabled && !p.IsInside && p.PressureStress>=p.tuning.stressWarning)return Signal.Pressure;
            return Signal.None;
        }
        internal static bool AttackNearby(UiContext c)
        {
            var t=c.Threats;
            if(!t || !t.Reef.Active)return false;
            Vector3 here=c.Player.transform.position;
            if(ReefHabitats.InShelter(here))return false;
            var r=t.Reef;
            return ((t.AnglerStage is ThreatDirector.HuntStage.Display or ThreatDirector.HuntStage.Pursuit) && (here-t.AnglerPosition).sqrMagnitude<13f*13f)
                || (r.Deep && r.EelPhase==2 && (here-r.Eel).sqrMagnitude<7f*7f)
                || (r.Deep && r.KrakenPhase==1 && (here-r.Sweep).sqrMagnitude<4.3f*4.3f);
        }
        internal void Animate()
        {
            if(!edges.enabled || !card.gameObject.activeInHierarchy)return;
            float elapsed=Time.unscaledTime-pulseStarted;
            float envelope=Mathf.Clamp01(1-elapsed/.95f);
            float impact=UiRoot.Instance!=null?Mathf.Clamp01(UiRoot.Instance.Settings.hitImpulse):1f;
            float steady=critical?.11f:.045f;
            edges.Set(tint,steady+impact*envelope*envelope*(critical?.55f:.28f),signal==Signal.Impact?hurtSide:0);
        }
    }
}
