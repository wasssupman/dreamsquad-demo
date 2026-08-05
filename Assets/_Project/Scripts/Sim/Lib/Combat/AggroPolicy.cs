using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/1 — 도발 프로파일. 구 `AggroAttackProfile` 이식.
    /// `AttackState` 가 없는 적이 가디언을 때릴 때 쓰는 폴백 명세다.
    /// </summary>
    public struct AggroAttackProfile
    {
        public float damage;
        public float cooldown;
        public float range;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-F/1 — 어그로 획득/해제 정책. 구 `AggroPolicy` 이식.
    /// 정의 계층(아키텍처 무참조) — `held`/`capacity`/`bool` 만 받는다.
    /// </summary>
    public static class AggroPolicy
    {
        /// capacity 게이트 + **선점**: 아직 안 걸렸고 상한 여유가 있을 때만 획득.
        public static bool CanAcquire(int held, int capacity, bool alreadyAggroed)
            => !alreadyAggroed && held < capacity;

        /// 해제 = 링크 가디언이 살아있지 않음. 단순하지만 **해제 조건의 확장 지점**으로 유지한다.
        public static bool ShouldRelease(bool guardianAlive) => !guardianAlive;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-F/1 — 어그로 추격의 목적지 후보/도달가능 판정.
    /// 구 `AggroChaseMath` 이식. BFS 본체는 <see cref="FlowFieldBuilder"/>, 하강은
    /// <see cref="FlowRecovery"/> 재사용 — 여기는 **해석과 조합만** 담는다.
    ///
    /// ⚠ 스크래치를 인자로 받는 이유는 `PatrolAreaMath` 와 같다(구 `Allocator.Temp` 자리).
    /// </summary>
    public static class AggroChaseMath
    {
        public const int NoAttack = -1;

        /// <summary>
        /// 적의 유효 공격 tileRange: native `AttackState` 우선, 없으면 도발 프로파일 폴백.
        /// ⚠ **둘 다 없으면 `NoAttack`** — 그 적은 가디언을 때릴 수단이 없으므로 어그로 획득을
        /// 거부한다. 이 거부가 없으면 때리지도 못하면서 영원히 쫓는 **Chasing 고착**이 난다.
        /// </summary>
        public static int ResolveTileRange(bool hasAttack, float attackRange,
                                           bool hasProfile, float profileRange)
        {
            if (hasAttack) return GridMath.RangeToTiles(attackRange);
            if (hasProfile) return GridMath.RangeToTiles(profileRange);
            return NoAttack;
        }

        /// <summary>
        /// 가디언 셀 기준 "사거리를 만족하는 walk 셀" 집합을 소스로 chase dist field 를 굽는다.
        /// 반환 = 소스 수(**0 = 목적지 후보 없음 → 거부**). `outDist[적셀] == int.MaxValue`
        /// = 도달 불가 → 거부.
        ///
        /// ⚠ **소스 도달 ⟺ 발사 가능**이 정의상 일치한다 — `CollectDefenderSources` 가
        /// FSM·공격 루프와 **같은 체비셰프 디스크**를 쓰기 때문이다. 메트릭이 갈리면
        /// "도착했는데 못 쏘는" 스톨이 생긴다.
        /// </summary>
        public static int BuildChaseField(byte[] walkMask, SimInt2 gridSize,
                                          SimInt2 guardianCell, int tileRange,
                                          SimVec2[] tempFlow, int[] outDist,
                                          List<SimInt2> sourcesBuffer, ref SimInt2[] sourceArray)
        {
            if (sourceArray.Length < 1) sourceArray = new SimInt2[16];
            sourceArray[0] = guardianCell;

            int count = FlowFieldBuilder.CollectDefenderSources(
                walkMask, gridSize, sourceArray, 1, tileRange, sourcesBuffer);
            if (count == 0)
            {
                for (int i = 0; i < outDist.Length; i++) outDist[i] = int.MaxValue;
                return 0;
            }

            if (sourceArray.Length < sourcesBuffer.Count) sourceArray = new SimInt2[sourcesBuffer.Count];
            for (int i = 0; i < sourcesBuffer.Count; i++) sourceArray[i] = sourcesBuffer[i];
            FlowFieldBuilder.BuildFromSources(walkMask, gridSize, sourceArray, sourcesBuffer.Count,
                                              tempFlow, outDist);
            return count;
        }
    }
}
