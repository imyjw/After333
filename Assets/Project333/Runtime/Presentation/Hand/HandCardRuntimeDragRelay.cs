using System;
using UnityEngine.EventSystems;

namespace Project333.Runtime.Presentation.Hand
{
    public sealed class HandCardRuntimeDragRelay : UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public Action<PointerEventData> BeginDrag;

        public Action<PointerEventData> Drag;

        public Action<PointerEventData> EndDrag;

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeginDrag?.Invoke(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Drag?.Invoke(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndDrag?.Invoke(eventData);
        }
    }
}
