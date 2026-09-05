using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // enemy-detection-range unit 5 — Combat→Bridge **발견 사건**. 30번째 NativeQueue 채널.
    //
    // 감지는 그냥 두면 «조용한 상태 변화»다 — 적이 갑자기 방향을 트는데 플레이어는 왜인지 모른다.
    // 이 채널이 그 순간을 1회 사건으로 내보내 화면이 말하게 한다.
    //
    // **값 스냅샷이다**(`SkillFiredEvent` 선례). `Entity` 를 싣지 않는다 — 드레인 시점에 그 적이
    // 이미 죽었을 수 있고, 브리지가 엔티티 핸들을 들면 재활용된 엔티티를 건드린다.
    //
    // ⚠ `targetSimId` 는 **트레이스·로그 전용**이다. 화면에서 그 유닛을 가리키거나 선을 그으면
    // 안 된다 — 감지는 직선 최근접을 고르는데 몸은 공용 사냥판을 따라가 **실측 5.0%** 에서 다른
    // 방어유닛에게 간다. 대상을 가리키면 그 5.0% 에서 화면이 규칙을 **틀리게 가르친다**.
    public struct DetectionEvent
    {
        public int    enemySimId;    // 발견한 쪽
        public int    targetSimId;   // 발견당한 방어유닛 (로그 전용)
        public float3 enemyPos;      // 표식을 띄울 자리
    }

    // Queue owned by BattleBridge (기존 NativeQueue 싱글턴 lifecycle 패턴).
    public struct DetectionEventsSingleton : IComponentData
    {
        public NativeQueue<DetectionEvent> queue;
    }
}
