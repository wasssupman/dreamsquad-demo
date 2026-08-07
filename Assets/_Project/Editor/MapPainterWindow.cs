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
        private enum Tool { Road, Buildable, Deco, Spawn, Goal, PlaceMask }

        private const float Cell = 26f;

        private int _w = 15, _h = 10;
        private MapTileType[] _tiles;
        private byte[] _placeMask;   // placement-mask unit 2/4 — 셀이 여는 배치 층 비트. 타일 종류와 직교.
        private PlacementLayer _maskBrushLayer = PlacementLayer.Ground;   // unit 4 — 어느 층을 칠할지
        private bool _maskPaintValue;   // 드래그 = 시작 셀의 반전값으로 set (재토글 깜빡임 방지 — Spawn/Goal 이 click-only 인 이유와 동일 함정)
        private bool _maskStrokePrimed; // 스트로크 시작값이 이번 드래그에서 잡혔나 — 격자 밖 MouseDown 후 진입 드래그가 직전 스트로크 잔존값으로 칠하는 엣지 방지
        private readonly List<Vector2Int> _spawns = new();
        private readonly List<Vector2Int> _goals = new();   // multi-goal-map — 골 1~4
        private readonly List<float> _goalStability = new();   // _goals 와 index 정렬 — per-goal 최대 안정도 M (goal-stability unit 0)
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
            _goalStability.Clear();
        }

        // placement-mask unit 2 — 파생값 = tiles==Place. 마스크 브러시로 만든 차이만 이 값과 달라진다.
        private byte DerivedMask(int i) => PlacementLayers.Derive(_tiles[i]);   // 런타임과 같은 단일 파생 정의

        private void ResetMaskToDerived()
        {
            _placeMask = new byte[_w * _h];
            for (int i = 0; i < _placeMask.Length; i++) _placeMask[i] = DerivedMask(i);
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

            // 안정도: _goals 와 길이 일치 시 채택, 아니면 전 골 0 (런타임 폴백과 같은 규칙).
            _goalStability.Clear();
            var st = doc.GoalMaxStability;
            for (int i = 0; i < _goals.Count; i++)
                _goalStability.Add(st != null && st.Count == _goals.Count ? Mathf.Max(0f, st[i]) : 0f);
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawGrid();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"{_w}×{_h}  spawns={_spawns.Count}  goals={_goals.Count}", EditorStyles.miniLabel);
            DrawGoalStability();
            DrawValidationAndBake();
        }

        // goal-stability unit 0 — per-goal 최대 안정도 M 입력. 0 = 유출 지점 현행, >0 = 공성 대상.
        private void DrawGoalStability()
        {
            SyncGoalStabilityLength();
            if (_goals.Count == 0) return;
            EditorGUILayout.LabelField("골 안정도 (0 = 현행 유출, >0 = 공성 대상)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < _goals.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"골 ({_goals[i].x},{_goals[i].y})", GUILayout.Width(90));
                    _goalStability[i] = Mathf.Max(0f, EditorGUILayout.FloatField(_goalStability[i], GUILayout.Width(70)));
                }
            }
        }

        // 도메인 리로드 등으로 두 리스트 길이가 어긋나면 0 패딩/절단으로 재정렬.
        private void SyncGoalStabilityLength()
        {
            while (_goalStability.Count < _goals.Count) _goalStability.Add(0f);
            while (_goalStability.Count > _goals.Count) _goalStability.RemoveAt(_goalStability.Count - 1);
        }

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
            if (i < _goalStability.Count) _goalStability.RemoveAt(i);
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
                    new[] { "Road", "Buildable", "Deco", "Spawn", "Goal", "Mask" }, EditorStyles.toolbarButton);
                // placement-mask unit 4 — Mask 브러시가 칠할 층. 유닛 SO 의 placementLayers 와 같은 축이다.
                using (new EditorGUI.DisabledScope(_tool != Tool.PlaceMask))
                    _maskBrushLayer = GUILayout.Toolbar(_maskBrushLayer == PlacementLayer.Path ? 1 : 0,
                        new[] { "지면", "경로" }, EditorStyles.toolbarButton, GUILayout.Width(72)) == 1
                        ? PlacementLayer.Path : PlacementLayer.Ground;
                if (GUILayout.Button("Mask=파생 리셋", EditorStyles.toolbarButton, GUILayout.Width(96)))
                    ResetMaskToDerived();
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

            HandlePaint(area);
        }

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
                        _goalStability.Add(0f);   // 기본 0 = 현행 유지
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

        private List<string> Validate()
        {
            var errs = new List<string>();
            if (_tiles == null) { errs.Add("격자 없음"); return errs; }

            // 런타임 MapConnectivity 가 스폰 <2 를 거부(fallback linear 로 교체)하므로 authoring 에서 ≥2 강제.
            if (_spawns.Count < 2 || _spawns.Count > 4)
                errs.Add($"스폰 {_spawns.Count}개 (2~4 필요)");
            foreach (var s in _spawns)
                if (!IsWalk(s.x, s.y)) errs.Add($"스폰 ({s.x},{s.y}) 이 Walk 아님");
            if (_goals.Count < 1 || _goals.Count > 4)
                errs.Add($"골 {_goals.Count}개 (1~4 필요)");
            foreach (var g in _goals)
                if (!IsWalk(g.x, g.y)) errs.Add($"골 ({g.x},{g.y}) 이 Walk 아님");

            // (2×2 walk 블록 금지는 map-painter-tool unit 4 에서 철회 — 폭 1 은 저작 규칙이었지
            // 런타임 요구가 아니다. flow field/CellTrim 은 임의 폭 walkable 에서 성립.)

            // BFS 연결성: goals 전체에서 Walk 로 flood(멀티-소스), 각 스폰이 아무 골이든 도달 확인
            if (_goals.Count > 0 && _spawns.Count > 0)
            {
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
                        if (IsWalk(nx, ny) && !vis[Idx(nx, ny)])
                        { vis[Idx(nx, ny)] = true; q.Enqueue(Idx(nx, ny)); }
                    }
                }
                foreach (var s in _spawns)
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

                SyncGoalStabilityLength();
                var goalStability = new NativeArray<float>(_goals.Count, Allocator.Temp);
                for (int i = 0; i < _goals.Count; i++) goalStability[i] = Mathf.Max(0f, _goalStability[i]);

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
                    goalMaxStability = goalStability,
                    seed = -1,
                    generatorVersion = 0,
                };
                MapDocumentBuilder.WriteToDocument(target, in gm);
                goalStability.Dispose();
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
