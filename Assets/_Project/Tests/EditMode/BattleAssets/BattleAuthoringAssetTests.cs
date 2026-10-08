using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.EditModeAssets
{
    // battle-content-finish unit 0 · 4 — **판 저작 SO 세 장이 채워져 있고 씬이 그것을 가리킨다.**
    //
    // 저작이 씬 필드에서 SO 로 옮겨 가면서 「빈 참조」가 조용해질 자리가 생겼다 — 드라이버는 SO 둘이 비면 크게 말하지만,
    // SO 안의 슬롯 하나가 비면 그 기능만 사라진다(장판 0 · 효과 타일 0 · 머티리얼 마젠타). 여기서 슬롯을 전수로 본다.
    // 밸런스 수치는 보지 않는다 — 참조가 있나, 셰이더가 이 프로젝트 것인가만.
    public class BattleAuthoringAssetTests
    {
        private const string ContentPath = "Assets/_Project/Runtime/Battle/Data/BattleContent.asset";
        private const string LoadoutPath = "Assets/_Project/Runtime/Battle/Data/DefaultLoadout.asset";
        private const string MaterialsPath = "Assets/_Project/Runtime/Battle/Data/Materials/Runtime/RuntimeMaterialSet.asset";
        private const string ScenePath = "Assets/_Project/Scenes/BattleCoreScene.unity";

        private static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"{path} 가 없다");
            return a;
        }

        [Test]
        public void BattleContent_필수_슬롯이_전부_채워져_있다()
        {
            var c = Load<BattleContent>(ContentPath);
            Assert.IsNotNull(c.defenderCatalog, "defenderCatalog");
            Assert.IsNotNull(c.stoneCatalog, "stoneCatalog");
            Assert.IsNotNull(c.cardCatalog, "cardCatalog");
            Assert.IsNotNull(c.imbueCaps, "imbueCaps");
            Assert.IsNotNull(c.movementTuning, "movementTuning");
            Assert.IsNotNull(c.seasonRegistry, "seasonRegistry");
            Assert.IsNotNull(c.ActiveMapTheme, "활성 시즌의 맵 테마 — 비면 효과 타일이 한 칸도 안 뽑힌다");
            Assert.IsNotNull(c.bonus, "bonus");
            Assert.IsNotNull(c.runtimeMaterials, "runtimeMaterials");
            AssertNoNull(c.hazards, "hazards");
            AssertNoNull(c.extraBlockers, "extraBlockers");
            AssertNoNull(c.stackModifiers, "stackModifiers");
            AssertNoNull(c.activePool, "activePool");
            AssertNoNull(c.activeCards, "activeCards");
            Assert.GreaterOrEqual(c.activeCount, 0, "activeCount");
        }

        [Test]
        public void DefaultLoadout_편성과_덱이_채워져_있다()
        {
            var l = Load<DefaultLoadout>(LoadoutPath);
            Assert.Greater(l.defenders.Length, 0, "기본 편성 유닛");
            AssertNoNull(l.defenders, "defenders");
            AssertNoNull(l.dreamstones, "dreamstones");
            Assert.IsNotNull(l.deck, "deck — 비우면 입력의 덱 + 굴린 액티브로 짓는다(의도면 이 단언을 고친다)");
            AssertNoNull(l.deck.cards, "deck.cards");
            Assert.Greater(l.DeckCards.Length, 0, "기본 덱 카드");
        }

        [Test]
        public void RuntimeMaterialSet_슬롯_6이_이_프로젝트_셰이더로_채워져_있다()
        {
            var s = Load<RuntimeMaterialSet>(MaterialsPath);
            var slots = new (string name, Material mat)[]
            {
                (nameof(s.solidOpaque), s.solidOpaque), (nameof(s.solidTransparent), s.solidTransparent),
                (nameof(s.texturedOpaque), s.texturedOpaque), (nameof(s.boardOverlay), s.boardOverlay),
                (nameof(s.cardCrumpleUi), s.cardCrumpleUi), (nameof(s.hazardParticle), s.hazardParticle),
            };
            foreach (var (name, mat) in slots)
            {
                Assert.IsNotNull(mat, $"{name} 슬롯이 비었다 — 그 머티리얼을 쓰는 것이 마젠타로 그려진다");
                Assert.IsNotNull(mat.shader, $"{name}: 셰이더 없음");
                Assert.IsTrue(mat.shader.name.StartsWith("Somnia/Battle/") || mat.shader.name.StartsWith("Universal Render Pipeline/"),
                    $"{name}: 셰이더 '{mat.shader.name}' 은 이 프로젝트(Somnia/Battle/*) 나 URP 것이 아니다 — 빌드 스트리핑 대상");
            }
        }

        [Test]
        public void 씬_드라이버가_저작_SO_두_장을_가리킨다()
        {
            // 씬은 에디터 안에서만 고친다(일회용 메뉴) — 그래서 배선 여부를 YAML 로 대조한다. `LiveDefinitionSmokeTests` 와 같은 방식.
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/_Project/Runtime/Battle/Scripts/BattleCoreUnity/BattleDriver.cs");
            string scriptGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            string yaml = System.IO.File.ReadAllText(ScenePath);
            int at = yaml.IndexOf("guid: " + scriptGuid, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(at, 0, "BattleCoreScene 에 BattleDriver 가 없다");
            int end = yaml.IndexOf("\n--- ", at, System.StringComparison.Ordinal);
            string block = end > 0 ? yaml.Substring(at, end - at) : yaml.Substring(at);

            Assert.AreEqual(AssetDatabase.AssetPathToGUID(ContentPath), Ref(block, "_content"), "_content 가 Data/BattleContent.asset 이 아니다");
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(LoadoutPath), Ref(block, "_loadout"), "_loadout 이 Data/DefaultLoadout.asset 이 아니다");
            // 옛 필드가 씬에 고아로 남아 있으면 저장이 안 된 것이다.
            foreach (var old in new[] { "_defenders", "_cards", "_seasonRegistry", "_activePool", "_hazards", "_stackModifiers" })
                Assert.IsFalse(Regex.IsMatch(block, @"\n  " + old + ":"), $"옛 필드 {old} 가 씬에 남아 있다 — 일회용 메뉴로 씬을 저장하라");
        }

        private static string Ref(string block, string field)
        {
            var m = Regex.Match(block, @"\n  " + field + @": \{fileID: -?\d+(?:, guid: ([0-9a-f]{32}))?");
            Assert.IsTrue(m.Success, $"드라이버 칸 {field} 를 못 찾았다");
            return m.Groups[1].Success ? m.Groups[1].Value : null;
        }

        private static void AssertNoNull<T>(T[] arr, string name) where T : Object
        {
            Assert.IsNotNull(arr, name);
            for (int i = 0; i < arr.Length; i++) Assert.IsNotNull(arr[i], $"{name}[{i}] 가 비었다");
        }
    }
}
