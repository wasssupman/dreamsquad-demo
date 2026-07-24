# 1 — 홀드 제스처 & 이동모드

## 목적

Battle 중 보드의 배치 유닛을 1초 홀드하면 이동모드에 진입한다: 유닛 하이라이트 + 슬로모 + 카메라 포커스.
취소·남용 방지(진입 쿨다운·타임아웃)까지 이 unit 의 책임. 배치 자체(탭/드래그)는 unit 2.

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (신규 — 기존 드래그 컨트롤러 비대 방지)
- `Assets/_Project/Scripts/Data/RelocationSettings.cs` + SO 에셋 (신규 — 노브 응집)
- 씬 배선: 컨트롤러 GameObject + SerializeField (bridge / mainCamera / dragController / settings) —
  `unity-feature-wiring` 스킬 절차 준수

## 구현

1. **`RelocationSettings` SO**: `holdSeconds`(1) · `entryCooldownSeconds` · `moveModeTimeoutSeconds` ·
   `redeploySeconds`(unit 3 소비) · `highlightColor`. 하드코딩 금지(제약 6).
2. **press 시작 조건** (README 계약 10): `GameManager.CurrentPhase == Battle` && `!GameManager.IsAiming` &&
   `!PointerOverUi()`(터치는 `primaryTouch.touchId` — tap-to-place 계약과 동일) &&
   `!dragController.HasArmedUnit` && 드래그 세션 비활성 && `bridge.TryScreenToCell` 성공 &&
   해당 셀에 배치 유닛 존재(Bridge 에 `TryGetDefenderAt(cell, out unit)` 읽기 seam 1개 추가) &&
   진입 쿨다운 경과.
3. **홀드 판정**: press 유지 `holdSeconds` 도달 → 이동모드. 홀드 중 이동 임계(픽셀, 기존 보드 제스처
   임계 재사용) 초과 시 홀드 취소. 임계 전 릴리즈 = 아무것도 소비하지 않음(탭은 기존 소비자 몫 — unit 4).
   홀드 진행 표시는 최소(타일 위 라디얼 스윕 — placement-cooldown 오버레이 패턴 참조하되 스코프 최소).
4. **이동모드 진입**: `_slowmoLease = TimeManager.Instance.Request(TimeDomain.Battle, dragSlowmoScale)`
   (기존 드래그와 동일 스케일 소스·priority 0) · 유닛 하이라이트(해당 entity 뷰 틴트 — SpineUnitView seam,
   `highlightColor`) · `CameraDirector.SetInspectFocus(GridCellToViewCenter(cell))` (DirectionAim 패턴 L101) ·
   진입 쿨다운 시작(확정/취소 무관 — 슬로모 남용 방지 README 계약 7).
5. **취소 경로** (전부 슬로모 해제 + 하이라이트 해제 + 카메라 복귀):
   - 본인 타일 탭 / 무효 입력은 unit 2 에서 판정 후 이 컨트롤러의 `CancelMoveMode()` 호출
   - `moveModeTimeoutSeconds` 경과 → 자동 취소 (무한 슬로모 차단)
   - 이동모드 중 대상 유닛 사망 → 즉시 자동 취소 (`_defenderByTile` 에서 사라짐 폴링 또는 death 이벤트 구독)
6. **상태 노출**: `bool InMoveMode` / `Vector2Int MoveSourceCell` / `DefenderUnitData MoveUnit` —
   unit 2 가 소비. 기존 `_armedUnit`(트레이) 과 완전 분리.

## 구현 노트 (구현서와 달라진 점)

- **드래그 컨트롤러 참조**: 씬 직렬화가 아니라 `DefenderSelector.DragController` 경유 lazy 해석 —
  드래그 컨트롤러는 DefenderSelector 가 런타임 AddComponent 로 만들기 때문(씬에 존재하지 않음).
- **홀드 진행 표시**: 별도 라디얼 위젯 대신 `SetHoverHighlight` 틴트를 진행률로 페이드-인(스코프 최소).
  전용 게이지는 후속 폴리시 후보.
- **임계 공유**: 스와이프 판정 임계도 `BoardDragThreshold` seam 으로 드래그 컨트롤러와 공유.

## 완료 기준

- [x] 컴파일 클린 + 씬 배선 완료(참조 누락 0 — 씬 YAML fileID 5/5 비영 확인)
- [x] 홀드 → 이동모드(슬로모 lease) / 임계 전 릴리즈 무반응 — PlayMode `RelocationMoveModeTest`
      (상태 머신 reflection 구동, 원격 검증 경로). 하이라이트·카메라 포커스는 코드 경로 —
      시각 체감은 unit 3 의 사용자 Play 게이트에서 함께 확인
- [x] 진입 쿨다운: 취소 직후 재홀드가 쿨다운만큼 거부됨 — 테스트 검증
- [x] 타임아웃 자동 취소 + 슬로모 해제(배속 1× 복귀) — 테스트 검증. 대상 사망 취소는 타임아웃과
      같은 exit 경로(`StillValidSource`) — 코드 경로 확인
- [x] 스킬 조준 중·트레이 armed 중·드래그 중 홀드 진입 불가 — 가드 코드 확인(자동 테스트 없음,
      unit 4 경합 정리에서 재점검)

2026-07-24 자동 검증 통과 (PlayMode 1/1, 에디터 실행).

## 버그 수정 (2026-07-24) — 이동모드 전혀 발동 안 됨

**증상**: 실제 Play 에서 유닛을 홀드해도 이동모드에 전혀 진입 안 함. 자동 테스트(Step reflection
구동)는 통과했으나 그것들은 `Update()`→입력 경로와 UI 게이트를 실질적으로 안 탔다(거짓 신뢰).

**근본 원인**: `TryBeginHold` 의 UI 게이트가 `EventSystem.IsPointerOverGameObject()` 기반이었는데,
이 API 는 EventSystem 의 "지난 프레임/다른 pointer" 상태를 읽어 **보드 위에서도 true 를 반환** →
홀드가 시작조차 안 됨. `DcInspectController` 는 이미 이 API 를 버리고 명시 좌표 `RaycastAll` 을
쓴다(그래서 인스펙트는 동작). 확정 근거: 사용자가 "터치다운에 선택모드(인스펙트) 발동"을 봤다 =
그 유닛 위에서 DcInspect 의 `RaycastAll==0` (UI 없음) → 같은 지점에서 relocation 의
`IsPointerOverGameObject`==true 였다는 비대칭.

**수정**: relocation 의 UI 판정을 DcInspect 와 동일한 **명시 좌표 `RaycastAll`**(`IsOverUi(screen)`)로
교체. "탭으로 인스펙트되는 유닛은 홀드도 시작된다"를 보장(둘이 같은 press 를 동일 해석).

**검증 한계**: press 전이(`wasPressedThisFrame`)는 코루틴 하네스로 신뢰 재현이 안 돼(InputTestFixture
필요) 실입력 자동 검증은 불가. 근본 원인은 코드 로직+사용자 관측으로 확정. **실제 Play 시각 확인 필요.**
reflection Step 테스트는 UI 게이트가 환경 의존이라, 테스트의 런타임 Overlay 아티팩트를 캔버스
비활성으로 제거해 "보드 위 UI 없음"(실 Play) 상태로 맞춰 상태머신을 검증한다.
