# Phase 9 Flow Field 길찾기 교체 Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace waypoint-based pathfinding with a static-per-play flow field on the existing `PrototypeMap.asset` (20×10), so enemies displaced by Portal / Tornado / future knockback recover their path autonomously via current-cell flow lookup instead of a frozen `currentWaypointIndex`.

**Architecture:** Effects 맥락이 `FlowFieldSingleton` 을 소유. BattleBridge 가 판 시작 시 BFS 로 goal 로부터 field 계산 후 Allocator.Persistent 로 싱글톤 엔티티에 주입, 판 종료 / OnDestroy 시 dispose. Movement 맥락은 field 를 읽기 전용으로 consume — 매 프레임 attacker 의 world pos 를 `GridMath.WorldToCell` 로 변환하여 해당 cell 의 flow 방향 벡터를 속도에 곱해 이동. Portal/Tornado 변위 후에도 다음 프레임 자동 복귀. `PortalLink.exitWaypointIndex` / `BattleBridge.ResolveExitWaypointIndex` / `PathWaypoint` DynamicBuffer / `PathFollowState.currentWaypointIndex` 제거.

**Tech Stack:** Unity `6000.3.5f2`, `com.unity.entities 1.4.5`, `com.unity.burst 1.8.21`, `com.unity.collections 2.5.7`, `com.unity.mathematics 1.3.2`. C# ECS (ISystem + Burst). EditMode 테스트 (NUnit, `Wassup.Tests.EditMode` asmdef).

**설계 문서:** `docs/plans/2026-04-19-phase9-flow-field-design.md` (Q1~Q5 결정 + Codex 공백 해결 매트릭스).

**선행 작업:** Entities 6.x 업그레이드는 Phase 9→10 사이 재논의로 연기 (설계 §5 참조). 본 계획은 1.4.5 환경에서 수행.

---

## Task 0: 기준선 Play 녹화 (P9-11) — 사용자 수작업

**파일:** 없음 (회귀 판정용 외부 증거)

**단계:**

1. Unity Editor 로 현재 `main` 상태에서 Play 진입
2. 다음 3 케이스를 스크린 녹화 (각각 짧은 영상 또는 연속 스크린샷 3~5장):
   - **케이스 A — Portal 동선**: exit 타일을 (i) 경로 위, (ii) 경로 옆(1타일), (iii) 경로에서 먼 곳(5+ 타일) 각각 지정 후 적이 포탈 통과한 뒤 이동 방향 기록
   - **케이스 B — Tornado 해제 후 복귀**: Tornado 를 경로 옆 타일에 캐스팅해 적 끌어당긴 후 지속시간 종료 시 이동 궤적 기록
   - **케이스 C — 평상시 진행**: 단일 적이 spawn → goal 까지 평상시 진행 (비교용 기준)
3. 녹화 파일을 `docs/plans/recordings/phase9-baseline/` (새 폴더) 에 저장
4. 녹화 상태를 한 줄 요약으로 `docs/plans/recordings/phase9-baseline/README.md` 에 기재

**Commit (사용자 수행):**

```bash
git add docs/plans/recordings/phase9-baseline/
git commit -m "chore(phase9): 기준선 Play 녹화 (pre flow-field)"
```

**회귀 판정:** Phase 9 완료 후 Task 10 에서 동일 시나리오 재녹화하여 비교.

---

## Task 1: MapData.goalCell / spawnCells 필드 추가 + PrototypeMap 편집 (P9-01)

**Files:**
- Modify: `Assets/_Project/Scripts/Data/MapData.cs`
- Modify: `Assets/_Project/Scripts/Data/Maps/PrototypeMap.asset` (YAML 직접 편집 또는 Inspector)

**Step 1: `MapData.cs` 에 필드 추가 + `[Obsolete]` paths 표기**

Modify `Assets/_Project/Scripts/Data/MapData.cs`:

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    public enum TileType : byte
    {
        Buildable = 0,
        Path = 1,
        Obstacle = 2,
    }

    [CreateAssetMenu(fileName = "MapData", menuName = "Wassup/MapData", order = 0)]
    public class MapData : ScriptableObject
    {
        public const int Width = 20;
        public const int Height = 10;

        [SerializeField] private TileType[] tiles = new TileType[Width * Height];

        [Obsolete("Unused since Phase 9 (flow field). Removed in Phase 10 asset migration.", error: false)]
        [SerializeField] private List<PathDefinition> paths = new List<PathDefinition>();

        [SerializeField] private Vector2Int goalCell = new Vector2Int(19, 5);
        [SerializeField] private Vector2Int[] spawnCells = { new Vector2Int(0, 5) };

        public TileType GetTile(int x, int y) => tiles[y * Width + x];
        public TileType[] RawTiles => tiles;

        public Vector2Int GoalCell => goalCell;
        public IReadOnlyList<Vector2Int> SpawnCells => spawnCells;

#pragma warning disable 618
        [Obsolete("Unused since Phase 9. Remove in Phase 10.")]
        public IReadOnlyList<PathDefinition> Paths => paths;
#pragma warning restore 618

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (tiles != null && tiles.Length != Width * Height)
                UnityEngine.Debug.LogWarning($"[MapData] tiles length must be {Width * Height}, got {tiles.Length}");
            if (spawnCells == null || spawnCells.Length == 0)
                UnityEngine.Debug.LogWarning("[MapData] spawnCells must contain at least 1 cell");
        }
#endif
    }

    [Serializable]
    public class PathDefinition
    {
        public string id;
        public List<Vector2Int> waypoints = new List<Vector2Int>();
    }
}
```

**Step 2: Unity 콘솔 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors. `[Obsolete]` 경고는 `Paths` 호출부 (MapView / BattleBridge) 에서 발생 예상 — Task 2 이후 단계에서 해당 참조 제거 시 사라짐.

**Step 3: `PrototypeMap.asset` 에 goalCell + spawnCells 추가**

Modify `Assets/_Project/Scripts/Data/Maps/PrototypeMap.asset` (YAML):

```yaml
%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  # ... (기존 meta 유지) ...
  tiles: 000000...  (기존 유지)
  paths:  # Obsolete, Phase 10 migration 때 제거
  - id: A
    waypoints:
    - {x: 0, y: 5}
    - {x: 19, y: 5}
  - id: B
    waypoints:
    - {x: 0, y: 2}
    - {x: 10, y: 2}
    - {x: 10, y: 8}
    - {x: 19, y: 8}
  goalCell: {x: 19, y: 5}
  spawnCells:
  - {x: 0, y: 5}
```

**Step 4: Unity 에디터 asset 재임포트**

Tool: `mcp__UnityMCP__refresh_unity`
Expected: asset 이 새 필드 반영 + 기존 tiles/paths 보존.

**Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Data/MapData.cs Assets/_Project/Scripts/Data/Maps/PrototypeMap.asset
git commit -m "feat(phase9): MapData.goalCell/spawnCells 필드 + PrototypeMap single-goal/single-spawn 설정 + MapData.paths [Obsolete] (P9-01)"
```

---

## Task 2: GridMath helper + EditMode 테스트 (P9-02)

**Files:**
- Create: `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`
- Create: `Assets/_Project/Tests/EditMode/GridMathTests.cs`

**Step 1: 테스트 먼저 작성**

