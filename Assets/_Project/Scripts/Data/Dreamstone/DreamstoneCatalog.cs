using System.Collections.Generic;
using UnityEngine;

namespace Wassup.Data
{
    [CreateAssetMenu(fileName = "DreamstoneCatalog", menuName = "Wassup/DreamstoneCatalog", order = 24)]
    public class DreamstoneCatalog : ScriptableObject
    {
        public DreamstoneData[] stones;

        public DreamstoneData ById(string id)
        {
            if (string.IsNullOrEmpty(id) || stones == null) return null;
            for (int i = 0; i < stones.Length; i++)
            {
                if (stones[i] != null && stones[i].id == id) return stones[i];
            }
            return null;
        }

        public IEnumerable<string> AllIds()
        {
            if (stones == null) yield break;
            for (int i = 0; i < stones.Length; i++)
            {
                if (stones[i] != null && !string.IsNullOrEmpty(stones[i].id))
                    yield return stones[i].id;
            }
        }
    }
}
