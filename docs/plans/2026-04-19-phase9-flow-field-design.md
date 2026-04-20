# Phase 9 Design — Flow Field 길찾기 교체

> 2026-04-19 브레인스토밍 (Q1~Q5) + Codex 2차 리뷰 결과로 확정된 Phase 9 설계 문서. 구현 종료 스펙은 `docs/PHASE9.md` (추후 Phase 9 종료 시 작성). 축 B 이관 항목은 `docs/phase10-prep.md`.

---

## 1. 배경

Phase 0~8 까지 길찾기는 `MapData.Paths` 코너 waypoint + `PathFollowState.currentWaypointIndex` 기반. Portal / Tornado / 향후 넉백 등 변위 발생 시:

- index 꼬임 (closest+1 fallback 이 "이미 지난 waypoint" 를 다음 목표로 지정)
- 기계적 직선 복귀 (Tornado 해제 후 waypoint 까지 직선 이동)
- 다중 경로 지원 구조적 부담 (`ResolveExitWaypointIndex` 가 `map.Paths[0]` 만 스캔)

이슈 A(포탈 동선), B(변위 후 복귀), C(다중 레인) 가 전부 waypoint index 모델에서 기인. flow field 로 교체하여 "적 현재 cell → field lookup" 구조로 해결한다.

## 2. 브레인스토밍 결정 (Q1~Q5)

### 2.1 Scope — 축 A 만 (Phase 9), 축 B 는 Phase 10 (Codex 재조정)

원 Q1 결정 B(축 A+B 통합) 는 Codex 2차 리뷰에서 **scope 팽창** 판정. Phase 9 를 축 A(flow field 교체) only 로 축소, 축 B(procedural + 테마 + multi-cell + enum 재분류) 는 `docs/phase10-prep.md` 로 전량 이관. 축 C(환경효과) 는 더 후속.

### 2.2 Goal 모델 — Single goal + Single spawn (Phase 9 한정)

Q4 원 결정 B(single goal + multi spawn) 를 유지하되 Phase 9 는 **single goal + single spawn 으로 더 축소**. `PrototypeMap.asset` 을 single-path(Path A 만) 로 단순화. Path B 는 Phase 10 multi-spawn 에서 부활.

### 2.3 Flow field 재계산 — Static per-play

Q3 결정: 판 시작 시 1회 BFS. 재계산 없음. Phase 10 환경효과 / 동적 장애물 도입 시 event-driven 으로 확장 가능 (싱글톤 레이아웃 불변).

### 2.4 Tile model — Phase 9 는 현재 enum 유지

Q2 결정 A(enum 재분류 Empty/Walkable/Placeable/Blocked) 는 Phase 10 이관. Phase 9 는 `TileType { Buildable=0, Path=1, Obstacle=2 }` 그대로 유지하여 `PrototypeMap.asset` silent corruption(Codex H-3) 위험 회피.

## 3. Codex 리뷰 공백 해결 매트릭스

(Phase 9 scope 축소 후 재평가)

