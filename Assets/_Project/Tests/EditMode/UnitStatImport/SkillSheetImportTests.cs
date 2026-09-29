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
            // skill-data-table unit 9 — 광역 지속 피해(AreaDot)는 cc_kind 를 안 쓴다 → 경고하고 무시(에셋 칸 그대로).
            Assert.AreEqual(default(Wassup.Data.Authoring.CcKind), e.values.ccKind, "안 쓰는 칸은 쓰지 않는다");
            Assert.AreEqual(20f, e.values.damage, "빈 칸 = 그대로");
        }

        // ── skill-data-table unit 9 — U20(공격 변형 = 방어유닛 쪽 소유자만 · 시트 층) ─────────────────────────────

        private EffectData Mod(string id, EffectKind kind, float mul = 2f)
        {
            var e = New<EffectData>();
            e.id = id;
            e.name = "Effect_" + id;
            e.values = new EffectValues { kind = kind, mul = mul, count = 2, rangeTiles = 3 };
            return e;
        }

        [Test]
        public void U20_EnemyOwner_WithProjectileBounce_SkipsWholeOwner_AndReports()
        {
            var bounce = Mod("bounce", EffectKind.ProjectileBounce, 1f);
            var aoe = Effect("aoe", EffectKind.SelfTileAoe, 20f, 1);
            var enemy = New<AttackUnitData>(); enemy.id = "boss";
            var keep = new[] { Bind(TriggerKind.OnDeath, aoe) };
            enemy.bindings = keep;
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[]
                {
                    new SkillOwnerRowDto { ownerKind = "enemy", ownerId = "boss", slot = 0, kind = TriggerKind.OnDeath, effectId = "aoe" },
                    new SkillOwnerRowDto { ownerKind = "enemy", ownerId = "boss", slot = 1, kind = TriggerKind.None, effectId = "bounce" },
                },
            }, Index(new[] { bounce, aoe }, enemies: new[] { enemy }), true, null, new StringBuilder());

            StringAssert.Contains("'enemy/boss' U20 — attack modifier 'bounce' (ProjectileBounce) on owner_kind = enemy", log);
            StringAssert.Contains("owner's sheet rows skipped (asset bindings untouched)", log);
            Assert.AreSame(keep, enemy.bindings, "그 소유자의 시트 줄 전체를 건너뛴다(줄만 빼고 재구성하지 않는다)");
        }

        [Test]
        public void U20_EnemyOwner_WithHeavyStrike_IsSkipped()
        {
            var heavy = Mod("heavy", EffectKind.HeavyStrike);
            var enemy = New<AttackUnitData>(); enemy.id = "boss";
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[] { new SkillOwnerRowDto { ownerKind = "enemy", ownerId = "boss", slot = 0, kind = TriggerKind.AttackN, period = 3, effectId = "heavy" } },
            }, Index(new[] { heavy }, enemies: new[] { enemy }), true, null, new StringBuilder());
            StringAssert.Contains("U20 — attack modifier 'heavy' (HeavyStrike)", log);
            Assert.IsNull(enemy.bindings);
        }

        [Test]
        public void U20_DefenderOwner_WithProjectileBounce_PassesTheSheetLayer()
        {
            // 시트 층은 통과(방어유닛 쪽 소유자) — 굽기는 unit 8 규칙대로 「배선 전(NotWired)」을 말한다(시트의 일이 아니다).
            var bounce = Mod("bounce", EffectKind.ProjectileBounce, 1f);
            var unit = New<DefenderUnitData>(); unit.id = "u";
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[] { new SkillOwnerRowDto { ownerKind = "defender", ownerId = "u", slot = 0, kind = TriggerKind.None, effectId = "bounce" } },
            }, Index(new[] { bounce }, units: new[] { unit }), true, null, new StringBuilder());
            StringAssert.DoesNotContain("U20", log);
            Assert.AreSame(bounce, unit.bindings[0].effect);
        }

        [Test]
        public void U20_Card_OnlyWhenHostKindsAreDefenderOnly()
        {
            var front = Mod("front", EffectKind.FrontmostTarget, 1.2f);
            var ok = New<DreamcatcherCard>(); ok.id = "ok"; ok.hostKinds = HostKinds.Defender;
            var both = New<DreamcatcherCard>(); both.id = "both"; both.hostKinds = HostKinds.Defender | HostKinds.Enemy;
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[]
                {
                    new SkillOwnerRowDto { ownerKind = "card", ownerId = "ok", slot = 0, kind = TriggerKind.None, effectId = "front" },
                    new SkillOwnerRowDto { ownerKind = "card", ownerId = "both", slot = 0, kind = TriggerKind.None, effectId = "front" },
                },
            }, Index(new[] { front }, new[] { ok, both }), true, null, new StringBuilder());
            Assert.AreSame(front, ok.bindings[0].effect, "방어유닛 숙주 카드는 통과");
            Assert.IsNull(both.bindings, "적 숙주도 켠 카드는 건너뛴다");
            StringAssert.Contains("'card/both' U20 — attack modifier 'front' (FrontmostTarget) on a card whose host_kinds = Defender, Enemy", log);
        }

        [Test]
        public void U20_UsesTheKindAfterThisImport()
        {
            // 같은 import 의 Skills 탭이 효과를 공격 변형으로 바꾸면 그 뒤 종류로 판정한다.
            var e = Effect("e", EffectKind.SelfTileAoe, 20f, 1);
            var enemy = New<AttackUnitData>(); enemy.id = "boss";
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                skills = new[] { new SkillRowDto { id = "e", kind = EffectKind.DamageVsSleeping, mul = 2f } },
                owners = new[] { new SkillOwnerRowDto { ownerKind = "enemy", ownerId = "boss", slot = 0, kind = TriggerKind.None, effectId = "e" } },
            }, Index(new[] { e }, enemies: new[] { enemy }), true, null, new StringBuilder());
            StringAssert.Contains("U20 — attack modifier 'e' (DamageVsSleeping)", log);
            Assert.IsNull(enemy.bindings);
        }

        [Test]
        public void EnemyOwned_FactionStatBuff_NotAll_Warns_ButApplies()
        {
            var buff = New<EffectData>();
            buff.id = "buff";
            buff.values = new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.AttackDamage, percent = 10f, allyFilter = CardTargetAxis.ClassRanger };
            var enemy = New<AttackUnitData>(); enemy.id = "boss";
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                owners = new[] { new SkillOwnerRowDto { ownerKind = "enemy", ownerId = "boss", slot = 0, kind = TriggerKind.None, effectId = "buff" } },
            }, Index(new[] { buff }, enemies: new[] { enemy }), true, null, new StringBuilder());
            StringAssert.Contains("'enemy/boss' warning — enemy-owned FactionStatBuff 'buff' ally_filter=ClassRanger", log);
            Assert.AreSame(buff, enemy.bindings[0].effect, "경고일 뿐 — 적용한다");
        }

        // ── skill-data-table unit 9 — 종류별 사용 칸(`EffectSlots.UsedColumns`) ─────────────────────────────

        [Test]
        public void Export_WritesOnlyColumnsTheKindUses()
        {
            // 배치 오라 — 옛 이전이 채운 cc_kind · stack_kind 기본값이 에셋에 남아 있어도 시트에는 안 나온다.
            var aura = New<EffectData>();
            aura.id = "aura";
            aura.values = new EffectValues
            {
                kind = EffectKind.PlacementAura, percent = 50f, durationSec = 2f, allyFilter = CardTargetAxis.All,
                ccKind = Wassup.Data.Authoring.CcKind.Stun, stackKind = Wassup.Data.Authoring.StackKind.Fire, damage = 7f,
            };
            aura.projectile = New<ProjectileData>();
            aura.projectile.id = "stray_projectile";
            var row = SkillSheet.Export(new[] { aura }, null, null, null).skills.Single();
            Assert.AreEqual(50f, row.percent);
            Assert.AreEqual(2f, row.durationSec);
            Assert.AreEqual(CardTargetAxis.All, row.allyFilter);
            Assert.IsNull(row.ccKind, "오라는 cc_kind 를 안 쓴다");
            Assert.IsNull(row.stackKind, "오라는 stack_kind 를 안 쓴다");
            Assert.IsNull(row.damage);
            Assert.IsNull(row.projectileId, "안 쓰는 참조 칸도 안 나온다");
            string json = SkillSheet.ToJson(new[] { row });
            StringAssert.DoesNotContain("cc_kind", json);
            StringAssert.DoesNotContain("stack_kind", json);
        }

        [Test]
        public void Import_UnusedColumn_WarnsAndIgnores_UsedColumnApplies()
        {
            var aura = New<EffectData>();
            aura.id = "aura";
            aura.values = new EffectValues { kind = EffectKind.PlacementAura, percent = 50f };
            var proj = New<ProjectileData>();
            proj.id = "p";
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                skills = new[] { new SkillRowDto { id = "aura", percent = 30f, ccKind = Wassup.Data.Authoring.CcKind.Sleep, damage = 9f, projectileId = "p" } },
            }, Index(new[] { aura }, projectiles: new[] { proj }), true, null, new StringBuilder());

            Assert.AreEqual(30f, aura.values.percent, "쓰는 칸은 반영");
            Assert.AreEqual(default(Wassup.Data.Authoring.CcKind), aura.values.ccKind, "안 쓰는 칸은 무시");
            Assert.AreEqual(0f, aura.values.damage);
            Assert.IsNull(aura.projectile, "안 쓰는 참조 칸도 무시");
            StringAssert.Contains("'aura' cc_kind=Sleep — kind PlacementAura does not use this column; ignored.", log);
            StringAssert.Contains("'aura' damage=9 — kind PlacementAura does not use this column; ignored.", log);
            StringAssert.Contains("'aura' projectile_id='p' — kind PlacementAura does not use this column; ignored.", log);
            StringAssert.Contains("ignored cells 3", log);
        }

        [Test]
        public void Import_KindChange_UsesTheNewKindsColumns()
        {
            // 줄이 종류를 바꾸면 칸 규칙도 새 종류를 따른다(자리 폭발 → 광역 지속 피해: tick_sec 이 새로 쓰는 칸).
            var e = Effect("x", EffectKind.SelfTileAoe, 20f, 1);
            string log = SkillSheet.Import(new SkillSheetPayload
            {
                skills = new[] { new SkillRowDto { id = "x", kind = EffectKind.AreaDot, tickSec = 0.5f, flightSec = 3f } },
            }, Index(new[] { e }), true, null, new StringBuilder());
            Assert.AreEqual(EffectKind.AreaDot, e.values.kind);
            Assert.AreEqual(0.5f, e.values.tickSec);
            Assert.AreEqual(0f, e.values.flightSec, "새 종류(AreaDot)는 flight_sec 을 안 쓴다");
            StringAssert.Contains("flight_sec=3 — kind AreaDot does not use this column; ignored.", log);
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
