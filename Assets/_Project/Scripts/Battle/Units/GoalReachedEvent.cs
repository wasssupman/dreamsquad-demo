using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Units
{
    public struct GoalReachedEvent
    {
        public Entity entity;

        // goal-tower-siege unit 1 — 이 적이 **골에 남아 타워를 때릴 수 있는가**
        // (= AttackState 를 갖고 있는가). 소비자(BattleBridge)가 두 경로로 갈린다:
        //
        //   true  → 공성. 엔티티가 살아 있으니 뷰를 지우지 않고 targetMask 에 GoalTower 를 연다.
        //   false → 돌격형 자폭. **마음을 조준할 수 없는 적**(마스크에 DefenderCore 없음 —
        //           heart-stress-axis unit 7 이 판정을 「AttackState 보유」에서 이걸로 정밀화했다.
        //           라이브에서는 Runner·Swift 2종이고 둘은 일반 공격을 갖는다)은 골에 붙어도
        //           마음에 아무것도 못 하면서 "필드에 적 0기" 판정만 영구히 막는다.
        //           그래서 기존대로 사라지되, 마음 직격은 타워 버퍼로 넣어준다.
        //
        // 생산자(UnitLifecycleSystem)가 판정해 실어 보낸다 — 소비 시점엔 엔티티가 이미
        // 파괴됐을 수 있어 브리지가 컴포넌트를 되읽을 수 없다.
        public bool canSiege;

        // goal-tower-siege(rev 2) — 도달 지점(sim). 자폭 경로에서 **어느 골에 부딪혔는지**를
        // 가르는 데 쓴다(골 2개 맵). 소비 시점엔 엔티티가 파괴돼 위치를 되읽을 수 없다.
        public float3 position;
    }
}
