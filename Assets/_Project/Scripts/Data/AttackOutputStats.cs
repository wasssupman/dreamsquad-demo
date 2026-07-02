namespace Wassup.Data
{
    // unit-stat-projection Unit 1 — single implementation of the "unique output
    // of a kind" invariant shared by the draft-card stat display and the
    // spreadsheet importer projection. A kind with 0 or 2+ entries is ambiguous:
    // both callers must refuse rather than guess (see spec 0_projection_contract).
    public static class AttackOutputStats
    {
        public static bool TryGetUniqueMagnitude(AttackOutput[] outputs, AttackOutputKind kind, out float magnitude)
        {
            magnitude = 0f;
            int index = FindUniqueIndex(outputs, kind);
            if (index < 0) return false;
            magnitude = outputs[index].magnitude;
            return true;
        }

        public static bool TrySetUniqueMagnitude(AttackOutput[] outputs, AttackOutputKind kind, float value)
        {
            int index = FindUniqueIndex(outputs, kind);
            if (index < 0) return false;
            outputs[index].magnitude = value;
            return true;
        }

        private static int FindUniqueIndex(AttackOutput[] outputs, AttackOutputKind kind)
        {
            if (outputs == null) return -1;
            int found = -1;
            for (int i = 0; i < outputs.Length; i++)
            {
                if (outputs[i].kind != kind) continue;
                if (found >= 0) return -1; // 2+ entries — ambiguous
                found = i;
            }
            return found;
        }
    }
}
