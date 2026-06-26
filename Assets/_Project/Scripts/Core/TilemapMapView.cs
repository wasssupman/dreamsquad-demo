using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Core
{
    // tilemap-view-backend unit 1 — GeneratedMap 을 Unity Tilemap 에 칠하는 write-only 뷰.
    // source of truth 는 MapDocument/GeneratedMap. 이 클래스는 절대 읽히지 않는다 (GetTile 호출 0건).
    // 셀↔월드 정합의 권위는 Grid (BoardSpace 와 동일). 정합은 TilemapMapViewTests 가 못 박는다.
    public class TilemapMapView : MonoBehaviour
    {
        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap overlayTilemap;
        // tilemap-real-shadows unit 0 — 그림자 receive 머티리얼(Wassup/Tile_ShadowReceive). 비면 기존 머티리얼 유지.
        [SerializeField] private Material groundShadowMaterial;

        private TileSetData _tileSet;
        private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();
        private readonly HashSet<Vector2Int> _hoverCells = new();
        // tilemap-world-surround unit 2 — 배경 프랍 호스트(Deco) 판정용 셀/리전 메타 + 프랍 인스턴스 루트.
        private BoardVisualPlan _visualPlan;
        private Transform _backgroundPropsRoot;
        // tilemap-world-surround unit 4 — 외곽 링 원경 프랍 인스턴스 루트.
        private Transform _ringPropsRoot;

        public Grid Grid => grid;
        public BoardVisualPlan VisualPlan => _visualPlan;

        // 평면 빌보드 프랍이 바닥 타일과 z-fight 나지 않도록 살짝 띄우는 world +Y 오프셋.
        private const float PropGroundLift = 0.02f;

        // BattleBridge 맵 빌드 시 호출 (unit 2). Grid cellLayout/cellSize 를 모드에 맞춰 설정한 뒤
        // 전체 셀을 일괄 페인트한다. 재진입(RebuildDraftMap) 안전 — Clear 선행.
        public void Initialize(in GeneratedMap map, float tileSize, TileSetData tileSet, BoardViewMode mode,
            bool realShadows = false)
        {
            Clear();
            _tileSet = tileSet;
            ConfigureGrid(tileSize, tileSet, mode, realShadows);
            PaintGround(in map);
            PaintMarkers(in map);
            CenterBoardAtWorldOrigin(in map);
            // 배경 프랍 배치(Deco/Env 호스트) 판정에 쓰는 셀/리전/anchor 메타. Legacy MapView 와 동일 빌더.
            _visualPlan = map.IsCreated ? BoardVisualPlanBuilder.Build(map, map.seed) : null;
        }

        // tilted-billboard unit 1 — 보드 중앙을 월드 원점(X·Z=0)에 맞춘다. XZ 바닥이라 수평면(X,Z)만 정렬,
        // Y(바닥 높이)는 보존. Tilemap 모드는 sim origin=0, 월드 배치는 grid.transform 권위라 view 전용 변경 — sim 무영향.
        // ToView/ToSim/RaycastPlane 모두 grid 기준 live 라 정합 유지. 맵 크기 달라져도 재계산·idempotent.
        private void CenterBoardAtWorldOrigin(in GeneratedMap map)
        {
            if (grid == null || !map.IsCreated) return;
            // 보드 양 끝 셀의 월드 코너 중점 = 현재 보드 중심(rect/iso 모두 affine 이라 동일).
            Vector3 min = grid.CellToWorld(new Vector3Int(0, 0, 0));
            Vector3 max = grid.CellToWorld(new Vector3Int(map.gridSize.x, map.gridSize.y, 0));
            Vector3 center = (min + max) * 0.5f;
            grid.transform.position -= new Vector3(center.x, 0f, center.z);
        }

        public void Clear()
        {
            StopAllFlashes();
            _hoverCells.Clear();
            if (groundTilemap != null) groundTilemap.ClearAllTiles();
            if (overlayTilemap != null) overlayTilemap.ClearAllTiles();
            if (_backgroundPropsRoot != null) { SafeDestroy(_backgroundPropsRoot.gameObject); _backgroundPropsRoot = null; }
            if (_ringPropsRoot != null) { SafeDestroy(_ringPropsRoot.gameObject); _ringPropsRoot = null; }
        }

        private void ConfigureGrid(float tileSize, TileSetData tileSet, BoardViewMode mode, bool realShadows)
        {
            if (grid == null) return;
            if (mode == BoardViewMode.TilemapIso)
            {
                grid.cellLayout = GridLayout.CellLayout.Isometric;
                grid.cellSize = tileSet != null ? tileSet.isoCellSize : new Vector3(1f, 0.5f, 1f);
            }
            else // TilemapRect (Legacy3D 는 이 뷰를 쓰지 않는다)
            {
                grid.cellLayout = GridLayout.CellLayout.Rectangle;
                grid.cellSize = new Vector3(tileSize, tileSize, 1f);
            }

            // tilted-billboard — 타일맵을 XZ 바닥에 눕힌다(퍼스펙티브 3D 룩). grid 로컬 XY → 월드 XZ.
            // BoardSpace.ToView/ToSim/RaycastPlane 가 모두 grid 기준이라 회전을 자동 추종한다.
            grid.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // 셀 중심 anchor — GetCellCenterWorld 정합의 전제 (정합 테스트와 일치).
            var anchor = new Vector3(0.5f, 0.5f, 0f);
            if (groundTilemap != null) groundTilemap.tileAnchor = anchor;
            if (overlayTilemap != null) overlayTilemap.tileAnchor = anchor;

            // unit 4 — "보드 레이어 < 유닛 레이어" 1규칙. 유닛/VFX 는 BoardSortOrder(양수) 사용 → 보드는 음수.
            SetRendererSorting(groundTilemap, -20);
            SetRendererSorting(overlayTilemap, -10);

            // tilemap-real-shadows — 진짜 그림자 모드일 때만 바닥 receive 머티리얼 적용. 아니면 기존 룩 유지.
            // 타일/맵은 RECEIVE 만(유닛·프랍만 CAST). receive 셰이더엔 ShadowCaster 패스가 없어 이미
            // cast 못 하지만, 의도 못박기 위해 두 타일맵 모두 cast off 를 명시한다.
            SetRendererCastShadows(groundTilemap, false);
            SetRendererCastShadows(overlayTilemap, false);
            if (realShadows && groundShadowMaterial != null && groundTilemap != null)
            {
                var tmr = groundTilemap.GetComponent<TilemapRenderer>();
                if (tmr != null)
                {
                    tmr.sharedMaterial = groundShadowMaterial;
                    tmr.receiveShadows = true;
                }
            }
        }

        // unit 1 — 페인트된 ground 영역의 월드 bounds (카메라 프레이밍용; iso 마름모도 실측).
        public bool TryGetBoardWorldBounds(out Bounds bounds)
        {
            bounds = default;
            if (groundTilemap == null) return false;
            var r = groundTilemap.GetComponent<TilemapRenderer>();
            if (r == null || r.bounds.size == Vector3.zero) return false;
            bounds = r.bounds;
            return true;
        }

        private static void SetRendererSorting(Tilemap tilemap, int order)
        {
            if (tilemap == null) return;
            var r = tilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.sortingOrder = order;
        }

        // tilemap-real-shadows — 타일/맵은 그림자를 드리우지 않는다(유닛·프랍만 cast).
        private static void SetRendererCastShadows(Tilemap tilemap, bool cast)
        {
            if (tilemap == null) return;
            var r = tilemap.GetComponent<TilemapRenderer>();
            if (r != null)
                r.shadowCastingMode = cast
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void PaintGround(in GeneratedMap map)
        {
            if (groundTilemap == null || !map.IsCreated) return;

            int w = map.gridSize.x;
            int h = map.gridSize.y;
            var bounds = new BoundsInt(0, 0, 0, w, h, 1);
            var tiles = new TileBase[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var type = map.TileAt(new int2(x, y));
                tiles[y * w + x] = _tileSet != null ? _tileSet.GroundTileFor(type) : null;
            }
            groundTilemap.SetTilesBlock(bounds, tiles);
            PaintSurroundRing(w, h);
        }

        // tilemap-world-surround unit 3 — 플레이 보드 밖 외곽 링을 터레인 타일로 칠하고 톤다운 틴트.
        // sim 무관(순수 시각). 바깥쪽으로 갈수록 어둡게(surroundEdgeFade) 해 어두운 배경에 자연 블렌딩.
        private void PaintSurroundRing(int w, int h)
        {
            if (groundTilemap == null || _tileSet == null) return;
            int R = _tileSet.ringRadius;
            // 원경 링도 플레이 영역과 같은 풀 타일(decoTile)을 쓴다. terrainTile 지정 시 그것 우선.
            var tile = _tileSet.TerrainTileOrFallback;
            if (R <= 0 || tile == null) return;

            for (int y = -R; y < h + R; y++)
            for (int x = -R; x < w + R; x++)
            {
                if (x >= 0 && x < w && y >= 0 && y < h) continue; // 플레이 영역 스킵 (원색 유지)
                var pos = new Vector3Int(x, y, 0);
                groundTilemap.SetTile(pos, tile);
                groundTilemap.SetTileFlags(pos, TileFlags.None); // per-cell color 허용
                int ringDist = RingDistance(x, y, w, h);          // 1..R (보드 경계로부터)
                float baseT = R > 1 ? Mathf.Clamp01((ringDist - 1) / (float)(R - 1)) : 0f;
                // 노이즈로 그라데이션을 교란해 동심 사각형 banding 을 유기적으로 깬다(+1000 오프셋: Perlin 음수좌표 회피).
                float n = Mathf.PerlinNoise((x + 1000) * _tileSet.surroundNoiseScale, (y + 1000) * _tileSet.surroundNoiseScale);
                float t = Mathf.Clamp01(baseT + (n - 0.5f) * _tileSet.surroundNoiseAmount);
                // 안쪽(보드 경계, t=0)=플레이 영역 풀 타일 원색(흰색), 바깥(t=1)=surroundFarColor 로 그라데이션.
                Color c = Color.Lerp(Color.white, _tileSet.surroundFarColor, t);
                groundTilemap.SetColor(pos, new Color(c.r, c.g, c.b, 1f));
            }
        }

        // 플레이 보드(0..w, 0..h) 경계로부터의 링 레이어 거리(Chebyshev, 1..R).
        private static int RingDistance(int x, int y, int w, int h)
        {
            int dx = x < 0 ? -x : (x >= w ? x - (w - 1) : 0);
            int dy = y < 0 ? -y : (y >= h ? y - (h - 1) : 0);
            return Mathf.Max(dx, dy);
        }

        // 원경 링 가중치: distantRingWeight 가 지정(>=0)되면 그것, 아니면 placementWeight.
        private static float RingWeight(Wassup.Data.PropData p)
            => p.distantRingWeight >= 0f ? p.distantRingWeight : Mathf.Max(0, p.placementWeight);

        private void PaintMarkers(in GeneratedMap map)
        {
            if (overlayTilemap == null || _tileSet == null || !map.IsCreated) return;

            if (_tileSet.goalTile != null)
                overlayTilemap.SetTile(ToCell(map.goal), _tileSet.goalTile);

            if (_tileSet.spawnTile != null && map.spawns.IsCreated)
            {
                for (int i = 0; i < map.spawns.Length; i++)
                    overlayTilemap.SetTile(ToCell(map.spawns[i]), _tileSet.spawnTile);
            }
        }

        // --- 배치 피드백 (MapView.SetPlacementHover/FlashTileReject/ClearPlacementHover 대응) ---

        public void SetPlacementHover(Vector2Int cell, bool valid)
        {
            if (overlayTilemap == null || _tileSet == null) return;
            StopFlash(cell);
            overlayTilemap.SetTile(ToCell(cell), valid ? _tileSet.hoverTile : _tileSet.rejectTile);
            _hoverCells.Add(cell);
        }

        public void ClearPlacementHover(Vector2Int cell)
        {
            if (!_hoverCells.Remove(cell)) return;
            if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(cell), null);
        }

        public void ClearPlacementHover()
        {
            if (overlayTilemap != null)
                foreach (var cell in _hoverCells)
                    overlayTilemap.SetTile(ToCell(cell), null);
            _hoverCells.Clear();
        }

        public void FlashTileReject(Vector2Int cell)
        {
            if (overlayTilemap == null || _tileSet == null || _tileSet.rejectTile == null) return;
            ClearPlacementHover(cell);
            StopFlash(cell);
            _activeFlashes[cell] = StartCoroutine(FlashCoroutine(cell));
        }

        private IEnumerator FlashCoroutine(Vector2Int cell)
        {
            var pos = ToCell(cell);
            overlayTilemap.SetTile(pos, _tileSet.rejectTile);
            yield return new WaitForSeconds(0.2f);
            if (overlayTilemap != null) overlayTilemap.SetTile(pos, null);
            _activeFlashes.Remove(cell);
        }

        private void StopFlash(Vector2Int cell)
        {
            if (_activeFlashes.TryGetValue(cell, out var c) && c != null) StopCoroutine(c);
            _activeFlashes.Remove(cell);
        }

        private void StopAllFlashes()
        {
            foreach (var c in _activeFlashes.Values)
                if (c != null) StopCoroutine(c);
            _activeFlashes.Clear();
        }

        // --- 배경 프랍 (tilemap-world-surround unit 2) ---

        // Deco/Env 셀에 배경 프랍 프리팹을 배치한다. 위치는 grid 권위(BoardSpace.ToView 와 동일 수식) —
        // Legacy 의 raw (x,y)*tileSize 와 달리 90° 회전·센터링을 자동 반영. 정렬/마커/틴트는 MapView 헬퍼 재사용.
        public void InstantiateBackgroundProps(
            BoardVisualPlan plan,
            MapThemeData theme,
            IReadOnlyList<PropPlacement> placements,
            bool castShadows)
        {
            if (_backgroundPropsRoot != null) { SafeDestroy(_backgroundPropsRoot.gameObject); _backgroundPropsRoot = null; }
            if (grid == null || plan == null || theme == null || theme.tileProps == null ||
                placements == null || placements.Count == 0)
                return;

            var propsRoot = new GameObject("BackgroundProps");
            _backgroundPropsRoot = propsRoot.transform;
            _backgroundPropsRoot.SetParent(transform, false);

            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement.propIndex < 0 || placement.propIndex >= theme.tileProps.Length) continue;
                var prop = theme.tileProps[placement.propIndex];
                if (prop == null || prop.prefab == null) continue;

                float centerX = placement.x + (placement.width - 1) * 0.5f;
                float centerY = placement.y + (placement.height - 1) * 0.5f;
                var instance = Instantiate(prop.prefab, _backgroundPropsRoot);
                instance.name = $"{prop.name}_{placement.x}_{placement.y}";
                // world 좌표/회전으로 설정 → 부모(grid) 90° 회전 비상속. PropBillboard 가 LateUpdate 로 facing override.
                instance.transform.position = CellCenterToWorld(centerX, centerY);
                instance.transform.rotation = Quaternion.Euler(0f, placement.rotationYaw, 0f);
                instance.transform.localScale = Vector3.one * placement.scale;
                MapView.ApplyPropSorting(instance, prop, placement, plan);
                MapView.DisablePropDebugMarkers(instance);
                // unit 9 — 데스크톱=real cast / 모바일(real off)=발밑 blob 폴백(캐릭터와 대칭).
                if (castShadows) SetPropCastShadows(instance);
                else AttachPropBlob(instance, prop);
                if (theme.propGlobalTint != Color.white)
                    MapView.ApplyPropGlobalTint(instance, theme.propGlobalTint);
            }
        }

        // tilemap-world-surround unit 4 — 외곽 터레인 링 셀에 원경 프랍을 저밀도로 흩뿌린다.
        // VisualPlan(sim 그리드) 밖이라 BackgroundPropPlacer 를 못 쓰는 별도 경량 scatter.
        // 바깥쪽 falloff 로 밀도 감소(가장자리 페이드), 원경이라 그림자 OFF 기본, 꽃 등은 제외.
        public void InstantiateRingProps(MapThemeData theme, int2 playableSize, int seed, bool castShadows, float densityScale = 1f)
        {
            if (_ringPropsRoot != null) { SafeDestroy(_ringPropsRoot.gameObject); _ringPropsRoot = null; }
            if (grid == null || _tileSet == null || theme == null || theme.tileProps == null) return;
            int R = _tileSet.ringRadius;
            float density = theme.ringPropDensity * Mathf.Clamp01(densityScale);
            if (R <= 0 || density <= 0f) return;
            int w = playableSize.x, h = playableSize.y;

            float totalW = 0f;
            for (int i = 0; i < theme.tileProps.Length; i++)
            {
                var p = theme.tileProps[i];
                if (p != null && p.prefab != null && !p.excludeFromDistantRing) totalW += RingWeight(p);
            }
            if (totalW <= 0f) return;

            // unit 8 — 틸트 나무가 플레이 영역을 덮지 않게, 플레이 셀(Walk/Place) 근처 링 셀은 비운다.
            int clearance = theme.ringPlayClearanceCells;

            var rng = Unity.Mathematics.Random.CreateFromIndex((uint)(seed ^ 0x246813) | 1u);
            var root = new GameObject("RingProps");
            _ringPropsRoot = root.transform;
            _ringPropsRoot.SetParent(transform, false);

            for (int y = -R; y < h + R; y++)
            for (int x = -R; x < w + R; x++)
            {
                if (x >= 0 && x < w && y >= 0 && y < h) continue;
                if (clearance > 0 && WouldOccludePlay(_visualPlan, x, y, clearance)) continue;
                int ringDist = RingDistance(x, y, w, h);
                float falloff = Mathf.Clamp01(1f - theme.ringPropFalloffPerCell * (ringDist - 1));
                if (rng.NextFloat() > density * falloff) continue;

                float roll = rng.NextFloat(0f, totalW);
                Wassup.Data.PropData prop = null;
                for (int i = 0; i < theme.tileProps.Length; i++)
                {
                    var p = theme.tileProps[i];
                    if (p == null || p.prefab == null || p.excludeFromDistantRing) continue;
                    roll -= RingWeight(p);
                    if (roll <= 0f) { prop = p; break; }
                }
                if (prop == null) continue;

                var inst = Instantiate(prop.prefab, _ringPropsRoot);
                inst.name = prop.name + "_ring_" + x + "_" + y;
                inst.transform.position = CellCenterToWorld(x, y);
                inst.transform.localScale = Vector3.one * (1f + rng.NextFloat(-prop.scaleJitter, prop.scaleJitter));
                MapView.DisablePropDebugMarkers(inst);
                if (castShadows) SetPropCastShadows(inst);
            }
        }

        // unit 8 (rev, unit 10 맥락) — 링 셀의 +y(틸트 누운 방향, 보드 안쪽)쪽 r 이내에 플레이 셀(Walk/Place)이
        // 있으면 true = 이 링이 플레이 영역 하단(-y)에 있어 틸트(+y 누움)로 플레이를 가리는 경우. 그 경우만 비운다.
        // 플레이의 상/좌/우 원경 링은 +y 로 누워도 플레이를 안 가리므로 허용(빽빽한 숲 유지).
        private static bool WouldOccludePlay(BoardVisualPlan plan, int cx, int cy, int r)
        {
            if (plan == null) return false;
            int w = plan.gridSize.x, h = plan.gridSize.y;
            for (int dy = 1; dy <= r; dy++) // +y(위, 보드 안쪽)쪽만 검사
            for (int dx = -r; dx <= r; dx++)
            {
                int x = cx + dx, y = cy + dy;
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                var z = plan.CellAt(new int2(x, y)).zoneType;
                if (z == BoardZoneType.Walk || z == BoardZoneType.Place) return true;
            }
            return false;
        }

        // grid 권위 cell→world. BoardSpace.ToView 와 동일 셀중심(+0.5) 수식. 바닥 z-fight 회피용 미세 +Y lift.
        public Vector3 CellCenterToWorld(float cellX, float cellY)
        {
            if (grid == null) return new Vector3(cellX, 0f, cellY);
            Vector3 world = grid.transform.TransformPoint(
                grid.CellToLocalInterpolated(new Vector3(cellX + 0.5f, cellY + 0.5f, 0f)));
            world.y += PropGroundLift;
            return world;
        }

        // tilemap-real-shadows — 근경 프랍은 실루엣 그림자 CAST(TwoSided). 평면 빌보드 alpha-clip 은 셰이더 책임.
        private static void SetPropCastShadows(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        }

        // unit 9 — real cast 가 꺼지는 모바일에서 근경 프랍 발밑 blob 폴백(캐릭터와 대칭).
        // blob 은 부모 lossyScale 보정으로 월드 크기 고정 → 프랍 크기 반영은 size *= visualScale.
        private static void AttachPropBlob(GameObject instance, PropData prop)
        {
            if (BattleBridge.BlobShadowSprite == null || prop == null) return;
            BlobShadow.Attach(
                instance.transform,
                BattleBridge.BlobShadowSprite,
                BattleBridge.BlobShadowSize * Mathf.Max(0.01f, prop.visualScale),
                BattleBridge.BlobShadowFootprint,
                BattleBridge.BlobShadowColor,
                BattleBridge.BlobShadowGroundY,
                BoardSortOrder.ShadowOrder);
        }

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        // GeneratedMap 셀 (x, y) → Tilemap cell (x, y, 0). 변환 헬퍼 단일 지점.
        private static Vector3Int ToCell(int2 cell) => new Vector3Int(cell.x, cell.y, 0);
        private static Vector3Int ToCell(Vector2Int cell) => new Vector3Int(cell.x, cell.y, 0);
    }
}
