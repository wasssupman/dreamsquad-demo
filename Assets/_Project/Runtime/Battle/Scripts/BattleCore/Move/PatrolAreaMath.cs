// salvaged from Assets/_Project/Scripts/Battle/Effects/PatrolAreaMath.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray`/`NativeList`/`Allocator.Temp` → 재사용 스크래치(`PatrolScratch`).
// 규칙(구역 마스크 자가 0 초기화 · N-소스 BFS · cardinal 하강 · 접근 보정)은 그대로다.
using Unity.Mathematics;
using Somnia.Battle.BattleCore.Combat;
using Somnia.Battle.BattleCore.Map;

namespace Somnia.Battle.BattleCore.Move
{
    // 거점 순찰 아군의 이동 방향.
    //
    // **새 이동 알고리즘을 만들지 않는다.** 박스 제약을 walk 마스크 마스킹으로 표현하면
    // 목적지 BFS · 도달 불가 판정 · cardinal 하강을 전부 재사용할 수 있다. 그리디 스텝은
    // 금지다 — 직선 greedy 는 벽 고착으로 폐기됐고, 대각 이웃은 코너 슬립에 걸린다.
    public static class PatrolAreaMath
    {
        public static bool IsInArea(int2 cell, int2 anchorCell, int tileRadius)
            => math.abs(cell.x - anchorCell.x) <= tileRadius
            && math.abs(cell.y - anchorCell.y) <= tileRadius;

        // 구역 ∩ walk 마스크를 채운다.
        //
        // ⚠ **버퍼를 먼저 0 으로 지운다**(M11). 예전엔 「호출자가 0 초기화해 넘긴다」는 주석
        // 계약이었는데 코드로 강제되지 않았다: 호출처가 버퍼를 재사용하는 순간 앞 유닛의 구역
        // 칸이 1 로 남아 **뒤 유닛이 자기 구역 밖을 걷는 칸으로 본다** = 순찰병이 거점을 벗어나
        // 걸어나간다. 순찰병 2기 이상에서만 재현되는 버그라, 말로 된 계약 대신 함수가 보장한다.
        public static void FillAreaMask(byte[] walkMask, int2 gridSize, int2 anchorCell, int tileRadius,
                                        byte[] outMask)
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

        // 이번 틱의 자기주도 이동 방향. zero = 정지.
        //
        // areaMask = 박스 ∩ walk · fullMask = 박스 무시 walk(**외력으로 박스 밖에 밀려났을 때만**).
        //
        // ⚠ **중심(anchorCell)과 집(homeCell)은 다른 칸이다.**
        //   anchorCell = 박스 중심(소환사 칸). 구역 판정·사격 위치 수집의 기준.
        //   homeCell   = 대기·복귀 칸. 「여기 서 있으면 정지」의 기준.
        // 겸직시키면 소환물이 소환사와 같은 칸에 겹친다.
        public static float2 StepDir(byte[] areaMask, byte[] fullMask, int2 gridSize,
                                     int2 anchorCell, int2 homeCell, int tileRadius,
                                     int2 selfCell, float3 selfPos, float selfBodyRadiusTiles,
                                     int attackTileRange, float tileSize,
                                     int2[] enemyCells, float3[] enemyPositions, float[] enemyBodyRadii,
                                     int enemyCount, PatrolScratch scratch)
        {
            int selfIdx = GridMath.CellIndex(selfCell, gridSize);

            // 박스 밖 = 외력에 밀려남. 마스크 없는 필드로 **집**까지 복귀 경로를 잡는다.
            if (!IsInArea(selfCell, anchorCell, tileRadius))
                return DescendToHome(fullMask, gridSize, homeCell, selfCell, scratch);

            int srcCount = BuildAreaChaseField(areaMask, gridSize, anchorCell, tileRadius,
                                               attackTileRange, enemyCells, enemyCount, scratch);

            // 소스 0(구역 안에 사격 위치 없음) 또는 도달 불가(벽으로 갈린 구역)면 적을 포기하고
            // 집으로 — 좀비 추격을 만들지 않는다.
            if (srcCount > 0 && scratch.Dist[selfIdx] != int.MaxValue)
            {
                float2 chase = FlowRecovery.RecoveryDir(selfCell, scratch.Dist, gridSize);
                if (!chase.Equals(float2.zero)) return chase;
                // 격자상 «사격 칸»에 도착했다. 하지만 사거리 판정은 칸 안 어디에 섰는지를 본다 —
                // 아직 멀면 **계속 다가간다**. 이 한 줄이 없으면 격자는 「도착」이라 멈추고 공격은
                // 「멀다」고 거부해 교착이 난다.
                return CloseInDir(areaMask, gridSize, anchorCell, tileRadius, selfCell, selfPos,
                                  attackTileRange, tileSize, selfBodyRadiusTiles,
                                  enemyCells, enemyPositions, enemyBodyRadii, enemyCount);
            }

            if (selfCell.Equals(homeCell)) return float2.zero;
            return DescendToHome(areaMask, gridSize, homeCell, selfCell, scratch);
        }

