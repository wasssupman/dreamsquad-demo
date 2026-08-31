using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Combat;   // AggroChaseMath — 목적지 디스크 + BFS 조합 재사용
using Wassup.Battle.Movement; // GridMath · FlowRecovery

namespace Wassup.Battle.Effects
{
    // summon-patrol-defender unit 1 — 거점 순찰 아군의 이동 방향 계산(순수, 아키텍처 무참조).
    //
    // **신규 이동 알고리즘을 만들지 않는다.** 박스 제약을 walkMask 마스킹으로 표현하면
    // 목적지 BFS(FlowFieldBuilder) · 도달 불가 판정 · cardinal 하강(FlowRecovery.RecoveryDir)을
    // 전부 재사용할 수 있다. 그리디 스텝은 금지다 — aggro-tile-chase 가 직선 greedy 를
    // 벽 고착(좀비버그)으로 폐기했고, 대각 이웃은 미수리 결함("대각 코너 슬립 차단", 백로그)에
    // 걸린다. 현행 이동이 cardinal 인 것은 의도다.
    public static class PatrolAreaMath
    {
        public static bool IsInArea(int2 cell, int2 anchorCell, int tileRadius)
            => math.abs(cell.x - anchorCell.x) <= tileRadius
            && math.abs(cell.y - anchorCell.y) <= tileRadius;

        // 구역 ∩ walk 마스크를 채운다.
        //
        // **버퍼를 먼저 0 으로 지운다.** 예전엔 "호출자가 0 초기화해 넘긴다"는 주석 계약이었는데,
        // 그건 코드로 강제되지 않는다: 호출처가 버퍼를 재사용하도록 최적화하는 순간(그럴 유인이
        // 실제로 있다 — 이웃한 fullMask/scratch 들은 이미 프레임당 1회로 hoist 돼 있다) 앞
        // 엔티티의 구역 셀이 1 로 남아 **뒤 엔티티가 자기 구역 밖을 walkable 로 본다** =
        // 순찰병이 거점을 벗어나 걸어나간다. 순찰병 2기 이상에서만 재현되는 추적 난이도 높은
        // 버그라, 말로 된 계약 대신 함수가 스스로 보장한다. 그리드가 작아 비용은 무시 가능하다.
        public static void FillAreaMask(
            NativeArray<byte> walkMask, int2 gridSize,
            int2 anchorCell, int tileRadius,
            NativeArray<byte> outMask)
        {
            for (int i = 0; i < outMask.Length; i++) outMask[i] = 0;

            int minX = math.max(0, anchorCell.x - tileRadius);
            int maxX = math.min(gridSize.x - 1, anchorCell.x + tileRadius);
            int minY = math.max(0, anchorCell.y - tileRadius);
            int maxY = math.min(gridSize.y - 1, anchorCell.y + tileRadius);

            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int idx = GridMath.CellIndex(new int2(x, y), gridSize);
                outMask[idx] = walkMask[idx];
            }
        }

        // 구역 안 **모든** 적의 사격 위치를 소스로 chase field 를 굽는다. 반환 = 소스 수(0 = 없음).
        //
        // 최근접 적 1체를 먼저 고르지 않는 이유: 그러면 벽으로 갈린 구역에서 **도달 불가한
        // 최근접 적** 때문에 같은 구역의 도달 가능한 적을 통째로 포기한다(코앞의 적을 두고
        // 뒷걸음질). N-소스 BFS 는 "갈 수 있는 사격 위치 중 가장 가까운 곳"을 자동으로 고르므로
        // 그 실패가 구조적으로 사라지고, BFS 횟수도 1회로 같다.
        // 결정론: 소스 집합은 enemyCells 순서와 무관한 셀 집합이고, 하강은 RecoveryDir 의
        // 고정 cardinal 순서(+x,-x,+y,-y)를 따른다.
        private static int BuildAreaChaseField(
            NativeArray<byte> areaMask, int2 gridSize,
            int2 anchorCell, int tileRadius, int attackTileRange,
            NativeArray<int2> enemyCells,
            NativeArray<float2> scratchFlow, NativeArray<int> scratchDist)
        {
            var inArea = new NativeList<int2>(math.max(1, enemyCells.Length), Allocator.Temp);
            var sources = new NativeList<int2>(16, Allocator.Temp);
            try
            {
                for (int i = 0; i < enemyCells.Length; i++)
                    if (IsInArea(enemyCells[i], anchorCell, tileRadius))
                        inArea.Add(enemyCells[i]);
                if (inArea.Length == 0) return 0;

                int count = FlowFieldBuilder.CollectDefenderSources(
                    areaMask, gridSize, inArea.AsArray(), math.max(1, attackTileRange), sources);
                // ⚠ **여기는 `RangeToTiles` 로 통일하지 않는다** (distance-based-range unit 1 에서
                // 검토 후 보류). 소스 수집과 아래 `reach` 가 **같은 클램프**를 써야 하는데,
                // 여기만 `RangeToTiles` 로 바꾸면 사거리 0 유닛에서 «BFS 는 사격 칸을 세우는데
                // 도착 판정은 후보를 못 찾는» 교착이 난다. 둘 다 바꾸는 건 순찰 이동의 성격
                // 변경이라 이 unit(술어 수렴) 밖이다.
                if (count == 0)
                {
                    for (int i = 0; i < scratchDist.Length; i++) scratchDist[i] = int.MaxValue;
                    return 0;
                }
                FlowFieldBuilder.BuildFromSources(areaMask, gridSize, sources.AsArray(), scratchFlow, scratchDist);
                return count;
            }
            finally
            {
                inArea.Dispose();
                sources.Dispose();
            }
        }

