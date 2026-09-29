using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wassup.BattleCore.Trigger;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // skill-data-table unit 5 — 새 시트 두 탭(`Skills` · `SkillOwners`)의 임포터 하나(`SkillSheet`). 네트워크 · 디스크 0 — 메모리 SO 만.
    //  - 쓰기 전에 diff 표를 로그에 쓴다 · 미리보기(apply:false)는 아무것도 안 쓴다
    //  - 없는 id 는 만들지 않고 보고한다(효과 · 소유자 · 탄)
    //  - 카드 · 방어유닛 · 적이 같은 형식(owner_kind 열 하나)
    public class SkillSheetImportTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private T New<T>() where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            _made.Add(so);
            return so;
        }

        private EffectData Effect(string id, EffectKind kind, float damage, int radius)
        {
            var e = New<EffectData>();
            e.id = id;
            e.name = "Effect_" + id;
            e.values = new EffectValues { kind = kind, damage = damage, radiusTiles = radius };
            return e;
        }

        private static BindingSpec Bind(TriggerKind trigger, EffectData e, int period = 0)
            => new BindingSpec { trigger = new TriggerSpec { kind = trigger, period = period }, effect = e };

        private SkillSheetIndex Index(IEnumerable<EffectData> effects, IEnumerable<DreamcatcherCard> cards = null,
            IEnumerable<DefenderUnitData> units = null, IEnumerable<AttackUnitData> enemies = null,
            IEnumerable<ProjectileData> projectiles = null)
            => SkillSheetIndex.Build(effects, projectiles, null, null, cards, units, enemies, new StringBuilder());

        [Test]
        public void DtoColumns_PairEveryAuthoringField()
        {
            // 저작 칸(`EffectValues` · `TriggerSpec`)을 늘리고 시트 열을 잊으면 export 가 조용히 값을 잃는다.
            CollectionAssert.IsEmpty(SkillSheet.UnpairedAuthoringFields().ToList());
        }

        [Test]
        public void KindKo_NamesEveryEffectKind()
        {
            // U19 — 한국어 표시 열. 종류를 append 하고 이름을 잊으면 시트에 enum 이름이 그대로 샌다(skill-data-table unit 8 — 상시 4종 포함).
            foreach (EffectKind k in System.Enum.GetValues(typeof(EffectKind)))
                Assert.AreNotEqual(k.ToString(), SkillSheet.KindKo(k), k + " 의 한국어 표시가 없다");
            Assert.AreEqual("아군 전체 스탯", SkillSheet.KindKo(EffectKind.FactionStatBuff));
            Assert.AreEqual("투사체 튕김", SkillSheet.KindKo(EffectKind.ProjectileBounce));
            Assert.AreEqual("최전방 우선", SkillSheet.KindKo(EffectKind.FrontmostTarget));
            Assert.AreEqual("수면 적 특효", SkillSheet.KindKo(EffectKind.DamageVsSleeping));
        }

        [Test]
        public void Import_LogsDiffBeforeWriting_AndPreviewWritesNothing()
        {
            var e = Effect("aoe", EffectKind.SelfTileAoe, 20f, 1);
            var payload = new SkillSheetPayload { skills = new[] { new SkillRowDto { id = "aoe", damage = 35f } } };

            var preview = SkillSheet.Import(payload, Index(new[] { e }), apply: false, null, new StringBuilder());
            Assert.AreEqual(20f, e.values.damage, "미리보기는 에셋에 안 쓴다");
            StringAssert.Contains("[skills-diff] Skills aoe · damage: 20 → 35", preview);
            StringAssert.Contains("미리보기", preview);

            var written = new List<ScriptableObject>();
            var log = SkillSheet.Import(payload, Index(new[] { e }), apply: true, written.Add, new StringBuilder());
            Assert.AreEqual(35f, e.values.damage);
            Assert.AreEqual(1, e.values.radiusTiles, "빈 칸 = 그대로");
            CollectionAssert.AreEqual(new[] { e }, written, "바뀐 에셋만 저장 콜백을 받는다");
            // diff 는 쓰기 **전** 줄 — 요약(첫 줄) 뒤, 적용 전 표에 나온다.
            StringAssert.Contains("[skills-diff] 적용 전 · 바뀌는 칸 1", log);
        }

        [Test]
        public void Import_UnchangedRow_WritesNothing()
        {
            var e = Effect("aoe", EffectKind.SelfTileAoe, 20f, 1);
            var written = new List<ScriptableObject>();
            SkillSheet.Import(new SkillSheetPayload { skills = new[] { new SkillRowDto { id = "aoe", damage = 20f, kind = EffectKind.SelfTileAoe } } },
                Index(new[] { e }), true, written.Add, new StringBuilder());
            CollectionAssert.IsEmpty(written, "값이 같으면 에셋을 다시 저장하지 않는다");
        }

        [Test]
        public void Import_UnknownIds_AreReported_NotCreated()
        {
            var e = Effect("aoe", EffectKind.SelfTileAoe, 20f, 1);
            var card = New<DreamcatcherCard>();
            card.id = "c";
            card.bindings = new[] { Bind(TriggerKind.AttackN, e, 3) };
            var payload = new SkillSheetPayload
            {
                skills = new[]
                {
                    new SkillRowDto { id = "ghost", damage = 1f },
                    new SkillRowDto { id = "aoe", damage = 50f, projectileId = "no_such_projectile" },
                },
                owners = new[]
                {
                    new SkillOwnerRowDto { ownerKind = "card", ownerId = "c", slot = 0, effectId = "ghost" },
                    new SkillOwnerRowDto { ownerKind = "defender", ownerId = "nobody", slot = 0, kind = TriggerKind.OnPlace, effectId = "aoe" },
                    new SkillOwnerRowDto { ownerKind = "unit", ownerId = "c", slot = 0, kind = TriggerKind.OnPlace, effectId = "aoe" },
                },
            };
            string log = SkillSheet.Import(payload, Index(new[] { e }, new[] { card }), true, null, new StringBuilder());

            StringAssert.Contains("no effect for effect_id='ghost' — not created", log);
            StringAssert.Contains("projectile_id='no_such_projectile' not found — row skipped", log);
            Assert.AreEqual(20f, e.values.damage, "모르는 탄 id 가 있는 줄은 통째로 건너뛴다(반쯤 쓰지 않는다)");
            StringAssert.Contains("'card/c' slot 0 effect_id='ghost' not in Skills — owner skipped", log);
            Assert.AreSame(e, card.bindings[0].effect, "건너뛴 소유자는 그대로");
            StringAssert.Contains("no owner for owner_kind='defender' owner_id='nobody'", log);
            StringAssert.Contains("no owner for owner_kind='unit'", log);
        }

        [Test]
        public void Owners_OneShape_ForCardDefenderEnemy_SameEffectById()
        {
            var e = Effect("leap", EffectKind.SelfBlink, 60f, 2);
            var card = New<DreamcatcherCard>(); card.id = "c";
            var unit = New<DefenderUnitData>(); unit.id = "u";
            var enemy = New<AttackUnitData>(); enemy.id = "boss";
            var payload = new SkillSheetPayload
            {
                owners = new[]
                {
                    new SkillOwnerRowDto { ownerKind = "card", ownerId = "c", slot = 0, kind = TriggerKind.OnPlace, subject = BindingSubject.Any, effectId = "leap" },
                    new SkillOwnerRowDto { ownerKind = "defender", ownerId = "u", slot = 0, kind = TriggerKind.OnPlace, effectId = "leap" },
                    new SkillOwnerRowDto { ownerKind = "enemy", ownerId = "boss", slot = 0, kind = TriggerKind.PeriodicTimer, periodSeconds = 8f, fireCap = 1, effectId = "leap" },
                },
            };
            string log = SkillSheet.Import(payload, Index(new[] { e }, new[] { card }, new[] { unit }, new[] { enemy }), true, null, new StringBuilder());

            Assert.AreSame(e, card.bindings[0].effect);
            Assert.AreSame(e, unit.bindings[0].effect, "효과 id 하나를 방어유닛도 참조한다(복사 없음)");
            Assert.AreSame(e, enemy.bindings[0].effect);
            Assert.AreEqual(BindingSubject.Any, card.bindings[0].trigger.subject);
            Assert.AreEqual(8f, enemy.bindings[0].trigger.periodSeconds);
            Assert.AreEqual(1, enemy.bindings[0].fireCap);
            StringAssert.Contains("SkillOwners defender/u · slot 0 · added (OnPlace → leap)", log);
        }

        [Test]
        public void Owners_TabIsSheetSoT_RowsRebuildBindings()
        {
            var a = Effect("a", EffectKind.SelfTileAoe, 10f, 1);
            var b = Effect("b", EffectKind.SelfTileAoe, 20f, 1);
            var unit = New<DefenderUnitData>(); unit.id = "u";
            unit.bindings = new[] { Bind(TriggerKind.OnPlace, a), Bind(TriggerKind.OnDeath, b) };

            // slot 1 줄만 온다 → 소유 줄이 그 한 줄로 다시 지어진다(slot 0 은 지워진다) · 빈 칸은 옛 slot 1 값을 이어받는다.
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[] { new SkillOwnerRowDto { ownerKind = "defender", ownerId = "u", slot = 1, fireCap = 2 } },
            }, Index(new[] { a, b }, units: new[] { unit }), true, null, new StringBuilder());

            Assert.AreEqual(1, unit.bindings.Length);
            Assert.AreEqual(TriggerKind.OnDeath, unit.bindings[0].trigger.kind, "빈 칸 = 옛 slot 값");
            Assert.AreSame(b, unit.bindings[0].effect);
            Assert.AreEqual(2, unit.bindings[0].fireCap);
            StringAssert.Contains("bindings 2 → 1", log);
        }

        [Test]
        public void Owners_BadSlots_Or_NewSlotWithoutEffect_SkipOwner()
        {
            var a = Effect("a", EffectKind.SelfTileAoe, 10f, 1);
            var unit = New<DefenderUnitData>(); unit.id = "u";
            unit.bindings = new[] { Bind(TriggerKind.OnPlace, a) };
            var index = Index(new[] { a }, units: new[] { unit });

            string dup = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[]
                {
                    new SkillOwnerRowDto { ownerKind = "defender", ownerId = "u", slot = 0 },
                    new SkillOwnerRowDto { ownerKind = "defender", ownerId = "u", slot = 0 },
                },
            }, index, true, null, new StringBuilder());
            StringAssert.Contains("blank/negative/duplicate slot — owner skipped", dup);

            string missing = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[] { new SkillOwnerRowDto { ownerKind = "defender", ownerId = "u", slot = 3, kind = TriggerKind.OnKill } },
            }, index, true, null, new StringBuilder());
            StringAssert.Contains("new slot 3 needs trigger and effect_id — owner skipped", missing);
            Assert.AreEqual(1, unit.bindings.Length);
            Assert.AreEqual(TriggerKind.OnPlace, unit.bindings[0].trigger.kind);
        }

        [Test]
        public void Owners_DeprecatedEffect_IsRejected_EvenWhenDeprecatedInSameImport()
        {
            var a = Effect("a", EffectKind.SelfTileAoe, 10f, 1);
            var card = New<DreamcatcherCard>(); card.id = "c";
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                skills = new[] { new SkillRowDto { id = "a", deprecated = true } },
                owners = new[] { new SkillOwnerRowDto { ownerKind = "card", ownerId = "c", slot = 0, kind = TriggerKind.OnKill, effectId = "a" } },
            }, Index(new[] { a }, new[] { card }), true, null, new StringBuilder());
            StringAssert.Contains("effect 'a' is deprecated — owner skipped", log);
            Assert.IsNull(card.bindings);
        }

        [Test]
        public void Wire_SnakeHeaders_AreInContract_AndKindKoIsIgnored()
        {
            var e = Effect("aoe", EffectKind.SelfTileAoe, 20f, 1);
            string body = @"{ ""success"": true, ""data"": [ { ""effect_id"": ""aoe"", ""kind"": ""AreaDot"", ""kind_ko"": ""아무 말"", ""radius_tiles"": 4, ""tick_sec"": 0.5, ""cc_kind"": ""Sleep"" } ] }";
            var log = new StringBuilder();
            var rows = SheetEnvelopeParser.ParseSheetLogged<SkillRowDto>(body, null, "Skills", log);
            StringAssert.DoesNotContain("headers not in contract", log.ToString());

            SkillSheet.Import(new SkillSheetPayload { skills = rows }, Index(new[] { e }), true, null, new StringBuilder());
            Assert.AreEqual(EffectKind.AreaDot, e.values.kind);
            Assert.AreEqual(4, e.values.radiusTiles);
            Assert.AreEqual(0.5f, e.values.tickSec);
            Assert.AreEqual(Wassup.Data.Authoring.CcKind.Sleep, e.values.ccKind);
            Assert.AreEqual(20f, e.values.damage, "빈 칸 = 그대로");
        }

        [Test]
        public void Export_WritesKindAndTriggerAlways_OtherValuesOnlyWhenSet()
        {
            var e = Effect("aoe", EffectKind.SelfTileAoe, 20f, 0);
            var unit = New<DefenderUnitData>(); unit.id = "u";
            unit.bindings = new[] { Bind(TriggerKind.None, e) };
            var sheet = SkillSheet.Export(new[] { e }, null, new[] { unit }, null);

            var row = sheet.skills.Single();
            Assert.AreEqual(EffectKind.SelfTileAoe, row.kind);
            Assert.AreEqual(20f, row.damage);
            Assert.IsNull(row.radiusTiles, "0 = 빈 칸");
            Assert.AreEqual(SkillSheet.KindKo(EffectKind.SelfTileAoe), row.kindKo);
            var owner = sheet.owners.Single();
            Assert.AreEqual("defender", owner.ownerKind);
            Assert.AreEqual(TriggerKind.None, owner.kind, "트리거는 None 이어도 쓴다(새 줄의 필수 칸)");
            Assert.AreEqual("aoe", owner.effectId);
            StringAssert.Contains("\"effect_id\": \"aoe\"", SkillSheet.ToJson(sheet.skills));
            StringAssert.Contains("\"kind\": \"SelfTileAoe\"", SkillSheet.ToJson(sheet.skills));
        }
    }
}
