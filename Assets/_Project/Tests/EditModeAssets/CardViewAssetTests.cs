using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Cards;
using Wassup.Core;
using Wassup.Data;
using Wassup.Skills;

namespace Wassup.Tests.EditModeAssets
{
    // battle-core-rebuild unit 7c — **카드 화면이 읽는 뷰 표**가 번호를 매긴 순회와 같은가 + 카드 단위 범위 도형이 옛 것과 같은가.
    //
    // 빌더 매핑 누락은 조용히 죽는다(인계 함정 8) — 카드 에셋 목록의 번호가 하나 밀리면 손패가 **다른 카드의 문안**을 그리고,
    // 빔 번호가 안 실리면 버스터즈 개시 빔이 **요청조차 안 된다**(`AreaDotSkill` 은 `HasData` 일 때만 빔을 낸다).
    public class CardViewAssetTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";

        private static List<DreamcatcherCard> LiveCards()
        {
            var list = new List<DreamcatcherCard>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                var c = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(AssetDatabase.GUIDToAssetPath(guid));
                if (c != null) list.Add(c);
            }
            Assert.IsNotEmpty(list);
            return list;
        }

        private static AwakeningConfig Awakening()
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            return AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [Test]
        public void 카드_에셋_목록은_카드_줄_번호와_같다_빈_칸을_건너뛰어도()
        {
            var cards = LiveCards();
            var withHole = new List<DreamcatcherCard>(cards);
            withHole.Insert(1, null);   // 덱 한가운데 빈 칸 — 빌더가 건너뛰면 뒤 번호가 한 칸씩 당겨진다
            var def = new MatchDefinition();
            var view = new MatchViewAssets();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = withHole, Awakening = Awakening() },
                                           new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                           CardDefinitionBuilder.WithCardHazards(null, cards), view);
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            Assert.AreEqual(def.Cards.Length, view.Cards.Count, "카드 줄 수 = 카드 에셋 수");
            for (int i = 0; i < def.Cards.Length; i++)
                Assert.AreEqual(def.Cards[i].Id, view.Card(i).id, $"카드 줄 {i} 의 에셋이 다른 카드다 — 손패가 남의 문안을 그린다");
        }

        [Test]
        public void 카드_단위_범위_도형은_옛_카탈로그와_같다()
        {
            // 옛 `DcRangeCatalog.ResolveCard`(managed SO 를 돌며 첫 공간 도형) ↔ 새 `CoreCardDragSlot.CardRangeOf`(정의표 규칙 줄을 돌며
            // `RangeCatalog.Resolve`). 둘 다 **도형 반경 N + 형**만 담는다 — 원점 항은 host 를 아는 자리가 같은 함수로 더한다.
            var cards = LiveCards();
            var def = new MatchDefinition();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = Awakening() },
                                           new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                           CardDefinitionBuilder.WithCardHazards(null, cards));
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            int spatial = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].type == CardType.Active) continue;
                var old = DcRangeCatalog.ResolveCard(cards[i]);
                var now = CoreCardDragSlot.CardRangeOf(def, i);
                bool oldHas = old.shape != DcRangeShape.None;
                bool nowHas = now.Shape != RangeShape.None;
                Assert.AreEqual(oldHas, nowHas, $"'{cards[i].id}': 옛 카탈로그와 범위 유무가 다르다");
                // skill-data-table 4-정리(B21) — 옛 카탈로그 삭제 전 기대값 고정용 실측 줄. 형식: `[B21] id|shape|radius|metric`.
                TestContext.WriteLine($"[B21] {cards[i].id}|{(oldHas ? old.shape.ToString() : "None")}|{old.radiusTiles}|{old.metric}");
                if (!oldHas) continue;
                spatial++;
                Assert.AreEqual(old.radiusTiles, now.RadiusTiles, 1e-5f, $"'{cards[i].id}': 도형 반경이 다르다");
                Assert.AreEqual(old.metric, now.Metric, $"'{cards[i].id}': 형(원점 항)이 다르다");
            }
            Assert.Greater(spatial, 0, "공간 도형 카드가 하나도 없다 — 이 대조가 아무것도 증언하지 않는다");
        }

        [Test]
        public void 메커닉이_선언한_빔과_오라는_뷰_표에_실린다()
        {
            var defenders = AssetDatabase.LoadAssetAtPath<DefenderCatalog>("Assets/_Project/Data/DefenderCatalog.asset");
            var enemies = AssetDatabase.LoadAssetAtPath<EnemyCatalog>("Assets/_Project/Data/EnemyCatalog.asset");
            Assert.IsNotNull(defenders); Assert.IsNotNull(enemies);
            var units = new List<DefenderUnitData>();
            foreach (var d in defenders.units) if (d != null) units.Add(d);
            var def = new MatchDefinition { Units = new UnitDef[units.Count], Enemies = new EnemyDef[enemies.units.Length] };
            for (int i = 0; i < units.Count; i++) def.Units[i] = MatchDefinitionBuilder.ToUnitDef(units[i]);
            var view = new MatchViewAssets();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                BindingDefinitionBuilder.Fill(def, units, enemies.units, new List<ProjectileData>(),
                                              new List<ProjectilePatternData>(), System.Array.Empty<HazardSO>(), view);
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }

            int beams = 0, auras = 0;
            for (int row = 0; row < def.Bindings.Length; row++)
            {
                ref var b = ref def.Bindings[row];
                var fx = def.EffectOf(in b);
                if (fx.Kind == EffectKind.AreaDot && fx.DataIndex >= 0)
                {
                    Assert.IsNotNull(view.SkillVfx(fx.DataIndex), $"'{b.Label}': 빔 번호가 뷰 표 밖을 가리킨다");
                    Assert.IsFalse(view.TryGetBindingAura(row, out _, out _),
                        $"'{b.Label}': 빔 프리팹이 오라로도 등록됐다(옛 bake 의 겸용 결함 — 빔만 옮긴다)");
                    beams++;
                }
                if (view.TryGetBindingAura(row, out var prefab, out _))
                {
                    Assert.IsNotNull(prefab);
                    auras++;
                }
            }
            // 라이브 저작: 버스터즈 개시 빔(`Ability_OpeningBeam_Busters`) · 보스 나이트메어 바람 오라. 수는 에셋이 정본이라 「있다」만 본다.
            Assert.Greater(beams, 0, "빔을 선언한 지속 피해 규칙이 뷰 표에 없다 — 개시 빔이 요청조차 안 된다");
            Assert.Greater(auras, 0, "오라를 선언한 규칙이 뷰 표에 없다");
        }

        [Test]
        public void 액티브_굴림은_판_시드로_재현된다()
        {
            var pool = new List<SkillData>();
            foreach (var guid in AssetDatabase.FindAssets("t:SkillData"))
            {
                var s = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                if (s != null) pool.Add(s);
            }
            Assert.GreaterOrEqual(pool.Count, 2);
            var a = CoreDeckComposition.RollActives(pool, 2, 1234);
            var b = CoreDeckComposition.RollActives(pool, 2, 1234);
            Assert.AreEqual(2, a.Count);
            CollectionAssert.AreEqual(a, b, "같은 시드 = 같은 액티브(S1 — 벽시계 시드 금지, S2)");
            Assert.AreNotSame(a[0], a[1], "한 판에 같은 스킬이 두 번 굴렸다");
            Assert.IsEmpty(CoreDeckComposition.RollActives(pool, 0, 1234), "0 장 = 빈 목록(S3)");
            Assert.IsEmpty(CoreDeckComposition.RollActives(new List<SkillData>(), 2, 1234), "빈 풀 = 빈 목록(S3)");
        }

        [Test]
        public void 확정_덱이_무효면_부착_덱은_비어_있다()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CardsRoot + "/DreamcatcherCardCatalog.asset");
            Assert.IsNotNull(catalog);
            var profile = UnityEngine.ScriptableObject.CreateInstance<PlayerProfileSO>();
            try
            {
                profile.profile = new PlayerProfile();
                // 확정 덱 없음 → 빈 목록(기본 덱 폴백 없음 — D3).
                Assert.IsEmpty(CoreDeckComposition.ResolveAttachDeck(profile, catalog));
                // 장수가 모자란 덱 → 검증 실패 → 빈 목록.
                var preset = new DreamcatcherPreset { id = "t", cardIds = new List<string> { catalog.cards[0].id } };
                profile.profile.dreamcatcherDecks = new List<DreamcatcherPreset> { preset };
                profile.profile.selectedDeckId = "t";
                Assert.IsEmpty(CoreDeckComposition.ResolveAttachDeck(profile, catalog), "무효 덱이 그대로 실렸다(D3)");
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
    }
}
