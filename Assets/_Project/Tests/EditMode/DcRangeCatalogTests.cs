using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // dreamcatcher-attach-range-preview unit 1 — 공간성 카탈로그(concrete → 도형·반경)의 핀.
    //
    // 지키는 것 셋: ① 도형은 kind(concrete)로만 정한다 — 값(`tileRange`)으로 추정하지 않는다.
    // 겸직 kind 는 어떤 값이 와도 None 이다(시트가 값을 매 로그인마다 덮는다). ② 반경은 판정 입력의
    // 복사본 — 광역은 `N + CellHalfWidthTiles`, 발사명세는 `N`. ③ 모르는 concrete 는 None(fail-closed) —
    // 없는 범위를 지어내지 않는다.
    public class DcRangeCatalogTests
    {
        private static readonly int[] AreaCircleIds =
        {
            SelfAreaBlastSkill.Id, AreaSleepSkill.Id, AreaCcSkill.Id, AreaDotSkill.Id, AreaStackSkill.Id,
            AreaTauntSkill.Id, AllySpeedAuraSkill.Id, AllyStatAuraSkill.Id, OpponentStatAuraSkill.Id,
            GrantShieldSkill.Id,
        };

        private static readonly int[] NoShapeIds =
        {
            DeathSiteBlastSkill.Id, DeathSiteHazardSkill.Id, ConeBreathSkill.Id, TargetProjectileSkill.Id,
            TargetStackSkill.Id, TargetCcSkill.Id, SelfStatBuffSkill.Id, ThresholdSelfBuffSkill.Id,
            BountyMarkSkill.Id, OrbitProjectileSkill.Id, TileStatBurstSkill.Id,
        };

        [Test]
        public void AreaConcretes_AreCircles_OfRangePlusCellHalfWidth()
        {
            foreach (int id in AreaCircleIds)
            {
                var spec = DcRangeCatalog.Resolve(id, 2);
                Assert.AreEqual(DcRangeShape.Circle, spec.shape, $"skill {id}");
                Assert.AreEqual(2f + SkillMath.CellHalfWidthTiles, spec.radiusTiles, 1e-6f, $"skill {id} 반경은 N + 칸 반폭");
            }
        }

        [Test]
        public void AreaConcretes_WithZeroRange_HaveNoShape()
        {
            // GrantShield 0 = 자기만, 그 외 0 은 저작 오류 — 어느 쪽이든 그릴 범위가 없다.
            foreach (int id in AreaCircleIds)
                Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.Resolve(id, 0).shape, $"skill {id}");
        }

        [Test]
        public void EmitPattern_IsACircle_OfRangeWithoutCellHalfWidth()
        {
            // 사거리 자 — 탄 비행 거리라 칸 반폭을 더하지 않는다(EmitPatternSkill 의 Euclidean arm).
            var spec = DcRangeCatalog.Resolve(EmitPatternSkill.Id, 3);
            Assert.AreEqual(DcRangeShape.Circle, spec.shape);
            Assert.AreEqual(3f, spec.radiusTiles, 1e-6f);
            Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.Resolve(EmitPatternSkill.Id, 0).shape);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(30)]
        public void NonSpatialConcretes_HaveNoShape_WhateverTheValue(int tileRange)
        {
            // 겸직 tileRange(피해감소% · 누적 상한 · maxStack · 폴백 반경 · 궤도 반경)는 값이 커도 범위가 아니다.
            foreach (int id in NoShapeIds)
                Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.Resolve(id, tileRange).shape, $"skill {id} tileRange {tileRange}");
        }

        [Test]
        public void UnknownConcrete_HasNoShape()
        {
            Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.Resolve(9999, 3).shape);
            Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.Resolve(SkillRegistry.NotRouted, 3).shape);
        }

        [Test]
        public void ResolveCard_PicksTheSpatialMechanic()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            try
            {
                card.mechanics = new[]
                {
                    Mechanic(DcTriggerKind.OnDamagedN, DcPayloadKind.SelfTileAoe, tileRange: 1),
                };
                var spec = DcRangeCatalog.ResolveCard(card);
                Assert.AreEqual(DcRangeShape.Circle, spec.shape);
                Assert.AreEqual(1f + SkillMath.CellHalfWidthTiles, spec.radiusTiles, 1e-6f);
            }
            finally { Object.DestroyImmediate(card); }
        }

        [Test]
        public void ResolveCard_DeathSiteAndNonSpatial_HaveNoShape()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            try
            {
                card.mechanics = new[]
                {
                    Mechanic(DcTriggerKind.OnDeath, DcPayloadKind.SelfTileAoe, tileRange: 2),      // 죽은 자리 — 위치 없음
                    Mechanic(DcTriggerKind.AttackN, DcPayloadKind.SelfStatBuff, tileRange: 10),   // 누적 상한
                };
                Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.ResolveCard(card).shape);
                card.mechanics = null;
                Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.ResolveCard(card).shape, "메커닉 없는 카드");
                Assert.AreEqual(DcRangeShape.None, DcRangeCatalog.ResolveCard(null).shape, "null 카드");
            }
            finally { Object.DestroyImmediate(card); }
        }

        private static DcMechanic Mechanic(DcTriggerKind trigger, DcPayloadKind kind, int tileRange)
            => new DcMechanic
            {
                trigger = new DcTriggerSpec { kind = trigger },
                payload = new DcPayloadSpec { kind = kind, tileRange = tileRange },
            };
    }
}
