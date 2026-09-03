using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // dreamcatcher-attach-range-preview unit 1 — 실제 카드 46장에 대한 **단일 도형 불변식**(README 계약 6).
    //
    // 범위 채널이 하나라 카드당 공간 페이로드는 최대 1개여야 한다. 위반은 loud 로 잡아 결정을 강제한다
    // (조용히 첫 것만 그리면 두 번째 범위가 없는 것처럼 보인다). `mechanics` 와 `attackMods` **양쪽**을
    // 훑는다 — attackMods 의 tileRange(팅김 탐색 반경)는 host 중심이 아니라 카탈로그 대상이 아니고,
    // 그래서 이 축이 도형을 만들지 않음을 여기서 못박는다.
    //
    // ⚠ 오늘 공간 카드 목록은 **로그로만** 남긴다 — 카드가 늘어도 이 테스트가 빨개지면 안 된다.
    // ⚠ 한계(리뷰 M-3): 여기는 **디스크 SO** 만 본다. `DcSheetApplier` 가 런타임에 `payload.tileRange` 를 덮으면
    // 카탈로그는 `tileRange > 0` 으로 도형을 가르므로 라이브 불변식은 `DcRangeCatalog.ResolveCard` 의 1회 경고가
    // 지킨다. kind 는 시트가 못 바꾸므로(구조 변경은 Unity 안에서만) 「어느 mechanic 이 공간인가」 자체는 여기서 확정된다.
    public class DcCardRangeInvariantTests
    {
        private const string CardsRoot = "Assets/_Project/Data/Dreamcatcher";

        private static List<DreamcatcherCard> LoadCards()
        {
            var result = new List<DreamcatcherCard>();
            foreach (var guid in AssetDatabase.FindAssets("t:DreamcatcherCard", new[] { CardsRoot }))
            {
                var card = AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) result.Add(card);
            }
            Assert.IsNotEmpty(result, "DreamcatcherCard 에셋을 찾지 못했다 — 경로 규약이 바뀌었나?");
            return result;
        }

        [Test]
        public void EveryCard_HasAtMostOneSpatialShape()
        {
            var drawable = new StringBuilder();
            foreach (var card in LoadCards())
            {
                var distinct = new HashSet<(DcRangeShape, float)>();
                if (card.mechanics != null)
                {
                    foreach (var m in card.mechanics)
                    {
                        int skillId = DcSkillRouting.SkillIdFor(m.trigger.kind, m.payload.kind);
                        var spec = DcRangeCatalog.Resolve(skillId, m.payload.tileRange, m.trigger.kind);
                        if (spec.shape != DcRangeShape.None) distinct.Add((spec.shape, spec.radiusTiles));
                    }
                }
                Assert.LessOrEqual(distinct.Count, 1,
                    $"{card.id}: 공간 페이로드가 {distinct.Count}개 — 범위 채널은 하나다. 카드 분할 또는 표기 결정이 필요하다.");

                var resolved = DcRangeCatalog.ResolveCard(card);
                if (resolved.shape != DcRangeShape.None)
                    drawable.Append(card.id).Append('(').Append(resolved.radiusTiles).Append(") ");
            }
            Debug.Log($"[DcCardRangeInvariant] 오늘 범위를 그리는 카드: {drawable}");
        }

        [Test]
        public void AttackModOnlyCards_DrawNothing()
        {
            // 팅김 반경(bouncy_bead 3) 등은 착탄점 기준이라 host 중심 범위가 아니다.
            foreach (var card in LoadCards())
            {
                bool mechanicsEmpty = card.mechanics == null || card.mechanics.Length == 0;
                bool hasAttackMods = card.attackMods != null && card.attackMods.Length > 0;
                if (!mechanicsEmpty || !hasAttackMods) continue;
                Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.ResolveCard(card).shape,
                    $"{card.id}: attackMods 만 있는 카드가 범위를 그리면 안 된다");
            }
        }

        [Test]
        public void ResolveCard_NeverThrows_ForAnyAuthoredCard()
        {
            foreach (var card in LoadCards())
                Assert.DoesNotThrow(() => DcRangeCatalog.ResolveCard(card), card.id);
        }
    }
}
