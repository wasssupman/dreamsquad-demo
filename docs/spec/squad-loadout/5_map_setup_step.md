# 5 — 배치 이전 맵 설정 스텝 + 인게임 회귀 수정

> 추가 2026-06-03. B 종료 후 인게임 회귀 보고로 발생한 후속 작업.

## 목적

스쿼드 모드가 드래프트 경로의 준비 단계를 우회하면서 사라진 3가지(맵 스타일/배경 프랍, 유닛 선택 UI, 맵 설정 UI)를 복원한다. 맵 설정은 **배치 이전 자유 조정 스텝**으로 되살린다(원래 드래프트 단계에 있던 UX).

## 변경 대상

- 수정 `Assets/_Project/Scripts/Core/GameManager.cs` — StartSquadMatch + MapSetupRequested 이벤트
- 수정 `Assets/_Project/Scripts/UI/DefenderSelector.cs` — PlacementRequested 구독
- 신규 `Assets/_Project/Scripts/UI/Outgame/SquadPrepView.cs`
- 수정 `Assets/_Project/Scenes/BattleScene.unity` — SquadPrep GameObject
- 수정 `Assets/_Project/Tests/PlayMode/SquadCarryInSmokeTest.cs`

## 구현 / 계약

- **맵 스타일+프랍**: `StartSquadMatch` 가 `SetMapGenerationOptions(Default)` 직후 `battleBridge.PrepareDraftMap()` 호출. 드래프트 경로와 동일하게 themed 맵(타일 스타일 + BackgroundProps) 빌드. BeginPlacement 은 이미 생성된 맵 재사용.
- **유닛 선택 UI**: `DefenderSelector` 가 `GameManager.PlacementRequested` 도 구독(기존 DraftConfirmed 유지). 두 경로 모두 동일 strip 표시.
- **맵 설정 스텝**: `StartSquadMatch` 가 끝에서 `MapSetupRequested` 발화(구독자 없으면 `PlacementRequested` 폴백 — 헤드리스/테스트). `SquadPrepView` 가 구독 → 기존 `MapSettingsPanelView.Initialize(draftController)` + 표시 + START 버튼. START → `GameManager.RequestPlacement()` → `PlacementRequested` → 배치.
- 맵 변경은 `DraftController` → `RebuildDraftMap` 경유(배치 전이라 안전).

## 새 스쿼드 인게임 흐름

```
게임시작 → 맵 빌드(스타일+프랍)
  → [MAP SETUP] 공격패턴(WavePatternStrip) + MapSettingsPanel 자유 조정 → START
  → [드림캐쳐 첫 3중1] → [배치] DefenderSelector + 유닛 배치 → START BATTLE → 전투
```

## 후속 보강 2026-06-04 — 공격패턴 미리보기 복원

드래프트 단계에 있던 **공격패턴 미리보기**(`WavePatternStripView`)도 squad 흐름에서 누락 → MAP SETUP 스텝에 복원.
- `SquadPrepView` 에 `wavePatternStrip` 참조 추가. MapSetupRequested 시 `RebuildFromDeck()` + `FadeIn()`, START 시 `SnapHidden()`+비활성.
- strip 의 `AttackDeck` = BattleBridge.deck (동일 WaveA) → 미리보기가 실제 전투 웨이브와 일치(둘 다 WavePatternGenerator.Generate).
- BattleScene `SquadPrep.wavePatternStrip` → 기존 `DraftView/WavePatternStrip` 와이어링.
- Play 검증: MAP SETUP 에서 웨이브 카드 10장 표시, START 시 숨김+Placement. (스킬 미리보기 패널은 별도 후속.)

## 완료 기준

- Play: 시작 → MAP SETUP(맵 패널+START, phase=None) → START → Placement(DefenderSelector 7슬롯) + backgroundProps>0.
- 빈/미선택 스쿼드 → 드래프트 폴백 유지.
- PlayMode 6/6(SquadCarryInSmokeTest 는 prep START 후 Placement 검증).

> 완료 확인 2026-06-03 · 커밋 `a277a50`(회귀 2건) + `f68dff0`(맵 설정 스텝) — Play 전과정 + PlayMode 6/6.
> 주의: SquadPrepView 미배치 시 MapSetupRequested 구독자 0 → 바로 배치(폴백). MapSettingsPanel 은 DraftView 자식이지만 자기완결형이라 재사용. 라벨 영문(한글 폰트 후속).
