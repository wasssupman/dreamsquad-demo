# match-intro-phase-toggles

> 상태: **완료 2026-08-18** (units 0~1, `b79859a7`). 인계: [`2_handoff_summary.md`](2_handoff_summary.md). 매치 인트로 두 페이즈(기믹 리빌 · 배치)를 `BattleConfig` 플래그로 끌 수 있게 한다.

## 상위 목표

인게임 진입 시 만나는 **인트로 페이즈를 config 플래그로 껐다 켤 수 있게** 한다. 기믹은 이미 플래그가 있으므로 값만 확정하고, 배치 페이즈에 같은 성격의 토글을 신설한다. 배치를 끄면 **3초 카운트다운(입력 불가) 후 자동으로 전투가 시작**된다.

## 검증 질문

1. **"플래그 하나로 배치 페이즈가 사라지고 3초 뒤 전투가 시작되는가?"** — `placementPhaseEnabled=false`.
2. **"그 3초 동안 아무것도 배치할 수 없는가?"** — 트레이 드래그·손패·클릭 배치 전부 불가.
3. **"플래그가 켜져 있으면 완전 무변화인가?"** — 현행 30초 배치 + START 버튼 그대로.
4. **"자동 시작으로 들어간 판이 정상 판인가?"** — 트레이·코스트 리젠·쿨타임이 전투 중 정상 동작.

## 배경 (현행 구조)

- **기믹 토글은 이미 있다** — `BattleConfig.gimmickEnabled`(`gimmick-match-integration` 검증 질문 3). `false` → `GameManager.AssignedGimmick=null` → 리빌 페이즈 스킵(`GimmickPhaseView.BeginIntro`) + 기믹 config 싱글턴 미주입(`BattleBridge.CreateGimmickConfigIfActive`) → 기믹 시스템 8종이 `RequireForUpdate` 로 전부 dormant. **이 spec 은 이 값을 `false` 로 확정하기만 한다**(사용자 결정 2026-08-18).
- 배치 페이즈 = `PlacementPhaseView`. `BeginPlacementPhase()` 가 `SetPhase(Placement)` · 코스트 `ResetToStart()` · 쿨타임 `ResetAll()` · `bridge.BeginPlacement()` · 30초 타이머(`CostConfig.placementPhaseDuration`) · START 버튼을 한 묶음으로 시작하고, `FinishPlacement()` 가 `SetPhase(Battle)` + 코스트 `BeginRegen()` + `bridge.StartBattle()` 로 닫는다.
- 진입점은 `GimmickPhaseView.BeginIntro()` 하나(리빌을 재생하든 스킵하든 `BeginPlacementPhase` 를 정확히 한 번 부른다). 미배선 폴백은 `BattleBridge.EnterPlacementOrIntro()`.
- **유닛 트레이는 Placement 진입 신호에서 슬롯을 구성한다** — `DefenderSelector.OnPhaseChanged` 의 `Placement` 분기가 `RebuildSlots` + 패널 노출을 하고, `Battle` 분기는 크기만 만진다.
- 배치 입력 = **드래그-드롭 전용**. 클릭 배치는 은퇴 상태(`PlacementInput.clickPlacementEnabled=false`)이고, 살아 있어도 `IsPointerOverGameObject()` 가드가 붙어 있다.
- 첫 판 튜토리얼이 배치 페이즈를 붙잡는다 — `FirstSessionTutorialController` → `BeginTutorialGate/UnlockTutorialStart/EndTutorialGate`.

## feature-wide 계약

