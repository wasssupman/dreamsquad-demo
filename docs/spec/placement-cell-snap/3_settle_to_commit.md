# 3 — settle-to-commit 디바운스 (deliberate tile lock)

**작업 구분**: feature · 의존: unit 1·2

## 목적

타일 이동 판정을 매 프레임 실시간 추종(휙휙)도, 멈출 때까지 freeze 도 아닌 **주기적(throttle)** 으로 커밋한다.
공간 히스테리시스(margin) 위에 시간 레이어를 얹어: `commitInterval`(0.2s)마다 현재 target 으로 타일을 갱신하고
사이 구간엔 유지 → **이동 중에도 interval 간격으로 "스텝" 이동**, 정지하면 다음 tick 에 현재 칸 확정(같으면 no-op).
(고스트는 스냅 안 함 — 하이라이트만 확정 팝(unit 4).)

> **해석 이력**: settle(정지 후 확정) 모델은 이동 중 하이라이트가 freeze 돼 "어디 갈지 안 보임" → 사용자가
> **"이동 중에도 0.2초마다 주기적 갱신"**(throttle)으로 확정. dwell/속도게이트/셀-거리 게이트는 폐기.

## 변경 대상

- New: `Assets/_Project/Scripts/UI/PlacementSnapDebounce.cs` (순수 `Step` + `State`)
- New: `Assets/_Project/Tests/EditMode/PlacementSnapDebounceTests.cs`
- Modify: `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (`ResolveFocusAndTarget(dt)` 에 Step 합성, `_debounce` 상태, ClearHover 리셋)
- Modify: `Assets/_Project/Scripts/Data/DragSwaySettings.cs` (`placementCommitDelay`, `commitImmediateDistance`; `placementStickMargin` 기본 0.18→0.3)

## 구현

- **순수 함수** `Step(ref State, committed, target, dt, interval) → 확정 셀`:
  - `interval ≤ 0`: target 즉시(매 프레임 실시간, throttle off).
  - `elapsed += dt`. `elapsed ≥ interval` 이면 `elapsed=0` + target 반환(tick), 아니면 committed 유지.
- **컨트롤러**: `hoverTile` 있으면 `cell = Step(ref _debounce, hoverTile.Value, target, dt, Cfg.placementCommitInterval)`.
  없으면(첫 프레임) `cell = target` + `_debounce` 리셋. `dt = Time.unscaledDeltaTime`(배치 슬로우모 무관).
- `ClearHover` 에서 `_debounce = default`.
- **SO**: `placementCommitInterval`(기본 **0.2**, `[0,1]`), `placementStickMargin` 기본 0.3.

## 완료 기준

- 컴파일 클린. EditMode: `PlacementSnapDebounceTests`(tick사이유지/tick커밋/커밋후재대기/이동중5Hz스텝/interval0실시간) 5케 통과.
- Play: 활발히 스와이프하는 중에도 0.2초마다 타일이 현재 칸으로 스텝 갱신(실시간 휙휙 아님, freeze 아님). 정지하면 현재 칸에 안착.
- 사용자 Play 체감 확인 일자 + 커밋 해시 추가 후 커밋.

**완료: 2026-07-17 · `a3812079` — 최종 `placementCommitInterval=0.5`(2Hz). settle→throttle 반전 이력은 위 "해석 이력" 참조.**
