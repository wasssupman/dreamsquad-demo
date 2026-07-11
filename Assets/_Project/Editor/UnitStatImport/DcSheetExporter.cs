using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Editor.UnitStatImport
{
    // dreamcatcher-sheet-sync unit 3 — SO → per-tab JSON rows, the reverse of
    // DcSheetApplier. Child arrays unroll into (cardId, slot) rows; `_`-prefixed
    // informational columns (asset-ref ids, structural enums) are filled here by
    // hand and ignored by the importer. Output rows match the seed JSON shape.
    public static class DcSheetExporter
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            NullValueHandling = NullValueHandling.Ignore,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        };

        // Export-only rows: the extra `_` fields must not exist on the import DTOs,
        // or the reflection mapper / row binding would have to special-case them.
        private class CardRow : DcCardDto { public string _skillId; }
        private class MechanicRow : DcMechanicDto { public string _projectileId; }
        private class SkillRow : DcSkillDto { public string _effect; public string _target; }

        // tabNames order: cards, cardEffects, mechanics, attackMods, skills, config.
        public static string ExportToFolder(string folder, string[] tabNames,
            string dcAssetFolder, string skillAssetFolder)
        {
            var log = new StringBuilder();

            var cards = UnitAssetScan.Enumerate<DreamcatcherCard>(dcAssetFolder)
                .OrderBy(so => so.id, System.StringComparer.Ordinal).ToList();
            var skills = UnitAssetScan.Enumerate<SkillData>(skillAssetFolder)
                .OrderBy(so => so.id, System.StringComparer.Ordinal).ToList();

            var cardRows = new List<CardRow>();
            var effectRows = new List<DcCardEffectDto>();
            var mechanicRows = new List<MechanicRow>();
            var attackModRows = new List<DcAttackModDto>();
            foreach (var so in cards)
            {
                var row = new CardRow();
                UnitStatFieldMapper.ReadFieldsToDto(so, row);
                row._skillId = so.skill != null ? so.skill.id : null;
                cardRows.Add(row);

                for (int i = 0; i < (so.effects?.Length ?? 0); i++)
                {
                    var e = so.effects[i];
                    effectRows.Add(new DcCardEffectDto
                    { cardId = so.id, slot = i, kind = e.kind, percent = e.percent });
                }
                for (int i = 0; i < (so.mechanics?.Length ?? 0); i++)
                {
                    var m = so.mechanics[i];
                    mechanicRows.Add(new MechanicRow
                    {
                        cardId = so.id, slot = i,
                        triggerKind = m.trigger.kind, triggerPeriod = m.trigger.period,
                        payloadKind = m.payload.kind, magnitude = m.payload.magnitude,
                        tileRange = m.payload.tileRange, duration = m.payload.duration,
                        _projectileId = m.payload.projectile != null ? m.payload.projectile.id : null,
                    });
                }
                for (int i = 0; i < (so.attackMods?.Length ?? 0); i++)
                {
                    var a = so.attackMods[i];
                    attackModRows.Add(new DcAttackModDto
                    {
                        cardId = so.id, slot = i, kind = a.kind,
                        count = a.count, tileRange = a.tileRange, damageMul = a.damageMul,
                    });
                }
            }

            var skillRows = new List<SkillRow>();
            foreach (var so in skills)
            {
                var row = new SkillRow();
                UnitStatFieldMapper.ReadFieldsToDto(so, row);
                row._effect = so.effect.ToString();
                row._target = so.target.ToString();
                skillRows.Add(row);
            }

            var configRows = new List<DcConfigDto>();
            foreach (var so in UnitAssetScan.Enumerate<AwakeningConfig>(dcAssetFolder))
            {
                configRows.Add(new DcConfigDto
                {
                    id = so.id, gaugeMax = so.gaugeMax, gaugeStart = so.gaugeStart,
                    costSquad = so.costSquad, costUnit = so.costUnit, costActive = so.costActive,
                    handSize = so.handSize, maxAttachPerUnit = so.maxAttachPerUnit,
                    slomoTimeScale = so.slomoTimeScale,
                });
            }
            foreach (var so in UnitAssetScan.Enumerate<DeckRuleConfig>(dcAssetFolder))
            {
                configRows.Add(new DcConfigDto
                { id = so.id, deckSize = so.deckSize, maxSquad = so.maxSquad, maxUnit = so.maxUnit });
            }
            configRows.Sort((a, b) => string.CompareOrdinal(a.id, b.id));

            WriteTab(folder, tabNames[0], cardRows, log);
            WriteTab(folder, tabNames[1], effectRows, log);
            WriteTab(folder, tabNames[2], mechanicRows, log);
            WriteTab(folder, tabNames[3], attackModRows, log);
            WriteTab(folder, tabNames[4], skillRows, log);
            WriteTab(folder, tabNames[5], configRows, log);
            return log.ToString();
        }

        private static void WriteTab<T>(string folder, string tabName, List<T> rows, StringBuilder log)
        {
            string path = Path.Combine(folder, $"{tabName.Trim()}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(rows, Formatting.Indented, Settings),
                new UTF8Encoding(false));
            log.AppendLine($"Exported {rows.Count} rows → {path}");
        }
    }
}
