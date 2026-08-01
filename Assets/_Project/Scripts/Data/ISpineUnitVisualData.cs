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
        // enemy-walk-anim-speed unit 4 — 이동 중 재생할 걷기 애니. 비어 있으면
        // 이동/정지 구분 없이 SpineIdleAnimation 단일 루프(현행 동작 = 회귀 없음).
        // 설정 시: 이동 중 이 애니, 정지 중 SpineIdleAnimation 으로 자동 전환.
        string SpineWalkAnimation { get; }
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

        // spine-weapon-trail unit 3 — 무기 궤적. 유닛 타입을 가리지 않는다(디펜더·적·보스).
        // **프리팹 유무가 유일한 게이트** — 미할당이면 무궤적이라 잡몹 전원은 영향 없다.
        // 리그 프리팹이 본 이름·포인트 오프셋·룩을 전부 들고 있어 유닛 데이터는 "무엇을 붙일지"만 안다.
        GameObject SpineWeaponTrailPrefab { get; }
        // 방출 종료 지점 = 공격 애니 길이 대비 비율. 스윙 구간에만 걸어야 복귀 동작에
        // 궤적이 따라붙지 않는다. 애니마다 다르므로 유닛 데이터가 소유한다.
        float SpineWeaponTrailEndNormalized { get; }
    }
}
