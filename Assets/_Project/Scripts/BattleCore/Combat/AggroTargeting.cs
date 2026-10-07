// salvaged from Assets/_Project/Scripts/Battle/Combat/AggroTargeting.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `NativeArray` → 배열 + count · `AttackShapeBaked`/`AttackReach` 를 코어 사본으로.
//   선정 규칙과 그 이력 주석은 그대로다 — 「가정하던 곳이 안 고쳐졌다」는 교훈이 여기 살아 있다.
using Unity.Mathematics;

namespace Wassup.BattleCore.Combat
{
    // 가디언의 공격 타겟 선정(누구를 때릴지). 히트 모델 자석의 핵심이다:
    // 여유가 있으면 **아직 안 끌린 적**을 우선 때려 신규 팩을 흡수하고, 상한이 차면
    // 이미 겹친 팩을 정리한다.
    //
    // ⚠ **이 파일이 정하는 것은 «누구를 먼저 고르나» 뿐이다. «어디까지 닿나» 는 정하지 않는다** —
    // 사거리는 발사 게이트와 **같은 술어**(`AttackReach.InReach`)를 지난다.
    //
    // 왜 그것이 제약으로 승격됐나(C13 의 근거): 종전엔 여기서 「후보 칸 ↔ 가디언 칸, 사거리」
    // 칸 자로 걸렀다. 그 술어는 **공격자가 딱 한 칸을 차지한다(몸 0.5)** 를 상수로 박으므로
    // 발사 게이트(`사거리 + 내 몸 + 상대 몸`)와 답이 갈렸다:
    //
    //   | 공격자 몸 | 게이트 도달 | 옛 선정 도달 | 사각지대(휘두르는데 피해 0) |
    //   |---|---|---|---|
    //   | 0.5 (1×1) | 1.75 | 1.5 | 0.25 |
    //   | 1.0 (2×2) | 2.25 | 1.5 | 0.75 |
    //   | **1.5 (3×2)** | **2.75** | 1.5 | **1.25 — 자기 몸 가장자리가 이미 밖** |
    //
    // 오래 살아남은 이유: 몸이 **상수 가정에서 데이터로 승격**됐는데, 그 값을 *쓰던* 곳이 아니라
    // **가정하던** 곳이 안 고쳐졌다. 흔적이 리터럴 `0.5` 뿐이라 grep 이 못 잡는다.
    public struct AggroCandidate
    {
        /// <summary>월드 위치 — 사거리 판정 + 최근접 정렬 공용.</summary>
        public float3 Pos;
        /// <summary>이 후보의 몸(타일). 게이트와 **같은 항**이다.</summary>
        public float BodyRadius;
        /// <summary>이미 어그로된 적인가(선점 상태).</summary>
        public bool Aggroed;
    }

    public static class AggroTargeting
    {
        /// <summary>
        /// `outIdx` 에 이번 공격이 때릴 후보 인덱스를 채우고 개수를 반환한다.
        /// `held &lt; capacity` → 비-어그로 최근접 우선 + 부족분 일반 최근접.
        /// `held &gt;= capacity` → 일반 최근접(겹친 팩 정리).
        /// </summary>
        public static int SelectTargets(
            float3 gPos, float rangeTiles, float tileSize, float selfBodyRadius,
            int held, int capacity, in AttackShapeBaked shape,
            AggroCandidate[] cands, int candCount, int[] outIdx, int maxTargets)
        {
            if (maxTargets <= 0 || rangeTiles < 0f) return 0;
            if (maxTargets > outIdx.Length) maxTargets = outIdx.Length;

            int count = 0;
            // Pass A — 여유가 있으면 비-어그로만으로 먼저 채운다(신규 팩 흡수).
            if (held < capacity)
                count = FillNearest(gPos, rangeTiles, tileSize, selfBodyRadius, in shape,
                                    cands, candCount, outIdx, maxTargets, count, freshOnly: true);
            // Pass B — 남은 슬롯을 일반 최근접으로 채운다(이미 뽑힌 인덱스 제외).
            count = FillNearest(gPos, rangeTiles, tileSize, selfBodyRadius, in shape,
                                cands, candCount, outIdx, maxTargets, count, freshOnly: false);
            return count;
        }

        private static int FillNearest(
            float3 gPos, float rangeTiles, float tileSize, float selfBodyRadius, in AttackShapeBaked shape,
            AggroCandidate[] cands, int candCount, int[] outIdx, int maxTargets, int count, bool freshOnly)
        {
            while (count < maxTargets)
            {
                // 주 대상(outIdx[0])은 **원**에서 뽑고, 그 뒤부터는 주 대상을 향한 실제 방향의
                // 도형 안에서만. Pass A/B 모두 같은 규칙 — 방향은 `outIdx[0]` 하나다.
                bool shaped = count > 0;
                float2 dir = shaped
                    ? new float2(cands[outIdx[0]].Pos.x - gPos.x, cands[outIdx[0]].Pos.z - gPos.z)
                    : float2.zero;
                int best = -1;
                float bestSq = float.MaxValue;
                for (int i = 0; i < candCount; i++)
                {
                    if (AlreadyPicked(outIdx, count, i)) continue;
                    var c = cands[i];
                    if (freshOnly && c.Aggroed) continue;
                    // 발사 게이트와 **같은 본체**. 여기서 모양을 다시 그리지 않는다.
                    bool reach = shaped
                        ? AttackReach.InReachShaped(gPos, c.Pos, rangeTiles, tileSize, selfBodyRadius,
                                                    c.BodyRadius, in shape, dir)
                        : AttackReach.InReach(gPos, c.Pos, rangeTiles, tileSize, selfBodyRadius, c.BodyRadius);
                    if (!reach) continue;
                    float dx = c.Pos.x - gPos.x;
                    float dz = c.Pos.z - gPos.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestSq)    // strict < → 동률은 낮은 인덱스가 이긴다(결정론)
                    {
                        bestSq = d2;
                        best = i;
                    }
                }
                if (best < 0) break;
                outIdx[count++] = best;
            }
            return count;
        }

        private static bool AlreadyPicked(int[] outIdx, int count, int idx)
        {
            for (int k = 0; k < count; k++) if (outIdx[k] == idx) return true;
            return false;
        }
    }
}
