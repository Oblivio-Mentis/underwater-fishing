using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    sealed class HudDefenseSignals
    {
        sealed class Marker
        {
            public RectTransform root;
            public Image icon, stem;
            public TMP_Text caption;
        }

        readonly Marker[] markers=new Marker[4];

        internal HudDefenseSignals(RectTransform parent)
        {
            for(int i=0;i<markers.Length;i++)
            {
                var card=UiKit.Panel(parent,"Defense contact",new Color(.12f,.022f,.032f,.81f));
                UiKit.Tile(card);
                UiKit.Place(card.rectTransform,Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(112,27));
                var icon=UiKit.Symbol(card.transform,"Contact type",DiveInstruments.Mark.Jaw,UiTheme.Red);
                UiKit.Place(icon.rectTransform,new Vector2(0,.5f),Vector2.one*.5f,new Vector2(17,0),new Vector2(21,21));
                var caption=UiKit.Label(card.transform,"Contact distance","",16,new Color(1,.44f,.40f));
                UiKit.Place(caption.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(31,0),new Vector2(78,23));
                var stem=UiKit.Panel(card.transform,"Bearing",UiTheme.Red);
                UiKit.Place(stem.rectTransform,Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,-15),new Vector2(3,8));
                markers[i]=new Marker{root=card.rectTransform,icon=icon,stem=stem,caption=caption};
                card.gameObject.SetActive(false);
            }
        }

        internal void Present(UiContext context)
        {
            var director=context.Threats;
            var player=context.Player;
            var eye=UnderwaterLook.Instance?UnderwaterLook.Instance.DiverCamera:null;
            int used=0;
            if(director && director.DefenseActive && player && !player.IsDead && eye && !UiInputGate.Modal)
            {
                // Rescue and stolen fish take precedence over shield bearings when the screen is busy.
                for(int priority=(int)DefenseUrgency.Rescue;priority>=(int)DefenseUrgency.Shield && used<markers.Length;priority--)
                {
                    int shown=0;
                    int maximum=priority==(int)DefenseUrgency.Shield?1:2;
                    for(int i=0;i<director.DefenseActorCount && used<markers.Length && shown<maximum;i++)
                    {
                        var actor=director.DefenseActorAt(i);
                        if((int)actor.urgency!=priority)continue;
                        var point=actor.position+Vector3.up*(actor.scale*.64f);
                        if(actor.urgency==DefenseUrgency.Rescue)
                        {
                            if(actor.victim==player.OwnerId)continue;
                            foreach(var diver in context.Players)
                                if(diver && diver.OwnerId==actor.victim){point=diver.transform.position+Vector3.up*.95f;break;}
                        }
                        float distance=Vector3.Distance(eye.transform.position,point);
                        if(distance>155 || distance<1.2f)continue;
                        Vector3 view=eye.WorldToViewportPoint(point);
                        Vector2 centre=new(.5f,.5f);
                        Vector2 direction=(Vector2)view-centre;
                        if(view.z<0)direction=-direction;
                        if(view.z<0 && direction.sqrMagnitude<.01f)direction=Vector2.down;
                        bool edge=view.z<.1f || Mathf.Abs(direction.x)>.39f || direction.y<-.30f || direction.y>.32f;
                        var at=edge?centre+direction.normalized*.49f:centre+direction;
                        at=new Vector2(Mathf.Clamp(at.x,.11f,.89f),Mathf.Clamp(at.y,.20f,.81f));
                        if(shown>0 && (at-markers[used-1].root.anchorMin).sqrMagnitude<.004f)continue;
                        Show(markers[used++],actor.urgency,at,edge?direction.normalized:Vector2.down,distance,edge);
                        shown++;
                    }
                }
            }
            for(int i=used;i<markers.Length;i++)markers[i].root.gameObject.SetActive(false);
        }

        static void Show(Marker marker,DefenseUrgency urgency,Vector2 at,Vector2 direction,float distance,bool edge)
        {
            marker.root.gameObject.SetActive(true);
            marker.root.anchorMin=marker.root.anchorMax=at;
            marker.root.anchoredPosition=edge?Vector2.zero:new Vector2(0,13);
            marker.stem.rectTransform.anchoredPosition=Vector2.Scale(direction,new Vector2(57,15));
            marker.stem.rectTransform.localRotation=Quaternion.Euler(0,0,Vector2.SignedAngle(Vector2.down,direction));
            marker.icon.sprite=DiveInstruments.Art(urgency==DefenseUrgency.Rescue?DiveInstruments.Mark.Helmet:
                urgency==DefenseUrgency.Stock?DiveInstruments.Mark.Lantern:DiveInstruments.Mark.Shield);
            UiKit.Set(marker.caption,(urgency==DefenseUrgency.Rescue?"SOS  ":"")+Mathf.RoundToInt(distance)+" м");
        }
    }
}