        // 구역 안 **모든** 적의 사격 위치를 소스로 추격 필드를 굽는다. 반환 = 소스 수.
        //
        // 최근접 적 1체를 먼저 고르지 않는 이유: 그러면 벽으로 갈린 구역에서 **도달 불가한
        // 최근접 적** 때문에 같은 구역의 도달 가능한 적을 통째로 포기한다(코앞의 적을 두고
        // 뒷걸음질). N-소스 BFS 는 「갈 수 있는 사격 위치 중 가장 가까운 곳」을 자동으로 고르므로
        // 그 실패가 구조적으로 사라지고, BFS 횟수도 1회로 같다.
        private static int BuildAreaChaseField(byte[] areaMask, int2 gridSize, int2 anchorCell,
                                               int tileRadius, int attackTileRange,
                                               int2[] enemyCells, int enemyCount, PatrolScratch scratch)
        {
            int inArea = 0;
            for (int i = 0; i < enemyCount; i++)
                if (IsInArea(enemyCells[i], anchorCell, tileRadius))
                    scratch.PushTarget(enemyCells[i], ref inArea);
            if (inArea == 0) return 0;

            // ⚠ **여기는 `RangeToTiles` 로 통일하지 않는다.** 소스 수집과 아래 `reach` 가 **같은
            // 클램프**를 써야 하는데, 여기만 바꾸면 사거리 0 유닛에서 «BFS 는 사격 칸을 세우는데
            // 도착 판정은 후보를 못 찾는» 교착이 난다.
            int range = math.max(1, attackTileRange);
            scratch.EnsureSources(inArea * FlowFieldBuilder.DiscArea(range));
            int count = FlowFieldBuilder.CollectDefenderSources(
                areaMask, gridSize, scratch.Targets, inArea, range, scratch.Sources);
            if (count == 0)
            {
                for (int i = 0; i < scratch.Dist.Length; i++) scratch.Dist[i] = int.MaxValue;
                return 0;
            }
            FlowFieldBuilder.BuildFromSources(areaMask, gridSize, scratch.Sources, count,
                                              scratch.Flow, scratch.Dist, scratch.Queue);
            return count;
        }

        // 사거리 게이트를 이동 쪽에서 만족시키는 마지막 접근.
        // 칸으로는 이미 사거리 안이지만 월드 거리가 상한을 넘는 적을 골라, **지배축 cardinal**
        // 로 한 칸 밀어준다. 하나도 없으면 zero — «칸도 안, 몸 거리도 안» 이므로 정지가 맞다.
        private static float2 CloseInDir(byte[] areaMask, int2 gridSize, int2 anchorCell, int tileRadius,
                                         int2 selfCell, float3 selfPos, int attackTileRange, float tileSize,
                                         float selfBodyRadiusTiles,
                                         int2[] enemyCells, float3[] enemyPositions, float[] enemyBodyRadii,
                                         int enemyCount)
        {
            if (enemyPositions == null) return float2.zero;

            int reach = math.max(1, attackTileRange);

            float bestGap = 0f;
            float3 bestPos = default;
            bool found = false;
            for (int i = 0; i < enemyCount; i++)
            {
                // **구역 안 적만** 본다 — 소스 수집과 같은 술어. 이게 없으면 구역 밖 적을 향해
                // 박스를 걸어나가고, 다음 틱 복귀가 되돌려 경계에서 진동한다.
                if (!IsInArea(enemyCells[i], anchorCell, tileRadius)) continue;
                if (!AttackReach.InCellRange(selfCell, enemyCells[i], reach)) continue;
                // ⚠ **대상 몸을 넘긴다.** 안 넘기면 여기만 다른 답을 받는다 — 보스가 사거리
                // 안인데 이동은 밖으로 읽어 **이미 쏠 수 있는데 계속 다가간다.**
                if (AttackReach.InReach(selfPos, enemyPositions[i], reach, tileSize,
                                        selfBodyRadiusTiles,
                                        enemyBodyRadii != null && enemyBodyRadii.Length > i ? enemyBodyRadii[i] : 0f))
                    continue;
                float gap = math.max(math.abs(enemyPositions[i].x - selfPos.x),
                                     math.abs(enemyPositions[i].z - selfPos.z));
                if (!found || gap < bestGap) { bestGap = gap; bestPos = enemyPositions[i]; found = true; }
            }
            if (!found) return float2.zero;

            float dx = bestPos.x - selfPos.x;
            float dz = bestPos.z - selfPos.z;
            // 지배축 우선, 막히면 나머지 축. 둘 다 막히면 정지 — raw cardinal 을 그대로 뱉으면
            // 벽에 밀려 «걷는 애니로 제자리»가 된다.
            // 지배축 선택은 `AggroChaseMath.CloseInCardinals` 가 소유한다 — 추격 보정과 **같은
            // 함수**여야 한다. 두 벌이면 조용히 갈린다.
            AggroChaseMath.CloseInCardinals(dx, dz, out var primary, out var secondary);
            if (Passable(areaMask, gridSize, selfCell, primary)) return primary;
            if (math.lengthsq(secondary) > 0f && Passable(areaMask, gridSize, selfCell, secondary))
                return secondary;

            return float2.zero;
        }

