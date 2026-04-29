# BlockingHazard Data Model

**작업 구분**: 3

## 목적

차단형 hazard entity 의 ECS 데이터 모델 신설 — `BlockingHazard` 마커 + `BlockingHazardCellsBuffer` (멀티셀 점유). 이 단위는 컴포넌트 정의만, spawn / lifetime / visual 통합은 후속 unit 에서.

## 변경 대상

- Add: `Assets/_Project/Scripts/Battle/Effects/BlockingHazard.cs`
- Add: `Assets/_Project/Scripts/Battle/Effects/BlockingHazardCellsBuffer.cs`

## 구현

### BlockingHazard.cs

```csharp
using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // Marker + meta. Owned by Effects context.
    // entity layout: Obstacle + BlockingHazard + BlockingHazardCellsBuffer
    //              + Health (Units) + IncomingDamage (Units, buffer)
    //              + HealthBarState (Units) + FactionTag { BlockingHazard }
    //              + LocalTransform (center of cells)
    public struct BlockingHazard : IComponentData
    {
        public int hazardSoIndex;   // BlockingHazardSO registry 식별 (visual prefab 매핑용; -1 = unknown)
        public float maxHp;         // for HealthBarState normalization & destruction event payload
    }
}
```

### BlockingHazardCellsBuffer.cs

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Multi-cell occupancy. ObstacleLifetimeSystem reads this to populate ObstacleSingleton.blockedCells.
    // AttackSystem (range check) reads this to find nearest occupied cell from attacker.
    // Owned by Effects context — written only at spawn (EffectSpawner.SpawnBlockingHazard).
    public struct BlockingHazardCellsBuffer : IBufferElementData
    {
        public int2 cell;
    }
}
```

### 컴포넌트 조합 (entity 전체)

| 컴포넌트 | 맥락 | 역할 |
|---|---|---|
| `Obstacle` | Effects | cell (대표 cell — buffer 의 첫 entry 또는 center), worldPosition, remainingLife=∞ |
| `BlockingHazard` | Effects | 본 unit 신설. SO ref + maxHp |
| `BlockingHazardCellsBuffer` | Effects | 본 unit 신설. 점유 cell 목록 |
| `Health` | Units | 재사용 — HP 관리 |
| `IncomingDamage` (buffer) | Units | 재사용 — 데미지 누적 |
| `HealthBarState` | Units | 재사용 — HP bar 시각 |
| `FactionTag { BlockingHazard }` | Units | Unit 0 신설 — 적 attack target query 합류 |
| `LocalTransform` | (transforms) | center cell worldPosition |
| `DeadTag` | Units | HP 0 시 자동 부착 (DamageApplicationSystem) |

→ **AttackState 미부착** (반격 X). **AttackUnitTag/DefenderUnitTag 미부착** (각 시스템 식별 외 역할 없음).

## 단위 테스트 (EditMode)

없음 — 데이터 정의만. 실제 동작은 Unit 4/5/7 에서.

## 완료 기준

- 컴파일 성공.
- 기존 테스트 회귀 0.
- 동작 변화 0 (entity factory 미존재).
- 콘솔 에러/경고 0.

검증: 2026-04-29 — 컴파일 성공, EditMode 149/149 통과, 콘솔 에러/경고 0. 커밋 `3f5ab31`.
