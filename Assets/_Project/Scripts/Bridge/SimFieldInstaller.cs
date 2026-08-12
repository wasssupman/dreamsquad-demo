using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Effects;
using Wassup.Data;

namespace Wassup.Bridge
{
    // continuous-agent-movement unit 0 — 판 단위 sim 필드 3종의 할당·해제.
    // BattleBridge 에서 추출(동작 불변). 세 필드를 함께 옮긴 이유는 라이프사이클 공유가
    // 이미 명시적 계약이기 때문 — goal field / defender field / pickup spawn state 는
    // 맵 빌드에서 같이 서고 판 정리에서 같이 죽는다. 나눠 두면 그 계약이 두 파일에 걸친다.
    //
    // ⚠ 이 struct 는 Persistent NativeArray 를 소유한 컴포넌트를 가리킨다. Teardown 없이
    // 핸들을 버리면 곧바로 누수다 (BuildFlowField 의 "CRITICAL #1" 주석 참조).
    public struct SimFieldHandles
    {
        public Entity flowField;
        public Entity defenderField;
        public Entity pickupSpawnState;

        public void Reset()
        {
            flowField = Entity.Null;
            defenderField = Entity.Null;
            pickupSpawnState = Entity.Null;
        }
    }

    // MonoBehaviour 가 아니다 — BattleBridge 만이 호출하는 plain static helper.
    // 제약 1(ECS 경계)은 "그 외 MonoBehaviour 에서 EntityManager 직접 호출 금지"이며,
    // MonoBehaviour ↔ ECS 창구는 여전히 BattleBridge 하나다.
    public static class SimFieldInstaller
    {
        // instinct-content unit 3 — 거점 목적지의 BFS 소스 = footprint 전체(중심 3×3).
        // 경계·통행 필터는 빌더 소관이라 여기서 복제하지 않는다.
        private static void FillFootprint(int2 center, NativeArray<int2> outCells)
        {
            int half = Wassup.Data.StructurePlacements.InstinctFootprint / 2;
            int k = 0;
            for (int dy = -half; dy <= half; dy++)
                for (int dx = -half; dx <= half; dx++)
                    outCells[k++] = new int2(center.x + dx, center.y + dy);
        }