| # | Codex 지적 | Phase 9 해결? | 해결 방법 |
|---|---|---|---|
| C-1 | FlowFieldSingleton NativeArray 수명 | ✅ | BattleBridge 가 Allocator.Persistent 로 allocate, TeardownCurrentBattle + OnDestroy 에서 dispose. 기존 GoalReachedEventsSingleton 패턴 재사용 |
| C-2 | Goal 도달 판정 규칙 | ✅ | `cell == goalCell` 정수 비교. PastGoalTag 트리거 위치 MovementSystem 유지 |
| C-3 | GeneratedMap 주입 | ⬇️ 축소 | Phase 9 는 MapData SO 그대로 사용. 신설 싱글톤은 FlowFieldSingleton 하나. 전체 주입 경로 설계는 Phase 10 |
| C-4 | SpawnEntry.pathId 의미 전환 | ⬇️ 축소 | Phase 9 는 pathId 유지 (PrototypeMap 단일 path 상황). spawnTileId 전환은 Phase 10 |
| H-1 | 패키지 버전 (Entities 6.x vs 1.4.5) | 미결 | Unity Editor 6000.3.5f2 → 6000.4+ 업그레이드 결정 대기 (§5) |
| H-2 | 축 A+B 통합 | ✅ | A 만 유지 |
| H-3 | TileType enum 충돌 | ➡️ Phase 10 | Phase 9 에서 enum 손대지 않음 |
| H-4 | Path carve 연결성 보장 | ➡️ Phase 10 | Phase 9 는 PrototypeMap 고정이라 무관 |
| H-5 | WorldToCell 변환 규칙 | ✅ | `cell = (int2)math.round(worldXZ / tileSize)` + bounds clamp. Burst 호환 static helper `GridMath` |
| H-6 | Spawn/goal metadata | ✅ | TileType 은 그대로. spawn/goal 은 `MapData.goalCell`(Vector2Int) + `MapData.spawnCells`(Vector2Int[]) 필드 추가 |
| M-1 | static flow field invariant | ✅ | 설계 §4.8 에 "defender 는 flow field 에 영향 주지 않음 (Placeable ≠ Walkable 전제)" 명시 |
| M-2 | tileSize 중복 소스 | ✅ | BattleBridge 단일 소스. MapView/PlacementInput 는 생성자/Init 주입. 원 `[SerializeField] tileSize` 3곳 제거 |
| M-3 | VFX world pos 일관성 | ✅ | `BattleBridge.GridToWorldCenter(int2 cell)` helper 도입. Slow/Tornado/Meteor/Portal 전부 통과 |
| M-4 | MapView 재작성 계약 | ⬇️ 부분 | Phase 9: Path LineRenderer 제거만. 전면 재작성은 Phase 10 |
| M-5 | retry seed 유도식 | ➡️ Phase 10 | procedural 도입 시 |
| M-6 | RNG 출처 | ➡️ Phase 10 | procedural 도입 시 |
| M-7 | greedy termination | ➡️ Phase 10 | |
| M-8 | Placeable 쿼터 | ➡️ Phase 10 | |
| M-9 | path carve 알고리즘 | ➡️ Phase 10 | |
| M-10 | PortalLink 영향 소스 | ✅ | BattleBridge + EffectSpawner + MovementSystem + EffectTickSystem 4곳 migration |
| L-1 | TileType 이름 변경 | ➡️ Phase 10 | |
| L-2 | gridSize SO | ➡️ Phase 10 | |
| L-3 | MapView obstacle root | ➡️ Phase 10 | |

## 4. Phase 9 구현 설계

### 4.1 FlowFieldSingleton (Effects 맥락)

```csharp
namespace Wassup.Battle.Effects
{
    public struct FlowFieldSingleton : IComponentData
    {
        public NativeArray<float2> flow;    // per cell, unit-length (goal cell = zero)
        public NativeArray<int>    dist;    // BFS cost from goal (unreachable = int.MaxValue)
        public int2                gridSize;
        public float               tileSize;
        public int                 version; // debug / Phase 10 event-driven rebuild marker
    }
}
```

- Allocator: `Allocator.Persistent`
- Lifecycle 책임: `BattleBridge`
  - 판 시작: `BuildFlowField(MapData)` 호출 시 allocate → entity 에 부착
  - `TeardownCurrentBattle`: singleton entity 파괴 전 arrays dispose
  - `OnDestroy`: idem
- 쓰기 맥락: Effects 가 소유. 읽기: Movement 허용
- 현재 운영 중 NativeQueue 채널들(`GoalReached/DefenderDeath/MeteorBurst/DefenderAttack`) 과 같은 패턴

### 4.2 GridMath helper (Movement 맥락 static)

