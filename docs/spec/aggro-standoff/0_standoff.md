# 0 — aggro 사거리 정지 (standoff)

## 목적

aggro 도달 조건을 "guardian 중심"(stackThreshold 0.05)에서 **"공격범위 안"(`dist ≤ AttackState.range`)** 으로 바꾼다. 사거리에서 정지 → 이동·공격 일치, 엣지 겹침 해소.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — aggro 분기: `attackStateLookup`(RO) 추가, 도달 조건 `dist > stackThreshold(0.05)` → `dist > standoff(=AttackState.range)`.
- `Assets/_Project/Scripts/Battle/Combat/TauntAttackGrantSystem.cs` — `[UpdateBefore(typeof(MovementSystem))]` 추가(부여 range 동일 프레임 가시).

## 구현

- `standoff = attackStateLookup.HasComponent(entity) ? AttackState.range : 0f`.
- `dist ≤ standoff` 면 이동 종료(step 0, 그 자리 유지) → `continue`. 아니면 현행 guardian 접근(cell-trim, unit 2).
- range=1 → 직교 인접 walk 셀 중심(P 중심에서 dist 1.0)에서 도달 충족 → 셀 중심 정지(엣지 아님).
- range 없는 aggro 적(이론상 없음) → 0 → 경계까지 접근 폴백.

## 완료 기준

- compile 0 에러.
- Play: aggro 적이 공격범위(기본 사거리 1)에서 **정지(인접 walk 셀 ~중심, dist≈1)**, 그 자리에서 공격, **엣지 겹침 0**. guardian 처치 시 walk 타일로 복귀(`enemy-tile-movement-integrity` unit 2 흡수).
- standoff 비교(`dist ≤ range`)는 trivial → 전용 EditMode 없음. Play 통합으로 검증(거리/겹침 측정).