Create `Assets/_Project/Tests/EditMode/GridMathTests.cs`:

```csharp
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class GridMathTests
    {
        [Test]
        public void WorldToCell_Origin_ReturnsZero()
        {
            var cell = GridMath.WorldToCell(new float3(0, 0, 0), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(0, 0), cell);
        }

        [Test]
        public void WorldToCell_ExactCellCenter_ReturnsCell()
        {
            var cell = GridMath.WorldToCell(new float3(5, 0, 3), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void WorldToCell_Rounds_NotFloors()
        {
            // 0.6 should round to 1, not floor to 0
            var cell = GridMath.WorldToCell(new float3(0.6f, 0, 0.4f), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(1, 0), cell);
        }

        [Test]
        public void WorldToCell_OutOfBounds_ClampsToEdge()
        {
            var cellHigh = GridMath.WorldToCell(new float3(100, 0, 100), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(19, 9), cellHigh);

            var cellLow = GridMath.WorldToCell(new float3(-10, 0, -10), tileSize: 1f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(0, 0), cellLow);
        }

        [Test]
        public void WorldToCell_DifferentTileSize_Scales()
        {
            var cell = GridMath.WorldToCell(new float3(10, 0, 5), tileSize: 2f, gridSize: new int2(20, 10));
            Assert.AreEqual(new int2(5, 3), cell);
        }

        [Test]
        public void CellToWorldCenter_MatchesWorldToCellInverse()
        {
            var world = GridMath.CellToWorldCenter(new int2(7, 4), tileSize: 1f);
            Assert.AreEqual(7f, world.x);
            Assert.AreEqual(0f, world.y);
            Assert.AreEqual(4f, world.z);
        }
    }
}
```

**Step 2: 테스트 실행 → 실패 확인**

Tool: `mcp__UnityMCP__run_tests` (testPlatform: EditMode, filter: `GridMathTests`)
Expected: 컴파일 에러 "type or namespace 'GridMath' could not be found"

**Step 3: 구현**

Create `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`:

```csharp
using Unity.Burst;
using Unity.Mathematics;

namespace Wassup.Battle.Movement
{
    [BurstCompile]
    public static class GridMath
    {
        public static int2 WorldToCell(float3 worldPos, float tileSize, int2 gridSize)
        {
            int cx = (int)math.round(worldPos.x / tileSize);
            int cy = (int)math.round(worldPos.z / tileSize);
            return new int2(
                math.clamp(cx, 0, gridSize.x - 1),
                math.clamp(cy, 0, gridSize.y - 1)
            );
        }

        public static float3 CellToWorldCenter(int2 cell, float tileSize, float y = 0f)
            => new float3(cell.x * tileSize, y, cell.y * tileSize);

        public static int CellIndex(int2 cell, int2 gridSize) => cell.y * gridSize.x + cell.x;
    }
}
```

**Step 4: 테스트 통과 확인**

Tool: `mcp__UnityMCP__run_tests` (testPlatform: EditMode, filter: `GridMathTests`)
Expected: 6 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Battle/Movement/GridMath.cs Assets/_Project/Tests/EditMode/GridMathTests.cs
git commit -m "feat(phase9): GridMath WorldToCell/CellToWorldCenter + EditMode 테스트 (P9-02)"
```

---

## Task 3: BattleBridge.GridToWorldCenter 도입 + VFX 4개 사이트 통일 (P9-08)

**Files:**
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

**목적:** 현재 `tile.x * tileSize` 를 4~5 사이트 (ApplySlow / ApplyTornado / ApplyMeteor / ApplyPortal / SpawnMeteorWarningVisual) 에 반복. Phase 9 에서 단일 helper 로 통일하여 Phase 10 tileSize 가변 시 영향 최소화.

**Step 1: `BattleBridge.GridToWorldCenter` 메서드 추가**

Modify `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — private 섹션에 추가:

```csharp
// Phase 9 — 모든 스킬 대상 타일 → world center 계산의 단일 소스.
// Phase 10 에서 tileSize 가 theme 파라미터로 승격될 때 이 helper 만 바꾸면 됨.
private float3 GridToWorldCenter(Vector2Int cell, float y = 0f)
    => new float3(cell.x * tileSize, y, cell.y * tileSize);
```

**Step 2: 기존 4~5 사이트 교체**

Grep 로 사이트 수집:

```bash
grep -n "tile.x \* tileSize\|tile\.x\*tileSize" Assets/_Project/Scripts/Bridge/BattleBridge.cs
```

각 사이트 예시 (`ApplyPortal` 기준):

Before:
```csharp
float3 entryWorld = new float3(entryTile.x * tileSize, 0f, entryTile.y * tileSize);
float3 exitWorld = new float3(exitTile.x * tileSize, 0f, exitTile.y * tileSize);
```

After:
```csharp
float3 entryWorld = GridToWorldCenter(entryTile);
float3 exitWorld = GridToWorldCenter(exitTile);
```

`ApplySlow / ApplyTornado / ApplyMeteor / SpawnMeteorWarningVisual` 각각 동일 패턴. Adjust `y` 인자를 필요 사이트에 전달 (기존 y=0f 이면 기본값).

**Step 3: Unity 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors.

**Step 4: PlayMode smoke test (수동 또는 MCP)**

- Slow / Tornado / Meteor / Portal 각 스킬 1회 발동
- 기존과 동일한 위치에 VFX 가 생성되는지 확인

**Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Bridge/BattleBridge.cs
git commit -m "refactor(phase9): BattleBridge.GridToWorldCenter helper + VFX 4개 사이트 통일 (P9-08)"
```

---

## Task 4: FlowFieldSingleton struct (P9-03 data)

**Files:**
- Create: `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs`

**Step 1: 구현**

Create `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs`:

```csharp
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — Effects 맥락이 소유하는 정적 flow field.
    // Allocator.Persistent 로 할당, 판 종료 / OnDestroy 에서 dispose.
    // Movement 맥락이 읽기 전용으로 consume.
    public struct FlowFieldSingleton : IComponentData
    {
        public NativeArray<float2> flow;        // [width * height], 각 cell 의 단위 방향. goal = zero.
        public NativeArray<int>    dist;        // BFS cost from goal. Unreachable = int.MaxValue.
        public int2                gridSize;    // (Width, Height)
        public int2                goalCell;
        public float               tileSize;
        public int                 version;     // 디버그 / Phase 10 event-driven rebuild 마커

        public bool IsCreated => flow.IsCreated && dist.IsCreated;

        public void Dispose()
        {
            if (flow.IsCreated) flow.Dispose();
            if (dist.IsCreated) dist.Dispose();
        }
    }
}
```

**Step 2: Unity 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors.

**Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs
git commit -m "feat(phase9): FlowFieldSingleton IComponentData + Dispose (P9-03 data)"
```

---

## Task 5: FlowFieldBuilder BFS 순수 함수 + EditMode 테스트 (P9-04)

**Files:**
- Create: `Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs`
- Create: `Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs`

**Step 1: 테스트 먼저 작성**

Create `Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs`:

