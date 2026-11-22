using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mark=Raid.UI.DiveInstruments.Mark;

namespace Raid.UI
{
    // Shared icons and amounts for the team balance, costs and processing forecasts.
    internal sealed class MaterialStrip
    {
        static readonly Mark[] Icons={Mark.Biomass,Mark.Chitin,Mark.Scrap,Mark.Electronics};
        static readonly string[] Labels={UiText.BiomassLabel,UiText.ChitinLabel,UiText.ScrapLabel,UiText.ElectronicsLabel};
        static readonly Color[] Tints={UiTheme.Hex("B4DFB0"),UiTheme.Hex("E4C5A3"),UiTheme.Hex("C3D0DE"),UiTheme.Hex("97D9DA")};

        readonly TMP_Text[] amounts=new TMP_Text[4];
        readonly Image[] surfaces=new Image[4];
        readonly bool ledger;
        internal readonly RectTransform Root;

        internal MaterialStrip(Transform parent,string name,bool ledger=false)
        {
            this.ledger=ledger;
            float height=ledger?68:33;
            Root=UiKit.Row(parent,name,ledger?10:7);
            UiKit.Size(Root,height);
            for(int i=0;i<Icons.Length;i++)
            {
                var card=surfaces[i]=UiKit.Panel(Root,"Material "+Icons[i],TerminalParts.MonitorCard);
                UiKit.Tile(card);
                UiKit.Size(card,height,ledger?145:135,ledger?1:0);
                var rail=UiKit.Panel(card.transform,"Resource accent",Tints[i]);
                var rr=rail.rectTransform;rr.anchorMin=new Vector2(0,0);rr.anchorMax=new Vector2(0,1);
                rr.offsetMin=new Vector2(1,4);rr.offsetMax=new Vector2(4,-4);
                var emblem=UiKit.Symbol(card.transform,"Resource icon",Icons[i],Color.white);
                UiKit.Place(emblem.rectTransform,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(ledger?14:6,0),
                    Vector2.one*(ledger?49:26));
                if(ledger)
                {
                    var label=UiKit.Label(card.transform,"Resource name",Labels[i].ToUpperInvariant(),16,Tints[i]);
                    Stretch(label.rectTransform,71,12,39,60);
                    label.textWrappingMode=TextWrappingModes.NoWrap;
                    amounts[i]=UiKit.Label(card.transform,"Balance","—",28);
                    amounts[i].fontStyle=FontStyles.Bold;
                    Stretch(amounts[i].rectTransform,71,12,5,42);
                }
                else
                {
                    amounts[i]=UiKit.Label(card.transform,"Amount","",17,Tints[i]);
                    Stretch(amounts[i].rectTransform,32,5,1,32);
                    amounts[i].textWrappingMode=TextWrappingModes.NoWrap;
                    amounts[i].enableAutoSizing=true;
                    amounts[i].fontSizeMin=13;
                    amounts[i].fontSizeMax=17;
                }
            }
        }

        static void Stretch(RectTransform rect,float left,float right,float bottom,float top)
        {
            rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);
            rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,top);
        }

        internal static MaterialCost Yield(CatchItem item) => new(item.biomass,item.chitin,item.scrap,item.electronics);

        internal void Quote(MaterialCost quote,bool hundredths=false,bool showZero=false)
        {
            for(int i=0;i<4;i++)
            {
                int value=Amount(quote,i);
                surfaces[i].gameObject.SetActive(showZero || value>0);
                if(showZero || value>0)UiKit.Set(amounts[i],(i==2?"Лом":Labels[i])+" "+(hundredths?TeamProgress.FoodText(value):value.ToString()));
            }
        }

        internal void Balances(TeamProgress view)
        {
            if(!ledger)return;
            for(int i=0;i<Icons.Length;i++)
                UiKit.Set(amounts[i],view==null?"—":Amount(view.Treasury,i).ToString());
        }

        internal static Mark Icon(int index) => Icons[index];
        internal static Color Tint(int index) => Tints[index];
        internal static string Label(int index) => Labels[index];
        internal static int Amount(MaterialCost cost,int index) => index switch
        {
            0=>cost.biomass,1=>cost.chitin,2=>cost.scrap,_=>cost.electronics
        };
    }
}
