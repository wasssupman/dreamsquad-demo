# 7 — 선택 중 범위 좁히기 (아이콘 버튼 숨김 + Active 카드 차단)

> 추가 2026-07-29 (사용자 결정 2건). units 0~4 이후의 정책 조정 — 선택 상태를 **부착 전용**으로 좁힌다.

## 목적

선택 중 화면에 나오는 것과 할 수 있는 것을 줄인다.

1. **유닛 주변 아이콘 버튼 UI(이동 + 더미 2개)를 노출하지 않는다.** 나중에 활용할 자리라
   코드는 남기고 표시만 끈다.
2. **Active 카드는 선택 중 사용 불가.** 선택 중 손패의 역할은 "이 유닛에 붙이기" 하나다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` — 플립북 표시 토글
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — Active 차단(드래그·탭)

## 구현

### A. 아이콘 버튼 숨김

`[SerializeField] private bool showActionFlipbook = false;` 를 두고 `Select` 의 `Show(...)` 만
게이트한다. **`Hide()` 경로는 손대지 않는다** — Play 중 토글을 켜도 잔상 없이 정상 복귀한다.
`DcActionFlipbookView`·`OnMovePressed`·`DefenderRelocationController` 는 그대로 남는다.

⚠ **재배치가 도달 불가로 잠든다.** 이동 버튼은 재배치의 **유일한 진입 경로**다(홀드 진입은
defender-relocation unit 5 에서 은퇴). 토글을 켜면 즉시 되살아난다 — 기능을 지운 것이 아니다.

### B. Active 카드 차단

차단 지점은 사용 경로 2개뿐이다(둘 다 `SelectionTarget != Entity.Null` 로 판정):

- **드래그**: `OnBeginDrag` 에서 `card.type == Active` 면 `_mode` 를 `None` 으로 되돌리고 즉시
  반환 — `_dragging` 을 세우지 않으므로 조준(`IsAiming`)·`_activeAiming` 진입 자체가 없고
  `EndInteraction` 을 지나지 않는다(정리할 상태가 없다).
- **탭 즉발**: `OnPointerClick` 의 기존 "부착 카드만" 가드에서 Active 를 이 사유로 분기.

**포탈 2탭은 별도 가드가 필요 없다** — 2탭 대기는 드래그가 선행해야 성립하고, 반대로 조준
중에는 선택이 시작될 수 없다(`TapGated`/캐처 press-스냅샷이 `IsAiming` 을 막는다). 즉 "선택 +
Active 조준" 동시 상태는 도달 불가다.

**피드백**: 움찔 + 기존 브리핑 표면. 헤더에 조작법(`끌어서 시전`)을 그대로 쓰면 "사용 불가" 와
모순되므로 헤더는 **해제 방법**("빈 곳을 탭하면 해제")을 안내한다. `usable`(게이지) 의미를
확장해 dim 처리하지는 않았다 — 그러면 "각성치 부족" 문안이 거짓이 된다.

## 완료 기준

- [x] compile 클린 (2026-07-29 — dotnet build 0/0, Unity 콘솔 0)
- [ ] Play: 유닛 선택 → 주변 아이콘 버튼이 **나오지 않는다**(패널·리티클·줌·손패는 정상)
- [ ] Play: 선택 중 Active 카드를 **끌면** 움찔 + "선택 중 사용 불가", 조준 오버레이·dim 이 뜨지
      않고 차감 0
- [ ] Play: 선택 중 Active 카드를 **탭하면** 같은 피드백
- [ ] Play: 선택을 해제한 뒤 같은 Active 카드가 **정상 사용**된다(타일/포탈 모두)
- [ ] Play: 선택 중 Unit/Squad 카드 부착(탭·드래그)은 그대로 동작
- [ ] `showActionFlipbook` 을 켜면 이동 버튼과 재배치가 즉시 복귀한다(회귀 확인)