        // 이번 틱의 자기주도 이동 방향. zero = 정지.
        //
        // areaMask  = 박스 ∩ walk (FillAreaMask 결과)
        // fullMask = 박스 무시 walk — **외력으로 박스 밖에 밀려났을 때만** 쓴다.
        //            포털/토네이도/임펄스는 faction 을 안 보므로 순찰병을 박스 밖으로 민다
        //            (README 계약 6). areaMask 로는 박스 밖 셀 dist 가 int.MaxValue 라
        //            하강이 zero 가 되어 영구 정지한다.
        // unit 9 — **중심(anchorCell)과 집(homeCell)은 다른 칸이다.**
        //   anchorCell = 박스 중심(소환사 셀). 구역 판정·사격 위치 수집의 기준.
        //   homeCell   = 대기·복귀 칸(소환사 주변). "여기 서 있으면 정지"의 기준.
        // 겸직시키면 소환물이 소환사와 같은 칸에 겹친다(`PatrolAnchor` 주석 참조).
        public static float2 StepDir(
            NativeArray<byte> areaMask,
            NativeArray<byte> fullMask,
            int2 gridSize,
            int2 anchorCell,
            int2 homeCell,
            int tileRadius,
            int2 selfCell,
            int attackTileRange,
            NativeArray<int2> enemyCells,
            NativeArray<float2> scratchFlow,
            NativeArray<int> scratchDist,
            float3 selfPos,
            NativeArray<float3> enemyPositions,
            float tileSize)
        {
            int selfIdx = GridMath.CellIndex(selfCell, gridSize);

            // 박스 밖 = 외력에 밀려남. 마스크 없는 필드로 **집**까지 복귀 경로를 잡는다.
            if (!IsInArea(selfCell, anchorCell, tileRadius))
                return DescendToHome(fullMask, gridSize, homeCell, selfCell, scratchFlow, scratchDist);

            // 구역 안 적 전원의 사격 위치를 소스로 BFS. 도달 = 발사 가능(같은 Chebyshev 메트릭).
            int srcCount = BuildAreaChaseField(
                areaMask, gridSize, anchorCell, tileRadius, attackTileRange,
                enemyCells, scratchFlow, scratchDist);

            // 소스 0(구역 안에 사격 위치 없음) 또는 도달 불가(벽으로 갈린 구역)면
            // 적을 포기하고 집으로 — 좀비 추격을 만들지 않는다.
            if (srcCount > 0 && scratchDist[selfIdx] != int.MaxValue)
            {
                float2 chase = FlowRecovery.RecoveryDir(selfCell, scratchDist, gridSize);
                if (!chase.Equals(float2.zero)) return chase;
                // 격자상 «사격 칸» 에 도착했다. 하지만 사거리 판정의 2차(물리 거리)는 칸 안
                // 어디에 섰는지를 본다 — 아직 멀면 **계속 다가간다**. 이 한 줄이 없으면
                // 격자는 "도착"이라 멈추고 공격은 "멀다"고 거부해 교착이 난다(AttackReach 주석).
                return CloseInDir(areaMask, gridSize, anchorCell, tileRadius,
                    selfCell, selfPos, attackTileRange, enemyCells, enemyPositions, tileSize);
            }

            if (selfCell.Equals(homeCell)) return float2.zero;
            return DescendToHome(areaMask, gridSize, homeCell, selfCell, scratchFlow, scratchDist);
        }

