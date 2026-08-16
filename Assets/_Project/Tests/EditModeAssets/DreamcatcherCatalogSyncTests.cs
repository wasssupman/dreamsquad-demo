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

        // 의도적으로 카탈로그에서 뺀 카드(= 뽑히지 않음). SO·기능 코드는 남겨 두므로
        // 이 가드가 "등록 깜빡"으로 오인하지 않게 여기에 사유와 함께 명시한다.
        //   Card_IncubusPact(희생계약) — 2026-08-08 사용자 결정으로 비활성화. 유출 허용치를
        //   선불로 지불하는 카드인데, goal-tower-siege 이후 유출이 **골 파괴 뒤에만** 생겨
        //   지불이 사실상 무비용이 됐다. 유출/스트레스 규칙이 안정되면 재등록 여부를 재판단.
        private static readonly HashSet<string> IntentionallyDisabled = new() { "Card_IncubusPact" };

        [Test]
        public void EveryNonActiveCard_IsRegisteredInCatalog()
        {
            var catalog = LoadCatalog();
            var registered = new HashSet<DreamcatcherCard>(catalog.cards);
            var missing = new List<string>();
            foreach (var card in LoadAllCards())
            {
                if (card.type == CardType.Active) continue; // 공용 — 카탈로그 제외 규약
                if (IntentionallyDisabled.Contains(card.name)) continue;
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

        [Test]
        public void SubconsciousPool_MatchesCursedRoster()
        {
            var actual = new List<string>();
            foreach (var card in LoadAllCards())
                if (card.category == CardCategory.Subconscious)
                    actual.Add(card.id);

            // subconscious-curse-expansion — 무의식 풀 6장 확정(기존 3 + 신규 3).
            // unit 0: 호접몽 / unit 1: 몽마의 계약 / unit 2: 살찌운 제물.
            // 림의 선물은 이 풀에서 서로 다른 2장 추출.
            CollectionAssert.AreEquivalent(
                new[] { "slow_awakening", "calamity_heart", "cracked_grail",
                        "sub_butterfly_dream", "sub_incubus_pact", "sub_fattened_offering" },
                actual);
            Assert.AreEqual(6, actual.Count, "Subconscious pool size changed");
        }

        [Test]
        public void ButterflyDreamAsset_MatchesAuthoredContract()
        {
            // subconscious-curse-expansion unit 0 — 호접몽: 즉발 DreamCocoon 단일
            // mechanic (잠 4초, 완주 시 공격력 +35%). 수치는 SO 소유 — 이 테스트는
            // 에셋이 계약대로 저작돼 있음을 잠근다.
            var byId = new Dictionary<string, DreamcatcherCard>();
            foreach (var card in LoadAllCards()) byId[card.id] = card;

            Assert.IsTrue(byId.ContainsKey("sub_butterfly_dream"), "sub_butterfly_dream in catalog");
            var butterfly = byId["sub_butterfly_dream"];
            Assert.AreEqual(CardType.Unit, butterfly.type);
            Assert.AreEqual(CardCategory.Subconscious, butterfly.category);
            Assert.AreEqual(CardTargetAxis.All, butterfly.axis);
            Assert.IsEmpty(butterfly.effects);
            Assert.IsEmpty(butterfly.attackMods);
            Assert.AreEqual(1, butterfly.mechanics.Length);
            var m = butterfly.mechanics[0];
            Assert.AreEqual(DcTriggerKind.None, m.trigger.kind);
            Assert.AreEqual(DcPayloadKind.DreamCocoon, m.payload.kind);
            Assert.AreEqual(35f, m.payload.magnitude, 0.001f, "완주 버프 %");
            Assert.AreEqual(4f, m.payload.duration, 0.001f, "잠 초");
            Assert.AreEqual(CardBuffKind.AttackDamage, m.payload.buffStat);
        }

        [Test]
        public void IncubusPactAsset_MatchesAuthoredContract()
        {
            // subconscious-curse-expansion unit 1 — 몽마의 계약: hosted Squad 버프
            // (전군 공격력 +25%) + 유출 허용치 1 선불. 수치는 SO 소유 — 에셋 계약 잠금.
            var byId = new Dictionary<string, DreamcatcherCard>();
            foreach (var card in LoadAllCards()) byId[card.id] = card;

            Assert.IsTrue(byId.ContainsKey("sub_incubus_pact"), "sub_incubus_pact in catalog");
            var pact = byId["sub_incubus_pact"];
            Assert.AreEqual(CardType.Squad, pact.type);
            Assert.AreEqual(CardCategory.Subconscious, pact.category);
            Assert.AreEqual(CardTargetAxis.All, pact.axis);
            Assert.AreEqual(1, pact.effects.Length);
            Assert.AreEqual(CardBuffKind.AttackDamage, pact.effects[0].kind);
            Assert.AreEqual(25f, pact.effects[0].percent, 0.001f, "전군 공격력 +25%");
            Assert.IsEmpty(pact.mechanics);
            Assert.IsEmpty(pact.attackMods);
            Assert.AreEqual(1, pact.leakAllowanceCost, "유출 허용치 선불 1");
        }

        [Test]
        public void FattenedOfferingAsset_MatchesAuthoredContract()
        {
            // subconscious-curse-expansion unit 2 — 살찌운 제물: BountyMark 단일
            // mechanic (각성 배율 ×3, 받는 피해 −30%). 수치는 SO 소유 — 에셋 계약 잠금.
            var byId = new Dictionary<string, DreamcatcherCard>();
            foreach (var card in LoadAllCards()) byId[card.id] = card;

            Assert.IsTrue(byId.ContainsKey("sub_fattened_offering"), "sub_fattened_offering in catalog");
            var offering = byId["sub_fattened_offering"];
            Assert.AreEqual(CardType.Unit, offering.type);
            Assert.AreEqual(CardCategory.Subconscious, offering.category);
            Assert.AreEqual(CardTargetAxis.All, offering.axis);
            Assert.IsEmpty(offering.effects);
            Assert.IsEmpty(offering.attackMods);
            Assert.AreEqual(1, offering.mechanics.Length);
            var m = offering.mechanics[0];
            Assert.AreEqual(DcTriggerKind.None, m.trigger.kind);
            Assert.AreEqual(DcPayloadKind.BountyMark, m.payload.kind);
            Assert.AreEqual(3f, m.payload.magnitude, 0.001f, "각성 배율 ×3");
            Assert.AreEqual(30, m.payload.tileRange, "받는 피해 감소 %");
        }

        // gift-phase-removal unit 1 — RimGift_LiveCatalogPool_PicksTwoDistinctSubconscious
        // 는 삭제됐다. 림의 선물(시드 2장 추출)이 폐지되면서 GiftDeckComposer 자체가
        // 사라졌기 때문이다. 이 파일이 지키던 나머지 계약 — 무의식 로스터 6장과 카드별
        // 트리거/페이로드 — 은 위아래 케이스에 그대로 남아 있고, 이제 그 6장은 선물이
        // 아니라 **저장 덱에 직접 넣어** 판에 들어온다(unit 0).

        [Test]
        public void CursedRelicAssets_MatchAuthoredContracts()
        {
            var byId = new Dictionary<string, DreamcatcherCard>();
            foreach (var card in LoadAllCards()) byId[card.id] = card;

            var heart = byId["calamity_heart"];
            Assert.AreEqual(CardType.Unit, heart.type);
            Assert.AreEqual(CardCategory.Subconscious, heart.category);
            Assert.AreEqual(CardTargetAxis.All, heart.axis);
            Assert.IsEmpty(heart.effects);
            Assert.AreEqual(3, heart.mechanics.Length);
            Assert.AreEqual(DcPayloadKind.SelfBuffLethal, heart.mechanics[0].payload.kind);
            Assert.AreEqual(100f, heart.mechanics[0].payload.magnitude);
            Assert.AreEqual(6f, heart.mechanics[0].payload.duration);
            Assert.AreEqual(DcTriggerKind.AttackN, heart.mechanics[1].trigger.kind);
            Assert.AreEqual(3, heart.mechanics[1].trigger.period);
            Assert.AreEqual(DcPayloadKind.HeavyStrike, heart.mechanics[1].payload.kind);
            Assert.AreEqual(2f, heart.mechanics[1].payload.magnitude);
            Assert.AreEqual(DcTriggerKind.OnDeath, heart.mechanics[2].trigger.kind);
            Assert.AreEqual(DcPayloadKind.SelfTileAoe, heart.mechanics[2].payload.kind);
            Assert.AreEqual(400f, heart.mechanics[2].payload.magnitude);
            Assert.AreEqual(2, heart.mechanics[2].payload.tileRange);
            Assert.AreSame(byId["farewell"].mechanics[0].payload.projectile,
                heart.mechanics[2].payload.projectile);
            Assert.AreEqual("dreamcatcher_card_24", heart.art.name);

            var grail = byId["cracked_grail"];
            Assert.AreEqual(CardType.Squad, grail.type);
            Assert.AreEqual(CardCategory.Subconscious, grail.category);
            Assert.AreEqual(CardTargetAxis.All, grail.axis);
            Assert.IsEmpty(grail.mechanics);
            Assert.AreEqual(2, grail.effects.Length);
            Assert.AreEqual(CardBuffKind.AttackDamage, grail.effects[0].kind);
            Assert.AreEqual(70f, grail.effects[0].percent);
            Assert.AreEqual(CardBuffKind.EffectiveHealth, grail.effects[1].kind);
            Assert.AreEqual(-40f, grail.effects[1].percent);
            Assert.AreEqual("dreamcatcher_card_25", grail.art.name);
        }

        [Test]
        public void CompletedCardArt_HasExpectedSpriteImportContract()
        {
            var byId = new Dictionary<string, DreamcatcherCard>();
            foreach (var card in LoadAllCards()) byId[card.id] = card;

            var expected = new Dictionary<string, string>
            {
                { "nightmare_afterglow", "dreamcatcher_card_21" },
                { "eye_on_the_end", "dreamcatcher_card_22" },
                { "heavy_strike", "dreamcatcher_card_23" },
                { "calamity_heart", "dreamcatcher_card_24" },
                { "cracked_grail", "dreamcatcher_card_25" },
                // subconscious-curse-expansion unit 5 — 신규 저주 3장 실아트.
                { "sub_butterfly_dream", "dreamcatcher_card_26" },
                { "sub_incubus_pact", "dreamcatcher_card_27" },
                { "sub_fattened_offering", "dreamcatcher_card_28" },
            };

            foreach (var pair in expected)
            {
                var sprite = byId[pair.Key].art;
                Assert.IsNotNull(sprite, $"{pair.Key} art missing");
                Assert.AreEqual(pair.Value, sprite.name, $"{pair.Key} art mismatch");
                Assert.AreEqual(1024, sprite.texture.width, $"{pair.Key} art width");
                Assert.AreEqual(1536, sprite.texture.height, $"{pair.Key} art height");

                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
                Assert.IsFalse(importer.mipmapEnabled);
            }
        }
    }
}
