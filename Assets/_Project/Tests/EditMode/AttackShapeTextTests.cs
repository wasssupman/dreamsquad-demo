using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // directional-attack-shape unit 3 — 카드 문안. 기존 어휘(「최대 N체 동시 타격」)를 키우고 새 기호를 얹지
    // 않는다. ⚠ 「전방」을 쓰지 않는다 — 방향은 «캐릭터가 보는 쪽»이고 문안이 그 사실을 말한다.
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
        public void Sector_SaysFacingSideAndAngle()
        {
            var u = Unit();
            u.attackTargetCount = 3;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 90f };
            Assert.AreEqual("파이터 · 근접형. 보는 쪽 90° 안 최대 3체 동시 타격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Sector_SingleTarget_StillSaysShape()
        {
            // rev 2 — 획득도 자르므로 단일 타겟 도형이 유효하다. 문안도 낸다.
            var u = Unit();
            u.attackTargetCount = 1;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 60f };
            Assert.AreEqual("파이터 · 근접형. 보는 쪽 60° 안의 적만 공격.", UnitKitSummary.Build(u));
        }

        [Test]
        public void Rect_SaysLineAndWidth()
        {
            var u = Unit();
            u.attackTargetCount = 3;
            u.attackShape = new AttackShape { kind = AttackShapeKind.Rect, width = 1f };
            Assert.AreEqual("파이터 · 근접형. 보는 쪽 일직선(세로 폭 1) 최대 3체 동시 타격.", UnitKitSummary.Build(u));
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
