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
        public void LengthMismatch_NoIntent_NoCrash()
        {
            var tiles = MakeTiles();
            var shortMask = new NativeArray<byte>(2, Allocator.Temp);   // 4 != 2
            try
            {
                Assert.IsFalse(ObstaclePlacer.HasAuthoredMaskIntent(tiles, shortMask),
                    "길이 불일치 = 판정 불가 → 의도 없음 (IndexOutOfRange 크래시 방어)");
            }
            finally { tiles.Dispose(); shortMask.Dispose(); }
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

        // ── Track A 리뷰 MAJOR-1 — 커빙 실행 후 마스크 동기 회귀 가드 ────────────
        // DesignateDeco(Place→Deco 변이) 뒤 RederivePlaceMask 가 빠지거나 어긋나면
        // "장식물 위 배치 가능"(Deco 셀 mask=1 잔존)이라는 플레이어 가시 회귀가 된다.

        [Test]
        public void Curving_ThenRederive_MaskStaysInSyncWithTiles()
        {
            const int w = 8, h = 4, n = w * h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Temp);
            var mask = new NativeArray<byte>(n, Allocator.Temp);
            try
            {
                for (int i = 0; i < n; i++) tiles[i] = MapTileType.Place;
                for (int x = 0; x < w; x++) tiles[2 * w + x] = MapTileType.Walk;   // 복도
                for (int i = 0; i < n; i++) mask[i] = (byte)(tiles[i] == MapTileType.Place ? 1 : 0);

                var rng = Unity.Mathematics.Random.CreateFromIndex(7u);
                ObstaclePlacer.DesignateDeco(ref rng, tiles, new Unity.Mathematics.int2(w, h), 0.5f);
                ObstaclePlacer.RederivePlaceMask(tiles, mask);

                bool anyDeco = false;
                for (int i = 0; i < n; i++)
                {
                    if (tiles[i] == MapTileType.Deco) anyDeco = true;
                    Assert.AreEqual(tiles[i] == MapTileType.Place ? 1 : 0, mask[i],
                        $"mask[{i}] — 커빙 후 파생 동기 (Deco 셀 mask=1 잔존 금지)");
                }
                Assert.IsTrue(anyDeco, "keepFraction 0.5 커빙이 실제로 Deco 를 만들었어야 테스트가 유효");
            }
            finally { tiles.Dispose(); mask.Dispose(); }
        }

        [Test]
        public void Rederive_LengthMismatchOrUncreated_IsNoOp()
        {
            var tiles = MakeTiles();
            var shortMask = new NativeArray<byte>(2, Allocator.Temp);
            try
            {
                shortMask[0] = 9; shortMask[1] = 9;
                Assert.DoesNotThrow(() => ObstaclePlacer.RederivePlaceMask(tiles, shortMask),
                    "길이 불일치 = no-op (HasAuthoredMaskIntent 가드와 대칭)");
                Assert.AreEqual(9, shortMask[0], "no-op — 쓰기 없음");
                Assert.DoesNotThrow(() => ObstaclePlacer.RederivePlaceMask(tiles, default));
            }
            finally { tiles.Dispose(); shortMask.Dispose(); }
        }
    }
}