        private static bool Passable(byte[] mask, int2 gridSize, int2 from, float2 dir)
        {
            int2 to = from + new int2((int)dir.x, (int)dir.y);
            if (to.x < 0 || to.y < 0 || to.x >= gridSize.x || to.y >= gridSize.y) return false;
            return mask[GridMath.CellIndex(to, gridSize)] != 0;
        }

        // 집 한 칸을 소스로 BFS 후 하강. `CollectDefenderSources` 를 쓰지 않는 이유: 그쪽은
        // **중심 칸을 제외**한다(배치 유닛 자기 칸 = 벽 전제). 집은 순찰병이 실제로 서야 하는
        // 칸이라 소스에서 빠지면 안 된다.
        //
        // `dist[self] == MaxValue` 가드를 두지 않는다. 그 값은 두 상황에서 나온다:
        //  (1) 진짜 고립 — 4이웃도 전부 MaxValue 라 하강이 알아서 zero 를 돌려준다(가드는 중복).
        //  (2) **자기 칸이 마스크 0** — 차단형 장판이 발밑에 깔린 경우. 여기서 가드를 두면 탈출
        //      자체를 막아 순찰병이 장애물 안에 영구히 박힌다.
        private static float2 DescendToHome(byte[] mask, int2 gridSize, int2 homeCell, int2 selfCell,
                                            PatrolScratch scratch)
        {
            scratch.EnsureSources(1);
            scratch.Sources[0] = homeCell;
            FlowFieldBuilder.BuildFromSources(mask, gridSize, scratch.Sources, 1,
                                              scratch.Flow, scratch.Dist, scratch.Queue);
            return FlowRecovery.RecoveryDir(selfCell, scratch.Dist, gridSize);
        }
    }

    // 순찰 계산이 쓰는 재사용 버퍼. **판당 한 개**를 들고 다닌다 — 틱 중 할당 0.
    public sealed class PatrolScratch
    {
        public readonly float2[] Flow;
        public readonly int[] Dist;
        public readonly byte[] AreaMask;
        public readonly CellQueue Queue;

        public int2[] Sources = new int2[64];
        public int2[] Targets = new int2[16];

        public PatrolScratch(int cellCount)
        {
            int n = math.max(1, cellCount);
            Flow = new float2[n];
            Dist = new int[n];
            AreaMask = new byte[n];
            Queue = new CellQueue(n * 2);
        }

        public void EnsureSources(int n)
        {
            if (Sources.Length >= n) return;
            int cap = Sources.Length;
            while (cap < n) cap *= 2;
            Sources = new int2[cap];
        }

        public void PushTarget(int2 cell, ref int count)
        {
            if (count >= Targets.Length)
            {
                var next = new int2[Targets.Length * 2];
                System.Array.Copy(Targets, next, Targets.Length);
                Targets = next;
            }
            Targets[count++] = cell;
        }
    }
}
