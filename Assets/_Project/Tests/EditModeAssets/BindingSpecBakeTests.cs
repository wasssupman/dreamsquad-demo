using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // skill-data-table unit 4 — **새 저작 형식**(효과 에셋 참조 소유 줄)을 굽는 한 경로의 증언.
    //
    // ① 이전 전 증명: 라이브 저작 전부에 이전 계획(`LegacyBindingMigration`)을 **메모리 사본**에 입혀 굽고, 옛 굽기와 규칙 줄 값이 같다
    //    (라벨 텍스트 제외 · 알려진 해시 변화 = dry-run 표의 깃발 하나 — 실드 캐스트 반각). 에셋은 쓰지 않는다.
    // ② 검증 질문 ① — 같은 효과 에셋을 두 소유자(방어유닛 배치 · 카드 「남의 배치」)가 참조하면 **같은 효과 줄**을 가리킨다.
    // ③ 하드 케이스 3 신설 — 방어유닛이 짱쎈의 도약 효과를 **같은 id 로** 소유한다(자리 문제는 범위 밖 — 코어 탐침의 `[Ignore]` 그대로).
    public class BindingSpecBakeTests
    {
        private const string DataRoot = "Assets/_Project/Data";
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

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

        private T Clone<T>(T src) where T : ScriptableObject
        {
            var c = Object.Instantiate(src);
            c.name = src.name;   // 라벨 머리 = 에셋 이름
            _made.Add(c);
            return c;
        }

        private T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        private static string BakeHosts(List<DefenderUnitData> units, List<AttackUnitData> enemies, List<string> paths)
        {
            var def = new MatchDefinition { Units = new UnitDef[units.Count], Enemies = new EnemyDef[enemies.Count] };
            for (int i = 0; i < units.Count; i++) def.Units[i] = MatchDefinitionBuilder.ToUnitDef(units[i]);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                BindingDefinitionBuilder.Fill(def, units, enemies.ToArray(), new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                              System.Array.Empty<HazardSO>(), new MatchViewAssets());
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            var sb = new StringBuilder();
            for (int i = 0; i < units.Count; i++) Host(sb, CardProbe.CanonicalHostRulesText(def, false, i), paths[i]);
            for (int i = 0; i < enemies.Count; i++) Host(sb, CardProbe.CanonicalHostRulesText(def, true, i), paths[units.Count + i]);
            return sb.ToString();
        }

        private static void Host(StringBuilder sb, string body, string path)
        {
            if (body.Length == 0) return;
            sb.Append("[host ").Append(path).Append("]\n").Append(body);
        }

        private static string BakeCards(List<DreamcatcherCard> cards)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { return CardBakeSnapshotTests.Bake(cards); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        // 두 굽기 텍스트의 차이 — 규칙 머리줄은 번호만 본다(라벨 = 해시 밖). 차이 줄마다 「그 줄이 속한 옛 규칙 라벨 · 옛 값 → 새 값」.
        private static List<string> Diffs(string before, string after)
        {
            var a = before.Split('\n');
            var b = after.Split('\n');
            var diffs = new List<string>();
            string rule = "?";
            for (int i = 0; i < System.Math.Max(a.Length, b.Length); i++)
            {
                string x = i < a.Length ? a[i] : "<끝>";
                string y = i < b.Length ? b[i] : "<끝>";
                if (x.StartsWith("[rule ")) { rule = x; x = RuleHead(x); y = RuleHead(y); }
                if (x != y) diffs.Add($"{rule} :: `{x}` → `{y}`");
            }
            return diffs;
        }

        private static string RuleHead(string line)
        {
            int close = line.IndexOf(']');
            return close > 0 ? line.Substring(0, close + 1) : line;
        }

        [Test]
        public void 이전_계획을_입힌_사본의_굽기가_옛_굽기와_값으로_같다()
        {
            var units = AssetsByPath<DefenderUnitData>("t:DefenderUnitData");
            var enemies = AssetsByPath<AttackUnitData>("t:AttackUnitData");
            var cards = CardEffectWitnessTests.Cards();
            var paths = new List<string>();
            foreach (var u in units) paths.Add(AssetDatabase.GetAssetPath(u));
            foreach (var e in enemies) paths.Add(AssetDatabase.GetAssetPath(e));

            string hostsBefore = BakeHosts(units, enemies, paths);
            string cardsBefore = BakeCards(cards);

            var plan = LegacyBindingMigration.Build(cards, units, enemies);
            var clones = new Dictionary<ScriptableObject, ScriptableObject>();
            var unitClones = new List<DefenderUnitData>();
            var enemyClones = new List<AttackUnitData>();
            var cardClones = new List<DreamcatcherCard>();
            foreach (var u in units) { var c = Clone(u); clones[u] = c; unitClones.Add(c); }
            foreach (var e in enemies) { var c = Clone(e); clones[e] = c; enemyClones.Add(c); }
            foreach (var k in cards) { var c = Clone(k); clones[k] = c; cardClones.Add(c); }
            int rows = 0;
            foreach (var o in plan.Owners)
            {
                rows += o.Rows.Count;
                LegacyBindingMigration.Apply(o, clones[o.Owner], r =>
                {
                    var fx = LegacyBindingMigration.Materialize(r);
                    _made.Add(fx);
                    return fx;
                });
            }
            Assert.Greater(rows, 0, "이전할 규칙이 하나도 없다면 테스트가 공허하다");

            string hostsAfter = BakeHosts(unitClones, enemyClones, paths);
            string cardsAfter = BakeCards(cardClones);

            var diffs = Diffs(hostsBefore, hostsAfter);
            diffs.AddRange(Diffs(cardsBefore, cardsAfter));
            // 알려진 해시 변화(dry-run 깃발): 실드 캐스트 줄은 옛 전용 굽기가 반각을 안 구워 (0,0) 이었다 — 일반 경로는 (0,1).
            var unexpected = diffs.FindAll(d => !(d.Contains("실드 캐스트") && d.Contains("coneSinCos=0,0") && d.Contains("coneSinCos=0,1")));
            foreach (var d in diffs) TestContext.WriteLine(d);
            Assert.IsEmpty(unexpected, "새 형식 굽기가 옛 굽기와 갈렸다:\n" + string.Join("\n", unexpected));
        }

        [Test]
        public void 같은_효과_에셋을_두_소유자가_참조하면_같은_효과_줄이다()
        {
            // 검증 질문 ① — 캐논 배치 폭격(내 배치) · 개사기(남의 배치)가 **같은 효과**를 트리거만 달리 참조한다(복사 없음).
            var barrel = Make<ProjectileData>();
            barrel.flightMode = ProjectileFlightMode.Homing;
            barrel.id = "fixture_barrel";
            var pattern = Make<ProjectilePatternData>();
            pattern.id = "fixture_strike";
            pattern.barrel = barrel;
            pattern.scopeTileRange = 2;
            pattern.fanOutToAllCandidates = true;
            var effect = Make<EffectData>();
            effect.id = "fixture_strike";
            effect.values = new EffectValues { kind = EffectKind.EmitProjectilePattern, damage = 100f };
            effect.pattern = pattern;

            var unit = Make<DefenderUnitData>();
            unit.name = "Fixture_Cannon";
            unit.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.OnPlace }, effect = effect } };
            var card = Make<DreamcatcherCard>();
            card.id = "fixture_gaesagi";
            card.type = CardType.Unit;
            card.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.OnPlace, subject = BindingSubject.Any }, effect = effect } };

            var def = new MatchDefinition { Units = new[] { MatchDefinitionBuilder.ToUnitDef(unit) }, Enemies = System.Array.Empty<EnemyDef>() };
            var projectiles = new List<ProjectileData>();
            var patterns = new List<ProjectilePatternData>();
            BindingDefinitionBuilder.Fill(def, new List<DefenderUnitData> { unit }, System.Array.Empty<AttackUnitData>(), projectiles, patterns,
                                          System.Array.Empty<HazardSO>(), new MatchViewAssets());
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = new List<DreamcatcherCard> { card }, Awakening = AwakeningAsset() },
                                       projectiles, patterns, System.Array.Empty<HazardSO>());

            Assert.IsNotNull(def.Units[0].Bindings);
            Assert.IsNotNull(def.Cards[0].Bindings);
            var own = def.Bindings[def.Units[0].Bindings[0]];
            var others = def.Bindings[def.Cards[0].Bindings[0]];
            Assert.AreEqual(BindingSubject.Self, own.Subject);
            Assert.AreEqual(BindingSubject.Any, others.Subject, "트리거(주체)만 다르다");
            Assert.AreEqual(own.EffectIndex, others.EffectIndex, "같은 효과 에셋 = 같은 효과 줄(복사 없음)");
            Assert.AreEqual("fixture_strike", def.EffectOf(in own).Id, "효과 id = 저작 id(서버 어휘)");
            Assert.AreEqual(100f, def.EffectOf(in own).Damage, "U10 — 피해는 효과 줄에서");
        }

        [Test]
        public void 방어유닛이_짱쎈_도약_효과를_같은_id_로_소유한다()
        {
            // 하드 케이스 3 — 짱쎈(적)의 라이브 도약 규칙을 이전 계획으로 효과 에셋화하고, 그 **같은 에셋**을 방어유닛이 소유 줄로 든다.
            AttackUnitData jjangssen = null;
            foreach (var e in AssetsByPath<AttackUnitData>("t:AttackUnitData")) if (e.name == "Enemy_Boss_Jjangssen") jjangssen = e;
            Assert.IsNotNull(jjangssen, "라이브 짱쎈이 없다");
            var plan = LegacyBindingMigration.Build(null, null, new[] { jjangssen });
            Assert.AreEqual(1, plan.Owners.Count);
            LegacyBindingMigration.RowPlan blink = null;
            foreach (var r in plan.Owners[0].Rows) if (r.Values.kind == EffectKind.SelfBlink) { blink = r; break; }
            Assert.IsNotNull(blink, "짱쎈에 도약(SelfBlink) 규칙이 없다");
            var effect = LegacyBindingMigration.Materialize(blink);
            _made.Add(effect);

            var enemy = Clone(jjangssen);
            enemy.bindings = new[] { new BindingSpec { trigger = blink.Trigger, fireCap = blink.FireCap, effect = effect } };
            var unit = Make<DefenderUnitData>();
            unit.name = "Fixture_Defender";
            unit.bindings = new[] { new BindingSpec { trigger = blink.Trigger, fireCap = blink.FireCap, effect = effect } };

            var def = new MatchDefinition { Units = new[] { MatchDefinitionBuilder.ToUnitDef(unit) }, Enemies = new EnemyDef[1] };
            BindingDefinitionBuilder.Fill(def, new List<DefenderUnitData> { unit }, new[] { enemy }, new List<ProjectileData>(),
                                          new List<ProjectilePatternData>(), System.Array.Empty<HazardSO>(), new MatchViewAssets());
            Assert.IsNotNull(def.Units[0].Bindings, "방어유닛 소유 줄이 안 구워졌다");
            Assert.IsNotNull(def.Enemies[0].Bindings, "적 소유 줄이 안 구워졌다");
            var mine = def.Bindings[def.Units[0].Bindings[0]];
            var boss = def.Bindings[def.Enemies[0].Bindings[0]];
            Assert.AreEqual(boss.EffectIndex, mine.EffectIndex, "같은 효과 줄");
            Assert.AreEqual(blink.EffectId, def.EffectOf(in mine).Id, "같은 효과 id");
            Assert.AreEqual(EffectKind.SelfBlink, def.EffectOf(in mine).Kind);
            Assert.IsNotNull(mine.Skill, "방어유닛 소유 줄에도 실행자가 있다(발동한다)");
        }

        [Test]
        public void 카드는_켜진_숙주_종류마다_조합을_검증한다()
        {
            // U5 — 「남의 배치」는 적 숙주에게도 터진다(주체 = 남) · 「자기 퇴근」은 적 숙주에게 없다 → 적을 켜면 거절.
            var effect = Make<EffectData>();
            effect.id = "fixture_blast";
            effect.values = new EffectValues { kind = EffectKind.SelfTileAoe, damage = 10f, radiusTiles = 1 };
            effect.projectile = Make<ProjectileData>();
            var card = Make<DreamcatcherCard>();
            card.id = "fixture_retire_blast";
            card.type = CardType.Unit;
            card.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.OnRetire }, effect = effect } };

            card.hostKinds = HostKinds.Defender;
            Assert.AreEqual(1, BakeCardRules(card), "방어유닛 숙주 — 퇴근이 있다");
            card.hostKinds = HostKinds.Defender | HostKinds.Enemy;
            Assert.AreEqual(0, BakeCardRules(card), "적 숙주도 켜면 — 적은 퇴근하지 않는다(영영 안 터짐) → 거절");
        }

        [Test]
        public void 액티브_시전_줄의_비율형은_주인_없는_시전이라_거절된다()
        {
            var effect = Make<EffectData>();
            effect.id = "fixture_meteor";
            effect.values = new EffectValues { kind = EffectKind.ActiveMeteor, radiusTiles = 2, flightSec = 1f,
                                               magnitudeMode = MagnitudeMode.OwnerStatRatio, basisStat = BasisStat.Attack, ratio = 1f };
            effect.projectile = Make<ProjectileData>();
            var card = Make<DreamcatcherCard>();
            card.id = "fixture_active";
            card.type = CardType.Active;
            card.bindings = new[] { new BindingSpec { trigger = new TriggerSpec { kind = TriggerKind.Cast }, fireCap = 1, effect = effect } };
            var def = BakeCard(card);
            Assert.AreEqual(-1, def.Cards[0].ActiveBinding, "비율형 × 주인 없는 시전 = NoRatioBasis");

            effect.values.magnitudeMode = MagnitudeMode.Flat;
            effect.values.damage = 40f;
            def = BakeCard(card);
            Assert.GreaterOrEqual(def.Cards[0].ActiveBinding, 0, "고정 수치는 굽힌다");
            var row = def.Bindings[def.Cards[0].ActiveBinding];
            Assert.AreEqual(TriggerKind.None, row.Trigger, "시전은 규칙 줄에 None + 발동 상한으로 실린다(해시 무변)");
            Assert.AreEqual(BindingLifetime.UntilFireCap, row.Lifetime);
            Assert.AreEqual(40f, def.EffectOf(in row).Magnitude);
            Assert.AreEqual(1f, def.EffectOf(in row).Duration, "메테오 지속 = 낙하 예고");
        }

        private static AwakeningConfig AwakeningAsset()
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            return AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static MatchDefinition BakeCard(DreamcatcherCard card)
        {
            var def = new MatchDefinition();
            var cards = new List<DreamcatcherCard> { card };
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = AwakeningAsset() },
                                           new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                           CardDefinitionBuilder.WithCardHazards(null, cards));
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            return def;
        }

        private static int BakeCardRules(DreamcatcherCard card)
        {
            var def = BakeCard(card);
            return def.Cards[0].Bindings != null ? def.Cards[0].Bindings.Length : 0;
        }
    }
}
