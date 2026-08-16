using Unity.Entities;

namespace Wassup.Battle.Effects
{
    // aggro-targeting Unit 0 — Effects-owned. Sticky link from an aggroed enemy
    // to the guardian holding it. writer/clearer 는 둘 다 Effects 맥락 —
    // AggroStateSystem(히트 게이트 통과 시 획득 / 가디언 소멸·해제 시 제거)과
    // FlowFieldRebuildSystem(장애물 변경 시 무효화). 후자가 떼는 이유: 낡은 chase field 가
    // 가리키는 경로가 무효가 되므로 Marching 으로 되돌려 다음 히트에 재획득시킨다.
    // MovementSystem and AttackSystem read it cross-context (read-only), mirroring
    // the TornadoField→MovementSystem precedent.
    // on-place-skill-rework unit 3 — remover 가 둘이라는 사실을 여기 적어 둔다(위 주석의
    // 「AggroStateSystem 만 쓴다」는 이미 stale 이었다): AggroStateSystem(해제·만료) +
    // FlowFieldRebuildSystem(장애물 변경 무효화).
    public struct Aggroed : IComponentData
    {
        public Entity guardian;  // the guardian that aggroed this enemy (first-come, sticky)

        // on-place-skill-rework unit 3 — 도발 잔여 시간(초).
        //   <= 0 : **무기한** — 기존 히트 획득. 해제는 가디언 사망뿐이다.
        //   >  0 : 시한 도발. 매 틱 감소하고 0 이하가 되면 가디언 생존과 무관하게 해제된다.
        //
        // ⚠ `0 = 무기한` sentinel 이 기존 픽스처 8곳을 보호한다 — `new Aggroed { guardian = … }`
        // 로 만드는 테스트(AggroAoeWidth · GoalTauntGrant · EnemyAiStateSystem)가 이 필드를
        // 0 으로 받아 종전 의미 그대로다. 이 규약을 뒤집지 말 것.
        //
        // 별도 `Taunted` 컴포넌트를 두지 않는 이유: `Aggroed` 소비처가 6곳이라(Movement 추격 ·
        // AttackSystem sticky · TauntAttackGrant · FlowFieldRebuild · 브리지 아이콘 reconcile ·
        // 자기 자신) 상위 레이어를 만들면 그 6곳이 전부 "둘 중 어느 쪽이냐"를 물어야 한다.
        // 보스 어그로 면역이 **부착 1곳 차단**으로 풀린 것과 같은 판단이다.
        public float remainingTime;
    }
}
