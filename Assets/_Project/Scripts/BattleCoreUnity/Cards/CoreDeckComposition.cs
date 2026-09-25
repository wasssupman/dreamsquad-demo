using System.Collections.Generic;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.BattleCoreUnity.Cards
{
    // battle-core-rebuild unit 7c — **이 판의 드림캐쳐 덱을 판 밖에서 고른다**(← 옛 `DreamcatcherHandController.BuildDeck`
    // 의 조립 몫 D2·D3·D4 + `SkillLoadoutController.Roll` S1~S5 — `rule-holders.md` 가 둘 다 `MatchDefinitionBuilder` 쪽으로
    // 귀속한 행이다). 여기는 **어느 카드 에셋이 덱에 드나**만 정하고, 섞기·손패·순환은 코어 `HandDeck` 이 한다(섞기는 한 곳 — D2).
    //
    //   · 부착 10 = 프로필의 **확정 덱**(`CommittedDeck`)을 카탈로그로 푼 것. 검증(`DeckRules.Validate`)에 실패하면 **비어 있다** —
    //     기본 덱 폴백은 없앴다(D3 · 사용자 결정 2026-07-15 — 기본 덱이 모든 배치 유닛을 상시 버프하던 사고).
    //   · 액티브 2 = 스킬 풀에서 **판의 시드로** 굴린 것을 감싸는 액티브 카드(D4 — 감싸는 카드가 없으면 그 장만 빠진다).
    //     ⚠ 옛 롤은 시드 0 이면 **벽시계**로 새로 만들었다(S2) — 그러면 「같은 modeId+seed = 같은 판」이 액티브 2장에서 깨진다.
    //     그래서 판의 시드에서 파생한다(`rule-holders.md` S2 비고). 난수는 `Unity.Mathematics.Random`(코어 `HandDeck` 과 같은 결 —
    //     런타임이 바뀌어도 순열이 같다), 옛 `System.Random` 순열과는 다르다(골든·밸런스가 특정 순열에 안 기댄다).
    //   · 숨긴 카드의 스킬은 풀에서 뺀다(S4 · 옛 `SkillLoadoutController.FilterHiddenSkills` 의 본문을 8c 에서 **이리 옮겼다** — 순수 함수, 옛 쪽은 위임).
    public static class CoreDeckComposition
    {
        /// <summary>굴림 시드 소금 — 판 시드를 그대로 쓰면 `HandDeck` 의 섞기와 같은 첫 난수를 공유한다(서로 상관되지 않게).</summary>
        private const uint RollSalt = 0x9E3779B9u;

        public static List<DreamcatcherCard> Compose(PlayerProfileSO profile, DreamcatcherCardCatalog catalog,
                                                     IReadOnlyList<SkillData> activePool, int activeCount,
                                                     IReadOnlyList<DreamcatcherCard> activeCards, int seed,
                                                     System.Action<string> warn = null)
        {
            var result = ResolveAttachDeck(profile, catalog);
            var picked = RollActives(FilterHiddenSkills(
                                         activePool ?? System.Array.Empty<SkillData>(), catalog),
                                     activeCount, seed);
            for (int i = 0; i < picked.Count; i++)
            {
                var card = FindActiveCard(activeCards, picked[i]);
                if (card != null) result.Add(card);
                else warn?.Invoke($"[CoreDeckComposition] 굴린 스킬 '{picked[i].id}' 를 감싸는 액티브 카드가 없다 — 그 장은 빠진다(D4).");
            }
            return result;
        }

        /// <summary>S4 — 카탈로그에 이 스킬을 감싸는 액티브 카드가 있고 그 **전부**가 숨김(visible 0)이면 풀에서 뺀다.
        /// 감싸는 카드가 없는 스킬은 남긴다. null 풀 = 빈 목록 · null 카탈로그 = 무필터(옛 `SkillLoadoutController` 그대로).</summary>
        public static List<SkillData> FilterHiddenSkills(IEnumerable<SkillData> pool, DreamcatcherCardCatalog catalog)
        {
            var result = new List<SkillData>();
            if (pool == null) return result;
            var cards = catalog != null ? catalog.cards : null;
            foreach (var skill in pool)
            {
                if (skill == null || cards == null) { result.Add(skill); continue; }
                bool wrapped = false, anyVisible = false;
                for (int i = 0; i < cards.Length; i++)
                {
                    var c = cards[i];
                    if (c == null || c.type != CardType.Active || c.skill != skill) continue;
                    wrapped = true;
                    if (c.visible != 0) { anyVisible = true; break; }
                }
                if (!wrapped || anyVisible) result.Add(skill);
            }
            return result;
        }

        /// <summary>D3 — 확정 덱(검증 통과)만. 없거나 무효면 빈 목록.</summary>
        public static List<DreamcatcherCard> ResolveAttachDeck(PlayerProfileSO profile, DreamcatcherCardCatalog catalog)
        {
            var result = new List<DreamcatcherCard>();
            var save = profile != null && profile.profile != null ? profile.profile.CommittedDeck() : null;
            if (save == null || catalog == null || !DeckRules.Validate(save.cardIds, catalog, out _)) return result;
            foreach (var id in save.cardIds)
            {
                var card = catalog.ById(id);
                if (card != null) result.Add(card);
            }
            return result;
        }

        /// <summary>S1·S3 — 풀에서 `count` 장을 부분 셔플로. 빈 풀·0 장이면 빈 목록. 같은 시드 = 같은 결과.</summary>
        public static List<SkillData> RollActives(IReadOnlyList<SkillData> pool, int count, int seed)
        {
            var picked = new List<SkillData>();
            if (pool == null || pool.Count == 0 || count <= 0) return picked;
            var working = new List<SkillData>(pool.Count);
            for (int i = 0; i < pool.Count; i++) if (pool[i] != null) working.Add(pool[i]);
            uint s = unchecked((uint)seed ^ RollSalt);
            var rng = new Unity.Mathematics.Random(s != 0 ? s : 1u);
            int take = System.Math.Min(count, working.Count);
            for (int i = 0; i < take; i++)
            {
                int j = i + rng.NextInt(0, working.Count - i);
                (working[i], working[j]) = (working[j], working[i]);
                picked.Add(working[i]);
            }
            return picked;
        }

        private static DreamcatcherCard FindActiveCard(IReadOnlyList<DreamcatcherCard> cards, SkillData skill)
        {
            if (cards == null || skill == null) return null;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] != null && cards[i].type == CardType.Active && cards[i].skill == skill) return cards[i];
            return null;
        }
    }
}
