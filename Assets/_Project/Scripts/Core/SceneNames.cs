namespace Wassup.Core
{
    // outgame-scene-and-flow Unit 3 — single source of truth for build scene
    // names, shared by the Outgame ↔ Battle transition call sites.
    public static class SceneNames
    {
        public const string Outgame = "OutgameScene";
        // battle-core-rebuild unit 8b — **로비 교대.** 로비의 「시작」이 새 전투 씬(순수 C# 전투 코어)을 연다.
        // 옛 `BattleScene` 과 그것을 경로로 열던 옛 PlayMode lane 은 unit 9 에서 지웠다(이력).
        public const string Battle = "BattleCoreScene";
    }
}