        // 사거리 2차 게이트(물리 거리)를 이동 쪽에서 만족시키는 마지막 접근.
        // 셀로는 이미 사거리 안이지만 월드 거리가 상한을 넘는 적을 골라, **지배축 cardinal**
        // 로 한 칸 밀어준다. 대각 벡터를 쓰지 않는 이유는 이동 전반의 규약과 같다 —
        // 8-이웃 성분은 대각 코너 슬립(백로그 미수리)에 걸린다. 상한이 체비셰프라
        // 지배축을 줄이는 것이 곧 거리를 줄이는 것이므로 cardinal 로 충분하다.
        //
        // 하나도 없으면 zero — «셀도 사거리 안, 물리도 사거리 안» 이므로 정지가 맞다.
        private static float2 CloseInDir(
            NativeArray<byte> areaMask, int2 gridSize, int2 anchorCell, int tileRadius,
            int2 selfCell, float3 selfPos, int attackTileRange,
            NativeArray<int2> enemyCells, NativeArray<float3> enemyPositions, float tileSize)
        {
            if (!enemyPositions.IsCreated || enemyPositions.Length != enemyCells.Length)
                return float2.zero;

            // 소스 수집(BuildAreaChaseField)이 max(1, range) 로 클램프하므로 여기도 같은 값을 써야
            // 한다. 갈리면 range 0 유닛이 «BFS 는 사격 칸이라 세우고 여긴 후보를 못 찾는» 교착이 난다.
            int reach = math.max(1, attackTileRange);

            float bestGap = 0f;
            float3 bestPos = default;
            bool found = false;
            for (int i = 0; i < enemyCells.Length; i++)
            {
                // **구역 안 적만** 본다 — BuildAreaChaseField 와 같은 술어. 이게 없으면 구역 밖 적을
                // 향해 박스를 걸어나가고, 다음 프레임 DescendToHome 이 되돌려 경계에서 진동한다.
                if (!IsInArea(enemyCells[i], anchorCell, tileRadius)) continue;
                if (!AttackReach.InCellRange(selfCell, enemyCells[i], reach)) continue;
                if (AttackReach.InReach(selfPos, enemyPositions[i], reach, tileSize)) continue;
                float gap = math.max(math.abs(enemyPositions[i].x - selfPos.x),
                                     math.abs(enemyPositions[i].z - selfPos.z));
                if (!found || gap < bestGap) { bestGap = gap; bestPos = enemyPositions[i]; found = true; }
            }
            if (!found) return float2.zero;

            float dx = bestPos.x - selfPos.x;
            float dz = bestPos.z - selfPos.z;
            // 지배축 우선, 막히면 나머지 축. 둘 다 막히면 정지 — raw cardinal 을 그대로 뱉으면
            // 벽에 밀려 «걷는 애니로 제자리» 가 된다(AgentCollision 이 변위를 먹는다).
            float2 primary = math.abs(dx) >= math.abs(dz)
                ? new float2(dx >= 0f ? 1f : -1f, 0f)
                : new float2(0f, dz >= 0f ? 1f : -1f);
            if (Passable(areaMask, gridSize, selfCell, primary)) return primary;

            float2 secondary = math.abs(dx) >= math.abs(dz)
                ? new float2(0f, dz >= 0f ? 1f : -1f)
                : new float2(dx >= 0f ? 1f : -1f, 0f);
            if (math.lengthsq(secondary) > 0f && Passable(areaMask, gridSize, selfCell, secondary))
                return secondary;

            return float2.zero;
        }

        // 한 칸 앞이 구역 안 통행 가능 셀인가. areaMask 는 walkMask ∩ 박스라 두 조건을 함께 본다.
        private static bool Passable(NativeArray<byte> mask, int2 gridSize, int2 from, float2 dir)
        {
            int2 to = from + new int2((int)dir.x, (int)dir.y);
            if (to.x < 0 || to.y < 0 || to.x >= gridSize.x || to.y >= gridSize.y) return false;
            return mask[GridMath.CellIndex(to, gridSize)] != 0;
        }

        // 집 1셀을 소스로 BFS 후 하강. CollectDefenderSources 를 쓰지 않는 이유:
        // 그쪽은 **중심 셀을 제외**한다(방어유닛 자기 셀 = Place = 벽 전제). 집은
        // 순찰병이 실제로 서야 하는 칸이라 소스에서 빠지면 안 된다.
        private static float2 DescendToHome(
            NativeArray<byte> mask, int2 gridSize, int2 homeCell, int2 selfCell,
            NativeArray<float2> scratchFlow, NativeArray<int> scratchDist)
        {
            var sources = new NativeArray<int2>(1, Allocator.Temp);
            try
            {
                sources[0] = homeCell;
                FlowFieldBuilder.BuildFromSources(mask, gridSize, sources, scratchFlow, scratchDist);
                // `dist[self] == MaxValue` 가드를 두지 않는다. 그 값은 두 상황에서 나온다:
                //  (1) 진짜 고립(walkable 인데 anchor 와 단절) — 4이웃도 전부 MaxValue 라
                //      RecoveryDir 이 알아서 zero 를 돌려준다. 가드는 중복이다.
                //  (2) **자기 셀이 마스크 0** — 차단형 해저드가 발밑에 깔린 경우. 여기서
                //      가드를 두면 탈출 자체를 막아 순찰병이 장애물 안에 영구히 박힌다.
                //      RecoveryDir 은 best=MaxValue 로 시작하므로 유한한 이웃 아무 쪽으로나
                //      빠져나간다 — 어그로 추격 경로가 가드 없이 RecoveryDir 을 직접 부르는
                //      것과 같은 이유다(MovementSystem 의 Chasing 분기).
                return FlowRecovery.RecoveryDir(selfCell, scratchDist, gridSize);
            }
            finally
            {
                sources.Dispose();
            }
        }
    }
}
