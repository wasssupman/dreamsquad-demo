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
        // summon-patrol-defender unit 10 — idle 변형 풀. 2개 이상이면 루프가 한 바퀴 끝날 때마다
        // 다음 변형을 뽑아 이어 재생한다(직전과 같은 것은 피한다). 비어 있으면 SpineIdleAnimation
        // 단일 루프 = 현행 동작이라 미저작 유닛은 무회귀다.
        // 공용 인터페이스에 두는 이유: **어떤 유닛이든 가질 수 있는 성질**이다. 디펜더 전용
        // IDefenderSpineExtras 에 넣어 적을 배제하는 방식은 무기 궤적에서 한 번 막다른 길이었다
        // (그 인터페이스 주석 참조 — 보스/구조물을 넣을 길을 스스로 막았다).
        IReadOnlyList<string> SpineIdleVariants { get; }
        // enemy-walk-anim-speed unit 4 — 이동 중 재생할 걷기 애니. 비어 있으면
        // 이동/정지 구분 없이 SpineIdleAnimation 단일 루프(현행 동작 = 회귀 없음).
        // 설정 시: 이동 중 이 애니, 정지 중 SpineIdleAnimation 으로 자동 전환.
        string SpineWalkAnimation { get; }
        string SpineAttackAnimation { get; }
        string SpineDeathAnimation { get; }
        float SpineVisualScale { get; }
        // tilted-billboard unit 9 — 점유 폭(가로 셀). 블롭 그림자 지름의 기준이다.
        // **공용 인터페이스에 두는 이유**: 점유 폭은 디펜더 전용 개념이 아니다. 보스가 2×2 를 쓰게 되는
        // 날 IDefenderSpineExtras 에 넣어뒀다면 길이 막힌다 — 무기 궤적에서 이미 겪은 함정이다
        // (위 SpineIdleVariants 주석 참조). 적의 1 은 «범위 밖 더미»가 아니라 참값이다(sim 이 1칸 점유).
        int FootprintWidthCells { get; }
        // distance-based-range unit 15 — **판정 몸 반경**(타일). 그림자 지름 = 2r 의 유일한
        // 소스다: 그림자·링·판정이 같은 값에서 유도돼야 「그림자가 링에 닿으면 사거리 안」이
        // 판정식과 동치가 된다(계약 1 rev 3). 방어유닛 = min(W,H)/2 파생, 적 = 티어.
        float BodyRadiusTiles { get; }
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
