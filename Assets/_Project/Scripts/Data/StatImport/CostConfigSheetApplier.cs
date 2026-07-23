using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Wassup.Data.StatImport
{
    // sheet-export-push unit 7 — CostConfig 탭 apply 코어. DcSheetApplier 의 flat 탭
    // (cards/skills/configs)과 같은 id-match 부분갱신 규칙이다: 빈 셀(null)은 기존 값
    // 유지, 중복 id 행은 스킵, 미매칭 id 는 로그만. 에디터 import(AssetDatabase 인덱스 +
    // 디스크 저장)와 런타임 refresher(in-memory)가 이 코어를 공유한다.
    public static class CostConfigSheetApplier
    {
        public static string Apply(CostConfigDto[] rows, Dictionary<string, CostConfig> byId,
            Action<ScriptableObject> onApplied, StringBuilder log)
        {
            log ??= new StringBuilder();
            if (rows == null) return log.ToString();

            int matched = 0, unmatched = 0, skipped = 0, fieldsApplied = 0;
            var seen = new HashSet<string>();
            foreach (var dto in rows)
            {
                string id = dto?.id;
                if (!seen.Add(id ?? ""))
                {
                    skipped++;
                    log.AppendLine($"[cost-config] duplicate row for id='{id}' — skipped.");
                    continue;
                }
                if (string.IsNullOrEmpty(id) || byId == null || !byId.TryGetValue(id, out var so) || so == null)
                {
                    unmatched++;
                    log.AppendLine($"[cost-config] no match for id='{id}'");
                    continue;
                }
                fieldsApplied += UnitStatFieldMapper.ApplyNonNullFields(dto, so);
                onApplied?.Invoke(so);
                matched++;
            }

            log.Insert(0, $"[cost-config] matched {matched}, unmatched {unmatched}, fields applied {fieldsApplied}, skipped {skipped}.\n");
            return log.ToString();
        }
    }
}
