using Unity.Burst;

namespace Wassup.Battle.Combat
{
    // target-persistence unit 1 — 타겟 락 유지 술어의 **단일 정의**.
    //
    // 이 함수의 가치는 산술이 아니라 **두 시스템이 같은 규칙을 본다**는 것이다.
    // `AttackSystem`(누구를 때릴까)과 `EnemyAiStateSystem`(움직일까 멈출까)이 각자
    // 같은 판정을 복제하고 있었고, 후자의 주석이 이미 이렇게 경고하고 있었다:
    //
    //     ⚠ AttackSystem fire 조건 미러. 타겟 선정 로직 변경 시 동기화 필요.
    //
    // 두 벌이 갈리면 «락은 있는데 FSM 은 Marching» 데드락이 난다 — 적이 대상을 잡은 채
    // 발사도 안 하고 골로 걸어가는 상태다. 말로 된 계약 대신 **같은 함수를 부르게** 해서
    // 구조로 막는다. `AggroPolicy.CanAcquire/ShouldRelease` 와 같은 형태다.
    //
    // 순수 함수. plain 값만 받는다(엔티티 룩업은 호출자 몫).
    [BurstCompile]
    public static class TargetPersistence
    {
        // 락을 계속 붙들까? false = 놓고 그 프레임에 이미 계산된 후보를 새로 채택한다.
        //
        // **사거리 이탈은 해제 사유다** (D2, 2026-08-09 사용자 확정). 이전에는 이탈해도
        // 락을 재저장하고 발사만 보류해서, `FocusUntilDead` 적이 **바로 옆 방어유닛을
        // 영원히 무시하고 골로 걸어갔다**. 방어유닛은 (재배치를 빼면) 움직이지 않으므로
        // 이탈은 대부분 «적이 그를 지나쳐 간 경우»이고, 그때는 다시 고르는 것이 옳다.
        //
        // tileDistance / tileRange 는 **체비셰프 타일 거리**다(사거리 판정의 기존 단위).
        public static bool KeepsLock(bool targetAlive, int tileDistance, int tileRange)
            => targetAlive && tileDistance <= tileRange;
    }
}
