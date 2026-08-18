using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Presentation;

namespace Wassup.Tests.EditMode
{
    public class BoardSortOrderTests
    {
        [Test]
        public void CharacterOffsetSortsAheadOfPropInSameCell()
        {
            var grid = new int2(12, 10);
            int prop = BoardSortOrder.Compute(grid, 3, 4);
            int character = BoardSortOrder.Compute(grid, 3, 4, BoardSortOrder.CharacterOffset);

            Assert.That(character, Is.EqualTo(prop + 1));
        }

        [Test]
        public void LowerBoardYSortsAheadOfHigherBoardY()
        {
            var grid = new int2(12, 10);
            int lower = BoardSortOrder.Compute(grid, 3, 2);
            int higher = BoardSortOrder.Compute(grid, 3, 7);

            Assert.That(lower, Is.GreaterThan(higher));
        }

        [Test]
        public void ComputeFromWorldRoundsToBoardCell()
        {
            var grid = new int2(12, 10);
            var world = new Vector3(2.51f, 0f, 4.49f);

            int fromWorld = BoardSortOrder.ComputeFromWorld(grid, world, 1f);

            Assert.That(fromWorld, Is.EqualTo(BoardSortOrder.Compute(grid, 3, 4)));
        }

        // ── map-diorama-stage unit 3 — 정렬 대역 계약 추가 고정 (critic M-9) ──────────
        //   ① near/far: 행 간격이 폭 종속(max(10, w+2))이 되어 폭 48 에서도 앞줄이 항상 위.
        //   ② 대역: Compute 최대치(상한 48×48)가 ProjectileOffset 아래 — 넘으면 투사체가
        //      유닛 뒤에 깔리는 대역 붕괴(빔/브레스에서 실측된 증상 계열).

        [Test]
        public void Compute_FrontRow_AlwaysAboveBackRow_AtWidth48()
        {
            var grid = new int2(48, 32);
            // 구 결함의 최악 조합: 뒷줄(y+1) 맨 오른쪽 vs 앞줄(y) 맨 왼쪽 — 간격 10 시절 폭>10 이면 역전됐다.
            for (int y = 0; y < grid.y - 1; y++)
            {
                int backRight = BoardSortOrder.Compute(grid, grid.x - 1, y + 1);
                int frontLeft = BoardSortOrder.Compute(grid, 0, y);
                Assert.Greater(frontLeft, backRight,
                    $"y={y}: 앞줄 왼쪽({frontLeft})이 뒷줄 오른쪽({backRight})보다 커야 한다");
            }
        }

        [Test]
        public void Compute_MaxAtGridCap_StaysBelowProjectileOffset()
        {
            var grid = new int2(BoardSortOrder.MaxGridSide, BoardSortOrder.MaxGridSide);
            int max = BoardSortOrder.Compute(grid, grid.x - 1, 0, BoardSortOrder.CharacterOffset);
            Assert.Less(max, BoardSortOrder.ProjectileOffset,
                "유닛 order 최대치가 투사체 대역을 침범 — playArea 상한 또는 대역 재설계 필요");
        }

        [Test]
        public void Compute_SmallGrid_KeepsLegacyStride()
        {
            // 폭 ≤ 8 인 기존 소형 픽스처의 order 값이 구 산식(간격 10)과 동일 — 좁은 맵 무회귀.
            var grid = new int2(8, 6);
            Assert.AreEqual((6 - 2) * 10 + 3, BoardSortOrder.Compute(grid, 3, 2));
        }
    }
}
