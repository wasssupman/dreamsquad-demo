using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore.Trigger;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditModeAssets
{
    // skill-data-table unit 8 — **이전 전 증명**(unit 4 `BindingSpecBakeTests` 선례): 라이브 카드에 상시 효과 이전 계획을 **메모리 사본**으로
    // 입혀(에셋 0 · 디스크 쓰기 0) 굽기 스냅샷 · 전 카드 문안 · 칩이 오늘과 글자까지 같은지 본다. 그리고 옛 칸(`effects` · `attackMods`)을
    // 비운 사본도 같다 — 단계 B(옛 칸 제거)가 아무것도 안 바꾼다는 증거다.
    public class AlwaysOnEffectMigrationLiveTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private static List<string> ExistingEffectIds()
        {
            var ids = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:EffectData", new[] { DataRoot }))
            {
                var e = AssetDatabase.LoadAssetAtPath<EffectData>(AssetDatabase.GUIDToAssetPath(guid));
                if (e != null && !string.IsNullOrEmpty(e.id)) ids.Add(e.id);
            }
            return ids;
        }

        private AlwaysOnEffectMigration.Plan PlanOf(List<DreamcatcherCard> cards)
        {
            var inputs = new List<AlwaysOnEffectMigration.CardInput>();
            foreach (var c in cards) inputs.Add(AlwaysOnEffectMigrationApply.InputOf(c, AssetDatabase.GetAssetPath(c)));
            return AlwaysOnEffectMigration.Build(inputs, ExistingEffectIds());
        }

        // 계획을 입힌 사본(카드 · 배치 오라 효과). `dropLegacy` = 옛 칸을 비운다(단계 B 모의).
        private List<DreamcatcherCard> Migrated(List<DreamcatcherCard> cards, AlwaysOnEffectMigration.Plan plan, bool dropLegacy)
        {
            var result = new List<DreamcatcherCard>();
            foreach (var src in cards)
            {
                var c = Object.Instantiate(src);
                c.name = src.name;
                _made.Add(c);
                if (c.bindings != null) c.bindings = (BindingSpec[])c.bindings.Clone();
                foreach (var a in plan.AuraFilters)
                {
                    if (a.CardId != c.id || c.bindings == null) continue;
                    for (int i = 0; i < c.bindings.Length; i++)
                    {
                        var e = c.bindings[i].effect;
                        if (e == null || e.id != a.EffectId) continue;
                        var copy = Object.Instantiate(e);
                        copy.name = e.name;
                        copy.values.allyFilter = a.To;
                        _made.Add(copy);
                        c.bindings[i].effect = copy;
                    }
                }
                Assert.IsTrue(AlwaysOnEffectMigrationApply.AppendRows(c, plan.Rows, r =>
                {
                    var fx = AlwaysOnEffectMigrationApply.Materialize(r);
                    _made.Add(fx);
                    return fx;
                }), c.id);
                if (dropLegacy) { c.effects = null; c.attackMods = null; }
                result.Add(c);
            }
            return result;
        }

        [Test]
        public void 라이브_계획은_효과_줄_18_배치_오라_1_깃발_0이다()
        {
            var plan = PlanOf(CardEffectWitnessTests.Cards());
            TestContext.WriteLine(AlwaysOnEffectMigration.Report(plan, null));
            Assert.AreEqual(18, plan.Rows.Count, "스쿼드 스탯 효과 15(13장) + 공격 수식자 3");
            Assert.AreEqual(15, plan.Rows.Count(r => r.Values.kind == EffectKind.FactionStatBuff));
            Assert.AreEqual(1, plan.AuraFilters.Count, "배치 오라 = slow_awakening 하나");
            Assert.AreEqual("slow_awakening", plan.AuraFilters[0].CardId);
            Assert.IsFalse(plan.Rows.Any(r => r.Notes.Any(AlwaysOnEffectMigration.IsFlag)), "효과 줄 깃발 0");
            Assert.IsFalse(plan.AuraFilters.Any(a => a.Notes.Any(AlwaysOnEffectMigration.IsFlag)));
            var ids = ExistingEffectIds();
            foreach (var r in plan.Rows) CollectionAssert.DoesNotContain(ids, r.EffectId, "기존 효과 id 와 충돌 0");
        }

        [Test]
        public void 계획을_입힌_사본의_카드_굽기가_굳힌_스냅샷과_같다_옛_칸을_비워도_같다()
        {
            var cards = CardEffectWitnessTests.Cards();
            var plan = PlanOf(cards);
            string committed = CardBakeSnapshotTests.Committed();
            Assert.IsNull(CardBakeSnapshotTests.FirstDiff(committed, CardBakeSnapshotTests.Bake(Migrated(cards, plan, dropLegacy: false))),
                          "이전(옛 칸 유지 — 과도기) 뒤 굽기 = 스냅샷");
            Assert.IsNull(CardBakeSnapshotTests.FirstDiff(committed, CardBakeSnapshotTests.Bake(Migrated(cards, plan, dropLegacy: true))),
                          "옛 칸 제거(단계 B 모의) 뒤 굽기 = 스냅샷");
        }

        [Test]
        public void 계획을_입힌_사본의_전_카드_문안과_칩이_같다_옛_칸을_비워도_같다()
        {
            var cards = CardEffectWitnessTests.Cards();
            var plan = PlanOf(cards);
            foreach (bool drop in new[] { false, true })
            {
                var migrated = Migrated(cards, plan, drop);
                var changed = new List<string>();
                for (int i = 0; i < cards.Count; i++)
                {
                    var a = cards[i];
                    var b = migrated[i];
                    int? cost = a.type == CardType.Active ? 20 : (int?)null;
                    if (DreamcatcherCardText.Body(a, null, cost) != DreamcatcherCardText.Body(b, null, cost)
                        || DreamcatcherCardText.BodyCompact(a, null, cost) != DreamcatcherCardText.BodyCompact(b, null, cost)
                        || DreamcatcherCardText.BodyLinesOnly(a, null, cost) != DreamcatcherCardText.BodyLinesOnly(b, null, cost)
                        || DreamcatcherCardText.EffectOnly(a) != DreamcatcherCardText.EffectOnly(b)
                        || CardCategoryStyle.TargetTag(a) != CardCategoryStyle.TargetTag(b))
                    {
                        changed.Add(a.id);
                        TestContext.WriteLine($"[{a.id}] 전: {DreamcatcherCardText.Body(a, null, cost).Replace("\n", " / ")}\n  후: {DreamcatcherCardText.Body(b, null, cost).Replace("\n", " / ")}");
                    }
                }
                CollectionAssert.IsEmpty(changed, (drop ? "옛 칸 제거 뒤" : "이전 뒤") + " 문안 · 칩이 바뀐 카드");
            }
        }
    }
}
