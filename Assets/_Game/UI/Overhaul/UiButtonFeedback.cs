using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Raid.UI
{
    public sealed class UiButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        public bool primary;
        Button button;TMP_Text caption;Outline border;
        bool hovered,selected,known,lastInteractable;
        void Awake(){button=GetComponent<Button>();caption=GetComponentInChildren<TMP_Text>();border=GetComponent<Outline>();}
        void LateUpdate()
        {
            bool available=button.IsInteractable();
            if(!known || available!=lastInteractable)
            {known=true;lastInteractable=available;caption.color=!available?UiTheme.Text:primary?UiTheme.Steel:UiTheme.Text;RefreshBorder();}
        }
        void RefreshBorder(){if(border!=null)border.effectColor=button!=null&&button.IsInteractable()&&(hovered||selected)?UiTheme.Amber:UiTheme.Edge;}
        public void OnPointerEnter(PointerEventData e){hovered=true;RefreshBorder();}
        public void OnPointerExit(PointerEventData e){hovered=false;RefreshBorder();}
        public void OnSelect(BaseEventData e){selected=true;RefreshBorder();}
        public void OnDeselect(BaseEventData e){selected=false;RefreshBorder();}
    }
}
