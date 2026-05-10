namespace Wassup.Data.Season
{
    public static class SeasonRuntime
    {
        private static SeasonRegistry _registry;

        public static void Bind(SeasonRegistry registry) => _registry = registry;
        public static SeasonData Active => _registry != null ? _registry.activeSeason : null;
    }
}
