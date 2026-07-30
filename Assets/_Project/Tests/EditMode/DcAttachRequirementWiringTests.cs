using System.IO;
using System.Reflection;
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
            // authored-preset-removal unit 2 — 구 `DreamcatcherDeckBuilderView` 핀은 제거됐다.
            // 그 컴포넌트는 사문화(m_Enabled 0) 상태로 씬에 남아 있던 레거시이고 이제
            // 삭제됐다. 남은 덱 표면은 DeckPage 하나다.
            //
            // DeckPage 는 런타임 생성되는 DreamcatcherCardDetailView 의 주입원 —
            // 여기가 비면 덱 상세 문안이 유닛 id 로 보인다.
            AssertWired(OutgameScene, "Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs");
        }

        // review M3 — DreamcatcherCardDetailView 는 씬 와이어가 불가(런타임 AddComponent)라
        // DreamcatcherDeckPage 가 SetField("defenderCatalog", ...) 로 **문자열 이름** 리플렉션
        // 주입한다. SetField 는 필드를 못 찾으면 조용히 no-op 하므로, 필드명을 바꾸면
        // 덱 상세 문안이 경고 없이 유닛 id 로 되돌아간다. 그 rename 을 여기서 잡는다.
        [Test]
        public void DetailView_FieldName_MatchesDeckPageInjectionKey()
        {
            var f = typeof(DreamcatcherCardDetailView).GetField("defenderCatalog",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f,
                "DreamcatcherCardDetailView.defenderCatalog 필드명이 DeckPage 의 SetField 주입 키와 어긋났다 — 덱 상세 문안이 조용히 id 폴백된다");
            Assert.AreEqual(typeof(DefenderCatalog), f.FieldType);

            // 주입 호출이 실제로 그 이름을 쓰는지도 확인(양쪽이 함께 틀어지는 것 방지).
            string page = File.ReadAllText("Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPage.cs");
            Assert.That(page, Does.Contain("SetField(detailView, \"defenderCatalog\""),
                "DeckPage 가 detailView 에 defenderCatalog 를 주입하지 않는다");
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
            card.attachType = DcAttachType.UnitId;
            card.attachValue = id;

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

            // 참조가 '비어있지 않은' 것만 보면 다른 에셋을 가리켜도 통과한다 → 실제
            // DefenderCatalog guid 와 일치하는지까지 본다(review 지적).
            string catalogGuid = AssetDatabase.AssetPathToGUID("Assets/_Project/Data/DefenderCatalog.asset");
            Assert.IsFalse(string.IsNullOrEmpty(catalogGuid), "DefenderCatalog 에셋 guid");

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
                Assert.IsTrue(block.Contains($"defenderCatalog: {{fileID: 11400000, guid: {catalogGuid}"),
                    $"{name}: defenderCatalog 가 DefenderCatalog.asset 이 아닌 다른 에셋을 가리킨다");
            }
            // 인스턴스가 2개 이상이어도(변형·디버그 캔버스) 전부 배선돼 있으면 통과 —
            // 개수 자체는 이 테스트가 핀할 대상이 아니다(review 지적).
            Assert.GreaterOrEqual(found, 1, $"{name}: 씬에서 컴포넌트를 찾지 못했다");
        }
    }
}