```csharp
namespace Wassup.Battle.Movement
{
    public static class GridMath
    {
        public static int2 WorldToCell(float3 worldPos, float tileSize, int2 gridSize)
        {
            int cx = (int)math.round(worldPos.x / tileSize);
            int cy = (int)math.round(worldPos.z / tileSize);
            return new int2(math.clamp(cx, 0, gridSize.x - 1), math.clamp(cy, 0, gridSize.y - 1));
        }

        public static float3 CellToWorldCenter(int2 cell, float tileSize, float y = 0f)
            => new float3(cell.x * tileSize, y, cell.y * tileSize);
    }
}
```

- Burst 호환 static
- round + clamp: Portal exit / Tornado pull 결과가 경계 밖이어도 가장 가까운 valid cell 로 snap
- EditMode 테스트: 원점 / 경계 / 경계 초과 / 음수 좌표 각각

### 4.3 MapData 필드 추가 (Units / Data 맥락)

```csharp
public class MapData : ScriptableObject
{
    // 기존 유지
    public const int Width = 20;
    public const int Height = 10;
    [SerializeField] private TileType[] tiles;
    [SerializeField] private List<PathDefinition> paths;   // Phase 9 에서 deprecated. Phase 10 에서 완전 삭제

    // 신규
    [SerializeField] private Vector2Int goalCell = new Vector2Int(19, 5);
    [SerializeField] private Vector2Int[] spawnCells = { new Vector2Int(0, 5) };

    public Vector2Int GoalCell => goalCell;
    public IReadOnlyList<Vector2Int> SpawnCells => spawnCells;
}
```

- `paths` 는 Phase 9 에서 더이상 읽히지 않음 (MapView Path LineRenderer / BattleBridge spawn 경로 lookup 모두 제거). 필드 자체는 Phase 10 migration 까지 제거하지 않음 (asset schema 보존)

### 4.4 PrototypeMap.asset 수정

- `goalCell = (19, 5)` (Path A 의 끝)
- `spawnCells = [(0, 5)]` (Path A 의 시작)
- `paths` 는 asset 에서 손대지 않음 (코드 참조 제거로 충분)

### 4.5 BFS Flow Field Builder (Effects 맥락, 순수 함수)

```csharp
public static class FlowFieldBuilder
{
    public static void Build(
        NativeArray<byte> walkMask,    // 1 = Walkable, 0 = blocked
        int2 gridSize,
        int2 goal,
        NativeArray<float2> outFlow,   // allocated, [width*height]
        NativeArray<int>    outDist)   // allocated, [width*height]
    {
        // BFS from goal, 4-neighbor
        // outDist[goal] = 0; others = int.MaxValue
        // outFlow[cell] = normalize(neighbor_with_min_dist - cell)
        // outFlow[goal] = float2.zero
    }
}
```

- Walkable 기준: Phase 9 는 `TileType == Buildable || TileType == Path` (Obstacle 만 blocked)
  - 사유: 현재 enum 에서 Walkable 의미를 별도 가질 수 없음. Buildable 은 적이 지나갈 수 있어야 함(현재 waypoint 가 Buildable 타일 관통). Phase 10 enum 재분류 후 Walkable 단일 플래그로 명료화
- EditMode 테스트 3종:
  - 직선 맵 (장애물 없음) — 적 모든 cell 에서 flow 가 goal 방향
  - L자 우회 맵 — obstacle 존재 시 우회 방향 검증
  - 단절 맵 — goal 에서 도달 불가 cell 의 dist == int.MaxValue, flow == zero

### 4.6 MovementSystem 재작성

