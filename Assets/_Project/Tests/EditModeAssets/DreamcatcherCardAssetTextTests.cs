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

            // skill-data-table U18 — 액티브 문안의 비용 = 실제 비용(`AwakeningConfig.costActive` — 전투 손패가 넘기는 카드 값과 같다).
            // 에셋 description(폴백)도 그 숫자로 적혀 있어 같은 비용을 넘겨야 요약과 대조된다.
            var awakeningGuids = AssetDatabase.FindAssets("t:AwakeningConfig");
            Assert.IsNotEmpty(awakeningGuids, "AwakeningConfig 에셋이 없다");
            var awakening = AssetDatabase.LoadAssetAtPath<AwakeningConfig>(AssetDatabase.GUIDToAssetPath(awakeningGuids[0]));

            int structuredCount = 0;
            var unstructured = new List<string>();
            // 카드마다 멈추지 않고 모은다 — 한 장의 선행 실패가 다른 카드의 결과를 가리지 않게(실패 메시지 = 어긋난 카드 id 전부).
            var mismatched = new List<string>();
            foreach (var guid in guids)
            {
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (card == null) continue;

                // skill-data-table unit 8 — 모든 카드의 정형 데이터 = 소유 줄(진영 버프 · 수식자 · 규칙 · 시전 — 옛 effects · attackMods 은퇴).
                bool hasStructuredData = card.bindings != null && card.bindings.Length > 0;
                if (!hasStructuredData) { unstructured.Add(card.name); continue; }

                structuredCount++;
                int? activeCost = card.type == CardType.Active ? awakening.CostFor(CardType.Active) : (int?)null;
                string body = DreamcatcherCardText.Body(card, null, activeCost);
                if (string.IsNullOrEmpty(body)) { mismatched.Add(card.id + "(빈 본문)"); continue; }
                if (!string.IsNullOrEmpty(card.description))
                {
                    int first = body.IndexOf(card.description, System.StringComparison.Ordinal);
                    int last = body.LastIndexOf(card.description, System.StringComparison.Ordinal);
                    if (first < 0) mismatched.Add(card.id + "(description 이 요약에 없다)");
                    else if (first != last) mismatched.Add(card.id + "(description 중복)");
                }

            }

            // «현재 44장» 같은 개수 스냅샷은 카드를 추가할 때마다 깨진다 — 원 의도인
            // «모든 카드가 데이터 정형(소유 줄 저작 — skill-data-table unit 8)» 을 직접 단언한다
            // (test-suite-fast-lane unit 1).
            Assert.IsEmpty(mismatched, $"요약 문안과 description 이 어긋난 카드: [{string.Join(", ", mismatched)}]");
            Assert.IsEmpty(unstructured,
                $"데이터 정형이 아닌 카드: [{string.Join(", ", unstructured)}] — 문안이 구형 fallback 으로 조립된다");
            Assert.Greater(structuredCount, 0, "정형 카드가 하나도 없다 — 스캔 경로 확인");
        }

        // skill-data-table unit 5 — 칸 결합 낙하 탄(SkyFall)의 ProjectileToTarget 문안만 바뀌었다(별똥 타격). **전 카드 본문 전/후 대조**:
        // 옛 formatter 는 이 갈래에서 탄의 궤적을 부메랑 여부로만 갈랐다 — SkyFall 탄을 유도탄 사본으로 바꿔 끼운 카드가 곧 「옛 본문」이다.
        // 그 둘이 다른 카드는 SkyFall 을 든 카드뿐이어야 하고(= 별똥 타격), 나머지 카드는 한 글자도 같아야 한다.
        [Test]
        public void SkyFallStrike_OnlyStarStrikeTextChanges_AllOtherCardsIdentical()
        {
            var made = new List<UnityEngine.Object>();
            var changed = new List<string>();
            int compared = 0;
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { "Assets/_Project/Data/Dreamcatcher" }))
                {
                    var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(AssetDatabase.GUIDToAssetPath(guid));
                    if (card == null) continue;
                    var legacy = UnityEngine.Object.Instantiate(card);
                    made.Add(legacy);
                    if (card.bindings != null)
                    {
                        legacy.bindings = (BindingSpec[])card.bindings.Clone();
                        for (int i = 0; i < legacy.bindings.Length; i++)
                        {
                            var e = legacy.bindings[i].effect;
                            if (e == null || e.values.kind != Wassup.BattleCore.Trigger.EffectKind.ProjectileToTarget
                                || e.projectile == null || e.projectile.flightMode != ProjectileFlightMode.SkyFall) continue;
                            var e2 = UnityEngine.Object.Instantiate(e);
                            var p2 = UnityEngine.Object.Instantiate(e.projectile);
                            p2.flightMode = ProjectileFlightMode.Homing;
                            e2.projectile = p2;
                            made.Add(e2); made.Add(p2);
                            legacy.bindings[i].effect = e2;
                        }
                    }
                    int? cost = card.type == CardType.Active ? 20 : (int?)null;
                    string now = DreamcatcherCardText.Body(card, null, cost);
                    string before = DreamcatcherCardText.Body(legacy, null, cost);
                    compared++;
                    if (now != before)
                    {
                        changed.Add(card.id);
                        TestContext.WriteLine($"[{card.id}]\n  전: {before.Replace("\n", " / ")}\n  후: {now.Replace("\n", " / ")}");
                    }
                }
            }
            finally
            {
                foreach (var o in made) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            }
            TestContext.WriteLine($"대조한 카드 {compared}장 · 바뀐 카드 [{string.Join(", ", changed)}]");
            Assert.Greater(compared, 0);
            CollectionAssert.AreEqual(new[] { "star_strike" }, changed, "칸 결합 낙하 문안 변경은 별똥 타격 한 장만 바꿔야 한다");

            var star = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>("Assets/_Project/Data/Dreamcatcher/Card_StarStrike.asset");
            StringAssert.Contains("공격마다 → 맞은 적 자리에 운석 낙하 · 0.5초 후 반경 1칸 피해 30", DreamcatcherCardText.Body(star));
            StringAssert.DoesNotContain("추가 투사체", DreamcatcherCardText.Body(star));
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
                Assert.IsNotNull(card.RuleView()[0].payload.stackModifier,
                    $"{id} 의 payload.stackModifier 미연결");
                StringAssert.Contains("중첩", DreamcatcherCardText.EffectOnly(card),
                    $"{id} 문안에 임계 요약이 없다");
            }
        }
    }
}
