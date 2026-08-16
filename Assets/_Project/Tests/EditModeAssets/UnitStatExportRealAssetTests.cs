using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data.StatImport;
using Wassup.Editor.UnitStatImport;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // test-suite-fast-lane unit 0 — UnitStatImportTests 에서 추출한 실에셋 통합 검증.
    // 임포터 기구 테스트(인라인 JSON 픽스처)는 코어 lane 에 남는다.
    public class UnitStatExportRealAssetTests
    {
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
    }
}
