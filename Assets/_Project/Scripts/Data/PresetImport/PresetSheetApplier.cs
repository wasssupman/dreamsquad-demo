using System;
using System.Collections.Generic;
using System.Text;

namespace Wassup.Data.PresetImport
{
    // preset-sheet-import unit 1 — 시트 행 → SquadPresetCollection 재구성 순수 코어.
    // 해석기(id→SO)를 Func 로 받아 에디터(AssetDatabase 인덱스)·런타임(카탈로그 ById) 공용.
    // 아키텍처 무의존 → EditMode 단위 테스트 대상.
    //
    // 시트 = 리스트 전체 SoT: 행들로 collection.presets 를 통째 재구성한다. 단, rows 가
    // null/빈(=fetch 실패·빈 응답)이면 no-op — 기존 리스트를 날리지 않는다(원자성).
    public static class PresetSheetApplier
    {
        public static bool Apply(
            IReadOnlyList<PresetDto> rows,
            Func<string, DefenderUnitData> resolveUnit,
            Func<string, DreamcatcherCard> resolveCard,
            int maxUnits,
            SquadPresetCollection collection,
            StringBuilder log)
        {
            if (collection == null) return false;
            if (rows == null || rows.Count == 0)
            {
                log?.AppendLine("[preset] 0 rows — 기존 프리셋 유지(no-op).");
                return false;
            }

            int unitMatched = 0, unitUnmatched = 0, unitOverflow = 0, cardMatched = 0, cardUnmatched = 0;
            var built = new List<SquadPreset>(rows.Count);

            foreach (var dto in rows)
            {
                // 유닛: csv 순서 = 슬롯 순서. 미해결은 null 슬롯(순서 보존), maxUnits 초과분은 drop.
                var units = new List<DefenderUnitData>();
                foreach (var token in Split(dto?.squad))
                {
                    if (units.Count >= maxUnits) { unitOverflow++; continue; }
                    var u = resolveUnit?.Invoke(token);
                    if (u == null) unitUnmatched++; else unitMatched++;
                    units.Add(u);
                }

                // 카드: 슬롯 없는 리스트. 미해결은 스킵(홀 없음).
                var cards = new List<DreamcatcherCard>();
                foreach (var token in Split(dto?.dreamcatcher))
                {
                    var c = resolveCard?.Invoke(token);
                    if (c == null) { cardUnmatched++; continue; }
                    cardMatched++;
                    cards.Add(c);
                }

                built.Add(new SquadPreset
                {
                    presetName = dto?.presetName,
                    units = units.ToArray(),
                    cards = cards.ToArray(),
                });
            }

            collection.presets = built;
            log?.AppendLine($"[preset] rows {rows.Count} → presets {built.Count}. " +
                $"units matched {unitMatched}/unmatched {unitUnmatched}/overflow {unitOverflow}, " +
                $"cards matched {cardMatched}/unmatched {cardUnmatched}.");
            return true;
        }

        // "," split → trim → 빈 항목 제거.
        private static List<string> Split(string csv)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(csv)) return result;
            foreach (var part in csv.Split(','))
            {
                var t = part.Trim();
                if (t.Length > 0) result.Add(t);
            }
            return result;
        }
    }
}