1. **플래그 자리 = `BattleConfig`**(기믹 토글 옆). `placementPhaseEnabled`(기본 `true` = 현행) + `autoStartCountdownSeconds`(기본 3). **3초를 코드에 박지 않는다**(제약 6).
2. **노출 = `GameManager.BattleConfig` 읽기 전용 프로퍼티.** `CostConfig` 선례 그대로. 판정은 페이즈 소유자인 뷰가 하고 `GameManager` 가 대행하지 않는다.
3. **배치 페이즈 진입을 건너뛰지 않는다.** `SetPhase(Placement)` · 코스트 리셋 · 쿨타임 리셋 · `bridge.BeginPlacement()` · `PlacementReady` 는 **두 경로 공통**이다. 페이즈를 통째로 건너뛰면 트레이가 슬롯을 구성하지 못해 전투 내내 빈 채로 남는다(배경 참조). 바뀌는 것은 **창의 길이와 입력 가능 여부**뿐이다.
4. **종료 경로는 `FinishPlacement()` 하나.** 자동 시작도 이 함수로 합류한다. 전투 시작 호출부가 늘어나면 코스트 리젠·페이즈 전이 중 하나를 빠뜨린 두 번째 경로가 생긴다.
5. **자동 시작 창에는 입력이 없다.** 전면 raycast 블로커 + START 미노출. 배치 경로가 UI 드래그 하나뿐이라 블로커 하나로 닫힌다.
6. **~~튜토리얼이 배치 창을 잡으면 자동 시작은 기다린다.~~ → 폐기.** `tutorial-content-teardown` unit 0(2026-08-18)이 튜토리얼 콘텐츠를 걷으면서 이 계약의 두 겹이 **둘 다 소멸**했다. ① 첫 판 예외(`ShouldRunCore`)는 판정 술어 자체가 사라졌고, 그대로 두면 «튜토리얼이 없는데 첫 판만 30초»라는 유령 규칙이 됐을 것이다. ② 홀드 정지는 홀드를 거는 주체가 없어졌다. **지금은 `placementPhaseEnabled` 플래그가 곧 진실이다** — `UseAutoStart(placementPhaseEnabled)` 인자 하나. `TickAutoStart` 의 게이트(`CanFinishPlacement()`)는 남는다: 튜토리얼이 아니라 **드래그/조준 중 종료 금지**라는 원래 규칙을 지키고, 종료 가드와 술어를 일치시켜 판이 벽돌이 되는 것을 막는다(`a1392b4d`).
7. **판정은 순수 함수.** `PlacementPhasePolicy.UseAutoStart(configEnabled, tutorialCore)` — 이미 그 파일이 이 종류의 판정을 담고 EditMode 테스트를 갖고 있다.
8. **연출은 되돌릴 수 있게 분리한다.** unit 0 은 최소 라벨로 동작을 완결하고, 브롤스타즈식 카운트다운 연출은 unit 1 이 얹는다. unit 1 만 revert 해도 자동 시작은 살아 있다.
9. **범위**: 배치 페이즈 자체의 규칙(30초 길이·START 조건·코스트 산식)은 건드리지 않는다. 기믹 쪽은 **값만** 내린다(코드 0줄).

## 작업 단위

| 파일 | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 배관 | `0_placement_toggle_and_auto_start.md` | `BattleConfig` 2필드 + `GameManager` 노출 + 자동 시작 분기 + 입력 차단 + 기믹 값 확정 |
| 1 | 연출 | `1_countdown_presentation.md` | 중앙 대형 3·2·1 → GO! 펀치 카운트다운 |
| 2 | handoff | `2_handoff_summary.md` | 종료/인계 요약 |

## 파이프라인 커버리지

N/A — 데이터 SO 2필드 + MonoBehaviour View 분기/연출만 다룬다. 새 플레이 오브젝트(유닛·적·투사체·해저드)나 생성→렌더 경로 신설·변경이 없으므로 `docs/reference/object-pipeline-map.md` 대조 대상이 아니다.

## 후속 후보 (현 spec 범위 밖)

- **카운트다운 효과음** — 틱 3회 + GO 1회. 클립 저작이 별건이라 뺐다(`docs/spec/battle-audio` · ElevenLabs 파이프라인). 붙일 때는 `GimmickRevealConfig` 의 nullable 슬롯 패턴을 따른다.
- **자동 시작 판의 시작 코스트 재조정** — 배치 창이 없으면 첫 웨이브 전 배치량이 0이라 체감 난도가 올라간다. 밸런스 결정이라 분리(`CostConfig.startingCost`).
- **`BattleConfig` 시트 임포터** — 토글이 2개로 늘어 시트 관리 가치가 올라간다. `gimmick-match-integration` 후속 후보와 동일 항목.
- **인트로 스킵 프리셋** — 두 토글을 "인트로 없음" 한 스위치로 묶기. 토글이 3개 이상이 되면 그때.
