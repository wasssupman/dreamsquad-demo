# 1. AttackSystem Chebyshev 전환

## 목적

`AttackSystem` 의 사거리 판정을 Chebyshev 타일 거리로 교체한다.  
타겟 선택 우선순위(`bestSq`)는 기존 월드 거리 유지 — 범위 체크만 교체.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`

## 구현

### tileSize / gridSize 소스

`FlowFieldSingleton` 을 기존 코드에서 이미 읽고 있으므로 그대로 활용:

```csharp
bool hasFlowField = SystemAPI.TryGetSingleton<FlowFieldSingleton>(out var flowField);
float tileSize = hasFlowField ? flowField.tileSize : 1f;
int2  gridSize = hasFlowField ? flowField.gridSize : new int2(128, 128);
```

### 공격자 루프 — 범위 체크 교체

기존:
```csharp
float range   = attack.ValueRO.range;
float rangeSq = range * range;
...
float d2 = DistanceSqToTarget(...);
if (d2 <= rangeSq && d2 < bestSq) { ... }
```

변경:
```csharp
int   tileRange = GridMath.RangeToTiles(attack.ValueRO.range);
int2  atkCell   = GridMath.WorldToCell(atkPos, tileSize, gridSize);
...
int2  tgtCell   = GridMath.WorldToCell(targetPos, tileSize, gridSize);
if (GridMath.ChebyshevDistance(atkCell, tgtCell) > tileRange) continue;

float d2 = DistanceSqToTarget(atkPos, targetEntities[i], targetPos, ...);
if (d2 < bestSq) { bestSq = d2; bestTarget = ...; bestTargetPos = ...; }
```

`rangeSq` 변수 제거. `bestSq` 초기값 `float.MaxValue` 유지.  
`DistanceSqToTarget` 함수 자체는 무변경 (장애물 우회 경로 타겟 선택용).

### AoE 멀티타겟 루프 동일 적용

같은 시스템 내 AoE 타겟 수집 루프도 동일 패턴:  
`d2 <= rangeSq` → `GridMath.ChebyshevDistance(atkCell, tgtCell) <= tileRange`

## 완료 기준

- [ ] compile error 0
- [ ] PlayMode: range=1 defender 가 대각 1칸 적 공격 (기존 Euclidean 에서는 1.41 거리 → 사거리 밖이었음)
- [ ] PlayMode: range=3 defender 기존 행동 회귀 없음
