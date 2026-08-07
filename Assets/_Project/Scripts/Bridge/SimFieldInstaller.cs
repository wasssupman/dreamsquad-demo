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
        // 호출 전 Teardown 이 선행되어야 한다(멱등성 계약은 호출부에 남겨 순서가 보이게 한다).
        // CRITICAL #1 (Codex 2차 리뷰): AddComponentData 는 component 존재 시 throw,
        // 그리고 기존 arrays 가 dispose 없이 덮어써지면 누수.
        public static void InstallNavFields(
            EntityManager em,
            in GeneratedMap map,
            float tileSize,
            float3 origin,
            ref SimFieldHandles handles)
        {
            int w = map.gridSize.x;
            int h = map.gridSize.y;
            int n = w * h;

            var walk = new NativeArray<byte>(n, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++)
                    walk[i] = (byte)(map.tiles[i] == MapTileType.Walk ? 1 : 0);

                var flow = new NativeArray<float2>(n, Allocator.Persistent);
                var dist = new NativeArray<int>(n, Allocator.Persistent);
                NativeArray<int2> goalsField = default;
                try
                {
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

                    FlowFieldBuilder.BuildFromSources(walk, gridSize, goalsField, flow, dist);

                    var data = new FlowFieldSingleton
                    {
                        flow = flow,
                        dist = dist,
                        gridSize = gridSize,
                        goalCell = goal,
                        goals = goalsField,
                        tileSize = tileSize,
                        origin = origin,
                        version = map.generatorVersion,
                    };

                    handles.flowField = em.CreateEntity();
                    em.AddComponentData(handles.flowField, data);
                    Debug.Log($"[SimFieldInstaller] FlowField built — boardOrigin={origin} tileSize={tileSize} grid={gridSize}");
                }
                catch
                {
                    if (flow.IsCreated) flow.Dispose();
                    if (dist.IsCreated) dist.Dispose();
                    if (goalsField.IsCreated) goalsField.Dispose();   // 싱글턴 이관 전 실패 시만
                    throw;
                }

                // boss-defender-field unit 1 — 방어유닛-지향 필드 싱글톤. walkMask 는 위의
                // Temp `walk` 를 Persistent 로 복사(goal field 는 저장 안 하는 값).
                // flow/dist 는 초기 "소스 0" 상태(dist=MaxValue) — 내용은 DefenderFieldSystem 이
                // 매 프레임 재빌드. teardown 은 Teardown 이 함께 처리(멱등).
                var dWalk = new NativeArray<byte>(n, Allocator.Persistent);
                var dFlow = new NativeArray<float2>(n, Allocator.Persistent);
                var dDist = new NativeArray<int>(n, Allocator.Persistent);
                try
                {
                    dWalk.CopyFrom(walk);
                    for (int i = 0; i < n; i++) dDist[i] = int.MaxValue;

                    handles.defenderField = em.CreateEntity();
                    em.AddComponentData(handles.defenderField, new DefenderFieldSingleton
                    {
                        walkMask = dWalk,
                        flow     = dFlow,
                        dist     = dDist,
                        gridSize = map.gridSize,
                        tileSize = tileSize,
                        origin   = origin,
                    });
                }
                catch
                {
                    if (dWalk.IsCreated) dWalk.Dispose();
                    if (dFlow.IsCreated) dFlow.Dispose();
                    if (dDist.IsCreated) dDist.Dispose();
                    throw;
                }
            }
            finally
            {
                if (walk.IsCreated) walk.Dispose();
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
