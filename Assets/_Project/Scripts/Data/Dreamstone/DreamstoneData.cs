using UnityEngine;

namespace Wassup.Data
{
    public enum DreamstoneGrade
    {
        Common,
        Rare,
        Epic,
        Unique
    }

    [CreateAssetMenu(fileName = "DreamstoneData", menuName = "Wassup/Dreamstone", order = 23)]
    public class DreamstoneData : ScriptableObject
    {
        public string id;
        public string displayName;
        public DreamstoneGrade grade;
        public CardEffect effect;
    }
}
