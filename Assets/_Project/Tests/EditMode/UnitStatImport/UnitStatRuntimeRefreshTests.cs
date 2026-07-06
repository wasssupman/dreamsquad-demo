using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // runtime-stat-refresh Unit 1 — catalog-based apply core, driven without a
    // network via UnitStatRuntimeRefresher.ApplyBodies (string bodies in).
    public class UnitStatRuntimeRefreshTests
    {
        private static string SuccessBody(string rowsJson) => $"{{ \"success\": true, \"data\": [{rowsJson}] }}";
        private const string ErrorBody = @"{ ""success"": false, ""errorDetail"": { ""errorCode"": ""INTERNAL_SERVER_ERROR"", ""detailMessage"": ""구글 시트 연동 실패"" } }";

        private static DefenderCatalog MakeDefenderCatalog(params DefenderUnitData[] units)
        {
            var catalog = ScriptableObject.CreateInstance<DefenderCatalog>();
            catalog.units = units;
            return catalog;
        }

        private static EnemyCatalog MakeEnemyCatalog(params AttackUnitData[] units)
        {
            var catalog = ScriptableObject.CreateInstance<EnemyCatalog>();
            catalog.units = units;
            return catalog;
        }

        private static DefenderUnitData Defender(string id, float health)
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.id = id;
            so.health = health;
            so.outputs = new[] { new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 1f } };
            return so;
        }

        private static AttackUnitData Enemy(string id, float health)
        {
            var so = ScriptableObject.CreateInstance<AttackUnitData>();
            so.id = id;
            so.health = health;
            so.outputs = new[] { new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 1f } };
            return so;
        }

        [Test]
        public void EnemyCatalog_ById_ResolvesAndNullOnMiss()
        {
            var basic = Enemy("basic", 60f);
            var catalog = MakeEnemyCatalog(basic);

            Assert.AreSame(basic, catalog.ById("basic"));
            Assert.IsNull(catalog.ById("nope"));
            Assert.IsNull(catalog.ById(null));

            Object.DestroyImmediate(basic);
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void BuildIndex_DuplicateId_DropsAllIncludingThird()
        {
            // 3+ assets sharing an id must ALL stay dropped (the pre-refactor
            // editor scan re-admitted the third one).
            var a = Enemy("dup", 1f);
            var b = Enemy("dup", 2f);
            var c = Enemy("dup", 3f);
            var log = new StringBuilder();

            var index = UnitStatApplier.BuildIndex(new[] { a, b, c }, so => so.id, log, "test");

            Assert.IsFalse(index.ContainsKey("dup"), "ambiguous id must not be writable");
            StringAssert.Contains("duplicate asset id 'dup'", log.ToString());

            Object.DestroyImmediate(a);
            Object.DestroyImmediate(b);
            Object.DestroyImmediate(c);
        }

        [Test]
        public void ApplyBodies_UpdatesCatalogInstancesInMemory()
        {
            var archer = Defender("archer", 500f);
            var basic = Enemy("basic", 60f);
            var defenderCatalog = MakeDefenderCatalog(archer);
            var enemyCatalog = MakeEnemyCatalog(basic);

            string log = UnitStatRuntimeRefresher.ApplyBodies(
                SuccessBody(@"{ ""id"": ""archer"", ""health"": ""450"", ""atk"": ""20"" }"),
                SuccessBody(@"{ ""id"": ""basic"", ""health"": 80 }"),
                "Defenders", "Enemies", defenderCatalog, enemyCatalog);

            Assert.AreEqual(450f, archer.health);
            Assert.AreEqual(20f, archer.outputs[0].magnitude, "atk must project onto the unique Damage output");
            Assert.AreEqual(80f, basic.health);
            StringAssert.Contains("Matched 2, unmatched 0", log);

            Object.DestroyImmediate(archer);
            Object.DestroyImmediate(basic);
            Object.DestroyImmediate(defenderCatalog);
            Object.DestroyImmediate(enemyCatalog);
        }

        [Test]
        public void ApplyBodies_OneSheetFails_AppliesHealthySheetOnly()
        {
            var archer = Defender("archer", 500f);
            var defenderCatalog = MakeDefenderCatalog(archer);
            var enemyCatalog = MakeEnemyCatalog();

            string log = UnitStatRuntimeRefresher.ApplyBodies(
                SuccessBody(@"{ ""id"": ""archer"", ""health"": 450 }"),
                ErrorBody,
                "Defenders", "Enemies", defenderCatalog, enemyCatalog);

            Assert.AreEqual(450f, archer.health, "healthy sheet must still apply");
            StringAssert.Contains("[Enemies] fetch failed", log);
            StringAssert.Contains("구글 시트 연동 실패", log);

            Object.DestroyImmediate(archer);
            Object.DestroyImmediate(defenderCatalog);
            Object.DestroyImmediate(enemyCatalog);
        }

        [Test]
        public void ApplyBodies_BothSheetsFail_ReturnsErrorsWithoutApply()
        {
            var archer = Defender("archer", 500f);
            var defenderCatalog = MakeDefenderCatalog(archer);
            var enemyCatalog = MakeEnemyCatalog();

            string log = UnitStatRuntimeRefresher.ApplyBodies(
                ErrorBody, ErrorBody, "Defenders", "Enemies", defenderCatalog, enemyCatalog);

            Assert.AreEqual(500f, archer.health, "no apply may happen when both sheets fail");
            StringAssert.DoesNotContain("Matched", log);

            Object.DestroyImmediate(archer);
            Object.DestroyImmediate(defenderCatalog);
            Object.DestroyImmediate(enemyCatalog);
        }
    }
}
