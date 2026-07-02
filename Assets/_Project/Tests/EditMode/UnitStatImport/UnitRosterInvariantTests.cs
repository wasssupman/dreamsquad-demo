using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // unit-stat-projection Unit 3 — freezes the roster invariants the atk/heal
    // projection depends on: a kind with 0 or 2+ entries cannot be projected.
    // A failure here is a RENEGOTIATION prompt, not a hard prohibition — see the
    // assert messages and docs/spec/unit-stat-projection/README.md.
    public class UnitRosterInvariantTests
    {
        private const string DefenderFolder = "Assets/_Project/Data/Defenders";
        private const string EnemyFolder = "Assets/_Project/Data/Enemies";

        private const string DamageHint =
            "투영 규칙은 Damage 항목이 정확히 1개인 유닛만 지원한다. 2개+가 필요하면 " +
            "투영 규칙(spec unit 0)을 갱신하거나 이 유닛을 시트 비관리(atk 미사용)로 표기하라.";
        private const string HealHint =
            "투영 규칙은 Heal 항목이 정확히 1개인 유닛만 지원한다. 위와 동일하게 재협상하라.";

        [Test]
        public void AllDefenders_SatisfyProjectionInvariants()
        {
            AssertOutputInvariants(LoadAll<DefenderUnitData>(DefenderFolder), so => so.name, so => so.outputs);
        }

        [Test]
        public void AllEnemies_SatisfyProjectionInvariants()
        {
            AssertOutputInvariants(LoadAll<AttackUnitData>(EnemyFolder), so => so.name, so => so.outputs);
        }

        // Uniqueness is per-type: the importer matches defenders[] rows against the
        // Defenders folder and enemies[] rows against the Enemies folder in separate
        // id indexes, so a defender and an enemy may legitimately share an id
        // (e.g. Defender_Sniper / Enemy_Sniper).
        [Test]
        public void DefenderIds_NonEmptyAndUnique()
        {
            var seen = new Dictionary<string, string>();
            foreach (var so in LoadAll<DefenderUnitData>(DefenderFolder))
                AssertId(so.id, so.name, seen);
        }

        [Test]
        public void EnemyIds_NonEmptyAndUnique()
        {
            var seen = new Dictionary<string, string>();
            foreach (var so in LoadAll<AttackUnitData>(EnemyFolder))
                AssertId(so.id, so.name, seen);
        }

        private static void AssertId(string id, string assetName, Dictionary<string, string> seen)
        {
            Assert.IsFalse(string.IsNullOrEmpty(id), $"'{assetName}' has an empty id — the importer matches on id.");
            Assert.IsFalse(seen.ContainsKey(id),
                $"id '{id}' is shared by '{assetName}' and '{(seen.TryGetValue(id, out var other) ? other : "?")}' — import would skip both.");
            seen[id] = assetName;
        }

        private static void AssertOutputInvariants<T>(IEnumerable<T> assets, System.Func<T, string> nameOf, System.Func<T, AttackOutput[]> outputsOf)
        {
            foreach (var so in assets)
            {
                var outputs = outputsOf(so);
                if (outputs == null) continue;
                int damage = 0, heal = 0;
                foreach (var o in outputs)
                {
                    if (o.kind == AttackOutputKind.Damage) damage++;
                    else if (o.kind == AttackOutputKind.Heal) heal++;
                }
                Assert.LessOrEqual(damage, 1, $"'{nameOf(so)}' has {damage} Damage outputs. {DamageHint}");
                Assert.LessOrEqual(heal, 1, $"'{nameOf(so)}' has {heal} Heal outputs. {HealHint}");
            }
        }

        private static List<T> LoadAll<T>(string folder) where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }
    }
}
