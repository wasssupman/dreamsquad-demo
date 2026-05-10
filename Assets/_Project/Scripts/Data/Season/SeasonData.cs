using UnityEngine;
using Wassup.Data;

namespace Wassup.Data.Season
{
    [CreateAssetMenu(menuName = "Wassup/Season/SeasonData", fileName = "season")]
    public sealed class SeasonData : ScriptableObject
    {
        public string seasonId = "S1_Forest";
        public string displayName = "Verdant Bloom";
        public MapThemeData mapTheme;
        public SeasonBackdropData backdrop;
    }
}
