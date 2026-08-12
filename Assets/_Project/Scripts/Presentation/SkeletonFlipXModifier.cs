using Spine;
using Spine.Unity;
using UnityEngine;

namespace Wassup.Presentation
{
    // Asset-level horizontal mirror for Spine rigs authored facing the opposite
    // direction from the project convention. The project convention is "rig faces
    // -x (left) at Skeleton.ScaleX = +1" — matched by player-main and assumed by
    // SpineUnitView.FaceToward's sign rule. Some source rigs (e.g. BellKnight) are
    // drawn facing +x, so without normalization the single FaceToward rule mirrors
    // them backward.
    //
    // Attaching this to a SkeletonDataAsset.skeletonDataModifiers list negates the
    // root bone's setup-pose ScaleX, mirroring the whole rig once at load time.
    // This fixes the inconsistent DATA instead of threading per-unit flip flags
    // through the runtime, so no facing code changes are needed and the result
    // composes correctly with FaceToward (net facing = Skeleton.ScaleX * rootScaleX).
    //
    // Runs once per SkeletonData load (SkeletonDataAsset applies modifiers right
    // after deserialization, before any Skeleton instance is created).
    [CreateAssetMenu(fileName = "SkeletonFlipX", menuName = "Wassup/Spine/Skeleton Flip X", order = 100)]
    public class SkeletonFlipXModifier : SkeletonDataModifierAsset
    {
        public override void Apply(SkeletonData skeletonData)
        {
            if (skeletonData == null || skeletonData.Bones.Count == 0) return;
            BoneData root = skeletonData.Bones.Items[0];
            BonePose setupPose = root.GetSetupPose();
            float magnitude = Mathf.Approximately(setupPose.ScaleX, 0f) ? 1f : Mathf.Abs(setupPose.ScaleX);
            // Force-negative (not toggle) so a reload that re-applies stays mirrored.
            setupPose.ScaleX = -magnitude;
        }
    }
}
