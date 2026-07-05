# 6 — MOVE 스톤 폐기 → 코스트 생산속도 스톤 + 인게임 배선

## 목적

배치형 디펜더에 무의미한 이동속도(MOVE) 스톤 16종(stone_049~064)을 **코스트 생산속도(CostRate) 스톤**으로 교체하고, 장착 시 매치의 코스트 재생 속도가 실제로 빨라지게 배선한다. (사용자 결정 2026-07-06)

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — `CardBuffKind.CostRate` **끝에 append**
- `Assets/_Project/Data/Dreamstones/Stone_049~064.asset` — kind=CostRate, displayName "{등급} Cost Stone", 티어 수치는 기존 표 그대로
- `Assets/_Project/Scripts/Core/CostRuntime.cs` — 재생 배율 훅
- `Assets/_Project/Scripts/Core/GameManager.cs` — 장착 스톤 분리 적용 (엔티티 스톤 vs 코스트 스톤)
- `Assets/_Project/Scripts/Core/DraftController.cs` — 드래프트 확정 시 배율 1.0 리셋
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs` — 요약 약칭 `COST`
- 테스트: DreamstoneCatalogTests(validator) + CostRuntime EditMode + PlayMode 반입/리셋 회귀

## 구현

- **CostRuntime**: `RegenRateMultiplier` (기본 1, `SetRegenRateMultiplier(float)`, 하한 0). `Update` 의 재생식을 `_regenPerSec * RegenRateMultiplier * dt` 로. 테스트 가능성을 위해 Update 본문을 `Tick(float dt)` (internal/public) 로 추출 — 동작 불변.
- **배율 소유권 계약 (set-then-apply 교훈의 대칭)**: `ResetToStart()`/`Configure()` 는 배율을 **건드리지 않는다**. 배율 설정은 매치 진입 결정 지점만:
  - `StartSquadMatch`/`StartTestModeMatch`: 장착 스톤 중 CostRate 합산 → `1 + Σ%/100`
  - `DraftController.TryConfirm`: `SetDreamstones(null)` 옆에서 `SetRegenRateMultiplier(1f)` — 드래프트 매치 무버프 보장 (REDRAFT 누수 수정과 동일 진입점)
  - RestartBattle(동일 매치 재시작): 무접촉 — 스톤 재적용 계약과 일관
- **GameManager 분리 적용**: `ResolveEquippedStones` 결과를 kind 로 분리 — CostRate 는 % 합산 → CostRuntime, 나머지는 기존 `SetDreamstones` (엔티티 경로). `MapDcEffect` 는 CostRate 를 모름 → default false 로 안전 skip (방어선, 코드 변경 불필요 — 주석만).
- **에셋**: stone_049~064 = Cost 블록 (등급 내림차순 × [상,중,중,하], 수치 표 기존 그대로: 7.5/6/6/4.5 · 5/4/4/3 · 3/2.4/2.4/1.8 · 2/1.6/1.6/1.2). MoveSpeed enum 값은 유지(직렬화 보존) — 사용 에셋만 0.
- **validator**: 카탈로그에 MoveSpeed 스톤 0 + CostRate 블록 존재 + 기존 64종/순차/티어 검사 유지.
- **UI**: `StoneSummary` 약칭 체인에 CostRate → `"COST"`.

## 완료 기준

- EditMode: validator PASS + `CostRuntime.Tick` 배율 반영 단위 테스트 (mul 2 → 재생 2배, 기본 1 → 불변)
- PlayMode: 코스트 스톤 장착 스쿼드 → StartSquadMatch → `RegenRateMultiplier == 1 + Σ%/100` assert · 드래프트 확정(누수 회귀 확장) → 1.0 리셋 assert · 기존 smoke 전체 회귀
- 육안: 코스트 스톤 장착 후 인게임 코스트 게이지가 체감 빠르게 차는지

> 완료 확인 2026-07-06 — 리그 게이트 PASS: EditMode 27/27 (CostRuntimeTests 4종 + validator MoveSpeed 0/CostRate 블록) + PlayMode 9/9 (코스트 반입 1.075, 드래프트 확정 배율 1.0 리셋, 전체 회귀). 육안: 코스트 게이지 체감 확인 잔여.
