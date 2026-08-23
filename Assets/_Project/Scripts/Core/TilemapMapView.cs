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

        [Header("배치 확정 팝 (placement-cell-snap unit 4)")]
        [Tooltip("포커스 타일이 확정(변경)될 때 셀 위에 뜨는 스케일-페이드 팝의 지속(초).")]
        [SerializeField] private float commitPopDuration = 0.16f;
        [Tooltip("팝 시작 스케일(타일 기준). >1 = 타일보다 크게 시작해 안착.")]
        [SerializeField] private float commitPopStartScale = 1.3f;
        [Tooltip("팝 끝 스케일(타일 기준).")]
        [SerializeField] private float commitPopEndScale = 1.0f;
        [Tooltip("팝 시작 알파(0 으로 페이드).")]
        [SerializeField, Range(0f, 1f)] private float commitPopStartAlpha = 0.85f;
        [SerializeField] private Color commitPopValidColor = new Color(0.35f, 1f, 0.9f, 1f);
        [SerializeField] private Color commitPopInvalidColor = new Color(1f, 0.4f, 0.32f, 1f);

        // active-ally-zone unit 2 — 아군 장판 색(민트). 조준 시안/불가 적색과 구분되는 색.
        // 타일맵당 색 1개라 채널 균일하다. 이 파일의 색 규약(SerializeField 또는 tileSet SO)을 따른다.
        [SerializeField] private Color allyZoneColor = new Color(0.42f, 0.95f, 0.72f, 0.42f);
        // ultimate-leap unit 4 — 착지 예고. **dimmed 주황 채움**(사용자 결정 2026-08-02).
        // 빨강 outline 에서 바꾼 이유: 빨강은 이미 `rangeInvalidColor`(배치 불가)가 쓰고 있어
        // 의미가 겹치고, outline 은 면적이 없어 "여기서 나가라" 가 주변시로 안 읽힌다.
        // 주황은 `aimRangeColor`(조준)와 계열이 같지만 그건 배치 조작 중에만 뜨고 이건 전투
        // 중에만 떠서 화면에 공존하지 않는다. 알파는 낮게 — 밑의 유닛·바닥이 비쳐야 어느 칸인지 읽힌다.
        [SerializeField] private Color landingTelegraphColor = new Color(1f, 0.45f, 0.08f, 0.42f);

        [Header("끈적 액체 타일 (placement-cell-snap unit 7 rev)")]
        // 포커스 셀 하이라이트 자체가 액체 — 테두리(셀 고정) + 내부 번짐(손가락 방향).
        // 모양 튜닝(테두리 폭/도달/목 등)은 PlacementLiquidTile.mat 인스펙터. 여기는 팔레트 + 관성만.
        [SerializeField] private Color liquidValidBorder = new Color(0.45f, 1f, 0.55f, 0.95f);
        [SerializeField] private Color liquidValidFill = new Color(0.35f, 0.95f, 0.45f, 0.45f);
        [SerializeField] private Color liquidInvalidBorder = new Color(1f, 0.45f, 0.35f, 0.95f);
        [SerializeField] private Color liquidInvalidFill = new Color(1f, 0.35f, 0.28f, 0.4f);
        [Tooltip("점액 관성 스프링 강성 — 당김 신호를 늦게 따라오게. ↑=빠릿, ↓=더 걸쭉하게 늘어짐.")]
        [SerializeField] private float liquidSpring = 90f;
        [Tooltip("감쇠 — ↓=멈출 때 출렁임(오버슈트) 큼, ↑=바로 멎음. 셀 전환 직후 되돌아오는 출렁도 이 값.")]
        [SerializeField] private float liquidDamping = 9f;

        private TileSetData _tileSet;
        private Tilemap _rangeTilemap;
        // placement-enemy-see-through unit 6 — 하이라이트 상승 상태(sticky). range 타일맵 lazy 생성 시 반영.
        private bool _highlightAbove;
        private readonly HashSet<Vector2Int> _rangeCells = new();
        // 범위 타일 세기 배율(펄스 알파에 곱). 1 = 기존 사각 범위. unit 9.
        private float _rangeAlphaMul = 1f;
        // placement-thumb-occlusion unit 3 — 배치 판정 유효성 + 전이 스탬프(SetPlacementRangeValidity 단독 소유).
        private bool _rangeInvalid;
        private float _rangeInvalidSince;
        // unit 4 — 지금 깔려 있는 범위 표시가 조준 페이즈 스타일인가. 페인트 시점에 정해지고
        // Update() 의 틴트가 같은 플래그로 색을 고른다(타일과 색이 갈라지지 않게).
        private bool _rangeAimStyle;
        // placement-eligible-tile-highlight unit 1 — 배치 가능 셀 밝은 하이라이트(정적, 펄스 없음).
        // range 와 분리된 전용 타일맵. 드래그 중엔 range 처럼 유닛 위로 상승(9998).
        private Tilemap _placeableTilemap;
        private bool _placeableActive;
        private float _placeableShowTime; // unscaledTime 캡처(페이드인 기준)
        // first-run-tutorial unit 1 — 배치 **불가** 칸 하이라이트(맵 설명 전용). placeable 과
        // 정의상 서로 겹치지 않는 집합이라 같은 sorting 층을 쓰되 z 만 살짝 갈라 z-fight 를 피한다.
        private Tilemap _blockedTilemap;
        private bool _blockedActive;
        private float _blockedShowTime;
        // defender-directional-volley unit 9 — 방향 지정 화살표(재사용 풀, 최대 4).
        private readonly List<SpriteRenderer> _aimArrows = new();
        private static Sprite _arrowSprite;
        private int2 _gridSize;
        private readonly Dictionary<Vector2Int, Coroutine> _activeFlashes = new();
        private readonly HashSet<Vector2Int> _hoverCells = new();
        // placement-cell-snap unit 4 — 확정 팝: 재사용 SpriteRenderer(grid 자식, 코플레이너) + 절차적 흰 스프라이트.
        private SpriteRenderer _commitPop;
        private Coroutine _commitPopCo;
        private static Sprite _popSprite;
        // placement-cell-snap unit 7 rev — 끈적 액체 하이라이트: 상주형 SpriteRenderer(확정 팝과 동일 PopSprite 쿼드 시드).
        // 렌더 = PopSprite 쿼드 + PlacementLiquidTile 셰이더 인스턴스 머티리얼(원본 .mat 비오염).
        private SpriteRenderer _liquidTile;
        private Material _liquidTileMat;
        private bool _liquidTileMatMissing; // 미배선 경고 1회 게이트
        // 점액 관성 — 표시용 당김 벡터(dir×t)를 스프링으로 지연/출렁. 신호(정책)는 그대로, 시각만 늦는다.
        private Vector2 _pullSmoothed;
        private Vector2 _pullVel;
        // 쿼드 한 변(셀 배수). 혀 최대 도달 = reach×t^pow + 방울반지름×신장 ≈ 오버슈트(1.2)에서 1.9셀 —
        // 캔버스(±절반)가 이보다 좁으면 옆 타일 위에서 칼로 잘린다. 셰이더 _QuadCells 와 동기(여기가 단일 소스).
        private const float LiquidQuadCells = 4f;
        private static readonly int PullId = Shader.PropertyToID("_Pull");
        private static readonly int BorderColorId = Shader.PropertyToID("_BorderColor");
        private static readonly int FillColorId = Shader.PropertyToID("_FillColor");
        private static readonly int QuadCellsId = Shader.PropertyToID("_QuadCells");
        // tilemap-world-surround unit 2 — 배경 프랍 호스트(Deco) 판정용 셀/리전 메타 + 프랍 인스턴스 루트.
        private BoardVisualPlan _visualPlan;
        private Transform _backgroundPropsRoot;
        // tilemap-world-surround unit 4 — 외곽 링 원경 프랍 인스턴스 루트.
        private Transform _ringPropsRoot;
        // prop-placement-layer unit 1 — goal/spawn 구조물 프랍 루트. 부모(90°X)를 역회전 상쇄해 메쉬가 똑바로 선다.
        private Transform _structurePropsRoot;
        // waypoint-routing 후속(사용자 결정 B, 2026-08-12) — 붕괴한 골의 프랍을 셀로 찾아
        // «이미 뚫린 곳» 으로 전환하기 위한 추적. 붕괴 후에도 프랍이 멀쩡히 서 있어서
        // 적이 그 골에서 소멸(유출 전환)하는 것이 «살아있는 마음을 안 때리는 버그» 로 읽혔다.
        private readonly Dictionary<Vector2Int, GameObject> _goalPropsByCell = new();
        // first-session-tutorial unit 2 — 셀 중심이 아니라 실제 구조물 renderer 중심을 노출한다.
        // 구조물 미사용 테마에서는 같은 API가 셀 중심으로 폴백한다.
        private bool _hasGoalVisualAnchor;
        private Vector3 _goalVisualAnchorWorld;
        private readonly List<Vector3> _spawnVisualAnchorsWorld = new();
        // first-session-tutorial unit 26 — 효과 타일이 칠해진 셀. 튜토리얼 마커 조회용이며
        // 소유권은 BattleBridge._effectTilesByCell 에 있다(여기는 "보이는 곳" 미러).
        private readonly List<Vector2Int> _effectTileCells = new();
        // effect-tiles unit 1 — 효과 타일 전용 런타임 타일맵. overlayTilemap 은 hover/reject 가
        // SetTile/null 로 덮어쓰므로 공유 금지. sorting -15 = ground(-20) 위 / overlay·hover(-10) 아래.
        // 런타임 생성 → 씬 SerializeField/저장 불필요.
        private Tilemap _effectTilemap;

        public Grid Grid => grid;
        public BoardVisualPlan VisualPlan => _visualPlan;

        public bool TryGetGoalVisualAnchor(out Vector3 worldPosition)
        {
            worldPosition = _goalVisualAnchorWorld;
            return _hasGoalVisualAnchor;
        }

        public bool TryGetSpawnVisualAnchor(int index, out Vector3 worldPosition)
        {
            if (index >= 0 && index < _spawnVisualAnchorsWorld.Count)
            {
                worldPosition = _spawnVisualAnchorsWorld[index];
                return true;
            }

            worldPosition = default;
            return false;
        }

        // 평면 빌보드 프랍이 바닥 타일과 z-fight 나지 않도록 살짝 띄우는 world +Y 오프셋.
        private const float PropGroundLift = 0.02f;
        // defender-directional-volley unit 9 — 조준 화살표도 같은 이유로 띄운다. 프랍보다
        // 조금 더(범위 타일 위에도 얹혀야) — 여전히 시각적으로 무시 가능한 높이.
        private const float ArrowGroundLift = 0.05f;
        // unit 4 — 화살표를 조준 색에서 얼마나 밝히나. 화살표 스케일/알파(0.92·0.7·0.5)와 같은
        // 결의 프레젠테이션 상수 — 색 자체는 TileSetData 가 소유하고 여기선 명도만 민다.
        private const float AimArrowLighten = 0.72f;

        // BattleBridge 맵 빌드 시 호출 (unit 2). Grid cellLayout/cellSize 를 설정한 뒤
        // 전체 셀을 일괄 페인트한다. 재진입(RebuildDraftMap) 안전 — Clear 선행.
        public void Initialize(in GeneratedMap map, float tileSize, TileSetData tileSet,
            bool realShadows = false)
        {
            Clear();
            _gridSize = map.IsCreated ? map.gridSize : default;
            _tileSet = tileSet;
            ConfigureGrid(tileSize, realShadows);
            PaintGround(in map);
            PaintMarkers(in map);
            CenterBoardAtWorldOrigin(in map);
            // 배경 프랍 배치(Deco/Env 호스트) 판정에 쓰는 셀/리전/anchor 메타.
            _visualPlan = map.IsCreated ? BoardVisualPlanBuilder.Build(map, map.seed) : null;
            ResetStructureVisualAnchors(in map);
        }

        // tilted-billboard unit 1 — 보드 중앙을 월드 원점(X·Z=0)에 맞춘다. XZ 바닥이라 수평면(X,Z)만 정렬,
        // Y(바닥 높이)는 보존. Tilemap 모드는 sim origin=0, 월드 배치는 grid.transform 권위라 view 전용 변경 — sim 무영향.
        // ToView/ToSim/RaycastPlane 모두 grid 기준 live 라 정합 유지. 맵 크기 달라져도 재계산·idempotent.
        private void CenterBoardAtWorldOrigin(in GeneratedMap map)
        {
            if (grid == null || !map.IsCreated) return;
            // 보드 양 끝 셀의 월드 코너 중점 = 현재 보드 중심(셀↔월드가 affine 이라 성립).
            Vector3 min = grid.CellToWorld(new Vector3Int(0, 0, 0));
            Vector3 max = grid.CellToWorld(new Vector3Int(map.gridSize.x, map.gridSize.y, 0));
            Vector3 center = (min + max) * 0.5f;
            grid.transform.position -= new Vector3(center.x, 0f, center.z);
        }

        public void Clear()
        {
            StopAllFlashes();
            if (_commitPopCo != null) { StopCoroutine(_commitPopCo); _commitPopCo = null; }
            if (_commitPop != null) { SafeDestroy(_commitPop.gameObject); _commitPop = null; } // grid 자식 → 맵 리빌드 시 함께 정리
            if (_liquidTile != null) { SafeDestroy(_liquidTile.gameObject); _liquidTile = null; } // unit 7 — 액체 하이라이트도 동일
            if (_liquidTileMat != null) { SafeDestroy(_liquidTileMat); _liquidTileMat = null; }
            _liquidTileMatMissing = false; // 맵 리빌드 시 tileSet 이 바뀔 수 있으니 재시도 허용
            _hoverCells.Clear();
            if (groundTilemap != null) groundTilemap.ClearAllTiles();
            if (overlayTilemap != null) overlayTilemap.ClearAllTiles();
            if (_effectTilemap != null) _effectTilemap.ClearAllTiles();
            if (_rangeTilemap != null) _rangeTilemap.ClearAllTiles();
            _rangeCells.Clear();
            _rangeInvalid = false; // unit 3 — 맵 리빌드 경계 방어(정상 경로는 호출부 리셋이 덮는다)
            if (_placeableTilemap != null) _placeableTilemap.ClearAllTiles(); // placement-eligible-tile-highlight unit 1
            _placeableActive = false;
            if (_blockedTilemap != null) _blockedTilemap.ClearAllTiles(); // first-run-tutorial unit 1
            _blockedActive = false;
            ClearZoneCells(); // active-ally-zone unit 2 — 맵 리빌드/티어다운에서 장판 점등 회수
            ClearTelegraphCells(); // ultimate-leap unit 4 — 예고가 맵 너머로 살아남지 않게
            if (_backgroundPropsRoot != null) { SafeDestroy(_backgroundPropsRoot.gameObject); _backgroundPropsRoot = null; }
            if (_ringPropsRoot != null) { SafeDestroy(_ringPropsRoot.gameObject); _ringPropsRoot = null; }
            if (_structurePropsRoot != null) { SafeDestroy(_structurePropsRoot.gameObject); _structurePropsRoot = null; }
            _hasGoalVisualAnchor = false;
            _spawnVisualAnchorsWorld.Clear();
            _effectTileCells.Clear(); // unit 26 — 타일맵을 비웠으니 기억도 비운다(맵 리빌드 경계)
        }

        private void Update()
        {
            if (_tileSet == null) return;
            // 사거리 알파 — 펄스 제거(사용자 요청): 정적 레벨(rangePulseMaxAlpha)만 적용.
            // 세기 차이(방향 미정 십자 vs 선택된 레인)는 _rangeAlphaMul 배율로만.
            if (_rangeTilemap != null && _rangeCells.Count > 0) ApplyRangeTint();
            // ultimate-leap unit 4 — 예고 tint 도 같은 이유로 매 프레임(셀이 있을 때만).
            if (_telegraphCells.Count > 0) ApplyTelegraphTint();
            // placement-eligible-tile-highlight unit 1 — 배치 하이라이트 페이드인(정적, 펄스 없음).
            // range 와 독립: 탭 arm 처럼 range 가 없어도 페이드가 돌아야 한다.
            if (_placeableActive && _placeableTilemap != null)
            {
                float pt = _tileSet.placeableFadeInDuration > 0f
                    ? Mathf.Clamp01((Time.unscaledTime - _placeableShowTime) / _tileSet.placeableFadeInDuration) : 1f;
                var pc = _tileSet.placeableColor; pc.a *= pt;
                _placeableTilemap.color = pc;
            }
            // first-run-tutorial unit 1 — 불가 하이라이트도 같은 페이드 규약(placeableFadeInDuration 공유).
            if (_blockedActive && _blockedTilemap != null)
            {
                float bt = _tileSet.placeableFadeInDuration > 0f
                    ? Mathf.Clamp01((Time.unscaledTime - _blockedShowTime) / _tileSet.placeableFadeInDuration) : 1f;
                var bc = _tileSet.blockedColor; bc.a *= bt;
                _blockedTilemap.color = bc;
            }
        }

        private void ConfigureGrid(float tileSize, bool realShadows)
        {
            if (grid == null) return;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSize = new Vector3(tileSize, tileSize, 1f);

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

        // camera-direction unit 8 — 플레이 그리드(경로·배치 셀)의 월드 bounds.
        // **ground 렌더러 실측을 쓰면 안 된다** — 렌더러 bounds 는 외곽 터레인 링과 데코 지대까지
        // 포함해 20×12 맵이 35×32 로 잡히고, 카메라가 거리 54 까지 물러나 플레이 영역이 화면
        // 중앙의 작은 조각이 된다. 그래서 grid 셀 좌표로 플레이 범위만 직접 만든다.
        // **4코너를 모두 감싼다** — grid 는 회전해 있어서(XZ 바닥 90°X) 마주보는 두 코너만으로는
        // 월드 AABB 의 극단을 놓친다. 높이는 0(평면) — 유닛이 솟는 만큼은 fit margin 이 흡수한다.
        public bool TryGetPlayfieldWorldBounds(Vector2Int gridSize, out Bounds bounds)
        {
            bounds = default;
            if (grid == null || gridSize.x <= 0 || gridSize.y <= 0) return false;
            bounds = new Bounds(grid.CellToWorld(new Vector3Int(0, 0, 0)), Vector3.zero);
            bounds.Encapsulate(grid.CellToWorld(new Vector3Int(gridSize.x, 0, 0)));
            bounds.Encapsulate(grid.CellToWorld(new Vector3Int(0, gridSize.y, 0)));
            bounds.Encapsulate(grid.CellToWorld(new Vector3Int(gridSize.x, gridSize.y, 0)));
            return true;
        }

        private static void SetRendererSorting(Tilemap tilemap, int order)
        {
            if (tilemap == null) return;
            var r = tilemap.GetComponent<TilemapRenderer>();
            if (r != null) r.sortingOrder = order;
        }

        // placement-enemy-see-through unit 6 — 드래그 중 배치 하이라이트(range/overlay)를 적(빌보드) 위로 임시 상승.
        // 10000/10002 = 유닛(Compute 수백)·투사체(+1000) 위, 힛바(16000)·드래그 프리뷰(20000) 아래.
        // 드래그 종료 시 기본값(overlay -10 / range -12) 복원. "보드<유닛" 기본 규칙은 드래그 밖에서 불변.
        public void SetPlacementHighlightAboveUnits(bool above)
        {
            _highlightAbove = above; // range/placeable 타일맵이 아직 없으면 Ensure 시 이 값을 반영.
            SetRendererSorting(overlayTilemap, above ? 10002 : -10);
            SetRendererSorting(_rangeTilemap, above ? 10000 : -12);
            SetRendererSorting(_placeableTilemap, above ? 9998 : -13); // placement-eligible-tile-highlight unit 1
            SetRendererSorting(_zoneTilemap, above ? 9997 : -14);     // active-ally-zone unit 2
            // ultimate-leap unit 4 — **이 목록에 반드시 있어야 한다.** 빠지면 예고가 -13 에 굳어,
            // 플레이어가 유닛을 빼려고 드래그를 시작하는 **바로 그 순간**(이 스킬이 성립하는 순간)
            // range/placeable 만 위로 올라가고 예고가 그 아래로 묻힌다.
            SetRendererSorting(_telegraphTilemap, above ? 9999 : -13);
            // first-run-tutorial unit 1 — 같은 이유로 이 목록에 있어야 한다. 지금은 맵 설명 전용이라
            // 드래그와 겹칠 일이 없지만, 빠뜨린 타일맵은 언젠가 -13 에 굳는다(위 ultimate-leap 사례).
            SetRendererSorting(_blockedTilemap, above ? 9998 : -13);
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
            {
                // multi-goal-map — 골 마커 goals 순회(폴백 map.goal)
                if (map.goals.IsCreated && map.goals.Length > 0)
                    for (int i = 0; i < map.goals.Length; i++)
                        overlayTilemap.SetTile(ToCell(map.goals[i]), _tileSet.goalTile);
                else
                    overlayTilemap.SetTile(ToCell(map.goal), _tileSet.goalTile);
            }

            if (_tileSet.spawnTile != null && map.spawns.IsCreated)
            {
                for (int i = 0; i < map.spawns.Length; i++)
                    overlayTilemap.SetTile(ToCell(map.spawns[i]), _tileSet.spawnTile);
            }
        }

        // --- 배치 피드백 (SetPlacementHover/FlashTileReject/ClearPlacementHover) ---

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

        // placement-cell-snap unit 4 — 포커스 타일 확정(변경) 시 셀 위에 스케일 오버슈트+알파 페이드 팝.
        // 하이라이트 타일(overlay)은 그대로 남고, 이 팝이 "여기로 확정됨"을 한 번 punctuate 한다.
        public void PulsePlacementHover(Vector2Int cell, bool valid)
        {
            if (grid == null) return;
            var sr = EnsureCommitPop();
            // 액체 하이라이트와 동일 — Ground(ZWrite On) 코플레이너 z-fight 방지 리프트.
            var popLocal = grid.CellToLocalInterpolated(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
            popLocal.z = -PropGroundLift;
            sr.transform.localPosition = popLocal;
            Color c = valid ? commitPopValidColor : commitPopInvalidColor;
            if (_commitPopCo != null) StopCoroutine(_commitPopCo);
            _commitPopCo = StartCoroutine(CommitPopCoroutine(sr, c));
        }

        private SpriteRenderer EnsureCommitPop()
        {
            if (_commitPop != null) return _commitPop;
            var go = new GameObject("PlacementCommitPop");
            go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너
            go.transform.localRotation = Quaternion.identity;
            _commitPop = go.AddComponent<SpriteRenderer>();
            _commitPop.sprite = PopSprite();
            _commitPop.sortingOrder = BoardSortOrder.PlacementCommitPopOrder;
            var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
            if (overlayR != null) _commitPop.sortingLayerID = overlayR.sortingLayerID; // overlay 와 같은 sorting layer
            go.SetActive(false);
            return _commitPop;
        }

        // placement-cell-snap unit 7 rev — 포커스 셀 하이라이트 자체가 끈적한 액체(오버레이 아님).
        // dir = 당김 방향(셀 공간), t = 0(중심)~1(파열). 신호는 PlacementCellSnap.EvaluateStretch 가 Resolve 와
        // 같은 밴드로 계산 → t=1 이 실제 전환점과 일치(하이라이트가 거짓말 안 함).
        // 렌더 = 셀 2배 쿼드 1장 + Wassup/PlacementLiquidTile 셰이더(SDF): 테두리(둥근사각)는 셀에 고정 =
        // "릴리즈하면 여기" 계약, 내부 fill 은 손가락 방향 액적과 smin 블렌드로 번지다 테두리를 넘는다.
        // 모양 튜닝은 .mat 인스펙터(라이브 반영). 쿼드는 회전하지 않는다 — 테두리는 축 정렬, 방향은 셰이더 uniform.
        public void SetPlacementStretch(Vector2Int cell, Vector2 dir, float t, bool valid)
        {
            if (grid == null) return;
            var sr = EnsureLiquidTile();
            if (sr == null) return; // 머티리얼 미배선 — EnsureLiquidTile 이 1회 경고
            t = Mathf.Clamp01(t);
            float cs = grid.cellSize.x; // rect 보드·균일 cellSize 전제(ConfigureGrid)

            // 점액 관성 — 목표 당김(dir×t)을 스프링 추종. 늦게 따라오고, 멈추면/셀이 넘어가면 출렁하며 이완.
            // 표시가 숨겨져 있었으면 스냅 리셋(이전 세션 잔여 스윙 금지). 드래프트 일시정지에도 살아야 하니 unscaled.
            Vector2 targetPull = dir * t;
            if (!sr.gameObject.activeSelf) { _pullSmoothed = targetPull; _pullVel = Vector2.zero; }
            else
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f); // 히치 스파이크 시 폭주 방지
                _pullVel += (targetPull - _pullSmoothed) * (liquidSpring * dt);
                _pullVel /= 1f + liquidDamping * dt;
                _pullSmoothed += _pullVel * dt;
            }
            float tS = _pullSmoothed.magnitude;
            Vector2 dirS = tS > 1e-4f ? _pullSmoothed / tS : Vector2.zero;
            tS = Mathf.Min(tS, 1.2f); // 셰이더 허용 오버슈트 한계와 동기

            // local -Z = world +Y(부모 90°X 회전). Ground 가 ZWrite On 이라 코플레이너면 z-fight 로 깜빡인다 —
            // 프랍과 같은 해법(PropGroundLift)으로 띄운다. 타일맵끼리는 같은 메쉬라 안 겪는 문제.
            var local = grid.CellToLocalInterpolated(new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f));
            local.z = -PropGroundLift;
            sr.transform.localPosition = local;
            sr.transform.localScale = new Vector3(LiquidQuadCells * cs, LiquidQuadCells * cs, 1f);
            _liquidTileMat.SetVector(PullId, new Vector4(dirS.x, dirS.y, tS, 0f));
            _liquidTileMat.SetColor(BorderColorId, valid ? liquidValidBorder : liquidInvalidBorder);
            _liquidTileMat.SetColor(FillColorId, valid ? liquidValidFill : liquidInvalidFill);
            if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
        }

        public void ClearPlacementStretch()
        {
            if (_liquidTile != null) _liquidTile.gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureLiquidTile()
        {
            if (_liquidTile != null) return _liquidTile;
            if (_liquidTileMatMissing) return null;
            var srcMat = _tileSet != null ? _tileSet.placementLiquidMaterial : null;
            if (srcMat == null)
            {
                // 에디터 한정 폴백 없이 명시 실패 — Shader.Find 폴백은 기기 빌드에서만 조용히 죽는다(2026-07-15 사고).
                Debug.LogWarning("TilemapMapView: TileSetData.placementLiquidMaterial 미할당 — 액체 하이라이트 생략. " +
                                 "PlacementLiquidTile.mat 을 tileSet 에 배선할 것.", this);
                _liquidTileMatMissing = true;
                return null;
            }
            _liquidTileMat = new Material(srcMat); // 인스턴스 — 에셋 원본을 런타임 파라미터로 오염시키지 않는다
            _liquidTileMat.SetFloat(QuadCellsId, LiquidQuadCells); // 쿼드 크기 ↔ 셰이더 매핑 단일 소스 동기
            var go = new GameObject("PlacementLiquidTile");
            go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너. 회전 없음(테두리 축 정렬).
            go.transform.localRotation = Quaternion.identity;
            _liquidTile = go.AddComponent<SpriteRenderer>();
            _liquidTile.sprite = PopSprite(); // 1×1 흰 full-rect — 모양은 전부 셰이더 SDF
            _liquidTile.sharedMaterial = _liquidTileMat;
            _liquidTile.sortingOrder = BoardSortOrder.PlacementLiquidOrder;
            var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
            if (overlayR != null) _liquidTile.sortingLayerID = overlayR.sortingLayerID;
            go.SetActive(false);
            return _liquidTile;
        }

        // defender-directional-volley unit 9 — 방향 지정 화살표. 보드에 눕는 탭 어포던스라
        // 블롭/팝과 같은 방식(grid 자식 절차적 스프라이트)으로 그린다. 이 뷰는 write-only —
        // 어느 칸에 어느 각도로 무엇이 선택됐는지는 전부 호출부가 정해 넘긴다.
        // emphasized — unit 5. 방향이 확정된 상태(그 레인만 남은 상태)면 전부 또렷·크게,
        // 미선택(4레인 전부 표시)이면 전부 흐리게·작게. 개별 선택 인덱스는 없다.
        public void SetAimArrows(IReadOnlyList<Vector2Int> cells, IReadOnlyList<float> anglesDeg, bool emphasized)
        {
            if (grid == null || cells == null || anglesDeg == null || _tileSet == null) return;
            float cellWorld = grid.cellSize.x;
            for (int i = 0; i < cells.Count && i < anglesDeg.Count; i++)
            {
                var sr = EnsureAimArrow(i);
                var local = grid.CellToLocalInterpolated(new Vector3(cells[i].x + 0.5f, cells[i].y + 0.5f, 0f));
                sr.transform.localPosition = local;
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, anglesDeg[i]);
                // 셀 평면에 그대로 눕히면 불투명 바닥 타일과 코플레이너라 z-acne(자글거림)가
                // 난다 — 밉맵으로도 안 사라지는 렌더 단계 문제. 프랍이 PropGroundLift 로
                // 푸는 바로 그 함정. world +Y 로 살짝 띄워 항상 타일 앞에 오게 한다(부모
                // 회전/스케일 무관하게 정확히 +Y 만큼 — localPosition 뒤 world 로 보정).
                sr.transform.position += Vector3.up * ArrowGroundLift;

                bool on = emphasized;
                // 선택된 화살표만 또렷하고 살짝 크다. 색은 조준 슬롯(unit 4)에서 오되 **명도를
                // 올려** 쓴다 — 레인이 solid 로 채워지면 같은 색 화살표는 그 위에서 사라진다.
                // 같은 색상(hue)을 유지하니 "레인과 한 몸"이라는 신호는 남고, 값 대비로 읽힌다.
                var c = Color.Lerp(RangeTintColor(aimStyle: true), Color.white, AimArrowLighten);
                c.a = on ? 1f : 0.5f;
                sr.color = c;
                float s = cellWorld * (on ? 0.92f : 0.7f);
                sr.transform.localScale = new Vector3(s, s, 1f);
                sr.gameObject.SetActive(true);
            }
            for (int i = cells.Count; i < _aimArrows.Count; i++) _aimArrows[i].gameObject.SetActive(false);
        }

        public void ClearAimArrows()
        {
            for (int i = 0; i < _aimArrows.Count; i++)
                if (_aimArrows[i] != null) _aimArrows[i].gameObject.SetActive(false);
        }

        private SpriteRenderer EnsureAimArrow(int index)
        {
            while (_aimArrows.Count <= index)
            {
                var go = new GameObject($"AimArrow{_aimArrows.Count}");
                go.transform.SetParent(grid.transform, false); // grid 자식 → 타일과 코플레이너
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ArrowSprite();
                sr.sortingOrder = BoardSortOrder.AimArrowOrder;
                var overlayR = overlayTilemap != null ? overlayTilemap.GetComponent<TilemapRenderer>() : null;
                if (overlayR != null) sr.sortingLayerID = overlayR.sortingLayerID;
                go.SetActive(false);
                _aimArrows.Add(sr);
            }
            return _aimArrows[index];
        }

        // +Y 를 향하는 삼각형(호출부가 Z 회전으로 방향을 만든다). 가장자리를 살짝 흐려
        // 계단을 없앤다 — 타일 위에 눕는 작은 도형이라 에일리어싱이 그대로 보인다.
        // 꼭짓점 (0, 0.9) / 밑변 (±0.75, -0.7). 한 점이 삼각형 안이면 true(하드 테스트).
        private static bool InArrowTriangle(float u, float v)
        {
            if (v < -0.7f || v > 0.9f) return false;
            float halfWidth = Mathf.Lerp(0f, 0.75f, Mathf.InverseLerp(0.9f, -0.7f, v));
            return Mathf.Abs(u) <= halfWidth;
        }

        private static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            // 자글거림은 두 층이다:
            // (1) 소스 텍스처 앨리어싱 — 저해상도 + 대각 빗변에 수직이 아닌 페더. 해법 =
            //     커버리지 슈퍼샘플링(텍셀당 SS×SS 하드 테스트 → 통과 비율을 알파로).
            // (2) 렌더 minification — 보드가 40~58° 기울어 화살표가 비스듬히 눕는다.
            //     밉맵이 없으면 축소 방향에서 계단이 그대로 샌다(둥근 블롭은 저주파라
            //     안 보이지만 화살표는 대각 샤프 엣지라 두드러진다). 해법 = 밉체인 +
            //     Trilinear + aniso. 소팅(11500)은 레인 타일 위라 z-fight 는 아니다.
            const int R = 128;
            const int SS = 4;
            var tex = new Texture2D(R, R, TextureFormat.RGBA32, mipChain: true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            var px = new Color[R * R];
            float inv = 1f / (SS * SS);
            for (int y = 0; y < R; y++)
                for (int x = 0; x < R; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SS; sy++)
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = (x + (sx + 0.5f) / SS) / R * 2f - 1f;
                            float v = (y + (sy + 0.5f) / SS) / R * 2f - 1f;
                            if (InArrowTriangle(u, v)) hits++;
                        }
                    px[y * R + x] = new Color(1f, 1f, 1f, hits * inv);
                }
            tex.SetPixels(px);
            tex.Apply();
            _arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, R, R), new Vector2(0.5f, 0.5f), R);
            return _arrowSprite;
        }

        private static Sprite PopSprite()
        {
            if (_popSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _popSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width); // pivot 중심, 1 world unit → localScale 로 타일 크기 맞춤
            }
            return _popSprite;
        }

        private IEnumerator CommitPopCoroutine(SpriteRenderer sr, Color color)
        {
            float cellWorld = grid != null ? grid.cellSize.x : 1f; // 로컬 1셀 크기
            sr.gameObject.SetActive(true);
            float t = 0f;
            float dur = Mathf.Max(commitPopDuration, 0.01f);
            while (t < dur)
            {
                float u = t / dur;
                float ease = 1f - (1f - u) * (1f - u); // OutQuad — 빠르게 커졌다 감속 안착
                float s = Mathf.Lerp(commitPopStartScale, commitPopEndScale, ease) * cellWorld;
                sr.transform.localScale = new Vector3(s, s, 1f);
                color.a = Mathf.Lerp(commitPopStartAlpha, 0f, ease);
                sr.color = color;
                t += Time.unscaledDeltaTime; // 배치 슬로우모 무관 실시간
                yield return null;
            }
            sr.gameObject.SetActive(false);
            _commitPopCo = null;
        }

        // --- 배경 프랍 (tilemap-world-surround unit 2) ---

        // Deco/Env 셀에 배경 프랍 프리팹을 배치한다. 위치는 grid 권위(BoardSpace.ToView 와 동일 수식) —
        // raw (x,y)*tileSize 와 달리 90° 회전·센터링을 자동 반영. 정렬/마커/틴트는 PropInstanceUtil 헬퍼 사용.
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
        private GameObject InstantiateProp(PropData prop, PropPlacement placement,
                                           BoardVisualPlan plan, MapThemeData theme, Transform root)
        {
            float centerX = placement.x + (placement.width - 1) * 0.5f;
            float centerY = placement.y + (placement.height - 1) * 0.5f;
            var instance = Instantiate(prop.prefab, root);
            instance.name = $"{prop.name}_{placement.x}_{placement.y}";
            instance.transform.position = CellCenterToWorld(centerX, centerY);
            instance.transform.localScale = Vector3.one * placement.scale;
            PropInstanceUtil.ApplyPropSorting(instance, prop, placement, plan);
            PropInstanceUtil.DisablePropDebugMarkers(instance);
            // 프랍 그림자 = 프리팹 authored 블롭 (shadow-polish unit 6). 런타임 부착 없음 — 프리팹이 source of truth.
            if (theme.propGlobalTint != Color.white)
                PropInstanceUtil.ApplyPropGlobalTint(instance, theme.propGlobalTint);
            return instance;
        }

        // prop-placement-layer unit 1 — goal/spawn 셀에 3D 메쉬 구조물 프랍을 세운다.
        // 메쉬는 빌보드 아님 → 부모(XZ 바닥 90°X)를 역회전(-90°X)한 root 아래 두면 identity 로 똑바로 선다.
        // 구조물이 놓인 셀의 placeholder 마커 타일(overlay)은 제거. sim(GeneratedMap/FlowField) 무변경.
        public void InstantiateStructureProps(in GeneratedMap map, MapThemeData theme, BoardVisualPlan plan)
        {
            if (_structurePropsRoot != null) { SafeDestroy(_structurePropsRoot.gameObject); _structurePropsRoot = null; }
            _goalPropsByCell.Clear();   // 프랍 루트와 같은 수명 — 재빌드 시 stale 참조 방지
            ResetStructureVisualAnchors(in map);
            if (grid == null || theme == null || !map.IsCreated) return;
            if (theme.goalStructureProp == null && theme.spawnStructureProp == null) return;

            var root = new GameObject("StructureProps");
            _structurePropsRoot = root.transform;
            _structurePropsRoot.SetParent(transform, false);
            // 부모(grid, 월드 90°X) 상쇄 → root 월드 업라이트. 메쉬 child 는 회전 없이 똑바로 선다.
            _structurePropsRoot.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            if (theme.goalStructureProp != null && theme.goalStructureProp.prefab != null)
            {
                // multi-goal-map — 골 구조물 goals 순회(폴백 map.goal). 비주얼 앵커는 primary(goals[0]) 유지.
                if (map.goals.IsCreated && map.goals.Length > 0)
                {
                    for (int i = 0; i < map.goals.Length; i++)
                    {
                        var gi = PlaceStructure(theme.goalStructureProp, map.goals[i], plan, theme);
                        if (i == 0) _goalVisualAnchorWorld = ResolveVisualAnchor(gi, _goalVisualAnchorWorld);
                        if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(map.goals[i]), null);
                        _goalPropsByCell[new Vector2Int(map.goals[i].x, map.goals[i].y)] = gi;
                    }
                }
                else
                {
                    var instance = PlaceStructure(theme.goalStructureProp, map.goal, plan, theme);
                    _goalVisualAnchorWorld = ResolveVisualAnchor(instance, _goalVisualAnchorWorld);
                    if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(map.goal), null);
                    _goalPropsByCell[new Vector2Int(map.goal.x, map.goal.y)] = instance;
                }
            }
            if (theme.spawnStructureProp != null && theme.spawnStructureProp.prefab != null && map.spawns.IsCreated)
            {
                for (int i = 0; i < map.spawns.Length; i++)
                {
                    var instance = PlaceStructure(theme.spawnStructureProp, map.spawns[i], plan, theme);
                    _spawnVisualAnchorsWorld[i] = ResolveVisualAnchor(
                        instance, _spawnVisualAnchorsWorld[i]);
                    if (overlayTilemap != null) overlayTilemap.SetTile(ToCell(map.spawns[i]), null);
                }
            }
        }

        // waypoint-routing 후속(사용자 결정 B) — 붕괴한 골을 «이미 뚫린 곳» 으로 전환.
        // 프랍 교체 아트가 없으므로(백로그) 코드만으로 읽히게 한다: 그을린 틴트 + 주저앉음.
        // 스프라이트 프랍은 SpriteRenderer.color(공용 머티리얼 무오염), 메쉬는 MPB.
        // 붕괴 상태는 매치 수명 — 프랍 루트 재빌드가 원복을 겸한다(_goalPropsByCell.Clear).
        // three-minute-kill-race unit 2 — **마음의 남은 체력은 균열로만 보인다**(게이지 금지).
        // `MarkGoalCollapsed`(붕괴)의 형제이고 같은 제약을 따른다: 프랍 교체 아트가 없으므로
        // 코드만으로 읽히게 한다 — 단계가 올라갈수록 그을린다.
        //
        // **단계로 양자화하는 것이 핵심이다.** 체력 비율을 연속으로 틴트하면 매 프레임 미세
        // 변화라 아무도 못 읽는다. 단계가 있어야 「금이 하나 더 갔다」가 **사건**으로 보인다.
        // 호출자는 단계가 바뀔 때만 부른다(브리지가 셀별 마지막 단계를 기억한다).
        //
        // stage: 0 = 온전(원색) · 1~3 = 진행. 0 단계 복구도 지원한다(힐러가 마음을 고치면
        // 색이 돌아와야 «회복됐다» 가 읽힌다).
        public void SetGoalCrack(Vector2Int cell, int stage)
        {
            if (!_goalPropsByCell.TryGetValue(cell, out var prop) || prop == null) return;
            stage = Mathf.Clamp(stage, 0, 3);
            // 온전(1,1,1) → 3단계(0.42,0.36,0.32). 붕괴색(0.22,0.19,0.17)보다 밝게 남겨
            // «금이 갔다» 와 «무너졌다» 가 한눈에 갈린다.
            float k = stage / 3f;
            var tint = new Color(
                Mathf.Lerp(1f, 0.42f, k),
                Mathf.Lerp(1f, 0.36f, k),
                Mathf.Lerp(1f, 0.32f, k), 1f);
            ApplyPropTint(prop, tint);
        }

        // 스프라이트는 SpriteRenderer.color(공용 머티리얼 무오염), 메쉬는 MPB.
        private static void ApplyPropTint(GameObject prop, Color tint)
        {
            foreach (var sr in prop.GetComponentsInChildren<SpriteRenderer>())
                sr.color = tint;
            var mpb = new MaterialPropertyBlock();
            foreach (var r in prop.GetComponentsInChildren<Renderer>())
            {
                if (r is SpriteRenderer) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", tint);
                mpb.SetColor("_Color", tint);
                r.SetPropertyBlock(mpb);
            }
        }

        public void MarkGoalCollapsed(Vector2Int cell)
        {
            if (!_goalPropsByCell.TryGetValue(cell, out var prop) || prop == null) return;
            ApplyPropTint(prop, new Color(0.22f, 0.19f, 0.17f, 1f));
            // 주저앉음 — 실루엣 자체가 «무너졌다» 를 말하게 한다. 앵커가 바닥이 아니어도
            // 60% 스케일이면 붕괴 읽힘이 충분하고 이웃 골(2칸 거리)과 즉시 구분된다.
            prop.transform.localScale *= 0.6f;
        }

        // ── heart-stress-axis unit 1 rev 2 — 마음이 붉게 물든다 ────────────────────
        //
        // rev 1(머리 위 차오르는 바)·rev 2(주변 3×3 잠식) 둘 다 반려됐다. 바는 다른 바들과
        // 문법이 같아 임팩트가 없었고, 주변 타일 하이라이트는 «쓸모없다»(사용자, 2026-08-23).
        // 남은 결론: **마음 자체가 말한다.** 프랍이 스트레스만큼 붉어지고 심박에 맞춰 뛴다.
        //
        // ⚠ 프랍 틴트의 writer 가 둘이면 안 된다. 구 `SetGoalCrack`(그을림 4단계)은 호출을
        // 끊었다 — 같은 렌더러 색을 두 곳이 쓰면 마지막에 쓴 쪽이 이긴다.
        // 붕괴(`MarkGoalCollapsed`)는 판이 끝나는 순간이라 겹치지 않는다.
        public void SetGoalStressTint(Vector2Int cell, float stress01, float beatScale)
        {
            if (!_goalPropsByCell.TryGetValue(cell, out var prop) || prop == null || _tileSet == null) return;
            float k = Mathf.Clamp01(stress01);
            // 온전(흰색) → 스트레스 색. 심박은 밝기 배율이라 «붉게 물든 것이 뛴다» 로 읽힌다.
            var tint = Color.Lerp(Color.white, _tileSet.heartStressPropTint, k);
            float b = Mathf.Lerp(1f, beatScale, k);   // 스트레스가 낮으면 거의 안 뛴다
            tint.r *= b; tint.g *= b; tint.b *= b;
            tint.a = 1f;
            ApplyPropTint(prop, tint);
        }

        private GameObject PlaceStructure(PropData prop, int2 cell, BoardVisualPlan plan, MapThemeData theme)
        {
            var footprint = prop.Footprint;
            var placement = new PropPlacement(0, cell.x, cell.y, footprint.x, footprint.y, 0u, 0f, prop.visualScale, -1);
            return InstantiateProp(prop, placement, plan, theme, _structurePropsRoot);
        }

        private void ResetStructureVisualAnchors(in GeneratedMap map)
        {
            _hasGoalVisualAnchor = map.IsCreated;
            _goalVisualAnchorWorld = map.IsCreated
                ? CellCenterToWorld(map.goal.x, map.goal.y)
                : default;
            _spawnVisualAnchorsWorld.Clear();
            if (!map.IsCreated) return;

            for (int i = 0; i < map.spawns.Length; i++)
            {
                int2 spawn = map.spawns[i];
                _spawnVisualAnchorsWorld.Add(CellCenterToWorld(spawn.x, spawn.y));
            }
        }

        private static Vector3 ResolveVisualAnchor(GameObject instance, Vector3 fallback)
        {
            if (instance == null) return fallback;

            bool hasBounds = false;
            Bounds visualBounds = default;
            var renderers = instance.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled || IsNonVisualAnchorRenderer(renderer)) continue;
                if (!hasBounds)
                {
                    visualBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    visualBounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds ? visualBounds.center : fallback;
        }

        private static bool IsNonVisualAnchorRenderer(Renderer renderer)
        {
            string objectName = renderer.gameObject.name.ToLowerInvariant();
            return objectName.Contains("shadow") || objectName.Contains("marker") ||
                   objectName.Contains("footprint") || objectName.Contains("debug") ||
                   objectName.Contains("bounds");
        }

        // effect-tiles unit 1 — 효과 타일 페인트. Initialize(Clear 포함) 이후 호출 계약 (아니면 지워짐).
        public void SetEffectTile(Vector2Int cell, TileBase tile)
        {
            if (grid == null) return;
            EnsureEffectTilemap();
            _effectTilemap.SetTile(ToCell(cell), tile);
            // first-session-tutorial unit 26 — 칠한 셀을 기억한다. 튜토리얼이 "빛나는 타일" 하나를
            // 월드 마커로 지목하는 데 쓴다. 앵커를 미리 굳히지 않고 셀만 들고 조회 시점에
            // CellCenterToWorld 로 푸는 이유는 그리드 설정이 재빌드로 바뀔 수 있어서다.
            // 같은 셀 재페인트는 덮어쓰기이므로(BattleBridge._effectTilesByCell 과 동형) 중복을 막는다.
            if (tile != null) { if (!_effectTileCells.Contains(cell)) _effectTileCells.Add(cell); }
            else _effectTileCells.Remove(cell);
        }

        // unit 26 — 튜토리얼 조회용. 인덱스는 페인트 순서이고 의미는 없다(어느 하나면 된다).
        public int EffectTileCount => _effectTileCells.Count;

        public bool TryGetEffectTileAnchor(int index, out Vector3 worldPosition)
        {
            if (index >= 0 && index < _effectTileCells.Count)
            {
                var cell = _effectTileCells[index];
                worldPosition = CellCenterToWorld(cell.x, cell.y);
                return true;
            }

            worldPosition = default;
            return false;
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

        private void EnsureRangeTilemap()
        {
            if (_rangeTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("PlacementRangeTiles");
            go.transform.SetParent(grid.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.05f); // z-fight 방지(ground depth 평면 분리). placeable(-0.04)보다 카메라 쪽.
            _rangeTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _rangeTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 10000 : -12; // 드래그 중 lazy 생성 시 상승 반영(unit 6).
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null)
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        // unit 4 — 범위 표시의 두 스타일. 조준 페이즈 슬롯이 비어 있으면(기본) 전부 range*
        // 로 폴백해 기존 동작과 바이트 동일하다 — 스타일 분리는 에셋 할당으로 켜진다.
        private TileBase RangeTileFor(bool aimStyle)
            => aimStyle && _tileSet.aimRangeTile != null ? _tileSet.aimRangeTile : _tileSet.rangeTile;

        // 알파는 세기 배율(_rangeAlphaMul) 을 곱하기 전 기준값까지만 담는다 — 배율 적용은
        // 알파를 소유한 Update() 의 몫(unit 9 계약).
        private Color RangeTintColor(bool aimStyle)
        {
            // placement-thumb-occlusion — **유효성 면제는 에셋 유무가 아니라 aimStyle 이 결정한다.**
            // 초판은 `aimStyle && aimRangeTile != null` 로 묶어서, aimRangeTile 미배선 tileset(새 시즌을
            // CreateAssetMenu 로 만들면 기본 null)에서 조준 페이즈가 아래 invalid 분기로 떨어져 배치
            // 유효성의 적색을 상속했다 — range-preview unit 4 가 약속한 "슬롯 비면 기존 동작과 바이트
            // 동일" 폴백이 깨진다. aimStyle 은 "조준 채널이다"라는 **의미**로 쓰고, 에셋 유무는 타일·색
            // 폴백에만 쓴다(스타일 축과 유효성 축을 직교로 유지).
            if (aimStyle)
            {
                if (_tileSet.aimRangeTile == null)
                {
                    var fc = _tileSet.rangeColor; fc.a = _tileSet.rangePulseMaxAlpha; return fc;
                }
                var ac = _tileSet.aimRangeColor; ac.a = _tileSet.aimRangeAlpha; return ac;
            }
            // placement-thumb-occlusion unit 3 — 배치 불가면 적색 + 전이 순간 1회 플래시.
            // aimStyle 경로는 유효성을 모른다(드롭 후 방향 지정 채널). 단 스킬 조준/텔레그래프는
            // aimStyle=false 로 그려지므로 이 분기 밖이 아니라 **owner 전환 시 리셋**이 그들을 지킨다
            // (BattleBridge.ClearRange) — 색이 아니라 소유권이 경계다.
            if (_rangeInvalid)
            {
                var ic = _tileSet.rangeInvalidColor;
                ic.a = _tileSet.rangePulseMaxAlpha;
                float flashDur = _tileSet.rangeInvalidFlashSeconds;
                if (flashDur > 0f) // boost 0 은 아래 Lerp 가 항등이라 별도 가드 불요(0除만 막는다)
                {
                    float t = Mathf.Clamp01((Time.unscaledTime - _rangeInvalidSince) / flashDur);
                    float boost = _tileSet.rangeInvalidFlashBoost * (1f - t);
                    ic = Color.Lerp(ic, Color.white, boost);
                    ic.a = Mathf.Lerp(_tileSet.rangePulseMaxAlpha, 1f, boost);
                }
                return ic;
            }
            var c = _tileSet.rangeColor; c.a = _tileSet.rangePulseMaxAlpha; return c;
        }

        // placement-thumb-occlusion unit 3 — 배치 판정 유효성. **이 메서드가 유일한 소유자**다.
        // Set/ClearPlacementRange 는 절대 건드리지 않는다: SetPlacementRange 가 내부에서
        // ClearPlacementRange 를 먼저 부르므로(:903/:927) 거기서 리셋하면 셀이 바뀔 때마다
        // false→true 전이가 재발생해 무효 영역을 훑는 동안 플래시가 연발한다.
        // 세션 경계 리셋은 호출부(컨트롤러 ClearHover/ClearBoardScout, bridge ClearRange)가 명시적으로 한다.
        public void SetPlacementRangeValidity(bool valid)
        {
            bool invalid = !valid;
            if (invalid == _rangeInvalid) return; // 전이에만 반응 — 매 프레임 호출돼도 스팸 아님
            _rangeInvalid = invalid;
            if (invalid) _rangeInvalidSince = Time.unscaledTime;
        }

        // includeCenter — 배치 프리뷰는 중심 셀(유닛 위치)을 비우고, 스킬 AOE 는
        // 중심도 피해 범위라 포함한다 (range-preview unit 3).
        public void SetPlacementRange(Vector2Int center, int tileRange, bool includeCenter = false)
        {
            if (grid == null || _tileSet == null || _tileSet.rangeTile == null || tileRange <= 0) return;
            ClearPlacementRange();
            EnsureRangeTilemap();
            _rangeAlphaMul = 1f;
            _rangeAimStyle = false;
            for (int dx = -tileRange; dx <= tileRange; dx++)
            for (int dz = -tileRange; dz <= tileRange; dz++)
            {
                if (!includeCenter && dx == 0 && dz == 0) continue;
                var cell = new Vector2Int(center.x + dx, center.y + dz);
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _rangeTilemap.SetTile(ToCell(cell), _tileSet.rangeTile);
                _rangeCells.Add(cell);
            }
            ApplyRangeTint();
        }

        // defender-directional-volley unit 9 — 임의 셀 집합 점등(방향 레인). 사각 범위와
        // 같은 타일맵·수명·펄스를 공유하고 셀 목록만 호출부가 정한다. alphaMul = 세기
        // (방향 미정 십자는 흐리게, 선택된 레인은 또렷하게).
        // aimStyle — unit 4. 조준 페이즈(레인/착지셀)는 배치 단계와 다른 타일·색을 쓴다.
        // 드래그 프리뷰·스킬 조준/텔레그래프는 false 로 남아 기존 outline 그대로.
        public void SetPlacementCells(IReadOnlyList<Vector2Int> cells, float alphaMul = 1f, bool aimStyle = false)
        {
            if (grid == null || _tileSet == null || _tileSet.rangeTile == null || cells == null) return;
            ClearPlacementRange();
            EnsureRangeTilemap();
            _rangeAlphaMul = Mathf.Clamp01(alphaMul);
            _rangeAimStyle = aimStyle;
            var tile = RangeTileFor(aimStyle);
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _rangeTilemap.SetTile(ToCell(cell), tile);
                _rangeCells.Add(cell);
            }
            // 이 프레임의 Update() 가 이미 지났으면 새 타일이 옛 색으로 한 프레임 보인다 —
            // 두 스타일의 색이 다르므로 즉시 반영한다(리페인트는 방향이 바뀔 때만이라 저렴).
            ApplyRangeTint();
        }

        private void ApplyRangeTint()
        {
            if (_rangeTilemap == null) return;
            var c = RangeTintColor(_rangeAimStyle); c.a *= _rangeAlphaMul;
            _rangeTilemap.color = c;
        }

        public void ClearPlacementRange()
        {
            if (_rangeCells.Count == 0) return;
            if (_rangeTilemap != null)
                foreach (var cell in _rangeCells) _rangeTilemap.SetTile(ToCell(cell), null);
            _rangeCells.Clear();
        }

        // ── active-ally-zone unit 2 — 액티브 아군 장판 점등 ──────────────────────
        // 전용 타일맵이다. 조준 프리뷰(range)·맵 효과 타일(effect) 채널은 둘 다 단일 owner
        // set/clear 라 재사용하면 서로를 지운다(조준이 장판을 지우거나 그 반대).
        //
        // **칸별 refcount 필수**: 장판은 동시에 여러 장 존재할 수 있어서, 단순 set/clear 를
        // 복사하면 먼저 만료된 장판이 겹친 칸을 지우고 살아 있는 장판의 발자국이 사라진다.
        private Tilemap _zoneTilemap;
        private readonly Dictionary<Vector2Int, int> _zoneCellRefs = new Dictionary<Vector2Int, int>();

        public void AddZoneCells(IReadOnlyList<Vector2Int> cells)
        {
            if (grid == null || _tileSet == null || _tileSet.rangeTile == null || cells == null) return;
            EnsureZoneTilemap();
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _zoneCellRefs.TryGetValue(cell, out int refs);
                _zoneCellRefs[cell] = refs + 1;
                if (refs == 0) _zoneTilemap.SetTile(ToCell(cell), _tileSet.rangeTile);
            }
        }

        public void RemoveZoneCells(IReadOnlyList<Vector2Int> cells)
        {
            if (_zoneTilemap == null || cells == null) return;
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (!_zoneCellRefs.TryGetValue(cell, out int refs)) continue;
                if (refs <= 1)
                {
                    _zoneCellRefs.Remove(cell);
                    _zoneTilemap.SetTile(ToCell(cell), null); // 마지막 참조만 실제로 끈다
                }
                else _zoneCellRefs[cell] = refs - 1;
            }
        }

        public void ClearZoneCells()
        {
            _zoneCellRefs.Clear();
            if (_zoneTilemap != null) _zoneTilemap.ClearAllTiles();
        }

        // ── ultimate-leap unit 4 — 착지 예고 타일 ────────────────────────────────
        // **또 하나의 전용 타일맵이다.** range 채널을 공유할 수 없다: 그쪽은 `SetPlacementRange`/
        // `SetPlacementCells` 가 매번 `ClearPlacementRange()` 로 시작하는 단일 owner set/clear 라,
        // 예고 2초 동안 플레이어가 유닛을 드래그하면 배치 프리뷰와 예고가 서로를 지운다.
        // (예고 중 배치는 막을 수 없다 — 유닛을 빼고 다시 놓는 것이 이 스킬의 놀이다.)
        //
        // zone 처럼 refcount 는 두지 않는다: 궁극기는 생존당 1회라 동시 예고가 존재할 수 없다.
        // 두 번째 소비처가 생기면 그 spec 이 refcount 를 붙인다(제약 8).
        private Tilemap _telegraphTilemap;
        private readonly List<Vector2Int> _telegraphCells = new List<Vector2Int>();
        private bool _telegraphFallbackWarned; // 배선 누락 경고 1회 제한(매 발동 스팸 방지)

        public void SetTelegraphCells(IReadOnlyList<Vector2Int> cells)
        {
            if (grid == null || _tileSet == null || cells == null) return;
            // **전용 채움 타일을 쓴다.** 다른 채널 타일을 빌리면 그쪽 저작 의도에 종속된다 —
            // `placeableTile`(슬랩)은 자체 색이 회색(0.80)이라 tint 를 곱하면 색이 죽어 "어두운
            // dim" 으로만 보이고, `rangeTile` 은 격자 outline 이라 면적이 없어 주변시로 안 읽힌다.
            // `telegraphTile` 은 흰색 solid + TileFlags.None 이라 아래 tint 가 원색으로 실린다.
            // 폴백은 "안 보이는 것보다 낫다" 수준의 열화 경로다.
            var tile = _tileSet.telegraphTile;
            if (tile == null)
            {
                // 폴백은 남기되(예고가 아예 안 뜨면 회피 불가 = 불공정) **한 번은 시끄럽게** 알린다.
                // 실제로 이 조용한 열화가 "색이 안 먹는다" 로 위장해 색·정렬·머티리얼·스프라이트를
                // 차례로 의심하게 만들었다 — 원인은 tileSet 하나의 배선 누락이었다.
                // ⚠ tileSet 은 여러 개다(예: Generated/ 아래 실사용본). **전부** 배선해야 한다.
                if (!_telegraphFallbackWarned)
                {
                    _telegraphFallbackWarned = true;
                    Debug.LogWarning($"[TilemapMapView] '{_tileSet.name}' 에 telegraphTile 이 없어 " +
                        "placeable/range 타일로 폴백한다 — 그 타일들은 자체 색이 있어 예고 tint 가 죽는다. " +
                        "TileSetData.telegraphTile 을 배선하라.", this);
                }
                tile = _tileSet.placeableTile != null ? _tileSet.placeableTile : _tileSet.rangeTile;
            }
            if (tile == null) return;
            ClearTelegraphCells();
            EnsureTelegraphTilemap();
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _telegraphTilemap.SetTile(ToCell(cell), tile);
                _telegraphCells.Add(cell);
            }
        }

        public void ClearTelegraphCells()
        {
            if (_telegraphCells.Count == 0) return;
            if (_telegraphTilemap != null)
                foreach (var cell in _telegraphCells) _telegraphTilemap.SetTile(ToCell(cell), null);
            _telegraphCells.Clear();
        }

        private void EnsureTelegraphTilemap()
        {
            if (_telegraphTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("LandingTelegraphTiles");
            go.transform.SetParent(grid.transform, false);
            // zone(-0.03) 위, range(-0.05) 아래 — 예고는 배치 프리뷰에 가려지지 않아야 한다.
            go.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            _telegraphTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _telegraphTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            // placeable(9998/-13) 바로 위 — 예고는 배치 하이라이트에 가리면 안 된다.
            // ⚠ 이 값을 바꾸면 `SetPlacementHighlightAboveUnits` 의 같은 쌍도 함께 바꿔야 한다.
            r.sortingOrder = _highlightAbove ? 9999 : -13;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
            _telegraphTilemap.color = landingTelegraphColor;
        }

        // 생성 시 1회 대입만 하면 **Play 중 인스펙터 튜닝이 안 먹는다** — 타일맵 GameObject 는 맵
        // 리빌드까지 살아남으므로 첫 생성 때의 색이 굳는다. range 가 `ApplyRangeTint()` 를 매 프레임
        // 호출하는 것과 같은 이유로 여기도 매 프레임 반영한다(셀이 있을 때만 — 평소 비용 0).
        private void ApplyTelegraphTint()
        {
            if (_telegraphTilemap == null) return;
            if (_telegraphTilemap.color != landingTelegraphColor)
                _telegraphTilemap.color = landingTelegraphColor;
        }

        private void EnsureZoneTilemap()
        {
            if (_zoneTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("AllyZoneTiles");
            go.transform.SetParent(grid.transform, false);
            // z-fight 방지 + 깊이 순서: effect(-15) 위, placeable(-0.04)/range(-0.05) 아래.
            go.transform.localPosition = new Vector3(0f, 0f, -0.03f);
            _zoneTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _zoneTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 9997 : -14;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용(range/placeable 과 동일)
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
            _zoneTilemap.color = allyZoneColor;
        }

        // placement-eligible-tile-highlight unit 1 — 배치 가능 셀 밝은 하이라이트(전용 타일맵, range 와 분리).
        private void EnsurePlaceableTilemap()
        {
            if (_placeableTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("PlacementHighlightTiles");
            go.transform.SetParent(grid.transform, false);
            // z-fight 방지: ground(TileShadowReceive = depth write)와 coplanar 면 카메라 이동 중 자글거림.
            // grid 는 90°X 회전 → local -Z 가 카메라 쪽(world +Y). 살짝 띄워 깊이 평면 분리(셀 정렬 영향 없음).
            go.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            _placeableTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _placeableTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 9998 : -13; // 정적 −13(effect −15 위·range −12 아래) / 드래그 상승 9998.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // 검증된 반투명 tint 경로 재사용(range 와 동일).
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        // 배치 가능 셀 집합을 은은한 fill+림으로 밝힌다. 균일 tint(per-cell 색 없음). 색/알파는 Update() 소유.
        public void SetPlacementHighlight(IReadOnlyList<Vector2Int> placeable)
        {
            if (grid == null || _tileSet == null || _tileSet.placeableTile == null || placeable == null) return;
            EnsurePlaceableTilemap();
            if (!_placeableActive)
            {
                _placeableActive = true;
                _placeableShowTime = Time.unscaledTime;
                var c0 = _tileSet.placeableColor; c0.a = 0f;
                _placeableTilemap.color = c0; // 첫 프레임 흰 불투명 번쩍 방지(Update 가 알파 세팅 전)
            }
            _placeableTilemap.ClearAllTiles();
            for (int i = 0; i < placeable.Count; i++)
            {
                var cell = placeable[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _placeableTilemap.SetTile(ToCell(cell), _tileSet.placeableTile);
            }
            // 리프레시(_placeableActive 유지)면 showTime 안 리셋 → 재페이드 없음.
        }

        public void ClearPlacementHighlight()
        {
            if (_placeableTilemap != null) _placeableTilemap.ClearAllTiles();
            _placeableActive = false;
        }

        // first-run-tutorial unit 1 — 배치 **불가** 칸 하이라이트. EnsurePlaceableTilemap 과 같은 관용구.
        private void EnsureBlockedTilemap()
        {
            if (_blockedTilemap != null) return;
            if (grid == null) return;
            var go = new GameObject("PlacementBlockedTiles");
            go.transform.SetParent(grid.transform, false);
            // placeable(-0.04)과 겹치지 않게 한 겹 더 띄운다 — 두 하이라이트를 동시에 켜는 구간이 있다.
            go.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            _blockedTilemap = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            _blockedTilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            r.sortingOrder = _highlightAbove ? 9998 : -13;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (overlayTilemap != null) // placeable 과 같은 검증된 반투명 tint 경로.
            {
                var or = overlayTilemap.GetComponent<TilemapRenderer>();
                if (or != null) r.sharedMaterial = or.sharedMaterial;
            }
        }

        // 배치 불가 셀 집합을 칠한다. blockedTile 미할당이면 조용히 no-op(placeable 과 같은 규약).
        public void SetBlockedHighlight(IReadOnlyList<Vector2Int> blocked)
        {
            if (grid == null || _tileSet == null || _tileSet.blockedTile == null || blocked == null) return;
            EnsureBlockedTilemap();
            if (!_blockedActive)
            {
                _blockedActive = true;
                _blockedShowTime = Time.unscaledTime;
                var c0 = _tileSet.blockedColor; c0.a = 0f;
                _blockedTilemap.color = c0; // 첫 프레임 흰 불투명 번쩍 방지
            }
            _blockedTilemap.ClearAllTiles();
            for (int i = 0; i < blocked.Count; i++)
            {
                var cell = blocked[i];
                if (cell.x < 0 || cell.x >= _gridSize.x || cell.y < 0 || cell.y >= _gridSize.y) continue;
                _blockedTilemap.SetTile(ToCell(cell), _tileSet.blockedTile);
            }
        }

        public void ClearBlockedHighlight()
        {
            if (_blockedTilemap != null) _blockedTilemap.ClearAllTiles();
            _blockedActive = false;
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
                PropInstanceUtil.DisablePropDebugMarkers(inst);
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
