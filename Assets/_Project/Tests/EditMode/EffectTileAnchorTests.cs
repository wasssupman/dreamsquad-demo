using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // first-session-tutorial unit 26 — TilemapMapView 의 효과 타일 셀 부기.
    //
    // 튜토리얼 마커가 가리킬 좌표의 출처라, 여기가 어긋나면 **허공을 가리키거나 안내가 조용히
    // 생략된다**. 프로필 토큰 쪽(TutorialProgressTests)은 이 경로를 전혀 보지 않는다.
    //
    // 특히 `overlayTile` 이 없는 EffectTileData(= null 페인트)를 목록에서 빼는 분기가 없으면
    // 아무것도 안 보이는 셀을 마커가 지목하게 된다 — 리뷰가 지적한 회귀 시나리오다.
    public class EffectTileAnchorTests
    {
        private GameObject _root;
        private TilemapMapView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("EffectTileAnchorTest", typeof(Grid));
            _view = _root.AddComponent<TilemapMapView>();
            // grid 는 SetEffectTile 의 유일한 전제다(_effectTilemap 은 EnsureEffectTilemap 이 만든다).
            typeof(TilemapMapView)
                .GetField("grid", System.Reflection.BindingFlags.Instance |
                                  System.Reflection.BindingFlags.NonPublic)
                .SetValue(_view, _root.GetComponent<Grid>());
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private static UnityEngine.Tilemaps.TileBase MakeTile() =>
            ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();

        [Test]
        public void PaintedCells_AreRecorded_AndDeduplicated()
        {
            var tile = MakeTile();

            _view.SetEffectTile(new Vector2Int(2, 3), tile);
            _view.SetEffectTile(new Vector2Int(5, 1), tile);
            Assert.AreEqual(2, _view.EffectTileCount);

            // 같은 셀 재페인트는 덮어쓰기다(BattleBridge._effectTilesByCell 과 동형) — 중복 금지.
            _view.SetEffectTile(new Vector2Int(2, 3), tile);
            Assert.AreEqual(2, _view.EffectTileCount, "같은 셀을 다시 칠해도 목록은 늘지 않는다");
        }

        [Test]
        public void NullTile_RemovesTheCell()
        {
            var tile = MakeTile();
            _view.SetEffectTile(new Vector2Int(4, 4), tile);
            Assert.AreEqual(1, _view.EffectTileCount);

            // overlayTile 이 비어 있는 EffectTileData 는 null 페인트가 된다. 목록에 남기면
            // 마커가 **아무것도 안 보이는 셀**을 가리킨다.
            _view.SetEffectTile(new Vector2Int(4, 4), null);
            Assert.AreEqual(0, _view.EffectTileCount,
                "빈 타일로 칠한 셀은 목록에서 빠져야 한다 — 마커가 허공을 가리키지 않도록");
        }

        [Test]
        public void Clear_DropsAllCells()
        {
            var tile = MakeTile();
            _view.SetEffectTile(new Vector2Int(1, 1), tile);
            _view.SetEffectTile(new Vector2Int(2, 2), tile);

            _view.Clear(); // 맵 리빌드 경계 — 이전 판의 셀이 남으면 다음 판 마커가 어긋난다

            Assert.AreEqual(0, _view.EffectTileCount);
            Assert.IsFalse(_view.TryGetEffectTileAnchor(0, out _));
        }

        [Test]
        public void TryGetEffectTileAnchor_RespectsBounds()
        {
            Assert.IsFalse(_view.TryGetEffectTileAnchor(0, out _), "빈 목록");

            _view.SetEffectTile(new Vector2Int(3, 7), MakeTile());

            Assert.IsTrue(_view.TryGetEffectTileAnchor(0, out Vector3 world));
            Assert.AreEqual(_view.CellCenterToWorld(3, 7), world,
                "앵커는 조회 시점에 CellCenterToWorld 로 푼다(굳은 좌표를 들고 있지 않는다)");
            Assert.IsFalse(_view.TryGetEffectTileAnchor(1, out _), "범위 밖");
            Assert.IsFalse(_view.TryGetEffectTileAnchor(-1, out _), "음수 인덱스");
        }
    }
}
