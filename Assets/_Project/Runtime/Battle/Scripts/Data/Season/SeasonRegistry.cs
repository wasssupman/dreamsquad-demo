using System;
using UnityEngine;

namespace Somnia.Battle.Data.Season
{
    [CreateAssetMenu(menuName = "Somnia/Battle/Season/SeasonRegistry", fileName = "SeasonRegistry")]
    public sealed class SeasonRegistry : ScriptableObject
    {
        public SeasonData[] allSeasons = Array.Empty<SeasonData>();
        public SeasonData defaultSeason;

        public SeasonData activeSeason => defaultSeason;
    }
}
