using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Editor.UnitStatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 1 — regression coverage for the JSON contract
    // (string enums, targetClassMask flags array) and the partial-update mapper.
    public class UnitStatImportTests
    {
        [Test]
        public void Deserialize_DefenderPayload_ParsesStringEnumsAndNumbers()
        {
            const string json = @"{
                ""defenders"": [
                    { ""id"": ""archer"", ""role"": ""Ranger"", ""health"": 55, ""cost"": 2 }
                ],
                ""enemies"": []
            }";

            var payload = JsonConvert.DeserializeObject<UnitStatImportPayload>(json);

            Assert.AreEqual(1, payload.defenders.Length);
            var dto = payload.defenders[0];
            Assert.AreEqual("archer", dto.id);
            Assert.AreEqual(DefenderClass.Ranger, dto.role);
            Assert.AreEqual(55f, dto.health);
            Assert.AreEqual(2, dto.cost);
            Assert.IsNull(dto.attackRange, "omitted field must deserialize to null, not a default value");
        }

        [Test]
        public void Deserialize_TargetClassMask_CombinesFlagsFromArray()
        {
            const string json = @"{ ""defenders"": [], ""enemies"": [
                { ""id"": ""basic"", ""targetClassMask"": [""Ranger"", ""Guardian""] }
            ] }";

            var payload = JsonConvert.DeserializeObject<UnitStatImportPayload>(json);

            Assert.AreEqual(DefenderClassFlags.Ranger | DefenderClassFlags.Guardian, payload.enemies[0].targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskEverything_ParsesAsEverythingSentinel()
        {
            const string json = @"{ ""defenders"": [], ""enemies"": [
                { ""id"": ""basic"", ""targetClassMask"": [""Everything""] }
            ] }";

            var payload = JsonConvert.DeserializeObject<UnitStatImportPayload>(json);

            Assert.AreEqual(DefenderClassFlags.Everything, payload.enemies[0].targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskEmptyArray_ParsesAsNone()
        {
            const string json = @"{ ""defenders"": [], ""enemies"": [
                { ""id"": ""basic"", ""targetClassMask"": [] }
            ] }";

            var payload = JsonConvert.DeserializeObject<UnitStatImportPayload>(json);

            Assert.AreEqual(DefenderClassFlags.None, payload.enemies[0].targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskMixesEverythingWithOthers_Throws()
        {
            const string json = @"{ ""defenders"": [], ""enemies"": [
                { ""id"": ""basic"", ""targetClassMask"": [""Everything"", ""Ranger""] }
            ] }";

            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<UnitStatImportPayload>(json));
        }

        // hotfix ⑤ — one parsing rule for planners: member names are accepted
        // case-insensitively across plain enums and the flags array alike.
        [Test]
        public void Deserialize_TargetClassMaskLowercaseNames_Accepted()
        {
            const string json = @"{ ""defenders"": [], ""enemies"": [
                { ""id"": ""basic"", ""targetClassMask"": [""ranger"", ""guardian""] }
            ] }";

            var payload = JsonConvert.DeserializeObject<UnitStatImportPayload>(json);

            Assert.AreEqual(DefenderClassFlags.Ranger | DefenderClassFlags.Guardian, payload.enemies[0].targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskUnknownName_Throws()
        {
            const string json = @"{ ""defenders"": [], ""enemies"": [
                { ""id"": ""basic"", ""targetClassMask"": [""Rangr""] }
            ] }";

            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<UnitStatImportPayload>(json));
        }

        // hotfix ⑥ — a payload repeating the same id must apply only the first
        // row. Fake ids match no asset, so this exercises the dedup path
        // read-only against the real AssetDatabase.
        [Test]
        public void ApplyPayload_DuplicatePayloadId_SkipsSubsequentRows()
        {
            var payload = new UnitStatImportPayload
            {
                defenders = new[]
                {
                    new DefenderStatDto { id = "zz_dup_test", health = 1f },
                    new DefenderStatDto { id = "zz_dup_test", health = 2f },
                },
                enemies = new EnemyStatDto[0],
            };

            string log = UnitStatImportWindow.ApplyPayload(payload);

            StringAssert.Contains("duplicate row for id='zz_dup_test'", log);
        }

        // hotfix ④ — pins the clarified contract: displayName is a normal
        // partial-update field, overwritten when provided.
        [Test]
        public void ApplyNonNullFields_DisplayName_OverwritesWhenProvided()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.displayName = "Archer";

            var dto = new DefenderStatDto { id = "archer", displayName = "Longbow Archer" };
            UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual("Longbow Archer", so.displayName);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ApplyNonNullFields_OnlyOverwritesProvidedFields()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.id = "archer";
            so.health = 50f;
            so.attackRange = 3f;
            so.cost = 1;

            var dto = new DefenderStatDto { id = "archer", health = 999f }; // attackRange/cost intentionally omitted

            int applied = UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual(999f, so.health, "provided field must be overwritten");
            Assert.AreEqual(3f, so.attackRange, "omitted field must keep its existing SO value");
            Assert.AreEqual(1, so.cost, "omitted field must keep its existing SO value");
            Assert.AreEqual(1, applied, "only the single non-null field should count as applied");
            Assert.AreEqual("archer", so.id, "id must never be overwritten by the generic field copy");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ApplyNonNullFields_EnumField_OverwritesWhenProvided()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.role = DefenderClass.None;

            var dto = new DefenderStatDto { id = "archer", role = DefenderClass.Guardian };
            UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual(DefenderClass.Guardian, so.role);

            Object.DestroyImmediate(so);
        }

        // ── unit-stat-projection Unit 3 ──────────────────────────────────────────

        [Test]
        public void Deserialize_AtkAndHeal_Populated()
        {
            const string json = @"{ ""defenders"": [
                { ""id"": ""healer"", ""atk"": 12, ""heal"": 20 }
            ], ""enemies"": [] }";

            var dto = JsonConvert.DeserializeObject<UnitStatImportPayload>(json).defenders[0];

            Assert.AreEqual(12f, dto.atk);
            Assert.AreEqual(20f, dto.heal);
        }

        [Test]
        public void ApplyNonNullFields_SkipsProjectedAndDeprecatedFields()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.attackDamage = 25f; // legacy field still exists until unit 4

            // atk/heal/attackDamage must NOT be reflection-copied (no SO field / shim / projected).
            var dto = new DefenderStatDto { id = "archer", atk = 99f, heal = 99f, attackDamage = 99f };
            int applied = UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual(0, applied, "projected/deprecated fields must not count as reflection-applied");
            Assert.AreEqual(25f, so.attackDamage, "deprecated attackDamage must never be written by the mapper");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ApplyNonNullFields_AggroAttackDamage_StaysReflectionMapped()
        {
            var so = ScriptableObject.CreateInstance<AttackUnitData>();
            so.aggroAttackDamage = 0f;

            // Live field — must be reflection-mapped, NOT swept into the skip-list.
            var dto = new EnemyStatDto { id = "runner", aggroAttackDamage = 7f };
            UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual(7f, so.aggroAttackDamage);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ProjectMagnitude_UniqueDamage_UpdatesMagnitude()
        {
            var outputs = new[]
            {
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 15f },
                new AttackOutput { kind = AttackOutputKind.ApplyStack, magnitude = 1f },
            };
            var log = new System.Text.StringBuilder();
            int projected = 0, skipped = 0;

            UnitStatImportWindow.ProjectMagnitude(outputs, AttackOutputKind.Damage, 30f, "atk", "defender 'x'", log, ref projected, ref skipped);

            Assert.AreEqual(30f, outputs[0].magnitude);
            Assert.AreEqual(1, projected);
            Assert.AreEqual(0, skipped);
        }

        [Test]
        public void ProjectMagnitude_NoDamageOutput_SkipsWithReason()
        {
            var outputs = new[] { new AttackOutput { kind = AttackOutputKind.ApplyStack, magnitude = 1f } };
            var log = new System.Text.StringBuilder();
            int projected = 0, skipped = 0;

            UnitStatImportWindow.ProjectMagnitude(outputs, AttackOutputKind.Damage, 30f, "atk", "defender 'poisoncaster'", log, ref projected, ref skipped);

            Assert.AreEqual(0, projected);
            Assert.AreEqual(1, skipped);
            StringAssert.Contains("no Damage output", log.ToString());
        }

        [Test]
        public void ProjectMagnitude_TwoDamageOutputs_SkipsWithReason()
        {
            var outputs = new[]
            {
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f },
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f },
            };
            var log = new System.Text.StringBuilder();
            int projected = 0, skipped = 0;

            UnitStatImportWindow.ProjectMagnitude(outputs, AttackOutputKind.Damage, 30f, "atk", "defender 'x'", log, ref projected, ref skipped);

            Assert.AreEqual(0, projected);
            Assert.AreEqual(1, skipped);
            Assert.AreEqual(10f, outputs[0].magnitude, "ambiguous target must not be mutated");
            StringAssert.Contains("2 Damage outputs", log.ToString());
        }

        [Test]
        public void ProjectMagnitude_Heal_UpdatesHealMagnitude()
        {
            var outputs = new[] { new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 15f } };
            var log = new System.Text.StringBuilder();
            int projected = 0, skipped = 0;

            UnitStatImportWindow.ProjectMagnitude(outputs, AttackOutputKind.Heal, 25f, "heal", "defender 'healer'", log, ref projected, ref skipped);

            Assert.AreEqual(25f, outputs[0].magnitude);
            Assert.AreEqual(1, projected);
        }

        [Test]
        public void ProjectMagnitude_NullValue_NoOpKeepsMagnitude()
        {
            var outputs = new[] { new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 15f } };
            var log = new System.Text.StringBuilder();
            int projected = 0, skipped = 0;

            UnitStatImportWindow.ProjectMagnitude(outputs, AttackOutputKind.Damage, null, "atk", "defender 'x'", log, ref projected, ref skipped);

            Assert.AreEqual(15f, outputs[0].magnitude, "omitted atk must keep existing magnitude");
            Assert.AreEqual(0, projected);
            Assert.AreEqual(0, skipped);
        }

        [Test]
        public void WarnDeprecatedAttackDamage_WhenPresent_LogsWarning()
        {
            var log = new System.Text.StringBuilder();

            UnitStatImportWindow.WarnDeprecatedAttackDamage(25f, "defender 'archer'", log);

            StringAssert.Contains("attackDamage", log.ToString());
            StringAssert.Contains("NOT applied", log.ToString());
        }

        [Test]
        public void WarnDeprecatedAttackDamage_WhenAbsent_NoLog()
        {
            var log = new System.Text.StringBuilder();

            UnitStatImportWindow.WarnDeprecatedAttackDamage(null, "defender 'archer'", log);

            Assert.AreEqual(0, log.Length);
        }
    }
}
