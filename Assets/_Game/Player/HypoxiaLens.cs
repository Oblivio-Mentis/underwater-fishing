using UnityEngine;
using UnityEngine.UI;

namespace Raid
{
    // The mask belongs to the viewing diver. Navigation and button labels remain legible above it.
    public sealed class HypoxiaLens : MonoBehaviour
    {
        DiverMotor diver;
        RawImage mask;
        Texture2D texture;
        float severity;
        public float Intensity => severity;

        public void Bind(DiverMotor owner) { diver=owner; }

        void LateUpdate()
        {
            if(diver==null)return;
            float target=diver.IsDead?0:diver.HypoxiaSeverity;
            severity=Mathf.MoveTowards(severity,target,Time.unscaledDeltaTime*(target>severity?.9f:1.6f));
            if(severity<=0)
            {
                if(mask!=null)mask.enabled=false;
                return;
            }
            if(mask==null)CreateMask();
            mask.enabled=true;
            float breath=.5f+.5f*Mathf.Cos(Time.time*(2.2f+severity));
            mask.color=new Color(.012f,.017f,.031f,Mathf.Min(.92f,.2f+.68f*severity+.055f*breath));
            float span=.8f+.5f*severity;
            mask.uvRect=new Rect((1-span)*.5f,(1-span)*.5f,span,span);
        }

        void CreateMask()
        {
            var go=new GameObject("Peripheral hypoxia",typeof(Canvas));go.transform.SetParent(transform,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=-35;
            mask=new GameObject("Peripheral vision",typeof(RectTransform),typeof(RawImage)).GetComponent<RawImage>();
            mask.transform.SetParent(go.transform,false);
            var rect=mask.rectTransform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            mask.raycastTarget=false;
            const int size=128;
            texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true){name="Soft diver peripheral mask",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=(2f*x+1)/size-1,dy=(2f*y+1)/size-1;
                float radius=Mathf.Sqrt(dx*dx+dy*dy);
                float opacity=.13f+.87f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.13f,.91f,radius));
                pixels[y*size+x]=new Color32(255,255,255,(byte)(opacity*255));
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);mask.texture=texture;
        }
        void OnDestroy(){if(texture!=null)Destroy(texture);}
    }
}