        // 호출 전 Teardown 이 선행되어야 한다(멱등성 계약은 호출부에 남겨 순서가 보이게 한다).
        // CRITICAL #1 (Codex 2차 리뷰): AddComponentData 는 component 존재 시 throw,
        // 그리고 기존 arrays 가 dispose 없이 덮어써지면 누수.
        // waypoint-routing unit 1 — `slotMasks` 와 map waypoint 목적지의 곱으로 슬롯을 굽는다.
        // DefaultMask 를 항상 첫 마스크로 정규화해 슬롯 0 = (골, DefaultMask)를 고정한다.
        // 미생성이면 슬롯 1개(`TraversalSlots.DefaultMask`)로 떨어져 현행과 byte 동일하다.
        public static void InstallNavFields(
            EntityManager em,
            in GeneratedMap map,
            float tileSize,
            float3 origin,
            ref SimFieldHandles handles,
            NativeArray<byte> slotMasks = default)
        {
            int w = map.gridSize.x;
            int h = map.gridSize.y;
            int n = w * h;

            // continuous-agent-movement unit 1 — 정적 walk 마스크는 Persistent 1개만 만든다.
            // (이전: Temp `walk` + DefenderField 용 Persistent 사본, 총 2개.)
            // 실패 시 소유권 이관 전이므로 catch 에서 직접 dispose 한다.
            var walk = new NativeArray<byte>(n, Allocator.Persistent);
            // traversal-layers unit 0 — 셀 층 비트를 sim 으로 넘긴다. walk 와 수명·소유권이 같다.
            var cellLayers = new NativeArray<byte>(n, Allocator.Persistent);
            bool walkOwnedBySingleton = false;   // AddComponentData 성공 시점에 소유권 이관
            try
            {
                for (int i = 0; i < n; i++)
                {
                    walk[i] = (byte)(map.tiles[i] == MapTileType.Walk ? 1 : 0);
                    // traversal-layers unit 1b 회귀 수리 — **저작된 `placeMask` 를 읽지 않는다.**
                    // 통행 층은 `tiles` 에서만 파생한다.
                    //
                    // 처음엔 `placeMask` 를 정본으로 삼았다(rev 2 계약 1: "셀 층 한 벌을
                    // 배치·통행이 공유"). **틀렸다.** `placeMask` 의 저작 의미는 «칸의 종류»가
                    // 아니라 **«어느 유닛이 여기 설 수 있나»** 다. 실측(MapDocument_Test):
                    // `Walk` 칸 23개에 마스크 0 — 저작자가 "여기 배치 금지"를 칠한 것이고,
                    // "여기 통행 금지"를 뜻하지 않는다. 그걸 통행으로 읽으면 그 맵에서
                    // **통로 23칸이 라우팅에서 사라지고** 데코 7칸이 새 통로가 됐다.
                    //
                    // 더 나쁜 건 그 다음이다 — 라우팅은 `cellLayers`, 벽 충돌(`NavGrid`)은
                    // `walkMask` 를 보므로 둘이 갈리면 **필드가 벽 안쪽을 가리키고 trim 이
                    // 막는다**. "적이 통로에서 안 움직인다"로 나타나고 통행층과 무관한 이동
                    // 버그로 오진된다.
                    //
                    // 파생만 쓰면 `cellLayers` 는 `walkMask`(1비트)의 **N비트 일반화**가 되어
                    // 정의가 갈릴 수 없다. 물타일이 오면 `MapTileType.Water` → `Derive` 에
                    // case 한 줄이고, 통행을 **저작**으로 나누고 싶어지면 그때 별도 배열을
                    // 만든다(그때는 실제 예가 있을 것이다 — 지금은 없다).
                    cellLayers[i] = PlacementLayers.Derive(map.tiles[i]);
                }

                // 목적지 0은 골 센티널, 이후는 저작 순서대로 중복 제거한 웨이포인트 셀.
                var destinations = new System.Collections.Generic.List<int2>
                {
                    FlowFieldSingleton.GoalSentinel,
                };
                if (map.waypointCells.IsCreated)
                    for (int i = 0; i < map.waypointCells.Length; i++)
                    {
                        int2 candidate = map.waypointCells[i];
                        if (!destinations.Contains(candidate)) destinations.Add(candidate);
                    }

                // instinct-content unit 3 — 방어 본능도 목적지다. 적이 마음으로 직행하는 대신
                // 가까운 본능을 먼저 고를 수 있으려면 그 셀로 흐르는 슬롯이 있어야 한다.
                // 마음은 이미 골 센티널이므로 여기 넣지 않는다.
                var structureDestinations = new System.Collections.Generic.List<int2>();
                if (map.structures.IsCreated)
                    for (int i = 0; i < map.structures.Length; i++)
                    {
                        var st = map.structures[i];
                        if (st.faction != Wassup.Battle.Units.Faction.DefenderInstinct) continue;
                        if (destinations.Contains(st.cell)) continue;
                        destinations.Add(st.cell);
                        structureDestinations.Add(st.cell);
                    }

                // DefaultMask 를 첫 슬롯에 고정하고 나머지는 호출자 순서를 보존해 중복 제거.
                var masks = new System.Collections.Generic.List<byte> { TraversalSlots.DefaultMask };
                if (slotMasks.IsCreated)
                    for (int i = 0; i < slotMasks.Length; i++)
                    {
                        byte candidate = slotMasks[i] != 0
                            ? slotMasks[i]
                            : TraversalSlots.DefaultMask;
                        if (!masks.Contains(candidate)) masks.Add(candidate);
                    }

                int maskCount = masks.Count;
                int slotCount = destinations.Count * maskCount;
                NativeArray<float2> flow = default;
                NativeArray<int> dist = default;
                NativeArray<byte> maskValues = default;
                NativeArray<int2> destCells = default;
                NativeArray<int2> waypointCells = default;
                NativeArray<int2> waypointRanges = default;
                NativeArray<int2> goalsField = default;
                try
                {
                    // 연속 Persistent 할당은 첫 할당부터 catch 범위 안에 둔다. 중간 할당 실패가
                    // 앞선 배열을 고아로 남기면 판 재시작마다 native leak 이 누적된다.
                    flow = new NativeArray<float2>(slotCount * n, Allocator.Persistent);
                    dist = new NativeArray<int>(slotCount * n, Allocator.Persistent);
                    maskValues = new NativeArray<byte>(slotCount, Allocator.Persistent);
                    destCells = new NativeArray<int2>(slotCount, Allocator.Persistent);
                    if (map.waypointCells.IsCreated)
                    {
                        waypointCells = new NativeArray<int2>(map.waypointCells.Length, Allocator.Persistent);
                        waypointCells.CopyFrom(map.waypointCells);
                    }
                    if (map.waypointRanges.IsCreated)
                    {
                        waypointRanges = new NativeArray<int2>(map.waypointRanges.Length, Allocator.Persistent);
                        waypointRanges.CopyFrom(map.waypointRanges);
                    }
                    for (int destinationIndex = 0; destinationIndex < destinations.Count; destinationIndex++)
                    for (int maskIndex = 0; maskIndex < maskCount; maskIndex++)
                    {
                        int slot = destinationIndex * maskCount + maskIndex;
                        maskValues[slot] = masks[maskIndex];
                        destCells[slot] = destinations[destinationIndex];
                    }

                    var gridSize = map.gridSize;
                    var goal = map.goal;   // primary = goals[0] (FlowFieldSingleton.goalCell·폴백)

                    // multi-goal-map 유닛 1·2 — 골 집합을 Persistent 로 만들어 (a) N-소스 BFS 소스
                    // (최근접-골 라우팅) (b) FlowFieldSingleton.goals 저장(IsGoalCell 멤버십). goals
                    // 미초기화/빈 생산자(라이브 폴백 BuildFallbackLinear·legacy)는 [goal] 로 폴백.
                    // 성공 시 goalsField 소유권은 싱글턴으로 이관 → Teardown 이 dispose.
                    bool hasGoals = map.goals.IsCreated && map.goals.Length > 0;
                    goalsField = new NativeArray<int2>(hasGoals ? map.goals.Length : 1, Allocator.Persistent);
                    if (hasGoals) goalsField.CopyFrom(map.goals);
                    else goalsField[0] = goal;

                    // traversal-layers unit 1b — 슬롯마다 «셀 층 ∩ 슬롯 마스크» 로 굽는다.
                    // 슬롯이 DefaultMask(Path) 하나면 walk(= tiles==Walk)와 같은 집합이라
                    // 결과가 현행과 바이트 동일하다(회귀 축).
                    NativeArray<byte> slotWalk = default;
                    NativeArray<int2> waypointSource = default;
                    NativeArray<int2> footprintSource = default;
                    try
                    {
                        slotWalk = new NativeArray<byte>(n, Allocator.Temp);
                        waypointSource = new NativeArray<int2>(1, Allocator.Temp);
                        footprintSource = new NativeArray<int2>(
                            Wassup.Data.StructurePlacements.InstinctFootprint
                            * Wassup.Data.StructurePlacements.InstinctFootprint, Allocator.Temp);
                        for (int slot = 0; slot < slotCount; slot++)
                        {
                            TraversalSlots.FillWalkMask(in cellLayers, maskValues[slot], slotWalk);
                            int2 destination = destCells[slot];
                            NativeArray<int2> sources;
                            if (destination.Equals(FlowFieldSingleton.GoalSentinel))
                            {
                                sources = goalsField;
                            }
                            else if (structureDestinations.Contains(destination))
                            {
                                // instinct-content unit 3 — 거점 목적지의 소스는 **footprint 전체**다.
                                // 중심 1칸으로 쓰면 안 된다: Coil 의 본능 중심 (10,6) 은 Place 타일
                                // 이라 BuildFromSources 가 그 소스를 버리고 슬롯이 통째로 빈 필드가
                                // 된다(Duel 은 9/9 가 Walk 라 이 함정을 혼자서는 못 잡는다).
                                //
                                // 통행 교집합을 여기서 다시 거르지 않는다 — 빌더가 이미 경계·통행
                                // 으로 소스를 거르고, 유효 소스 0 이면 전 셀 int.MaxValue 로 두어
                                // «못 가는 건물» 을 골 폴백 신호로 표현한다(그 규약을 복제하지 않는다).
                                //
                                // 다중 소스라 적은 중심이 아니라 **가장 가까운 벽면**에 도착한다.
                                FillFootprint(destination, footprintSource);
                                sources = footprintSource;
                            }
                            else
                            {
                                waypointSource[0] = destination;
                                sources = waypointSource;
                            }
                            FlowFieldBuilder.BuildFromSources(slotWalk, gridSize, sources,
                                flow.GetSubArray(slot * n, n), dist.GetSubArray(slot * n, n));
                        }
                    }
                    finally
                    {
                        if (footprintSource.IsCreated) footprintSource.Dispose();
                        if (waypointSource.IsCreated) waypointSource.Dispose();
                        if (slotWalk.IsCreated) slotWalk.Dispose();
                    }

                    var data = new FlowFieldSingleton
                    {
                        flow = flow,
                        dist = dist,
                        walkMask = walk,
                        cellLayers = cellLayers,
                        maskValues = maskValues,
                        destCells = destCells,
                        waypointCells = waypointCells,
                        waypointRanges = waypointRanges,
                        gridSize = gridSize,
                        goalCell = goal,
                        goals = goalsField,
                        tileSize = tileSize,
                        origin = origin,
                        version = map.generatorVersion,
                    };

                    handles.flowField = em.CreateEntity();
                    em.AddComponentData(handles.flowField, data);
                    walkOwnedBySingleton = true;   // 이후 dispose 책임은 FlowFieldSingleton.Dispose
                    Debug.Log($"[SimFieldInstaller] FlowField built — boardOrigin={origin} tileSize={tileSize} grid={gridSize}");
                }
                catch
                {
                    if (flow.IsCreated) flow.Dispose();
                    if (dist.IsCreated) dist.Dispose();
                    if (maskValues.IsCreated) maskValues.Dispose();
                    if (destCells.IsCreated) destCells.Dispose();
                    if (waypointCells.IsCreated) waypointCells.Dispose();
                    if (waypointRanges.IsCreated) waypointRanges.Dispose();
                    if (goalsField.IsCreated) goalsField.Dispose();   // 싱글턴 이관 전 실패 시만
                    throw;
                }

                // boss-defender-field unit 1 — 방어유닛-지향 필드 싱글톤.
                // continuous-agent-movement unit 1 — walkMask 사본을 더 만들지 않는다.
                // 정적 벽은 FlowFieldSingleton 이 단독 소유하고 DefenderFieldSystem 이 읽는다.
                // flow/dist 는 초기 "소스 0" 상태(dist=MaxValue) — 내용은 DefenderFieldSystem 이
                // 매 프레임 재빌드. teardown 은 Teardown 이 함께 처리(멱등).
                var dFlow = new NativeArray<float2>(n, Allocator.Persistent);
                var dDist = new NativeArray<int>(n, Allocator.Persistent);
                try
                {
                    for (int i = 0; i < n; i++) dDist[i] = int.MaxValue;

                    handles.defenderField = em.CreateEntity();
                    em.AddComponentData(handles.defenderField, new DefenderFieldSingleton
                    {
                        flow     = dFlow,
                        dist     = dDist,
                        gridSize = map.gridSize,
                        tileSize = tileSize,
                        origin   = origin,
                    });
                }
                catch
                {
                    if (dFlow.IsCreated) dFlow.Dispose();
                    if (dDist.IsCreated) dDist.Dispose();
                    throw;
                }
            }
            catch
            {
                // 이관 전 실패만 여기서 해제한다. 이관 후라면 FlowFieldSingleton 이 소유하며
                // 호출부의 Teardown 이 정리한다(flow/dist/goals 와 같은 규약).
                // traversal-layers unit 0 — cellLayers 는 walk 와 소유권이 같다(같은 싱글턴).
                if (!walkOwnedBySingleton && walk.IsCreated) walk.Dispose();
                if (!walkOwnedBySingleton && cellLayers.IsCreated) cellLayers.Dispose();
                throw;
            }
        }

