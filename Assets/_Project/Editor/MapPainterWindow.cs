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
        private enum Tool { Road, Buildable, Deco, Spawn, Goal }

        private const float Cell = 26f;

        private int _w = 15, _h = 10;
        private MapTileType[] _tiles;
        private readonly List<Vector2Int> _spawns = new();
        private readonly List<Vector2Int> _goals = new();   // multi-goal-map — 골 1~4
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
            _spawns.Clear();
            _goals.Clear();
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
            _spawns.Clear();
            if (doc.Spawns != null)
                foreach (var s in doc.Spawns) _spawns.Add(new Vector2Int(s.x, s.y));
            _goals.Clear();
            if (doc.Goals != null && doc.Goals.Count > 0)
                foreach (var g in doc.Goals) _goals.Add(new Vector2Int(g.x, g.y));
            else
                _goals.Add(new Vector2Int(doc.Goal.x, doc.Goal.y));   // 레거시 단일골 폴백
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(4);
            DrawGrid();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField($"{_w}×{_h}  spawns={_spawns.Count}  goals={_goals.Count}", EditorStyles.miniLabel);
            DrawValidationAndBake();
        }

        private void DrawValidationAndBake()
        {
            var errors = Validate();
            bool ok = errors.Count == 0;

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
                    new[] { "Road", "Buildable", "Deco", "Spawn", "Goal" }, EditorStyles.toolbarButton);
            }
        }

        private void DrawGrid()
        {
            if (_tiles == null) return;
            Rect area = GUILayoutUtility.GetRect(_w * Cell, _h * Cell, GUILayout.ExpandWidth(false));

            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    // 화면 위쪽 = y=H-1 (y=0 하단)
                    var r = new Rect(area.x + x * Cell, area.y + (_h - 1 - y) * Cell, Cell - 1f, Cell - 1f);
                    var cell = new Vector2Int(x, y);
                    Color c = ColorFor(_tiles[Idx(x, y)]);
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
                }
            }

            HandlePaint(area);
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
            int idx = Idx(x, y);
            var cell = new Vector2Int(x, y);
            switch (_tool)
            {
                case Tool.Road:
                    _tiles[idx] = MapTileType.Walk;
                    break;
                case Tool.Buildable:
                    _tiles[idx] = MapTileType.Place;
                    _spawns.Remove(cell);
                    _goals.Remove(cell);
                    break;
                case Tool.Deco:
                    _tiles[idx] = MapTileType.Deco; // 장식(배치·이동 불가)
                    _spawns.Remove(cell);
                    _goals.Remove(cell);
                    break;
                case Tool.Spawn:
                    if (!isDown) return; // 토글은 클릭만
                    if (_spawns.Contains(cell)) _spawns.Remove(cell);
                    else if (_spawns.Count < 4)
                    {
                        _tiles[idx] = MapTileType.Walk; // 스폰은 Walk 셀
                        _spawns.Add(cell);
                    }
                    break;
                case Tool.Goal:
                    if (!isDown) return; // 토글은 클릭만
                    if (_goals.Contains(cell)) _goals.Remove(cell);
                    else if (_goals.Count < 4)
                    {
                        _tiles[idx] = MapTileType.Walk; // 골은 Walk 셀
                        _goals.Add(cell);
                    }
                    break;
            }
        }

        // ── Validation (unit 1) — 런타임 계약과 일치 ──────────────────────────────
        private bool IsWalk(int x, int y) => InBounds(x, y) && _tiles[Idx(x, y)] == MapTileType.Walk;

        private List<string> Validate()
        {
            var errs = new List<string>();
            if (_tiles == null) { errs.Add("격자 없음"); return errs; }

            if (_spawns.Count < 1 || _spawns.Count > 4)
                errs.Add($"스폰 {_spawns.Count}개 (1~4 필요)");
            foreach (var s in _spawns)
                if (!IsWalk(s.x, s.y)) errs.Add($"스폰 ({s.x},{s.y}) 이 Walk 아님");
            if (_goals.Count < 1 || _goals.Count > 4)
                errs.Add($"골 {_goals.Count}개 (1~4 필요)");
            foreach (var g in _goals)
                if (!IsWalk(g.x, g.y)) errs.Add($"골 ({g.x},{g.y}) 이 Walk 아님");

            // 2×2 walk 블록 금지
            for (int y = 0; y < _h - 1; y++)
                for (int x = 0; x < _w - 1; x++)
                    if (IsWalk(x, y) && IsWalk(x + 1, y) && IsWalk(x, y + 1) && IsWalk(x + 1, y + 1))
                    { errs.Add($"2×2 walk 블록 ({x},{y})"); y = _h; break; }

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

            int n = _w * _h;
            var tiles = new NativeArray<MapTileType>(n, Allocator.Temp);
            var merge = new NativeArray<byte>(n, Allocator.Temp);
            var choke = new NativeArray<byte>(n, Allocator.Temp);
            var prop = new NativeArray<byte>(n, Allocator.Temp);
            try
            {
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        int i = Idx(x, y);
                        tiles[i] = _tiles[i];
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
                    gridSize = new int2(_w, _h),
                    spawns = spawns,
                    goals = goals,
                    goal = _goals.Count > 0 ? goals[0] : new int2(0, 0),   // primary = goals[0]
                    seed = -1,
                    generatorVersion = 0,
                };
                MapDocumentBuilder.WriteToDocument(target, in gm);
                goals.Dispose();
                spawns.Dispose();
            }
            finally
            {
                tiles.Dispose(); merge.Dispose(); choke.Dispose(); prop.Dispose();
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
            _target = target; // 연속 편집
            Debug.Log($"[MapPainter] Bake 완료 → {AssetDatabase.GetAssetPath(target)} ({_w}×{_h}, spawns={_spawns.Count}, goals={_goals.Count})");
        }
    }
}
