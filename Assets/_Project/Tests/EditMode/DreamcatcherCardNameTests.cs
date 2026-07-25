using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-data-hygiene unit 3 — displayName is a short UI summary;
    // ids and effect data remain the stable source of truth.
    public class DreamcatcherCardNameTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";

        private static readonly Dictionary<string, string> ExpectedNames = new()
        {
            { "active_meteor", "운석" },
            { "active_portal", "포탈" },
            { "active_power_surge", "공격폭증" },
            { "active_rapid_fire", "속사" },
            { "active_slow_field", "감속장" },
            { "active_tornado", "회오리" },
            { "all_atk", "올딜" },
            { "all_move", "올이속" },
            { "bouncy_bead", "튕구슬" },
            { "sub_butterfly_dream", "나비꿈" },
            { "calamity_heart", "시한폭탄" },
            { "cornered_burst", "궁지폭발" },
            { "corpse_burst", "시체폭발" },
            { "cost1_as", "1코속" },
            { "cost1_hp", "1코체" },
            { "cracked_grail", "피값딜" },
            { "devouring_craving", "킬속" },
            { "ember_bite", "출혈" },
            { "eye_on_the_end", "우선조준" },
            { "farewell", "사망폭발" },
            { "sub_fattened_offering", "제물표식" },
            { "frost_arrow", "빙결" },
            { "frostbite", "동상" },
            { "gale_shove", "밀치기" },
            { "guardian_as", "가디언속" },
            { "guardian_fortress", "가디언벽" },
            { "guardian_hp", "가디언체" },
            { "heavy_strike", "강타" },
            { "sub_incubus_pact", "희생계약" },
            { "last_flame", "불꽃폭주" },
            { "last_stand", "빈사폭주" },
            { "lullaby_dart", "자장가" },
            { "nightmare_afterglow", "킬딜" },
            { "poke_needle", "관통침" },
            { "ranger_as", "레인저속" },
            { "ranger_atk", "레인저딜" },
            { "ranger_hp", "레인저체" },
            { "shatter_hymn", "CC딜" },
            { "shield_burst", "실드폭발" },
            { "shield_lull", "실드수면" },
            { "slow_awakening", "공속각성" },
            { "thornmail", "가시반격" },
            { "tremor_plate", "진동갑주" },
        };

        [Test]
        public void AllDreamcatcherCards_UseTheApprovedShortNames()
        {
            var actual = new Dictionary<string, DreamcatcherCard>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(path);
                if (card != null) actual[card.id] = card;
            }

            Assert.AreEqual(ExpectedNames.Count, actual.Count,
                "카드가 추가/누락되면 spec 매핑과 이름 테스트를 함께 갱신해야 한다.");
            Assert.AreEqual(ExpectedNames.Count,
                new HashSet<string>(ExpectedNames.Values).Count,
                "축약 displayName은 카드 간 중복이 없어야 한다.");

            foreach (var pair in ExpectedNames)
            {
                Assert.IsTrue(actual.ContainsKey(pair.Key), $"card id missing: {pair.Key}");
                Assert.AreEqual(pair.Value, actual[pair.Key].displayName, pair.Key);
                Assert.LessOrEqual(pair.Value.Length, 5, $"name too long: {pair.Key}");
            }
        }
    }
}
