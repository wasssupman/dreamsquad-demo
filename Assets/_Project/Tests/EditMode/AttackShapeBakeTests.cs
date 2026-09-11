using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Combat;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // directional-attack-shape unit 1 — 저작 → bake 의 절대값과 **폴백 방향**을 못박는다.
    //
    // 폴백이 전부 Omni(=오늘 동작)인 이유: 도형은 **게이트**라 fail-open 이 안전하다. 반대로 접으면
    // 저작 실수 하나(각도 0, reflex 각)가 유닛을 무력화한다. 기존 27개 에셋은 YAML 에 키가 없어
    // struct 가 0 으로 오는데, 그것도 이 폴백이 받아야 한다.
    public class AttackShapeBakeTests
    {
        private static AttackShape Circle(float deg) => new AttackShape { kind = AttackShapeKind.Circle, angleDeg = deg };
        private static AttackShape Rect(float width) => new AttackShape { kind = AttackShapeKind.Rect, width = width };

        [Test]
        public void DefaultStruct_IsOmni_ThatIsTheYamlMissingKeyCase()
        {
            var baked = AttackShapeBake.From(default(AttackShape), out bool ok);
            Assert.IsTrue(baked.IsOmni);
            Assert.IsTrue(ok, "0 = 미저작 — 에러가 아니다");
        }

        [Test]
        public void Circle360_And_Circle0_AreOmni()
        {
            Assert.IsTrue(AttackShapeBake.From(Circle(360f), out _).IsOmni);
            Assert.IsTrue(AttackShapeBake.From(Circle(0f), out _).IsOmni);
            Assert.IsTrue(AttackShapeBake.From(AttackShape.Omni, out _).IsOmni);
        }

        [Test]
        public void Circle120_BakesHalfAngleSinCos()
        {
            var b = AttackShapeBake.From(Circle(120f), out bool ok);
            Assert.IsTrue(ok);
            Assert.AreEqual(AttackShapeBaked.SectorKind, b.kind);
            Assert.AreEqual(math.sin(math.radians(60f)), b.sinHalf, 1e-5f);
            Assert.AreEqual(math.cos(math.radians(60f)), b.cosHalf, 1e-5f);
        }

        [Test]
        public void Circle180_IsHalfPlane_SinOneCosZero()
        {
            var b = AttackShapeBake.From(Circle(180f), out _);
            Assert.AreEqual(AttackShapeBaked.SectorKind, b.kind);
            Assert.AreEqual(1f, b.sinHalf, 1e-5f);
            Assert.AreEqual(0f, b.cosHalf, 1e-5f);
        }

        [Test]
        public void ReflexAngle_IsRejected_FallsOpenToOmni()
        {
            // (180, 360) — reflex 부채꼴. 정의역 밖이라 ok=false, 동작은 오늘(Omni)로.
            var b = AttackShapeBake.From(Circle(270f), out bool ok);
            Assert.IsFalse(ok, "정의역 밖은 저작자에게 말한다");
            Assert.IsTrue(b.IsOmni, "그러나 유닛을 무력화하지 않는다");
        }

        [Test]
        public void Rect_BakesHalfWidth()
        {
            var b = AttackShapeBake.From(Rect(1.0f), out bool ok);
            Assert.IsTrue(ok);
            Assert.AreEqual(AttackShapeBaked.BandKind, b.kind);
            Assert.AreEqual(0.5f, b.halfWidth, 1e-6f);
        }

        [Test]
        public void Rect_ZeroWidth_IsStillBand_NotOmni()
        {
            // 폭 0 = 「몸이 축에 걸치면」— 유효한 저작이다(게이트 테스트가 그 성질을 고정한다).
            var b = AttackShapeBake.From(Rect(0f), out bool ok);
            Assert.IsTrue(ok);
            Assert.AreEqual(AttackShapeBaked.BandKind, b.kind);
        }
    }
}
