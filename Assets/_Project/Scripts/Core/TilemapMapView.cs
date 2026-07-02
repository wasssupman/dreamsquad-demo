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
        // prop-placement-layer unit 1 — goal/spawn 구조물 프랍 루트. 부모(90°X)를 역회전 상쇄해 메쉬가 똑바로 선다.
        private Transform _structurePropsRoot;
        // effect-tiles unit 1 — 효과 타일 전용 런타임 타일맵. overlayTilemap 은 hover/reject 가
        // SetTile/null 로 덮어쓰므로 공유 금지. sorting -15 = ground(-20) 위 / overlay·hover(-10) 아래.
        // 런타임 생성 → 씬 SerializeField/저장 불필요.
        private Tilemap _effectTilemap;

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
            if (_effectTilemap != null) _effectTilemap.ClearAllTiles();
            if (_backgroundPropsRoot != null) { SafeDestroy(_backgroundPropsRoot.gameObject); _backgroundPropsRoot = null; }
            if (_ringPropsRoot != null) { SafeDestroy(_ringPropsRoot.gameObject); _ringPropsRoot = null; }
            if (_structurePropsRoot != null) { SafeDestroy(_structurePropsRoot.gameObject); _structurePropsRoot = null; }
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
            IReadOnlyList<PropPlacement> placements)
        {
            if (_backgroundPropsRoot != null) { SafeDestroy(_backgroundPropsRoot.gameObject); _backgroundPropsRoot = null; }
            if (grid == null || plan == null || theme == null || theme.playAreaProps == null ||
                placements == null || placements.Count == 0)
                return;

            var propsRoot = new GameObject("BackgroundProps");
            _backgroundPropsRoot = propsRoot.transform;
            _backgroundPropsRoot.SetParent(transform, false);
            // prop-upright-root unit 1 — 부모(90°X)를 상쇄해 프랍 저작 프레임을 upright 로.
            // +Y=월드 위. 위치는 transform.position(월드)로 세팅되므로 placement 무변경, 로컬 프레임만 upright.
            _backgroundPropsRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                if (placement.propIndex < 0 || placement.propIndex >= theme.playAreaProps.Length) continue;
                var prop = theme.playAreaProps[placement.propIndex]?.prop;
                if (prop == null || prop.prefab == null) continue;

                InstantiateProp(prop, placement, plan, theme, _backgroundPropsRoot);
            }
        }

        // prop-placement-layer unit 0 — 단일 프랍 인스턴스화(배경/구조물 공통 재사용 지점).
        // resolved PropData 를 받는다 — placement.propIndex 로 playAreaProps 를 재조회하지 않는다
        // (구조물 프랍은 playAreaProps 밖이라 이게 필수 계약).
        // rotation 은 부모(BackgroundProps/그 외 root, XZ 바닥 90°) 상속 — Euler(0,yaw,0) 강제 시
        // 부모 90° 를 무시해 visualOffset 이 월드 +y 로 적용되는 공중부양 버그. yaw 는 PropBillboard 가 override.
        private void InstantiateProp(PropData prop, PropPlacement placement,
                                     BoardVisualPlan plan, MapThemeData theme, Transform root)
        {
            float centerX = placement.x + (placement.width - 1) * 0.5f;
            float centerY = placement.y + (placement.height - 1) * 0.5f;
            var instance = Instantiate(prop.prefab, root);
            instance.name = $"{prop.name}_{placement.x}_{placement.y}";
            instance.transform.position = CellCenterToWorld(centerX, centerY);
            instance.transform.localScale = Vector3.one * placement.scale;
            MapView.ApplyPropSorting(instance, prop, placement, plan);
            MapView.DisablePropDebugMarkers(instance);
            // 프랍 그림자 = 프리팹 authored 블롭 (shadow-polish unit 6). 런타임 부착 없음 — 프리팹이 source of truth.
            if (theme.propGlobalTint != Color.white)
                MapView.ApplyPropGlobalTint(instance, theme.propGlobalTint);
        }

        // prop-placement-layer unit 1 — goal/spawn 셀에 3D 메쉬 구조물 프랍을 세운다.
        // 메쉬는 빌보드 아님 → 부모(XZ 바닥 90°X)를 역회전(-90°X)한 root 아래 두면 identity 로 똑바로 선다.
        // 구조물이 놓인 셀의 placeholder 마커 타일(overlay)은 제거. sim(GeneratedMap/FlowField) 무변경.
        public void InstantiateStructureProps(in GeneratedMap map, MapThemeData theme, BoardVisualPlan plan)
        {
            if (_structurePropsRoot != null) { SafeDestroy(_structurePropsRoot.gameObject); _structurePropsRoot = null; }
            if (grid == null || theme == null || !map.IsCreated) return;
            if (theme.goalStructureProp == null && theme.spawnStructureProp == null) return;

            var root = new GameObject("StructureProps");
            _structurePropsRoot = root.transform;
            _structurePropsRoot.SetParent(transform, false);
            // 부모(grid, 월드 90°X) 상쇄 → root 월드 업라이트. 메쉬 child 는 회전 없이 똑바로 선다.
            _structurePropsRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            if (theme.goalStructureProp != null && theme.goalStructureProp.prefab != null)
            {
                PlaceStructure(theme.goalStructureProp, map.goal, plan, theme);
                if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(map.goal), null);
            }
            if (theme.spawnStructureProp != null && theme.spawnStructureProp.prefab != null && map.spawns.IsCreated)
            {
                for (int i = 0; i < map.spawns.Length; i++)
                {
                    PlaceStructure(theme.spawnStructureProp, map.spawns[i], plan, theme);
                    if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(map.spawns[i]), null);
                }
            }
        }

        private void PlaceStructure(PropData prop, int2 cell, BoardVisualPlan plan, MapThemeData theme)
        {
            var footprint = prop.Footprint;
            var placement = new PropPlacement(0, cell.x, cell.y, footprint.x, footprint.y, 0u, 0f, prop.visualScale, -1);
            InstantiateProp(prop, placement, plan, theme, _structurePropsRoot);
        }

        // effect-tiles unit 1 — 효과 타일 페인트. Initialize(Clear 포함) 이후 호출 계약 (아니면 지워짐).
        public void SetEffectTile(Vector2Int cell, TileBase tile)
        {
            if (grid == null) return;
            EnsureEffectTilemap();
            _effectTilemap.SetTile(ToCell(cell), tile);
        }

        // 효과 타일맵 전용 머티리얼 지정(펄스 발광 등). null 이면 기본 유지. TilemapRenderer 는
        // 타일맵당 머티리얼 1개라 전용 _effectTilemap 전체에 균일 적용된다.
        public void SetEffectTileMaterial(Material material)
        {
            if (grid == null || material == null) return;
            EnsureEffectTilemap();
            var r = _effectTilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.sharedMaterial = material;
        }

        private void EnsureEffectTilemap()
        {
            if (_effectTilemap != null) return;
            var go = new GameObject("EffectTiles");
            go.transform.SetParent(grid.transform, false); // grid 90°X 회전 상속 — ground/overlay 와 동일 평면
            _effectTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _effectTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f); // 셀 중심 anchor (정합 전제와 일치)
            r.sortingOrder = -15;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // tilemap-world-surround unit 4 — 외곽 터레인 링 셀에 원경 프랍을 저밀도로 흩뿌린다.
        // VisualPlan(sim 그리드) 밖이라 BackgroundPropPlacer 를 못 쓰는 별도 경량 scatter.
        // 바깥쪽 falloff 로 밀도 감소(가장자리 페이드), 꽃 등은 제외.
        // background-prop-shadow-polish unit 1 — 원경 프랍도 근경과 동일 접지 블롭 부착(이전엔 그림자 OFF).
        public void InstantiateRingProps(MapThemeData theme, int2 playableSize, int seed, float densityScale = 1f)
        {
            if (_ringPropsRoot != null) { SafeDestroy(_ringPropsRoot.gameObject); _ringPropsRoot = null; }
            if (grid == null || _tileSet == null || theme == null || theme.distantRingProps == null) return;
            int R = _tileSet.ringRadius;
            float density = theme.ringPropDensity * Mathf.Clamp01(densityScale);
            if (R <= 0 || density <= 0f) return;
            int w = playableSize.x, h = playableSize.y;

            // prop-area-pools unit 2 — 원경 풀은 distantRingProps(WeightedProp[]). 근경(playAreaProps)과 독립.
            // 리스트 소속 자체가 opt-in — 프랍을 원경에서 빼려면 이 리스트에서 제거하면 된다.
            float totalW = 0f;
            for (int i = 0; i < theme.distantRingProps.Length; i++)
            {
                var entry = theme.distantRingProps[i];
                if (entry != null && entry.prop != null && entry.prop.prefab != null) totalW += Mathf.Max(0f, entry.weight);
            }
            if (totalW <= 0f) return;

            // unit 8 — 틸트 나무가 플레이 영역을 덮지 않게, 플레이 셀(Walk/Place) 근처 링 셀은 비운다.
            int clearance = theme.ringPlayClearanceCells;

            var rng = Unity.Mathematics.Random.CreateFromIndex((uint)(seed ^ 0x246813) | 1u);
            var root = new GameObject("RingProps");
            _ringPropsRoot = root.transform;
            _ringPropsRoot.SetParent(transform, false);
            // prop-upright-root unit 1 — background 와 동일 upright 프레임(부모 90° 상쇄).
            _ringPropsRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            for (int y = -R; y < h + R; y++)
            for (int x = -R; x < w + R; x++)
            {
                if (x >= 0 && x < w && y >= 0 && y < h) continue;
                // unit 8rev + 11(B) — 링이 플레이 하단(-y)에 있어 +y 누움으로 가리는 경우만 비운다.
                // 근경과 동일한 occlusion 판정 공유(width=2r+1 중심, depth=r).
                if (clearance > 0 && BackgroundPropPlacer.OccludesPlay(_visualPlan, x, y, 2 * clearance + 1, clearance)) continue;
                int ringDist = RingDistance(x, y, w, h);
                float falloff = Mathf.Clamp01(1f - theme.ringPropFalloffPerCell * (ringDist - 1));
                if (rng.NextFloat() > density * falloff) continue;

                float roll = rng.NextFloat(0f, totalW);
                Wassup.Data.PropData prop = null;
                for (int i = 0; i < theme.distantRingProps.Length; i++)
                {
                    var entry = theme.distantRingProps[i];
                    if (entry == null || entry.prop == null || entry.prop.prefab == null) continue;
                    roll -= Mathf.Max(0f, entry.weight);
                    if (roll <= 0f) { prop = entry.prop; break; }
                }
                if (prop == null) continue;

                var inst = Instantiate(prop.prefab, _ringPropsRoot);
                inst.name = prop.name + "_ring_" + x + "_" + y;
                inst.transform.position = CellCenterToWorld(x, y);
                inst.transform.localScale = Vector3.one * (1f + rng.NextFloat(-prop.scaleJitter, prop.scaleJitter));
                MapView.DisablePropDebugMarkers(inst);
                // 원경도 동일 프리팹이라 authored 블롭 포함 (shadow-polish unit 6). 런타임 부착 없음.
            }
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
