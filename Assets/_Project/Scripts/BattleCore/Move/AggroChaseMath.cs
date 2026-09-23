// salvaged from Assets/_Project/Scripts/Battle/Combat/AggroChaseMath.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `NativeArray`/`NativeList` → 배열 + 호출자 스크래치, `Allocator.Temp` 제거.
//   ⚠ `ResolveTileRange` 의 「도발 프로파일」 인자는 그대로 두었다 — 도발 대상이 무기 없는
//   적일 때 사거리의 출처가 프로파일이고, 그 규칙은 unit 3 에서 프로파일이 생겨도 안 바뀐다.
using Unity.Mathematics;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore.Move
{
    // 어그로 추격의 목적지 후보 / 도달 가능 판정.
    //
    // **새 이동 알고리즘을 만들지 않는다.** 목적지 BFS(`FlowFieldBuilder`) · 도달 불가 판정 ·
    // cardinal 하강(`FlowRecovery`)을 전부 재사용한다. 그리디 스텝은 금지다 — 직선 greedy 는
    // 벽 고착(좀비버그)으로 이미 폐기됐다.
    public static class AggroChaseMath
    {
        public const int NoAttack = -1;

        // 적의 유효 공격 칸 사거리. 둘 다 없으면 `NoAttack` — 이 적은 가디언을 때릴 수단이
        // 없으므로 어그로 획득을 거부한다(구 「공격 수단 없으면 추격 고착」의 원천 차단).
        public static int ResolveTileRange(bool hasAttack, float attackRange, bool hasProfile, float profileRange)
        {
            if (hasAttack) return GridMath.RangeToTiles(attackRange);
            if (hasProfile) return GridMath.RangeToTiles(profileRange);
            return NoAttack;
        }

        // 「사격 칸에 도착했는데 월드 사거리 밖」일 때 대상 쪽으로 미는 cardinal(M4).
        // 소스가 칸 디스크(체비셰프)인데 발사가 월드 원이라, 원이 잘라낸 모서리에서 생기는
        // 구간을 이동 쪽에서 닫는다.
        //
        // 대각을 쓰지 않는 이유는 순찰 보정과 같다 — 8-이웃 성분이 대각 코너 슬립에 걸린다.
        // 지배축을 줄이는 것이 곧 거리를 줄이는 것이므로 cardinal 로 충분하고, 지배축이
        // 막히면 호출부가 `secondary` 로 폴백한다.
        public static void CloseInCardinals(float dx, float dz, out float2 primary, out float2 secondary)
        {
            bool xDominant = math.abs(dx) >= math.abs(dz);
            float2 xStep = new float2(dx >= 0f ? 1f : -1f, 0f);
            float2 zStep = new float2(0f, dz >= 0f ? 1f : -1f);
            primary = xDominant ? xStep : zStep;
            secondary = xDominant ? zStep : xStep;
        }

        // 대상 칸 기준 「사거리를 만족하는 걷는 칸」 집합을 소스로 추격 거리장을 굽는다.
        // 반환 = 소스 수(0 = 목적지 후보 없음 → 거부). `outDist[적 칸] == int.MaxValue` = 도달 불가 → 거부.
        //
        // ⚠ **여기서 소스를 원으로 좁히지 말 것** — 사거리 1 이면 칸 전체가 원 안인 소스가
        // 하나도 없어 어그로가 통째로 거부된다. 모서리 구간은 `CloseInCardinals` 가 닫는다.
        public static int BuildChaseField(byte[] walkMask, int2 gridSize, int2 targetCell, int tileRange,
                                          int2[] sourceScratch, float2[] tempFlow, int[] outDist,
                                          CellQueue queue)
        {
            var one = new int2[1];
            one[0] = targetCell;
            int count = FlowFieldBuilder.CollectDefenderSources(
                walkMask, gridSize, one, 1, math.max(1, tileRange), sourceScratch);
            if (count == 0)
            {
                for (int i = 0; i < outDist.Length; i++) outDist[i] = int.MaxValue;
                return 0;
            }
            FlowFieldBuilder.BuildFromSources(walkMask, gridSize, sourceScratch, count,
                                              tempFlow, outDist, queue);
            return count;
        }
    }
}