        // season-gimmick-overwork unit 4 — 픽업 스폰 후보 셀(Walk∪Place) 싱글턴 구축.
        // FlowFieldSingleton 동형: Persistent NativeArray 소유, Teardown 이 dispose.
        // 호출 전 TeardownPickupSpawnState 선행(멱등)은 호출부 책임.
        public static void InstallPickupSpawnState(
            EntityManager em,
            in GeneratedMap map,
            uint pickupSeed,
            ref SimFieldHandles handles)
        {
            int2 gridSize = map.gridSize;
            int n = gridSize.x * gridSize.y;

            // 이동/배치 타일영역 = Walk∪Place 셀 수집.
            var cells = new System.Collections.Generic.List<int2>(n);
            for (int i = 0; i < n; i++)
            {
                var t = map.tiles[i];
                if (t == MapTileType.Walk || t == MapTileType.Place)
                    cells.Add(new int2(i % gridSize.x, i / gridSize.x));
            }
            if (cells.Count == 0) return;

            var candidateCells = new NativeArray<int2>(cells.Count, Allocator.Persistent);
            for (int i = 0; i < cells.Count; i++) candidateCells[i] = cells[i];

            handles.pickupSpawnState = em.CreateEntity();
            em.AddComponentData(handles.pickupSpawnState, new PickupSpawnState
            {
                candidateCells = candidateCells,
                elapsed = 0f,
                rng = new Unity.Mathematics.Random(pickupSeed),
            });
            Debug.Log($"[SimFieldInstaller] PickupSpawnState built — 후보 셀 {candidateCells.Length}개 (Walk∪Place), seed={pickupSeed}");
        }

