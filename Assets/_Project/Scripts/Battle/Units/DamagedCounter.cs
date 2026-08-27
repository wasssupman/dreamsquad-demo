using Unity.Entities;

namespace Wassup.Battle.Units
{
    // dreamcatcher-content-1 unit 3 (① 가시 갑옷) — per-instance "N회 피격" counter,
    // owned by Units. Kept OUT of Combat's DcTriggerSlot on purpose: the count is
    // written where the defender takes damage (DamageApplicationSystem, Units), and
    // component/buffer writes must stay within the owning context (TRD 맥락 경계).
    // Buffer element, not a single component, so two copies of the same card get
    // independent counters (mirrors DcTriggerSlot). Attach via BattleBridge only.
    [InternalBufferCapacity(2)]
    public struct DamagedCounter : IBufferElementData
    {
        public int instanceId;

        // skill-layer-migration unit 3d‴ — 라우팅 키. `DcTriggerSlot` 과 **다른 버퍼**라
        // 그쪽 키를 못 빌린다(카운터 쓰기가 Units 소유라 여기 산다 — 위 주석).
        // 0(=NotRouted) 이면 **스킬이 아니다**(발동 규칙·공격의 성질). arm 은 철거됐고, 아니면 스킬 레이어가 실행한다.
        public int skillId;
        public ushort period;   // OnDamagedN: fire on every N-th damaged frame
        public ushort counter;  // owned write: DamageApplicationSystem only

        // dreamcatcher-trigger-gates unit 0 — payload 개통 (위드닝). DcTriggerSlot 로
        // 통합하지 않는 이유는 위와 동일: counter 쓰기가 Units 라 소유를 여기 둔다.
        // NextAttackDoubleFire = 기존 charge handoff, SelfTileAoe = ShieldBreakEvents
        // 큐로 emit(이 시스템이 이미 쓰는 채널). 나머지 kind 는 발동 시 loud 경고.
        public Wassup.Data.DcPayloadKind payload;
        public float magnitude;  // SelfTileAoe: flat AoE 데미지
        public int tileRange;    // SelfTileAoe: Chebyshev 반경
        public int aoeDataIndex; // SelfTileAoe: AoE view ProjectileData index (-1 = none)
        // 2026-08-26 사용자 결정 — 폭발 그림 배율을 저작이 정한다. 레거시는 1 로
        // 하드코딩해서 탄 에셋의 값이 무시됐고, **같은 탄**이 경로마다 다른 크기로
        // 나왔다. 형제들은 `DcTriggerSlot.visualScale` 을 쓰는데 이 버퍼는 따로라
        // 필드도 여기 산다. 0 = 저작 없음 → 어댑터가 1 로 읽는다.
        public float aoeVisualScale;

        // dreamcatcher-trigger-gates unit 1 — 게이트 (OnDamagedN×Self 배선 전용이라
        // subject 필드는 생략 — Self 고정). 판정 hp = 이 피격 프레임의 적용 후(newHp):
        // "이하 상태로 만든 그 피격부터" 카운트된다. gate=None 기본값 = 기존 카드 무손상.
        public Wassup.Data.DcGateKind gate;
        public float gateValue;
    }
}
