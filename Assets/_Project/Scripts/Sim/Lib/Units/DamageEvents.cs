using Wassup.Sim.Combat;

namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 적 처치 원샷. 구 `EnemyKilledEvent` 이식.
    ///
    /// ⚠ 필드 넷(`awakeningReward`·`killScore`·burst 4종)이 **값 복사**인 이유는 하나다 —
    /// 소비 시점엔 엔티티가 이미 파괴돼 있다. 참조로 두면 드레인이 빈손이 된다.
    /// `entity` 만 예외로 실리는데, 그건 **등록부 키로만** 쓴다(역참조 금지 — 파괴 후에도
    /// id 비교는 유효하다).
    ///
    /// ⚠ **유출된 적은 이 이벤트를 내지 않는다** — 목표 도달 제거는 수명 시스템이 처리하고
    /// HP&lt;=0 분기를 지나지 않는다.
    /// </summary>
    public struct EnemyKilledEvent
    {
        public SimVec3 position;
        public int awakeningReward;
        /// 등록부 키 전용(역참조 금지).
        public SimEntityId entity;
        public int killScore;

        // ── 시체폭발: killer 의 OnKill×SelfTileAoe 첫 매칭 슬롯을 킬 시점에 스탬프 ──
        // 드레인 시점엔 슬롯을 못 읽으므로 값을 실어 보낸다. 폭발발 킬이 OnKill 을 재발동시키는
        // 연쇄가 사양이다(flat 데미지 + 반경이 자연 제동).
        public bool hasKillBurst;
        public float burstDamage;
        public int burstTileRange;
        public int burstDataIndex;
        /// 폭발 데미지 귀속용 owner.
        public SimEntityId killer;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 적 피격 표시 원샷. 구 `DamageNumberEvent` 이식.
    ///
    /// ⚠ `amount` 는 **히트 1건**의 경감 후 피해다 — 프레임 합이 아니다. 버퍼 엔트리마다 하나씩
    /// 나가므로 같은 프레임의 투사체 히트와 드림캐쳐 히트가 두 개의 숫자로 보인다.
    /// 반면 `hpRatio` 는 **프레임 정산 후** 비율이다(치명타 프레임엔 0). 이 프레임의 모든 폰트가
    /// 같은 최종 비율을 싣는 것이 의도다 — 마이크로바가 한 번만 움직인다.
    /// </summary>
    public struct DamageNumberEvent
    {
        public SimVec3 position;
        public float amount;
        public SimEntityId entity;
        public float hpRatio;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 회복 펄스 원샷. 구 `HealAppliedEvent` 이식.
    /// ⚠ `RegenPerSec` 은 **의도적으로 제외**된다(매 프레임 VFX 도배 방지).
    /// </summary>
    public struct HealAppliedEvent
    {
        public SimVec3 position;
        public float amount;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-G/3 — 실드 파열 / 피격 폭발 원샷. 구 `ShieldBreakEvent` 이식.
    ///
    /// **두 사건이 한 채널을 공유한다**: ① 실드가 피격으로 완전 소진(Sum&gt;0→0)
    /// ② `OnDamagedN × SelfTileAoe`(피격 폭발). 발동 시점이 같고(Units, 피해 정산) 실행기가
    /// 같아서 신규 채널을 만들지 않았다 — 구분은 <see cref="fromDamagedTrigger"/>.
    ///
    /// ⚠ 시간 만료로 사라지는 실드는 이 경로를 타지 않는다 — 파열 감지가 `Absorb` 전용이라
    /// **구조적으로** 배제된다(조건문이 아니라 호출 지점이 보장한다).
    /// </summary>
    public struct ShieldBreakEvent
    {
        public SimEntityId host;
        /// AoE 중심 / 적 쿼리 중심.
        public SimVec3 position;
        public DcPayloadKind payload;
        /// SelfTileAoe: AoE 데미지 / AreaSleep: 적 수 cap(M).
        public float magnitude;
        /// AoE 반경 / 수면 반경 (Chebyshev, N).
        public int tileRange;
        /// AreaSleep: 수면 초(L).
        public float duration;
        /// SelfTileAoe: AoE 뷰 데이터 index (-1 = 없음).
        public int aoeDataIndex;
        /// true = 피격 폭발(OnDamagedN 발), false = 실드 파열.
        public bool fromDamagedTrigger;
    }
}
