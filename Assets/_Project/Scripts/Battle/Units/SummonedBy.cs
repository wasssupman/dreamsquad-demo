using Unity.Entities;

namespace Wassup.Battle.Units
{
    // summon-patrol-defender — 소환 경로가 붙이는 수명 링크. owner 가 죽으면 이 유닛도 죽는다.
    //
    // Units 소유(죽음은 DeadTag·HealthDeathSystem 이 있는 이 맥락의 것). PatrolAnchor
    // (Movement, 이동 제약)와 **맥락이 다른 것**이 둘을 별도 컴포넌트로 두는 근거다 —
    // 미래 확장이 아니라 오늘의 소유권이다.
    //
    // 디버그 스폰처럼 소유자가 없는 순찰병에는 부착하지 않는다(= 연쇄 소멸 대상 아님).
    // 소비자는 PatrolLifecycleSystem(unit 4).
    public struct SummonedBy : IComponentData
    {
        public Entity owner;
    }
}
