# 2 — 유닛 드래그·arm 의 ESC/뒤로가기 하드 취소

## 목적

손가락을 어디로 옮기든 상관없이 **한 번에** 취소할 수단을 준다. 드림캐쳐 손패에는 이미 있는 규칙
(`DreamcatcherHandView.Update` 의 ESC → `CancelAllCardInteraction`)을 유닛 배치에도 맞춘다.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` — `Update` 에 ESC 분기

## 구현

```
// Update() 최상단(하이라이트 파생 다음, 보드 제스처 앞)
var kb = Keyboard.current;
if (kb != null && kb.escapeKey.wasPressedThisFrame)
{
    if (_session.active && !_simulatedDrag) { CleanupSession(); ...cancel SFX; }
    else if (_armedUnit != null) Disarm();
}
```

우선순위는 **드래그 > arm** 이다. 둘이 동시에 성립하지 않지만(BeginDrag 가 Disarm 을 먼저 부른다)
분기 순서를 못 박아 두면 나중에 순서가 바뀌어도 "가장 최근 상호작용을 먼저 되돌린다"가 유지된다.

`_simulatedDrag` 제외 = 계약 5(탭 배치 비행은 확정된 배치의 연출이다). 비행 중 ESC 는 아무 일도
하지 않는다 — 비행을 끊으면 이미 지불된 코스트로 유닛이 사라진다.

포인터를 누른 채로 ESC 를 눌러도 안전하다: 나중에 도착하는 `OnEndDrag → EndDrag` 는
`if (!_session.active) return;` 으로 물러난다.

### Android 뒤로가기

Unity Android 백엔드는 하드웨어 back 을 `Keyboard.escapeKey` 로 보고한다 — 별도 분기를 두지 않는다.
(에디터/데스크톱의 ESC 와 같은 코드 경로.)

### 전투 메뉴와의 관계

`MenuPopup` 은 ESC 를 구독하지 않는다(버튼 전용). 그래서 ESC 소비자는 드림캐쳐 손패와 여기 둘뿐이고,
둘은 상호배타(손패가 열리면 트레이는 숨는다)라 경합이 없다.

## 완료 기준

- [x] 컴파일 통과, CS 에러 0
- [x] EditMode 전량 통과(신규 실패 0) · PlayMode 신규 실패 0
- [ ] Play(에디터) — 유닛 드래그 중 ESC → 프리뷰가 사라지고 코스트가 줄지 않는다
- [ ] Play(에디터) — 슬롯 탭으로 arm 한 뒤 ESC → arm 해제(하이라이트/스카우트 소거)
- [ ] Play(에디터) — 탭 배치 비행 중 ESC → 배치가 정상 완료된다(끊기지 않는다)
- [ ] 실기기 — Android 뒤로가기로 같은 취소가 동작한다 (미확인 · handoff Follow-up)
