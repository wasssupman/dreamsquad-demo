using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Tests.EditMode.MapGrid
{
    // bonus-wave-pull unit 1 — 포탈 칸 저작의 **양성 조건 3개**를 고정한다.
    // 금지 목록이 아니라 양성 조건인 이유: 포탈은 보드 한가운데 열리므로 「벽이 아니다」
    // 만으로는 부족하다. 격리 칸에 저작하면 그 적들이 골에 영영 못 간다.
    public class BonusSpawnAuthoringRulesTests
    {
        private const int W = 6;
        private const int H = 4;

        // (0,2)~(5,2) 한 줄만 통행 가능한 작은 맵. 골은 (5,2).
        private static MapTileType[] Corridor()
        {
            var tiles = new MapTileType[W * H];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = MapTileType.Place;
            for (int x = 0; x < W; x++) tiles[2 * W + x] = MapTileType.Walk;
            return tiles;
        }

        private static readonly Vector2Int[] Goals = { new Vector2Int(5, 2) };

        private static List<string> Run(params Vector2Int[] cells)
        {
            var errors = new List<string>();
            BonusSpawnAuthoringRules.Validate(cells, W, H, Corridor(), Goals, errors);
            return errors;
        }

        [Test]
        public void 미저작은_에러가_아니다()
        {
            var errors = new List<string>();
            BonusSpawnAuthoringRules.Validate(null, W, H, Corridor(), Goals, errors);
            Assert.IsEmpty(errors);
            Assert.IsEmpty(Run());
        }

        [Test]
        public void 정상_두_칸은_통과한다()
        {
            Assert.IsEmpty(Run(new Vector2Int(1, 2), new Vector2Int(3, 2)));
        }

        [Test]
        public void 걸을_수_없는_칸은_에러다()
        {
            // (1,0) 은 Place = 벽.
            var errors = Run(new Vector2Int(1, 0), new Vector2Int(3, 2));
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("걸을 수 없는", string.Join("\n", errors));
        }

        [Test]
        public void 골에_도달_못하는_칸은_에러다()
        {
            // 복도에서 떨어진 고립 Walk 칸을 하나 만든다.
            var tiles = Corridor();
            tiles[0 * W + 0] = MapTileType.Walk;   // (0,0) — 복도와 안 붙어 있다
            var errors = new List<string>();
            BonusSpawnAuthoringRules.Validate(
                new[] { new Vector2Int(0, 0), new Vector2Int(3, 2) },
                W, H, tiles, Goals, errors);
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("도달할 수 없다", string.Join("\n", errors));
        }

        // ★회귀 가드 — 규칙을 `!= Place` 로 느슨하게 쓰면 여기가 통과해버린다.
        // Duel 의 중앙 열이 정확히 Env 기둥이라 실맵에서 바로 밟는 함정이다.
        [Test]
        public void Env_칸도_벽이므로_에러다()
        {
            var tiles = Corridor();
            tiles[2 * W + 1] = MapTileType.Env;   // 복도 한가운데를 Env 로
            var errors = new List<string>();
            BonusSpawnAuthoringRules.Validate(
                new[] { new Vector2Int(1, 2), new Vector2Int(3, 2) },
                W, H, tiles, Goals, errors);
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("걸을 수 없는", string.Join("\n", errors));
        }

        [Test]
        public void 같은_칸을_두_번_찍으면_에러다()
        {
            var errors = Run(new Vector2Int(3, 2), new Vector2Int(3, 2));
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("두 번", string.Join("\n", errors));
        }

        [Test]
        public void 개수가_계약과_다르면_에러다()
        {
            var errors = Run(new Vector2Int(1, 2));
            Assert.IsNotEmpty(errors);
            StringAssert.Contains($"{BonusSpawnAuthoringRules.RequiredPortalCount}개",
                string.Join("\n", errors));
        }

        [Test]
        public void 격자_밖은_에러다()
        {
            var errors = Run(new Vector2Int(99, 2), new Vector2Int(3, 2));
            Assert.IsNotEmpty(errors);
            StringAssert.Contains("격자 밖", string.Join("\n", errors));
        }
    }
}
