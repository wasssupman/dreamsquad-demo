using Wassup.Sim.Effects;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/2 — 드림캐쳐(발동형 카드) 어휘.
    ///
    /// **왜 참조가 아니라 이식인가**: 원본은 `Wassup.Data`(정의 계층) 인데 그 어셈블리는
    /// `UnityEngine`(`[Serializable]`·`ScriptableObject`)에 매달려 있다. sim asmdef 는
    /// `noEngineReferences: true` · `references: []` 라 컴파일러가 애초에 못 잇는다(I3).
    /// ⇒ **값만** 옮긴다. 저작 계층(SO 필드·인스펙터)은 그대로 `Wassup.Data` 가 소유하고,
    /// 18-K 의 bake 가 두 어휘를 잇는다(`DcCcKind`→`CcKind` 번역과 같은 자리).
    ///
    /// ⚠ **세 enum 전부 append-only.** 상태 해시가 정수로 찍고 카드 에셋이 int 로 직렬화하므로
    /// 중간 삽입은 기존 에셋을 통째로 재라벨한다. `DcPayloadKind` 는 원본이 명시값을 달아
    /// 두었으므로 여기서도 **명시값을 그대로 복사**한다 — 한쪽만 값을 지우면 두 어휘가
    /// 조용히 갈라진다.
    /// </summary>
    public enum DcTriggerKind
    {
        None, AttackN, OnDamagedN, OnDeath, PeriodicTimer, HealthThreshold, OnKill, OnShieldBreak
    }

    /// <summary>
    /// 발동 시 실행할 것. 값은 `Wassup.Data.DcPayloadKind` 와 **1:1 동일**해야 한다.
    ///
    /// 각 kind 가 어떤 슬롯 필드를 재사용하는지(같은 `magnitude`/`duration`/`tileRange` 가
    /// kind 마다 다른 뜻)는 원본 정의 계층 주석이 정본이다 — 여기 복제하지 않는다.
    /// sim 이 아는 것은 **분기 값**뿐이고, 해석은 각 payload 를 소비하는 시스템이 한다.
    /// </summary>
    public enum DcPayloadKind
    {
        None = 0,
        ProjectileToTarget = 1,
        SelfTileAoe = 2,
        NextAttackDoubleFire = 3,
        SelfBuffLethal = 4,
        AreaBarrage = 5,
        SelfBlink = 6,
        /// 예약(핸들러 미구현) — 어떤 카드도 쓰지 않는다. 값 보존 목적으로만 잔존.
        SelfWarmupBuff = 7,
        PlacementAura = 8,
        AllyMoveSpeedAura = 9,
        ApplyCcToTarget = 10,
        ApplyStackToTarget = 11,
        SelfStatBuff = 12,
        HeavyStrike = 13,
        DreamCocoon = 14,
        BountyMark = 15,
        AreaSleep = 16,
        EmitProjectilePattern = 17,
        UltimateLeap = 18,
    }

    /// 사건 트리거에 얹는 상태 술어. 트리거 kind(언제 평가하나)와 **직교** — 조합마다
    /// kind 를 늘리지 않기 위한 분해다. v1 어휘는 `HpBelow` 하나.
    public enum DcGateKind { None, HpBelow }

    /// 게이트의 주어. 배선된 조합은 <see cref="DcTrigger.GateComboSupported"/> 가 정본.
    public enum DcGateSubject { Self, EventTarget }

    /// <summary>
    /// battle-sim-extraction unit 18-G/2 — 부착된 발동형 메커니즘 한 인스턴스의 런타임 슬롯.
    /// 구 `Wassup.Battle.Combat.DcTriggerSlot`(`IBufferElementData`) 이식 — sim 에서는
    /// 그냥 버퍼 원소 struct 다(`SimWorld.WithBuffer&lt;DcTriggerSlot&gt;()`).
    ///
    /// **25필드를 통째로 옮긴 근거**: 이식 판정 기준은 "누가 소유하나" 가 아니라
    /// **"부분 이식이 해시를 깨뜨리는가"** 다(`AttackState` 9필드를 통째로 옮긴 것과 같은 기준).
    /// 이 슬롯은 카운터 3종(`counter`·`elapsed`·`nextBoundaryIndex`)이 **상태**라서
    /// 그중 하나만 빠져도 A/B 가 어긋난다. 필드를 골라 옮기면 18-K 에서야 빈 arm 이 드러나고
    /// 그때 되돌릴 반경이 훨씬 크다.
    ///
    /// **쓰기 소유(구 sim 계약 그대로 유지)**:
    /// <list type="bullet">
    /// <item>`counter` — 공격 루프 전용(#33). host 하나는 RESOLVE / 폭탄 발사 훅 / 캐스트 드레인
    ///       셋 중 **정확히 1곳만** 탄다(attack-decoupling 계약 2).</item>
    /// <item>`elapsed` — 주기 트리거(#42 계열)</item>
    /// <item>`nextBoundaryIndex` — 체력 임계(#43 계열)</item>
    /// </list>
    /// 셋이 서로 다른 시스템 소유라 한 슬롯을 세 시스템이 나눠 쓴다 — **필드별 소유**가
    /// 이 타입의 실제 경계다.
    ///
    /// ⚠ `patternIndex` 의 유효 초기값은 **-1**(미배선)이다. struct default 0 은 **유효 index** 라
    /// bake 가 명시 초기화하지 않으면 미배선 슬롯이 0번 패턴을 쏜다. 신 sim 에서 이 슬롯을
    /// 손으로 만드는 자리(테스트 포함)도 같은 의무를 진다.
    /// </summary>
    public struct DcTriggerSlot
    {
        /// 이펙트 인스턴스 id — 같은 카드를 두 장 붙이면 카운터가 독립이다.
        /// ⚠ `statBuffStackId`(StatModifier stackId 네임스페이스)와 **절대 비교하지 않는다**.
        public int instanceId;
        public DcTriggerKind trigger;
        /// AttackN: N 번째 RESOLVE 마다 발동. 0 = inert(<see cref="DcTrigger.Tick"/> 가드).
        public ushort period;
        public ushort counter;
        public DcPayloadKind payload;
        /// flat damage — 시전자의 `damageMul` 은 **의도적으로 적용하지 않는다**(카드 값 예측성).
        public float magnitude;

        // ── ProjectileToTarget 베이크 (부착 시점) ─────────────────────────────
        /// 세션 수명 index — 배치 시작이 리셋하지 않는다.
        public int projectileDataIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        /// SelfTileAoe: 폭발 반경 · AreaBarrage: 진앙 반경 · SelfBlink: 착지 탐색 반경.
        public int tileRange;

        // ── 주기 / 임계 트리거 상태 ────────────────────────────────────────────
        /// PeriodicTimer 주기 초. &lt;= 0 = no-fire(함수 내부 가드, 계약 9).
        public float periodSeconds;
        /// PeriodicTimer 누산기(잔여 이월 — drift-free).
        public float elapsed;
        /// HealthThreshold 경계 간격. &lt;= 0 = no-fire(동일 가드).
        public float fraction;
        /// HealthThreshold 래치 k — **베이크 시 1**, 단조 전진(회복해도 되감기지 않는다).
        public int nextBoundaryIndex;
        /// 스폰 시점 maxHp 스냅샷 — 경계 기준을 고정한다(게이트의 "현재 max" 와 다르다).
        public float maxHpRef;
        /// AreaBarrage 낙하 텔레그래프 초 · AllyMoveSpeedAura 펄스당 modifier TTL 초.
        public float duration;

        // ── 온-히트 payload 선택자 (bake 가 데이터 계층 enum 을 번역해 저장) ──
        public CcKind ccKind;
        public StackKind stackKind;
        /// SelfStatBuff 대상 스탯. 기본값 `DamageMul`(0) 은 SelfStatBuff 가 아닌 슬롯에선 inert.
        public StatKind buffStat;

        // ── 게이트 축 ─────────────────────────────────────────────────────────
        public DcGateKind gate;
        public DcGateSubject gateSubject;
        public float gateValue;

        /// <summary>
        /// SelfStatBuff 재부여 merge 키. ⚠ 위 `instanceId` 와 **다른 네임스페이스** —
        /// 이건 StatModifier stackId 쪽 값이다. 같은 슬롯이 매번 같은 stackId 로 넣어
        /// 비스택 refresh(지속만 갱신)가 된다. `instanceId` 를 잘라 쓰지 않는다.
        /// </summary>
        public ushort statBuffStackId;

        /// EmitProjectilePattern 이 가리키는 host 병렬 패턴 버퍼의 index. **미배선 = -1**(위 ⚠).
        public int patternIndex;

        // ── SelfBlink 착지 슬램 ───────────────────────────────────────────────
        /// 0 = 슬램 없음. 슬램 타이밍은 **뷰 도착 프레임**이라 소비자는 브리지다(sim 은 값만 싣는다).
        public float slamDamage;
        public int slamTileRange;
    }
}
