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
        // spine-weapon-trail unit 3 — 무기 궤적 필드는 ISpineUnitVisualData 로 옮겼다.
        // unit 1 에선 "적 제외가 코드 분기 없이 성립"을 노려 여기 뒀지만, 그 이점이
        // 보스/구조물을 넣을 길을 막는 제약이 됐다. 게이트는 프리팹 null 이 대신한다.
    }
}
