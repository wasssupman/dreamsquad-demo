using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat.Projectile
{
    // dreamcatcher-attack-mod-bounce unit 1 — the retarget DECISION for a
    // bouncing projectile: nearest living candidate the impact can reach
    // (distance-based edge-to-edge since distance-based-range unit 18 — the old
    // Chebyshev cell radius is retired), excluding the just-hit target. Pure
    // geometry — no world, no system, no frame, no Entity. This is the
    // architecture-neutral "logic" layer (EditMode-testable with a plain float3
    // array); the ImpactSystem arm that calls it is the thin ECS glue (unit 2).
    // Mirrors SkillMath.InBodyReach / BallisticArc.ArcPosition — same
    // pure-function-plus-EditMode pattern.
    public static class BounceRetarget
    {
        // Returns the positions index of the next bounce target, or -1 if none.
        // Skips excludeIndex (previous target); keeps candidates within tileRange
        // of hitPos (edge-to-edge body reach — unit 18); picks the smallest XZ
        // squared distance. Ties resolve to the lower index (snapshot order =
        // deterministic). tileRange <= 0 → -1.
        public static int FindNext(
            float3 hitPos, int excludeIndex,
            NativeArray<float3> positions,
            int tileRange, float tileSize, int2 gridSize, float3 origin)
            => FindNext(hitPos, excludeIndex, positions, default, 0,
                tileRange, tileSize, gridSize, origin);

        // waypoint-routing unit 4 rev 4 — same geometry with an optional
        // traversal-layer eligibility array aligned to positions. Keeping the
        // original overload preserves every legacy producer/test (mask 0).
        public static int FindNext(
            float3 hitPos, int excludeIndex,
            NativeArray<float3> positions,
            NativeArray<byte> targetTraversalLayers,
            byte attackTargetLayers,
            int tileRange, float tileSize, int2 gridSize, float3 origin)
            => FindNext(hitPos, excludeIndex, positions, targetTraversalLayers, attackTargetLayers,
                        default, 0, tileRange, tileSize, gridSize, origin);

        // skill-layer-migration unit 8 — **진영 적격 배열을 더한다.** 층 오버로드와 같은
        // 모양이고 같은 이유다: 후보 풀이 이제 «적만» 이 아니라 **양 진영**이라, 누구를
        // 튕겨 갈 수 있는지를 호출자가 말해야 한다.
        //
        // ⚠ 마스크 0 = 「진영을 안 본다」 = 옛 동작. 기존 producer/test 는 위 오버로드로
        // 그대로 산다(층 오버로드가 세운 규약을 그대로 따른다).
        public static int FindNext(
            float3 hitPos, int excludeIndex,
            NativeArray<float3> positions,
            NativeArray<byte> targetTraversalLayers,
            byte attackTargetLayers,
            NativeArray<int> candidateFactions,
            int wantedFactionMask,
            int tileRange, float tileSize, int2 gridSize, float3 origin,
            // 리뷰 H2 (unit 18) — 후보 몸 반경(타일). 미생성(default) = 점(0) — 기존 호출부 무회귀.
            NativeArray<float> candidateBodyRadii = default)
        {
            if (tileRange <= 0) return -1;
            // unit 18 — 위치 기반(셀 양자화 제거). gridSize/origin 인자는 호출부 보존을 위해
            // 남지만 더는 읽지 않는다.
            float invT = tileSize > 1e-6f ? 1f / tileSize : 1f;
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < positions.Length; i++)
            {
                if (i == excludeIndex) continue;
                if (targetTraversalLayers.IsCreated
                    && !Wassup.Data.PlacementLayers.CanTarget(
                        attackTargetLayers, targetTraversalLayers[i])) continue;
                // 진영 게이트. 마스크 0 이면 옛 동작(적 전용 풀 전제)을 그대로 둔다.
                if (wantedFactionMask != 0 && candidateFactions.IsCreated
                    && (candidateFactions[i] & wantedFactionMask) == 0) continue;
                float3 pos = positions[i];
                // unit 18 — 위치 기반 + 후보 몸(리뷰 H2: 다른 5곳과 같은 자 — 큰 몸은 더 멀리서 걸린다).
                float bodyR = candidateBodyRadii.IsCreated && i < candidateBodyRadii.Length
                    ? candidateBodyRadii[i] : 0f;
                if (!Wassup.Skills.SkillMath.ReachFromCell(
                        (pos.x - hitPos.x) * invT, (pos.z - hitPos.z) * invT,
                        tileRange, bodyR)) continue;
                float dx = pos.x - hitPos.x;
                float dz = pos.z - hitPos.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestSq) // strict < → lower index wins ties (deterministic)
                {
                    bestSq = d2;
                    best = i;
                }
            }
            return best;
        }
    }
}
