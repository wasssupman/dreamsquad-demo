# 1. MaxHealthMul — 최대체력 모디파이어 신설 + 번아웃 3룰 완성

## 목적

번아웃(-20%)과 라스트런(-90%)이 요구하는 **최대체력 배율** 스탯을 모디파이어 프레임에 추가한다. Effects 는 배율을 결정만 하고, Health 쓰기는 Units 안에서만 일어난다 (맥락 경계 유지).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs` — `StatKind.MaxHealthMul` append
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStats.cs` — `maxHealthMul` 필드
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStatsAggregateSystem.cs` — 집계 분기 + 전용 clamp
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스폰 시 ModifierStats init 2곳에 `maxHealthMul = 1f`
- `Assets/_Project/Scripts/Battle/Units/Health.cs` — 순수 함수 `ScaleMax`
- `Assets/_Project/Scripts/Battle/Units/MaxHealthScaleState.cs` — 신규 컴포넌트 (baseMax 보존 + 적용 배율 캐시)
- `Assets/_Project/Scripts/Battle/Units/MaxHealthScaleSystem.cs` — 신규 시스템 (배율 소비 + Health 쓰기)
- `Assets/_Project/Data/Gimmick/StackModifier_Fatigue.asset` — 3룰 완성 (Edge AS / Edge DMG / Consume MaxHP)
- `Assets/_Project/Tests/EditMode/HealthScaleMaxTests.cs` — 순수 함수 테스트

## 구현

1. **StatKind/ModifierStats append**: `MaxHealthMul` (base 1). 스폰 init 2곳(defender/enemy)에 `maxHealthMul = 1f` 필수 — dirty 는 disabled 로 추가되므로 무-모디파이어 유닛은 집계가 안 돈다 (damageVsCcMul 전례와 동일).
2. **집계**: 기존 6스탯과 동일한 mul/add/override 패턴. clamp 는 전용 `MaxHealthMulFloor = 0.05f` — 라스트런 ×0.1 이 기존 `MulStatFloor(0.2)` 에 걸리면 안 된다. ceil 은 `MulStatCeil(5)` 공유.
3. **소비 (Units)**: `MaxHealthScaleSystem` — `BattleSimGroup`, `ModifierStatsAggregateSystem` 이후.
   - lazy attach: `Health + ModifierStats` 보유 & `maxHealthMul != 1` 인 엔티티에 `MaxHealthScaleState { baseMax = 현재 max, appliedMul = 1 }` 부착 (스폰 경로 무수정).
   - 적용: `maxHealthMul != appliedMul` 일 때만 `Health.ScaleMax` 로 재계산. `mul <= 0` 은 미초기화 방어로 skip.
4. **순수 함수** (`Health.ScaleMax(value, baseMax, mul) → (newValue, newMax)`):
   - `newMax = max(1, baseMax * mul)` — 1 HP 바닥 (max≤0 방지)
   - `newValue = min(value, newMax)` — 축소 시 현재 체력 클램프, **복원 시 무료 힐 없음** (배율이 돌아와도 value 유지)
5. **번아웃 3룰**: `[Edge@5 AS ×0.8, Edge@5 DMG ×0.8, Consume@5 MaxHP ×0.8]`, 각 15s. Consume 마지막 계약 (unit 0) 준수.

## 완료 기준

- compile 통과 + EditMode `HealthScaleMaxTests` 전건 green (identity / 축소 클램프 / 복원 무료힐 없음 / 1 HP 바닥).
- 기존 `ModifierMathTests` 회귀 없음.
- BattleScene Play smoke 콘솔 클린.
- (실동작 검증은 unit 3 에서 번아웃 end-to-end 로 — 최대체력 감소가 HP 바에 반영되는지 확인.)

확인 2026-07-15 · 커밋 `c465c6a7` · EditMode 775 passed / 0 failed
