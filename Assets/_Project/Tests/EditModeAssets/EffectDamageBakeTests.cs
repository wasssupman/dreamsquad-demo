using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Trigger;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // skill-data-table unit 1b(U10) — **피해는 효과 줄 한 칸**(`EffectDef.Damage`). 패턴 · 장판 · 길막 줄에서 피해 칸이 빠졌고,
    // unit 4 전까지는 빌더가 옛 SO 필드(`ProjectilePatternData.damage` · 장판 DoT `param1` · 길막 `explodeDamage` ·
    // `slamDamage`)를 읽어 효과 줄로 옮긴다. 이 테스트는 라이브 저작 전량에서 **옮긴 값 == 원래 SO 값**을 본다.
    // 규칙 줄 ↔ 메커닉 대응은 진단 이름(`{소유자} mechanic {i}` · `카드 '{id}' mechanic {i}`)으로 잇는다(테스트 전용).
    public class EffectDamageBakeTests
    {
        private const string DataRoot = "Assets/_Project/Data";

        private static List<T> All<T>(string filter) where T : Object
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

        private enum Source { None, Pattern, Blocker, Hazard, Slam }

        // 옛 저작에서 그 메커닉의 피해가 어디 있었나 — 종류가 정한다(U10 목록).
        private static float Expected(in DcMechanic m, out Source source)
        {
            var p = m.payload;
            switch (p.kind)
            {
                case DcPayloadKind.EmitProjectilePattern:
                    if (p.pattern != null && p.pattern.barrel != null && p.pattern.barrel.spawnBlocker != null)
                    { source = Source.Blocker; return p.pattern.barrel.spawnBlocker.explodeDamage; }
                    source = Source.Pattern;
                    return p.pattern != null ? p.pattern.damage : 0f;
                case DcPayloadKind.SpawnHazard:
                    source = Source.Hazard;
                    if (p.hazard?.effects != null)
                        foreach (var he in p.hazard.effects)
                            if (he.kind == Wassup.Data.Authoring.CcKind.DoT) return he.param1;
                    return 0f;
                case DcPayloadKind.SelfBlink:
                case DcPayloadKind.UltimateLeap:
                    source = Source.Slam;
                    return Mathf.Max(0f, p.slamDamage);
                default:
                    source = Source.None;
                    return 0f;
            }
        }

        private static void Check(MatchDefinition def, string labelPrefix, DcMechanic[] mechanics, Dictionary<Source, int> seen)
        {
            if (mechanics == null) return;
            for (int r = 0; r < def.Bindings.Length; r++)
            {
                var label = def.Bindings[r].Label;
                for (int i = 0; i < mechanics.Length; i++)
                {
                    string exact = labelPrefix + " mechanic " + i;
                    if (label != exact && !label.StartsWith(exact + " ")) continue;
                    float want = Expected(in mechanics[i], out var source);
                    Assert.AreEqual(want, def.EffectOf(in def.Bindings[r]).Damage, 1e-6f, $"'{label}' 의 효과 줄 피해 ≠ 원래 SO 값({source})");
                    if (source != Source.None && want > 0f) seen[source] = seen.TryGetValue(source, out int n) ? n + 1 : 1;
                }
            }
        }

        [Test]
        public void 라이브_굽기의_효과_줄_피해는_원래_SO_값과_같다()
        {
            var seen = new Dictionary<Source, int>();

            var units = All<DefenderUnitData>("t:DefenderUnitData");
            var enemies = All<AttackUnitData>("t:AttackUnitData");
            var def = new MatchDefinition { Units = new UnitDef[units.Count], Enemies = new EnemyDef[enemies.Count] };
            for (int i = 0; i < units.Count; i++) def.Units[i] = MatchDefinitionBuilder.ToUnitDef(units[i]);
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                BindingDefinitionBuilder.Fill(def, units, enemies.ToArray(), new List<ProjectileData>(),
                                              new List<ProjectilePatternData>(), System.Array.Empty<HazardSO>(), new MatchViewAssets());
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            foreach (var u in units) Check(def, u.name, u.GetAbility<UnitSkillAbility>()?.mechanics, seen);
            foreach (var e in enemies) Check(def, e.name, e.nightmareMechanics, seen);

            var cards = CardEffectWitnessTests.Cards();
            var cardDef = new MatchDefinition();
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try
            {
                CardDefinitionBuilder.Fill(cardDef, new CardAuthoring { Cards = cards, Awakening = null },
                                           new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                           CardDefinitionBuilder.WithCardHazards(null, cards));
            }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
            foreach (var c in cards) Check(cardDef, $"카드 '{c.id}'", c.mechanics, seen);

            // 공허하지 않게 — 라이브에 넷 다 있다(캐논 등 명세 · 폭탄맨 배럴 · 불씨 장판 · 짱쎈 도약).
            foreach (var s in new[] { Source.Pattern, Source.Blocker, Source.Hazard, Source.Slam })
                Assert.Greater(seen.TryGetValue(s, out int n) ? n : 0, 0, $"라이브에 {s} 피해가 없다면 테스트가 공허하다");
        }
    }
}
