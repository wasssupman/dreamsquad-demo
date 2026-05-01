using Spine.Unity;

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
    }
}
