using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // nightmare-whip-aura unit 0 — pulse target pick for AllyMoveSpeedAura:
    // every candidate within Chebyshev tileRange of the host cell, boundary
    // inclusive (same idiom as the TileAoe impact check). Pure math over plain
    // arrays (제약 10 — sim-critical targeting), EditMode-pinned; the caller
    // snapshots the same-faction pool and owns entity identity — host
    // self-exclusion is NOT done here (a same-cell ally must still be hit).
    public static class AuraPulse
    {
        // Fills `results` (cleared on entry — safe to reuse across pulses) with
        // the indices into `candidateCells` within `tileRange` of `hostCell`.
        // Negative tileRange selects nothing (degenerate guard).
        public static void SelectTargets(in NativeArray<int2> candidateCells, int2 hostCell,
                                         int tileRange, ref NativeList<int> results)
            => SelectRing(candidateCells, hostCell, 0, tileRange, ref results);

        // boss-mamemo unit 1 — 도넛(annulus) 선택: `minRange` **미만**은 제외한다.
        // 자장가가 이걸 쓰는 이유는 게임 규칙이다 — 마메모는 `BossTag` 이라 방어유닛을 사냥해
        // 붙어서 때리고, 이 엔진의 수면은 맞으면 풀린다. 그래서 사거리 안을 재우면 **자기
        // 평타로 자기가 깨우는** 자기무효화가 된다. 사거리 밖만 재우면 "앞은 때리고 뒤를
        // 재운다" 는 읽히는 모양이 되고, 규칙 하나로 그 사고가 구조적으로 사라진다.
        //
        // minRange <= 0 이면 기존 전범위 선택과 동치다(whip 오라 무회귀).
        public static void SelectRing(in NativeArray<int2> candidateCells, int2 hostCell,
                                      int minRange, int maxRange, ref NativeList<int> results)
        {
            results.Clear();
            if (maxRange < 0) return;
            for (int i = 0; i < candidateCells.Length; i++)
            {
                int2 d = math.abs(candidateCells[i] - hostCell);
                int cheb = math.max(d.x, d.y);
                if (cheb <= maxRange && cheb >= minRange) results.Add(i);
            }
        }
    }
}
