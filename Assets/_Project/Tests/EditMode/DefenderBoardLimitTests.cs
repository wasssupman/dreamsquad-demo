using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // defender-board-limit 0 — 판 위 동시 배치 상한의 저작 계약.
    //
    // 여기서 지키는 것은 두 가지다: (1) 상한 값 해석이 미저작/오저작에서도 1 로 수렴하는가,
    // (2) 거부 사유 enum 의 기존 직렬화 값이 보존됐는가. 실제 카운트(_defenderByTile 순회)는
    // 엔티티 월드가 필요해 여기서 다루지 않는다 — Play 검증의 몫이다.
    public class DefenderBoardLimitTests
    {
        private readonly List<Object> _created = new();

        private DefenderUnitData MakeUnit()
        {
            var so = ScriptableObject.CreateInstance<DefenderUnitData>();
            _created.Add(so);
            return so;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        // 미저작(= 기존 asset 이 YAML 에 키 없이 로드되는 경우와 같은 상태) → 1.
        // 이 값이 0 으로 새면 전 유닛이 "배치 불가" 가 되므로 가장 중요한 단언이다.
        [Test]
        public void Unauthored_DefaultsToOne()
        {
            Assert.AreEqual(1, MakeUnit().maxOnBoard);
            Assert.AreEqual(1, MakeUnit().EffectiveMaxOnBoard);
        }

        // 인스펙터/시트에서 0·음수가 들어오는 두 번째 방어선.
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(-999)]
        public void NonPositive_FallsBackToOne(int authored)
        {
            var unit = MakeUnit();
            unit.maxOnBoard = authored;
            Assert.AreEqual(1, unit.EffectiveMaxOnBoard);
        }

        // 저작값은 그대로 통과한다. 100 = 배치 가능 셀 수보다 커서 사실상 무제한
        // (예외 분기 없이 "지금과 동일 동작" 을 표현하는 방법).
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(100)]
        public void Authored_PassesThrough(int authored)
        {
            var unit = MakeUnit();
            unit.maxOnBoard = authored;
            Assert.AreEqual(authored, unit.EffectiveMaxOnBoard);
        }

        // 신규 사유는 enum **끝에** 붙어야 한다. 중간에 끼우면 이미 직렬화된 기존 값의 의미가
        // 통째로 밀린다(defender-relocation 이 같은 이유로 뒤에 붙였다).
        [Test]
        public void LimitReached_IsAppendedLast()
        {
            Assert.AreEqual(8, (int)PlacementRejectReason.InsufficientCost);
            Assert.AreEqual(11, (int)PlacementRejectReason.SameCell);
            Assert.AreEqual(12, (int)PlacementRejectReason.LimitReached);
        }
    }
}
