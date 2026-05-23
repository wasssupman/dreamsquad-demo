using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.MapGrid;

namespace Wassup.Editor.MapGrid
{
    public class MapGridDebugWindow : EditorWindow
    {
        private MapGridGenerationSettings _settings;
        private int _seed = 0;
        private MapGridPreset _preset = MapGridPreset.Wide30x15;
        private bool _usePresetOverride;

        private GeneratedMap _currentMap;
        private int _lastAttempt;
        private int _chokepointCount;
        private string _sweepResult = "";

        [MenuItem("Window/Wassup/Map Grid Debug")]
        public static void Open() => GetWindow<MapGridDebugWindow>("Map Grid Debug");

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGui;
            DisposeCurrent();
        }

        private void DisposeCurrent()
        {
            if (_currentMap.IsCreated) _currentMap.Dispose();
            _currentMap = default;
        }

        private void OnGUI()
        {
            _settings = (MapGridGenerationSettings)EditorGUILayout.ObjectField(
                "Settings", _settings, typeof(MapGridGenerationSettings), false);

            using (new EditorGUILayout.HorizontalScope())
            {
                _seed = EditorGUILayout.IntField("Seed", _seed);
                if (GUILayout.Button("Re-roll", GUILayout.Width(80)))
                    _seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            }

            _usePresetOverride = EditorGUILayout.Toggle("Use Preset Override", _usePresetOverride);
            if (_usePresetOverride)
                _preset = (MapGridPreset)EditorGUILayout.EnumPopup("Preset", _preset);

            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_settings == null))
            {
                if (GUILayout.Button("Generate"))
                    Generate();
                if (GUILayout.Button("Sweep 100 seeds"))
                    Sweep(100);
                if (GUILayout.Button("Bake to MapDocument..."))
                    BakeToDocument();
            }

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Last Attempt", _lastAttempt.ToString());
            EditorGUILayout.LabelField("Chokepoint Cells", _chokepointCount.ToString());
            if (!string.IsNullOrEmpty(_sweepResult))
                EditorGUILayout.HelpBox(_sweepResult, MessageType.Info);
        }

        private int2 PickGridSize()
        {
            if (_usePresetOverride) return MapGridGenerationSettings.PresetToGridSize(_preset);
            var presets = _settings.AllowedPresets;
            if (presets == null || presets.Count == 0) return new int2(20, 10);
            int idx = math.abs(_seed) % presets.Count;
            return MapGridGenerationSettings.PresetToGridSize(presets[idx]);
        }

        private void Generate()
        {
            DisposeCurrent();
            int2 size = PickGridSize();
            try
            {
                _currentMap = MapGridGenerator.Generate(_seed, size, _settings, Allocator.Persistent, out _lastAttempt);
                _chokepointCount = 0;
                for (int i = 0; i < _currentMap.chokepoint.Length; i++)
                    if (_currentMap.chokepoint[i] != 0) _chokepointCount++;
                SceneView.RepaintAll();
            }
            catch (MapGenerationFailedException ex)
            {
                Debug.LogError($"[MapGridDebug] {ex.Message}");
                _chokepointCount = 0;
                _lastAttempt = ex.Attempts;
            }
        }

        private void Sweep(int count)
        {
            int success = 0, totalAttempts = 0, withChoke = 0;
            int2 size = PickGridSize();
            for (int seed = 0; seed < count; seed++)
            {
                GeneratedMap m = default;
                try
                {
                    m = MapGridGenerator.Generate(seed, size, _settings, Allocator.TempJob, out int a);
                    success++;
                    totalAttempts += a;
                    for (int i = 0; i < m.chokepoint.Length; i++)
                        if (m.chokepoint[i] != 0) { withChoke++; break; }
                }
                catch (MapGenerationFailedException) { }
                finally { if (m.IsCreated) m.Dispose(); }
            }
            _sweepResult = $"sweep={count} success={success} avg_attempts={(success > 0 ? totalAttempts / (float)success : 0f):F2} chokeRate={(success > 0 ? withChoke / (float)success * 100f : 0f):F1}%";
            Debug.Log($"[MapGridDebug] {_sweepResult}");
        }

        private void BakeToDocument()
        {
            if (!_currentMap.IsCreated)
            {
                EditorUtility.DisplayDialog("Bake", "Generate first.", "OK");
                return;
            }
            string path = EditorUtility.SaveFilePanelInProject(
                "Bake MapDocument", $"MapDocument_seed{_seed}", "asset", "Save MapDocument");
            if (string.IsNullOrEmpty(path)) return;

            var doc = CreateInstance<MapDocument>();
            MapDocumentBuilder.WriteToDocument(doc, in _currentMap);
            AssetDatabase.CreateAsset(doc, path);
            AssetDatabase.SaveAssets();
            EditorUtility.RevealInFinder(path);
        }

        private void OnSceneGui(SceneView view)
        {
            if (!_currentMap.IsCreated) return;
            var size = _currentMap.gridSize;
            for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var c = new Vector3(x, 0, y);
                    int idx = MapGridIndex.CellIndex(new int2(x, y), size);
                    var tile = _currentMap.tiles[idx];

                    Color color = tile == MapTileType.Walk ? new Color(0.2f, 0.2f, 0.25f, 0.85f)
                                                            : new Color(0.6f, 0.6f, 0.6f, 0.25f);
                    Handles.color = color;
                    Handles.DrawSolidRectangleWithOutline(
                        new[] {
                            c, c + new Vector3(1, 0, 0),
                            c + new Vector3(1, 0, 1), c + new Vector3(0, 0, 1),
                        },
                        color,
                        new Color(0, 0, 0, 0.4f));

                    if (_currentMap.chokepoint[idx] != 0)
                    {
                        Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                        Handles.DrawWireDisc(c + new Vector3(0.5f, 0.05f, 0.5f), Vector3.up, 0.35f);
                    }
                }

            var goal = _currentMap.goal;
            Handles.color = Color.red;
            Handles.DrawWireDisc(new Vector3(goal.x + 0.5f, 0.1f, goal.y + 0.5f), Vector3.up, 0.4f);
            for (int i = 0; i < _currentMap.spawns.Length; i++)
            {
                var sp = _currentMap.spawns[i];
                Handles.color = Color.green;
                Handles.DrawWireDisc(new Vector3(sp.x + 0.5f, 0.1f, sp.y + 0.5f), Vector3.up, 0.4f);
            }
        }
    }
}
