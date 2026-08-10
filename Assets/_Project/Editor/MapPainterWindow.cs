using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.EditorTools
{
    // map-painter-tool unit 0 — MapDocument(수동 맵)을 격자에서 직접 칠해 만드는 에디터 창.
    // 손으로 다루는 1차 데이터는 Walk/Place + spawns + goal 뿐. mergeDegree/chokepoint 는 Bake 시 계산(unit 1).
    // 좌표 규약: y=0 이 하단, 화면 위쪽이 y=H-1 (런타임 스폰 상단/골 하단 규약과 일치).
    public class MapPainterWindow : EditorWindow
    {
        private enum Tool { Road, Buildable, Deco, Spawn, Goal, PlaceMask, Structure }

        private const float Cell = 26f;

        private int _w = 15, _h = 10;
        private MapTileType[] _tiles;
        private byte[] _placeMask;   // placement-mask unit 2/4 — 셀이 여는 배치 층 비트. 타일 종류와 직교.
        private PlacementLayer _maskBrushLayer = PlacementLayer.Ground;   // unit 4 — 어느 층을 칠할지
        private bool _maskPaintValue;   // 드래그 = 시작 셀의 반전값으로 set (재토글 깜빡임 방지 — Spawn/Goal 이 click-only 인 이유와 동일 함정)
        private bool _maskStrokePrimed; // 스트로크 시작값이 이번 드래그에서 잡혔나 — 격자 밖 MouseDown 후 진입 드래그가 직전 스트로크 잔존값으로 칠하는 엣지 방지
        private readonly List<Vector2Int> _spawns = new();
        private readonly List<Vector2Int> _goals = new();   // multi-goal-map — 골 1~4
        // battle-structures unit 3 — 거점 저작(본능 + 적 마음).
        //
        // **방어 마음은 여기 넣지 않는다.** 현행 9장이 전부 goals[] 로 방어 골을 저작하고
        // 라이브 타워가 이미 DefenderCore 진영이라, structures[] 로 옮기면 «콘텐츠 이관 0»
        // 이 깨지고 골이 또 두 벌이 된다(이 스펙이 상대해 온 바로 그 병). goals[] 가 방어
        // 마음의 정본이고, (Defender, Core) 조합은 Validate 가 에러로 막는다.
        private readonly List<StructureEntry> _structures = new();
        private StructureSide _structureSide = StructureSide.Enemy;
        private StructureData _structureData;
        private MapDocument _target;
        private Tool _tool = Tool.Road;
        private int _newW = 15, _newH = 10;

        [MenuItem("Window/Wassup/Map Painter")]
        public static void Open() => GetWindow<MapPainterWindow>("Map Painter");

        private void OnEnable()
        {
            if (_tiles == null) NewGrid(_w, _h);
        }

        private int Idx(int x, int y) => y * _w + x;
        private bool InBounds(int x, int y) => x >= 0 && x < _w && y >= 0 && y < _h;

        private void NewGrid(int w, int h)
        {
            _w = Mathf.Max(2, w);
            _h = Mathf.Max(2, h);
            _tiles = new MapTileType[_w * _h];
            for (int i = 0; i < _tiles.Length; i++) _tiles[i] = MapTileType.Place;
            ResetMaskToDerived();
            _spawns.Clear();
            _goals.Clear();
            _structures.Clear();
        }

        // placement-mask unit 2 — 파생값 = tiles==Place. 마스크 브러시로 만든 차이만 이 값과 달라진다.
        private byte DerivedMask(int i) => PlacementLayers.Derive(_tiles[i]);   // 런타임과 같은 단일 파생 정의

        private void ResetMaskToDerived()
        {
            _placeMask = new byte[_w * _h];
            for (int i = 0; i < _placeMask.Length; i++) _placeMask[i] = DerivedMask(i);
        }

        // 파생 비트를 OR 로만 더한다(기존 저작 보존). 층이 추가된 뒤 옛 문서를 되살리는 경로.
        private void FillMissingDerivedLayers()
        {
            EnsureMask();
            int added = 0;
            for (int i = 0; i < _placeMask.Length; i++)
            {
                byte after = (byte)(_placeMask[i] | DerivedMask(i));
                if (after != _placeMask[i]) { _placeMask[i] = after; added++; }
            }
            Debug.Log($"[MapPainter] 빠진 층 채움 — {added}칸에 파생 층 추가(기존 저작 보존). Bake 해야 반영된다.");
        }

        // 도메인 리로드 등으로 마스크가 격자와 어긋나면 파생값으로 재생성.
        private void EnsureMask()
        {
            if (_placeMask == null || _placeMask.Length != _w * _h) ResetMaskToDerived();
        }

        private void LoadFrom(MapDocument doc)
        {
            if (doc == null) return;
            _w = Mathf.Max(2, doc.Width);
            _h = Mathf.Max(2, doc.Height);
            _tiles = new MapTileType[_w * _h];
            var t = doc.Tiles;
            for (int i = 0; i < _tiles.Length; i++)
                _tiles[i] = (t != null && i < t.Count) ? t[i] : MapTileType.Place;
            // 마스크: doc 저작본(길이 일치) 채택, 아니면 파생 (런타임 ToGeneratedMap 폴백과 같은 규칙).
            var dm = doc.PlaceMask;
            ResetMaskToDerived();
            if (dm != null && dm.Count == _w * _h)
                for (int i = 0; i < _placeMask.Length; i++) _placeMask[i] = PlacementLayers.Sanitize(dm[i]);
            _spawns.Clear();
            if (doc.Spawns != null)
                foreach (var s in doc.Spawns) _spawns.Add(new Vector2Int(s.x, s.y));
            _goals.Clear();
            if (doc.Goals != null && doc.Goals.Count > 0)
                foreach (var g in doc.Goals) _goals.Add(new Vector2Int(g.x, g.y));
            else
                _goals.Add(new Vector2Int(doc.Goal.x, doc.Goal.y));   // 레거시 단일골 폴백
            _structures.Clear();
            if (doc.Structures != null)
                foreach (var s in doc.Structures) _structures.Add(s);
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawStructureBrushBar();
            EditorGUILayout.Space(4);
            DrawGrid();
            EditorGUILayout.Space(4);
            // battle-structures unit 3 — 모드는 파생 배지다(드롭다운이 아니다).
            var mode = StructureAuthoringRules.DeriveMode(CountEnemyCores());
            string modeLabel = mode == MapMode.Siege ? "공성"
                : mode == MapMode.Invalid ? "에러(적 마음 2+)" : "침략";
            EditorGUILayout.LabelField(
                $"{_w}×{_h}  spawns={_spawns.Count}  goals={_goals.Count}  거점={_structures.Count}  모드={modeLabel}",
                EditorStyles.miniLabel);
            DrawValidationAndBake();
        }

        // battle-structures unit 0 — «골 안정도» 저작 컬럼을 제거했다. 그 값을 읽던 런타임
        // 경로(SpawnGoalEntities)가 없어져 저작해도 아무 일이 일어나지 않는 입력란이었다.
        // 거점 체력 저작은 unit 3 의 StructureData 가 맡는다.

        // placement-mask unit 4 — 저작된(파생과 상이한) 셀의 테두리 색 = 그 칸이 여는 층.
        private static Color AuthoredLayerColor(byte bits)
        {
            bool g = (bits & (byte)PlacementLayer.Ground) != 0;
            bool p = (bits & (byte)PlacementLayer.Path) != 0;
            if (g && p) return new Color(1f, 1f, 1f, 0.95f);          // 두 층 동시 개방
            if (g) return new Color(0.3f, 0.9f, 0.95f, 0.95f);        // 지면
            if (p) return new Color(0.95f, 0.45f, 0.9f, 0.95f);       // 경로
            return new Color(0.9f, 0.25f, 0.25f, 0.95f);              // 저작으로 닫은 칸
        }

        // placement-mask unit 4 — 셀 층 비트 → 사람이 읽는 라벨(경고/로그용).
        private static string LayerLabel(byte bits)
        {
            bool g = (bits & (byte)PlacementLayer.Ground) != 0;
            bool p = (bits & (byte)PlacementLayer.Path) != 0;
            if (g && p) return "지면+경로";
            if (g) return "지면";
            if (p) return "경로";
            return "없음";
        }

        private void RemoveGoal(Vector2Int cell)
        {
            int i = _goals.IndexOf(cell);
            if (i < 0) return;
            _goals.RemoveAt(i);
        }

        private void DrawValidationAndBake()
        {
            var errors = Validate();
            bool ok = errors.Count == 0;

            // placement-mask unit 2 — 마스크는 무제약(에러 없음). 저작 실수 가능성만 warning 으로.
            EnsureMask();
            var warnings = new List<string>();
            // 술어는 "파생과 상이"다. `!= 0` 으로 두면 Walk→Path 파생 때문에 모든 스폰·골이 상시 경고를
            // 띄워(스폰·골은 정의상 Walk 셀) 가드 신호가 죽는다 (unit 4 리뷰 M-2).
            // 참고: 런타임은 스폰·골 칸의 층을 어차피 닫으므로(BattleBridge 불변식) 이건 저작 의도 확인용이다.
            foreach (var s in _spawns)
                if (InBounds(s.x, s.y) && _placeMask[Idx(s.x, s.y)] != DerivedMask(Idx(s.x, s.y)))
                    warnings.Add($"스폰 ({s.x},{s.y}) 마스크가 파생과 다름 [{LayerLabel(_placeMask[Idx(s.x, s.y)])}] — 런타임은 스폰 칸을 닫는다");
            foreach (var g in _goals)
                if (InBounds(g.x, g.y) && _placeMask[Idx(g.x, g.y)] != DerivedMask(Idx(g.x, g.y)))
                    warnings.Add($"골 ({g.x},{g.y}) 마스크가 파생과 다름 [{LayerLabel(_placeMask[Idx(g.x, g.y)])}] — 런타임은 골 칸을 닫는다");
            if (warnings.Count > 0)
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    foreach (var w in warnings)
                        EditorGUILayout.LabelField("⚠ " + w, EditorStyles.miniLabel);

            var box = new GUIStyle(EditorStyles.helpBox);
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = ok ? new Color(0.5f, 0.9f, 0.5f) : new Color(0.95f, 0.55f, 0.55f);
            using (new EditorGUILayout.VerticalScope(box))
            {
                GUI.backgroundColor = prev;
                if (ok) EditorGUILayout.LabelField("✓ 유효 — Bake 가능", EditorStyles.boldLabel);
                else
                {
                    EditorGUILayout.LabelField("✗ 검증 실패", EditorStyles.boldLabel);
                    foreach (var e in errors) EditorGUILayout.LabelField("  • " + e, EditorStyles.miniLabel);
                }
            }
            GUI.backgroundColor = prev;

            using (new EditorGUI.DisabledScope(!ok))
                if (GUILayout.Button(_target != null ? $"Bake → {_target.name}" : "Bake → 새 MapDocument…", GUILayout.Height(28)))
                    Bake();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("New", EditorStyles.miniLabel, GUILayout.Width(26));
                _newW = EditorGUILayout.IntField(_newW, GUILayout.Width(34));
                _newH = EditorGUILayout.IntField(_newH, GUILayout.Width(34));
                if (GUILayout.Button("Blank", EditorStyles.toolbarButton, GUILayout.Width(48)))
                    NewGrid(_newW, _newH);

                GUILayout.Space(10);
                var t = (MapDocument)EditorGUILayout.ObjectField(_target, typeof(MapDocument), false, GUILayout.Width(170));
                _target = t;
                using (new EditorGUI.DisabledScope(_target == null))
                    if (GUILayout.Button("Load", EditorStyles.toolbarButton, GUILayout.Width(44)))
                        LoadFrom(_target);

                GUILayout.FlexibleSpace();
                _tool = (Tool)GUILayout.Toolbar((int)_tool,
                    new[] { "Road", "Buildable", "Deco", "Spawn", "Goal", "Mask", "거점" }, EditorStyles.toolbarButton);
                // placement-mask unit 4 — Mask 브러시가 칠할 층. 유닛 SO 의 placementLayers 와 같은 축이다.
                using (new EditorGUI.DisabledScope(_tool != Tool.PlaceMask))
                    _maskBrushLayer = GUILayout.Toolbar(_maskBrushLayer == PlacementLayer.Path ? 1 : 0,
                        new[] { "지면", "경로" }, EditorStyles.toolbarButton, GUILayout.Width(72)) == 1
                        ? PlacementLayer.Path : PlacementLayer.Ground;
                // 층이 늘어난 뒤 옛 문서를 열면 새 층 비트가 통째로 비어 있다(그 층 유닛이 놓일 곳 0).
                // 리셋은 기존 저작을 지우므로, 빠진 층만 OR 로 채우는 비파괴 경로를 따로 둔다.
                if (GUILayout.Button("빠진 층 채우기", EditorStyles.toolbarButton, GUILayout.Width(88)))
                    FillMissingDerivedLayers();
                if (GUILayout.Button("Mask=파생 리셋", EditorStyles.toolbarButton, GUILayout.Width(96)))
                    ResetMaskToDerived();
            }
        }

        // battle-structures — 거점 브러시 전용 행. 툴바 한 줄에 밀어넣었던 시절엔 툴 버튼 7개
        // 뒤로 밀려 창 폭에 따라 **잘려서 안 보였다**(SO 를 물릴 수 없으면 브러시가 아무것도
        // 안 한다 → «본능을 설치하는 방법을 못 찾겠다» 가 정확한 증상이었다).
        private void DrawStructureBrushBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool active = _tool == Tool.Structure;
                GUILayout.Label(active ? "거점 브러시 ●" : "거점 브러시 ○",
                    EditorStyles.miniBoldLabel, GUILayout.Width(84));
                using (new EditorGUI.DisabledScope(!active))
                {
                    GUILayout.Label("편", EditorStyles.miniLabel, GUILayout.Width(16));
                    _structureSide = GUILayout.Toolbar(_structureSide == StructureSide.Defender ? 0 : 1,
                        new[] { "방어", "적" }, GUILayout.Width(80)) == 0
                        ? StructureSide.Defender : StructureSide.Enemy;
                    GUILayout.Label("SO", EditorStyles.miniLabel, GUILayout.Width(20));
                    _structureData = (StructureData)EditorGUILayout.ObjectField(
                        _structureData, typeof(StructureData), false, GUILayout.Width(180));
                    GUILayout.Label(
                        _structureData != null
                            ? $"= {(_structureData.kind == StructureKind.Core ? "마음 1×1" : "본능 3×3")}"
                            : "← StructureData 를 물려라(마음/본능은 SO 의 kind 가 정한다)",
                        EditorStyles.miniLabel);
                }
                GUILayout.FlexibleSpace();
                GUILayout.Label($"배치 {_structures.Count}기", EditorStyles.miniLabel, GUILayout.Width(60));
                using (new EditorGUI.DisabledScope(_structures.Count == 0))
                    if (GUILayout.Button("전체 제거", EditorStyles.miniButton, GUILayout.Width(66)))
                        _structures.Clear();
            }
        }

        private void DrawGrid()
        {
            if (_tiles == null) return;
            EnsureMask();
            Rect area = GUILayoutUtility.GetRect(_w * Cell, _h * Cell, GUILayout.ExpandWidth(false));

            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    // 화면 위쪽 = y=H-1 (y=0 하단)
                    var r = new Rect(area.x + x * Cell, area.y + (_h - 1 - y) * Cell, Cell - 1f, Cell - 1f);
                    var cell = new Vector2Int(x, y);
                    int i = Idx(x, y);
                    Color c = ColorFor(_tiles[i]);
                    EditorGUI.DrawRect(r, c);

                    if (_goals.Contains(cell))
                    {
                        EditorGUI.DrawRect(r, new Color(0.85f, 0.7f, 0.15f));
                        GUI.Label(r, "G", CenterLabel);
                    }
                    else if (_spawns.Contains(cell))
                    {
                        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 3f), new Color(0.2f, 0.5f, 0.95f));
                        GUI.Label(r, "S", CenterLabel);
                    }

                    // placement-mask unit 2/4 — 마스크 오버레이. **테두리 = 손댄 칸(파생과 상이)** 이고
                    // **색 = 그 칸이 여는 층**이다(지면=시안 / 경로=마젠타 / 둘 다=흰 / 닫힘=적).
                    // 파생 일치 셀에는 아무것도 안 그린다 — 파생은 타일 색이 이미 말해주고(Place=흙 / Walk=회),
                    // Walk→Path 파생 때문에 도로 전체가 테두리로 덮이면 신호가 죽는다 (unit 4 리뷰 M-3).
                    // goal/spawn 채움 **뒤에** 그린다 — 그 셀에서 테두리가 가려지면 저작 중 인지가 죽는다.
                    bool differs = _placeMask[i] != DerivedMask(i);
                    if (differs) DrawMaskBorder(r, AuthoredLayerColor(_placeMask[i]));
                }
            }

            DrawStructureOverlay(area);
            HandlePaint(area);
        }

        // battle-structures — 거점 오버레이. 이게 없던 동안은 거점을 찍어도 화면이 그대로였다
        // (배치 개수만 라벨에 늘었다) — «설치했는데 아무 일도 안 일어난다» 로 보인다.
        // footprint 전체를 반투명으로 덮고 중심에 편·종류를 적는다: 3×3 본능의 점유 범위가
        // 곧 통행 차단 범위(계약 13)이므로 저작 중에 그게 보여야 한다.
        private void DrawStructureOverlay(Rect area)
        {
            foreach (var st in _structures)
            {
                if (st.data == null) continue;
                bool instinct = st.data.kind == StructureKind.Instinct;
                int half = (instinct ? StructurePlacements.InstinctFootprint
                                     : StructurePlacements.CoreFootprint) / 2;
                bool enemy = st.side == StructureSide.Enemy;
                // 적 = 붉은 계열 · 방어 = 청록 계열. 본능은 채움(점유), 마음은 테두리(비차단).
                Color fill = enemy ? new Color(0.9f, 0.25f, 0.2f, 0.35f)
                                   : new Color(0.2f, 0.8f, 0.75f, 0.35f);
                Color edge = enemy ? new Color(1f, 0.4f, 0.3f) : new Color(0.3f, 0.95f, 0.9f);

                for (int dy = -half; dy <= half; dy++)
                    for (int dx = -half; dx <= half; dx++)
                    {
                        int cx = st.cell.x + dx, cy = st.cell.y + dy;
                        if (!InBounds(cx, cy)) continue;
                        var rr = CellRect(area, cx, cy);
                        if (instinct) EditorGUI.DrawRect(rr, fill);
                    }

                var center = CellRect(area, st.cell.x, st.cell.y);
                if (!InBounds(st.cell.x, st.cell.y)) continue;
                DrawMaskBorder(center, edge);
                GUI.Label(center, enemy ? (instinct ? "敵I" : "敵C") : (instinct ? "防I" : "防C"), CenterLabel);
            }
        }

        private Rect CellRect(Rect area, int x, int y)
            => new Rect(area.x + x * Cell, area.y + (_h - 1 - y) * Cell, Cell - 1f, Cell - 1f);

        private static void DrawMaskBorder(Rect r, Color c)
        {
            const float t = 2f;
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, t, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static Color ColorFor(MapTileType t)
        {
            switch (t)
            {
                case MapTileType.Walk:  return new Color(0.34f, 0.34f, 0.38f);
                case MapTileType.Place: return new Color(0.55f, 0.5f, 0.38f);
                case MapTileType.Deco:  return new Color(0.25f, 0.42f, 0.25f);
                default:                return new Color(0.2f, 0.3f, 0.4f); // Env
            }
        }

        private static GUIStyle _centerLabel;
        private static GUIStyle CenterLabel =>
            _centerLabel ??= new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter };

        private void HandlePaint(Rect area)
        {
            var e = Event.current;
            if (e.type == EventType.MouseUp) { _maskStrokePrimed = false; return; }   // 스트로크 종료
            if (e.type != EventType.MouseDown && e.type != EventType.MouseDrag) return;
            if (!area.Contains(e.mousePosition)) return;

            int x = Mathf.FloorToInt((e.mousePosition.x - area.x) / Cell);
            int yScreen = Mathf.FloorToInt((e.mousePosition.y - area.y) / Cell);
            int y = _h - 1 - yScreen;
            if (!InBounds(x, y)) return;

            ApplyTool(x, y, e.type == EventType.MouseDown);
            e.Use();
            Repaint();
        }

        private void ApplyTool(int x, int y, bool isDown)
        {
            EnsureMask();
            int idx = Idx(x, y);
            var cell = new Vector2Int(x, y);
            switch (_tool)
            {
                // 타일 브러시는 셀 마스크를 파생값으로 추종시킨다 — 파생과 상이한 마스크는
                // Mask 브러시로 칠한 셀에만 생존 (stale mask 방지 계약, placement-mask unit 2).
                case Tool.Road:
                    _tiles[idx] = MapTileType.Walk;
                    _placeMask[idx] = DerivedMask(idx);
                    break;
                case Tool.Buildable:
                    _tiles[idx] = MapTileType.Place;
                    _placeMask[idx] = DerivedMask(idx);
                    _spawns.Remove(cell);
                    RemoveGoal(cell);
                    break;
                case Tool.Deco:
                    _tiles[idx] = MapTileType.Deco; // 장식(배치·이동 불가)
                    _placeMask[idx] = DerivedMask(idx);
                    _spawns.Remove(cell);
                    RemoveGoal(cell);
                    break;
                case Tool.Spawn:
                    if (!isDown) return; // 토글은 클릭만
                    if (_spawns.Contains(cell)) _spawns.Remove(cell);
                    else if (_spawns.Count < 4)
                    {
                        _tiles[idx] = MapTileType.Walk; // 스폰은 Walk 셀
                        _placeMask[idx] = DerivedMask(idx);   // 타일 변경 → 파생 추종
                        _spawns.Add(cell);
                    }
                    break;
                case Tool.Goal:
                    if (!isDown) return; // 토글은 클릭만
                    if (_goals.Contains(cell)) RemoveGoal(cell);
                    else if (_goals.Count < 4)
                    {
                        _tiles[idx] = MapTileType.Walk; // 골은 Walk 셀
                        _placeMask[idx] = DerivedMask(idx);   // 타일 변경 → 파생 추종
                        _goals.Add(cell);
                    }
                    break;
                case Tool.Structure:
                    if (!isDown) return; // 토글은 클릭만 (스폰·골과 같은 이유)
                    {
                        int existing = _structures.FindIndex(s => s.cell == cell);
                        if (existing >= 0) { _structures.RemoveAt(existing); break; }
                        if (_structureData == null)
                        {
                            Debug.LogWarning("[MapPainter] 거점 브러시: StructureData 를 먼저 물려라.");
                            break;
                        }
                        _structures.Add(new StructureEntry
                        {
                            cell = cell,
                            side = _structureSide,
                            data = _structureData,
                        });
                    }
                    break;
                case Tool.PlaceMask:
                    // 드래그 = 시작 셀의 반전값으로 set (같은 셀 MouseDrag 재토글 깜빡임 방지).
                    // 격자 밖 MouseDown → 진입 드래그도 첫 셀에서 시작값을 잡는다 (MINOR-5).
                    byte bit = (byte)_maskBrushLayer;
                    if (isDown || !_maskStrokePrimed)
                    {
                        _maskPaintValue = (_placeMask[idx] & bit) == 0;   // 시작 셀 기준 반전
                        _maskStrokePrimed = true;
                    }
                    _placeMask[idx] = _maskPaintValue
                        ? (byte)(_placeMask[idx] | bit)
                        : (byte)(_placeMask[idx] & ~bit);
                    break;
            }
        }

        // ── Validation (unit 1) — 런타임 계약과 일치 ──────────────────────────────
        private bool IsWalk(int x, int y) => InBounds(x, y) && _tiles[Idx(x, y)] == MapTileType.Walk;

        // battle-structures unit 3(투트랙 리뷰 M-a 정정) — 거점 규칙은 런타임
        // StructureAuthoringRules 가 단일 소유한다. 여기 인라인돼 있던 구현은 에디터
        // 어셈블리에 갇혀 테스트가 못 보고 인스펙터 우회를 못 막았다.
        private int CountEnemyCores() => StructureAuthoringRules.CountEnemyCores(_structures);

        private List<string> Validate()
        {
            var errs = new List<string>();
            if (_tiles == null) { errs.Add("격자 없음"); return errs; }

            // battle-structures unit 3 — 스폰·골 개수 규칙은 **모드에 따라 다르다**(공성은
            // spawns 저작 금지·멀티골 금지). 규칙은 런타임 순수 함수 하나가 소유한다 —
            // 여기 인라인하면 툴과 런타임이 갈린다.
            StructureAuthoringRules.ValidateMode(
                CountEnemyCores(), _goals.Count, _spawns.Count, errs);
            StructureAuthoringRules.ValidateStructures(_structures, _w, _h, errs, _tiles);
            foreach (var s in _spawns)
                if (!IsWalk(s.x, s.y)) errs.Add($"스폰 ({s.x},{s.y}) 이 Walk 아님");
            foreach (var g in _goals)
                if (!IsWalk(g.x, g.y)) errs.Add($"골 ({g.x},{g.y}) 이 Walk 아님");

            // (2×2 walk 블록 금지는 map-painter-tool unit 4 에서 철회 — 폭 1 은 저작 규칙이었지
            // 런타임 요구가 아니다. flow field/CellTrim 은 임의 폭 walkable 에서 성립.)

            // BFS 연결성: goals 전체에서 Walk 로 flood(멀티-소스), 각 스폰이 아무 골이든 도달 확인.
            // 리뷰 A-참고 — 공성 맵은 spawns 저작이 금지라 실제 스폰 = 적 마음 셀(unit 6 파생).
            // 저작 스폰 대신 파생 스폰을 검사해야 «툴 통과 → 런타임 폴백 선형맵으로 조용히
            // 증발» 을 막는다.
            var effectiveSpawns = new List<Vector2Int>(_spawns);
            if (CountEnemyCores() > 0)
            {
                effectiveSpawns.Clear();   // 런타임 파생과 동일: 적 마음이 있으면 저작 스폰은 덮인다
                foreach (var st in _structures)
                    if (st.data != null && st.side == StructureSide.Enemy && st.data.kind == StructureKind.Core)
                        effectiveSpawns.Add(st.cell);
            }
            if (_goals.Count > 0 && effectiveSpawns.Count > 0)
            {
                // 리뷰 H-2 패리티 — 본능 3×3 은 벽이다(런타임 MapConnectivity 와 같은 판정).
                // 여기서 안 잡으면 «툴은 통과인데 런타임이 fallback» 이 난다.
                var occluded = new bool[_w * _h];
                foreach (var st in _structures)
                {
                    if (st.data == null || st.data.kind != StructureKind.Instinct) continue;
                    int half = StructurePlacements.InstinctFootprint / 2;
                    for (int oy = -half; oy <= half; oy++)
                        for (int ox = -half; ox <= half; ox++)
                            if (InBounds(st.cell.x + ox, st.cell.y + oy))
                                occluded[Idx(st.cell.x + ox, st.cell.y + oy)] = true;
                }

                var vis = new bool[_w * _h];
                var q = new Queue<int>();
                foreach (var g in _goals)
                    if (IsWalk(g.x, g.y) && !vis[Idx(g.x, g.y)])
                    { vis[Idx(g.x, g.y)] = true; q.Enqueue(Idx(g.x, g.y)); }
                int[] dx = { 1, -1, 0, 0 }, dy = { 0, 0, 1, -1 };
                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    int cx = cur % _w, cy = cur / _w;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + dx[k], ny = cy + dy[k];
                        if (IsWalk(nx, ny) && !vis[Idx(nx, ny)] && !occluded[Idx(nx, ny)])
                        { vis[Idx(nx, ny)] = true; q.Enqueue(Idx(nx, ny)); }
                    }
                }
                foreach (var s in effectiveSpawns)
                    if (IsWalk(s.x, s.y) && !vis[Idx(s.x, s.y)])
                        errs.Add($"스폰 ({s.x},{s.y}) → 골 미도달");
            }
            return errs;
        }

        // ── Bake (unit 1) ─────────────────────────────────────────────────────────
        private void Bake()
        {
            var target = _target;
            if (target == null)
            {
                string path = EditorUtility.SaveFilePanelInProject(
                    "새 MapDocument 저장", "MapDocument_New", "asset",
                    "맵 데이터를 저장할 위치를 선택하세요.", "Assets/_Project/Data/Maps");
                if (string.IsNullOrEmpty(path)) return;
                target = ScriptableObject.CreateInstance<MapDocument>();
                AssetDatabase.CreateAsset(target, path);
            }

            EnsureMask();
            int n = _w * _h;
            int maskDiffCount = 0;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Temp);
            var merge = new NativeArray<byte>(n, Allocator.Temp);
            var choke = new NativeArray<byte>(n, Allocator.Temp);
            var prop = new NativeArray<byte>(n, Allocator.Temp);
            var mask = new NativeArray<byte>(n, Allocator.Temp);
            try
            {
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        int i = Idx(x, y);
                        tiles[i] = _tiles[i];
                        mask[i] = _placeMask[i];
                        if (_placeMask[i] != DerivedMask(i)) maskDiffCount++;
                        int d = 0;
                        if (_tiles[i] == MapTileType.Walk)
                        {
                            if (IsWalk(x + 1, y)) d++;
                            if (IsWalk(x - 1, y)) d++;
                            if (IsWalk(x, y + 1)) d++;
                            if (IsWalk(x, y - 1)) d++;
                        }
                        merge[i] = (byte)d;
                        choke[i] = (byte)(d >= 3 ? 1 : 0);
                        prop[i] = 0;
                    }

                var spawns = new NativeArray<int2>(_spawns.Count, Allocator.Temp);
                for (int i = 0; i < _spawns.Count; i++) spawns[i] = new int2(_spawns[i].x, _spawns[i].y);

                var goals = new NativeArray<int2>(_goals.Count, Allocator.Temp);
                for (int i = 0; i < _goals.Count; i++) goals[i] = new int2(_goals[i].x, _goals[i].y);

                var gm = new GeneratedMap
                {
                    tiles = tiles,
                    mergeDegree = merge,
                    chokepoint = choke,
                    propLayerId = prop,
                    placeMask = mask,
                    gridSize = new int2(_w, _h),
                    spawns = spawns,
                    goals = goals,
                    goal = _goals.Count > 0 ? goals[0] : new int2(0, 0),   // primary = goals[0]
                    seed = -1,
                    generatorVersion = 0,
                };
                // battle-structures unit 3 — 거점은 관리 참조(StructureData)라 GeneratedMap 이
                // 왕복시킬 수 없다. 저작 주체가 엔트리를 직접 넘긴다.
                MapDocumentBuilder.WriteToDocument(target, in gm, _structures.ToArray());
                goals.Dispose();
                spawns.Dispose();
            }
            finally
            {
                tiles.Dispose(); merge.Dispose(); choke.Dispose(); prop.Dispose(); mask.Dispose();
            }

            EditorUtility.SetDirty(target);

            // map-painter-tool unit 5 — 신규 맵을 dev 슬롯에 자동 노출. 풀 본편(entries)은 절대
            // 건드리지 않는다(seed % Count 결정론). 스테퍼(DevMapOverridePanel)가 풀 뒤 D 슬롯으로 순환.
            RegisterToDevSlot(target);

            AssetDatabase.SaveAssets();
            _target = target; // 연속 편집
            // maskDiff>0 = 수동 배치판(런타임 시드 커빙 skip) — 저작자 최종 인지용 (placement-mask unit 2).
            Debug.Log($"[MapPainter] Bake 완료 → {AssetDatabase.GetAssetPath(target)} ({_w}×{_h}, spawns={_spawns.Count}, goals={_goals.Count}, 마스크 상이 셀={maskDiffCount})");
        }

        // map-painter-tool unit 5 — Bake 된 신규 문서를 풀의 dev 슬롯에 자동 등록.
        // 풀 본편/dev 어디에도 없을 때만 추가(중복 방지) — 이미 라이브 풀에 있는 맵은 그대로 둔다.
        private static void RegisterToDevSlot(MapDocument doc)
        {
            var guids = AssetDatabase.FindAssets("t:MapDocumentPool");
            if (guids.Length == 0) return;
            if (guids.Length > 1)
                Debug.LogWarning($"[MapPainter] MapDocumentPool 이 {guids.Length}개 — 첫 번째에만 dev 등록한다.");
            var pool = AssetDatabase.LoadAssetAtPath<MapDocumentPool>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            if (pool == null) return;
            if (pool.EditorRegisterDevDocument(doc))
            {
                EditorUtility.SetDirty(pool);
                Debug.Log($"[MapPainter] '{doc.name}' 을 {pool.name} dev 슬롯에 등록 — 맵 스테퍼 D 슬롯으로 진입 가능(시드 선택 무영향).");
            }
        }
    }
}
