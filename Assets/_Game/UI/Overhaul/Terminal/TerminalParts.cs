using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    internal static class TerminalParts
    {
        public static RectTransform Shell(Transform parent,string name,string title,string kicker,IUiController app,out RectTransform root,float width=1180,float height=810)
        {
            var surface=UiKit.Frame(parent,name,width,height);root=(RectTransform)surface.parent;
            var column=UiKit.Column(surface,"Layout",12);UiKit.Fill(column,24);
            var heading=UiKit.Row(column,"Heading",20);UiKit.Size(heading,62);
            var titles=UiKit.Column(heading,"Titles",0);UiKit.Size(titles,62,flexibleWidth:1);
            var eyebrow=UiKit.Label(titles,"Kicker",kicker,17,UiTheme.Cyan);UiKit.Size(eyebrow,22);
            var label=UiKit.Label(titles,"Title",title,30);UiKit.Size(label,40);
            var close=UiKit.Button(heading,"Close",UiText.CloseTerminal,app.ClosePanel,false,52);UiKit.Size(close,52,116);close.GetComponent<LayoutElement>().flexibleWidth=0;
            return column;
        }
        public static RectTransform Card(Transform parent,string name,float height)
        {
            var panel=UiKit.Panel(parent,name,UiTheme.Panel);UiKit.Size(panel,height,flexibleWidth:1);
            var body=UiKit.Column(panel.transform,"CardLayout",6);UiKit.Fill(body,16);return body;
        }
        public static TMP_Text Text(Transform parent,string name,string value,float height=30,float size=21,Color? color=null)
        {
            var label=UiKit.Label(parent,name,value,size,color);UiKit.Size(label,height);return label;
        }
        public static ScrollRect Body(Transform parent,out RectTransform content)
        {
            var scroll=UiKit.Scroll(parent,"BodyScroll",out content);var element=UiKit.Size(scroll,0);element.flexibleHeight=1;return scroll;
        }
        public static void ButtonReason(Button button,TMP_Text label,string reason,string ready)
        {
            button.interactable=string.IsNullOrEmpty(reason);UiKit.Set(label,string.IsNullOrEmpty(reason)?ready:reason);label.color=string.IsNullOrEmpty(reason)?UiTheme.Green:UiTheme.Amber;
            button.GetComponentInChildren<TMP_Text>().color=button.interactable?UiTheme.Steel:UiTheme.Text;
        }
        public static string BaseReason(IUiController app,bool cargo=false)
        {
            var ctx=app.Context;if(!ctx.Connected || ctx.View==null)return UiText.NoConfirmedState;
            if(ctx.Player.IsHelpless)return UiText.NeedRescue;
            if(cargo?!ctx.NearCargo:!ctx.NearTerminal)return cargo?UiText.NeedCargo:UiText.NeedTerminal;
            if(ctx.World.TravelRemaining>0)return UiText.TravelWait;
            if(app.CommandPending)return UiText.WaitCommand;
            return "";
        }
        public static bool Execute(IUiController app,Func<string> reason,WorldAction action,long id=0)
        {
            string unavailable=reason();if(!string.IsNullOrEmpty(unavailable)){app.Toast(unavailable,true);return false;}
            app.Send(action,id);return true;
        }
    }
}
