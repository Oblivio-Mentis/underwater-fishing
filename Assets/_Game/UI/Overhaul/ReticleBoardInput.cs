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

        public bool Update(Vector2 center, bool down, bool up, float wheel)
        {
            pointer.position = center;
            hits.Clear();
            raycaster.Raycast(pointer, hits);
            pointer.pointerCurrentRaycast = hits.Count == 0 ? default : hits[0];
            GameObject hit = pointer.pointerCurrentRaycast.gameObject;
            GameObject next = ExecuteEvents.GetEventHandler<IPointerEnterHandler>(hit);
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
            if (dragged != null && !down && pressed != null && hovered == dragged && dragged.GetComponent<Scrollbar>() != null)
            {
                Vector2 point = raycaster.transform.InverseTransformPoint(pointer.pointerCurrentRaycast.worldPosition);
                if (!pointer.dragging)
                {
                    pointer.dragging = true;
                    ExecuteEvents.Execute(dragged, pointer, ExecuteEvents.beginDragHandler);
                }
                pointer.delta = point - lastPoint;
                ExecuteEvents.Execute(dragged, pointer, ExecuteEvents.dragHandler);
                lastPoint = point;
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
            return clicked;
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
