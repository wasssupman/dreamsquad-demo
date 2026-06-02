using System.Collections.Generic;

namespace Wassup.Core
{
    // squad-loadout Unit 1 — pure, deterministic field resolution. Given the
    // squad's slots and the owned pool, produce up to 7 unit ids to bring into
    // battle: squad units + up to 3 random others, then a random 7 of those.
    // No Unity / catalog dependency — caller resolves ids to DefenderUnitData.
    public static class SquadDraw
    {
        public const int VariableCount = 3;
        public const int FieldCount = 7;

        public static List<string> Resolve(
            IReadOnlyList<string> squadUnitIds,
            IReadOnlyList<string> ownedUnitIds,
            int seed)
        {
            var rng = new System.Random(seed);

            // 1. squad units: non-empty, de-duplicated, order preserved.
            var squad = new List<string>();
            var squadSet = new HashSet<string>();
            if (squadUnitIds != null)
            {
                foreach (var id in squadUnitIds)
                    if (!string.IsNullOrEmpty(id) && squadSet.Add(id)) squad.Add(id);
            }

            // 2. variable pool = owned − squad (non-empty, de-duplicated).
            var varPool = new List<string>();
            var varSet = new HashSet<string>();
            if (ownedUnitIds != null)
            {
                foreach (var id in ownedUnitIds)
                    if (!string.IsNullOrEmpty(id) && !squadSet.Contains(id) && varSet.Add(id))
                        varPool.Add(id);
            }
            Shuffle(varPool, rng);
            int varTake = varPool.Count < VariableCount ? varPool.Count : VariableCount;

            // 3. candidates = squad + up to VariableCount randoms.
            var candidates = new List<string>(squad);
            for (int i = 0; i < varTake; i++) candidates.Add(varPool[i]);

            // 4. random FieldCount of candidates (or all when fewer).
            Shuffle(candidates, rng);
            int fieldTake = candidates.Count < FieldCount ? candidates.Count : FieldCount;
            var result = new List<string>(fieldTake);
            for (int i = 0; i < fieldTake; i++) result.Add(candidates[i]);
            return result;
        }

        static void Shuffle(List<string> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
