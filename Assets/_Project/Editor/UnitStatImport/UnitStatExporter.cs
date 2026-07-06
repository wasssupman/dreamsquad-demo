using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // unit-stat-spreadsheet-schema Unit 5 — SO → JSON export, the reverse of the
    // importer. Produces one row-array file per sheet tab so the output maps 1:1
    // onto the sheet (and onto the API's `data` field for the future POST body).
    public static class UnitStatExporter
    {
        // null fields drop out of the file entirely — an absent key is the export
        // twin of the "blank cell = keep existing" import rule. StringEnumConverter
        // keeps the contract's "enum = member name string" on the write side too
        // (Json.NET defaults to ordinals; targetClassMask keeps its own converter
        // via the field attribute, which takes precedence).
        private static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        internal static DefenderStatDto ToDto(DefenderUnitData so)
        {
            var dto = new DefenderStatDto();
            UnitStatFieldMapper.ReadFieldsToDto(so, dto);
            // reverse projection — only a unique output of the kind maps to the
            // planner scalar; 0 or 2+ leaves the cell blank (same rule as import).
            if (AttackOutputStats.TryGetUniqueMagnitude(so.outputs, AttackOutputKind.Damage, out float atk)) dto.atk = atk;
            if (AttackOutputStats.TryGetUniqueMagnitude(so.outputs, AttackOutputKind.Heal, out float heal)) dto.heal = heal;
            return dto;
        }

        internal static EnemyStatDto ToDto(AttackUnitData so)
        {
            var dto = new EnemyStatDto();
            UnitStatFieldMapper.ReadFieldsToDto(so, dto);
            if (AttackOutputStats.TryGetUniqueMagnitude(so.outputs, AttackOutputKind.Damage, out float atk)) dto.atk = atk;
            return dto;
        }

        internal static string ToRowsJson<T>(T[] rows)
            => JsonConvert.SerializeObject(rows, Formatting.Indented, Settings);

        // Writes {folder}/{sheetName}.json per tab. Row order is id-ascending so
        // repeated exports diff cleanly.
        public static string ExportToFolder(string folder, string defenderSheetName, string enemySheetName,
            string defenderAssetFolder, string enemyAssetFolder)
        {
            var log = new StringBuilder();

            var defenders = LoadAllSortedById<DefenderUnitData>(defenderAssetFolder);
            var enemies = LoadAllSortedById<AttackUnitData>(enemyAssetFolder);

            var defenderRows = new DefenderStatDto[defenders.Count];
            for (int i = 0; i < defenders.Count; i++) defenderRows[i] = ToDto(defenders[i]);
            var enemyRows = new EnemyStatDto[enemies.Count];
            for (int i = 0; i < enemies.Count; i++) enemyRows[i] = ToDto(enemies[i]);

            string defenderPath = Path.Combine(folder, $"{defenderSheetName.Trim()}.json");
            string enemyPath = Path.Combine(folder, $"{enemySheetName.Trim()}.json");
            File.WriteAllText(defenderPath, ToRowsJson(defenderRows), new UTF8Encoding(false));
            File.WriteAllText(enemyPath, ToRowsJson(enemyRows), new UTF8Encoding(false));

            log.AppendLine($"Exported {defenderRows.Length} defenders → {defenderPath}");
            log.AppendLine($"Exported {enemyRows.Length} enemies → {enemyPath}");
            return log.ToString();
        }

        private static List<T> LoadAllSortedById<T>(string folder) where T : ScriptableObject
        {
            var assets = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) assets.Add(asset);
            }
            assets.Sort((a, b) => string.CompareOrdinal(IdOf(a), IdOf(b)));
            return assets;
        }

        private static string IdOf(ScriptableObject so) =>
            so is DefenderUnitData d ? d.id : so is AttackUnitData e ? e.id : so.name;
    }
}