        // 멱등. 월드가 죽었으면 핸들만 되돌린다(엔티티 접근 불가).
        public static void Teardown(World world, EntityManager em, ref SimFieldHandles handles)
        {
            if (world == null || !world.IsCreated || em == default)
            {
                handles.Reset();
                return;
            }
            if (handles.flowField != Entity.Null && em != null && em.Exists(handles.flowField))
            {
                if (em.HasComponent<FlowFieldSingleton>(handles.flowField))
                {
                    var data = em.GetComponentData<FlowFieldSingleton>(handles.flowField);
                    data.Dispose();
                }
                em.DestroyEntity(handles.flowField);
            }
            handles.flowField = Entity.Null;

            // boss-defender-field unit 1 — defender field 는 goal field 와 라이프사이클 공유.
            if (handles.defenderField != Entity.Null && em != null && em.Exists(handles.defenderField))
            {
                if (em.HasComponent<DefenderFieldSingleton>(handles.defenderField))
                {
                    var data = em.GetComponentData<DefenderFieldSingleton>(handles.defenderField);
                    data.Dispose();
                }
                em.DestroyEntity(handles.defenderField);
            }
            handles.defenderField = Entity.Null;

            // season-gimmick-overwork unit 4 — 픽업 스폰 상태도 맵 field 와 동일 lifecycle.
            TeardownPickupSpawnState(em, ref handles);
        }

        public static void TeardownPickupSpawnState(EntityManager em, ref SimFieldHandles handles)
        {
            if (handles.pickupSpawnState != Entity.Null && em != null && em.Exists(handles.pickupSpawnState))
            {
                if (em.HasComponent<PickupSpawnState>(handles.pickupSpawnState))
                {
                    var data = em.GetComponentData<PickupSpawnState>(handles.pickupSpawnState);
                    data.Dispose();
                }
                em.DestroyEntity(handles.pickupSpawnState);
            }
            handles.pickupSpawnState = Entity.Null;
        }
    }
}
