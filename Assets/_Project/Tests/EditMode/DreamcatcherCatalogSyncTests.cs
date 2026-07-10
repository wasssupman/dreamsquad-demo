using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-awakening-hand — 카탈로그 등록 누락 회귀 방지 (2026-07-10 실사고:
    // unit-trigger/content-1 등이 만든 카드 6장이 카탈로그에 미등록 → 덱빌더 미노출,
    // 세이브덱 검증 불가). card-art 확장 규약("새 카드 = SO + art + 카탈로그 등록")을
    // 에셋 전수 대조로 강제한다. Active 타입만 예외(매판 공통 배정, 덱 구성 대상 아님).
    public class DreamcatcherCatalogSyncTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string CatalogPath = CardsRoot + "/DreamcatcherCardCatalog.asset";

        private static DreamcatcherCardCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"catalog asset missing at {CatalogPath}");
            return catalog;
        }

        private static List<DreamcatcherCard> LoadAllCards()
        {
            var result = new List<DreamcatcherCard>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) result.Add(card);
            }
            Assert.IsNotEmpty(result, "no DreamcatcherCard assets found — path convention changed?");
            return result;
        }

        [Test]
        public void EveryNonActiveCard_IsRegisteredInCatalog()
        {
            var catalog = LoadCatalog();
            var registered = new HashSet<DreamcatcherCard>(catalog.cards);
            var missing = new List<string>();
            foreach (var card in LoadAllCards())
            {
                if (card.type == CardType.Active) continue; // 공용 — 카탈로그 제외 규약
                if (!registered.Contains(card)) missing.Add(card.name);
            }
            Assert.IsEmpty(missing,
                $"catalog 미등록 카드: [{string.Join(", ", missing)}] — 새 카드는 SO+art+카탈로그 등록까지가 한 세트다.");
        }

        [Test]
        public void ActiveCards_AreNotInCatalog()
        {
            var catalog = LoadCatalog();
            var leaked = new List<string>();
            foreach (var card in catalog.cards)
                if (card != null && card.type == CardType.Active) leaked.Add(card.name);
            Assert.IsEmpty(leaked,
                $"Active 카드가 카탈로그에 등록됨(덱빌더 오염): [{string.Join(", ", leaked)}] — 공용 카드는 매판 주입 전용.");
        }

        [Test]
        public void Catalog_HasNoNullOrDuplicateEntries_AndUniqueIds()
        {
            var catalog = LoadCatalog();
            var seen = new HashSet<DreamcatcherCard>();
            var ids = new HashSet<string>();
            for (int i = 0; i < catalog.cards.Length; i++)
            {
                var card = catalog.cards[i];
                Assert.IsNotNull(card, $"catalog.cards[{i}] is null");
                Assert.IsTrue(seen.Add(card), $"catalog.cards[{i}] '{card.name}' duplicated");
                Assert.IsFalse(string.IsNullOrEmpty(card.id), $"'{card.name}' has empty id");
                Assert.IsTrue(ids.Add(card.id), $"duplicate card id '{card.id}' ('{card.name}')");
            }
        }
    }
}
