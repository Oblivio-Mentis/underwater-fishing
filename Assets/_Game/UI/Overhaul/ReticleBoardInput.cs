using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Raid.UI
{
    // A ship screen has one pointer, fixed to the owner's sightline. Desktop menus use the Input System module.
    internal sealed class ReticleBoardInput
    {
        readonly GraphicRaycaster raycaster;
        readonly PointerEventData pointer;
        readonly List<RaycastResult> hits = new List<RaycastResult>(16);
        GameObject hovered, pressed, dragged;
        Vector2 lastPoint;
        public string ControlName => hovered == null ? "" : hovered.name;

        public ReticleBoardInput(GraphicRaycaster raycaster, EventSystem events)
        {
            this.raycaster = raycaster;
            pointer = new PointerEventData(events) { button = PointerEventData.InputButton.Left, pointerId = -1 };
        }

        public bool Update(Vector2 center, bool down, bool up, float wheel, bool skillPurchase = false)
        {
            pointer.position = center;
            hits.Clear();
            raycaster.Raycast(pointer, hits);
            pointer.pointerCurrentRaycast = hits.Count == 0 ? default : hits[0];
            GameObject hit = pointer.pointerCurrentRaycast.gameObject;
            GameObject next = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hit);
            bool purchased=false;
            if(skillPurchase && hit!=null && hit.GetComponentInParent<SkillPurchaseButton>() is SkillPurchaseButton node
                && node.IsActive() && node.IsInteractable())
            {
                node.Purchase?.Invoke();
                purchased=true;
            }
            if (next != hovered)
            {
                if (hovered != null) ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
                hovered = next;
                pointer.pointerEnter = next;
                if (next != null) ExecuteEvents.Execute(next, pointer, ExecuteEvents.pointerEnterHandler);
            }
            if (wheel != 0 && hit != null)
            {
                pointer.scrollDelta = new Vector2(0, wheel);
                ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.scrollHandler);
            }
            if (down && hit != null)
            {
                pointer.pressPosition = center;
                pointer.pointerPressRaycast = pointer.pointerCurrentRaycast;
                pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
                if (pressed == null) pressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                pointer.pointerPress = pressed;
                dragged = ExecuteEvents.GetEventHandler<IDragHandler>(hit);
                pointer.pointerDrag = dragged;
                lastPoint = raycaster.transform.InverseTransformPoint(pointer.pointerCurrentRaycast.worldPosition);
                if (dragged != null) ExecuteEvents.Execute(dragged, pointer, ExecuteEvents.initializePotentialDrag);
            }
            bool dragScrollbar=dragged!=null && hovered==dragged && dragged.GetComponent<Scrollbar>()!=null;
            bool dragGraph=dragged!=null && pressed==dragged && hit!=null && dragged.GetComponent<SkillGraphPan>()!=null &&
                hit.transform.IsChildOf(dragged.transform);
            if (!down && pressed!=null && (dragScrollbar || dragGraph))
            {
                Vector2 point = raycaster.transform.InverseTransformPoint(pointer.pointerCurrentRaycast.worldPosition);
                Vector2 delta=point-lastPoint;
                if(dragScrollbar || pointer.dragging || delta.sqrMagnitude>=4)
                {
                    if (!pointer.dragging)
                    {
                        pointer.dragging = true;
                        ExecuteEvents.Execute(dragged, pointer, ExecuteEvents.beginDragHandler);
                    }
                    if(delta.sqrMagnitude>.0001f)
                    {
                        pointer.delta=delta;
                        ExecuteEvents.Execute(dragged, pointer, ExecuteEvents.dragHandler);
                        lastPoint=point;
                    }
                }
            }
            bool clicked = false;
            if (up)
            {
                var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
                var click = pressed != null && pressed == target && !pointer.dragging;
                var prior = pressed;
                Release();
                if (click)
                {
                    var control = prior.GetComponent<Selectable>();
                    if (control == null || control.IsInteractable())
                        clicked = ExecuteEvents.Execute(prior, pointer, ExecuteEvents.pointerClickHandler);
                }
            }
            return clicked || purchased;
        }

        public void Clear()
        {
            if (hovered != null) ExecuteEvents.Execute(hovered, pointer, ExecuteEvents.pointerExitHandler);
            hovered = null;
            pointer.pointerEnter = null;
            Release();
        }

        void Release()
        {
            if (pressed != null) ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            if (dragged != null && pointer.dragging) ExecuteEvents.Execute(dragged, pointer, ExecuteEvents.endDragHandler);
            dragged = null;
            pressed = null;
            pointer.pointerPress = null;
            pointer.pointerDrag = null;
            pointer.dragging = false;
        }
    }
}
