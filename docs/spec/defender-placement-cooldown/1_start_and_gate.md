# 1 — 쿨타임 시작 + 배치 차단 (로직)

## 목적

배치가 **성공**하면 그 유닛 타입을 쿨타임에 넣고(unit 0 런타임), 쿨타임 중에는 그 슬롯에서 드래그/탭 배치 세션이 시작되지 않게 차단한다. 시각 표시는 unit 2 — 여기서는 게이트 **동작**만 완성한다.

## 변경 대상

- ~~`Assets/_Project/Scripts/UI/DefenderSelector.cs` — `PlacementCommitted` 구독 → 쿨타임 시작~~
  ⚠ **이 계약은 대체됨** (battle-sim-extraction unit 15-A, 2026-08-05): 쿨타임 시작은 이제
  `BattleBridge.StartPlacementCooldown` 이 단일 소유자다. UI 가 시작하면 **뷰를 거치지 않는 배치
  경로(세션 커맨드·클릭 배치·테스트)가 쿨타임을 통째로 무시**하기 때문이다. 아래 "시작" 절의
  코드는 역사 기록으로 남긴다. 게이트(딤 처리)는 그대로 UI 가 하고, 판정도 이제
  `MatchPlacementRules.Check` 가 `PlacementRejectReason.OnCooldown` 으로 낸다.
- `Assets/_Project/Scripts/UI/DefenderDragSlot.cs` — 드래그/탭 진입 게이트

## 구현

**시작 (DefenderSelector)** — 컨트롤러의 기존 이벤트를 구독한다. 컨트롤러는 런타임 AddComponent 라 수명 소유자인 selector 가 훅한다(costDisplay/gimmickGuide 주입 패턴과 동일).
- 구독은 `OnEnable()` 에서 한다(`Awake` 가 이미 `EnsureDragController` 로 컨트롤러를 확정하므로 존재 보장 — critic m1: `OnDisable` 해제와 대칭이라 selector GO 가 배치 전이 밖에서 disable/enable 돼도 구독이 유실되지 않는다). 멱등:
  ```csharp
  // OnEnable
  if (dragPlacementController != null) {
      dragPlacementController.PlacementCommitted -= OnDefenderPlaced; // 중복 방지
      dragPlacementController.PlacementCommitted += OnDefenderPlaced;
  }
  ```
- `OnDisable()` 에서 null 가드 해제: `if (dragPlacementController != null) dragPlacementController.PlacementCommitted -= OnDefenderPlaced;`
- 핸들러:
  ```csharp
  private void OnDefenderPlaced(DefenderUnitData unit)
  {
      var rt = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
      if (rt != null && unit != null) rt.StartCooldown(unit, unit.placementCooldown);
  }
  ```
- `placementCooldown == 0` 이면 `StartCooldown` 이 no-op(unit 0 계약) → 아무 일도 없음.
- `PlacementCommitted` 는 드래그·탭·보드드래그 경로가 모두 수렴하는 `CommitPlacementAt` 성공 시 발화한다. `RequiresFacing` 유닛은 조준 페이즈 진입 시점(엔티티 스폰·코스트 소모 확정 지점)에 발화 → 그때 쿨타임 시작(계약 3). 배치 거부/취소 경로는 발화하지 않으므로 쿨타임도 시작 안 함.

**차단 (DefenderDragSlot)** — 기존 코스트 사전 차단(`_suppressedDrag`)과 같은 자리에 쿨타임 체크를 **먼저** 둔다. 쿨타임과 코스트는 독립 사유:
- `OnBeginDrag` 진입부, 코스트 체크 앞:
  ```csharp
  var cdRuntime = GameManager.Instance != null ? GameManager.Instance.CooldownRuntime : null;
  if (cdRuntime != null && cdRuntime.RemainingFor(_unitData) > 0f)
  {
      _suppressedDrag = true; // 세션 시작 안 함(OnDrag/OnEndDrag 무시)
      return;
  }
  ```
- `OnPointerClick`(arm 토글) 도 동일 — 단 **이미 armed 슬롯의 재탭(=해제)** 은 쿨타임과 무관하게 허용(코스트 게이트의 `!IsArmed(this)` 가드와 동형).
- 최종 권한은 여전히 `BattleBridge.TryBeginDefenderDeployment`(코스트/점유). **쿨타임은 ECS 로 내려보내지 않는다** — 슬롯 레벨 사전 차단만(계약 5, 맥락 경계 유지).
- unit 1 에서는 **별도 피드백 위젯을 만들지 않는다**. 차단(무반응)만. 탭 시 흔들림 등 juice 는 unit 2(오버레이가 남은시간을 이미 보여주므로 affordance 존재).

**우회 경로 없음(critic 열린 질문 해소)**: 모든 표준 배치는 슬롯 입력에서 출발한다 — 드래그(`OnBeginDrag`), 탭 arm(`OnPointerClick`), 그리고 그 하위인 board-gesture/시뮬 드래그(arm 이후 파생). first-session-tutorial 은 `TryGetAffordableTutorialSlot` 주석대로 **soft 추천만**(입력/선택을 바꾸지 않음)이라 강제 배치 경로가 없다 → 게이트를 우회하지 않는다. 설령 우회하는 프로그램적 배치가 생겨도 **시작(START)** 은 `PlacementCommitted` 가 모든 경로에서 발화하므로 쿨타임 등록은 누락되지 않는다(차단만 슬롯 레벨).

## 완료 기준

- [ ] 컴파일 클린.
- [ ] `placementCooldown > 0` 유닛 배치 직후: 같은 슬롯 재드래그/재탭이 세션을 시작하지 않음(preview/slomo 안 뜸). 다른 슬롯은 정상 배치.
- [ ] 쿨타임 경과(배틀 시간) 후: 같은 슬롯 다시 배치 가능.
- [ ] `placementCooldown == 0` 유닛: 연속 배치 무제한(회귀 없음).
- [ ] 배치 거부(무효셀/코스트 부족)로 끝난 경우: 쿨타임 시작 안 함(다음 시도 가능).
- [ ] 콘솔 클린. (검증은 임시 로그 또는 unit 2 오버레이로 확인.)

✅ 확인: 2026-07-22 · commit `4b9caeeb` — 컴파일 클린. 사용자 시각 확인(오버레이로 배치→쿨타임 진입 관측). 재배치 차단·슬로모 감속·정지 동결 등 동작 엣지 전체 Play 패스는 handoff Follow-up.
