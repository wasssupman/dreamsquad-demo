# tile-range-unification

상태: 진행 중

## 목표

모든 범위 판정을 **Chebyshev 타일 거리**로 통일한다.  
기준 타일 주변 8개 타일 = distance 1. `max(|dx|, |dz|)` (타일 단위).

기존 Euclidean 제곱거리 비교(`dx²+dz²≤r²`)를 아래 열거된 위치에서 전부 교체한다.  
Euclidean 은 현 개발 단계에서 사용하지 않는다. 필요 시 별도 spec 으로 추가.

## 작업 단위 목록

| # | 파일 | 목적 |
|---|---|---|
| 0 | `0_gridmath_helpers.md` | `ChebyshevDistance` + `RangeToTiles` 헬퍼 추가 |
| 1 | `1_attacksystem_chebyshev.md` | `AttackSystem` 사거리 판정 → Chebyshev |
| 2 | `2_battlebridge_snapshot_checks.md` | BattleBridge 스냅샷 범위 체크 6종 + 시너지 8방향 |
| 3 | `3_tornado_chebyshev.md` | `TornadoField.radius` → `tileRange`, MovementSystem Chebyshev |
| 4 | `4_meteor_chebyshev.md` | `MeteorPending.radius` → `tileRange`, MeteorResolutionSystem Chebyshev |
| 5 | `5_tests_and_handoff.md` | EditMode 회귀 테스트 + SO 점검 + handoff |

의존 순서: `0 → 1, 2 (병렬) → 3, 4 (병렬) → 5`  
※ Unit 1 과 Unit 2 는 BattleBridge `InTileRange` 헬퍼를 공유하지 않음 — 각자 독립. 실제 독립 병렬 가능.

## Feature-wide 계약

- **거리 함수**: `GridMath.ChebyshevDistance(int2 a, int2 b) = math.cmax(math.abs(a - b))`. Burst 호환 static.
- **range → 타일 변환**: `GridMath.RangeToTiles(float r) = (int)(r + 0.5f)` (half-away-from-zero). `math.round` 미사용 — banker's rounding 회피. 모든 float range 를 int 로 변환할 때 반드시 이 헬퍼 사용.
- **월드→타일 변환**: 기존 `GridMath.WorldToCell(pos, tileSize, gridSize)` 그대로 사용.
- **tileSize / gridSize 소스**: ECS 시스템은 기존 `FlowFieldSingleton` 에서 읽음 (`SystemAPI.TryGetSingleton<FlowFieldSingleton>`). 신규 싱글턴 추가 없음.
- **타겟 선택 우선순위**: `AttackSystem` 의 `bestSq` (가장 가까운 타겟 선택) 는 기존 월드 거리 유지. **범위 체크(in/out 판정)만 Chebyshev 로 전환**.
- **TornadoField**: `float radius` → `int tileRange`. `float3 centerWorld` 는 풀 방향 계산에 유지. VFX 크기는 `GridMath.RangeToTiles(skill.range) * tileSize` 로 환산.  
  타일 경계 진동(boundary jitter) 은 허용 범위로 수용. 풀링은 지속 유지.
- **MeteorPending**: `float radius` → `int tileRange`. VFX 경고 링/버스트 크기는 `tileRange * tileSize` 로 환산.
- **시너지**: `dx=-1..1, dz=-1..1, (0,0) 제외` 8방향 루프.
- **BattleBridge 전환 대상 (6종)**: ApplySlow, ApplyOnPlaceEffect(SlowPulse / BindNearby / MeleeBurst / BoostNearbyDefenders / onPlacePushRadius).
- **ECS 맥락 경계**: `FlowFieldSingleton` 은 기존 패턴대로 여러 맥락이 읽기만 함. 쓰기는 BattleBridge 단독.

## 비목표

- `splashRadius` (ProjectileHitSystem) — 투사체 물리 특성, Euclidean 유지.
- `ForwardProjectile lateral width` (BattleBridge) — cone 형태, Euclidean 유지.
- `Portal entry radius` (MovementSystem) — 반경 catch zone, Euclidean 유지.
- `HazardSO.radius` (EffectSpawner HazardShapeSampler) — 별도 형상 샘플링 로직, 현행 유지.
- `AttackState.range` 타입 int 마이그레이션 — float 유지, `RangeToTiles` 헬퍼로 변환.
