// salvaged from Assets/_Project/Scripts/Bridge/BattleBridge.cs :: TryGetSpawnPathSim/4 ·
// AppendSpawnPathSegment/8 (battle-core-rebuild unit 5b)
// 이식 시 바뀐 것: `NativeArray`·`FlowFieldSingleton` → 코어의 `MapRuntime`/`FlowSlot`,
//   `List<Vector3>` → `List<float3>`(코어는 엔진을 모른다 — 계약 4), 거점 선택은 **인자로 받는다**
//   (고르는 자는 `AiMovePhase` 하나여야 하기 때문 — 아래 헤더 참조).
using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore.Move
{
    // **예고선이 그릴 대표 경로.** 스폰 칸 → 저작 웨이포인트들 → (거점) → 골.
    //
    // ⚠ 이 계산이 코어에 있는 이유: 예고선은 「적이 실제로 걸을 길」을 그린다고 약속한다.
    // 뷰에 두면 그 약속을 지킬 자가 뷰가 되고, 이동이 평활화·통행층·장애물을 바꾸는 날
    // **라인만 옛 규칙으로 남는다.** 옛 전투에서 그 어긋남이 실제로 두 번 났다
    // (필드 계단만 그려 유닛이 라인을 벗어나 걷던 것 · 거점으로 꺾는데 라인은 마음으로
    // 곧장 뻗던 것). 그래서 이동이 쓰는 것과 **같은 함수**(`PathSmoothing.TryStepTarget`)를
    // 같은 슬롯·같은 NavGrid 로 부른다.
    //
    // ⚠ **거점은 여기서 고르지 않는다.** `structureCell` 은 인자다 — 고르는 자는
    // `AiMovePhase.TryPickStructure` 하나이고(M18), 자를 하나 더 만들면 「가이드 ≠ 실제
    // 이동선」이 동률에서만 간헐적으로 재현된다.
    public static class SpawnPathPreview
    {
        /// <summary>
        /// 대표 경로를 `outPath` 에 채운다(sim 좌표, [0] = 스폰 칸 중앙). 2점 미만이면 false.
        ///
        /// `hasStructureLeg` 가 참이면 스폰 → `structureCell` → 골 두 구간으로 그린다.
        /// 거점을 부순 뒤 적이 재선정으로 결국 마음까지 가기 때문이다.
        /// </summary>
        public static bool Build(MapRuntime map, int lane, int pathIndex, byte layers,
                                 float agentRadius, bool hasStructureLeg, int2 structureCell,
                                 List<float3> outPath)
        {
            if (map == null || outPath == null) return false;
            outPath.Clear();

            var snapshot = map.Snapshot;
            if (snapshot.CellCount == 0) return false;
            if (lane < 0 || lane >= snapshot.Spawns.Length) return false;
            if (layers == 0) layers = TraversalSlots.DefaultMask;

            var nav = map.Nav.For(layers, map.Obstacles);
            float radius = agentRadius * map.TileSize;

            float3 pos = map.CenterOf(snapshot.Spawns[lane]);
            outPath.Add(pos);

            int waypoints = snapshot.WaypointCountAt(pathIndex);
            for (int i = 0; i < waypoints; i++)
                Append(map, in nav, snapshot.WaypointAt(pathIndex, i), layers, radius, ref pos, outPath);

            if (hasStructureLeg)
                Append(map, in nav, structureCell, layers, radius, ref pos, outPath);

            Append(map, in nav, MapSnapshot.GoalDestination, layers, radius, ref pos, outPath);

            return outPath.Count >= 2;
        }

        // 한 구간. 목적지 슬롯의 흐름을 따라 **이동과 같은 목표점 규칙**으로 걸어간다.
        //
        // 도달 불가(거리 무한)·고립(스텝 없음)이면 그 자리에서 멈춘다 — 조용히 직선을 긋지
        // 않는 것이 계약이다. 직선을 그으면 「벽을 통과하는 예고선」이 되고, 그건 라인이
        // 없는 것보다 나쁘다(화면이 규칙을 틀리게 가르친다).
        private static void Append(MapRuntime map, in NavGrid nav, int2 destination, byte layers,
                                   float radius, ref float3 pos, List<float3> outPath)
        {
            if (!map.Flow.HasSlot(destination, layers)) return;
            var slot = map.Flow.Slot(destination, layers);

            // 상한 = 칸 수 + 1. 흐름장은 단조 하강이라 칸 수를 넘게 걸을 수 없지만,
            // 평활화가 같은 자리를 되돌려 주는 퇴화 입력에서도 멈춰야 한다.
            int guard = map.Snapshot.CellCount + 1;
            for (int i = 0; i < guard; i++)
            {
                var cell = map.CellOf(pos);
                if (!map.InBounds(cell)) return;
                if (!slot.Reaches(cell)) return;
                if (slot.DistAt(cell) == 0) return;   // 도착

                if (!PathSmoothing.TryStepTarget(pos, in nav, slot.Flow, radius,
                                                 PathSmoothing.DefaultLookahead, out float3 next))
                    return;
                if (math.distancesq(pos, next) <= 1e-8f) return;

                pos = next;
                outPath.Add(pos);
            }
        }
    }
}
