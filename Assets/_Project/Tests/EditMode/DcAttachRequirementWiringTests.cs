using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attach-requirement unit 5 — 문안 resolver 배선 핀.
    //
    // 실제 실패 모드는 "SerializeField 가 씬에서 비어 있다"다: 컴파일도 되고 포매터
    // 단위 테스트도 통과하는데 화면에는 유닛 id 가 그대로 보인다. 그래서 씬 **에셋**을
    // 텍스트로 읽어 각 뷰 블록에 defenderCatalog 참조가 실제로 있는지 확인한다.
    //
    // 왜 PlayMode 가 아닌가: 씬을 런타임 로드하는 검증은 아웃게임 부트스트랩(프로필/
    // 로드아웃 로드)을 돌려 뒤따르는 전투 테스트의 장착 상태를 오염시킨다(전체 실행에서
    // DreamcatcherCombatDamage·GateE2E 가 단독으론 통과하는데 함께 돌면 실패). 배선은
    // 정적 사실이므로 에셋을 직접 보는 쪽이 결정론적이고 부작용이 없다.
    public class DcAttachRequirementWiringTests
    {
        private const string BattleScene = "Assets/_Project/Scenes/BattleScene.unity";
        private const string OutgameScene = "Assets/_Project/Scenes/OutgameScene.unity";

        [Test]
        public void BattleScene_HandAndInspectViews_HaveCatalogAssigned()
        {
            AssertWired(BattleScene, "Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs");
            AssertWired(BattleScene, "Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs");
        }

        [Test]
        public void OutgameScene_DeckSurfaces_HaveCatalogAssigned()
        {
            AssertWired(OutgameScene, "Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs");
            // DeckPage 는 런타임 생성되는 DreamcatcherCardDetailView 의 주입원 —
            // 여기가 비면 덱 상세 문안이 유닛 id 로 보인다.
            AssertWired(OutgameScene, "Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs");
        }

        [Test]
        public void RealCatalog_ResolvesDisplayName_AndDrivesPrefix()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(
                "Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsNotNull(catalog, "DefenderCatalog 에셋");

            string id = null, expectedName = null;
            foreach (var u in catalog.units)
            {
                if (u == null || string.IsNullOrEmpty(u.id) || string.IsNullOrEmpty(u.displayName)) continue;
                id = u.id; expectedName = u.displayName; break;
            }
            Assert.IsNotNull(id, "표시명이 있는 유닛이 하나 이상 있어야 한다");
            Assert.AreEqual(expectedName, catalog.DisplayNameOf(id));
            Assert.IsNull(catalog.DisplayNameOf("no_such_unit"), "없는 id 는 null → 포매터 id 폴백");

            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.type = CardType.Unit;
            card.description = "부착 즉시 → 뭔가 한다";
            card.attachRequire = DcAttachRequireKind.UnitId;
            card.attachRequireUnitId = id;

            Assert.That(DreamcatcherCardText.BodyLinesOnly(card, catalog.DisplayNameOf),
                Does.StartWith($"{expectedName} 전용"), "해석기를 넘기면 표시명 접두");
            Assert.That(DreamcatcherCardText.BodyLinesOnly(card),
                Does.StartWith($"{id} 전용"), "미주입이면 id 폴백 — 배선의 효과가 관측된다는 대조");
            Object.DestroyImmediate(card);
        }

        // 씬 YAML 의 해당 스크립트 블록에 비어있지 않은 defenderCatalog 참조가 있는지.
        private static void AssertWired(string scenePath, string scriptPath)
        {
            string scriptGuid = AssetDatabase.AssetPathToGUID(scriptPath);
            Assert.IsFalse(string.IsNullOrEmpty(scriptGuid), $"script guid: {scriptPath}");
            Assert.IsTrue(File.Exists(scenePath), scenePath);

            string name = Path.GetFileNameWithoutExtension(scriptPath);
            int found = 0;
            foreach (string block in File.ReadAllText(scenePath).Split(new[] { "--- !u!" }, System.StringSplitOptions.None))
            {
                if (!block.Contains($"guid: {scriptGuid}")) continue;
                found++;
                Assert.IsTrue(block.Contains("defenderCatalog:"),
                    $"{name}: 씬 블록에 defenderCatalog 키가 없다 — 씬을 다시 저장할 것");
                Assert.IsFalse(block.Contains("defenderCatalog: {fileID: 0}"),
                    $"{name}: defenderCatalog 미할당 — 문안이 유닛 표시명 대신 id 로 보인다");
            }
            Assert.AreEqual(1, found, $"{name}: 씬에 컴포넌트 인스턴스가 정확히 1개여야 한다");
        }
    }
}
