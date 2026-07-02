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
    }
}
