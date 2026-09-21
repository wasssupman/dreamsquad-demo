using Unity.Entities;

namespace Wassup.Battle.Units
{
    // defender-deploy-phase unit 1 — 「배치 중」 페이즈. 태그가 아니라 **데이터**다.
    //
    //   InFlight (기본값 0) — 비행 중. sim 은 시계를 재지 않는다(비행은 프레젠테이션 시간). 브리지
    //                        `LandDeployedDefender` 가 Deploying 으로 전이시킨다. 착지 없이 영원히 남으면 좀비 —
    //                        비행을 끝내는 모든 출구(착지·중단·OnDisable)가 Land 를 부르는 것이 계약이다.
    //   Deploying           — 착지 후 배치 모션 재생 중. `remaining` 을 DeploymentActivationSystem 이 배틀 시간으로
    //                        틱하고 0 이 되면 이 컴포넌트를 걷고 JustDeployed 를 붙인다(= 활성화 = 배치 스킬 엣지).
    //
    // 존재하는 동안 유닛은 «판에 없는 것»이다 — 공격·캐스트·피격·타겟·감지·오라·픽업 쿼리 14곳이 WithNone 으로 배제한다.
    // 기존 코드의 `AddComponent<PendingDeployment>()`(재배치·테스트)는 기본값 InFlight 라 뜻이 그대로 «배제»다.
    public struct PendingDeployment : IComponentData
    {
        public const byte InFlight = 0;
        public const byte Deploying = 1;

        public byte stage;
        public float remaining;   // Deploying 에서만 의미. 배틀 시간(초)
    }
}
