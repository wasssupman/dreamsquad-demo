# 6. 브리핑 웨이브 스트립 per-map 동기화 (draft 배선 철회 — unit 7 로 대체)

> **철회 2026-07-22**: 이 unit 은 `DraftView` 경로를 고쳤으나, 실게임(스쿼드 모드)은 draft 를 건너뛰어 **DraftView 는 죽은 경로**였다. draft 배선(`DraftView.RunFlow` 주입 + `DraftController.BuildBriefingWavePlan`)은 되돌렸다. 살아남은 것은 재사용 헬퍼 **`BattleBridge.BuildBriefingWavePlan()` + `WavePatternStripView.RebuildFromPlan()`** 뿐이고, 실 라이브 픽스는 **unit 7**(`MenuPopup`)이 한다. 아래 원문은 이력 보존.

---


## 목적

draft 스트립(`WavePatternStripView`)이 정적 serialized `deck`(WaveA) 프리뷰를 만들어, TwinLane(WaveB) 선택 시 브리핑이 실전과 어긋나던 것을 해소. 스트립이 **선택된 `ActiveDeck` + 실전과 동일한 wave seed** 로 프리뷰를 만들게 한다. 새 씬 배선 없이 기존 `DraftView.controller`(DraftController → battleBridge) 통로를 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildBriefingWavePlan()` 공개
- `Assets/_Project/Scripts/Core/DraftController.cs` — bridge passthrough
- `Assets/_Project/Scripts/UI/Draft/DraftView.cs` — RunFlow 에서 실전 플랜 주입
- `Assets/_Project/Scripts/UI/Draft/WavePatternStripView.cs` — `RebuildFromPlan(plan)` 추가

## 구현

- `BattleBridge.BuildBriefingWavePlan()`: `TryInitializeGeneratedWaves` 의 **생성 경로와 동일** ActiveDeck·seed 로직 미러 → `waveSeed!=0 ? waveSeed : DeriveWaveSeed(matchSeed)`. authored-plan 경로 제외. ActiveDeck null/비생성이면 `default`(→ 스트립이 정적 deck 폴백). draft 시점엔 `_matchSeed`·ActiveDeck 이 이미 확정(PrepareDraftMap 선행)이라 **브리핑 = 실전 플랜 결정론적 동일**.
- `DraftController.BuildBriefingWavePlan()` → `battleBridge?.BuildBriefingWavePlan() ?? default` 패스스루(bridge 는 private 유지).
- `DraftView.RunFlow`: `strip.RebuildFromDeck()` → `plan = controller.BuildBriefingWavePlan(); if (plan.waves != null) strip.RebuildFromPlan(plan); else strip.RebuildFromDeck();`. 폴백은 정적 deck(아웃게임 SquadPrep 등 bridge 부재 컨텍스트 무영향 — 그 스트립은 RebuildFromDeck 그대로).
- `WavePatternStripView.RebuildFromPlan(GeneratedWavePlan)`: build+clear 후 plan.waves 로 카드 생성. `RebuildFromDeck` 은 정적 deck 로 플랜을 만들어 이 메서드에 위임(예외 시 메시지 카드).

## 완료 기준

- [x] compile 0 errors, 기존 EditMode green (1261/1263, 0 fail)
- [x] Play(BattleScene) `debugFixedMatchSeed=2` → `BuildBriefingWavePlan()` ActiveDeck=WaveB, seed=587014748=`DeriveWaveSeed(2)`(실전 동일), 유닛={swift,needler,sniper,runner,debuffer,boss} 전부 WaveB 구성(WaveA 전용 없음). 스트립 `RebuildFromPlan` 카드=`Swift ×5/Needler ×1/Sniper ×4…`(WaveB)
- [x] briefing plan.seed == 실전 wave seed(같은 ActiveDeck·`DeriveWaveSeed(matchSeed)` 미러) → 브리핑=실전 결정론적 동일
- [~] 아웃게임 SquadPrep 스트립은 bridge 부재 → 정적 deck 폴백 유지(무회귀, 코드 경로상 보장)

확인 2026-07-22 (unit 6 — 브리핑 스트립 per-map 동기화. EditMode 1261/1263, Play 플랜·카드 실증).
