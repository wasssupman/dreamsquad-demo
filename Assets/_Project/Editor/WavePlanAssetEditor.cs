using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor
{
    // wave-plan-authoring-inspector unit 0 — WavePlanAsset 경량 작성 인스펙터.
    // SerializedProperty 기반(Undo/dirty/멀티 정상). 데이터 모델/런타임 무변경.
    [CustomEditor(typeof(WavePlanAsset))]
    public class WavePlanAssetEditor : UnityEditor.Editor
    {
        private const float Epsilon = 0.0001f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
            var timerProp = serializedObject.FindProperty("timerDurationSec");
            EditorGUILayout.PropertyField(timerProp, new GUIContent("Timer Duration Sec", "0 = endless (전 웨이브 전멸 시 승리)"));

            var wavesProp = serializedObject.FindProperty("waves");

            DrawPlanSummary(wavesProp);
            DrawPlanWarnings(wavesProp);

            EditorGUILayout.Space(6f);
            for (int i = 0; i < wavesProp.arraySize; i++)
                DrawWave(wavesProp, i);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("+ Add Wave", GUILayout.Height(28f)))
                AddWave(wavesProp);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPlanSummary(SerializedProperty wavesProp)
        {
            int totalSpawns = 0;
            float totalLength = 0f;
            for (int i = 0; i < wavesProp.arraySize; i++)
            {
                var w = wavesProp.GetArrayElementAtIndex(i);
                totalLength += w.FindPropertyRelative("durationSec").floatValue;
                var groups = w.FindPropertyRelative("groups");
                for (int g = 0; g < groups.arraySize; g++)
                    totalSpawns += Mathf.Max(0, groups.GetArrayElementAtIndex(g).FindPropertyRelative("count").intValue);
            }
            EditorGUILayout.HelpBox(
                $"Waves: {wavesProp.arraySize}    Total length: {totalLength:0.#}s    Total spawns: {totalSpawns}",
                MessageType.None);
        }

        private void DrawPlanWarnings(SerializedProperty wavesProp)
        {
            var issues = new List<string>();
            if (wavesProp.arraySize == 0) issues.Add("웨이브가 없습니다.");
            for (int i = 0; i < wavesProp.arraySize; i++)
            {
                var w = wavesProp.GetArrayElementAtIndex(i);
                float dur = w.FindPropertyRelative("durationSec").floatValue;
                var groups = w.FindPropertyRelative("groups");
                if (groups.arraySize == 0) issues.Add($"W{i + 1}: 그룹이 비어 있습니다.");
                for (int g = 0; g < groups.arraySize; g++)
                {
                    var grp = groups.GetArrayElementAtIndex(g);
                    if (grp.FindPropertyRelative("unit").objectReferenceValue == null) issues.Add($"W{i + 1} G{g + 1}: unit 미지정.");
                    if (grp.FindPropertyRelative("count").intValue <= 0) issues.Add($"W{i + 1} G{g + 1}: count ≤ 0.");
                    float tt = grp.FindPropertyRelative("triggerTimeSec").floatValue;
                    if (tt < 0f) issues.Add($"W{i + 1} G{g + 1}: triggerTimeSec < 0.");
                    else if (tt > dur + Epsilon) issues.Add($"W{i + 1} G{g + 1}: triggerTimeSec({tt:0.#}) > durationSec({dur:0.#}).");
                }
            }
            if (issues.Count > 0)
                EditorGUILayout.HelpBox(string.Join("\n", issues), MessageType.Warning);
        }

        private void DrawWave(SerializedProperty wavesProp, int index)
        {
            var wave = wavesProp.GetArrayElementAtIndex(index);
            var durProp = wave.FindPropertyRelative("durationSec");
            var intervalProp = wave.FindPropertyRelative("intervalSec");
            var groups = wave.FindPropertyRelative("groups");

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            wave.isExpanded = EditorGUILayout.Foldout(wave.isExpanded,
                $"Wave {index + 1}   ({groups.arraySize} groups, {WaveSpawnCount(groups)} spawns, {durProp.floatValue:0.#}s)", true);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button("▲", GUILayout.Width(26f))) { wavesProp.MoveArrayElement(index, index - 1); }
            using (new EditorGUI.DisabledScope(index == wavesProp.arraySize - 1))
                if (GUILayout.Button("▼", GUILayout.Width(26f))) { wavesProp.MoveArrayElement(index, index + 1); }
            if (GUILayout.Button("Dup", GUILayout.Width(40f))) { wavesProp.InsertArrayElementAtIndex(index); }
            if (GUILayout.Button("✕", GUILayout.Width(26f))) { wavesProp.DeleteArrayElementAtIndex(index); EditorGUILayout.EndHorizontal(); EditorGUILayout.EndVertical(); return; }
            EditorGUILayout.EndHorizontal();

            if (wave.isExpanded)
            {
                EditorGUILayout.PropertyField(durProp, new GUIContent("Duration Sec (N)"));
                EditorGUILayout.PropertyField(intervalProp, new GUIContent("Interval Sec (0=동시)"));

                DrawTimeline(groups, durProp.floatValue);

                EditorGUILayout.Space(2f);
                for (int g = 0; g < groups.arraySize; g++)
                {
                    var grp = groups.GetArrayElementAtIndex(g);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(grp.FindPropertyRelative("unit"), GUIContent.none);
                    GUILayout.Label("@", GUILayout.Width(14f));
                    EditorGUILayout.PropertyField(grp.FindPropertyRelative("triggerTimeSec"), GUIContent.none, GUILayout.Width(50f));
                    GUILayout.Label("s ×", GUILayout.Width(22f));
                    EditorGUILayout.PropertyField(grp.FindPropertyRelative("count"), GUIContent.none, GUILayout.Width(44f));
                    if (GUILayout.Button("✕", GUILayout.Width(24f))) { groups.DeleteArrayElementAtIndex(g); break; }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ Add Group"))
                    AddGroup(groups);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawTimeline(SerializedProperty groups, float duration)
        {
            var rect = GUILayoutUtility.GetRect(0f, 18f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.14f, 1f));
            float dur = Mathf.Max(duration, Epsilon);
            for (int g = 0; g < groups.arraySize; g++)
            {
                var grp = groups.GetArrayElementAtIndex(g);
                float tt = Mathf.Clamp(grp.FindPropertyRelative("triggerTimeSec").floatValue, 0f, dur);
                float x = rect.x + (tt / dur) * (rect.width - 3f);
                bool valid = grp.FindPropertyRelative("unit").objectReferenceValue != null;
                var marker = new Rect(x, rect.y + 1f, 3f, rect.height - 2f);
                EditorGUI.DrawRect(marker, valid ? new Color(1f, 0.86f, 0.24f, 1f) : new Color(0.9f, 0.3f, 0.3f, 1f));
            }
        }

        private static int WaveSpawnCount(SerializedProperty groups)
        {
            int n = 0;
            for (int g = 0; g < groups.arraySize; g++)
                n += Mathf.Max(0, groups.GetArrayElementAtIndex(g).FindPropertyRelative("count").intValue);
            return n;
        }

        private static void AddWave(SerializedProperty wavesProp)
        {
            int i = wavesProp.arraySize;
            wavesProp.arraySize++;
            var w = wavesProp.GetArrayElementAtIndex(i);
            w.FindPropertyRelative("durationSec").floatValue = 12f;
            w.FindPropertyRelative("intervalSec").floatValue = 0.5f;
            w.FindPropertyRelative("groups").arraySize = 0;
            w.isExpanded = true;
        }

        private static void AddGroup(SerializedProperty groups)
        {
            int g = groups.arraySize;
            groups.arraySize++;
            var grp = groups.GetArrayElementAtIndex(g);
            grp.FindPropertyRelative("unit").objectReferenceValue = null;
            grp.FindPropertyRelative("triggerTimeSec").floatValue = 0f;
            grp.FindPropertyRelative("count").intValue = 1;
        }
    }
}
