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

        // spine-weapon-trail unit 1 — 무기 궤적. 여기(방어 유닛 전용 인터페이스)에 두는 것이
        // 설계 판단이다: SpineUnitView 는 적을 스폰할 때 _defenderExtras 가 null 이라,
        // 적 제외가 코드 분기 없이 자동으로 성립한다.
        GameObject SpineWeaponTrailPrefab { get; }
        float SpineWeaponTrailEndNormalized { get; }
    }
}
