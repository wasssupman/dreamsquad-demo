using NUnit.Framework;
using Unity.Collections;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // placement-mask unit 1 — 커빙 skip 조건(HasAuthoredMaskIntent) 회귀 방지.
    // "마스크 ≠ 파생값(tiles==Place)" 저작 의도 감지가 커빙 skip 을 결정한다.
    public class ObstaclePlacerMaskIntentTests
    {
        static NativeArray<MapTileType> MakeTiles()
        {
            // 4셀: Place, Walk, Place, Deco
            var tiles = new NativeArray<MapTileType>(4, Allocator.Temp);
            tiles[0] = MapTileType.Place;
            tiles[1] = MapTileType.Walk;
            tiles[2] = MapTileType.Place;
            tiles[3] = MapTileType.Deco;
            return tiles;
        }

        [Test]
        public void DerivedMask_NoIntent()
        {
            var tiles = MakeTiles();
            var mask = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                for (int i = 0; i < 4; i++)
                    mask[i] = (byte)(tiles[i] == MapTileType.Place ? 1 : 0);
                Assert.IsFalse(ObstaclePlacer.HasAuthoredMaskIntent(tiles, mask),
                    "파생 동일 마스크 = 저작 의도 없음 → 커빙 유지");
            }
            finally { tiles.Dispose(); mask.Dispose(); }
        }

        [Test]
        public void SingleDifferingCell_HasIntent()
        {
            var tiles = MakeTiles();
            var mask = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                for (int i = 0; i < 4; i++)
                    mask[i] = (byte)(tiles[i] == MapTileType.Place ? 1 : 0);
                mask[1] = 1;   // Walk 셀 배치 허용 — 상이 셀 1개.
                Assert.IsTrue(ObstaclePlacer.HasAuthoredMaskIntent(tiles, mask),
                    "상이 셀 1개 = 수동 배치판 → 커빙 skip");
            }
            finally { tiles.Dispose(); mask.Dispose(); }
        }

        [Test]
        public void UncreatedMask_NoIntent()
        {
            var tiles = MakeTiles();
            try
            {
                Assert.IsFalse(ObstaclePlacer.HasAuthoredMaskIntent(tiles, default),
                    "마스크 미생성 = 저작 의도 없음");
            }
            finally { tiles.Dispose(); }
        }

        [Test]
        public void NonBinaryMaskValue_ComparedAsBool()
        {
            var tiles = MakeTiles();
            var mask = new NativeArray<byte>(4, Allocator.Temp);
            try
            {
                for (int i = 0; i < 4; i++)
                    mask[i] = (byte)(tiles[i] == MapTileType.Place ? 1 : 0);
                mask[0] = 2;   // Place 셀에 비정규값 — bool 비교라 파생과 동치.
                Assert.IsFalse(ObstaclePlacer.HasAuthoredMaskIntent(tiles, mask),
                    "비정규값(≠0)도 '배치 가능'으로 접혀 파생과 동치");
            }
            finally { tiles.Dispose(); mask.Dispose(); }
        }
    }
}
