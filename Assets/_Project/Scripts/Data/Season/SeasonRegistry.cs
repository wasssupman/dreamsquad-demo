using System;
using UnityEngine;

namespace Wassup.Data.Season
{
    [CreateAssetMenu(menuName = "Wassup/Season/SeasonRegistry", fileName = "SeasonRegistry")]
    public sealed class SeasonRegistry : ScriptableObject
    {
        public SeasonData[] allSeasons = Array.Empty<SeasonData>();
        public SeasonData defaultSeason;

        public SeasonData activeSeason => defaultSeason;
    }
}
