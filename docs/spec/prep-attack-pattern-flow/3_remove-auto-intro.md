# 3 — 자동 인트로 제거 (진입 시 강제 공격패턴 표시 폐지)

> 상태: 진행 중 (2026-07-08)
> 범위: **Squad 모드 (`SquadPrepView`) 한정**. Draft 모드(`DraftView`)는 건드리지 않는다.

## 목적

Unit 0/1 이 만든 "진입 시 공격패턴 자동 인트로(Unroll→1초 dwell→Roll) 후 자동 진행" 을 폐지한다.
진입 시 공격패턴을 강제로 보여주지 않고 곧바로 배치(드캐 3중1 → placement)로 진행한다.
공격패턴은 좌상단 "!" 토글 버튼으로만 열람하며, 이 토글은 이미 배치·전투 전 구간에서 생존한다(페이즈 비의존).

## 검증 질문

Squad 진입 시 공격패턴이 **전혀 뜨지 않고** 곧바로 드캐 3중1 → 배치로 이어지는가?
그리고 배치 중·전투 중 좌상단 "!" 토글을 누르면 공격패턴을 열람/닫을 수 있는가?

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs`

(`WavePatternStripView.cs` / `MapSettingsPanelView.cs` 는 변경하지 않는다 — 공개 API 조합만 사용.)

## 구현

`SquadPrepView.OnMapSetupRequested` 에서 인트로 코루틴 재생을 제거하고, strip 을 "숨김 + 토글만 활성" 상태로 켠 뒤 곧바로 `AdvanceToPlacement()` 를 호출한다.

- 제거: `PlayIntro()` 코루틴, `introDwellSec` SerializeField, `_introRoutine` 필드, 관련 `StopCoroutine`.
- 유지:
  - mapSettings: `Initialize` + `SetActive(true)` (자체 토글, 패널 기본 접힘) — 변경 없음.
  - strip: `SetActive(true)` → `RebuildFromDeck()` → `SnapHidden()` → `SetToggleEnabled(true)`.
    - `RebuildFromDeck()` 유지: 토글로 열 때 현재 덱의 웨이브 카드가 올바로 보이도록.
    - `SnapHidden()` 유지: 진입 시 공격패턴이 화면에 안 뜨는 초기 상태.
  - 위 셋업 후 `AdvanceToPlacement()` 직접 호출 (기존엔 코루틴 끝에서 호출).
  - `_advanced` 가드 유지 — 진입 1회당 `RequestPlacement` 1회.
  - strip 미배선(headless) 시에도 `AdvanceToPlacement()` — 기존 동작 유지.

## 계약 변경 (Unit 0/1 대비)

- **자동 인트로 폐지**: 진입 시 Unroll/dwell/Roll 애니메이션을 재생하지 않는다. Unit 0 의 "Unroll 완료 대기 → dwell → Roll 완료 대기" 타이밍 로직은 더 이상 존재하지 않는다.
- **지속 토글은 유지**: Unit 1 의 계약(strip/맵설정을 자식으로 비활성화하지 않고 active 유지, "!" 토글 항상 사용 가능)은 그대로 살아 있다. 공격패턴 열람은 이 토글이 유일한 경로가 된다.
- `SnapHidden()` + `SetToggleEnabled(true)` 조합으로 진입 시 숨김·상시 토글 가능 상태를 만든다.
- ECS 경계·맥락과 무관한 순수 MonoBehaviour/UI 변경. `BattleBridge`/ECS 미접촉.

## 완료 기준

- compile: CS 에러 0 (UnityMCP `read_console`).
- Play (Squad 모드 진입): 공격패턴이 **뜨지 않고** 곧바로 드캐 3중1 선택 모달이 뜬다 → 배치로 이어진다.
- Play: **배치 중** "!" 토글로 공격패턴을 열고 닫을 수 있다.
- Play: **전투 중(GamePhase.Battle)** 에도 "!" 토글이 계속 동작한다.
- Play: 드캐 모달이 떠 있는 동안에는 토글이 (모달에 가려) 동작하지 않는다.
- ✅ 2026-07-08 Play 확인 통과 (사용자): 진입 시 공격패턴 미표시 → 곧바로 드캐 → 배치, "!" 토글 열람 동작.