```csharp
[BurstCompile]
public partial struct MovementSystem : ISystem
{
    public void OnCreate(ref SystemState state) => state.RequireForUpdate<PathFollowState>();

    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var dt = SystemAPI.Time.DeltaTime;

        var field = SystemAPI.GetSingleton<FlowFieldSingleton>();
        var slowLookup = SystemAPI.GetComponentLookup<SlowEffect>(isReadOnly: true);

        var portals = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build()
                               .ToComponentDataArray<PortalLink>(Allocator.Temp);
        var tornadoFields = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build()
                                     .ToComponentDataArray<TornadoField>(Allocator.Temp);

        foreach (var (transform, follow, entity) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<PathFollowState>>()
                          .WithNone<PastGoalTag>()
                          .WithEntityAccess())
        {
            float3 current = transform.ValueRO.Position;

            // 1. Portal entry (exitWaypointIndex 제거됨)
            for (int p = 0; p < portals.Length; p++) { /* teleport only */ }

            // 2. Current cell lookup
            int2 cell = GridMath.WorldToCell(current, field.tileSize, field.gridSize);

            // 3. Goal check
            if (cell.x == /* goal x */ && cell.y == /* goal y */)
            {
                ecb.AddComponent<PastGoalTag>(entity);
                continue;
            }

            // 4. Tornado pull (unchanged from Phase 8 §17)
            // ...

            // 5. Flow field step
            int idx = cell.y * field.gridSize.x + cell.x;
            float2 dir = field.flow[idx];
            if (math.lengthsq(dir) < 1e-6f) continue;  // unreachable

            float slowMul = slowLookup.HasComponent(entity) ? slowLookup[entity].multiplier : 1f;
            float step = follow.ValueRO.speed * slowMul * dt;
            transform.ValueRW.Position = current + new float3(dir.x, 0, dir.y) * step;
        }

        portals.Dispose();
        tornadoFields.Dispose();
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
```

- PathFollowState 필드 축소: `speed` 만 유지. `currentWaypointIndex` + `tileSize` 제거
- PathWaypoint DynamicBuffer 완전 삭제
- goal cell 은 FlowFieldSingleton 에 `int2 goalCell` 필드 추가 (§4.1 수정)

### 4.7 PortalLink 변경 (Effects)

```csharp
public struct PortalLink : IComponentData
{
    public float3 entryWorld;
    public float3 exitWorld;
    public float  entryRadius;
    public float  duration;
    // public int exitWaypointIndex; ← 제거
}
```

- `EffectSpawner.SpawnPortal` 시그니처에서 `exitWaypointIndex` 파라미터 제거
- `BattleBridge.ResolveExitWaypointIndex` 메서드 전체 삭제
- `BattleBridge.ApplyPortal` 에서 `ResolveExitWaypointIndex` 호출 제거

### 4.8 Static flow field 유효 전제

> **불변조건 (문서로 고정)**: Phase 9 범위에서 flow field 는 판 시작 시 1회 계산되며, 이후 판 동안 재계산되지 않는다. 재계산을 유발할 수 있는 런타임 이벤트는 다음 조건에서만 발생 가능하며, Phase 9 에서는 존재하지 않는다:
> 1. defender 배치가 Walkable 타일을 점유 → (전제 위배: Placeable ≠ Walkable)
> 2. 실시간 장애물 생성/파괴 → (Phase 10 환경효과에서 가능)
> 3. goal 타일 이동 → (Phase 9 불변)

### 4.9 MapView 변경

- Path LineRenderer 제거 (`BuildPathLines` 메서드 삭제)
- tile cube primitive 생성 로직 유지
- `tileSize` 는 `BattleBridge` 에서 주입 (현재 `[SerializeField]` 제거)

### 4.10 PlacementInput 변경

- `tileSize` 현재 `[SerializeField]` 제거, BattleBridge 로부터 주입
- placement 판정은 `TileType == Buildable` 그대로 (Phase 10 에서 Placeable 로 교체)

### 4.11 BattleBridge.GridToWorldCenter

```csharp
public float3 GridToWorldCenter(int2 cell, float y = 0f) =>
    new float3(cell.x * _tileSize, y, cell.y * _tileSize);
```

- `ApplySlow / ApplyTornado / ApplyMeteor / ApplyPortal` / `SpawnMeteorWarningVisual` / VFX 위치 계산 전부 이 helper 통과
- 현재 각자 `tile.x * tileSize` 반복하는 4~5 사이트 통일

