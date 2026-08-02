using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // leap-flight-state unit 0 — "도약 비행 중" 이라는 사실. 능력은 소비 지점에서 파생한다
    // (CcActionLock 선례 — 어디에도 "ActionLocked" 는 저장되지 않고 CcEffect 버퍼에서 파생된다).
    // 파생이 자명해서(태그 존재 = 공격·자기주도 이동 불가) 별도 술어 함수를 두지 않는다 —
    // 쿼리 필터와 lookup 체크가 곧 파생이다(제약 10 의 과잉 추출 금지).
    //
    // 의미: **공격 불가 · 자기주도 이동 불가 · 피격 가능.**
    //
    // ⚠ anti-계약: `DamageApplicationSystem` 과 **타겟 후보 수집**(AttackSystem 의
    // targetCandidatesQuery)에 절대 넣지 않는다. 바로 옆의 `PendingDeployment` 는 피격까지
    // 막지만 이건 아니다 — 넣는 순간 보스가 비행 내내 무적이 된다. 비대칭이 의도다.
    // (판 밖 존재 = 무적은 궁극기 전용이고, 그건 `UltimateLeapState` 축이 담당한다.)
    //
    // 쓰기 주체 2원화: 일반 도약 = BattleBridge(비행 창 0.83s 는 뷰 시계라 브리지만 안다.
    // 브리지는 ECS 창구라 경계 위반 아님) / 궁극기 = Combat 시스템(sim 시계 소유).
    // 같은 태그, 다른 시계 — 창(window)의 소유가 다를 뿐 의미는 동일하다.
    //
    // fail-open: 태그 부재 = 전부 허용. 붙이는 쪽이 누락돼도 유닛이 조용히 마비되지 않는다.
    public struct LeapFlight : IComponentData { }
}
