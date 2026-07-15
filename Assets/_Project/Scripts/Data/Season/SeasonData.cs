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
        // season-gimmick-overwork unit 2 — 시즌 기믹 (null 허용 = 기믹 없음, 기존 플레이 무변화).
        public GimmickData gimmick;
    }
}
