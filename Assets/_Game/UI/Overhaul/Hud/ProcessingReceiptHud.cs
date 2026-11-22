using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Raid.UI
{
    internal sealed class ProcessingReceiptHud
    {
        const float HoldSeconds=5f;
        readonly GameObject canvas;
        readonly RectTransform panel;
        readonly CanvasGroup fade;
        readonly TMP_Text title;
        readonly RectTransform[] rows=new RectTransform[4];
        readonly TMP_Text[] counts=new TMP_Text[4], additions=new TMP_Text[4];
        RaidWorld world;
        MaterialCost previous, current;
        bool salvage;
        float displayedAt, updatedAt;

        internal bool Visible => panel.gameObject.activeInHierarchy && fade.alpha>.1f;

        internal ProcessingReceiptHud(Scene scene)
        {
            canvas=new GameObject("Team wallet receipt",typeof(Canvas),typeof(CanvasScaler));
            if(scene.IsValid())SceneManager.MoveGameObjectToScene(canvas,scene);
            var layer=canvas.GetComponent<Canvas>();
            layer.renderMode=RenderMode.ScreenSpaceOverlay;
            layer.sortingOrder=120;
            var scaler=canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            var frame=UiKit.Panel(canvas.transform,"Deposit card",new Color(.035f,.082f,.10f,.96f));
            UiKit.Tile(frame);
            panel=frame.rectTransform;
            UiKit.Place(panel,Vector2.one,Vector2.one,new Vector2(-42,-125),new Vector2(386,228));
            fade=frame.gameObject.AddComponent<CanvasGroup>();
            fade.interactable=false;
            fade.blocksRaycasts=false;
            var content=UiKit.Column(panel,"Team wallet credit",6);
            UiKit.Fill(content,15);
            var accent=UiKit.Panel(content,"Receipt accent",UiTheme.Green);
            UiKit.Size(accent,2,flexibleWidth:1);
            title=UiKit.Label(content,"Source","",21,UiTheme.Cyan);
            UiKit.Size(title,32);
            title.fontStyle=FontStyles.Bold;
            for(int i=0;i<rows.Length;i++)
            {
                var row=rows[i]=UiKit.Row(content,MaterialStrip.Label(i),5);
                UiKit.Size(row,31);
                UiKit.Size(UiKit.Symbol(row,"Resource icon",MaterialStrip.Icon(i),Color.white),30,33);
                UiKit.Size(UiKit.Label(row,"Resource",MaterialStrip.Label(i),18,MaterialStrip.Tint(i)),30,131);
                counts[i]=UiKit.Label(row,"Previous and current balance","",19);
                UiKit.Size(counts[i],30,flexibleWidth:1);
                additions[i]=UiKit.Label(row,"Credit amount","",19,UiTheme.Green);
                UiKit.Size(additions[i],30,62);
                additions[i].alignment=TextAlignmentOptions.MidlineRight;
                additions[i].fontStyle=FontStyles.Bold;
            }
            panel.gameObject.SetActive(false);
        }

        internal void Update(RaidWorld source,bool connected)
        {
            if(world!=source)
            {
                if(world!=null)world.ProcessingFinished-=Credit;
                world=source;
                if(world!=null)world.ProcessingFinished+=Credit;
                panel.gameObject.SetActive(false);
            }
            if(!panel.gameObject.activeSelf)return;
            float now=Time.unscaledTime;
            float sinceUpdate=now-updatedAt;
            if(!connected || sinceUpdate>=HoldSeconds)
            {
                panel.gameObject.SetActive(false);
                return;
            }

            float appear=Mathf.SmoothStep(0,1,(now-displayedAt)/.28f);
            float advance=Mathf.SmoothStep(0,1,sinceUpdate/.55f);
            fade.alpha=appear*Mathf.Clamp01((HoldSeconds-sinceUpdate)/.52f);
            panel.anchoredPosition=new Vector2(-42+36*(1-appear),-125);
            for(int i=0;i<rows.Length;i++)
            {
                if(!rows[i].gameObject.activeSelf)continue;
                int from=MaterialStrip.Amount(previous,i);
                int to=MaterialStrip.Amount(current,i);
                int value=(int)System.Math.Round(from+(to-from)*(double)advance);
                UiKit.Set(counts[i],from+"  →  "+value);
            }
        }

        void Credit(MaterialCost yield,MaterialCost before,MaterialCost after,bool fromSalvage)
        {
            if(yield.IsEmpty)return;
            float now=Time.unscaledTime;
            bool merge=panel.gameObject.activeSelf && fromSalvage==salvage && before.Equals(current) && now-updatedAt<HoldSeconds-.52f;
            if(!merge)
            {
                displayedAt=now;
                previous=before;
            }
            current=after;
            updatedAt=now;
            salvage=fromSalvage;
            UiKit.Set(title,fromSalvage?"ПОДНЯТЫЙ ГРУЗ · КАЗНА":"ПЕРЕРАБОТКА · КАЗНА");
            int visible=0;
            for(int i=0;i<rows.Length;i++)
            {
                int delta=MaterialStrip.Amount(current,i)-MaterialStrip.Amount(previous,i);
                rows[i].gameObject.SetActive(delta>0);
                if(delta<=0)continue;
                UiKit.Set(additions[i],"+"+delta);
                visible++;
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,79+visible*37);
            panel.gameObject.SetActive(visible>0);
        }

        internal void Dispose()
        {
            if(world!=null)world.ProcessingFinished-=Credit;
            if(canvas!=null)Object.Destroy(canvas);
        }
    }
}