## 5. 패키지 버전 — **Phase 9→10 사이 재논의 (연기)**

결정: **Phase 9 는 현재 환경 (Unity `6000.3.5f2` + `com.unity.entities 1.4.5`) 에서 진행**. Unity Editor 6000.4+ 업그레이드 + Entities 6.x 전환은 **Phase 9 완료 후 Phase 10 착수 전 재논의**.

- 본 설계는 1.4 / 6.x 공통 API 만 사용하므로 업그레이드 여부와 독립적으로 적용 가능
- CLAUDE.md / TRD.md 의 "Entities 6.x" 표기는 향후 목표이며, 현재 구현은 1.4.5 위에서 수행됨을 본 문서로 확인
- 재논의 시점 근거: Phase 10 의 procedural 맵 생성이 Entities 6.x 신규 API 를 실질적으로 활용할 여지가 있고, Phase 10 scope 에서 업그레이드 리스크 상각이 더 쉬움

### Entities 6.x 로 전환 시점 참고

전환 결정 시 수행할 작업:
1. Unity Hub 에서 6000.4 LTS 또는 6000.5 설치
2. 프로젝트 Editor 버전 전환 + 패키지 재해결
3. URP / spine-unity / probuilder / timeline 호환성 재검증
4. ECS 핵심 API 컴파일 통과 확인 (ISystem / SystemAPI / ECB / DynamicBuffer / NativeQueue / BurstCompile)
5. PlayMode smoke test
6. CLAUDE.md / TRD.md Entities 버전 실제 설치 버전으로 갱신
7. manifest/packages-lock 단독 커밋

Phase 9 는 위 작업 없이 P9-01 부터 시작.

## 6. 작업 분해 (P9-XX)

- [ ] **P9-01** — MapData.goalCell / spawnCells 필드 추가. PrototypeMap.asset 값 편집. `MapData.paths` 에 `[Obsolete]` 표기
- [ ] **P9-02** — GridMath.WorldToCell / CellToWorldCenter + EditMode 테스트 (경계/clamp)
- [ ] **P9-03** — FlowFieldSingleton struct + NativeArray 수명 관리 (BattleBridge)
- [ ] **P9-04** — FlowFieldBuilder.Build (BFS 순수 함수) + EditMode 테스트 (직선/L자/단절)
- [ ] **P9-05** — MovementSystem flow field 기반 재작성. PathFollowState.currentWaypointIndex/tileSize 제거, PathWaypoint DynamicBuffer 삭제
- [ ] **P9-06** — PortalLink.exitWaypointIndex 제거. EffectSpawner.SpawnPortal 시그니처 수정. BattleBridge.ResolveExitWaypointIndex 삭제. EffectTickSystem 영향 검증
- [ ] **P9-07** — MapView: BuildPathLines 제거. tileSize SerializeField 제거
- [ ] **P9-08** — BattleBridge.GridToWorldCenter 도입. Slow/Tornado/Meteor/Portal 4곳 통일
- [ ] **P9-09** — Goal 도달 판정 cell 비교 교체 (MovementSystem 에서)
- [ ] **P9-10** — tileSize 단일 소스화 (BattleBridge → MapView, PlacementInput 주입)
- [ ] **P9-11** — 기준선 Play 녹화 (Portal / Tornado / normal move)
- [ ] **P9-12** — PlayMode 회귀: P9-11 과 동일 시나리오 비교

## 7. Phase 10 이관 항목

전부 `docs/phase10-prep.md` 에 상세 스펙.

## 8. Open Questions

- §5 Unity Editor 업그레이드 여부 (A / B)
- `MapData.paths` 필드를 Phase 9 에서 명시적 `[Obsolete]` 로 표기할지, 코드 참조만 제거할지

---

**작성**: 2026-04-19  
**상태**: 브레인스토밍 + Codex 2차 리뷰 반영 설계 완료. §5 미결 1건 + §8 Open questions 2건 해결 후 writing-plans 진입.
