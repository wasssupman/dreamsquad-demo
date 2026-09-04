using Unity.Collections;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // aggro-targeting Unit 9 — 정의 계층. 가디언의 공격 타겟 선정(누구를 때릴지).
    // 히트 모델의 자석 작동 핵심: 여유가 있으면 아직 안 끌린 적을 우선 때려 신규
    // 팩을 흡수하고, 상한이 차면 겹친 어그로 팩을 정리한다. 순수 기하 — no system,
    // no frame, no Entity. Candidate 는 인덱스로만 참조된다(BounceRetarget 선례).
    //
    // ⚠ **이 파일이 정하는 것은 «누구를 먼저 고르나» 뿐이다. «어디까지 닿나» 는
    // 정하지 않는다** — 사거리는 발사 게이트와 **같은 술어**(`AttackReach.InReach`)를 지난다.
    //
    // distance-based-range unit 22(2026-09-04 사용자 버그 보고 «배스티온이 공격은 하는데
    // 피해가 0») — 종전엔 여기서 `TileAoe.IsInRadius(후보 칸, 가디언 칸, 사거리)` 로 걸렀다.
    // 그 술어는 **공격자가 딱 한 칸을 차지한다(몸 0.5)** 를 상수로 박아 두므로,
    // 발사 게이트(몸 기반 `사거리 + 내 몸 + 상대 몸`)와 답이 갈렸다:
    //
    //   | 공격자 몸 | 게이트 도달 | 옛 선정 도달 | 사각지대(휘두르는데 피해 0) |
    //   |---|---|---|---|
    //   | 0.5 (1×1) | 1.75 | 1.5 | 0.25 |
    //   | 1.0 (2×2 가디언·실드셔틀) | 2.25 | 1.5 | 0.75 |
    //   | **1.5 (배스티온 3×2)** | **2.75** | 1.5 | **1.25 — 자기 몸 가장자리가 이미 밖** |
    //
    // 왜 오래 살아남았나: 이 줄은 unit 4b 가 **보고 고친 줄**이다(사각→원). 그때는
    // 「어그로의 중심은 유닛의 칸」이 **참**이었다 — 몸이 필드로만 있고 저작값이 0 이었다.
    // 하루 뒤 unit 12 가 아군 몸을 footprint 파생값으로 만들며 그 전제가 깨졌는데,
    // **전제를 근거로 내린 이 결정은 재검토되지 않았다.** 그 결정은 diff 가 아니라
    // spec 문서에 적혀 있었고, 이후 이 줄을 건드린 diff 는 0 이라 리뷰가 볼 기회도 없었다.
    // 교훈: 어떤 값이 상수 가정에서 **데이터로 승격**되면, 그 값을 *쓰던* 곳이 아니라
    // 그 값을 **가정하던** 곳 전부가 잠재 결함이 된다(흔적은 `0.5` 리터럴뿐이라 grep 이 못 잡는다).
    public struct AggroCandidate
    {
        public float3 pos;        // 월드 위치 — 사거리 판정 + 최근접 정렬 공용
        public float bodyRadius;  // 이 후보의 몸(타일). 게이트와 같은 항이다
        public bool aggroed;      // 이미 어그로된 적인가(선점 상태)
    }

    public static class AggroTargeting
    {
        // outIdx 에 이번 공격이 때릴 candidate 인덱스를 채우고 개수를 반환.
        // held < capacity → 비-어그로 최근접 우선 채움 + 부족분 일반 최근접.
        // held >= capacity → 일반 최근접(겹친 어그로 팩 정리).
        //
        // `gPos`·`selfBodyRadius`·`rangeTiles`·`tileSize` 는 발사 게이트가 쓰는 것과 **같은 인자**다.
        public static int SelectTargets(
            float3 gPos, float rangeTiles, float tileSize, float selfBodyRadius,
            int held, int capacity,
            NativeArray<AggroCandidate> cands, NativeArray<int> outIdx)
        {
            int maxTargets = outIdx.Length;
            if (maxTargets <= 0 || rangeTiles < 0f) return 0;

            int count = 0;
            // Pass A — 여유가 있으면 비-어그로만으로 먼저 채운다(신규 팩 흡수).
            if (held < capacity)
                count = FillNearest(gPos, rangeTiles, tileSize, selfBodyRadius, cands, outIdx, count, freshOnly: true);
            // Pass B — 남은 슬롯을 일반 최근접으로 채운다(이미 뽑힌 인덱스 제외).
            count = FillNearest(gPos, rangeTiles, tileSize, selfBodyRadius, cands, outIdx, count, freshOnly: false);
            return count;
        }

        private static int FillNearest(
            float3 gPos, float rangeTiles, float tileSize, float selfBodyRadius,
            NativeArray<AggroCandidate> cands, NativeArray<int> outIdx, int count, bool freshOnly)
        {
            int maxTargets = outIdx.Length;
            while (count < maxTargets)
            {
                int best = -1;
                float bestSq = float.MaxValue;
                for (int i = 0; i < cands.Length; i++)
                {
                    if (AlreadyPicked(outIdx, count, i)) continue;
                    var c = cands[i];
                    if (freshOnly && c.aggroed) continue;
                    // unit 22 — 발사 게이트와 **같은 본체**. 여기서 모양을 다시 그리지 않는다.
                    if (!AttackReach.InReach(gPos, c.pos, rangeTiles, tileSize,
                                             selfBodyRadius, c.bodyRadius)) continue;
                    float dx = c.pos.x - gPos.x;
                    float dz = c.pos.z - gPos.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestSq) // strict < → lower index wins ties (deterministic)
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

        private static bool AlreadyPicked(NativeArray<int> outIdx, int count, int idx)
        {
            for (int k = 0; k < count; k++) if (outIdx[k] == idx) return true;
            return false;
        }
    }
}
