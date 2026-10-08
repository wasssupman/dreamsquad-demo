#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.Data;

namespace Somnia.Battle.EditorTools.BattleCore
{
    // battle-core-rebuild unit 7e ② — **카드 굽기 스냅샷 갱신.** 의도한 카드 변경(시트 · SO)이면 이 메뉴로 다시 굽고
    // 파일 diff 를 같은 커밋에 싣는다. 테스트(`CardBakeSnapshotTests`)는 파일을 **읽기만** 한다.
    //
    // ⚠ 카드 목록 순서와 굽기는 테스트와 **같은 규칙**이다(카탈로그 순 + 카탈로그 밖은 경로 순 · 카드만 굽기 · 첫 `AwakeningConfig`).
    //    테스트 어셈블리를 에디터가 참조할 수 없어 두 곳에 있다 — 하나만 바꾸면 스냅샷 테스트가 첫 줄부터 빨개져 곧 드러난다.
    public static class CoreCardSnapshotMenu
    {
        private const string SnapshotPath = "Assets/_Project/Tests/EditModeAssets/Fixtures/card_bake_snapshot.txt";
        private const string Header = "# battle-core-rebuild 7e — 카드 굽기 스냅샷. 손으로 고치지 말 것: Somnia/Battle/BattleCore/Debug/카드 스냅샷 갱신\n";
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";
        private const string CatalogPath = "Assets/_Project/Data/Dreamcatcher/DreamcatcherCardCatalog.asset";

        [MenuItem("Somnia/Battle/BattleCore/Debug/카드 스냅샷 갱신")]
        private static void Update()
        {
            var cards = Cards();
            if (cards == null) return;
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            if (guids.Length == 0) { Debug.LogError("[CoreCardSnapshot] AwakeningConfig 가 없다 — 카드 값의 주인이 없다."); return; }
            var awakening = AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));

            var def = new MatchDefinition();
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = awakening },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                       CardDefinitionBuilder.WithCardHazards(null, cards));
            string text = Header + CardProbe.CanonicalDeckText(def);

            string before = File.Exists(SnapshotPath) ? File.ReadAllText(SnapshotPath).Replace("\r\n", "\n") : null;
            Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));
            File.WriteAllText(SnapshotPath, text, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(SnapshotPath);
            Debug.Log(before == text
                ? $"[CoreCardSnapshot] 카드 {def.Cards.Length}장 — 변화 없음."
                : $"[CoreCardSnapshot] 카드 {def.Cards.Length}장 — 갱신했다. 파일 diff 를 커밋에 싣는다: {SnapshotPath}");
        }

        private static List<DreamcatcherCard> Cards()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DreamcatcherCardCatalog>(CatalogPath);
            if (catalog == null) { Debug.LogError("[CoreCardSnapshot] 카드 카탈로그가 없다: " + CatalogPath); return null; }
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
