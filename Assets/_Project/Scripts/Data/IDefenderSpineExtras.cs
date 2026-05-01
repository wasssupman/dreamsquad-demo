using UnityEngine;

namespace Wassup.Data
{
    // Defender-only Spine knobs that have no meaning for enemy units
    // (drag-to-place feedback, deploy/landing animation, cast anchor bone for
    // projectile origin VFX). Keeping these out of ISpineUnitVisualData lets
    // AttackUnitData implement the common interface without supplying dummy
    // values. SpineUnitView treats this as optional — when null, deploy and
    // cast-anchor calls fall back to no-op or local-offset zero.
    public interface IDefenderSpineExtras
    {
        string SpineDragAnimation { get; }
        string SpineDeployAnimation { get; }
        string SpineCastAnchorBone { get; }
        Vector3 SpineCastAnchorLocalOffset { get; }
    }
}
