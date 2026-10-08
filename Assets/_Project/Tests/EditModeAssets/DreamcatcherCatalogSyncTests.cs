using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.BattleCore.Trigger;

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

        // skill-data-table unit 8 — 스쿼드 스탯 효과 = 진영 버프 효과 줄(트리거 None — 보유 시작 순간부터). 옛 `effects` 칸의 후계.
        private static List<EffectValues> FactionBuffRows(DreamcatcherCard card)
        {
            var rows = new List<EffectValues>();
            if (card.bindings == null) return rows;
            foreach (var b in card.bindings)
            {
                if (b.effect == null || b.effect.values.kind != EffectKind.FactionStatBuff) continue;
                Assert.AreEqual(TriggerKind.None, b.trigger.kind, $"'{card.id}': 진영 버프 줄의 트리거는 None(상시)");
                rows.Add(b.effect.values);
            }
            return rows;
        }

        // 의도적으로 카탈로그에서 뺀 카드(= 뽑히지 않음)를 사유와 함께 적는 자리 — 이 가드가 "등록 깜빡"으로 오인하지 않게.
        // 2026-08-08 비활성화했던 Card_IncubusPact(희생계약)는 battle-content-finish D8(2026-10-07)로 SO·효과·아트째 지웠다 —
        // 되살릴 땐 git 에서 꺼내고 여기 다시 적는다.
        private static readonly HashSet<string> IntentionallyDisabled = new();   // battle-content-finish D8 — Card_IncubusPact 는 2026-10-07 삭제(보관 종료)

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

            // subconscious-curse-expansion — 무의식 풀 6장 확정(기존 3 + 신규 3) → battle-content-finish D8 로 몽마의 계약 삭제(5장).
            // unit 0: 호접몽 / unit 1: 몽마의 계약 / unit 2: 살찌운 제물.
            // 림의 선물은 이 풀에서 서로 다른 2장 추출.
            CollectionAssert.AreEquivalent(
                new[] { "slow_awakening", "calamity_heart", "cracked_grail",
                        "sub_butterfly_dream", "sub_fattened_offering" },
                actual);
            Assert.AreEqual(5, actual.Count, "Subconscious pool size changed");
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
            Assert.AreEqual(1, butterfly.RuleView().Length, "소유 줄 = 고치 하나(진영 버프 · 수식자 줄 없음)");
            var m = butterfly.RuleView()[0];
            Assert.AreEqual(TriggerKind.None, m.trigger.kind);
            Assert.AreEqual(EffectKind.DreamCocoon, m.payload.kind);
            // magnitude·duration 은 DcSheet(mechanics 행) 소유 — 값은 자유 튜닝, 구조만 잠근다
            // (test-suite-fast-lane unit 1, 전례: WaveKillBudgetPinTests).
            Assert.Greater(m.payload.magnitude, 0f, "완주 버프 % 가 0 이면 고치가 보상 없는 잠이 된다");
            Assert.Greater(m.payload.duration, 0f, "잠 0 초면 고치가 즉시 깨어 수면 단계가 사라진다");
            Assert.AreEqual(CardBuffKind.AttackDamage, m.payload.buffStat);
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
            Assert.AreEqual(1, offering.RuleView().Length, "소유 줄 = 표식 하나(진영 버프 · 수식자 줄 없음)");
            var m = offering.RuleView()[0];
            Assert.AreEqual(TriggerKind.None, m.trigger.kind);
            Assert.AreEqual(EffectKind.BountyMark, m.payload.kind);
            // magnitude(각성 배율)·tileRange(받는 피해 감소 % 로 재해석) 는 DcSheet 소유 (unit 1).
            Assert.Greater(m.payload.magnitude, 1f, "배율이 1 이하면 «살찌운» 제물이 아니다");
            Assert.That(m.payload.tileRange, Is.InRange(1, 99),
                "피해 감소 % — 0 이면 무의미하고 100 이상이면 무적이 된다");
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
            Assert.AreEqual(3, heart.RuleView().Length);
            // 수치(magnitude·duration·period·tileRange)는 DcSheet 소유 — 자유 튜닝.
            // 여기서는 3-메커닉 구성(kind·trigger)과 부호·배율 구조만 잠근다 (unit 1).
            Assert.AreEqual(EffectKind.SelfBuffLethal, heart.RuleView()[0].payload.kind);
            Assert.Greater(heart.RuleView()[0].payload.magnitude, 0f);
            Assert.Greater(heart.RuleView()[0].payload.duration, 0f);
            Assert.AreEqual(TriggerKind.AttackN, heart.RuleView()[1].trigger.kind);
            Assert.Greater(heart.RuleView()[1].trigger.period, 0, "AttackN 주기 0 이면 트리거가 죽는다");
            Assert.AreEqual(EffectKind.HeavyStrike, heart.RuleView()[1].payload.kind);
            Assert.Greater(heart.RuleView()[1].payload.magnitude, 1f, "강타 배율이 1 이하면 강타가 아니다");
            Assert.AreEqual(TriggerKind.OnDeath, heart.RuleView()[2].trigger.kind);
            Assert.AreEqual(EffectKind.SelfTileAoe, heart.RuleView()[2].payload.kind);
            Assert.Greater(heart.RuleView()[2].payload.magnitude, 0f);
            Assert.Greater(heart.RuleView()[2].payload.tileRange, 0);
            Assert.AreSame(byId["farewell"].RuleView()[0].payload.projectile,
                heart.RuleView()[2].payload.projectile);
            Assert.AreEqual("dreamcatcher_card_24", heart.art.name);

            var grail = byId["cracked_grail"];
            Assert.AreEqual(CardType.Squad, grail.type);
            Assert.AreEqual(CardCategory.Subconscious, grail.category);
            // skill-data-table unit 8 — 스쿼드 스탯 효과 둘 = 진영 버프 효과 줄 둘(순서 = 옛 항목 순서 · Squad 카드 = 그 줄만).
            var grailBuffs = FactionBuffRows(grail);
            Assert.AreEqual(2, grailBuffs.Count);
            Assert.AreEqual(2, grail.bindings.Length, "Squad 카드 = 진영 버프 줄만");
            Assert.AreEqual(CardBuffKind.AttackDamage, grailBuffs[0].buffStat);
            Assert.AreEqual(CardBuffKind.EffectiveHealth, grailBuffs[1].buffStat);
            Assert.AreEqual(CardTargetAxis.All, grailBuffs[0].allyFilter);
            Assert.AreEqual(CardTargetAxis.All, grailBuffs[1].allyFilter);
            // percent 는 시트 소유 — 성배의 정체성은 값이 아니라 **부호**다(딜 ↑ · 체력 ↓).
            Assert.Greater(grailBuffs[0].percent, 0f, "공격 버프 부호가 뒤집혔다");
            Assert.Less(grailBuffs[1].percent, 0f, "체력 말루스 부호가 뒤집혔다 — 저주가 축복이 된다");
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
