using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Core
{
    // gift-phase unit 1 — 선물 이벤트/카드 선택의 순수 결정론 로직.
    // plain 입력 → plain 출력(아키텍처 중립, 제약 10). 셔플은 하지 않는다:
    // 최종 12장 순서는 DreamcatcherCycleDeck 생성자의 단일 Fisher-Yates 에 위임하고,
    // 여기서는 "어떤 2장을 붙일지"만 결정론적으로 고른다. EditMode 단위 테스트 대상.
    public static class GiftDeckComposer
    {
        // 매치 시드에서 파생하되 다른 시드 소비(덱 셔플 등)와 상관되지 않도록 salt.
        private const int KindSalt = 0x51F7;
        private const int RimSalt = 0x2C9B;

        // 가중 랜덤으로 Lucid vs Rim 선택. 같은 시드 → 같은 결과.
        public static GiftKind PickKind(int seed, float lucidWeight, float rimWeight)
        {
            float lw = lucidWeight > 0f ? lucidWeight : 0f;
            float rw = rimWeight > 0f ? rimWeight : 0f;
            float total = lw + rw;
            if (total <= 0f) return GiftKind.Lucid;
            var rng = new System.Random(seed ^ KindSalt);
            double r = rng.NextDouble() * total;
            return r < lw ? GiftKind.Lucid : GiftKind.Rim;
        }

        // 무의식 풀에서 시드로 count 장 추출(중복 없이, 부분 Fisher-Yates).
        // 풀<count 면 fallback 에서 부족분 채움(중복 허용) — 안전장치이며
        // unit 2 무의식 저작 후엔 거의 미발동. 같은 시드 → 같은 결과.
        public static List<DreamcatcherCard> PickRim(
            IReadOnlyList<DreamcatcherCard> pool,
            IReadOnlyList<DreamcatcherCard> fallback,
            int count, int seed)
        {
            var result = new List<DreamcatcherCard>(count > 0 ? count : 0);
            if (count <= 0) return result;
            var rng = new System.Random(seed ^ RimSalt);

            if (pool != null && pool.Count > 0)
            {
                var work = new List<DreamcatcherCard>(pool);
                int take = System.Math.Min(count, work.Count);
                for (int i = 0; i < take; i++)
                {
                    int j = i + rng.Next(work.Count - i);
                    (work[i], work[j]) = (work[j], work[i]);
                    if (work[i] != null) result.Add(work[i]);
                }
            }

            // 부족분 fallback(중복 허용). all-null 방어를 위해 시도 횟수 캡.
            int guard = 0;
            while (result.Count < count && fallback != null && fallback.Count > 0 && guard++ < count * 8)
            {
                var pick = fallback[rng.Next(fallback.Count)];
                if (pick != null) result.Add(pick);
            }
            return result;
        }
    }
}
