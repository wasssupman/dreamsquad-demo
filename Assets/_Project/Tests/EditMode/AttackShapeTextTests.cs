using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // directional-attack-shape rev 3 — 카드 문안. 도형은 부가 타격만 거르므로 「최대 N체 동시 타격」을 키운 형태이고,
    // N = 1 이면 도형 문안이 없다(효과 0). ⚠ 「전방」을 쓰지 않는다 — 방향은 «때리는 놈 쪽».
    public class AttackShapeTextTests
    {
        private static DefenderUnitData Unit()
        {
            var u = ScriptableObject.CreateInstance<DefenderUnitData>();
            u.role = DefenderClass.Fighter;
            u.projectile = null;
            return u;
        }

        [Test]
        public void Omni_TextUnchanged()
        {
            var u = Unit();
            u.attackTargetCount = 3;
            Assert.AreEqual("파이터 · 근접형. 최대 3체 동시 타격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Sector_SaysSwingSideAndAngle()
        {
            var u = Unit();
            u.attackTargetCount = 3;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 60f };
            Assert.AreEqual("파이터 · 근접형. 휘두르는 쪽 60° 안 최대 3체 동시 타격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Sector_SingleTarget_NoShapeText_ShapeHasNoEffect()
        {
            var u = Unit();
            u.attackTargetCount = 1;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 60f };
            Assert.AreEqual("파이터 · 근접형.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Rect_SaysLineAndWidth()
        {
            var u = Unit();
            u.attackTargetCount = 3;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Rect, width = 1f };
            Assert.AreEqual("파이터 · 근접형. 찌르는 방향 일직선(세로 폭 1) 최대 3체 동시 타격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void ReflexAngle_FallsToOmni_NoShapeText()
        {
            var u = Unit();
            u.attackTargetCount = 3;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 270f };
            Assert.AreEqual("파이터 · 근접형. 최대 3체 동시 타격.", UnitKitSummary.Build(u));
        }
    }
}
