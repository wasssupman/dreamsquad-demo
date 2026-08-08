using Unity.Entities;

namespace Wassup.Battle.Units
{
    // goal-tower-siege unit 1 — GoalReachedEvent 를 적 1기당 1회로 고정하는 마커.
    //
    // PastGoalTag 는 이제 사형 선고가 아니라 "타워에 붙었다" 는 상태라 엔티티에 영구히
    // 남는다. 마커가 없으면 UnitLifecycleSystem 이 매 프레임 같은 적의 도달을 재발화해
    // 스트레스 카운터가 폭주한다.
    //
    // **쿼리의 WithNone<> 으로 써야 한다.** in-loop 플래그 검사로 만들면 공성 인구 전원을
    // 매 프레임 순회하게 된다(마커만 확인하고 아무 일도 안 하면서).
    public struct GoalReachedMarker : IComponentData { }
}
