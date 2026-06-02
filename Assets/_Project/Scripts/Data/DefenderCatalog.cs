using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    // outgame-scene-and-flow Unit 0 — id -> DefenderUnitData resolution for
    // save/load. Authoritative list of defender units a profile can reference.
    [CreateAssetMenu(fileName = "DefenderCatalog", menuName = "Wassup/DefenderCatalog", order = 12)]
    public class DefenderCatalog : ScriptableObject
    {
        public DefenderUnitData[] units;

        public DefenderUnitData ById(string id)
        {
            if (string.IsNullOrEmpty(id) || units == null) return null;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && units[i].id == id) return units[i];
            }
            return null;
        }

        public IEnumerable<string> AllIds()
        {
            if (units == null) yield break;
            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null && !string.IsNullOrEmpty(units[i].id))
                    yield return units[i].id;
            }
        }
    }
}
