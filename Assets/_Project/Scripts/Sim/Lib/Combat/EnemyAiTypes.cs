using Wassup.Sim.Effects;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/3 — 공격 출력 1건. 구 `Wassup.Data.AttackOutput` 이식.
    /// **4분기 union** 이고 필드마다 유효한 kind 가 다르다(주석 참조).
    ///
    /// `DcTriggerSlot` 과 달리 여기서 옮긴다 — 7필드 자립 struct 이고 enum 3개가 이미 이식돼
    /// 있으며, **Combat 이 소유하는 가변 카운터가 없다**(저작 데이터다).
    /// </summary>
    public enum AttackOutputKind { Damage, Heal, ApplyStat, ApplyStack }

    public struct AttackOutput
    {
        public AttackOutputKind kind;
        /// 모든 kind — Damage/Heal 양 · Stat magnitude · Stack countDelta.
        public float magnitude;
        /// `ApplyStat`/`ApplyStack` 만 의미(Damage/Heal 은 무시).
        public float duration;
        /// `ApplyStat` 만.
        public StatKind stat;
        /// `ApplyStat` 만.
        public CombineOp op;
        /// `ApplyStack` 만.
        public StackKind stackKind;
        /// `ApplyStack` 만 — 저작 cap(0 = 미지정 시 소비자 디폴트).
        public byte stackMaxStack;
    }

    /// 유닛의 공격 출력 목록(버퍼 원소). 구 `AttackOutputElement` 이식.
    public struct AttackOutputElement
    {
        public AttackOutput value;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-F/3 — 적 FSM 상태. 구 `AiState` 이식.
    /// ⚠ **append-only**(상태 해시가 정수로 찍는다).
    /// </summary>
    public enum AiState : byte { Marching, Engaging, Chasing, Standoff }

    /// `EnemyAiState` 의 **유일한 writer 는 #14** 이고 이동·공격은 읽기만 한다.
    public struct EnemyAiState
    {
        public AiState value;
    }

    /// 적의 타겟 선택 모드. 구 `Wassup.Data.EnemyTargetMode` 이식.
    public enum EnemyTargetMode { None, Nearest, FocusUntilDead }

    /// <summary>
    /// `Engaging` 상태의 이동 정책. 구 `Wassup.Data.EngageMovement` 이식.
    /// `Halt` = 사거리 도달 시 정지하고 공격 · `Advance` = 이동하며 공격 ·
    /// `Pulse` = 타격 진행 중(`AttackState.hitDelayRemaining > 0`)엔 정지, 아니면 전진(진동).
    /// </summary>
    public enum EngageMovement : byte { Halt, Advance, Pulse }

    public struct EnemyBehavior
    {
        public EnemyTargetMode targetMode;
        public EngageMovement engageMovement;
    }

    /// <summary>
    /// 적의 타겟 클래스 필터. `classMask` 의 비트 = `1 &lt;&lt; (int)DefenderClass`.
    /// ⚠ **`-1` = 전체 허용**이 기존 컨벤션이다. 비트 0 은 의도적으로 미사용
    /// (`DefenderClass.None` 에는 플래그가 없고, 태그 없는 대상은 마스크를 우회한다).
    /// </summary>
    public struct EnemyTargetFilter
    {
        public int classMask;
        public int priorityClass;
    }

    /// `FocusUntilDead` 락의 현재 대상. 구 `FocusTarget` 이식.
    public struct FocusTarget
    {
        public SimEntityId current;
    }
}
