using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // test-suite-fast-lane unit 0 — DreamcatcherCardTextTests 에서 추출한 실카탈로그 검증.
    // 문안 조립 로직 테스트(합성 카드)는 코어 lane 에 남는다.
    public class DreamcatcherCardAssetTextTests
    {
        [Test]
        public void CardAssets_UseStructuredSummaryWhenDataExists()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:DreamcatcherCard", new[] { "Assets/_Project/Data/Dreamcatcher" });
            Assert.IsNotEmpty(guids);

            int structuredCount = 0;
            var unstructured = new List<string>();
            foreach (var guid in guids)
            {
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (card == null) continue;

                bool hasStructuredData = card.type == CardType.Squad
                    ? card.effects != null && card.effects.Length > 0
                    : card.type == CardType.Unit
                        ? (card.mechanics != null && card.mechanics.Length > 0)
                          || (card.attackMods != null && card.attackMods.Length > 0)
                        : card.skill != null;
                if (!hasStructuredData) { unstructured.Add(card.name); continue; }

                structuredCount++;
                string body = DreamcatcherCardText.Body(card);
                Assert.IsFalse(string.IsNullOrEmpty(body), $"empty body: {card.id}");
                if (!string.IsNullOrEmpty(card.description))
                {
                    int first = body.IndexOf(card.description, System.StringComparison.Ordinal);
                    int last = body.LastIndexOf(card.description, System.StringComparison.Ordinal);
                    Assert.GreaterOrEqual(first, 0, card.id);
                    Assert.AreEqual(first, last, card.id + " description must not be duplicated");
                }

            }

            // «현재 44장» 같은 개수 스냅샷은 카드를 추가할 때마다 깨진다 — 원 의도인
            // «모든 카드가 데이터 정형(effects/mechanics/skill 저작)» 을 직접 단언한다
            // (test-suite-fast-lane unit 1).
            Assert.IsEmpty(unstructured,
                $"데이터 정형이 아닌 카드: [{string.Join(", ", unstructured)}] — 문안이 구형 fallback 으로 조립된다");
            Assert.Greater(structuredCount, 0, "정형 카드가 하나도 없다 — 스캔 경로 확인");
        }

        [Test]
        public void StackAssets_CarryTheirModifierReference()
        {
            // 참조가 빠지면 문안에서 임계가 조용히 사라진다 — authoring 회귀 가드.
            foreach (var id in new[] { "Card_Frostbite", "Card_EmberBite" })
            {
                var path = $"Assets/_Project/Data/Dreamcatcher/{id}.asset";
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(path);
                Assert.IsNotNull(card, $"{path} 로드 실패");
                Assert.IsNotNull(card.mechanics[0].payload.stackModifier,
                    $"{id} 의 payload.stackModifier 미연결");
                StringAssert.Contains("중첩", DreamcatcherCardText.EffectOnly(card),
                    $"{id} 문안에 임계 요약이 없다");
            }
        }
    }
}
