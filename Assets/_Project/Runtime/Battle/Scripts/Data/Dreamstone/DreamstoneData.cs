using UnityEngine;

namespace Somnia.Battle.Data
{
    public enum DreamstoneGrade
    {
        Common,
        Rare,
        Epic,
        Unique
    }

    [CreateAssetMenu(fileName = "DreamstoneData", menuName = "Somnia/Battle/Dreamstone", order = 23)]
    public class DreamstoneData : ScriptableObject
    {
        public string id;
        public string displayName;
        public Sprite icon;
        public DreamstoneGrade grade;
        public CardEffect effect;
    }
}
