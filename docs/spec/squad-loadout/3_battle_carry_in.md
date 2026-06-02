# 3 — 전투 반입 (드래프트 스킵)

## 목적

스쿼드가 선택돼 있으면 드래프트 UI 없이 SquadDraw 결과로 배치 단계에 진입한다.

## 변경 대상

- 수정 `Assets/_Project/Scripts/Core/GameManager.cs` — `Start` 스쿼드 분기
- 수정 `Assets/_Project/Scripts/Core/GameManager.cs` — `PlayerProfileSO` + `DefenderCatalog` + `PlacementPhaseView` 참조 추가
- 신규 `Assets/_Project/Tests/PlayMode/SquadCarryInSmokeTest.cs`

## 구현

`GameManager.Start` 분기 (우선순위: 스쿼드 → 드래프트 폴백):
```
var squad = profileSO?.profile?.SelectedSquad();
bool hasSquad = squad != null && !squad.IsEmpty();
if (hasSquad) {
    StartSquadMatch(squad);        // 새 경로
} else if (draftController != null) {
    ... 기존 드래프트 ...
} else { ... 기존 폴백 ... }
```

`StartSquadMatch(SquadSave squad)`:
1. `battleBridge.SetMapGenerationOptions(MapGenerationOptions.Default)` (스쿼드 모드 기본 맵).
2. `var ids = SquadDraw.Resolve(squad.unitIds, profile.ownedUnitIds, GenerateSeed())`.
3. `ids` → `DefenderCatalog.ById` → `DefenderUnitData[]` (null 스킵). 0개면 드래프트 폴백 + 경고.
4. `battleBridge.SetDefenderPool(units)`.
5. 스킬: `SkillLoadout.Configure(default if empty)` → `Roll()` → `battleBridge.SetSkillLoadout(picked)` (DraftController.BeginDraft/TryConfirm 의 스킬 경로를 재사용; 공통 헬퍼로 추출 가능).
6. `SetPhase(GamePhase.Placement)` → `placementPhaseView.BeginPlacementPhase()` 직접 호출 (드래프트의 DraftConfirmed 트리거 대체).

- 드래프트 경로/폴백/DraftView 는 **그대로 유지** — 스쿼드 미선택 시 회귀 없음.
- `GenerateSeed` 는 DraftController 와 동일 패턴(별도 헬퍼 또는 복제). 매 매치 변동.
- BattleBridge API(`SetDefenderPool`/`SetSkillLoadout`/`SetMapGenerationOptions`/`BeginPlacement`)는 기존 그대로 사용. 신규 ECS/맥락 없음.

## 완료 기준

- 스쿼드 채운 뒤 게임 시작 → **드래프트 UI 안 뜨고** 바로 배치 단계, 스쿼드 기반 유닛이 배치 가능, 전투 진행.
- 빈 스쿼드(또는 선택 없음) → 기존 드래프트 정상 동작.
- PlayMode `SquadCarryInSmokeTest`: 프로필에 스쿼드 채움 → BattleScene 로드 → phase==Placement(드래프트 스킵) + 배치 풀 count>0.
- 기존 PlayMode(`OutgameFlowSmokeTest`, `DraftFlowSmokeTest` 등) 여전히 통과.
- read_console clean.
