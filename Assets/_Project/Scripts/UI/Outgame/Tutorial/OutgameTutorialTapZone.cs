using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wassup.UI
{
    // outgame-tutorial unit 1 — press-based tap reporter for the dim pieces.
    // Deliberately not a Button: IPointerClickHandler cancels the click once the
    // pointer passes the drag threshold, and the lobby is the screen that teaches
    // keyring swiping, so a swipe over the overlay would silently eat the tap.
    public sealed class OutgameTutorialTapZone : MonoBehaviour, IPointerDownHandler
    {
        public Action Pressed;

        public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke();
    }
}
