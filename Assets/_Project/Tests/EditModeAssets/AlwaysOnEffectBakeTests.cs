using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditModeAssets
{
    // skill-data-table unit 8 — **상시 효과도 스킬 줄이다**(계약 11). 카드 전용 저장처 둘(`effects` · `attackMods`)을 효과 줄 + 소유 줄(트리거
    // `None`)로 옮긴 뒤 굽기가 옛 두 갈래와 같은 코어 모양을 내는가(빌더 픽스처 — 합성 SO · 디스크 쓰기 0 · 옛 칸은 단계 B 에서 은퇴).
    //
    // ① 진영 버프 줄 = 옛 스쿼드 굽기와 같은 코어 줄(라벨 · 값 · `SquadBindings`) · 수식자 = 규칙 줄 0 · 옛 값 가드(라이브 동치 = 굽기 스냅샷).
    // ② 두 카드가 같은 진영 버프 효과를 참조하면 같은 코어 줄 모양 · 같은 효과 줄(복사 0).
    // ③ 방어유닛 · 적이 상시 효과를 들면 굽기가 「배선 전」을 말하고 뺀다(unit 7 후속).
    // ④ Squad 카드에 다른 종류 줄 → 거절. ⑥ 배치 오라의 수혜 대상 = 효과 칸(카드 축은 안 읽는다 — 단계 B).
    public class AlwaysOnEffectBakeTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        private EffectData Effect(string id, EffectValues v)
        {
            var e = Make<EffectData>();
            e.id = id;
            e.name = "Effect_" + id;
            e.values = v;
            return e;
        }

        private static BindingSpec Always(EffectData e) => new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.None }, effect = e };

        private DreamcatcherCard Card(string id, CardType type, CardTargetAxis axis = CardTargetAxis.All)
        {
            var c = Make<DreamcatcherCard>();
            c.id = id;
            c.name = "Card_" + id;
            c.type = type;
            c.axis = axis;
            c.hostKinds = HostKinds.Defender;
            return c;
        }

        private static AwakeningConfig AwakeningAsset()
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            return AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static MatchDefinition Bake(params DreamcatcherCard[] cards)
        {
            var def = new MatchDefinition();
            var list = new List<DreamcatcherCard>(cards);
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = list, Awakening = AwakeningAsset() },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(), CardDefinitionBuilder.WithCardHazards(null, list));
            return def;
        }

        // ── ① 코어 줄 모양(옛 BakeSquad · attackMods 루프와 같은 값 — 라이브 동치는 굽기 스냅샷이 증언한다) ───────────

        [Test]
        public void 진영_버프_줄은_효과_종류로_SquadBindings_에_가고_옛_라벨_모양이다()
        {
            var card = Card("fx_fortress", CardType.Squad, CardTargetAxis.All);   // 카드 축은 수혜 대상이 아니다
            card.bindings = new[]
            {
                Always(Effect("fx_fortress_0", new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.EffectiveHealth, percent = 50f, allyFilter = CardTargetAxis.ClassGuardian })),
                Always(Effect("fx_fortress_1", new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.AttackSpeed, percent = -50f, allyFilter = CardTargetAxis.ClassGuardian })),
            };
            var def = Bake(card);
            Assert.AreEqual(2, def.Cards[0].SquadBindings.Length, "진영 버프 줄은 효과 종류로 SquadBindings 에 간다");
            Assert.IsNull(def.Cards[0].Bindings);
            var r0 = def.Bindings[def.Cards[0].SquadBindings[0]];
            var r1 = def.Bindings[def.Cards[0].SquadBindings[1]];
            Assert.AreEqual("카드 'fx_fortress' effect 0", r0.Label, "옛 라벨 모양(굽기 스냅샷 글자 동치)");
            Assert.AreEqual("카드 'fx_fortress' effect 1", r1.Label);
            Assert.AreEqual(1 << (int)DefenderClass.Guardian, r0.SubjectClassMask, "수혜 대상 = 효과 칸");
            Assert.AreEqual("fx_fortress_0", def.EffectOf(in r0).Id, "효과 id = 저작 id");
            Assert.AreEqual((int)SkillStatKind.DmgTakenMul, def.EffectOf(in r0).StatKind, "체력 = 받는 피해 대리(역수)");
            Assert.AreEqual(1f / 1.5f, def.EffectOf(in r0).Magnitude, 1e-6f);
            Assert.AreEqual(0.5f, def.EffectOf(in r1).Magnitude, 1e-6f, "-50% 공속 = ×0.5");
        }

        [Test]
        public void 공격_수식자_줄은_규칙_줄을_안_만들고_소유_줄_순서로_접힌다()
        {
            var card = Card("fx_mods", CardType.Unit);
            card.bindings = new[]
            {
                Always(Effect("fx_mods_0", new EffectValues { kind = EffectKind.ProjectileBounce, count = 2, rangeTiles = 3, mul = 1f })),
                Always(Effect("fx_mods_1", new EffectValues { kind = EffectKind.FrontmostTarget, count = 7, rangeTiles = 7, mul = 1.2f })),
                Always(Effect("fx_mods_2", new EffectValues { kind = EffectKind.DamageVsSleeping, mul = 2f })),
            };
            var def = Bake(card);
            Assert.AreEqual(0, def.Bindings.Length, "수식자는 규칙 줄을 만들지 않는다(줄 번호가 밀리지 않는다)");
            Assert.IsNull(def.Cards[0].Bindings);
            Assert.IsNull(def.Cards[0].SquadBindings);
            var mods = def.Cards[0].AttackMods;
            Assert.AreEqual(3, mods.Length);
            Assert.AreEqual(AttackModKind.ProjectileBounce, mods[0].Kind);
            Assert.AreEqual(2, mods[0].Count);
            Assert.AreEqual(3, mods[0].TileRange);
            Assert.AreEqual(1f, mods[0].DamageMul);
            Assert.AreEqual(AttackModKind.FrontmostTarget, mods[1].Kind);
            Assert.AreEqual(0, mods[1].Count, "최전방은 수 · 반경을 안 읽는다(사용 칸 표)");
            Assert.AreEqual(0, mods[1].TileRange);
            Assert.AreEqual(1.2f, mods[1].DamageMul);
            Assert.AreEqual(AttackModKind.DamageVsSleeping, mods[2].Kind);
        }

        [Test]
        public void 공격_수식자_값_가드는_옛_카드_경로와_같다()
        {
            var card = Card("fx_mod_guards", CardType.Unit);
            card.bindings = new[]
            {
                Always(Effect("fx_bounce_zero", new EffectValues { kind = EffectKind.ProjectileBounce, count = 0, rangeTiles = 3, mul = 1f })),
                Always(Effect("fx_sleep_one", new EffectValues { kind = EffectKind.DamageVsSleeping, mul = 1f })),
                Always(Effect("fx_front_zero", new EffectValues { kind = EffectKind.FrontmostTarget, mul = 0f })),
            };
            LogAssert.Expect(LogType.Warning, new Regex("ProjectileBounce count <= 0"));
            LogAssert.Expect(LogType.Warning, new Regex("DamageVsSleeping mul <= 1"));
            LogAssert.Expect(LogType.Warning, new Regex("배율\\(mul\\) <= 0"));
            LogAssert.ignoreFailingMessages = true;   // 「구워진 규칙이 하나도 없다」 오류(의도)
            try { Assert.IsNull(Bake(card).Cards[0].AttackMods); }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        // ── ② 같은 효과 두 소유자 ────────────────────────────────────────────────

        [Test]
        public void 두_카드가_같은_진영_버프_효과를_참조하면_같은_코어_줄_모양이다()
        {
            var effect = Effect("fx_ranger_atk", new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.AttackDamage, percent = 10f, allyFilter = CardTargetAxis.ClassRanger });
            var a = Card("fx_a", CardType.Squad, CardTargetAxis.All);   // 카드 축은 수혜 대상이 아니다(효과의 뜻 — 계약 12)
            var b = Card("fx_b", CardType.Squad, CardTargetAxis.Cost1);
            a.bindings = new[] { Always(effect) };
            b.bindings = new[] { Always(effect) };
            var def = Bake(a, b);
            var ra = def.Bindings[def.Cards[0].SquadBindings[0]];
            var rb = def.Bindings[def.Cards[1].SquadBindings[0]];
            Assert.AreEqual(ra.EffectIndex, rb.EffectIndex, "같은 효과 에셋 = 같은 효과 줄(복사 0)");
            foreach (var r in new[] { ra, rb })
            {
                Assert.AreEqual(TriggerKind.OnPlace, r.Trigger, "남의 배치");
                Assert.AreEqual(BindingSubject.Any, r.Subject);
                Assert.AreEqual(1 << (int)DefenderClass.Ranger, r.SubjectClassMask, "수혜 대상 = 효과 칸(레인저)");
                Assert.AreEqual(0, r.SubjectCost);
                Assert.IsTrue(r.RevokeOnExpire, "숙주가 떠나면 소급 회수");
                Assert.AreEqual(BindingOrigin.Card, r.Origin);
                Assert.AreEqual(SelfStatBuffSkill.Id, r.SkillId);
                Assert.AreEqual(0, r.FireCap);
            }
            var fx = def.EffectOf(in ra);
            Assert.AreEqual(EffectKind.SelfStatBuff, fx.Kind, "코어 줄 = 자기 스탯 버프(영구)");
            Assert.AreEqual(1.1f, fx.Magnitude, 1e-6f);
            Assert.AreEqual("fx_ranger_atk", fx.Id);
        }

        // ── ③ 방어유닛 · 적 = 배선 전 ────────────────────────────────────────────

        private static List<T> AssetsByPath<T>(string filter) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { DataRoot })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<T>();
            foreach (var p in paths)
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(p);
                if (a != null && !list.Contains(a)) list.Add(a);
            }
            return list;
        }

        private T Clone<T>(T src, string name) where T : ScriptableObject
        {
            var c = Object.Instantiate(src);
            c.name = name;
            _made.Add(c);
            return c;
        }

        private BindingSpec[] AllFour()
            => new[]
            {
                Always(Effect("fx_buff", new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.AttackDamage, percent = 10f, allyFilter = CardTargetAxis.All })),
                Always(Effect("fx_bounce", new EffectValues { kind = EffectKind.ProjectileBounce, count = 2, rangeTiles = 3, mul = 1f })),
                Always(Effect("fx_front", new EffectValues { kind = EffectKind.FrontmostTarget, mul = 1.2f })),
                Always(Effect("fx_sleep", new EffectValues { kind = EffectKind.DamageVsSleeping, mul = 2f })),
            };

        [Test]
        public void 방어유닛_적이_상시_효과를_들면_굽기가_배선_전을_말하고_뺀다()
        {
            var units = AssetsByPath<DefenderUnitData>("t:DefenderUnitData");
            var enemies = AssetsByPath<AttackUnitData>("t:AttackUnitData");
            Assert.IsNotEmpty(units);
            Assert.IsNotEmpty(enemies);
            var unit = Clone(units[0], "Fixture_Defender");
            unit.bindings = AllFour();
            var enemy = Clone(enemies[0], "Fixture_Enemy");
            enemy.bindings = AllFour();
            enemy.splitUnit = null;
            for (int i = 0; i < 8; i++) LogAssert.Expect(LogType.Warning, new Regex("배선 전"));

            var def = new MatchDefinition { Units = new[] { MatchDefinitionBuilder.ToUnitDef(unit) }, Enemies = new EnemyDef[1] };
            BindingDefinitionBuilder.Fill(def, new List<DefenderUnitData> { unit }, new[] { enemy }, new List<ProjectileData>(),
                                          new List<ProjectilePatternData>(), System.Array.Empty<HazardSO>(), new MatchViewAssets());
            Assert.IsNull(def.Units[0].Bindings, "방어유닛 — 진영 버프 줄을 조용히 반쪽만 굽지 않는다");
            Assert.IsNull(def.Units[0].Attack.Mods, "방어유닛 — 수식자도 배선 전");
            Assert.IsNull(def.Enemies[0].Bindings);
            Assert.IsNull(def.Enemies[0].Attack.Mods);
            Assert.AreEqual(0, def.Bindings.Length);
        }

        [Test]
        public void 적_숙주도_켠_카드의_상시_효과는_배선_전이다()
        {
            var card = Card("fx_both_hosts", CardType.Unit);
            card.hostKinds = HostKinds.Defender | HostKinds.Enemy;
            card.bindings = new[]
            {
                Always(Effect("fx_front_both", new EffectValues { kind = EffectKind.FrontmostTarget, mul = 1.2f })),
                // 강타는 그대로(AttackN — 두 숙주 모두 사건이 있다) — 카드가 빈 채로 굽히지 않게 한 줄 둔다.
                new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 3 },
                                  effect = Effect("fx_heavy_both", new EffectValues { kind = EffectKind.HeavyStrike, mul = 2f }) },
            };
            LogAssert.Expect(LogType.Warning, new Regex("배선 전"));
            var def = Bake(card);
            var mods = def.Cards[0].AttackMods;
            Assert.AreEqual(1, mods.Length, "적 숙주 쪽이 배선 전이면 그 줄 전체를 뺀다(한쪽만 도는 상태 금지)");
            Assert.AreEqual(AttackModKind.HeavyStrike, mods[0].Kind, "강타는 그대로");
        }

        // ── ④ Squad 카드 분류 ────────────────────────────────────────────────────

        [Test]
        public void Squad_카드에_다른_종류_줄은_거절된다()
        {
            var card = Card("fx_squad_mixed", CardType.Squad);
            card.bindings = new[]
            {
                Always(Effect("fx_squad_buff", new EffectValues { kind = EffectKind.FactionStatBuff, buffStat = CardBuffKind.MoveSpeed, percent = 10f, allyFilter = CardTargetAxis.All })),
                Always(Effect("fx_squad_bounce", new EffectValues { kind = EffectKind.ProjectileBounce, count = 1, rangeTiles = 2, mul = 1f })),
                new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.AttackN, period = 3 },
                                  effect = Effect("fx_squad_self", new EffectValues { kind = EffectKind.SelfStatBuff, buffStat = CardBuffKind.AttackDamage, percent = 10f }) },
            };
            LogAssert.Expect(LogType.Warning, new Regex("Squad 카드는 아군 전체 스탯"));
            LogAssert.Expect(LogType.Warning, new Regex("Squad 카드는 아군 전체 스탯"));
            var def = Bake(card);
            Assert.AreEqual(1, def.Cards[0].SquadBindings.Length, "진영 버프 줄만 남는다");
            Assert.IsNull(def.Cards[0].Bindings);
            Assert.IsNull(def.Cards[0].AttackMods);
            Assert.AreEqual(1, def.Bindings.Length);
        }

        // ── ⑥ 배치 오라의 수혜 대상 ─────────────────────────────────────────────

        [Test]
        public void 배치_오라의_수혜_대상은_효과_칸이다_카드_축을_안_읽는다()
        {
            // 단계 B — 카드 축 폴백 은퇴(라이브 `slow_awakening` 효과 = All · 이전 43e6d841f).
            var aura = Effect("fx_aura", new EffectValues { kind = EffectKind.PlacementAura, percent = 50f, durationSec = 0f, allyFilter = CardTargetAxis.All });
            var card = Card("fx_aura_card", CardType.Unit, CardTargetAxis.ClassGuardian);
            card.bindings = new[] { Always(aura) };

            var def = Bake(card);
            var r = def.Bindings[def.Cards[0].Bindings[0]];
            Assert.AreEqual(0, r.SubjectClassMask, "효과 칸 All — 카드 축(가디언)은 수혜 대상이 아니다");
            Assert.AreEqual(0, r.SubjectCost);

            aura.values.allyFilter = CardTargetAxis.ClassGuardian;
            def = Bake(card);
            r = def.Bindings[def.Cards[0].Bindings[0]];
            Assert.AreEqual(1 << (int)DefenderClass.Guardian, r.SubjectClassMask, "효과의 뜻(계약 12)");
        }
    }
}
