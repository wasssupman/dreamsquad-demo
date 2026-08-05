using System.Collections.Generic;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Effects
{
    /// <summary>
    /// battle-sim-extraction unit 18-E/5 — 거점 순찰의 이번 틱 이동 방향. 구 `PatrolStep` 이식.
    /// **Effects 가 유일한 writer** 이고 `MovementSystem` 이 읽는다.
    /// ⚠ 이 컴포넌트 **보유 자체가 patrol 아키타입 판별**이다(Movement 의 분기 조건).
    /// </summary>
    public struct PatrolStep
    {
        public SimVec2 dir;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-E/5 — 순찰 방향 계산. 구 `PatrolAreaMath` 이식(순수).
    ///
    /// **신규 이동 알고리즘을 만들지 않는다.** 박스 제약을 walkMask 마스킹으로 표현하면
    /// 목적지 BFS · 도달 불가 판정 · cardinal 하강을 전부 재사용할 수 있다.
    /// ⚠ 그리디 스텝은 금지다 — 직선 greedy 는 벽 고착으로 이미 폐기됐고, 대각 이웃은
    /// 미수리 결함(대각 코너 슬립)에 걸린다. **현행 이동이 cardinal 인 것은 의도다.**
    ///
    /// ⚠ **스크래치 버퍼를 인자로 받는다.** 구 sim 은 함수 안에서 `Allocator.Temp` 로 잡았는데
    /// (범프 할당 = 사실상 무료) 관리 코드에서 같은 자리에 `new List` 를 두면 **엔티티당 프레임당
    /// 쓰레기**가 된다. 그래서 호출자가 재사용 버퍼를 넘긴다 — 순수성은 유지된다.
    /// </summary>
    public static class PatrolAreaMath
    {
        public static bool IsInArea(SimInt2 cell, SimInt2 anchorCell, int tileRadius)
            => SimMath.Abs(cell.x - anchorCell.x) <= tileRadius
            && SimMath.Abs(cell.y - anchorCell.y) <= tileRadius;

        /// <summary>
        /// 구역 ∩ walk 마스크를 채운다.
        ///
        /// ⚠ **버퍼를 먼저 0 으로 지운다.** 예전엔 "호출자가 0 초기화해 넘긴다" 는 주석 계약이었고
        /// 그건 코드로 강제되지 않았다 — 호출처가 버퍼를 재사용하는 순간(그럴 유인이 실제로 있고,
        /// 신 sim 은 **그걸 하고 있다**) 앞 엔티티의 구역 셀이 1 로 남아 **뒤 엔티티가 자기 구역
        /// 밖을 walkable 로 본다** = 순찰병이 거점을 벗어나 걸어나간다. 순찰병 2기 이상에서만
        /// 재현되는 버그라 말로 된 계약 대신 함수가 스스로 보장한다.
        /// </summary>
        public static void FillAreaMask(byte[] walkMask, SimInt2 gridSize,
                                        SimInt2 anchorCell, int tileRadius, byte[] outMask)
        {
            for (int i = 0; i < outMask.Length; i++) outMask[i] = 0;

            int minX = SimMath.Max(0, anchorCell.x - tileRadius);
            int maxX = SimMath.Min(gridSize.x - 1, anchorCell.x + tileRadius);
            int minY = SimMath.Max(0, anchorCell.y - tileRadius);
            int maxY = SimMath.Min(gridSize.y - 1, anchorCell.y + tileRadius);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int idx = GridMath.CellIndex(new SimInt2(x, y), gridSize);
                outMask[idx] = walkMask[idx];
            }
        }

        /// <summary>
        /// 이번 틱의 자기주도 이동 방향. zero = 정지.
        ///
        /// `areaMask` = 박스 ∩ walk · `fullMask` = 박스 무시 walk.
        /// ⚠ `fullMask` 는 **외력으로 박스 밖에 밀려났을 때만** 쓴다 — 포털/토네이도/임펄스는
        /// 진영을 안 보므로 순찰병을 박스 밖으로 민다. `areaMask` 로는 박스 밖 셀 dist 가
        /// `int.MaxValue` 라 하강이 zero 가 되어 **영구 정지**한다.
        /// </summary>
        public static SimVec2 StepDir(byte[] areaMask, byte[] fullMask, SimInt2 gridSize,
                                      SimInt2 anchorCell, int tileRadius,
                                      SimInt2 selfCell, int attackTileRange,
                                      SimInt2[] enemyCells, int enemyCount,
                                      SimVec2[] scratchFlow, int[] scratchDist,
                                      List<SimInt2> inAreaBuffer, List<SimInt2> sourcesBuffer,
                                      ref SimInt2[] sourceArray)
        {
            int selfIdx = GridMath.CellIndex(selfCell, gridSize);

            // 박스 밖 = 외력에 밀려남. 마스크 없는 필드로 거점 복귀 경로를 잡는다.
            if (!IsInArea(selfCell, anchorCell, tileRadius))
                return DescendToAnchor(fullMask, gridSize, anchorCell, selfCell,
                                       scratchFlow, scratchDist, ref sourceArray);

            int srcCount = BuildAreaChaseField(areaMask, gridSize, anchorCell, tileRadius,
                                               attackTileRange, enemyCells, enemyCount,
                                               scratchFlow, scratchDist,
                                               inAreaBuffer, sourcesBuffer, ref sourceArray);

            // 소스 0(구역 안에 사격 위치 없음) 또는 도달 불가(벽으로 갈린 구역)면 적을 포기하고
            // 거점으로 — **좀비 추격을 만들지 않는다.**
            if (srcCount > 0 && scratchDist[selfIdx] != int.MaxValue)
                return FlowRecovery.RecoveryDir(selfCell, scratchDist, gridSize);

            if (selfCell.Equals(anchorCell)) return SimVec2.Zero;
            return DescendToAnchor(areaMask, gridSize, anchorCell, selfCell,
                                   scratchFlow, scratchDist, ref sourceArray);
        }

        /// <summary>
        /// 구역 안 **모든** 적의 사격 위치를 소스로 chase field 를 굽는다. 반환 = 소스 수.
        ///
        /// ⚠ 최근접 적 1체를 먼저 고르지 않는다. 그러면 벽으로 갈린 구역에서 **도달 불가한
        /// 최근접 적** 때문에 도달 가능한 적을 통째로 포기한다(코앞의 적을 두고 뒷걸음질).
        /// N-소스 BFS 는 "갈 수 있는 사격 위치 중 가장 가까운 곳" 을 자동으로 고르고 BFS 횟수도 같다.
        /// </summary>
        private static int BuildAreaChaseField(byte[] areaMask, SimInt2 gridSize,
                                               SimInt2 anchorCell, int tileRadius, int attackTileRange,
                                               SimInt2[] enemyCells, int enemyCount,
                                               SimVec2[] scratchFlow, int[] scratchDist,
                                               List<SimInt2> inArea, List<SimInt2> sources,
                                               ref SimInt2[] sourceArray)
        {
            inArea.Clear();
            for (int i = 0; i < enemyCount; i++)
                if (IsInArea(enemyCells[i], anchorCell, tileRadius)) inArea.Add(enemyCells[i]);
            if (inArea.Count == 0) return 0;

            // 리스트를 배열 창으로 옮긴다(빌더가 배열 + 유효 길이를 받는다).
            EnsureCapacity(ref sourceArray, inArea.Count);
            for (int i = 0; i < inArea.Count; i++) sourceArray[i] = inArea[i];

            int count = FlowFieldBuilder.CollectDefenderSources(
                areaMask, gridSize, sourceArray, inArea.Count,
                SimMath.Max(1, attackTileRange), sources);
            if (count == 0)
            {
                // 도달 불가를 명시로 남긴다 — 호출자가 `scratchDist[self]` 를 본다.
                for (int i = 0; i < scratchDist.Length; i++) scratchDist[i] = int.MaxValue;
                return 0;
            }

            EnsureCapacity(ref sourceArray, sources.Count);
            for (int i = 0; i < sources.Count; i++) sourceArray[i] = sources[i];
            FlowFieldBuilder.BuildFromSources(areaMask, gridSize, sourceArray, sources.Count,
                                              scratchFlow, scratchDist);
            return count;
        }

        /// <summary>
        /// 거점 1셀을 소스로 BFS 후 하강.
        /// ⚠ `CollectDefenderSources` 를 쓰지 않는다 — 그쪽은 **중심 셀을 제외**한다(방어유닛
        /// 자기 셀 = Place = 벽 전제). 거점은 walk 셀이고 순찰병이 실제로 서야 하는 칸이다.
        ///
        /// ⚠ `dist[self] == MaxValue` 가드를 두지 않는다. 그 값은 두 상황에서 나온다:
        /// ① 진짜 고립 — 4이웃도 전부 MaxValue 라 `RecoveryDir` 이 알아서 zero 를 준다(가드는 중복).
        /// ② **자기 셀이 마스크 0**(차단형 해저드가 발밑) — 가드를 두면 탈출 자체를 막아
        ///    순찰병이 장애물 안에 영구히 박힌다. `RecoveryDir` 은 `best = MaxValue` 로 시작하므로
        ///    유한한 이웃 아무 쪽으로나 빠져나간다.
        /// </summary>
        private static SimVec2 DescendToAnchor(byte[] mask, SimInt2 gridSize,
                                               SimInt2 anchorCell, SimInt2 selfCell,
                                               SimVec2[] scratchFlow, int[] scratchDist,
                                               ref SimInt2[] sourceArray)
        {
            EnsureCapacity(ref sourceArray, 1);
            sourceArray[0] = anchorCell;
            FlowFieldBuilder.BuildFromSources(mask, gridSize, sourceArray, 1, scratchFlow, scratchDist);
            return FlowRecovery.RecoveryDir(selfCell, scratchDist, gridSize);
        }

        private static void EnsureCapacity(ref SimInt2[] a, int needed)
        {
            if (a.Length >= needed) return;
            int size = a.Length;
            while (size < needed) size *= 2;
            a = new SimInt2[size];
        }
    }
}
