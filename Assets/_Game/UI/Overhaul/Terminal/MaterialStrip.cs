using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mark=Raid.UI.DiveInstruments.Mark;

namespace Raid.UI
{
    // Shared visual language for the balance, confirmed gains, processing yields and quotes.
    internal sealed class MaterialStrip
    {
        static readonly Mark[] Icons={Mark.Biomass,Mark.Chitin,Mark.Scrap,Mark.Electronics};
        static readonly string[] Labels={UiText.BiomassLabel,UiText.ChitinLabel,UiText.ScrapLabel,UiText.ElectronicsLabel};
        static readonly Color[] Tints={UiTheme.Hex("B4DFB0"),UiTheme.Hex("E4C5A3"),UiTheme.Hex("C3D0DE"),UiTheme.Hex("97D9DA")};

        readonly TMP_Text[] amounts=new TMP_Text[4];
        readonly TMP_Text[] deltas=new TMP_Text[4];
        readonly Image[] surfaces=new Image[4];
        readonly int[] totals=new int[4], starts=new int[4];
        readonly float[] creditedAt=new float[4];
        readonly bool ledger;
        bool seenBalances;
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
                    Stretch(amounts[i].rectTransform,71,67,5,42);
                    deltas[i]=UiKit.Label(card.transform,"Confirmed addition","",18,UiTheme.Green);
                    var delta=deltas[i];delta.alignment=TextAlignmentOptions.MidlineRight;
                    UiKit.Place(delta.rectTransform,new Vector2(1,.5f),new Vector2(1,.5f),new Vector2(-12,-10),new Vector2(59,29));
                }
                else
                {
                    amounts[i]=UiKit.Label(card.transform,"Amount","",17,Tints[i]);
                    Stretch(amounts[i].rectTransform,32,5,1,32);
                    amounts[i].textWrappingMode=TextWrappingModes.NoWrap;
                }
            }
        }

        static void Stretch(RectTransform rect,float left,float right,float bottom,float top)
        {
            rect.anchorMin=new Vector2(0,0);rect.anchorMax=new Vector2(1,0);
            rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,top);
        }

        internal static MaterialCost Yield(CatchItem item) => new(item.biomass,item.chitin,item.scrap,item.electronics);
        internal static readonly MaterialCost Empty=default;

        internal void Quote(MaterialCost quote)
        {
            for(int i=0;i<4;i++)
            {
                int value=Amount(quote,i);
                surfaces[i].gameObject.SetActive(value>0);
                if(value>0)UiKit.Set(amounts[i],Labels[i]+" "+value);
            }
        }

        internal void Balances(TeamProgress view)
        {
            if(!ledger)return;
            if(view==null)
            {
                seenBalances=false;
                foreach(var amount in amounts)UiKit.Set(amount,"—");
                foreach(var delta in deltas)UiKit.Set(delta,"");
                return;
            }

            var balances=new MaterialCost(view.biomass,view.chitin,view.scrap,view.electronics);
            float now=Time.unscaledTime;
            for(int i=0;i<4;i++)
            {
                int value=Amount(balances,i);
                float t=seenBalances?now-creditedAt[i]:3;
                if(seenBalances && value>totals[i])
                {
                    if(t>=1.1f)starts[i]=totals[i];
                    creditedAt[i]=now;
                    t=0;
                }
                else if(!seenBalances || value<totals[i])
                {
                    starts[i]=value;
                    creditedAt[i]=now-3;
                    t=3;
                }
                totals[i]=value;
                int difference=value-starts[i];
                bool receipt=difference>0 && t<2.4f;
                int displayed=receipt?(int)Math.Round(starts[i]+difference*(double)Mathf.SmoothStep(0,1,t/1.1f)):value;
                UiKit.Set(amounts[i],displayed.ToString());
                UiKit.Set(deltas[i],receipt?"+"+difference:"");
                var tint=UiTheme.Green;tint.a=Mathf.Clamp01((2.4f-t)/.28f);deltas[i].color=tint;
                surfaces[i].color=Color.Lerp(TerminalParts.MonitorCard,new Color(.16f,.27f,.23f),receipt?Mathf.Clamp01(1-t/1.6f):0);
            }
            seenBalances=true;
        }

        static int Amount(MaterialCost cost,int index) => index switch
        {
            0=>cost.biomass,1=>cost.chitin,2=>cost.scrap,_=>cost.electronics
        };
    }
}
