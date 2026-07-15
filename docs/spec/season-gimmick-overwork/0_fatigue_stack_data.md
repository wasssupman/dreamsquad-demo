# 0. Fatigue 스택 데이터 — StackKind append + 번아웃 임계 룰 SO

## 목적

이상효과 "피로도"를 기존 StackModifier 임계값 파이프라인 위에 데이터로 올린다. 이 unit 이 끝나면 `StackModifierApplyEvent{kind=Fatigue}` 를 enqueue 하는 것만으로 5스택 도달 시 번아웃(스탯 디버프)이 발동한다 — 누적 소스(unit 3)와 무관하게 성립하는 토대.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs` — `StackKind` 에 `Fatigue` append
- `Assets/_Project/Data/Gimmick/StackModifier_Fatigue.asset` — 신규 SO (폴더 신설)
- `Assets/_Project/Scenes/BattleScene.unity` — BattleBridge `stackModifierAuthoring` 배열에 asset 추가

## 구현

1. `StackKind` enum 에 `Fatigue` 를 **append-only** 로 추가 (`None, Fire, Ice, Bleed, Poison, Fatigue`).
2. `StackModifier_Fatigue.asset` 생성:
   - `kind = Fatigue`, `maxStack = 5`, `policy = RefreshAll`
   - `perAppDuration = 25` — 누적 주기(10s)보다 충분히 길어야 슬롯이 누적 사이에 만료되지 않는다 (RefreshAll: 매 적용 시 remaining 갱신)
   - thresholds (본 unit 은 2룰, unit 1 에서 MaxHealthMul 추가로 3룰 완성):
     - `Edge@5` → ApplyStat `AttackSpeedMul` ×0.8, duration 15
     - `Consume@5` → ApplyStat `DamageMul` ×0.8, duration 15
   - **Consume 룰은 반드시 마지막**: `DispatchThresholds` 는 authored 순서로 순회하며 Consume 이 stackCount 를 즉시 차감하므로, 같은 atStack 의 Edge 룰이 Consume 뒤에 오면 발화하지 못한다. 이 순서 계약은 unit 1 의 3룰 구성(Edge AS / Edge DMG / Consume MaxHP)에도 그대로 적용된다.
3. BattleScene 의 BattleBridge `stackModifierAuthoring` 에 asset 참조 추가 → `BuildStackThresholdRegistry` 가 Fatigue kind 를 등록.

번아웃 지속시간 15s / 디버프 0.8 은 초기값 — 전부 SO 필드라 밸런스 튜닝 대상.

## 완료 기준

- compile 통과 (console 에러 0).
- BattleScene Play 진입 후 registry 확인: `BattleBridge.GetStackThresholds(StackKind.Fatigue)` 가 2룰 반환 (디버그 로그 또는 에디터 검사).
- 기존 Bleed 등록/동작에 회귀 없음 (registry 는 kind 별 독립 — Play smoke 로 확인).

확인 2026-07-15 · 커밋 `dfae1cd2`
