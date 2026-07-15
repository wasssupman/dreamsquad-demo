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
        // gimmick-match-integration unit 1 — 기믹은 시즌에서 분리되어 BattleConfig.gimmickPool 로
        // 이관됨(매치 시작 시 GameManager 가 배정). 시즌은 맵 테마 전담.
    }
}
