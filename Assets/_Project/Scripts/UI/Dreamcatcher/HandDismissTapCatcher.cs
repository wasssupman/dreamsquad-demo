using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wassup.UI
{
    // selection-hand-attach unit 2 — 손패 오픈 중 보드 탭 수신기(전화면 투명 Image 에 부착).
    // 수명·배치·활성 토글은 DreamcatcherHandView 가 소유하고, 여기서는 "이 탭이 보드 탭인가"만
    // 판정해 콜백으로 넘긴다.
    //
    // UGUI Button 을 쓰지 않는다 — 두 가지가 부족하다:
    //  (1) `onClick` 은 **release 프레임**에 발화하고 좌표를 주지 않는다. 포탈 출구 탭은
    //      press 프레임에 커밋되면서 IsAiming/IsPortalAiming 을 그 프레임에 내리므로, release
    //      시점 상태로 판정하면 그 릴리즈가 "보드 탭" 으로 통과해 선택을 전환/해제한다.
    //      → press 프레임 스냅샷(_pressBlocked)으로 막는다.
    //  (2) Button 은 IDragHandler 가 없어 **이동량과 무관하게** 릴리즈가 자기 위면 클릭이다.
    //      전화면 캐처에서는 보드를 크게 쓸어도 탭으로 읽힌다.
    //      → pressPosition→position 거리를 임계와 비교해 스와이프를 걸러낸다.
    //
    // 좌표는 `eventData` 를 쓴다(`Pointer.current` 금지 — 릴리즈 프레임의 전역 포인터는
    // 이 클릭의 press 지점을 모른다).
    public class HandDismissTapCatcher : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
    {
        private Func<bool> _pressBlocked;
        private Action<Vector2> _onTap;
        private float _moveThreshold = 24f;
        private bool _blocked;

        public void Init(Func<bool> pressBlocked, Action<Vector2> onTap, float moveThreshold)
        {
            _pressBlocked = pressBlocked;
            _onTap = onTap;
            _moveThreshold = moveThreshold;
        }

        // press 시점 스냅샷 — 이 프레임에 카드 인터랙션/조준이 살아 있었으면 뒤따르는 클릭은
        // 그 제스처의 릴리즈이지 보드 탭이 아니다.
        public void OnPointerDown(PointerEventData eventData)
        {
            _blocked = _pressBlocked != null && _pressBlocked();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_blocked) return;
            if (Vector2.Distance(eventData.pressPosition, eventData.position) > Mathf.Max(1f, _moveThreshold))
                return; // 스와이프 — 탭이 아니다
            _onTap?.Invoke(eventData.position);
        }

        // 캐처가 꺼질 때 스냅샷을 남기지 않는다(다음 활성화의 첫 클릭이 stale 로 막히는 것 방지).
        private void OnDisable() => _blocked = false;
    }
}
