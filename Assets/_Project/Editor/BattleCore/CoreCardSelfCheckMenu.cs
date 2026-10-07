#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.EditorTools.BattleCore
{
    // battle-core-rebuild unit 7e ③ — **카드 자가진단.** 플레이 중 사람이 ×만 보게 한다.
    //
    // 두 표를 찍는다:
    //   ⑴ 카탈로그 전량 — 살아 있는 드라이버의 저작(모드 · 덱 · 스테이지 · 스택 · 부여 상한 · 이동 튜닝)에 카탈로그 카드 전부,
    //      숙주 후보는 방어유닛 카탈로그 전부. `CardEffectWitnessTests` 와 같은 입력이다(그 테스트를 Play 에서 다시 돌리는 것).
    //   ⑵ 이 판의 덱 — 살아 있는 `BattleDriver.Definition` 그대로.
    //
    // ⚠ **사용자 판을 건드리지 않는다.** `CardProbe` 는 정의표를 **복사해** 별도 `BattleMatch` 를 세운다. 그래서 이 메뉴는
    //    `driver.Match` 에 커맨드를 한 번도 넣지 않는다 — 실행 전후 틱 · 개체 수가 같은지 스스로 확인하고 다르면 에러로 말한다.
    // ⚠ 판정은 코어의 것(`CardProbe` · `EffectWitness` · `BindingDiagnosis`)이다. 이 메뉴는 찍기만 한다.
    public static class CoreCardSelfCheckMenu
    {
        private const string MenuPath = "Wassup/BattleCore/Debug/카드 자가진단";
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string CatalogPath = "Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset";
        private const string DefenderCatalogPath = "Assets/_Project/Data/DefenderCatalog.asset";

        [MenuItem(MenuPath)]
        private static void Run()
        {
            if (!CoreObstacleDebugMenu.TryGetDriver(out var driver)) return;
            var match = driver.Match;
            int tick = match.Clock.Tick;
            int units = match.World.Units.Count;
            string hash = driver.Definition.ComputeConfigHash();

            var catalogDef = BuildCatalogDefinition(driver);
            var catalog = catalogDef != null ? CardProbe.RunAll(catalogDef) : System.Array.Empty<CardProbeResult>();
            var deck = CardProbe.RunAll(driver.Definition);

            Debug.Log("[CoreCardSelfCheck] ⑴ 카탈로그 전량\n" + CardProbe.FormatTable(catalog)
                      + "\n[CoreCardSelfCheck] ⑵ 이 판의 덱\n" + CardProbe.FormatTable(deck));
            foreach (var r in catalog) if (!r.Ok) Debug.LogWarning("[CoreCardSelfCheck] × 카탈로그 " + r);
            foreach (var r in deck) if (!r.Ok) Debug.LogWarning("[CoreCardSelfCheck] × 이 판의 덱 " + r);

            if (!ReferenceEquals(match, driver.Match) || match.Clock.Tick != tick || match.World.Units.Count != units
                || driver.Definition.ComputeConfigHash() != hash)
                Debug.LogError("[CoreCardSelfCheck] 자가진단이 사용자 판을 움직였다 — 틱·개체 수·정의표 해시가 전후로 다르다(도구 결함).");
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate() => Application.isPlaying;

        // 드라이버의 저작을 **읽기만** 해서 카탈로그 정의표를 짓는다(드라이버의 판 · 필드는 안 바꾼다).
        private static MatchDefinition BuildCatalogDefinition(BattleDriver driver)
        {
            var so = new SerializedObject(driver);
            T Obj<T>(string field) where T : Object => so.FindProperty(field)?.objectReferenceValue as T;

            var mode = typeof(BattleDriver).GetField("_resolvedMode", BindingFlags.NonPublic | BindingFlags.Instance)
                           ?.GetValue(driver) as MatchModeData ?? Obj<MatchModeData>("_mode");
            var content = driver.Content;   // battle-content-finish unit 0 — 판 콘텐츠는 SO 한 장
            var defenders = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(DefenderCatalogPath);
            var cards = Cards();
            if (mode == null || content == null || defenders == null || cards == null)
            {
                Debug.LogError("[CoreCardSelfCheck] 모드 · BattleContent · 방어유닛 카탈로그 · 카드 카탈로그 중 하나가 없다 — 카탈로그 표를 건너뛴다.");
                return null;
            }
            var mapField = typeof(BattleDriver).GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance);
            var map = mapField != null ? (GeneratedMap)mapField.GetValue(driver) : default;
            var seed = so.FindProperty("_seed");
            return MatchDefinitionBuilder.Build(
                mode, defenders.units, Obj<AttackDeck>("_deck"), Obj<WavePlanAsset>("_plan"), content.bonus,
                seed != null ? seed.intValue : 1,
                costRateMultiplier: 1f, map: in map, tileSize: driver.TileSize,
                structures: driver.StageStructures, viewAssets: null,
                movement: content.movementTuning,
                stackModifiers: content.stackModifiers,
                imbueCaps: content.imbueCaps,
                board: new BoardEffectAuthoring
                {
                    Hazards = content.hazards,
                    ExtraBlockers = content.extraBlockers,
                },
                cards: cards, dreamstones: null);
        }

        // 카탈로그 순 + 카탈로그 밖은 경로 순 — `CardEffectWitnessTests.Cards` 와 같은 규칙.
        private static List<DreamcatcherCard> Cards()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CatalogPath);
            if (catalog == null) return null;
            var list = new List<DreamcatcherCard>();
            foreach (var c in catalog.cards) if (c != null && !list.Contains(c)) list.Add(c);
            var rest = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var c = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(path);
                if (c != null && !list.Contains(c)) rest.Add(path);
            }
            rest.Sort(System.StringComparer.Ordinal);
            foreach (var p in rest) list.Add(AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(p));
            return list;
        }
    }
}
#endif
