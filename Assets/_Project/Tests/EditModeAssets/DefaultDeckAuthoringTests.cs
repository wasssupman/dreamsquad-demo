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

            string[] expected =
            {
                "ranger_atk", "poke_needle", "ranger_as", "bouncy_bead", "guardian_as",
                "thornmail", "ranger_hp", "guardian_hp", "farewell", "guardian_fortress",
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
