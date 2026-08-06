using System.Collections.Generic;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm F — 가디언의 공격 대상 후보. 구 `AggroCandidate` 이식.
    ///
    /// <see cref="cell"/> 은 Chebyshev 사거리 게이트용, <see cref="pos"/> 는 XZ 거리 정렬용이다
    /// (공격 루프의 `atkCell`/`atkPos` 이원화와 같은 이유 — 사거리는 격자, 순위는 연속).
    /// </summary>
    public struct AggroCandidate
    {
        public SimInt2 cell;
        public SimVec3 pos;
        /// 이미 어그로된 적인가(선점 상태).
        public bool aggroed;
        /// 등거리 동률 tiebreak — 이 축이 없으면 후보 배열 순서가 승자를 정한다.
        public int simId;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm F — 가디언이 **누구를 때릴지**. 구 `AggroTargeting` 이식.
    ///
    /// 히트 모델 자석의 핵심: 여유가 있으면 아직 안 끌린 적을 **우선** 때려 신규 팩을 흡수하고,
    /// 상한이 차면 겹친 어그로 팩을 정리한다. 순수 기하 — 시스템도 프레임도 엔티티도 모른다.
    /// 후보는 **인덱스로만** 참조된다.
    /// </summary>
    public static class AggroTargeting
    {
        /// <summary>
        /// 이번 공격이 때릴 후보 인덱스를 <paramref name="outIdx"/> 에 채우고 개수를 돌려준다.
        /// `held &lt; capacity` → 비-어그로 최근접 우선 채움 + 부족분 일반 최근접.
        /// `held &gt;= capacity` → 일반 최근접(겹친 팩 정리).
        /// </summary>
        public static int SelectTargets(
            SimInt2 gCell, SimVec3 gPos, int tileRange, int held, int capacity,
            List<AggroCandidate> cands, int[] outIdx, int maxTargets)
        {
            if (maxTargets <= 0 || tileRange < 0) return 0;

            int count = 0;
            // Pass A — 여유가 있으면 **비-어그로만으로** 먼저 채운다(신규 팩 흡수).
            if (held < capacity)
                count = FillNearest(gCell, gPos, tileRange, cands, outIdx, maxTargets, count, freshOnly: true);
            // Pass B — 남은 슬롯을 일반 최근접으로 채운다(이미 뽑힌 인덱스 제외).
            count = FillNearest(gCell, gPos, tileRange, cands, outIdx, maxTargets, count, freshOnly: false);
            return count;
        }

        private static int FillNearest(
            SimInt2 gCell, SimVec3 gPos, int tileRange,
            List<AggroCandidate> cands, int[] outIdx, int maxTargets, int count, bool freshOnly)
        {
            while (count < maxTargets)
            {
                int best = -1;
                float bestSq = float.MaxValue;
                int bestSimId = int.MaxValue;
                for (int i = 0; i < cands.Count; i++)
                {
                    if (AlreadyPicked(outIdx, count, i)) continue;
                    var c = cands[i];
                    if (freshOnly && c.aggroed) continue;
                    if (!TileAoe.IsInTileRange(c.cell, gCell, tileRange)) continue;
                    float dx = c.pos.x - gPos.x;
                    float dz = c.pos.z - gPos.z;
                    float d2 = dx * dx + dz * dz;
                    // ⚠ 등거리 동률은 **낮은 simId**. 이 축이 없으면 같은 판이 실행마다 갈린다.
                    if (d2 < bestSq || (d2 == bestSq && c.simId < bestSimId))
                    {
                        bestSq = d2;
                        bestSimId = c.simId;
                        best = i;
                    }
                }
                if (best < 0) break;
                outIdx[count++] = best;
            }
            return count;
        }

        private static bool AlreadyPicked(int[] outIdx, int count, int i)
        {
            for (int k = 0; k < count; k++)
                if (outIdx[k] == i) return true;
            return false;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 arm F — 넉업 띄우기 원샷 신호. 구 `KnockupVisualEvent` 이식.
    ///
    /// **왜 전용 채널인가**: 심에서 넉업의 실체는 **짧은 Stun** 이고, Stun 은 다른 출처와 구분되지
    /// 않는다. 뷰가 `CcEffect.kind == Stun` 을 보고 띄우면 일반 스턴까지 같이 떠오른다.
    /// 그래서 "누구를 띄웠는가" 는 넉업을 **건 쪽**이 직접 신호한다.
    /// </summary>
    public struct KnockupVisualEvent
    {
        public SimEntityId target;
        /// 떠 있는 시간 = 스턴 시간(같은 값이어야 착지와 해제가 맞는다).
        public float durationSec;
        /// 뷰 공간 최고 높이. **sim-Y 가 아니다** — sim 은 나르기만 한다.
        public float height;
    }
}
