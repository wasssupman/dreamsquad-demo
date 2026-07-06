using UnityEngine;

namespace Wassup.Data
{
    // runtime-stat-refresh Unit 1 — id -> AttackUnitData resolution for the
    // runtime stat refresher. Mirrors DefenderCatalog: authoritative list of
    // enemy units referenced by id.
    [CreateAssetMenu(fileName = "EnemyCatalog", menuName = "Wassup/EnemyCatalog", order = 13)]
    public class EnemyCatalog : ScriptableObject
    {
        public AttackUnitData[] units;

        public AttackUnitData ById(string id)
        {
            if (string.IsNullOrEmpty(id) || units == null) return null;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].id == id) return units[i];
            }
            return null;
        }
    }
}
