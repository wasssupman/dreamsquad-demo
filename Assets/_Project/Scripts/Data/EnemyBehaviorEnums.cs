using System;

namespace Wassup.Data
{
    // enemy-behavior-components Unit 0 — enemy behavior axes selected per-SO and
    // baked to ECS components. enemyClass is a label only; these drive runtime.

    public enum EnemyAttackMethod { None, Melee, Projectile }

    // FocusUntilDead 의 의미는 target-persistence unit 2(D2, 2026-08-09)로 «죽거나
    // **사거리를 벗어날 때까지**»가 됐다. 이름은 그대로 둔다 — 저작된 적 SO 6종의 참조를
    // 흔들 이유가 없다. 예전 의미(죽을 때까지만 해제)는 이탈한 적이 락을 붙든 채 발사를
    // 보류하고 골로 걸어가는 버그를 만들었다.
    //
    // ⚠ unit 3(2026-08-10)으로 **두 모드가 같은 락을 쓴다.** 해제 사유는 공통으로
    // «사망 · 사거리 이탈 · 어그로 끌림 · 자기 CC 해제»다. 남은 차이는 **선정 규칙**뿐이다:
    //   Nearest        = 락이 풀렸을 때 최근접을 고른다
    //   FocusUntilDead = 락이 풀렸을 때 같은 선정 사슬을 타되, 이름이 «집요함»을 저작 의도로 표시
    // 즉 이제 이 enum 은 «얼마나 오래 무는가»가 아니라 «어떻게 고르는가»의 축이다.
    // None 만이 락을 받지 않는다(= 공격하지 않는 적).
    public enum EnemyTargetMode { None, Nearest, FocusUntilDead }

    // enemy-ai-fsm — Engaging 상태의 이동 정책(구 aimMode 대체).
    // Halt = 타겟 사거리 도달 시 정지하고 공격, Advance = 목표로 이동하며 공격,
    // Pulse = 타격 진행 중(AttackState.hitDelayRemaining>0) 정지·아니면 전진(진동, unit 7).
    public enum EngageMovement : byte { Halt, Advance, Pulse }

    // Allowed defender classes for an enemy's target filter. bit = 1 << (int)DefenderClass
    // (matches EnemyTargetFilter.classMask). Everything == ~0 == -1 preserves the
    // existing "-1 = all" convention. Bit 0 is intentionally unused: DefenderClass.None
    // has no flag, and targets without DefenderClassTag (hazards) bypass the mask.
    [Flags]
    public enum DefenderClassFlags
    {
        None = 0,
        Ranger = 1 << 1,
        Guardian = 1 << 2,
        Fighter = 1 << 3,
        Caster = 1 << 4,
        Support = 1 << 5,
        Everything = ~0,
    }
}