```csharp
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;

namespace Wassup.Tests.EditMode
{
    public class FlowFieldBuilderTests
    {
        // 각 테스트 NativeArray 는 try/finally 로 dispose — Assert 실패 시 leak 방지.

        [Test]
        public void Build_StraightLine_AllCellsPointToGoal()
        {
            var gridSize = new int2(5, 1);
            var walk = new NativeArray<byte>(5, Allocator.Temp);
            var flow = new NativeArray<float2>(5, Allocator.Temp);
            var dist = new NativeArray<int>(5, Allocator.Temp);
            try
            {
                for (int i = 0; i < 5; i++) walk[i] = 1;

                FlowFieldBuilder.Build(walk, gridSize, new int2(4, 0), flow, dist);

                Assert.AreEqual(0, dist[4], "goal cell dist must be 0");
                Assert.AreEqual(4, dist[0], "distance from start to goal");
                Assert.AreEqual(new float2(1, 0), flow[0], "cell 0 must point +x");
                Assert.AreEqual(new float2(1, 0), flow[3], "cell 3 must point +x");
                Assert.AreEqual(new float2(0, 0), flow[4], "goal flow must be zero");
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        [Test]
        public void Build_ObstacleDetour_RoutesAround()
        {
            // 3x3 grid:
            //  . . G     y=2
            //  . X .     y=1   X = obstacle
            //  S . .     y=0
            var gridSize = new int2(3, 3);
            var walk = new NativeArray<byte>(9, Allocator.Temp);
            var flow = new NativeArray<float2>(9, Allocator.Temp);
            var dist = new NativeArray<int>(9, Allocator.Temp);
            try
            {
                for (int i = 0; i < 9; i++) walk[i] = 1;
                walk[1 * 3 + 1] = 0; // center obstacle

                FlowFieldBuilder.Build(walk, gridSize, new int2(2, 2), flow, dist);

                Assert.AreEqual(0, dist[2 * 3 + 2]);
                Assert.Greater(dist[0], 4, "start (0,0) must detour around obstacle, dist > manhattan 4");
                Assert.AreEqual(int.MaxValue, dist[1 * 3 + 1], "obstacle cell must be unreachable");
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }

        [Test]
        public void Build_Disconnected_UnreachableCellsHaveMaxDistAndZeroFlow()
        {
            // 3x1 grid, center obstacle splits left/right
            //  S X G
            var gridSize = new int2(3, 1);
            var walk = new NativeArray<byte>(3, Allocator.Temp);
            var flow = new NativeArray<float2>(3, Allocator.Temp);
            var dist = new NativeArray<int>(3, Allocator.Temp);
            try
            {
                walk[0] = 1; walk[1] = 0; walk[2] = 1;

                FlowFieldBuilder.Build(walk, gridSize, new int2(2, 0), flow, dist);

                Assert.AreEqual(0, dist[2]);
                Assert.AreEqual(int.MaxValue, dist[0], "left side unreachable from right goal");
                Assert.AreEqual(float2.zero, flow[0]);
            }
            finally { walk.Dispose(); flow.Dispose(); dist.Dispose(); }
        }
    }
}
```

**Step 2: 테스트 실행 → 실패 확인**

Tool: `mcp__UnityMCP__run_tests` (EditMode, filter: `FlowFieldBuilderTests`)
Expected: 컴파일 에러 "type 'FlowFieldBuilder' could not be found".

**Step 3: 구현**

Create `Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs`:

```csharp
using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    // Phase 9 — goal 에서 시작하는 4-neighbor BFS 로 dist + flow 계산.
    // 순수 함수. EditMode 테스트로 결정론 검증.
    public static class FlowFieldBuilder
    {
        private static readonly int2[] Dirs = {
            new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1),
        };

        public static void Build(
            NativeArray<byte>   walkMask, // 1 = walkable, 0 = blocked
            int2                gridSize,
            int2                goal,
            NativeArray<float2> outFlow,
            NativeArray<int>    outDist)
        {
            int w = gridSize.x, h = gridSize.y, n = w * h;

            for (int i = 0; i < n; i++) outDist[i] = int.MaxValue;
            for (int i = 0; i < n; i++) outFlow[i] = float2.zero;

            if (goal.x < 0 || goal.x >= w || goal.y < 0 || goal.y >= h) return;
            int goalIdx = goal.y * w + goal.x;
            if (walkMask[goalIdx] == 0) return;

            outDist[goalIdx] = 0;

            var queue = new NativeQueue<int2>(Allocator.Temp);
            queue.Enqueue(goal);

            while (queue.TryDequeue(out var c))
            {
                int cIdx = c.y * w + c.x;
                int cDist = outDist[cIdx];
                for (int d = 0; d < 4; d++)
                {
                    int2 n2 = c + Dirs[d];
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    int nIdx = n2.y * w + n2.x;
                    if (walkMask[nIdx] == 0) continue;
                    if (outDist[nIdx] <= cDist + 1) continue;
                    outDist[nIdx] = cDist + 1;
                    queue.Enqueue(n2);
                }
            }
            queue.Dispose();

            // Fill flow: 각 cell 에서 4-neighbor 중 dist 최소 방향 unit vector.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                if (outDist[idx] == int.MaxValue) { outFlow[idx] = float2.zero; continue; }
                if (outDist[idx] == 0)             { outFlow[idx] = float2.zero; continue; }

                int bestDist = outDist[idx];
                int2 bestDir = int2.zero;
                for (int d = 0; d < 4; d++)
                {
                    int2 n2 = new int2(x, y) + Dirs[d];
                    if (n2.x < 0 || n2.x >= w || n2.y < 0 || n2.y >= h) continue;
                    int nIdx = n2.y * w + n2.x;
                    if (outDist[nIdx] >= bestDist) continue;
                    bestDist = outDist[nIdx];
                    bestDir = Dirs[d];
                }
                outFlow[idx] = new float2(bestDir.x, bestDir.y);
            }
        }
    }
}
```

**Step 4: 테스트 통과 확인**

Tool: `mcp__UnityMCP__run_tests` (EditMode, filter: `FlowFieldBuilderTests`)
Expected: 3 tests PASS.

**Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Battle/Effects/FlowFieldBuilder.cs Assets/_Project/Tests/EditMode/FlowFieldBuilderTests.cs
git commit -m "feat(phase9): FlowFieldBuilder BFS 순수 함수 + EditMode 테스트 3종 (P9-04)"
```

---

## Task 6: BattleBridge 에서 FlowFieldSingleton 수명 관리 + 판 시작 시 BFS 실행 (P9-03 wiring)

**Files:**
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`

**Step 1: 싱글톤 entity 필드 + 수명 메서드 추가**

Modify `BattleBridge.cs` — 기존 `_defenderAttackQueue` 같은 singleton 수명 관리 패턴 옆에 추가:

