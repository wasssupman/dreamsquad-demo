using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;
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
            so.health = 50f;

            // atk/heal are projected (not reflection fields); attackDamage is a removed
            // shim column. None may be reflection-copied, and none matches an SO field,
            // so nothing is applied and no "no field" warning path corrupts real stats.
            var dto = new DefenderStatDto { id = "archer", atk = 99f, heal = 99f, attackDamage = 99f };
            int applied = UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual(0, applied, "projected/deprecated fields must not count as reflection-applied");
            Assert.AreEqual(50f, so.health, "unrelated fields untouched");

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

            UnitStatApplier.ProjectMagnitude(outputs, AttackOutputKind.Damage, 30f, "atk", "defender 'x'", log, ref projected, ref skipped);

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

            UnitStatApplier.ProjectMagnitude(outputs, AttackOutputKind.Damage, 30f, "atk", "defender 'poisoncaster'", log, ref projected, ref skipped);

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

            UnitStatApplier.ProjectMagnitude(outputs, AttackOutputKind.Damage, 30f, "atk", "defender 'x'", log, ref projected, ref skipped);

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

            UnitStatApplier.ProjectMagnitude(outputs, AttackOutputKind.Heal, 25f, "heal", "defender 'healer'", log, ref projected, ref skipped);

            Assert.AreEqual(25f, outputs[0].magnitude);
            Assert.AreEqual(1, projected);
        }

        [Test]
        public void ProjectMagnitude_NullValue_NoOpKeepsMagnitude()
        {
            var outputs = new[] { new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 15f } };
            var log = new System.Text.StringBuilder();
            int projected = 0, skipped = 0;

            UnitStatApplier.ProjectMagnitude(outputs, AttackOutputKind.Damage, null, "atk", "defender 'x'", log, ref projected, ref skipped);

            Assert.AreEqual(15f, outputs[0].magnitude, "omitted atk must keep existing magnitude");
            Assert.AreEqual(0, projected);
            Assert.AreEqual(0, skipped);
        }

        [Test]
        public void WarnDeprecatedAttackDamage_WhenPresent_LogsWarning()
        {
            var log = new System.Text.StringBuilder();

            UnitStatApplier.WarnDeprecatedAttackDamage(25f, "defender 'archer'", log);

            StringAssert.Contains("attackDamage", log.ToString());
            StringAssert.Contains("NOT applied", log.ToString());
        }

        [Test]
        public void WarnDeprecatedAttackDamage_WhenAbsent_NoLog()
        {
            var log = new System.Text.StringBuilder();

            UnitStatApplier.WarnDeprecatedAttackDamage(null, "defender 'archer'", log);

            Assert.AreEqual(0, log.Length);
        }

        // ── unit 4: API envelope adaptation ──────────────────────────────────────

        [Test]
        public void ParseSheetRows_SuccessEnvelope_BindsRowsAndCoercesNumericStrings()
        {
            const string body = @"{ ""success"": true, ""data"": [
                { ""id"": ""archer"", ""health"": ""500"", ""role"": ""RANGER"" }
            ] }";

            var rows = SheetEnvelopeParser.ParseSheetRows<DefenderStatDto>(body, out string error);

            Assert.IsNull(error);
            Assert.AreEqual(1, rows.Length);
            Assert.AreEqual("archer", rows[0].id);
            Assert.AreEqual(500f, rows[0].health, "numeric cells may arrive as strings");
            Assert.AreEqual(DefenderClass.Ranger, rows[0].role, "uppercase sheet enum names accepted");
        }

        [Test]
        public void ParseSheetRows_EmptyStringCell_TreatedAsOmitted()
        {
            const string body = @"{ ""success"": true, ""data"": [
                { ""id"": ""archer"", ""health"": """", ""cost"": ""  "" }
            ] }";

            var rows = SheetEnvelopeParser.ParseSheetRows<DefenderStatDto>(body, out string error);

            Assert.IsNull(error);
            Assert.IsNull(rows[0].health, "blank cell must behave like an omitted key (keep existing)");
            Assert.IsNull(rows[0].cost, "whitespace-only cell must behave like an omitted key");
        }

        [Test]
        public void ParseSheetRows_SuccessFalse_ReportsErrorDetail()
        {
            const string body = @"{ ""success"": false, ""errorDetail"": {
                ""errorCode"": ""INTERNAL_SERVER_ERROR"", ""code"": ""C004"",
                ""errorMessage"": ""서버 내부 오류가 발생했습니다."", ""detailMessage"": ""구글 시트 연동 실패"" } }";

            var rows = SheetEnvelopeParser.ParseSheetRows<DefenderStatDto>(body, out string error);

            Assert.IsNull(rows);
            StringAssert.Contains("INTERNAL_SERVER_ERROR", error);
            StringAssert.Contains("구글 시트 연동 실패", error);
        }

        [Test]
        public void ParseSheetRows_MalformedBody_ReturnsParseError()
        {
            var rows = SheetEnvelopeParser.ParseSheetRows<DefenderStatDto>("<html>oops</html>", out string error);

            Assert.IsNull(rows);
            StringAssert.Contains("JSON parse failed", error);
        }

        [Test]
        public void ParseSheetRows_EmptyBody_ReturnsError()
        {
            var rows = SheetEnvelopeParser.ParseSheetRows<DefenderStatDto>(null, out string error);

            Assert.IsNull(rows);
            StringAssert.Contains("empty response body", error);
        }

        [Test]
        public void Deserialize_TargetClassMaskCommaString_CombinesFlags()
        {
            var dto = JsonConvert.DeserializeObject<EnemyStatDto>(
                @"{ ""id"": ""basic"", ""targetClassMask"": ""Ranger, guardian"" }");

            Assert.AreEqual(DefenderClassFlags.Ranger | DefenderClassFlags.Guardian, dto.targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskStringEverything_ParsesAsEverything()
        {
            var dto = JsonConvert.DeserializeObject<EnemyStatDto>(
                @"{ ""id"": ""basic"", ""targetClassMask"": ""Everything"" }");

            Assert.AreEqual(DefenderClassFlags.Everything, dto.targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskStringNone_ParsesAsNone()
        {
            var dto = JsonConvert.DeserializeObject<EnemyStatDto>(
                @"{ ""id"": ""basic"", ""targetClassMask"": ""None"" }");

            Assert.AreEqual(DefenderClassFlags.None, dto.targetClassMask);
        }

        [Test]
        public void Deserialize_TargetClassMaskBlankString_ParsesAsNullKeepExisting()
        {
            var dto = JsonConvert.DeserializeObject<EnemyStatDto>(
                @"{ ""id"": ""basic"", ""targetClassMask"": ""  "" }");

            Assert.IsNull(dto.targetClassMask, "blank mask cell must mean keep-existing, not None");
        }

        [Test]
        public void Deserialize_TargetClassMaskStringSentinelMixed_Throws()
        {
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<EnemyStatDto>(
                @"{ ""id"": ""basic"", ""targetClassMask"": ""Everything,Ranger"" }"));
            Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<EnemyStatDto>(
                @"{ ""id"": ""basic"", ""targetClassMask"": ""None,Ranger"" }"));
        }

        [Test]
        public void BuildSheetUrl_TrimsSlashAndEscapesSheetName()
        {
            Assert.AreEqual(
                "https://x.example/api/sheet/My%20Sheet",
                SheetEnvelopeParser.BuildSheetUrl("https://x.example/api/sheet/ ", " My Sheet "));
        }

        // ── unit 5: SO → JSON export ─────────────────────────────────────────────

        [Test]
        public void ReadFieldsToDto_CopiesSubsetFieldsIncludingId()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.id = "archer";
            so.role = DefenderClass.Ranger;
            so.health = 500f;
            so.cost = 2;

            var dto = new DefenderStatDto();
            UnitStatFieldMapper.ReadFieldsToDto(so, dto);

            Assert.AreEqual("archer", dto.id, "id is the export row key and must be read");
            Assert.AreEqual(DefenderClass.Ranger, dto.role);
            Assert.AreEqual(500f, dto.health);
            Assert.AreEqual(2, dto.cost);
            Assert.IsNull(dto.attackDamage, "deprecated shim must never be exported");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ExporterToDto_UniqueOutputs_ReverseProjectAtkAndHeal()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.id = "healer";
            so.outputs = new[]
            {
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 12f },
                new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 20f },
            };

            var dto = UnitStatExporter.ToDto(so);

            Assert.AreEqual(12f, dto.atk);
            Assert.AreEqual(20f, dto.heal);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ExporterToDto_AmbiguousOrMissingOutputs_LeaveScalarNull()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.id = "caster";
            so.outputs = new[]
            {
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f },
                new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 5f },
            };

            var dto = UnitStatExporter.ToDto(so);

            Assert.IsNull(dto.atk, "2+ Damage outputs are ambiguous — cell must stay blank");
            Assert.IsNull(dto.heal, "no Heal output — cell must stay blank");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ToRowsJson_OmitsNullFields()
        {
            var rows = new[] { new DefenderStatDto { id = "caster" } };

            string json = UnitStatExporter.ToRowsJson(rows);

            StringAssert.Contains("\"id\": \"caster\"", json);
            StringAssert.DoesNotContain("atk", json);
            StringAssert.DoesNotContain("attackDamage", json);
        }

        [Test]
        public void ToRowsJson_WritesEnumsAsMemberNames()
        {
            var rows = new[] { new DefenderStatDto { id = "archer", role = DefenderClass.Ranger, rarity = DefenderRarity.Ego } };

            string json = UnitStatExporter.ToRowsJson(rows);

            StringAssert.Contains("\"role\": \"Ranger\"", json, "contract: enums are member-name strings, not ordinals");
            StringAssert.Contains("\"rarity\": \"Ego\"", json);
        }

        [Test]
        public void Serialize_TargetClassMask_WritesSheetCellScalar()
        {
            string partial = JsonConvert.SerializeObject(
                new EnemyStatDto { id = "x", targetClassMask = DefenderClassFlags.Ranger | DefenderClassFlags.Guardian });
            string everything = JsonConvert.SerializeObject(
                new EnemyStatDto { id = "x", targetClassMask = DefenderClassFlags.Everything });
            string none = JsonConvert.SerializeObject(
                new EnemyStatDto { id = "x", targetClassMask = DefenderClassFlags.None });

            StringAssert.Contains("\"targetClassMask\":\"Ranger,Guardian\"", partial);
            StringAssert.Contains("\"targetClassMask\":\"Everything\"", everything);
            StringAssert.Contains("\"targetClassMask\":\"None\"", none);
        }

        // Integration: exports the real Defender/Enemy assets into the project Temp
        // folder (gitignored). Asserts structure only — no value pinning, so balance
        // edits never break this test. File ↔ asset count equality catches scan bugs.
        [Test]
        public void ExportToFolder_RealAssets_WritesParseableRowFiles()
        {
            string folder = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(Application.dataPath, "../Temp/StatExportTest"));
            System.IO.Directory.CreateDirectory(folder);

            UnitStatExporter.ExportToFolder(folder, "Defenders", "Enemies",
                "Assets/_Project/Data/Defenders", "Assets/_Project/Data/Enemies");

            var defenders = JsonConvert.DeserializeObject<DefenderStatDto[]>(
                System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Defenders.json")));
            var enemies = JsonConvert.DeserializeObject<EnemyStatDto[]>(
                System.IO.File.ReadAllText(System.IO.Path.Combine(folder, "Enemies.json")));

            int defenderAssets = UnityEditor.AssetDatabase.FindAssets(
                "t:DefenderUnitData", new[] { "Assets/_Project/Data/Defenders" }).Length;
            int enemyAssets = UnityEditor.AssetDatabase.FindAssets(
                "t:AttackUnitData", new[] { "Assets/_Project/Data/Enemies" }).Length;

            Assert.AreEqual(defenderAssets, defenders.Length, "every defender asset must export one row");
            Assert.AreEqual(enemyAssets, enemies.Length, "every enemy asset must export one row");
            foreach (var row in defenders) Assert.IsNotEmpty(row.id, "exported defender row must carry its id");
            foreach (var row in enemies) Assert.IsNotEmpty(row.id, "exported enemy row must carry its id");
        }

        [Test]
        public void ExportImport_Roundtrip_PreservesValues()
        {
            var source = ScriptableObject.CreateInstance<AttackUnitData>();
            source.id = "basic";
            source.enemyClass = EnemyClass.Bruiser;
            source.engageMovement = EngageMovement.Pulse;
            source.targetClassMask = DefenderClassFlags.Ranger | DefenderClassFlags.Caster;
            source.health = 60f;
            source.moveSpeed = 2.5f;
            source.outputs = new[] { new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 10f } };

            string json = UnitStatExporter.ToRowsJson(new[] { UnitStatExporter.ToDto(source) });
            var parsed = JsonConvert.DeserializeObject<EnemyStatDto[]>(json)[0];

            var target = ScriptableObject.CreateInstance<AttackUnitData>();
            target.id = "basic";
            target.outputs = new[] { new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 0f } };
            UnitStatFieldMapper.ApplyNonNullFields(parsed, target);
            AttackOutputStats.TrySetUniqueMagnitude(target.outputs, AttackOutputKind.Damage, parsed.atk.Value);

            Assert.AreEqual(EnemyClass.Bruiser, target.enemyClass);
            Assert.AreEqual(EngageMovement.Pulse, target.engageMovement);
            Assert.AreEqual(DefenderClassFlags.Ranger | DefenderClassFlags.Caster, target.targetClassMask);
            Assert.AreEqual(60f, target.health);
            Assert.AreEqual(2.5f, target.moveSpeed);
            Assert.AreEqual(10f, target.outputs[0].magnitude);

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }

        // ── squad-character-page unit 7: desc sheet round-trip ───────────────────

        [Test]
        public void ApplyNonNullFields_Desc_OverwritesWhenProvided()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.desc = "old";

            var dto = new DefenderStatDto { id = "archer", desc = "새 설명" };
            UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual("새 설명", so.desc);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ApplyNonNullFields_DescOmitted_KeepsExisting()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.desc = "기존 설명";

            var dto = new DefenderStatDto { id = "archer", health = 10f }; // desc omitted (null)
            UnitStatFieldMapper.ApplyNonNullFields(dto, so);

            Assert.AreEqual("기존 설명", so.desc, "omitted desc cell must keep the existing SO value");

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ReadFieldsToDto_ReadsDesc()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            so.id = "archer";
            so.desc = "레인저 · 원거리형.";

            var dto = new DefenderStatDto();
            UnitStatFieldMapper.ReadFieldsToDto(so, dto);

            Assert.AreEqual("레인저 · 원거리형.", dto.desc);

            Object.DestroyImmediate(so);
        }

        [Test]
        public void ExportImport_Desc_Roundtrip()
        {
            var source = ScriptableObject.CreateInstance<DefenderUnitData>();
            source.id = "archer";
            source.desc = "직접 쓴 설명 · 특수";

            string json = UnitStatExporter.ToRowsJson(new[] { UnitStatExporter.ToDto(source) });
            var parsed = JsonConvert.DeserializeObject<DefenderStatDto[]>(json)[0];

            var target = ScriptableObject.CreateInstance<DefenderUnitData>();
            target.id = "archer";
            UnitStatFieldMapper.ApplyNonNullFields(parsed, target);

            Assert.AreEqual("직접 쓴 설명 · 특수", target.desc);

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(target);
        }
    }
}
