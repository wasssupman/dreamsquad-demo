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

        // 가디언 셀 기준 "사거리를 만족하는 walk 셀" 집합을 소스로 chase dist field 를 굽는다.
        // 반환 = 소스 수(0 = 목적지 후보 없음 → 거부). outDist[enemyCell]==int.MaxValue = 도달 불가 → 거부.
        // 소스 셀 도달 ⟺ tile-Chebyshev ≤ tileRange ⟺ 발사 가능 (CollectDefenderSources 가
        // EnemyAiStateSystem/AttackSystem 과 같은 Chebyshev 디스크 — 도달=발사 정의상 일치).
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
