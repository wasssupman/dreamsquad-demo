using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Spine;
using Spine.Unity;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Presentation
{
    // unit-parts-appearance 1 — 파츠 스킨 합성 + 캐시 + 공용 적용 헬퍼.
    // 스폰(SpineUnitView)과 드래그 프리뷰(DefenderDragPlacementController)가 같은 경로를 탄다.
    //
    // 캐시 키는 SkeletonData "인스턴스" — 이 프로젝트는 도메인 리로드 OFF 라 static 캐시가
    // 플레이 세션을 넘어 생존하는데, 에디터 리임포트가 SkeletonDataAsset.Clear() 후 새
    // SkeletonData 를 만들면 인스턴스 키라 자연 캐시 미스가 난다. ConditionalWeakTable 이라
    // 고아 엔트리는 GC 가 회수 — 정리 로직 불필요 (엔트리 상한 = 유닛 데이터 종수).
    public static class SpineCombinedSkinCache
    {
        private static readonly ConditionalWeakTable<SkeletonData, Dictionary<string, Skin>> Cache = new();

        // 스킨 적용 단일 진입점. 파츠 목록이 있으면 combined skin, 없으면 단일 SpineSkinName.
        // 슬롯 틴트는 SetSlotsToSetupPose "이후" 적용해야 한다(setup pose 가 슬롯 색을 리셋).
        public static void Apply(Skeleton skeleton, ISpineUnitVisualData data)
        {
            if (skeleton == null || data == null) return;
            Skin skin = ResolveSkin(skeleton.Data, data);
            if (skin != null)
            {
                skeleton.SetSkin(skin);
                skeleton.SetSlotsToSetupPose();
            }
            ApplySlotColors(skeleton, data.SpineSlotColors, data.SpineDisplayName);
        }

        public static Skin GetOrBuild(SkeletonData skeletonData, IReadOnlyList<string> parts, string ownerName = null)
        {
            Dictionary<string, Skin> perData = Cache.GetValue(skeletonData, _ => new Dictionary<string, Skin>());
            // 문서 순 join — AddSkin 은 (slotIndex, placeholder) 단위 교체 병합이라 순서가
            // 결과에 의미를 갖는다(뒤 파츠가 슬롯 단위로 덮음). 정렬 정규화 금지.
            string key = string.Join("|", parts);
            if (perData.TryGetValue(key, out Skin cached)) return cached;

            var combined = new Skin(key);
            for (int i = 0; i < parts.Count; i++)
            {
                Skin part = skeletonData.FindSkin(parts[i]);
                if (part == null)
                {
                    Debug.LogWarning($"[SpineCombinedSkinCache] Part skin '{parts[i]}' not found ('{ownerName}'). Skipped.");
                    continue;
                }
                combined.AddSkin(part);
            }
            perData[key] = combined;
            return combined;
        }

        private static Skin ResolveSkin(SkeletonData skeletonData, ISpineUnitVisualData data)
        {
            IReadOnlyList<string> parts = data.SpinePartSkins;
            if (parts != null && parts.Count > 0)
                return GetOrBuild(skeletonData, parts, data.SpineDisplayName);

            if (string.IsNullOrEmpty(data.SpineSkinName)) return null;
            Skin named = skeletonData.FindSkin(data.SpineSkinName);
            if (named == null)
                Debug.LogWarning($"[SpineCombinedSkinCache] Skin '{data.SpineSkinName}' not found for '{data.SpineDisplayName}'.");
            return named;
        }

        // 주의: 애니메이션이 rgba 를 키잉하는 슬롯(현 스켈레톤 기준 eye)은 첫 프레임에 틴트가
        // 덮인다 — 그런 슬롯은 틴트 대상에서 제외하는 것이 계약(unit 2 validator 가 경고).
        private static void ApplySlotColors(Skeleton skeleton, IReadOnlyList<SpineSlotColor> colors, string ownerName)
        {
            if (colors == null) return;
            for (int i = 0; i < colors.Count; i++)
            {
                SpineSlotColor entry = colors[i];
                if (string.IsNullOrEmpty(entry.slotName)) continue;
                Slot slot = skeleton.FindSlot(entry.slotName);
                if (slot == null)
                {
                    Debug.LogWarning($"[SpineCombinedSkinCache] Slot '{entry.slotName}' not found ('{ownerName}'). Skipped.");
                    continue;
                }
                slot.SetColor(entry.color);
            }
        }
    }
}
