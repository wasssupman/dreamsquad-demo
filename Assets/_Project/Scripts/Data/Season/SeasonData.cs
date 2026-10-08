using UnityEngine;
using Somnia.Battle.Data;

namespace Somnia.Battle.Data.Season
{
    [CreateAssetMenu(menuName = "Somnia/Battle/Season/SeasonData", fileName = "season")]
    public sealed class SeasonData : ScriptableObject
    {
        public string seasonId = "S1_Forest";
        public string displayName = "Verdant Bloom";
        public MapThemeData mapTheme;
        // gimmick-match-integration unit 1 — 기믹은 시즌에서 분리되어 BattleConfig.gimmickPool 로
        // 이관됨(지금은 `MatchModeData.gimmickPool` 에서 `MatchDefinitionBuilder` 가 배정). 시즌은 맵 테마 전담.
    }
}