```csharp
// Phase 9 flow field 싱글톤 entity reference
private Entity _flowFieldSingleton = Entity.Null;

// Idempotent: 재호출(판 재시작/redraft) 시 기존 Persistent arrays dispose 후 재생성.
// CRITICAL #1 (Codex 2차 리뷰): AddComponentData 는 component 존재 시 throw,
// 그리고 기존 arrays 가 dispose 없이 덮어써지면 누수. TeardownFlowField 선행으로 해결.
private void BuildFlowField()
{
    if (map == null || _em == null) return;

    // 기존 싱글톤 있으면 arrays dispose + entity destroy (멱등성 보장)
    TeardownFlowField();

    int w = MapData.Width;
    int h = MapData.Height;
    int n = w * h;

    var walk = new NativeArray<byte>(n, Allocator.Temp);
    try
    {
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            var t = map.GetTile(x, y);
            walk[y * w + x] = (byte)(t == TileType.Obstacle ? 0 : 1);
            // Phase 9: Buildable / Path 둘 다 walkable.
            // Phase 10 enum 재분류 후 Walkable 단일 플래그로 명료화.
        }

        var flow = new NativeArray<float2>(n, Allocator.Persistent);
        var dist = new NativeArray<int>(n, Allocator.Persistent);
        var gridSize = new int2(w, h);
        var goal = new int2(map.GoalCell.x, map.GoalCell.y);

        FlowFieldBuilder.Build(walk, gridSize, goal, flow, dist);

        var data = new FlowFieldSingleton
        {
            flow = flow,
            dist = dist,
            gridSize = gridSize,
            goalCell = goal,
            tileSize = tileSize,
            version = 1,
        };

        _flowFieldSingleton = _em.CreateEntity();
        _em.AddComponentData(_flowFieldSingleton, data);
    }
    finally
    {
        if (walk.IsCreated) walk.Dispose();
    }
}

private void TeardownFlowField()
{
    if (_flowFieldSingleton != Entity.Null && _em != null && _em.Exists(_flowFieldSingleton))
    {
        if (_em.HasComponent<FlowFieldSingleton>(_flowFieldSingleton))
        {
            var data = _em.GetComponentData<FlowFieldSingleton>(_flowFieldSingleton);
            data.Dispose();
        }
        _em.DestroyEntity(_flowFieldSingleton);
    }
    _flowFieldSingleton = Entity.Null;
}
```

**Step 2: 판 시작 / 종료 hook 에 연결**

- `StartBattle` 또는 판 준비 메서드 안에서 `BuildFlowField()` 호출 추가 (기존 queue 생성 근처)
- `TeardownCurrentBattle` 에서 queue dispose 옆에 `TeardownFlowField()` 호출 추가
- `OnDestroy` 에서도 `TeardownFlowField()` 호출 보장

**Step 3: 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors.

**Step 4: PlayMode 확인 — 판 진입 후 Entity inspector 로 FlowFieldSingleton 존재 확인**

- Play 진입
- Window → DOTS → Entities → Hierarchy 또는 log 에서 FlowFieldSingleton.version = 1 확인

**Step 5: Commit**

```bash
git add Assets/_Project/Scripts/Bridge/BattleBridge.cs
git commit -m "feat(phase9): BattleBridge 에서 FlowFieldSingleton 수명 관리 + 판 시작 시 BFS (P9-03 wiring)"
```

---

## Task 7: PortalLink.exitWaypointIndex 제거 + 파급 4개 소스 migration (P9-06)

**Files:**
- Modify: `Assets/_Project/Scripts/Battle/Effects/PortalLink.cs`
- Modify: `Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`ResolveExitWaypointIndex` 메서드 전체 삭제 + `ApplyPortal` 호출부 수정)
- Modify: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` (Portal 블록에서 `currentWaypointIndex` 덮어쓰기 제거 — Task 8 에서 전면 재작성되므로 최소 수정)

**Step 1: `PortalLink.cs` 에서 필드 제거**

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Effects
{
    public struct PortalLink : IComponentData
    {
        public float3 entryWorld;
        public float3 exitWorld;
        public float  entryRadius;
        public float  duration;
        // public int exitWaypointIndex;  ← removed in Phase 9
    }
}
```

**Step 2: `EffectSpawner.SpawnPortal` 시그니처 수정**

Grep for `SpawnPortal`:

```bash
grep -n "SpawnPortal" Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs Assets/_Project/Scripts/Bridge/BattleBridge.cs
```

Before:
```csharp
public static Entity SpawnPortal(EntityManager em, float3 entryWorld, float3 exitWorld,
                                  float entryRadius, float duration, int exitWaypointIndex)
{
    var e = em.CreateEntity();
    em.AddComponentData(e, new PortalLink {
        entryWorld = entryWorld, exitWorld = exitWorld,
        entryRadius = entryRadius, duration = duration,
        exitWaypointIndex = exitWaypointIndex,
    });
    return e;
}
```

After:
```csharp
public static Entity SpawnPortal(EntityManager em, float3 entryWorld, float3 exitWorld,
                                  float entryRadius, float duration)
{
    var e = em.CreateEntity();
    em.AddComponentData(e, new PortalLink {
        entryWorld = entryWorld, exitWorld = exitWorld,
        entryRadius = entryRadius, duration = duration,
    });
    return e;
}
```

**Step 3: `BattleBridge.ResolveExitWaypointIndex` 메서드 전체 삭제 + `ApplyPortal` 에서 호출 제거**

Delete `BattleBridge.cs:581~602` (ResolveExitWaypointIndex 메서드 블록 전체, 주석 포함).

Modify `ApplyPortal` (around `BattleBridge.cs:556~562`):

Before:
```csharp
int exitWaypointIdx = ResolveExitWaypointIndex(exitTile);
EffectSpawner.SpawnPortal(_em, entryWorld, exitWorld, entryRadius, skill.durationSec, exitWaypointIdx);
```

After:
```csharp
EffectSpawner.SpawnPortal(_em, entryWorld, exitWorld, entryRadius, skill.durationSec);
```

**Step 4: `MovementSystem.cs` 최소 수정 — Portal 블록에서 `exitWaypointIndex` 참조만 제거**

Find the Portal block (around line 49-62):

Before:
```csharp
if (portal.exitWaypointIndex >= 0 && portal.exitWaypointIndex <= waypoints.Length)
    follow.ValueRW.currentWaypointIndex = portal.exitWaypointIndex;
break;
```

After (minimal — Task 8 에서 MovementSystem 전면 재작성 시 전체 교체):
```csharp
// Phase 9: exitWaypointIndex 제거됨. 다음 프레임 flow field 가 새 방향을 공급.
break;
```

**Step 5: 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors.

**Step 6: Commit**

```bash
git add Assets/_Project/Scripts/Battle/Effects/PortalLink.cs \
        Assets/_Project/Scripts/Battle/Effects/EffectSpawner.cs \
        Assets/_Project/Scripts/Bridge/BattleBridge.cs \
        Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs
