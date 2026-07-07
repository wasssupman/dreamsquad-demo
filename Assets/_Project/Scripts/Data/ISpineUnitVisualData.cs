using System.Collections.Generic;
using Spine.Unity;
using UnityEngine;

namespace Wassup.Data
{
    // Common Spine visual contract shared by every unit type that renders via
    // SkeletonAnimation (defenders and enemies alike). Defender-specific knobs
    // (drag/deploy animations, cast anchor) live in IDefenderSpineExtras so
    // enemy data assets do not have to supply meaningless dummy values.
    public interface ISpineUnitVisualData
    {
        string SpineDisplayName { get; }
        SkeletonDataAsset SpineSkeletonDataAsset { get; }
        string SpineSkinName { get; }
        string SpineIdleAnimation { get; }
        string SpineAttackAnimation { get; }
        string SpineDeathAnimation { get; }
        float SpineVisualScale { get; }
        // enemy-spawn-positioning 0 — 유닛 타입별 비주얼 피봇 미세조정(view-space). 기본 0.
        Vector3 SpineVisualOffset { get; }
        // unit-parts-appearance 0 — 파츠 스킨 경로 목록("{category}/{category}_c_{n}").
        // 비어 있으면 SpineSkinName 단일 스킨 경로를 그대로 사용한다(하위 호환).
        IReadOnlyList<string> SpinePartSkins { get; }
        // unit-parts-appearance 0 — 슬롯 틴트 목록. 비어 있으면 미적용.
        // 애니메이션이 rgba 를 키잉하는 슬롯(현 스켈레톤 기준 eye)은 틴트가 덮여 무효.
        IReadOnlyList<SpineSlotColor> SpineSlotColors { get; }
    }
}
