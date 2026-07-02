# 0. Aggregate Clamp

## 목적

배율 스탯의 결합 결과에 floor/ceil 경계를 두어, 서로 다른 소스의 곱연산 modifier가 무한 누적(데미지 소멸 / 버프 런어웨이)되는 것을 막는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierMath.cs` (신규, static, runtime asm)
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStatsAggregateSystem.cs` (결합 라인 → helper 경유 + 경계 상수)
- `Assets/_Project/Tests/EditMode/ModifierMathTests.cs` (신규)

## 구현

- `ModifierMath.CombineMul(bool hasOver, float over, float add, float mul, float floor, float ceil)` →
  `math.clamp(hasOver ? over : (1f + add) * mul, floor, ceil)`. 순수 함수, Burst 호환.
- 집계 시스템: damageMul/attackSpeedMul/dmgTakenMul/moveSpeedMul 4개 write를 helper 경유로. 경계는 named const.
  regenPerSec는 `(0+Σadd)×Πmul` 유지(자원값, 배율 아님 — 클램프 제외).
- 경계 상수(framework 정책): DMG/AS/DTAKEN `[0.2, 5]`, MOVE `[0.15, 3]`.

## 완료 기준

- [x] `ModifierMathTests` 6종 통과 (2026-07-03): identity / 단일 디버프(범위 내) / 누적 디버프→floor / 누적 버프→ceil / additive / override 클램프 — TDD RED(타입 미존재 컴파일 실패) 확인 후 GREEN
- [x] compile 오류 없음
- [x] 전체 EditMode 스위트 회귀 없음 (450개, 기지 실패 ObstaclePlacer 1건 제외. 정상 범위 modifier는 클램프에 안 걸림)
- [ ] (수동) Play 재현: Guardian + Debuffer 다수 시 데미지가 floor(15×0.2=3) 아래로 안 떨어짐 — 사용자 확인 대기
