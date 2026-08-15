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
    // ⚠ **소비 사이트는 닫힌 집합이 아니다.** "판 밖" 은 소스가 1개(이 컴포넌트)지만 소비처는
    // 손으로 열거해야 하고 컴파일러가 도와주지 않는다 — 실제로 하나를 놓쳐 발사 패턴 유닛이
    // 화면 밖 보스를 쏘고 있었다. 적을 후보로 담는 쿼리를 새로 만들면 여기에 추가하고 아래
    // 목록도 갱신할 것. 두 번째 무적 소스(은신·석화)가 생기면 그때 공유 후보 스냅샷으로 접는다.
    //
    //   1. `AttackSystem` targetCandidatesQuery        — 방어유닛 타겟 후보
    //   2. `ProjectileMoveSystem` retarget 풀          — 호밍 대상 소실 시 재조준
    //   3. `ProjectileHitSystem` aoeQuery              — splash·TileAoe 피해자 + bounce 후보
    //   4. `ProjectileEmitterSystem` enemyQuery        — 발사 명세 패턴의 적 풀
    //   5. `DamageApplicationSystem` IncomingDamage    — **쿼리 제외가 아니라 버퍼 Clear**
    //   6. `BattleBridge.IsLegalOnPlaceTarget`         — 배치 스킬(전방 관통 일격)의 후보 집합
    //      (defender-on-place-skills unit 4 — 리뷰에서 놓친 것이 잡혔다. 이 목록이 일한 사례다)
    //
    // 5번이 이 축의 choke point 다 — 피해 producer 는 7개 파일에 흩어져 있는데 소비 1곳만 막아
    // 전부 커버한다. 1~4 는 "겨누지 않기"(그림), 5 는 "이미 날아온 것 버리기"(규칙)로 역할이 다르다.
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
