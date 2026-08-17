using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — ProfileStoreDefaultDeckTests 에서 추출한 실에셋 검증.
    // 시딩/복구 로직 테스트(합성 카탈로그)는 코어 lane 에 남는다.
    public class DefaultDeckAuthoringTests
    {
        private const string DefaultDeckPath =
            "Assets/_Project/Data/Dreamcatcher/DreamcatcherDeck_Default.asset";
        private const string CardCatalogPath =
            "Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset";

        [Test]
        public void AuthoredDefaultDeck_IsExpectedVisibleValidStarter()
        {
            var source = AssetDatabase.LoadAssetAtPath<DreamcatcherDeck>(DefaultDeckPath);
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CardCatalogPath);
            Assert.IsNotNull(source);
            Assert.IsNotNull(catalog);

            // 2026-08-17 재구성 — 옛 기본 덱 10장 중 6장(레인저 3종·가디언 3종)이 시트에서
            // `visible 0` 이 되면서 신규 프로필의 기본 덱이 DeckPrune 에 4장으로 잘렸다.
            // **사용자가 실제로 쓰는 덱(profile 의 deck_1) 10장을 그대로 기본 덱으로 삼는다** —
            // 전부 Unit 이라 Squad 는 0장이다(상한 2, 살아있는 Squad 카드는 CC딜 하나뿐).
            string[] expected =
            {
                "poke_needle", "bouncy_bead", "frost_arrow", "flame_spinner", "severance_meteor",
                "shield_lull", "moth_swarm", "boomerang", "ember_field", "frenzy",
            };
            CollectionAssert.AreEqual(expected, source.cards.Select(c => c != null ? c.id : null));
            Assert.That(source.cards, Has.All.Matches<DreamcatcherCard>(
                c => c != null && c.visible != 0), "기본 덱은 숨김 카드나 null을 포함하면 안 된다");

            var profile = ProfileStore.CreateDefault(null, source, catalog);
            Assert.IsNotNull(profile.CommittedDeck());
            CollectionAssert.AreEqual(expected, profile.CommittedDeck().cardIds);
            Assert.IsTrue(DeckRules.Validate(profile.CommittedDeck().cardIds, catalog, out var reason), reason);
        }
    }
}
