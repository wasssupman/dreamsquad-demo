using Unity.Burst;
using Unity.Mathematics;

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
        // ── 획득과 유지를 가르는 폭 (distance-based-range unit 4d) ──
        //
        // 획득 `gap ≤ N`, 유지 `gap ≤ N + h`. **원칙: 「여기서 쏠 수 있나 · 멈춰도 되나」는
        // 획득, 「이미 문 것을 놓나」는 유지.** 이동 정지 판정에 유지 임계를 쓰면
        // **적이 사거리 밖에서 멈춘다.**
        //
        // **`h` 는 측정에서 나왔다 — 추정이 아니다.** 「멈춘」 적(Engaging/Standoff)이 프레임마다
        // 실제로 얼마나 흔들리는지를 `RangePredicateMirrorTest` 가 잰다(밀어냄·분리가 만드는
        // 지터). 2026-08-31 실측 **0.047 · 0.051칸**(2회). 그 폭보다 좁으면 진동을 못 막는다.
        //
        // 0.1 로 잡은 이유(측정치의 약 2배):
        //   · 측정은 **한 판·한 맵**이고 두 번 재도 0.047↔0.051 로 흔들린다. 지터는 밀집도에
        //     따라 커지므로(군집 통과) 한 표본에 딱 맞추면 다른 맵에서 모자란다.
        //   · 그러면서도 옛 슬랙 0.5 보다 **다섯 배 작다** — `target-persistence` D2 가 없앤
        //     「지나쳐 갔는데 락을 붙들고 있는」 상태를 사실상 되살리지 않는다
        //     (사거리 1 의 유지 임계가 1.5 가 아니라 1.1 이다).
        //
        // ⚠ **코드 상수다. `sceneKnobs` 에 등재하지 않는다**(계약 9) — 등재하면 `configHash` 가
        // 움직여 골든 red 가 「조건 드리프트」로 읽히고 관측 도구 성격이 무너진다.
        // 튜닝이 필요해지면 그건 SO 의 문제이지 이 상수의 문제가 아니다.
        public const float HysteresisTiles = 0.1f;

        // 락을 계속 붙들까? 사거리 판정은 **획득과 같은 술어**를 쓰되 `h` 만큼 넓게 본다.
        public static bool KeepsLock(bool targetAlive, float3 atkPos, float3 tgtPos,
                                     float tileRange, float tileSize,
                                     float targetBodyRadiusTiles = 0f)
            => targetAlive && AttackReach.InReach(atkPos, tgtPos, tileRange + HysteresisTiles,
                                                  tileSize, targetBodyRadiusTiles);
    }
}
