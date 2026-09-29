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
    // DcSheetApplier. `_`-prefixed informational columns (asset-ref ids, structural enums) are filled here by
    // hand and ignored by the importer. Output rows match the seed JSON shape.
    // skill-data-table unit 8 단계 B — 카드 자식 탭 둘(DcCardEffects · DcAttackMods)은 은퇴 — 그 값은 `Skills` · `SkillOwners` 로 나간다.
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
        // active-dreamcatcher-tile-aim unit 0 — `_target` 정보 열은 대상축(SkillTargetType)
        // 폐기와 함께 제거. 모든 스킬이 타일 대상이라 열의 정보량이 0이다.
        private class SkillRow : DcSkillDto { public string _effect; }

        // tabNames order = `DcSheetTabs`(cards, DcSkills, config, Skills, SkillOwners).
        public static string ExportToFolder(string folder, string[] tabNames,
            string dcAssetFolder, string skillAssetFolder)
        {
            var log = new StringBuilder();

            var cards = UnitAssetScan.Enumerate<DreamcatcherCard>(dcAssetFolder)
                .OrderBy(so => so.id, System.StringComparer.Ordinal).ToList();
            var skills = UnitAssetScan.Enumerate<SkillData>(skillAssetFolder)
                .OrderBy(so => so.id, System.StringComparer.Ordinal).ToList();

            var cardRows = new List<CardRow>();
            foreach (var so in cards)
            {
                var row = new CardRow();
                UnitStatFieldMapper.ReadFieldsToDto(so, row);
                row._skillId = so.skill != null ? so.skill.id : null;
                // dreamcatcher-attach-requirement unit 2(+unit 7 rev) — 제한 없는 카드만
                // 2열 blank(null → 키 생략, NullValueHandling.Ignore). 현재 전 카드가 여기
                // 해당하므로 시트에 enum-zero 노이즈가 설정된 것처럼 보이지 않는다.
                //
                // 값 칸이 하나뿐이라 구 3필드 설계의 "잔존 companion 부활" 함정(review M2)이
                // 구조적으로 없다 — type 을 바꾸면 같은 칸의 값을 반드시 함께 보게 된다.
                if (so.attachType == DcAttachType.None)
                {
                    row.attachType = null;
                    row.attachValue = null;
                }
                cardRows.Add(row);
            }

            var skillRows = new List<SkillRow>();
            foreach (var so in skills)
            {
                var row = new SkillRow();
                UnitStatFieldMapper.ReadFieldsToDto(so, row);
                row._effect = so.effect.ToString();
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

            // skill-data-table unit 5 — 새 두 탭(효과 · 소유 줄). 카드 · 방어유닛 · 적이 같은 형식이라 소유자 종류는 열 하나다.
            var skillSheet = SkillSheet.Export(
                UnitAssetScan.Enumerate<EffectData>(UnitStatImportWindow.DataRoot),
                UnitAssetScan.Enumerate<DreamcatcherCard>(UnitStatImportWindow.DataRoot),
                UnitAssetScan.Enumerate<DefenderUnitData>(UnitStatImportWindow.DataRoot),
                UnitAssetScan.Enumerate<AttackUnitData>(UnitStatImportWindow.DataRoot));

            WriteTab(folder, tabNames[DcSheetTabs.CardsAt], cardRows, log);
            WriteTab(folder, tabNames[DcSheetTabs.ActiveSkillsAt], skillRows, log);
            WriteTab(folder, tabNames[DcSheetTabs.ConfigAt], configRows, log);
            WriteTab(folder, tabNames[DcSheetTabs.SkillsAt], skillSheet.skills, log);
            WriteTab(folder, tabNames[DcSheetTabs.SkillOwnersAt], skillSheet.owners, log);
            return log.ToString();
        }

        private static void WriteTab<T>(string folder, string tabName, IReadOnlyCollection<T> rows, StringBuilder log)
        {
            string path = Path.Combine(folder, $"{tabName.Trim()}.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(rows, Formatting.Indented, Settings),
                new UTF8Encoding(false));
            log.AppendLine($"Exported {rows.Count} rows → {path}");
        }

        // unit 8 — 한 파일로 시트 반영: DC 탭 전부(`DcSheetTabs`)를 탭명 키 단일 JSON 으로 합치고, 옆에
        // 시트 챗봇용 프롬프트(.md)를 함께 쓴다. 붙여넣기 1회로 전체 반영 가능.
        // 구현은 검증된 ExportToFolder 를 임시 폴더에 그대로 돌린 뒤 병합 — 수집
        // 로직 중복 없음. 반환 = 사람이 읽는 로그.
        public static string ExportCombinedFile(string outFilePath, string[] tabNames,
            string dcAssetFolder, string skillAssetFolder)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "wassup_dc_export_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string perTabLog = ExportToFolder(tempDir, tabNames, dcAssetFolder, skillAssetFolder);

                var root = new Newtonsoft.Json.Linq.JObject
                {
                    ["_note"] = "전 드림캐쳐 · 스킬 SO export 스냅샷. 각 탭명 키의 배열을 구글 시트 같은 이름 탭에 업서트(키=id · Skills=effect_id · SkillOwners=(owner_kind,owner_id,slot)). enum=C# 멤버명. SkillOwners=시트-SoT(행=배열항목), Skills=효과 id 별 값(kind_ko=보기 전용).",
                };
                foreach (var tab in tabNames)
                {
                    string p = Path.Combine(tempDir, $"{tab.Trim()}.json");
                    root[tab.Trim()] = Newtonsoft.Json.Linq.JArray.Parse(File.ReadAllText(p));
                }

                string json = root.ToString(Formatting.Indented);
                File.WriteAllText(outFilePath, json, new UTF8Encoding(false));

                string promptPath = Path.Combine(Path.GetDirectoryName(outFilePath), "dreamcatcher_sheet_prompt.md");
                File.WriteAllText(promptPath, BuildChatbotPrompt(json), new UTF8Encoding(false));

                return perTabLog
                    + $"\nCombined → {outFilePath}"
                    + $"\nPrompt   → {promptPath}";
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { /* 임시 폴더 정리 실패는 무해 */ }
            }
        }

        private static string BuildChatbotPrompt(string embeddedJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("너는 이 구글 스프레드시트를 편집하는 어시스턴트다. 아래 JSON 은 게임에서 export 한 드림캐쳐 마스터데이터 전량 스냅샷이다. 각 탭에 반영해라.");
            sb.AppendLine();
            sb.AppendLine("규칙:");
            sb.AppendLine("1. JSON top-level 키 = 시트 탭 이름(`_note` 제외). 각 배열을 같은 이름 탭에 반영, 없으면 생성.");
            sb.AppendLine("2. 배열 원소=행, 객체 키=열 헤더(1행 헤더, 2행부터 데이터). 특정 행에 없는 키는 셀 비움.");
            sb.AppendLine("3. 기존 헤더 순서 유지, JSON 에만 있는 새 열은 오른쪽에 추가.");
            sb.AppendLine("4. 업서트(중복 생성 금지): DcCards/DcSkills/DcConfig 키=id · Skills 키=effect_id · SkillOwners 키=(owner_kind,owner_id,slot). 같은 키 행은 갱신, 없으면 추가, slot 오름차순. JSON 에 없는 기존 행은 지우지 마라.");
            sb.AppendLine("5. 값 그대로: enum=문자열, 숫자=숫자, 한글 텍스트 원문 유지. 변형·번역·반올림 금지.");
            sb.AppendLine("6. 반영 후 탭별 추가/갱신 행 수를 요약.");
            sb.AppendLine();
            sb.AppendLine("JSON:");
            sb.AppendLine("```json");
            sb.AppendLine(embeddedJson);
            sb.AppendLine("```");
            return sb.ToString();
        }
    }
}
