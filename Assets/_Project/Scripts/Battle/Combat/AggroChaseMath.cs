using Unity.Collections;
using Unity.Mathematics;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;

namespace Wassup.Battle.Combat
{
    // aggro-tile-chase unit 0 — 정의 계층(아키텍처 무참조). 어그로 추격의 목적지 후보/
    // 도달가능 판정 순수 계산. BFS 본체는 FlowFieldBuilder(boss-defender-field) 재사용,
    // 하강은 FlowRecovery.RecoveryDir 재사용 — 여기는 해석과 조합만 담는다.
    public static class AggroChaseMath
    {
        public const int NoAttack = -1;

        // 적의 유효 공격 tileRange: native AttackState 우선, 없으면 taunt 프로파일 폴백.
        // 둘 다 없으면 NoAttack — 이 적은 가디언을 때릴 수단이 없으므로 어그로 획득을 거부한다
        // (구 M5 "AttackState 없으면 Chasing 고착"의 근본 차단).
        public static int ResolveTileRange(bool hasAttack, float attackRange, bool hasProfile, float profileRange)
        {
            if (hasAttack) return GridMath.RangeToTiles(attackRange);
            if (hasProfile) return GridMath.RangeToTiles(profileRange);
            return NoAttack;
        }

        // distance-based-range unit 4c — 「사격 칸에 도착했는데 월드 사거리 밖」일 때 가디언
        // 쪽으로 미는 cardinal. 소스가 셀 디스크(체비셰프)인데 발사가 월드 원이라 원이
        // 잘라낸 모서리에서 생기는 구간을 이동 쪽에서 닫는다.
        //
        // 대각을 쓰지 않는 이유는 순찰 보정(`PatrolAreaMath.CloseInDir`)과 같다 — 8-이웃
        // 성분이 대각 코너 슬립에 걸린다. 지배축을 줄이는 것이 곧 거리를 줄이는 것이므로
        // cardinal 로 충분하고, 지배축이 막히면 호출부가 `secondary` 로 폴백한다.
        public static void CloseInCardinals(float dx, float dz, out float2 primary, out float2 secondary)
        {
            bool xDominant = math.abs(dx) >= math.abs(dz);
            float2 xStep = new float2(dx >= 0f ? 1f : -1f, 0f);
            float2 zStep = new float2(0f, dz >= 0f ? 1f : -1f);
            primary   = xDominant ? xStep : zStep;
            secondary = xDominant ? zStep : xStep;
        }

        // 가디언 셀 기준 "사거리를 만족하는 walk 셀" 집합을 소스로 chase dist field 를 굽는다.
        // 반환 = 소스 수(0 = 목적지 후보 없음 → 거부). outDist[enemyCell]==int.MaxValue = 도달 불가 → 거부.
        // ⚠ **「소스 셀 도달 = 발사 가능」은 더 이상 참이 아니다**(distance-based-range unit 4a).
        // 소스는 셀 Chebyshev 디스크인데 발사는 월드 원이라, 원이 잘라낸 모서리에서는
        // 「도착했는데 사거리 밖」이 된다. 그 구간은 이동 쪽 접근 보정이 닫는다
        // (`MovementSystem` 의 `arrivedAtFiringCell` 분기). **여기서 소스를 원으로 좁히지 말 것** —
        // 사거리 1 이면 칸 전체가 원 안인 소스가 하나도 없어 어그로가 통째로 거부된다.
        public static int BuildChaseField(
            NativeArray<byte> walkMask,
            int2 gridSize,
            int2 guardianCell,
            int tileRange,
            NativeArray<float2> tempFlow,
            NativeArray<int> outDist)
        {
            var guardianCells = new NativeArray<int2>(1, Allocator.Temp);
            var sources = new NativeList<int2>((2 * tileRange + 1) * (2 * tileRange + 1), Allocator.Temp);
            try
            {
                guardianCells[0] = guardianCell;
                int count = FlowFieldBuilder.CollectDefenderSources(
                    walkMask, gridSize, guardianCells, tileRange, sources);
                if (count == 0)
                {
                    for (int i = 0; i < outDist.Length; i++) outDist[i] = int.MaxValue;
                    return 0;
                }
                FlowFieldBuilder.BuildFromSources(walkMask, gridSize, sources.AsArray(), tempFlow, outDist);
                return count;
            }
            finally
            {
                guardianCells.Dispose();
                sources.Dispose();
            }
        }
    }
}