git commit -m "refactor(phase9): PortalLink.exitWaypointIndex 제거 + ResolveExitWaypointIndex 삭제 + 파급 4개 소스 migration (P9-06)"
```

---

## Task 8: MovementSystem flow field 재작성 — 3개 서브태스크 (P9-05 + P9-09)

> Codex HIGH #2/#3/#4 반영. 원 Task 8 (MovementSystem 재작성 + PathFollowState 축소 + PathWaypoint 삭제 + BattleBridge 스폰 migration) 은 30~60분 추정 + 기존 EditMode 테스트 3개 동시 파괴 위험. 3 단계로 분할하여 **매 commit 컴파일 + 기존 테스트 통과** 보장.

| 서브태스크 | 목적 | 파일 | 컴파일/테스트 상태 |
|---|---|---|---|
| **8A** | 기존 테스트 flow-field 기반으로 rewrite + 새 MovementSystem 구현 | MovementSystemTests / EffectIntegrationTests / UnitLifecycleSystemTests / MovementSystem | 모두 통과 (PathFollowState 는 legacy 필드 보유) |
| **8B** | PathFollowState 축소 + BattleBridge 스폰 migration | PathFollowState / BattleBridge | PathWaypoint 파일은 물리적으로 남되 참조 0 |
| **8C** | PathWaypoint 파일 삭제 + 잔재 grep 정리 | PathWaypoint.cs (+ .meta) | Phase 9 코드 cleanup 완료 |

---

### Task 8A: 테스트 rewrite + 새 MovementSystem 구현

**Files:**
- Rewrite: `Assets/_Project/Tests/EditMode/MovementSystemTests.cs` (전면 재작성)
- Modify: `Assets/_Project/Tests/EditMode/EffectIntegrationTests.cs` (`Movement_Applies_SlowEffect_Multiplier_To_Step` 메서드만)
- Modify: `Assets/_Project/Tests/EditMode/UnitLifecycleSystemTests.cs` (`CreateUnitAtGoal` 헬퍼만)
- Rewrite: `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs`

**Compile-safety 핵심**:
- `PathFollowState` 는 **이번 서브태스크에서 손대지 않음** — `currentWaypointIndex`, `tileSize` legacy 필드 그대로 유지 (Task 8B 에서 제거)
- `PathWaypoint.cs` 는 **삭제하지 않음** — 파일 존재 (Task 8C 에서 삭제)
- `BattleBridge.SpawnUnit` 의 `AddBuffer<PathWaypoint>` / `tileSize = tileSize` 코드 **그대로 유지** (Task 8B 에서 교체)
- 새 MovementSystem 이 PathWaypoint buffer 를 읽지 않고 PathFollowState.speed 만 읽으므로 legacy 필드가 있어도 동작

**Step 1: `MovementSystemTests.cs` 전면 재작성 (flow-field 기반)**

```csharp
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Tests.EditMode
{
    public class MovementSystemTests
    {
        private World _world;
        private EntityManager _em;
        private SimulationSystemGroup _simGroup;
        private Entity _fieldEntity;

        [SetUp]
        public void SetUp()
        {
            _world = new World("MovementSystemTestWorld");
            _em = _world.EntityManager;
            _simGroup = _world.CreateSystemManaged<SimulationSystemGroup>();
            _simGroup.AddSystemToUpdateList(_world.CreateSystem<MovementSystem>());
        }

        [TearDown]
        public void TearDown()
        {
            // NativeArray<float2>/<int> 는 Persistent 로 할당했으므로 명시적 dispose.
            if (_fieldEntity != Entity.Null && _em.Exists(_fieldEntity)
                && _em.HasComponent<FlowFieldSingleton>(_fieldEntity))
            {
                var f = _em.GetComponentData<FlowFieldSingleton>(_fieldEntity);
                f.Dispose();
            }
            _world?.Dispose();
        }

        // 5x1 직선 맵: 모든 cell 이 +x 방향을 가리킴. goal = (4,0).
        private void CreateLinearFlowField(int width = 5, float tileSize = 1f)
        {
            int n = width * 1;
            var flow = new NativeArray<float2>(n, Allocator.Persistent);
            var dist = new NativeArray<int>(n, Allocator.Persistent);
            for (int i = 0; i < width - 1; i++) { flow[i] = new float2(1, 0); dist[i] = (width - 1) - i; }
            flow[width - 1] = float2.zero; dist[width - 1] = 0;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(width, 1),
                goalCell = new int2(width - 1, 0),
                tileSize = tileSize, version = 1,
            });
        }

        private Entity CreateUnit(float3 pos, float speed)
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, LocalTransform.FromPosition(pos));
            _em.AddComponentData(e, new PathFollowState { speed = speed });
            return e;
        }

        private void Tick(float dt)
        {
            _world.SetTime(new TimeData(_world.Time.ElapsedTime + dt, dt));
            _simGroup.Update();
        }

        [Test]
        public void Moves_Along_Flow_At_Configured_Speed()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 2f);

            Tick(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(2f, pos.x, 1e-4f, "2 units at speed 2 in 1s along +x flow");
            Assert.AreEqual(0f, pos.z, 1e-4f, "no drift on z");
            Assert.IsFalse(_em.HasComponent<PastGoalTag>(e));
        }

        [Test]
        public void Adds_PastGoalTag_When_Cell_Matches_GoalCell()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(4f, 0f, 0f), speed: 1f);

            Tick(0.1f);

            Assert.IsTrue(_em.HasComponent<PastGoalTag>(e),
                "MovementSystem must tag unit when WorldToCell result equals goalCell");
        }

        [Test]
        public void Does_Not_Move_On_Unreachable_Cell()
        {
            // Zero-flow cell (e.g., isolated by obstacle). Unit should stay put.
            var flow = new NativeArray<float2>(1, Allocator.Persistent);
            var dist = new NativeArray<int>(1, Allocator.Persistent);
            flow[0] = float2.zero; dist[0] = int.MaxValue;

            _fieldEntity = _em.CreateEntity();
            _em.AddComponentData(_fieldEntity, new FlowFieldSingleton
            {
                flow = flow, dist = dist,
                gridSize = new int2(1, 1),
                goalCell = new int2(99, 99), // different from (0,0)
                tileSize = 1f, version = 1,
            });

            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 5f);
            Tick(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(0f, pos.x, 1e-4f, "zero flow must produce zero movement");
        }

        [Test]
        public void SlowEffect_Halves_Flow_Step()
        {
            CreateLinearFlowField();
            var e = CreateUnit(new float3(0f, 0f, 0f), speed: 2f);
            _em.AddComponentData(e, new SlowEffect { remaining = 5f, multiplier = 0.5f });

            Tick(1f);

            var pos = _em.GetComponentData<LocalTransform>(e).Position;
            Assert.AreEqual(1f, pos.x, 1e-4f, "SlowEffect 0.5 × speed 2 × 1s = 1.0");
        }
    }
}
```

**Step 2: `EffectIntegrationTests.Movement_Applies_SlowEffect_Multiplier_To_Step` 메서드만 migration**

기존 메서드 (line 18~48) 전체를 아래로 교체:

```csharp
[Test]
public void Movement_Applies_SlowEffect_Multiplier_To_Step()
{
    using var world = new World("EffectIntegrationTests_Movement");
    var em = world.EntityManager;
    var simGroup = world.CreateSystemManaged<SimulationSystemGroup>();
    simGroup.AddSystemToUpdateList(world.CreateSystem<MovementSystem>());

    // 2-cell +x flow field. goal = (1,0).
    var flow = new NativeArray<float2>(2, Allocator.Persistent);
    var dist = new NativeArray<int>(2, Allocator.Persistent);
    flow[0] = new float2(1, 0); dist[0] = 1;
    flow[1] = float2.zero;      dist[1] = 0;
    var fieldEntity = em.CreateEntity();
    em.AddComponentData(fieldEntity, new FlowFieldSingleton
    {
        flow = flow, dist = dist,
        gridSize = new int2(2, 1),
        goalCell = new int2(1, 0),
        tileSize = 1f, version = 1,
    });

    var e = em.CreateEntity();
    em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
    em.AddComponentData(e, new PathFollowState { speed = 2f });
    em.AddComponentData(e, new SlowEffect { remaining = 5f, multiplier = 0.5f });

    world.SetTime(new TimeData(world.Time.ElapsedTime + 1f, 1f));
    simGroup.Update();

    var pos = em.GetComponentData<LocalTransform>(e).Position;
    Assert.AreEqual(1f, pos.x, 1e-4f, "SlowEffect 0.5 should halve this frame's step.");
    Assert.AreEqual(2f, em.GetComponentData<PathFollowState>(e).speed, 1e-5f,
        "Base speed field stays unchanged — Movement still owns it.");

    // Persistent NativeArray dispose (world.Dispose 가 entity 는 파괴하지만 NativeArray 는 leak).
    em.GetComponentData<FlowFieldSingleton>(fieldEntity).Dispose();
}
```

**Step 3: `UnitLifecycleSystemTests.CreateUnitAtGoal` 헬퍼 migration**

기존 헬퍼 (line 45~55) 교체. `PastGoalTag` 를 직접 부여하는 패턴은 유지 — flow field 설정 불필요 (PastGoalTag 가 MovementSystem 을 우회하여 UnitLifecycleSystem 직행):

```csharp
private Entity CreateUnitAtGoal()
{
    var e = _em.CreateEntity();
    _em.AddComponentData(e, LocalTransform.FromPosition(new float3(0f, 0f, 0f)));
    _em.AddComponent<AttackUnitTag>(e);
    _em.AddComponent<PastGoalTag>(e);
    // Phase 9: PathFollowState 축소. PathWaypoint DynamicBuffer 제거.
    // PastGoalTag 이미 있으므로 MovementSystem 의 .WithNone<PastGoalTag>() 에 의해 필터됨.
    _em.AddComponentData(e, new PathFollowState { speed = 1f });
    return e;
}
```

또한 `SetUp` 에서 MovementSystem 이 `state.RequireForUpdate<FlowFieldSingleton>()` 을 거는 것 때문에 FlowFieldSingleton 이 없으면 MovementSystem 은 skip — UnitLifecycleSystem 만 돌아 정상 동작. 추가 세팅 불필요.

**Step 4: `MovementSystem.cs` 재작성**

```csharp
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Effects;

