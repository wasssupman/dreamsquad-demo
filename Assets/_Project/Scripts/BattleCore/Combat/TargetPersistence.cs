// salvaged from Assets/_Project/Scripts/Battle/Combat/TargetPersistence.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `Unity.Burst` 제거(코어는 Burst 를 모른다) · `AttackReach` 를 코어 사본으로
//   가리킨다. 상수와 그 근거는 **한 글자도 안 바꿨다** — 실측에서 나온 값이라 다시 고를 수 없다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Combat
{
    // 타겟 락 유지 술어의 **단일 정의**.
    //
    // 이 함수의 가치는 산술이 아니라 **여러 소비처가 같은 규칙을 본다**는 것이다.
    // 옛 전투에서는 `AttackSystem`(누구를 때릴까) · `EnemyAiStateSystem`(움직일까 멈출까) ·
    // `DetectionSystem`(문 것을 놓을까)이 각자 같은 판정을 복제했고, 두 벌이 갈리면
    // «락은 있는데 Marching» 데드락이 났다 — 적이 대상을 잡은 채 발사도 안 하고 골로 걸어간다.
    //
    // 새 코어의 소비처(전부 이 함수를 지난다):
    //   · 공격 루프의 지속 락 유지 — `CombatPhase`
    //   · 한 공격 안의 커밋 유지 — `CombatPhase`
    //   · 감지 유지(「이미 문 것을 놓나」) — `AiMovePhase.StepDetection`
    public static class TargetPersistence
    {
        // ── 획득과 유지를 가르는 폭 ──
        //
        // 획득 `gap ≤ N`, 유지 `gap ≤ N + h`. **원칙: 「여기서 쏠 수 있나 · 멈춰도 되나」는
        // 획득, 「이미 문 것을 놓나」는 유지.** 이동 정지 판정에 유지 임계를 쓰면
        // **적이 사거리 밖에서 멈춘다.**
        //
        // **`h` 는 측정에서 나왔다 — 추정이 아니다.** 「멈춘」 적이 프레임마다 실제로 얼마나
        // 흔들리는지(밀어냄·분리가 만드는 지터)를 재서 2026-08-31 실측 **0.047 · 0.051칸**(2회).
        // 그 폭보다 좁으면 진동을 못 막는다.
        //
        // 0.1 로 잡은 이유(측정치의 약 2배):
        //   · 측정은 한 판·한 맵이고 두 번 재도 흔들린다. 지터는 밀집도에 따라 커지므로
        //     한 표본에 딱 맞추면 다른 맵에서 모자란다.
        //   · 그러면서도 옛 슬랙 0.5 보다 **다섯 배 작다** — 「지나쳐 갔는데 락을 붙들고 있는」
        //     상태를 사실상 되살리지 않는다(사거리 1 의 유지 임계가 1.5 가 아니라 1.1 이다).
        //
        // ⚠ **코드 상수다.** 정의표에 올리지 않는다 — 올리면 `configHash` 가 움직여 골든 빨강이
        // 「조건 드리프트」로 읽히고 관측 도구 성격이 무너진다.
        //
        // ⚠ **자는 하나다.** unit 2 의 `AiMovePhase` 가 감지 유지용으로 0.5 를 따로 들고 있었는데
        // (옛 전투의 감지도 이 함수를 재사용했으므로 0.1 이 옳다) unit 3 에서 이쪽으로 합쳤다.
        // 같은 종류의 진동을 막는 데 두 개의 자를 두지 않는다.
        public const float HysteresisTiles = 0.1f;

        /// <summary>
        /// 락을 계속 붙들까? false = 놓고 그 틱에 이미 계산된 후보를 새로 채택한다.
        ///
        /// **사거리 이탈은 해제 사유다**(2026-08-09 사용자 확정). 이전에는 이탈해도 락을
        /// 재저장하고 발사만 보류해서, 문 적이 **바로 옆 방어유닛을 영원히 무시하고 골로
        /// 걸어갔다.** 방어유닛은 (재배치를 빼면) 움직이지 않으므로 이탈은 대부분
        /// «적이 그를 지나쳐 간 경우»이고, 그때는 다시 고르는 것이 옳다.
        /// </summary>
        public static bool KeepsLock(bool targetAlive, float3 atkPos, float3 tgtPos,
                                     float tileRange, float tileSize,
                                     float selfBodyRadiusTiles,
                                     float targetBodyRadiusTiles)
            => targetAlive && AttackReach.InReach(atkPos, tgtPos,
                                                  tileRange + HysteresisTiles, tileSize,
                                                  selfBodyRadiusTiles, targetBodyRadiusTiles);
    }
}
