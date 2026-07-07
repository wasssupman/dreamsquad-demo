using System.Collections.Generic;
using Spine;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Animation = Spine.Animation;

namespace Wassup.Editor
{
    // unit-parts-appearance 2 — 파츠 조합 유효성 검사.
    // 아트팀이 콘솔을 안 봐도 되게 인스펙터 HelpBox 로 노출한다. 검증 코어는 순수 static
    // 함수로 분리해 배치 스모크(SpineUpgradeSmoke)가 같은 규칙을 검증한다.
    public static class UnitVisualDataValidator
    {
        public static List<string> CollectWarnings(ISpineUnitVisualData data)
        {
            var warnings = new List<string>();
            if (data == null) return warnings;

            IReadOnlyList<string> parts = data.SpinePartSkins;
            IReadOnlyList<SpineSlotColor> colors = data.SpineSlotColors;
            bool hasParts = parts != null && parts.Count > 0;
            bool hasColors = colors != null && colors.Count > 0;
            if (!hasParts && !hasColors) return warnings; // 빈 목록 = 단일 스킨 경로, 검증 대상 아님

            var sda = data.SpineSkeletonDataAsset;
            if (sda == null)
            {
                warnings.Add("skeletonDataAsset 없이 partSkins/slotColors 가 설정되어 있음");
                return warnings;
            }
            SkeletonData skel = sda.GetSkeletonData(true);
            if (skel == null)
            {
                warnings.Add("SkeletonData 로드 실패 — 에셋 임포트 상태 확인");
                return warnings;
            }

            if (hasParts) ValidateParts(skel, parts, warnings);
            if (hasColors) ValidateSlotColors(skel, colors, warnings);
            return warnings;
        }

        private static void ValidateParts(SkeletonData skel, IReadOnlyList<string> parts, List<string> warnings)
        {
            var categoryCounts = new Dictionary<string, int>();
            bool hasSkinBase = false, hasHelmet = false, hasHairShort = false, hasHairHat = false;

            for (int i = 0; i < parts.Count; i++)
            {
                string part = parts[i];
                if (string.IsNullOrEmpty(part))
                {
                    warnings.Add($"partSkins[{i}] 이 빈 항목");
                    continue;
                }
                if (skel.FindSkin(part) == null) warnings.Add($"없는 스킨 경로: '{part}'");

                int slash = part.IndexOf('/');
                string category = slash > 0 ? part.Substring(0, slash) : part;
                categoryCounts.TryGetValue(category, out int n);
                categoryCounts[category] = n + 1;
                switch (category)
                {
                    case "skin": hasSkinBase = true; break;
                    case "helmet": hasHelmet = true; break;
                    case "hair_short": hasHairShort = true; break;
                    case "hair_hat": hasHairHat = true; break;
                }
            }

            foreach (KeyValuePair<string, int> kv in categoryCounts)
                if (kv.Value > 1)
                    warnings.Add($"카테고리 중복: '{kv.Key}' {kv.Value}개 — 뒤가 통째로 이기지 않고 슬롯 단위로 섞여 병합됨. 하나만 남길 것");
            if (!hasSkinBase)
                warnings.Add("본체 파츠(skin/skin_1) 누락 — 몸통 없이 렌더됨");
            if (hasHelmet && hasHairShort)
                warnings.Add("helmet 착용 조합은 hair_short 대신 hair_hat 사용 (Layer Lab 배타 규칙)");
            if (!hasHelmet && hasHairHat)
                warnings.Add("helmet 미착용 조합은 hair_hat 대신 hair_short 사용 (Layer Lab 배타 규칙)");
        }

        private static void ValidateSlotColors(SkeletonData skel, IReadOnlyList<SpineSlotColor> colors, List<string> warnings)
        {
            HashSet<string> keyedSlots = CollectColorKeyedSlots(skel);
            for (int i = 0; i < colors.Count; i++)
            {
                SpineSlotColor entry = colors[i];
                if (string.IsNullOrEmpty(entry.slotName))
                {
                    warnings.Add($"slotColors[{i}] 슬롯 이름이 비어 있음");
                    continue;
                }
                if (skel.FindSlot(entry.slotName) == null)
                {
                    warnings.Add($"없는 슬롯: '{entry.slotName}'");
                    continue;
                }
                if (keyedSlots.Contains(entry.slotName))
                    warnings.Add($"슬롯 '{entry.slotName}' 은 애니메이션이 색을 키잉 — 틴트가 재생 즉시 덮여 무효. 파츠 스킨으로 대체할 것");
                if (entry.color.a <= 0f)
                    warnings.Add($"slotColors '{entry.slotName}' 의 알파가 0 — 해당 슬롯이 안 보이게 됨 (색 미지정?)");
            }
        }

        // 애니메이션이 색(rgba/rgb/alpha/tint-black)을 키잉하는 슬롯 수집. 현 스켈레톤 기준 eye 1개.
        public static HashSet<string> CollectColorKeyedSlots(SkeletonData skel)
        {
            var result = new HashSet<string>();
            foreach (Animation anim in skel.Animations)
                foreach (Timeline t in anim.Timelines)
                    if (t is RGBATimeline || t is RGBTimeline || t is AlphaTimeline || t is RGBA2Timeline || t is RGB2Timeline)
                        result.Add(skel.Slots.Items[((ISlotTimeline)t).SlotIndex].Name);
            return result;
        }
    }

    public abstract class SpineUnitVisualDataEditorBase : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (target is ISpineUnitVisualData data)
                foreach (string warning in UnitVisualDataValidator.CollectWarnings(data))
                    EditorGUILayout.HelpBox(warning, MessageType.Warning);
            LayerLabImportSection.Draw(this); // unit-parts-appearance 3
        }
    }

    [CustomEditor(typeof(DefenderUnitData))]
    public class DefenderUnitDataEditor : SpineUnitVisualDataEditorBase { }

    [CustomEditor(typeof(AttackUnitData))]
    public class AttackUnitDataEditor : SpineUnitVisualDataEditorBase { }
}
