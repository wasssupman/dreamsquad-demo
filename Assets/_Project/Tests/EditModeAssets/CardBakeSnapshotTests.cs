using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // battle-core-rebuild unit 7e ② — **카드를 구운 결과가 굳힌 파일과 같다.**
    //
    // 시트 임포트 · SO 편집이 카드를 조용히 되돌리는 함정(`project_sheet_overwrites_unit_tuning`)을 잡는다. 파일은 카드별
    // canonical 텍스트(`CardProbe.CanonicalCardText` — 정의표 해시와 **같은** canonicalize)를 카드 순으로 이은 것이라,
    // diff 가 곧 「무엇이 바뀌었나」다.
    //
    // 굽기는 카드만 싣는다(`CardDefinitionBuilder.Fill` — `CardBakeTests` 와 같은 경로). 판 저작(방어유닛 · 덱)을 같이 구우면
    // 카드와 무관한 탄이 늘 때 줄 번호가 밀려 스냅샷이 흔들린다.
    //
    // ⚠ **테스트는 파일을 쓰지 않는다.** 의도한 변경이면 `Wassup/BattleCore/Debug/카드 스냅샷 갱신` 메뉴로 갱신하고
    // 파일 diff 를 같은 커밋에 싣는다(메뉴 = `Editor/BattleCore/CoreCardSnapshotMenu.cs` — 굽기 규칙이 이 파일과 같다).
    public class CardBakeSnapshotTests
    {
        public const string SnapshotPath = "Assets/_Project/Tests/EditModeAssets/Fixtures/card_bake_snapshot.txt";
        public const string Header = "# battle-core-rebuild 7e — 카드 굽기 스냅샷. 손으로 고치지 말 것: Wassup/BattleCore/Debug/카드 스냅샷 갱신\n";

        private static AwakeningConfig Awakening()
        {
            var guids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(guids);
            return AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        public static string Bake(List<DreamcatcherCard> cards)
        {
            var def = new MatchDefinition();
            CardDefinitionBuilder.Fill(def, new CardAuthoring { Cards = cards, Awakening = Awakening() },
                                       new List<ProjectileData>(), new List<ProjectilePatternData>(),
                                       CardDefinitionBuilder.WithCardHazards(null, cards));
            Assert.AreEqual(cards.Count, def.Cards.Length, "덱 순서 = 카드 줄 순서");
            return Header + CardProbe.CanonicalDeckText(def);
        }

        private static string Committed()
        {
            Assert.IsTrue(File.Exists(SnapshotPath), "스냅샷 파일이 없다 — 메뉴로 한 번 굽는다: " + SnapshotPath);
            return File.ReadAllText(SnapshotPath).Replace("\r\n", "\n");
        }

        // 첫 차이의 줄 + 그 줄이 속한 카드. 전문 비교 실패 메시지는 읽을 수 없다.
        private static string FirstDiff(string expected, string actual)
        {
            var a = expected.Split('\n');
            var b = actual.Split('\n');
            string card = "?";
            for (int i = 0; i < System.Math.Max(a.Length, b.Length); i++)
            {
                string x = i < a.Length ? a[i] : "<끝>";
                string y = i < b.Length ? b[i] : "<끝>";
                if (x.StartsWith("[card ")) card = x;
                if (x != y) return $"{i + 1}번째 줄({card}): 굳힌 값 `{x}` ↔ 지금 `{y}`";
            }
            return null;
        }

        [Test]
        public void 카드_굽기가_굳힌_스냅샷과_같다()
        {
            string now = Bake(CardEffectWitnessTests.Cards());
            string diff = FirstDiff(Committed(), now);
            Assert.IsNull(diff, "카드 굽기가 스냅샷과 다르다 — 의도한 변경이면 메뉴로 갱신: " + diff);
        }

        [Test]
        public void 반증_카드_SO_값_하나를_메모리에서_바꾸면_빨갛다()
        {
            var cards = CardEffectWitnessTests.Cards();
            string committed = Committed();
            // 규칙 줄을 싣는 첫 카드의 첫 소유 줄이 가리키는 **효과 에셋**의 피해(메모리에서만 — 저장하지 않고 되돌린다).
            // skill-data-table unit 4 — 이전 뒤 빌더는 옛 칸(mechanics)이 아니라 소유 줄 → 효과 에셋을 읽는다.
            DreamcatcherCard target = null;
            foreach (var c in cards)
                if (c.type == CardType.Unit && c.bindings != null && c.bindings.Length > 0 && c.bindings[0].effect != null
                    && c.bindings[0].effect.values.damage > 0f) { target = c; break; }
            Assert.IsNotNull(target, "피해 효과를 참조하는 카드가 없다");
            var effect = target.bindings[0].effect;
            float was = effect.values.damage;
            try
            {
                effect.values.damage = was + 1f;
                string diff = FirstDiff(committed, Bake(cards));
                TestContext.WriteLine($"반증 대상 {target.id} → 효과 {effect.id}: {diff}");
                Assert.IsNotNull(diff, $"{target.id} 의 효과 '{effect.id}' 피해를 바꿨는데 스냅샷이 같다 — 스냅샷이 저작을 증언하지 못한다");
                StringAssert.Contains(target.id, diff, "차이가 바꾼 그 카드에서 난다");
            }
            finally
            {
                effect.values.damage = was;
            }
            Assert.IsNull(FirstDiff(committed, Bake(cards)), "되돌린 뒤엔 다시 같다");
        }
    }
}
