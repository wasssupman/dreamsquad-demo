using System.Collections.Generic;

namespace Wassup.Core
{
    // squad-loadout Unit 1 (rev 2026-06-05) — pure, deterministic field resolution.
    // Brings the saved squad in AS-IS: the squad's non-empty, de-duplicated unit ids
    // in slot order, capped at FieldCount. No random fill / shuffle / seed — the
    // lineup is identical every match (user decision: saved squad must not change
    // between game starts). Random "variable" fill was removed here.
    // No Unity / catalog dependency — caller resolves ids to DefenderUnitData.
    public static class SquadDraw
    {
        public const int FieldCount = 7;

        public static List<string> Resolve(IReadOnlyList<string> squadUnitIds)
        {
            var result = new List<string>();
            var seen = new HashSet<string>();
            if (squadUnitIds != null)
            {
                foreach (var id in squadUnitIds)
                {
                    if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                    result.Add(id);
                    if (result.Count >= FieldCount) break;
                }
            }
            return result;
        }
    }
}
