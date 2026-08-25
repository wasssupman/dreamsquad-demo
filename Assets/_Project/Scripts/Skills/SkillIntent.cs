using Unity.Mathematics;

namespace Wassup.Skills
{
    // skill-layer-foundation unit 3 — 스킬이 «무엇을 하고 싶은지» 말하는 어휘.
    //
    // concrete 는 상태를 바꾸지 않는다. 의도를 방출하고, 적용 시점·순서는 어댑터와
    // 소유 맥락이 정한다(계약 3). 그래서 도메인은 `IncomingDamage` 가 버퍼인지 큐인지
    // 모르고, 알 필요도 없다.
    //
    // ⚠ **두 계열로 갈린다**(unit 0 실측). 섞으면 안 된다:
    //   · `SimIntent`  — 시뮬레이션 상태를 바꾼다. 큐에 실려 다음 프레임에 소비된다.
    //   · `MetaIntent` — 판 밖 런타임(코스트·쿨다운)을 바꾼다. **즉시 반영**이고
    //     `battle-sim-extraction` M1 이후에도 Mono 쪽에 남는다.
    // 같은 큐에 넣으면 코스트 획득이 한 프레임 늦어지고, sim lib 을 뽑아낼 때
    // 그 둘이 한 덩어리로 딸려간다.

    public enum SimIntentKind : byte
    {
        None = 0,
        DealDamage,          // → IncomingDamage 인박스
        Heal,                // → IncomingHeal 인박스
        ApplyStatModifier,   // → StatModifierApplyEvents  ⚠ 병합 키 계약(아래)
        ApplyStack,          // → StackModifierApplyEvents
        ApplyCc,             // → EnemyCcEvents (이름과 달리 **진영 중립 채널**이다)
        ApplyDot,            // → DotApplyEvents
        ClearCc,             // → CcClearRequests
        GrantShield,         // → IncomingShield 인박스 (⚠ 다음 프레임 드레인이 의도)
        Taunt,               // → AggroAcquireEvents
        CreditThreat,        // → ThreatHitEvents
        Blink,               // → BlinkRequestEvents
        SpawnProjectile,     // → ProjectileSpawnRequest 캐리어
        EmitPattern,         // → PatternSlot 전진 + EmitterInstance (⚠ 성사와 원자)
        SpawnZoneCarrier,    // → EffectSpawner (장판·링크·오라 캐리어)

        // 연출 신호. **시뮬 상태를 안 바꾼다** — 그런데 어휘에 있어야 하는 이유는
        // 「언제 트는가」가 스킬의 판단이기 때문이다(효과 0이면 안 튼다, dataIndex<0 이면
        // 무연출 저작). 무엇을 어떻게 그리는지는 어댑터와 뷰가 소유한다.
        PlayVisual,
    }

    public enum MetaIntentKind : byte
    {
        None = 0,
        GainCost,            // → CostRuntime
        ReduceSkillCooldown, // → SkillRuntime
    }

    // 의도 하나. 페이로드가 kind 마다 다르지만 **필드를 겸직시키지 않는다** —
    // 겸직이 오늘 `DcTriggerSlot` 을 못 읽게 만든 원인이고(`tileRange` 13의미),
    // 그걸 그대로 물려받으면 이 레이어를 만드는 이유가 없어진다.
    // 필드가 남는 것은 값이 싸기 때문이다(struct, 큐에 실린다).
    public struct SimIntent
    {
        public SimIntentKind Kind;

        public SkillEntityId Target;   // 대상. 무효면 «자리»(Cell/Position)가 대상이다
        public int2 Cell;
        public float3 Position;
        public float2 DirectionXZ;

        public float Amount;           // 피해·회복·실드량·배율 — kind 가 뜻을 정한다
        public float Duration;
        public int TileRange;
        public int Count;              // 스택 수·발사 수·대상 상한

        public int DataIndex;          // 탄·해저드·패턴 index. **−1 = 없음**(0 은 유효)
        public int StackId;            // ⚠ ApplyStatModifier 의 병합 키 일부(아래)
        public int Selector;           // stat/cc/stack kind — 어댑터가 도메인 enum 으로 번역

        // 모디파이어 축. `Selector` 하나에 packing 하지 않는다 — 그 겸직이 이 레이어를
        // 만드는 이유였다.
        public SkillCombineOp Op;
        public SkillModifierOrigin Origin;

        // ⚠ 병합 키는 `(Source, Selector(stat), Op, StackId)` 다. **이 넷이 회수(revoke)
        // 가능성의 조건이다** — 회수가 「제거」가 아니라 같은 키로 항등을 재발행하는
        // 중립화라서, 키 구성이 바뀌면 host 가 죽어도 버프가 안 풀린다(투트랙 리뷰 지적).
        public SkillEntityId Source;
    }

    public struct MetaIntent
    {
        public MetaIntentKind Kind;
        public float Amount;
    }
}