namespace Wassup.Battle.Movement
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PathFollowState>();
            state.RequireForUpdate<FlowFieldSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            float dt = SystemAPI.Time.DeltaTime;

            var field = SystemAPI.GetSingleton<FlowFieldSingleton>();
            var slowLookup = SystemAPI.GetComponentLookup<SlowEffect>(isReadOnly: true);

            var portalQuery = SystemAPI.QueryBuilder().WithAll<PortalLink>().Build();
            var portals = portalQuery.ToComponentDataArray<PortalLink>(Allocator.Temp);

            var tornadoQuery = SystemAPI.QueryBuilder().WithAll<TornadoField>().Build();
            var tornadoFields = tornadoQuery.ToComponentDataArray<TornadoField>(Allocator.Temp);

            foreach (var (transform, follow, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRO<PathFollowState>>()
                              .WithNone<PastGoalTag>()
                              .WithEntityAccess())
            {
                float3 current = transform.ValueRO.Position;

                // 1. Portal entry: 내부에 있으면 exit 으로 텔레포트. exitWaypointIndex 제거됨 —
                //    다음 프레임 flow field 가 알아서 방향 공급.
                for (int p = 0; p < portals.Length; p++)
                {
                    var portal = portals[p];
                    float pdx = current.x - portal.entryWorld.x;
                    float pdz = current.z - portal.entryWorld.z;
                    if (pdx * pdx + pdz * pdz <= portal.entryRadius * portal.entryRadius)
                    {
                        transform.ValueRW.Position = new float3(portal.exitWorld.x, current.y, portal.exitWorld.z);
                        current = transform.ValueRW.Position;
                        break;
                    }
                }

                // 2. Current cell lookup + goal 판정
                int2 cell = GridMath.WorldToCell(current, field.tileSize, field.gridSize);
                if (cell.x == field.goalCell.x && cell.y == field.goalCell.y)
                {
                    ecb.AddComponent<PastGoalTag>(entity);
                    continue;
                }

                // 3. Tornado field: pull override (Phase 8 §17 유지).
                bool pulled = false;
                for (int t = 0; t < tornadoFields.Length; t++)
                {
                    var fieldT = tornadoFields[t];
                    float fdx = current.x - fieldT.centerWorld.x;
                    float fdz = current.z - fieldT.centerWorld.z;
                    if (fdx * fdx + fdz * fdz > fieldT.radius * fieldT.radius) continue;
                    float3 toCenter = fieldT.centerWorld - current;
                    toCenter.y = 0f;
                    float centerDist = math.length(toCenter);
                    float pullStep = fieldT.pullSpeed * dt;
                    transform.ValueRW.Position = (centerDist <= pullStep || centerDist < 1e-4f)
                        ? new float3(fieldT.centerWorld.x, current.y, fieldT.centerWorld.z)
                        : current + math.normalize(toCenter) * pullStep;
                    pulled = true;
                    break;
                }
                if (pulled) continue;

                // 4. Flow field step
                int idx = GridMath.CellIndex(cell, field.gridSize);
                float2 dir = field.flow[idx];
                if (math.lengthsq(dir) < 1e-6f) continue; // unreachable: 제자리 유지

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
}
```

**Step 5: 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors. 예상 warning:
- `MapData.Paths` Obsolete warning (BattleBridge.SpawnUnit 에서 아직 참조 — Task 8B 에서 제거)
- `PathFollowState.tileSize` / `PathFollowState.currentWaypointIndex` 는 **값 0 으로 할당만 되고 읽지 않는 상태** (새 MovementSystem 이 무시). Warning 없음

**Step 6: 테스트 실행**

Tool: `mcp__UnityMCP__run_tests` (EditMode 전체)
Expected:
- `MovementSystemTests` 4 개 전부 PASS (신규 flow-field 기반)
- `EffectIntegrationTests.Movement_Applies_SlowEffect_Multiplier_To_Step` PASS
- `UnitLifecycleSystemTests` 3개 전부 PASS
- 기존 다른 테스트 (`FlowFieldBuilderTests`, `GridMathTests`, `EffectTickSystemTests`, `CostRuntimeTests`, `SkillLoadoutControllerTests`, `DraftSessionTests`, `ProjectileSystemTests`) PASS

**Step 7: PlayMode smoke test**

- Play 진입 → 적이 spawn 에서 goal 까지 flow field 로 도달
- Portal 통과 시 자율 복귀
- Tornado 해제 시 자율 복귀
- 아직 `PathFollowState.tileSize` 가 `BattleBridge.SpawnUnit` 에서 `tileSize` 로 채워지고 있지만 MovementSystem 이 무시하므로 동작 영향 없음

**Step 8: Commit**

```bash
git add Assets/_Project/Tests/EditMode/MovementSystemTests.cs \
        Assets/_Project/Tests/EditMode/EffectIntegrationTests.cs \
        Assets/_Project/Tests/EditMode/UnitLifecycleSystemTests.cs \
        Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs
git commit -m "feat(phase9): MovementSystem flow-field 재작성 + EditMode 테스트 3개 migration (P9-05 part 1 / Task 8A)"
```

---

### Task 8B: PathFollowState 축소 + BattleBridge 적 스폰 migration

**Files:**
- Modify: `Assets/_Project/Scripts/Battle/Movement/PathFollowState.cs` (legacy 필드 제거)
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 경로)

**Compile-safety 핵심**:
- 새 MovementSystem (8A 에서 배포) 은 `PathFollowState.speed` 만 읽으므로 legacy 필드 제거해도 안전
- BattleBridge 의 `tileSize = tileSize` 할당이 컴파일 에러의 원인이 됨 → 같은 커밋에서 제거
- BattleBridge 의 `AddBuffer<PathWaypoint>` 는 PathWaypoint 타입이 아직 존재하므로 컴파일 OK, 다만 실행되지 않도록 교체
- 테스트들은 8A 에서 이미 `new PathFollowState { speed = ... }` 로만 초기화 (legacy 필드 미참조) — 재빌드 통과

**Step 1: `PathFollowState.cs` 축소**

```csharp
using Unity.Entities;

namespace Wassup.Battle.Movement
{
    public struct PathFollowState : IComponentData
    {
        public float speed;
        // Phase 9: currentWaypointIndex 제거 — flow field 가 대체
        // Phase 9: tileSize 제거 — FlowFieldSingleton.tileSize 가 단일 소스
    }
}
```

**Step 2: `BattleBridge` 적 스폰 경로 migration**

Target: `BattleBridge.cs` ~line 1146~1204 의 SpawnUnit (또는 적 스폰 메서드) 내부.

Before:
```csharp
PathDefinition path = null;
foreach (var p in map.Paths) if (p.id == entry.pathId) { path = p; break; }
if (path == null || path.waypoints.Count == 0)
{
    Debug.LogWarning($"[BattleBridge] Path '{entry.pathId}' not found or empty in MapData.");
    return;
}
// ... entity 생성 ...
var follow = new PathFollowState {
    speed = entry.unitType.moveSpeed,
    tileSize = tileSize,           // ← tileSize 필드 제거로 컴파일 에러 발생할 줄
};
_em.AddComponentData(entity, follow);
var buffer = _em.AddBuffer<PathWaypoint>(entity);
foreach (var wp in path.waypoints)
    buffer.Add(new PathWaypoint { cell = new int2(wp.x, wp.y) });
// spawn 위치도 path.waypoints[0] 사용 중일 가능성
```

After:
```csharp
// Phase 9: AttackDeck.SpawnEntry.pathId 는 남아있지만 무시. MapData.SpawnCells[0] 사용.
// Phase 10 에서 pathId → spawnTileIndex migration + multi-spawn 지원.
if (map.SpawnCells == null || map.SpawnCells.Count == 0)
{
    Debug.LogWarning("[BattleBridge] MapData.SpawnCells empty — cannot spawn attacker");
    return;
}
var spawnCell = map.SpawnCells[0];
var spawnWorldPos = GridToWorldCenter(spawnCell);
// ... entity 생성 시 LocalTransform.FromPosition(spawnWorldPos) 사용 ...
_em.AddComponentData(entity, new PathFollowState { speed = entry.unitType.moveSpeed });
// PathWaypoint DynamicBuffer 추가 제거 (이번 Step에서 `AddBuffer<PathWaypoint>` 호출 삭제)
```

**Step 3: 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors.

예상 warning:
- `MapData.Paths` Obsolete warning 사라짐 (BattleBridge 에서 마지막 참조 제거됨)

**Step 4: 테스트 실행**

Tool: `mcp__UnityMCP__run_tests` (EditMode 전체)
Expected: 전부 PASS (8A 완료 후 상태 그대로).

**Step 5: PlayMode smoke test**

- Play 진입 → 적이 `spawnCells[0]` 에서 스폰 되고 flow field 로 goal 이동
- PathWaypoint DynamicBuffer 가 attacker entity 에 **더이상 추가되지 않음** (Entity Inspector 확인)

**Step 6: Grep 확인 — PathWaypoint 참조 잔존 여부**

```bash
grep -rn "PathWaypoint" Assets/_Project/Scripts/
```
Expected: `Assets/_Project/Scripts/Battle/Movement/PathWaypoint.cs` 파일 자신의 정의만. 호출부/참조 0.

```bash
grep -rn "currentWaypointIndex\|\.tileSize\s*=" Assets/_Project/Scripts/
```
Expected: 0 hits for `currentWaypointIndex`. `\.tileSize =` 는 `BattleBridge.tileSize` 필드 자체 할당만 존재해야 함.

**Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Battle/Movement/PathFollowState.cs \
        Assets/_Project/Scripts/Bridge/BattleBridge.cs
git commit -m "refactor(phase9): PathFollowState legacy 필드 제거 + BattleBridge spawn → SpawnCells[0] (P9-05 part 2 / Task 8B)"
```

---

### Task 8C: PathWaypoint 파일 삭제

**Files:**
- Delete: `Assets/_Project/Scripts/Battle/Movement/PathWaypoint.cs`
- Delete: `Assets/_Project/Scripts/Battle/Movement/PathWaypoint.cs.meta`

**Compile-safety**:
- 8B 완료 시점에 PathWaypoint 에 대한 참조가 코드베이스 어디에도 없음 (8B Step 6 grep 으로 확인)
- 파일 삭제만으로 컴파일 깨질 일 없음

**Step 1: 파일 삭제**

```bash
git rm Assets/_Project/Scripts/Battle/Movement/PathWaypoint.cs \
       Assets/_Project/Scripts/Battle/Movement/PathWaypoint.cs.meta
```

**Step 2: Unity 에디터 asset 재임포트**

Tool: `mcp__UnityMCP__refresh_unity`
Expected: asset database 가 삭제 반영.

**Step 3: 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors.

**Step 4: 테스트 전수 실행**

Tool: `mcp__UnityMCP__run_tests` (EditMode 전체)
Expected: 전부 PASS.

**Step 5: Commit**

```bash
git commit -m "chore(phase9): PathWaypoint.cs 삭제 — Phase 9 waypoint cleanup 완료 (P9-09 / Task 8C)"
```

---

## Task 9: tileSize 단일 소스화 + MapView Path LineRenderer 제거 (P9-07 + P9-10)

**Files:**
- Modify: `Assets/_Project/Scripts/Core/MapView.cs`
- Modify: `Assets/_Project/Scripts/Core/PlacementInput.cs`
- Modify: `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`Initialize(map, tileSize)` 주입)

**Step 1: MapView 수정**

- `[SerializeField] private float tileSize = 1f;` 제거, `public void Initialize(MapData map, float tileSize)` 메서드 신설
- `Start()` 에서 map/tileSize 가 Initialize 로 전달되지 않았다면 SerializeField fallback 대신 `Debug.LogError`
- `BuildPathLines` 메서드 + `Start` 내부 호출 삭제
- `_lineMaterial` 필드 + OnDestroy 의 line material 해제 코드 제거
- 기존 cube primitive 생성 로직 유지

Simplified `MapView.cs` skeleton after change:

```csharp
public class MapView : MonoBehaviour
{
    private MapData _map;
    private float _tileSize = 1f;

    private readonly Dictionary<TileType, Material> _tileMaterials = new();
    private readonly Dictionary<Vector2Int, Renderer> _buildableRenderers = new();
    private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();

    public void Initialize(MapData map, float tileSize)
    {
        _map = map;
        _tileSize = tileSize;
        BuildSharedMaterials();
        BuildTiles();
        // BuildPathLines(): removed in Phase 9
    }

    private void Start()
    {
        if (_map == null)
        {
            Debug.LogError("[MapView] Initialize(map, tileSize) must be called before Start.");
        }
    }
    // ... (기존 BuildTiles, FlashTileReject, Flash coroutine 유지. BuildPathLines/_lineMaterial 전량 삭제.)
}
```

**Step 2: PlacementInput 수정**

- `[SerializeField] private float tileSize` 제거
- `public void Initialize(float tileSize)` 메서드 신설
- rounding 로직은 유지 (Phase 10 에서 GeneratedMap 기반으로 Init 수정 예정)

**Step 3: BattleBridge 주입 호출 추가**

판 초기화 메서드 (또는 `Awake` / `Start`) 안에서:

```csharp
if (mapView != null) mapView.Initialize(map, tileSize);
if (placementInput != null) placementInput.Initialize(tileSize);
```

**Step 4: 컴파일 확인**

Tool: `mcp__UnityMCP__read_console`
Expected: 0 errors. `[Obsolete] MapData.Paths` 경고가 BuildPathLines 제거로 사라짐.

**Step 5: Scene 의 MapView / PlacementInput 에서 SerializeField 제거 결과 Inspector 재확인**

- 기존 `tileSize` slot 이 사라짐
- 에디터에서 해당 값이 null 이 아니면 재직렬화 경고 발생 가능 → `[FormerlySerializedAs]` 필요 시 추가 또는 prefab 수정

**Step 6: PlayMode smoke test**

- Play 진입 → tile cube 생성, Path LineRenderer 없음, placement 정상 동작

**Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Core/MapView.cs \
        Assets/_Project/Scripts/Core/PlacementInput.cs \
        Assets/_Project/Scripts/Bridge/BattleBridge.cs
git commit -m "refactor(phase9): tileSize 단일 소스화 (BattleBridge → MapView/PlacementInput 주입) + MapView.BuildPathLines 제거 (P9-07, P9-10)"
```

---

## Task 10: PlayMode 회귀 + 기준선 비교 (P9-12)

**Files:** 없음 (수동 검증)

**Step 1: Phase 9 후 재녹화 — Task 0 과 동일한 3 케이스**

- 케이스 A: Portal 3가지 exit 위치에서 동선 기록
- 케이스 B: Tornado 해제 후 동선 기록
- 케이스 C: 평상시 start → goal 진행

**Step 2: Task 0 기준선과 비교**

- **기대 결과**:
  - 케이스 A: exit 타일이 어디든 다음 프레임 flow field lookup 으로 goal 방향 즉시 공급 → 역주행 없음
  - 케이스 B: Tornado 해제 직후 현재 cell 의 flow 방향으로 자율 복귀 (기계적 직선 없음)
  - 케이스 C: waypoint → flow field 교체 후에도 직선 goal 도달 (회귀 없음)

**Step 3: 잔존 버그 / 이상 동선이 있으면 이슈 문서화**

- `docs/residual-issues.md` 에 Phase 9 추가 검증 이슈 기재
- 심각하면 Task 8 까지 되돌아가 재작업

**Step 4: 통과 시 기록**

```bash
mkdir -p docs/plans/recordings/phase9-postflow
# 녹화 파일 저장
git add docs/plans/recordings/phase9-postflow/
git commit -m "chore(phase9): Phase 9 후 재녹화 — 기준선 비교 완료 (P9-12)"
```

**Step 5: PHASE9.md 구현 종료 스펙 작성**

Create `docs/PHASE9.md` 따라가기: Phase 8 형식 참고. Section:
- 1 목표, 2 확정 결정, 3 구현 결과 (체크박스 P9-01~P9-12), 4 TRD 금지 패턴 준수 확인, 5 종료 조건, 6 회귀 결과

```bash
git add docs/PHASE9.md
git commit -m "docs(phase9): PHASE9.md 구현 종료 스펙 작성"
```

**Step 6: Phase 9 종료 프로토콜**

- `docs/residual-issues.md` 재확인 — Phase 9 미체크 항목 사용자 처리 결정 질의
- 사용자 승인 후 Phase 9 clone + Phase 10 브레인스토밍 착수 (`docs/phase10-prep.md` 기반)

---

## 작업 순서 요약

| Task | 설계 문서 번호 | 성격 | 테스트 |
|---|---|---|---|
| 0 | P9-11 | 기준선 녹화 (사용자) | — |
| 1 | P9-01 | MapData 필드 + asset | — |
| 2 | P9-02 | Pure helper | EditMode ×6 |
| 3 | P9-08 | VFX 사이트 통일 | smoke |
| 4 | P9-03 data | IComponentData struct | — |
| 5 | P9-04 | BFS builder | EditMode ×3 (try/finally dispose) |
| 6 | P9-03 wiring | Singleton 수명 (idempotent) | smoke |
| 7 | P9-06 | PortalLink migration | compile |
| **8A** | P9-05 part 1 | MovementSystem rewrite + 테스트 3개 migration | EditMode ×4 new + 기존 통과 |
| **8B** | P9-05 part 2 | PathFollowState 축소 + BattleBridge spawn migration | EditMode 전부 통과 |
| **8C** | P9-09 | PathWaypoint 파일 삭제 | EditMode 전부 통과 |
| 9 | P9-07 + P9-10 | 주변 정리 | smoke |
| 10 | P9-12 | 회귀 + PHASE9.md | 기준선 비교 |

**Task 8 분할 근거 (Codex 2차 리뷰 HIGH #2/#3/#4)**:
- 원 Task 8 = MovementSystem rewrite + PathFollowState 축소 + PathWaypoint 삭제 + BattleBridge spawn migration (30~60분, bite-sized 상한 초과)
- 기존 EditMode 테스트 3개 (MovementSystemTests / EffectIntegrationTests / UnitLifecycleSystemTests) 가 PathWaypoint / currentWaypointIndex / tileSize 참조 → 원 Task 8 이 컴파일 깨뜨림
- 8A → 8B → 8C 순차로 **매 commit 컴파일 + 기존 테스트 통과** 가 불변조건이 됨
- 8A 가 flow-field 기반 EditMode 통합 테스트를 자연스럽게 포함 → Codex HIGH #3 (MovementSystem 통합 테스트 부재) 해소

---

## 참조 문서

- 설계: `docs/plans/2026-04-19-phase9-flow-field-design.md`
- Phase 9 체크리스트: `docs/phase9-prep.md`
- Phase 10 이관 스펙: `docs/phase10-prep.md`
- ECS / TRD 제약: `docs/TRD.md`
- 최근 Phase: `docs/PHASE8.md`

---

**작성**: 2026-04-19 (rev1: brainstorming + Codex 1차 리뷰)  
**업데이트**: 2026-04-20 (rev2: Codex 2차 리뷰 APPROVED WITH FIXES — CRITICAL #1 BuildFlowField 멱등성 / HIGH #2 테스트 migration / HIGH #3 MovementSystem 통합 테스트 / HIGH #4 Task 8 → 8A/8B/8C 분할 / HIGH #5 NativeArray try/finally 반영)
