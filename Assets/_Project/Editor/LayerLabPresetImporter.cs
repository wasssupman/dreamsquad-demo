using System;
using System.Collections.Generic;
using LayerLab.ArtMaker;
using Spine;
using UnityEditor;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Editor
{
    // unit-parts-appearance 3 — Layer Lab 조립 결과(export 프리팹/프리셋) → 유닛 데이터 임포트.
    //
    // Layer Lab 의 런타임 규칙을 에디터 타임에 "재현"한다 (씬 직렬화 스냅샷 파싱 금지):
    // - 인덱스→스킨 경로: GetCategorySkins = 스킨 목록을 PartsType 이름 prefix 로 StartsWith 스캔
    // - helmet/hair 배타: helmet 착용이면 hair_hat, 아니면 hair_short (Hair_Hat 인덱스는 항상
    //   Hair_Short 미러라 flat 변환 시 이중 머리가 된다 — critic F1 블로커)
    // - 색: 논리 키 4종(hair/beard/brow/body)을 슬롯 prefix 스캔으로 확장 (critic F2 블로커)
    public static class LayerLabPresetImporter
    {
        // PartsManager 의 ApplyColorByType → *Prefixes 테이블 재현
        private static readonly Dictionary<string, string[]> LogicalColorPrefixes = new()
        {
            { "hair", new[] { "hair", "helmet_hair" } },
            { "beard", new[] { "beard" } },
            { "brow", new[] { "brow" } },
            { "body", new[] { "body", "leg_l", "leg_r", "arm_l", "arm_r", "head" } },
        };

        // Layer Lab GetCategorySkins 재현 — JSON 스킨 순서가 인덱스의 원본.
        public static List<string> GetCategorySkins(SkeletonData data, PartsType category)
        {
            string categoryName = category.ToString();
            var result = new List<string>();
            foreach (Skin s in data.Skins)
                if (s.Name.StartsWith(categoryName, StringComparison.OrdinalIgnoreCase))
                    result.Add(s.Name);
            return result;
        }

        public static List<string> ResolveParts(
            SkeletonData skel, IReadOnlyDictionary<PartsType, (int index, bool hidden)> entries, List<string> issues)
        {
            var parts = new List<string>();
            bool helmetEquipped = entries.TryGetValue(PartsType.Helmet, out (int index, bool hidden) h)
                                  && h.index >= 0 && !h.hidden
                                  && h.index < GetCategorySkins(skel, PartsType.Helmet).Count;

            foreach (PartsType type in (PartsType[])Enum.GetValues(typeof(PartsType)))
            {
                if (type == PartsType.None) continue;
                if (type == PartsType.Hair_Short && helmetEquipped) continue;
                if (type == PartsType.Hair_Hat && !helmetEquipped) continue;
                if (!entries.TryGetValue(type, out (int index, bool hidden) e) || e.index < 0 || e.hidden) continue;

                List<string> list = GetCategorySkins(skel, type);
                if (e.index >= list.Count)
                {
                    issues.Add($"{type}: 인덱스 {e.index} 가 카테고리 목록({list.Count}개) 범위 밖 — 스켈레톤이 갱신됐을 수 있음");
                    continue;
                }
                parts.Add(list[e.index]);
            }
            return parts;
        }

        public static List<SpineSlotColor> ResolveColors(
            SkeletonData skel, IEnumerable<KeyValuePair<string, Color>> logicalColors, List<string> issues)
        {
            var colors = new List<SpineSlotColor>();
            foreach (KeyValuePair<string, Color> kv in logicalColors)
            {
                if (!LogicalColorPrefixes.TryGetValue(kv.Key, out string[] prefixes))
                {
                    issues.Add($"알 수 없는 색 키 '{kv.Key}' — 스킵");
                    continue;
                }
                foreach (string prefix in prefixes)
                    foreach (SlotData slot in skel.Slots)
                    {
                        if (!slot.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        // setup 색과 같으면 no-op 틴트 — 저장하지 않음 (아트가 색을 안 바꾼 키)
                        Color setup = slot.GetSetupPose().GetColor();
                        if (((Vector4)setup - (Vector4)kv.Value).sqrMagnitude < 1e-6f) continue;
                        colors.Add(new SpineSlotColor { slotName = slot.Name, color = kv.Value });
                    }
            }
            return colors;
        }

        // 입력 소스 1 (1차): export 프리팹의 CharacterPrefabData — 디스크 영속 + isHidden 보존
        public static List<string> ImportFromPrefabData(CharacterPrefabData source, ScriptableObject unitData)
        {
            SkeletonData skel = RequireSkeleton(unitData);
            var entries = new Dictionary<PartsType, (int, bool)>();
            foreach (CharacterPrefabData.SkinPartData p in source.skinParts)
                entries[p.partType] = (p.selectedIndex, p.isHidden);
            var logicalColors = new Dictionary<string, Color>();
            foreach (CharacterPrefabData.SlotColorData c in source.slotColors)
                logicalColors[c.slotName] = c.color; // slotName 은 논리 키(hair/beard/brow/body)

            return ApplyResolved(unitData, skel, entries, logicalColors);
        }

        // 입력 소스 2 (보조): PresetData + index — 런타임 저장이라 세션 한정 영속이므로 읽는 김에 SetDirty
        public static List<string> ImportFromPreset(PresetData preset, int presetIndex, ScriptableObject unitData)
        {
            SkeletonData skel = RequireSkeleton(unitData);
            Dictionary<PartsType, int> itemList = preset.LoadPreset(presetIndex);
            if (itemList.Count == 0)
                throw new InvalidOperationException($"프리셋 index {presetIndex} 가 비어 있음 (데모 씬에서 우클릭 저장했는지 확인)");
            var entries = new Dictionary<PartsType, (int, bool)>();
            foreach (KeyValuePair<PartsType, int> kv in itemList)
                entries[kv.Key] = (kv.Value, false); // 프리셋은 hide 상태 미저장 — 미착용(-1)만 유효

            // Play 중 저장된 프리셋을 디스크로 영속화 (dirty 플래그가 원래 없음)
            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();

            return ApplyResolved(unitData, skel, entries, preset.LoadPresetColors(presetIndex));
        }

        private static List<string> ApplyResolved(
            ScriptableObject unitData, SkeletonData skel,
            Dictionary<PartsType, (int, bool)> entries, IEnumerable<KeyValuePair<string, Color>> logicalColors)
        {
            var issues = new List<string>();
            List<string> parts = ResolveParts(skel, entries, issues);
            List<SpineSlotColor> colors = ResolveColors(skel, logicalColors, issues);

            Undo.RecordObject(unitData, "Import Layer Lab Appearance");
            switch (unitData)
            {
                case DefenderUnitData d: d.partSkins = parts; d.slotColors = colors; break;
                case AttackUnitData a: a.partSkins = parts; a.slotColors = colors; break;
                default: throw new InvalidOperationException($"지원하지 않는 유닛 데이터 타입: {unitData.GetType().Name}");
            }
            EditorUtility.SetDirty(unitData);
            foreach (string issue in issues)
                Debug.LogWarning($"[LayerLabPresetImporter] {unitData.name}: {issue}");
            Debug.Log($"[LayerLabPresetImporter] {unitData.name}: 파츠 {parts.Count}개 + 슬롯 색 {colors.Count}개 임포트 완료");
            return issues;
        }

        private static SkeletonData RequireSkeleton(ScriptableObject unitData)
        {
            var visual = unitData as ISpineUnitVisualData;
            SkeletonData skel = visual?.SpineSkeletonDataAsset != null
                ? visual.SpineSkeletonDataAsset.GetSkeletonData(true)
                : null;
            if (skel == null)
                throw new InvalidOperationException($"{unitData.name}: skeletonDataAsset 이 없거나 로드 실패 — 먼저 SkeletonData 를 지정할 것");
            return skel;
        }
    }

    // 유닛 데이터 인스펙터 하단의 "가져오기" 섹션 (SpineUnitVisualDataEditorBase 가 호출).
    public static class LayerLabImportSection
    {
        private static UnityEngine.Object _source;

        public static void Draw(UnityEditor.Editor editor)
        {
            if (!(editor.target is DefenderUnitData || editor.target is AttackUnitData)) return;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Layer Lab 외형 가져오기", EditorStyles.boldLabel);
            _source = EditorGUILayout.ObjectField("Export 프리팹 / PresetData", _source, typeof(UnityEngine.Object), false);

            if (_source is GameObject go)
            {
                CharacterPrefabData prefabData = go.GetComponent<CharacterPrefabData>();
                if (prefabData == null)
                {
                    EditorGUILayout.HelpBox("프리팹에 CharacterPrefabData 가 없음 — 데모 씬의 SavePrefab 버튼으로 export 한 프리팹을 지정할 것", MessageType.Warning);
                    return;
                }
                if (GUILayout.Button("프리팹에서 가져오기"))
                    LayerLabPresetImporter.ImportFromPrefabData(prefabData, (ScriptableObject)editor.target);
            }
            else if (_source is PresetData preset)
            {
                var available = new List<int>();
                foreach (PresetItem item in preset.presetItems) available.Add(item.index);
                available.Sort();
                EditorGUILayout.HelpBox($"저장된 프리셋 index: [{string.Join(", ", available)}] (데모 씬에서 우클릭=저장, 좌클릭=적용)", MessageType.Info);
                foreach (int idx in available)
                    if (GUILayout.Button($"프리셋 {idx} 가져오기"))
                        LayerLabPresetImporter.ImportFromPreset(preset, idx, (ScriptableObject)editor.target);
            }
            else if (_source != null)
            {
                EditorGUILayout.HelpBox("지원 소스: export 프리팹(GameObject) 또는 PresetData 에셋", MessageType.Warning);
            }
        }
    }
}
