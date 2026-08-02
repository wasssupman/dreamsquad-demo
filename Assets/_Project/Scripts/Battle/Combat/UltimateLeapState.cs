using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // ultimate-leap unit 0 — 이탈 상태(사실). **존재 자체가 "판 밖"** 이다:
    // 타겟 후보에서 빠지고 들어온 피해는 버려진다(unit 2).
    //
    // 공격·자기주도 이동 잠금은 여기 없다 — `LeapFlight`(leap-flight-state)가 담당한다.
    // 레이어가 갈리는 것이 계약이다: 잠금은 두 도약이 공유하고, 무적은 궁극기 전용이다.
    // 발동 시 두 컴포넌트가 함께 붙고 착지 시 함께 떨어진다.
    //
    // remaining 은 **Battle 도메인 dt** 로 감소한다(sim 규약). 2초는 회피 창이자 피해
    // 게이트 = 게임 규칙이라 연출 시계가 아니라 시뮬 시계가 소유한다. 일반 도약의 창이
    // 브리지(뷰 시계) 소유인 것과 비대칭이 맞다 — 그쪽은 연출 정합, 이쪽은 게임플레이.
    //
    // landingCell 은 **발동 프레임에 고정**된다(예고는 약속이다). 착지 직전 재계산하면
    // 빨간 타일을 보고 유닛을 빼는 회피 플레이가 거짓이 된다.
    public struct UltimateLeapState : IComponentData
    {
        public float remaining;         // 예고 잔여 초 (slot.duration 에서 시작)
        public int2 landingCell;        // 발동 프레임 고정
        public float3 landingWorld;     // 착지 셀 중심 월드 좌표 (셀→월드 재변환 회피)
        public float slamDamage;
        public int slamTileRange;       // 예고 타일 범위 = 슬램 피해 범위 (같은 값이 계약)
        public int projectileDataIndex; // 착지 슬램 VFX (<0 = 무연출)
    }
}
