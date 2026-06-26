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
    }
}
