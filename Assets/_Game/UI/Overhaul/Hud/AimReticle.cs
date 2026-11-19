using UnityEngine;
using UnityEngine.UI;

namespace Raid.UI
{
    // Frame-rate driven geometric marks; no hit marker or success state is fabricated.
    [DisallowMultipleComponent,DefaultExecutionOrder(300)]
    public sealed class AimReticle : MonoBehaviour
    {
        public Color ContextColor=UiTheme.Text;
        readonly RectTransform[] bars=new RectTransform[4];
        readonly RectTransform[] shadows=new RectTransform[4];
        readonly Image[] ink=new Image[5];
        IUiController app;
        CanvasGroup visibility;
        DiverMotor observed;
        Vector3 lastPosition;
        float movement;
        Camera camera;

        public void Initialize(IUiController controller)
        {
            app=controller;visibility=gameObject.AddComponent<CanvasGroup>();visibility.interactable=false;visibility.blocksRaycasts=false;
            for(int i=0;i<bars.Length;i++)
            {
                var shadow=UiKit.Panel(transform,"Reticle outline "+i,new Color(.005f,.012f,.014f,.9f));shadows[i]=shadow.rectTransform;
                var line=UiKit.Panel(transform,"Reticle arm "+i,UiTheme.Text);bars[i]=line.rectTransform;ink[i]=line;
            }
            var dotEdge=UiKit.Panel(transform,"Reticle centre outline",new Color(.005f,.012f,.014f,.92f));Place(dotEdge.rectTransform,Vector2.zero,new Vector2(4.5f,4.5f));
            var dot=UiKit.Panel(transform,"Reticle centre",UiTheme.Text);Place(dot.rectTransform,Vector2.zero,new Vector2(2.5f,2.5f));ink[4]=dot;
            SetGeometry(8,8,2);
        }

        void LateUpdate()
        {
            if(app==null||visibility==null)return;
            var context=app.Context;var player=context.Player;
            bool visible=context.Connected&&player!=null&&!UiInputGate.Modal;
            visibility.alpha=visible?1:0;if(!visible){observed=null;camera=null;movement=0;return;}
            if(observed!=player){observed=player;lastPosition=player.transform.position;camera=player.GetComponentInChildren<Camera>();movement=0;}
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            float distance=Vector3.Distance(lastPosition,player.transform.position);lastPosition=player.transform.position;
            float speed=dt>.0001f&&distance<.75f?distance/dt:0;
            movement=Mathf.Lerp(movement,Mathf.Clamp01(speed/4),1-Mathf.Exp(-dt*12));
            float aim=Nereid.NereidAim.Blend;
            SetGeometry(Mathf.Lerp(8+movement*3,3,aim),Mathf.Lerp(8,6,aim),Mathf.Lerp(2,1.5f,aim));
            var color=ContextColor;
            if(camera!=null&&context.Harpoon!=null&&context.Harpoon.tuning!=null)
            {
                RaycastHit hit;
                if(Physics.Raycast(camera.transform.position,camera.transform.forward,out hit,context.Harpoon.tuning.maxRange,context.Harpoon.tuning.hitMask,QueryTriggerInteraction.Ignore))
                {
                    var diver=hit.collider.GetComponentInParent<DiverMotor>();if(diver!=null&&diver!=player)color=UiTheme.Green;
                }
            }
            foreach(var mark in ink)if(mark.color!=color)mark.color=color;
        }

        void SetGeometry(float gap,float length,float thickness)
        {
            for(int i=0;i<4;i++)
            {
                bool horizontal=i<2;float side=i%2==0?-1:1;
                Vector2 position=horizontal?new Vector2(side*(gap+length*.5f),0):new Vector2(0,side*(gap+length*.5f));
                Vector2 size=horizontal?new Vector2(length,thickness):new Vector2(thickness,length);
                Place(shadows[i],position,size+Vector2.one*2);Place(bars[i],position,size);
            }
        }

        static void Place(RectTransform item,Vector2 position,Vector2 size)
        {UiKit.Place(item,new Vector2(.5f,.5f),new Vector2(.5f,.5f),position,size);}
    }
}
