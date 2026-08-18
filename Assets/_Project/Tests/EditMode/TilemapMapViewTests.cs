using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Battle.Movement;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // tilemap-view-backend unit 1 — Tilemap 페인트 위치와 BoardSpace(unit 0) 변환이
    // 같은 셀↔월드 정합을 공유함을 못 박는다. 이후 unit 에서 어긋나면 여기가 먼저 빨개진다.
    public class TilemapMapViewTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            // BoardSpace 리셋 불필요 — 안전 idle 모드 없음, 각 테스트가 자체 Configure 로 시작 (unit 3).
            if (_root != null) Object.DestroyImmediate(_root);
        }

        // cellLayout/cellSize/회전은 설정하지 않는다 — Initialize→ConfigureGrid 가 소유한다.
        // 여기서 미리 잡으면 "뷰가 정말 그리드를 구성하는가"를 테스트가 가려버린다.
        private TilemapMapView CreateView(out Tilemap ground, Vector3 position)
        {
            _root = new GameObject("TilemapBoardTest");
            _root.transform.position = position;
            var grid = _root.AddComponent<Grid>();

            ground = CreateTilemapChild("Ground");
            var overlay = CreateTilemapChild("Overlay");

            var view = _root.AddComponent<TilemapMapView>();
            SetField(view, "grid", grid);
            SetField(view, "groundTilemap", ground);
            SetField(view, "overlayTilemap", overlay);
            return view;
        }

        private Tilemap CreateTilemapChild(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            return tilemap;
        }

        private static void SetField(object obj, string name, object value)
        {
            var field = obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"field '{name}' not found on {obj.GetType().Name}");
            field.SetValue(obj, value);
        }

        private static int CountPaintedCells(Tilemap tilemap, int w, int h)
        {
            var tiles = tilemap.GetTilesBlock(new BoundsInt(0, 0, 0, w, h, 1));
            int n = 0;
            for (int i = 0; i < tiles.Length; i++)
                if (tiles[i] != null) n++;
            return n;
        }

        private static GeneratedMap BuildMap(int w, int h)
        {
            var map = new GeneratedMap
            {
                gridSize = new int2(w, h),
                tiles = new NativeArray<MapTileType>(w * h, Allocator.Persistent),
                spawns = new NativeArray<int2>(1, Allocator.Persistent),
                goal = new int2(w - 1, h - 1),
                seed = 1,
            };
            map.spawns[0] = new int2(0, 0);
            return map;
        }

        // --- 정합 고정: Tilemap.GetCellCenterWorld == BoardSpace.ToView(셀 중심), 3축 전부 ---

        [Test]
        public void PaintPositions_MatchBoardSpace_TileSize2()
            => AssertPaintMatchesBoardSpace(2f);

        [Test]
        public void PaintPositions_MatchBoardSpace_TileSize1()
            => AssertPaintMatchesBoardSpace(1f);

        private void AssertPaintMatchesBoardSpace(float tileSize)
        {
            var simOrigin = new float3(2f, 0f, -3f);
            var view = CreateView(out var ground, new Vector3(0.7f, -0.2f, 0f));
            var tileSet = ScriptableObject.CreateInstance<TileSetData>();

            var map = BuildMap(5, 4);
            try
            {
                // Initialize 가 Grid cellLayout/cellSize/회전을 설정한다 (정합의 출발점).
                view.Initialize(map, tileSize, tileSet);
                BoardSpace.Configure(simOrigin, tileSize, view.Grid);

                foreach (var cell in new[] { new int2(0, 0), new int2(1, 0), new int2(0, 1), new int2(4, 3), new int2(2, 1) })
                {
                    float3 simCenter = GridMath.CellToWorldCenter(cell, tileSize, simOrigin.y, simOrigin);
                    float3 viewCenter = BoardSpace.ToView(simCenter);
                    float3 tileCenter = ground.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
                    // **3축 전부** 일치해야 한다. Tilemap 의 tileAnchor z 를 0 으로 두는 것이
                    // ConfigureGrid 의 계약이라(z 오프셋 없음), 화면 평면만 비교하면 누군가
                    // anchor 를 (0.5,0.5,0.5) 로 바꿔도 초록으로 통과해 보드가 조용히 어긋난다.
                    Assert.Less(math.distance(tileCenter, viewCenter), 1e-3f,
                        $"cell {cell} (tileSize {tileSize}): tilemap {tileCenter}, boardspace {viewCenter}");
                }
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(tileSet);
            }
        }

        // --- map-diorama-stage unit 3: 바닥 페인팅 은퇴 pin + Clear 재진입 무잔상 ---

        [Test]
        public void Initialize_PaintsNoGroundTiles_RetirementPinned()
        {
            // 바닥 비주얼은 스테이지 프리팹 소유 — Initialize 가 ground 에 무엇이든 칠하면
            // 은퇴한 PaintGround 가 되살아난 회귀다 (디오라마 바닥과 z-fight).
            var view = CreateView(out var ground, Vector3.zero);
            var tileSet = ScriptableObject.CreateInstance<TileSetData>();
            var tile = ScriptableObject.CreateInstance<Tile>();
            tileSet.walkTile = tile;
            tileSet.placeTile = tile;
            tileSet.envTile = tile;
            tileSet.decoTile = tile;

            var map = BuildMap(4, 4);
            try
            {
                view.Initialize(map, 1f, tileSet);
                Assert.AreEqual(0, CountPaintedCells(ground, 4, 4), "바닥 페인팅은 은퇴했다 — 0셀");

                view.Clear();
                view.Initialize(map, 1f, tileSet);   // 재진입 무예외 + 여전히 0
                Assert.AreEqual(0, CountPaintedCells(ground, 4, 4), "재진입 후에도 0셀");
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(tile);
                Object.DestroyImmediate(tileSet);
            }
        }

        // --- map-diorama-stage unit 2/3 (critic C-1·M-3): 격자 정렬의 유일 writer 가드 ---

        [Test]
        public void AlignGridTo_PlacesCellZeroMinCorner_AtGivenWorldPosition()
        {
            var view = CreateView(out var ground, new Vector3(5f, 1f, -2f));
            var tileSet = ScriptableObject.CreateInstance<TileSetData>();
            var map = BuildMap(5, 4);
            try
            {
                view.Initialize(map, 1f, tileSet);
                // 스테이지 gridOriginLocal(월드) 상당의 임의 비0 좌표 — 정렬 후 셀 (0,0) 최소
                // 모서리가 정확히 그 자리여야 한다 (C-1: 프랍-논리 정렬의 자동 회귀망).
                var target = new Vector3(3f, 0.02f, -2f);
                view.AlignGridTo(target);

                Vector3 cellZeroCorner = ground.CellToWorld(new Vector3Int(0, 0, 0));
                Assert.Less(Vector3.Distance(cellZeroCorner, target), 1e-4f,
                    $"cell(0,0) corner {cellZeroCorner} != align target {target}");

                // BoardSpace 정합도 정렬을 따라온다. sim 은 **정수 = 셀 중심** 계약이라
                // 셀 (0,0)의 sim 중심은 (0,0) — GridMath 헬퍼로 규약을 코드에서 가져온다.
                BoardSpace.Configure(new float3(0f, 0f, 0f), 1f, view.Grid);
                float3 simCenter = GridMath.CellToWorldCenter(new int2(0, 0), 1f, 0f, float3.zero);
                float3 viewCenter = BoardSpace.ToView(simCenter);
                Vector3 tileCenter = ground.GetCellCenterWorld(new Vector3Int(0, 0, 0));
                Assert.Less(math.distance((float3)tileCenter, viewCenter), 1e-3f);
            }
            finally
            {
                map.Dispose();
                Object.DestroyImmediate(tileSet);
            }
        }

        // --- map-diorama-stage unit 4: 골 마커 뷰 훅 (구조물 프랍 경로의 후계) ---

        [Test]
        public void GoalMarker_AnchorUsesRendererCenter_CrackAndCollapseTintScale()
        {
            var root = new GameObject("GoalMarkerHost");
            try
            {
                var marker = root.AddComponent<Wassup.Core.GoalMarker>();
                var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mesh.transform.SetParent(root.transform, false);
                mesh.transform.localPosition = Vector3.up;   // 렌더러 중심 = +1Y

                Assert.That(marker.VisualAnchor().y, Is.EqualTo(1f).Within(1e-4f),
                    "앵커는 렌더러 바운즈 중심 (구 ResolveVisualAnchor 의미 승계)");

                // 균열 3단계 — 메쉬는 MPB 로 틴트 (공용 머티리얼 무오염 계약 승계).
                marker.SetCrackStage(3);
                var mpb = new MaterialPropertyBlock();
                mesh.GetComponent<Renderer>().GetPropertyBlock(mpb);
                Assert.That(mpb.GetColor("_BaseColor").r, Is.EqualTo(0.42f).Within(1e-3f));

                // 붕괴 — 60% 주저앉음, 중복 호출은 무해(스케일 1회만).
                Vector3 before = root.transform.localScale;
                marker.MarkCollapsed();
                marker.MarkCollapsed();
                Assert.That(root.transform.localScale.x, Is.EqualTo(before.x * 0.6f).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
