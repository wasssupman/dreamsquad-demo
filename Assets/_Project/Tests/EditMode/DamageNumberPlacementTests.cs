using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    // damage-number-visual-upgrade unit 0 — 겹침 방지 슬롯 탐색 순수 함수 회귀.
    // 결정론(RNG/시간 미사용) + 위쪽 편향 + 가장 가까운 빈 셀 계약을 고정한다.
    public class DamageNumberPlacementTests
    {
        [Test]
        public void FreeIntended_ReturnsIntended()
        {
            var occ = new HashSet<Vector2Int>();
            var cell = DamageNumberSpawner.FindFreeCell(new Vector2Int(3, 5), occ, 4);
            Assert.AreEqual(new Vector2Int(3, 5), cell);
        }

        [Test]
        public void OccupiedIntended_BumpsStraightUp()
        {
            // 의도 셀만 점유 → ring 1 첫 후보는 바로 위(위쪽 편향).
            var occ = new HashSet<Vector2Int> { new Vector2Int(0, 0) };
            var cell = DamageNumberSpawner.FindFreeCell(new Vector2Int(0, 0), occ, 4);
            Assert.AreEqual(new Vector2Int(0, 1), cell);
        }

        [Test]
        public void ColumnStack_RisesUpward()
        {
            // 같은 위치 연타: 의도+바로위 점유 → 그 다음 후보.
            var occ = new HashSet<Vector2Int> { new Vector2Int(0, 0), new Vector2Int(0, 1) };
            var cell = DamageNumberSpawner.FindFreeCell(new Vector2Int(0, 0), occ, 4);
            Assert.AreEqual(new Vector2Int(-1, 1), cell);
        }

        [Test]
        public void RingFull_FallsToNextRingTop()
        {
            // ring 0(의도) + ring 1 전부 점유 → ring 2 최상단 (0,2).
            var occ = new HashSet<Vector2Int>();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    occ.Add(new Vector2Int(dx, dy));
            var cell = DamageNumberSpawner.FindFreeCell(Vector2Int.zero, occ, 4);
            Assert.AreEqual(new Vector2Int(0, 2), cell);
        }

        [Test]
        public void AllRingsFull_ReturnsIntendedDegenerate()
        {
            // maxRings 내 전부 점유 → 의도 셀로 폴백(중첩 허용).
            var occ = new HashSet<Vector2Int>();
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    occ.Add(new Vector2Int(dx, dy));
            var cell = DamageNumberSpawner.FindFreeCell(Vector2Int.zero, occ, 1);
            Assert.AreEqual(Vector2Int.zero, cell);
        }

        [Test]
        public void Deterministic_SameInputsSameOutput()
        {
            var occ = new HashSet<Vector2Int> { new Vector2Int(2, 2), new Vector2Int(2, 3) };
            var a = DamageNumberSpawner.FindFreeCell(new Vector2Int(2, 2), occ, 4);
            var b = DamageNumberSpawner.FindFreeCell(new Vector2Int(2, 2), occ, 4);
            Assert.AreEqual(a, b);
        }

        [Test]
        public void NeverReturnsOccupiedWhenFreeExists()
        {
            var occ = new HashSet<Vector2Int>();
            // 넓게 점유하되 한 칸 비움 → 반환 셀은 비점유여야.
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    if (!(dx == 1 && dy == -2))
                        occ.Add(new Vector2Int(dx, dy));
            var cell = DamageNumberSpawner.FindFreeCell(Vector2Int.zero, occ, 4);
            Assert.IsFalse(occ.Contains(cell));
        }
    }
}
