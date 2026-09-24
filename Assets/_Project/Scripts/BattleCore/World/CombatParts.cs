using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.BattleCore.Combat;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 3 — 개체의 **때리고·맞고·죽는** 부분.
    //
    // unit 2 의 `UnitParts` 와 같은 규율이다: nullable 부분 객체(UML §2 의 `o--`)이고,
    // 「이 컴포넌트를 갖고 있나」 분기는 **정책 값**(`AttackPolicy`)이나 부재(null)로 표현한다.
    // 옛 전투는 `AttackSystem` 안에서만 `HasComponent` 게이트를 7곳 썼다 — 폭탄맨·소환사·
    // 가디언·힐러·frontmost·방향탄·캐스터가 전부 타입 질문이었다. 여기서는 타입을 묻지 않는다.
    //
    // ⚠ 부분이 늘면 `Unit.Reset` 도 같이 고친다.

    /// <summary>
    /// 아키타입 → **정책 값**. 옛 `DefenderAttackPolicy` 와 같은 축이고, 그 축이 「이 유닛은
    /// 어떤 종류의 공격자인가」의 유일한 답이다(census 「HasComponent 자연 분기」의 후계).
    /// </summary>
    public enum AttackPolicy : byte
    {
        /// <summary>대상을 골라 때린다. 기본값.</summary>
        Target = 0,
        /// <summary>사거리 안 최근접 적이 선 **칸**에 던진다. 대상 선정 앞에서 끝난다(C1·C3).</summary>
        Bomb = 1,
        /// <summary>순찰 소환물을 유지한다. 대상 선정 앞에서 끝난다(C2·C3).</summary>
        Summon = 2,
    }

    /// <summary>
    /// 지속 락의 저작 모드. 옛 `EnemyTargetMode` 와 같은 값이다 —
    /// `None` 이면 락을 아예 받지 않고, 나머지는 「물면 죽거나 벗어날 때까지」다(C13 의 대상).
    /// </summary>
    public enum TargetMode : byte
    {
        None = 0,
        Nearest = 1,
        FocusUntilDead = 2,
    }

    // 한 공격이 내는 출력 하나. 옛 `AttackOutput` 과 같은 축이고 **정의표에서** 온다.
    // 실제 적용(스탯·스택)은 unit 6 이고 이 unit 은 피해·회복까지 배선한다.
    public enum AttackOutputKind : byte { Damage = 0, Heal = 1, ApplyStat = 2, ApplyStack = 3 }

    public struct AttackOutputDef
    {
        public AttackOutputKind Kind;
        public float Magnitude;
        public float Duration;
        /// <summary>`ApplyStat` 전용 — 옛 `StatKind` 의 int 값. 소비는 unit 6.</summary>
        public int Stat;
        /// <summary>`ApplyStat` 전용 — 옛 `CombineOp` 의 int 값.</summary>
        public int Op;
        /// <summary>`ApplyStack` 전용 — 옛 `StackKind` 의 int 값.</summary>
        public int StackKind;
        public int StackMaxStack;
    }

    /// <summary>
    /// 때릴 때 함께 거는 군중 제어(저작). 「부여 측」만 여기 있다 — 실제 슬롯 적용은 unit 6 이다.
    /// ⚠ 스코프가 셋 다 다르다: 넉백·수면은 **주 대상 1체**, 넉업은 **때린 전원**.
    /// 하나로 합치면 그 유닛의 정체성이 조용히 망가진다(옛 주석이 그 경고를 남겼다).
    /// </summary>
    public struct CcOnHit
    {
        public float KnockbackDistance;
        public float KnockbackDuration;
        public float SleepSeconds;
        public float KnockupSeconds;
        /// <summary>띄우기 연출 최고 높이. 심은 읽지도 쓰지도 않고 **값으로 실어 보내기만** 한다.</summary>
        public float KnockupVisualHeight;

        public bool Any => KnockbackDistance > 0f || SleepSeconds > 0f || KnockupSeconds > 0f;
    }

    /// <summary>폭탄맨 저작. 정책이 `Bomb` 일 때만 읽힌다.</summary>
    public struct BombSpec
    {
        public int ProjectileDefIndex;
        public float Damage;
        public int AoeTileRange;
        public int AoeTargetCap;
        public float TravelSeconds;
        public float FuseSeconds;
        public float ArcHeight;
    }

    /// <summary>소환사 저작. 정책이 `Summon` 일 때만 읽힌다.</summary>
    public struct SummonSpec
    {
        /// <summary>순찰 소환물의 `MatchDefinition.Units` 인덱스. -1 = 미저작.</summary>
        public int PatrolDefIndex;
    }

    // 진행 중인 한 번의 발사(패턴 슬롯의 런타임). 옛 `EmitterInstance` 의 후계인데
    // **엔티티를 모른다** — 잠근 대상은 `SimEntityId` 다.
    public sealed class EmitterInstance
    {
        public int PatternDefIndex = -1;
        public Combat.Emission.EmitterRuntime Runtime;
        public SimEntityId LockedTarget = SimEntityId.None;
        /// <summary>
        /// 이 버스트 전탄의 피해 — **트리거 시점 실효값 스냅샷**(공격 산출물 피해 합 × 배율).
        /// 패턴 저작 피해(`PatternDef.Damage`)가 아니다(2026-09-24 드리프트 감사 H1).
        /// </summary>
        public float Damage;
        /// <summary>
        /// 트리거 시점의 **조준 방향**(XZ, 정규화). 발마다 대상을 고르지 않는 패턴(선정 규칙
        /// 없음 = 방향 발사)의 기준 방향이다 — 옛 전투가 template 에 스냅샷한 `fireDir`.
        /// </summary>
        public Unity.Mathematics.float2 AimDirection;
        /// <summary>이 발사에 쓰는 탄막 난수 씨앗 — `hash(사수 SimEntityId, 발사 카운터)`.</summary>
        public uint Seed;
        /// <summary>이 인스턴스가 쓸 간격표. 난수 저작이면 씨앗에서 매 트리거 다시 뽑는다.</summary>
        public float[] Intervals = System.Array.Empty<float>();
        /// <summary>같은 길이의 방향표(0~1). 난수 저작이 매 트리거 다시 뽑는 두 번째 축이다.</summary>
        public float[] Directions = System.Array.Empty<float>();

        /// <summary>
        /// 배열을 **재사용**한다. 트리거마다 새로 잡으면 초당 수십 번 할당이 나고, 그것이
        /// 「틱 중 할당 0」을 깨는 가장 흔한 경로다.
        /// </summary>
        public void EnsureCapacity(int shots)
        {
            if (Intervals.Length < shots) Intervals = new float[shots];
            if (Directions.Length < shots) Directions = new float[shots];
        }
    }

    /// <summary>
    /// 발사 명세 슬롯. **`FireCountBase` 가 이 타입의 존재 이유**다 — 인스턴스는 트리거마다
    /// 생겼다 사라지는 transient 라 발사 카운터를 영속시킬 수 없고, 0 에서 다시 시작하면
    /// RoundRobin 이 영원히 같은 순위를 고른다(그 유닛만 계속 폭격당한다).
    /// </summary>
    public sealed class PatternSlotState
    {
        public int PatternDefIndex = -1;
        public int FireCountBase;

        /// <summary>이 슬롯의 발사 인스턴스. **슬롯당 하나를 재사용한다**(할당 0).</summary>
        public readonly EmitterInstance Instance = new EmitterInstance();

        /// <summary>버스트가 진행 중인가. 인스턴스의 수명이 아니라 **상태**다.</summary>
        public bool Active;
    }

    // ── 공격 상태 ────────────────────────────────────────────────────────────
    //
    // 저작 값은 스폰 때 **정의표에서 스냅샷**된다. 런타임에 바뀌는 것(모디파이어)은 unit 6 이
    // 이 필드를 고치고, 그래서 루프가 매 틱 정의표를 다시 뒤지지 않는다.
    public sealed class AttackState
    {
        // ── 저작 스냅샷 ──
        public float Range;
        /// <summary>공격 간격(초). 실주기는 `max(Interval, HitDelay)` 다(C19 — 현행 보류).</summary>
        public float Interval;
        /// <summary>선딜(초). 0 이면 START 와 같은 틱에 RESOLVE.</summary>
        public float HitDelay;
        public int TargetCount = 1;
        /// <summary>때릴 수 있는 진영 비트(`Faction`). 0 = 미저작 → `TargetDefaults`.</summary>
        public int TargetMask;
        /// <summary>때릴 수 있는 통행 층. 0 = 무필터. 근접이 하늘로 안 번지는 근거다.</summary>
        public byte TargetLayers;
        /// <summary>우선 클래스(`DefenderClass` int). -1 = 없음.</summary>
        public int PriorityClass = -1;
        /// <summary>허용 클래스 비트. `HasClassFilter` 가 참일 때만 읽는다(그때 0 = 아무도 못 때림).</summary>
        public int ClassMask = -1;
        public bool HasClassFilter;
        /// <summary>걷기만 하는 적. 공격 루프·감지·어그로가 전부 건너뛴다.</summary>
        public bool Unarmed;
        public AttackShapeBaked Shape;
        public AttackPolicy Policy;
        public TargetMode Mode;
        public CcOnHit Cc;
        public BombSpec Bomb;
        public SummonSpec Summon;
        /// <summary>투사체 정의표 인덱스. -1 = 근접(즉시 해결).</summary>
        public int ProjectileDefIndex = -1;
        /// <summary>이 공격이 내는 출력 목록. 비면 피해가 0 이다(저작이 정본).</summary>
        public AttackOutputDef[] Outputs = System.Array.Empty<AttackOutputDef>();
        /// <summary>가디언이면 &gt;0. 「아직 안 물린 적 우선」의 자석이 이 값으로 돈다.</summary>
        public int AggroCapacity;

        /// <summary>
        /// 최전방을 겨누나. 지속 락 **제외 4종** 중 하나이고(매 공격마다 지금의 최전방이
        /// 그 카드의 계약이다), 스윙 중 유지는 커밋이 한다(strict lapse 가 같은 규칙).
        /// 오늘의 생산자는 없다 — 부착하는 것은 unit 7 의 카드다.
        /// </summary>
        public bool WantsFrontmost;

        /// <summary>
        /// 기절·수면·넉백 면역(보스). **출처를 묻지 않는다** — 면역은 대상의 성질이다.
        /// 면역 술어를 하드코딩하지 않고 이 한 값으로 두는 이유: 나중에 범위를 좁히면
        /// 부여 지점 전부가 자동으로 따라온다.
        /// </summary>
        public bool BossImmune;

        // ── 런타임 ──
        /// <summary>다음 발사까지 남은 초. **CC 중에도 돈다** — 풀리는 즉시 때리는 근거다.</summary>
        public float CooldownRemaining;
        /// <summary>선딜 잔여. &gt;0 = 스윙 중(START 는 막히고 RESOLVE 는 완료된다).</summary>
        public float HitDelayRemaining;

        /// <summary>이 공격이 겨눈 대상. START → RESOLVE 1회 수명이고 그 밖에서는 비어 있다.</summary>
        public SimEntityId CommittedTarget = SimEntityId.None;
        /// <summary>방향탄의 커밋 축. 대상이 사라져도 이 축으로 나간다(strict lapse 의 유일 예외).</summary>
        public float2 CommittedDirection;
        public bool HasCommittedDirection;

        /// <summary>지속 락. 한 번 문 대상을 죽거나 사거리를 벗어날 때까지 유지한다.</summary>
        public SimEntityId Lock = SimEntityId.None;

        /// <summary>발사 카운터(durable). 탄막 난수 씨앗과 순위 규칙의 축이다.</summary>
        public int FireCount;

        /// <summary>발사 명세 슬롯. 없으면 빈 목록이다(패턴 없는 유닛은 비용 0).</summary>
        public readonly List<PatternSlotState> PatternSlots = new List<PatternSlotState>(1);

        /// <summary>소환사가 한 번이라도 불렀나. 초회 게이트(구역 안에 적이 있을 때만)의 축.</summary>
        public bool HasSummonedOnce;

        /// <summary>도발로 **붙여 준** 공격인가. 해제 시 원복/제거를 가르는 유일한 표시다.</summary>
        public bool GrantedByTaunt;
        /// <summary>도발 전 원래 마스크(원복용). `GrantedByTaunt` 일 때만 의미 있다.</summary>
        public int PreviousTargetMask;

        public bool Swinging => HitDelayRemaining > 0f;

        /// <summary>실주기 — 애니가 실발사보다 빨리 끝나지 않게 하는 값이기도 하다(C19).</summary>
        public float Period(float intervalMul)
        {
            float interval = Interval * (intervalMul > 0f ? intervalMul : 1f);
            return math.max(interval, HitDelay);
        }

        public void ClearCommit()
        {
            CommittedTarget = SimEntityId.None;
            HasCommittedDirection = false;
            CommittedDirection = float2.zero;
        }

        public void Reset()
        {
            Range = 0f;
            Interval = 0f;
            HitDelay = 0f;
            TargetCount = 1;
            TargetMask = 0;
            TargetLayers = 0;
            PriorityClass = -1;
            ClassMask = -1;
            HasClassFilter = false;
            Unarmed = false;
            Shape = default;
            Policy = AttackPolicy.Target;
            Mode = TargetMode.None;
            Cc = default;
            Bomb = default;
            Summon = new SummonSpec { PatrolDefIndex = -1 };
            ProjectileDefIndex = -1;
            Outputs = System.Array.Empty<AttackOutputDef>();
            AggroCapacity = 0;
            WantsFrontmost = false;
            BossImmune = false;
            CooldownRemaining = 0f;
            HitDelayRemaining = 0f;
            CommittedTarget = SimEntityId.None;
            CommittedDirection = float2.zero;
            HasCommittedDirection = false;
            Lock = SimEntityId.None;
            FireCount = 0;
            // ⚠ `PatternSlots` 는 **비우지 않는다.** 슬롯이 발사 인스턴스와 그 간격·방향
            // 배열을 들고 있어서 버리면 다음 대여가 통째로 다시 할당한다(F4).
            // 개수·내용을 맞추는 것은 채우는 쪽(`CombatPhase.Fill`)이다.
            HasSummonedOnce = false;
            GrantedByTaunt = false;
            PreviousTargetMask = 0;
        }
    }

    // ── 행동 상태 ────────────────────────────────────────────────────────────
    //
    // **결정은 `Wassup.UnitAi`, 저장은 여기**다(spec 구현 12). 공격 루프와 **같은 술어 함수**를
    // 봐야 「락은 있는데 Marching」 데드락이 안 난다 — 옛 전투에서 두 벌이 갈렸을 때 적이
    // 대상을 문 채 발사도 않고 골로 걸어갔다.
    //
    // struct 인 이유: 개체마다 반드시 있고(UML §2 의 `*--`) 값이 둘뿐이라 할당할 이유가 없다.
    public struct AiStatus
    {
        public Wassup.UnitAi.DefenderAiState Defender;
        public Wassup.UnitAi.AiState Enemy;

        public void Reset()
        {
            Defender = Wassup.UnitAi.DefenderAiState.Ready;
            Enemy = Wassup.UnitAi.AiState.Marching;
        }
    }

    // ── 실드 ─────────────────────────────────────────────────────────────────
    public struct ShieldSlot
    {
        /// <summary>중첩 **키**일 뿐 수명 링크가 아니다 — 부여자가 죽어도 잔여 실드는 산다.</summary>
        public SimEntityId Source;
        public float Value;
    }

    public sealed class ShieldSlots
    {
        /// <summary>삽입 순서 = 부여 순서. FIFO 소모라 이 순서가 곧 규칙이다.</summary>
        public readonly List<ShieldSlot> Slots = new List<ShieldSlot>(2);

        public bool Any => Slots.Count > 0;

        public void Reset() => Slots.Clear();
    }

    // ── 인박스 ───────────────────────────────────────────────────────────────
    public struct DamageEntry
    {
        public float Amount;
        /// <summary>때린 자. `None` = 미귀속(지속 피해·환경·자해) → 처치 보상이 안 난다.</summary>
        public SimEntityId Source;
    }

    public struct ShieldGrant
    {
        public SimEntityId Source;
        public float Amount;
    }

    /// <summary>
    /// 이번 틱에 이 개체에게 들어온 것들. 옛 `IncomingDamage`/`IncomingHeal`/`IncomingShield`
    /// 버퍼의 후계다.
    ///
    /// ⚠ **비우는 시점이 셋이 같지 않다**(C17 — 현행 비대칭 그대로):
    ///   · 피해·회복 — **그 틱에** 비운다.
    ///   · 실드 부여 — **다음 틱** 드레인이 의도다. 그래서 생산자는 `ShieldPending` 에 쓰고,
    ///     틱 끝에서 `Shield` 로 옮겨진다. 버퍼가 사라지면 이 한 칸 차이가 조용히 없어진다.
    /// </summary>
    public sealed class Inbox
    {
        public readonly List<DamageEntry> Damage = new List<DamageEntry>(4);
        public readonly List<float> Heal = new List<float>(2);

        /// <summary>이번 틱 피해 단계가 소모할 부여분(지난 틱에 쌓인 것).</summary>
        public readonly List<ShieldGrant> Shield = new List<ShieldGrant>(2);
        /// <summary>이번 틱에 쌓이는 부여분. 틱 끝에서 위로 옮겨진다.</summary>
        public readonly List<ShieldGrant> ShieldPending = new List<ShieldGrant>(2);

        public bool Empty => Damage.Count == 0 && Heal.Count == 0 && Shield.Count == 0;

        /// <summary>실드 부여의 한 틱 지연을 만드는 **유일한 자리**. 틱 끝에서 불린다.</summary>
        public void StageShield()
        {
            if (ShieldPending.Count == 0) return;
            for (int i = 0; i < ShieldPending.Count; i++) Shield.Add(ShieldPending[i]);
            ShieldPending.Clear();
        }

        public void Reset()
        {
            Damage.Clear();
            Heal.Clear();
            Shield.Clear();
            ShieldPending.Clear();
        }
    }

    // ── 진행형 상태 ──────────────────────────────────────────────────────────
    //
    // **중단 정책 표**(census 열린 질문 2 — 「개시 의도는 다섯인데 대응하는 취소가 하나뿐」).
    // 옛 전투는 도중에 죽음·퇴근·CC 가 오면 무슨 일이 일어나는지를 각 시스템이 제각각 알았다.
    // 여기서는 **한 함수**(`Interrupt`)가 표를 이행한다:
    //
    //   | 상태          | 사망            | 퇴근(회수)        | CC              | 주인 소멸        |
    //   |---------------|-----------------|-------------------|-----------------|------------------|
    //   | 궁극기 도약   | **일어나지 않음**(C12 — 피해 버퍼를 비운다) | 취소·위치 복귀 | **면역**(잠금이 이미 걸려 있다) | N/A |
    //   | 치명 타이머   | 같이 사라진다   | 같이 사라진다     | 계속 흐른다     | N/A              |
    //   | 충전(더블파이어) | 같이 사라진다 | 같이 사라진다     | 유지(쿨은 CC 중에도 돈다) | N/A    |
    //   | 라스트런(6b2) | 같이 사라진다   | 같이 사라진다     | **계속 흐른다**  | N/A              |
    //
    // ⚠ 「궁극기 도약 중 사망이 없다」가 **착지 보장의 근거**다(C12). 가드가 사라지면 착지 예고
    // 미해제 경로가 한꺼번에 열린다 — 그래서 피해 단계가 이탈 중인 개체의 인박스를 **비운다.**
    public enum ProgressInterrupt : byte { Death = 0, Retire = 1, Cc = 2, OwnerDestroyed = 3 }

    /// <summary>
    /// 라스트런 창이 닫힌 까닭(`CoreEvent.LastRunEnded.Arg`). 닫히는 문은 둘 — 시간 끝(crash)과
    /// 중단 정책(`ProgressiveStates.Interrupt`) — 이고 둘 다 `BattleWorld` 가 사건을 낸다.
    /// append-only — 트레이스 `i` 칸에 그대로 실린다.
    /// </summary>
    public enum LastRunEndReason : byte
    {
        /// <summary>창의 시간이 다 됐다 — crash 피해가 같은 틱에 들어간다.</summary>
        Crash = 0,
        /// <summary>창이 열린 채 죽었다(crash 없음).</summary>
        Death = 1,
        /// <summary>창이 열린 채 퇴근했다(crash 없음).</summary>
        Retire = 2,
        /// <summary>죽음·퇴근이 아닌 제거(적 유출 · 디버그 제거 등).</summary>
        Removed = 3,
    }

    public sealed class ProgressiveStates
    {
        // 궁극기 도약 — 이탈(피격 불가 · 잠금 + 무적 **원자 개시**) → 예고 → 강습 → 착지 슬램.
        public bool LeapActive;
        public float LeapRemaining;
        /// <summary>착지 자리는 **발동 프레임에 고정**된다 — 예고는 약속이다.</summary>
        public float3 LandingWorld;
        public float SlamDamage;
        public int SlamTileRange;
        public int SlamProjectileDefIndex = -1;
        /// <summary>이탈 직전 자리. 퇴근 취소가 여기로 되돌린다.</summary>
        public float3 LeapOrigin;

        // 치명 타이머 — 시간이 끝나면 스스로 깎는다(자해라 킬 미귀속).
        public bool LethalActive;
        public float LethalRemaining;
        public float LethalFraction;

        /// <summary>다음 공격 한 번을 즉시 더 쏘는 충전. 각 발이 온전한 공격이다.</summary>
        public int Charge;

        // unit 6b2 — 라스트런(레드불) **지연 crash 타이머.** 공속 버프는 스탯 슬롯이 따로 들고
        // 스스로 만료된다 — 여기는 「시간이 끝나면 최대 체력의 일부를 스스로 깎는다」만 든다.
        // 별도 타이머 타입을 만들지 않은 이유: 집이 하나여야 중단 정책이 하나다(UML §2 `+LastRun?`).
        // ⚠ **켜져 있는 동안이 곧 재소비 락**이다 — 먹은 유닛은 crash 로 값을 치른 뒤에야 다시 먹는다.
        public bool LastRunActive;
        public float LastRunRemaining;
        public float LastRunFraction;

        /// <summary>라스트런 개시. 락이 걸려 있으면 부르지 않는다(대상 필터가 먼저 거른다).</summary>
        public void BeginLastRun(float seconds, float damageFraction)
        {
            LastRunActive = true;
            LastRunRemaining = seconds;
            LastRunFraction = damageFraction;
        }

        public bool Any => LeapActive || LethalActive || Charge > 0 || LastRunActive;

        /// <summary>
        /// 중단 정책 표의 **유일한 이행 지점**. 분기를 소비처로 흩지 말 것.
        /// 돌려주는 값 = 이 중단이 **열려 있던 라스트런 창을 닫았나** — 닫힘 사건은 이 값을 받은
        /// `BattleWorld.InterruptProgress` 가 낸다(여기는 버스를 모른다). 정책을 두 번 적지 않으려고
        /// 「닫혔나」를 표에서 다시 유도하지 않고 전후 값을 비교한다.
        /// </summary>
        public bool Interrupt(ProgressInterrupt reason)
        {
            bool lastRunWasOpen = LastRunActive;
            Apply(reason);
            return lastRunWasOpen && !LastRunActive;
        }

        private void Apply(ProgressInterrupt reason)
        {
            switch (reason)
            {
                case ProgressInterrupt.Cc:
                    // 도약은 면역(잠금이 이미 걸려 있다) · 치명·라스트런은 계속 흐른다 · 충전은 유지.
                    return;

                case ProgressInterrupt.Death:
                case ProgressInterrupt.OwnerDestroyed:
                    // 도약 중 사망은 정의상 일어나지 않지만(C12), 오버킬 경합으로 왔다면
                    // 착지 없이 상태만 걷어 「시체가 잠긴 채」 남지 않게 한다.
                    LeapActive = false;
                    LethalActive = false;
                    LastRunActive = false;
                    Charge = 0;
                    return;

                case ProgressInterrupt.Retire:
                    LeapActive = false;
                    LethalActive = false;
                    LastRunActive = false;
                    Charge = 0;
                    return;
            }
        }

        public void Reset()
        {
            LeapActive = false;
            LeapRemaining = 0f;
            LandingWorld = float3.zero;
            LeapOrigin = float3.zero;
            SlamDamage = 0f;
            SlamTileRange = 0;
            SlamProjectileDefIndex = -1;
            LethalActive = false;
            LethalRemaining = 0f;
            LethalFraction = 0f;
            LastRunActive = false;
            LastRunRemaining = 0f;
            LastRunFraction = 0f;
            Charge = 0;
        }
    }
}
