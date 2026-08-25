using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat
{
    // dreamcatcher-unit-trigger Unit 1 — baked, unmanaged form of one unit-bound
    // triggered card mechanic (definition layer: DcMechanic). Combat-owned:
    // counter writes happen only in AttackSystem RESOLVE; attach/remove happens
    // only through BattleBridge (the sole MonoBehaviour↔ECS gateway).
    [InternalBufferCapacity(2)]
    public struct DcTriggerSlot : IBufferElementData
    {
        // Effect-instance id — one slot per attached mechanic instance, so two
        // copies of the same card get independent counters. Separate namespace
        // from stat-modifier stackId: never compare the two.
        // skill-layer-foundation unit 4/5 — **이중 경로 라우팅 축.**
        //
        // 0 = legacy arm 이 처리한다. 0 이 아니면 감지자가 arm 을 돌리지 않고
        // `SkillFiredEvent` 에 실어 보내고 디스패처가 concrete 를 부른다.
        //
        // ⚠ 이 축이 **unmanaged 여야 하는 이유**: 감지자는 Burst ISystem 이라 managed
        // 레지스트리를 읽을 수 없다. 「이 슬롯은 새 경로인가」를 숫자 하나로 가를 수
        // 있어야 이전 중 매 커밋에서 게임이 돈다.
        public int skillId;

        public int instanceId;
        public DcTriggerKind trigger;
        public ushort period;   // AttackN: fire on every N-th attack resolve
        // owned write: AttackSystem 전용 — RESOLVE / 폭탄 발사 훅 / 캐스트 드레인
        // 세 지점이며, host 하나는 그중 정확히 1곳만 탄다(attack-decoupling 계약 2).
        public ushort counter;
        public DcPayloadKind payload;
        public float magnitude; // flat damage — attacker damageMul intentionally not applied

        // Baked from ProjectileData at attach time (ISystem cannot read managed SOs).
        // projectileDataIndex lifetime = session: _projectileDataByIndex is never
        // reset by BeginPlacement.
        //   ProjectileToTarget : speed = 월드 속도 · hitThreshold = 도달 판정 반경
        //   SelfOrbitProjectile: speed = **궤도 선속도**(arm 이 ÷반경 → 각속도) ·
        //                        hitThreshold = **피격 반경**(궤도 반경과 다른 축!)
        public int projectileDataIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        // dreamcatcher-content-5 unit 0 — 탄 SO 의 flightMode 를 bake 가 ECS 축으로 번역해
        // 싣는다(ResolveProjectileAxes — 저작→축 번역의 단일 지점). 위 speed/hitThreshold 와
        // 같은 자리·같은 이유(ISystem 이 managed SO 를 못 읽는다).
        //
        // 왜 필요했나: `ProjectileToTarget` 발사 arm 이 여태 (HomingToEntity, SingleSplash)
        // 를 **하드코딩**해서, 저작자가 탄 SO 에 어떤 비행을 골라도 유도탄으로 나갔다.
        // 기본값(0,0)이 바로 그 레거시 짝이라 기존 카드는 무변화다.
        public Projectile.MovementKind projectileMovement;
        public Projectile.PayloadKind projectilePayload;
        // dreamcatcher-content-5 unit 4 — SpawnHazard(잿불)가 깔 장판 SO 의 브리지
        // 레지스트리 인덱스. 소비는 **Units 맥락의 킬 처리**(RO 읽어 킬 이벤트에 스탬프)
        // 이고 실제 스폰은 브리지다 — 시체폭발과 같은 형태.
        // ⚠ **-1 이 «없음»** 이다(struct default 0 은 유효 index 다 — patternIndex 선례).
        // projectileDataIndex 를 겸직시키지 않는 이유: 레지스트리가 다르다.
        public int hazardDataIndex;
        // dreamcatcher-content-1 — SelfTileAoe(OnDeath 폭발): AOE 반경(타일). 기본 0.
        // nightmare-catcher — AreaBarrage: 진앙 AoE 반경 / SelfBlink: 착지 탐색 반경.
        // content-4 — SelfOrbitProjectile: **궤도 반경**(타일). 피격 반경은 hitThreshold 다.
        public int tileRange;

        // ── nightmare-catcher unit 5 — periodic/threshold trigger state +
        // barrage payload params. ⚠ dreamcatcher-content-4 unit 0 — periodSeconds 는
        // 이제 **카드 bake 도 싣는다**(불꽃 팽이). 예전 주석의 "보스 스폰 경로만 bake
        // (디펜더 카드는 0=inert)" 는 그 값을 안 실어 보내 생긴 조용한 무발동이었고,
        // 지금은 <=0 저작이 loud 거절된다. fraction/maxHpRef/nextBoundaryIndex
        // 는 dreamcatcher-kill-and-threshold unit 1 에서 디펜더 last_stand
        // (HealthThreshold×SelfStatBuff)도 bake 한다. Owned writes stay Combat:
        // elapsed/fireCount = BossPeriodicTriggerSystem, nextBoundaryIndex =
        // HealthThresholdSystem (counter above stays AttackSystem-only).
        public float periodSeconds;   // PeriodicTimer 주기 초 (<=0 = no-fire, 계약 9)
        public float elapsed;         // PeriodicTimer accumulator (잔여 이월)
        // (구 AreaBarrage 진앙 round-robin 카운터 fireCount 는 제거됐다 — 융단폭격이
        //  패턴으로 이관되며 영속 카운터가 PatternSlot.fireCountBase 로 옮겨갔다.)
        public float fraction;        // HealthThreshold 경계 간격 (<=0 = no-fire)
        public int nextBoundaryIndex; // HealthThreshold 래치 k (베이크 시 1, 단조 전진)
        public float maxHpRef;        // 스폰 시점 maxHp 스냅샷 (경계 기준 고정)
        public float duration;        // AreaBarrage 낙하 텔레그래프 초 → SkyFall flightTime
                                      // nightmare-whip-aura — AllyMoveSpeedAura: 펄스당 modifier TTL 초
                                      // content-4 — SelfOrbitProjectile: 화염구 **지속 초**(수명)
                                      // content-4 — SelfTileAoe: **낙하 예고 초**(퇴근 운석만 소비.
                                      //   기존 SelfTileAoe 카드는 전부 0 이라 즉시 착탄 그대로)

        // dreamcatcher-new-abilities unit 0 — 온-히트 payload 선택자. bake 시 데이터
        // 계층 DcCcKind/DcStackKind 를 Battle enum 으로 번역 저장(hot path 무번역).
        // ApplyCcToTarget=ccKind, ApplyStackToTarget=stackKind. 소비는 unit 1.
        public Wassup.Battle.Effects.CcKind ccKind;
        public Wassup.Battle.Effects.StackKind stackKind;

        // dreamcatcher-kill-and-threshold unit 0 — SelfStatBuff 대상 스탯. bake 시
        // CardBuffKind→StatKind 번역 저장(ccKind/stackKind 선례). arm 은 magnitude(배율)·
        // duration(TTL, <=0=영구)과 함께 self 에 StatModifierApplyEvent enqueue. 기본값
        // DamageMul(0) 은 SelfStatBuff 가 아닌 슬롯에선 inert.
        public Wassup.Battle.Effects.StatKind buffStat;
        // dreamcatcher-trigger-gates unit 1 — 게이트 축 (AttackN×EventTarget 배선).
        // bake 가 GateComboSupported 로 걸러 배선 조합만 착지. 판정은 AttackSystem 의
        // pre-scan(WouldFire∧GatePass)과 counter 루프(if(GatePass) Tick) 두 곳 —
        // 같은 프레임·같은 bestTarget·같은 HP 스냅샷이라 결과가 반드시 일치(합성 불변식).
        public Wassup.Data.DcGateKind gate;
        public Wassup.Data.DcGateSubject gateSubject;
        public float gateValue;

        // dreamcatcher-kill-and-threshold unit 1 — SelfStatBuff 재부여 merge 키(stackId).
        // 위 instanceId(트리거 인스턴스 네임스페이스)와 달리 이건 **StatModifier stackId
        // 네임스페이스**의 값 — bake 가 BattleBridge._dcStackCounter(squad 이펙트와 동일
        // 단일 할당자)에서 뽑는다. 같은 슬롯이 매 킬/틱 같은 stackId 로 enqueue → 비스택
        // refresh(지속만 갱신). instanceId 를 잘라 쓰지 않으므로 두 네임스페이스는 여전히
        // 분리(위 instanceId 주석의 불변식 유지).
        public ushort statBuffStackId;

        // projectile-emission-pattern unit 3 — EmitProjectilePattern 의 발사 명세는
        // 이 슬롯에 임베드하지 않고 host 의 병렬 PatternSlot 버퍼에 두고 **index 만**
        // 가리킨다. 이 struct 는 defender 카드 슬롯과 공유하는 원소 타입이라, 여기에
        // spec+template(~200B)을 넣으면 소비자는 보스뿐인데 모든 드림캐쳐 보유 유닛의
        // chunk 상주 비용이 커진다. **bake 가 -1 로 명시 초기화**한다(struct default 0
        // 은 유효 index 라 미배선 슬롯이 0번 패턴을 쏘게 된다).
        public int patternIndex;

        // boss-jjangssen unit 7 — SelfBlink 착지 슬램 파라미터. 슬램은 **뷰가 착지한 프레임**에
        // 터져야 하므로(sim 은 이미 텔레포트했다) 브리지 비행 코루틴이 소비한다 — 이 슬롯은
        // 값을 실어 BossLeapVisualEvent 로 넘기는 역할만 한다. 0 = 슬램 없음.
        public float slamDamage;
        public int slamTileRange;

        // elite-enemy-tier unit 4 — AreaBreath(화염 브레스) 부채꼴.
        // ★**저작은 도(degree), 런타임은 코사인²** 이다. 변환은 bake 에서 1회 — sim 이 삼각함수를
        // 부르지 않고, 저작값 하나가 두 표현으로 갈리지 않는다. 판정은 `TileAoe.IsInCone`.
        // `coneHalfAngleDeg` 는 **뷰가 쓴다**(부채꼴 VFX 폭) — sim 은 cosSq 만 본다.
        public float coneCosSq;
        public float coneHalfAngleDeg;
    }
}
