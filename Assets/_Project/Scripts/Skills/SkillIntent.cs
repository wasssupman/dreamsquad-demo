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

        // 진행형 상태 **개시**. 스킬은 「시작한다 + 수치」까지고 굴리는 것은 시스템이다
        // (계약 5). 이 의도가 필요한 이유: 개시가 **두 컴포넌트의 원자 동시 부착**이라
        // 어느 하나만 붙는 프레임이 있으면 안 된다(잠금 없이 무적이거나 그 반대).
        BeginUltimateLeap,

        // skill-layer-migration unit 2e — **자기 자신을 묶는다.** 스킬이 남에게 하는 일은
        // 다 표현됐는데 「나는 지금부터 N초 못 때린다」가 어휘에 없었다. 채널링(조사·시전)이
        // 계속 나올 개념이라 버스터즈 하나 때문이 아니라 **구멍**이라 메운다.
        //
        // ⚠ **새 컴포넌트를 만들지 않는다.** 그 필드는 이미 있다 — `AttackState` 의 공격
        // 대기 시간이고, 어댑터가 `max(현재, 요청)` 으로 민다(이미 걸린 대기를 줄이지
        // 않는다는 레거시 규칙 그대로). 도메인은 그것이 쿨다운인지 락인지 모른다.
        DelaySelfAttack,

        // 진단. 스킬이 **실패를 보고**한다 — 조용한 no-op 이 「왜 안 나왔지」를
        // 영영 못 풀게 만드는 자리가 있다(생존당 1회 스킬은 재현도 안 된다).
        // 문자열은 도메인에 두지 않는다 — 코드만 보내고 문장은 어댑터가 만든다.
        Report,

        // 연출 신호. **시뮬 상태를 안 바꾼다** — 그런데 어휘에 있어야 하는 이유는
        // 「언제 트는가」가 스킬의 판단이기 때문이다(효과 0이면 안 튼다, dataIndex<0 이면
        // 무연출 저작). 무엇을 어떻게 그리는지는 어댑터와 뷰가 소유한다.
        PlayVisual,

        // skill-layer-migration unit 3d‴ — **다음 공격을 예약한다.** 「지금 무엇을 한다」가
        // 아니라 「다음 번에 이렇게 된다」라, 기존 어휘 어디에도 없었다.
        // 스탯 모디파이어가 아닌 이유: 배율이 아니라 **횟수**이고 시간이 아니라 **다음 1회**로
        // 만료된다 — 모디파이어로 흉내내면 만료 조건이 시계에 매여 계약이 갈린다.
        //
        // ⚠ 「두 번 나간다」는 이 의도의 뜻이 아니다. 스킬은 충전(Amount 발)을 얹을 뿐이고
        // **그것을 어떻게 쓰는지는 소비자(RESOLVE)가 소유한다.** 그래서 이름이 DoubleFire 가
        // 아니라 GrantCharge 다 — 다른 소비 규칙이 붙어도 이 어휘는 그대로다.
        GrantCharge,

        // skill-layer-migration unit 3f — **한 점을 도는 탄.** 날아가는 탄과 다르다:
        // 목적지가 없고 중심과 반경과 각속도가 있으며, 수명이 다할 때까지 스치는 것을 때린다.
        // 기존 `SpawnProjectile` 에 얹지 않은 이유는 그 자리형 분기가 «떨어져서 한 번 터진다»
        // 로 굳어 있어서다 — 겸직시키면 어느 필드가 어느 모양에 쓰이는지 다시 흐려진다.
        SpawnOrbitProjectile,

        // skill-layer-migration unit 4a — **시한부로 만든다**(마지막 불꽃·재앙의 심장).
        // 「지금 무엇을 한다」가 아니라 「이 시간 뒤에 죽는다」다. 스탯 버프와 짝을 이루지만
        // **버프의 만료와 죽음은 다른 사건**이라 한 의도로 합치지 않는다 — 버프는 모디파이어
        // 시계가, 죽음은 `LethalTimer` 가 각각 소유하고, 저작이 둘을 같은 초로 적을 뿐이다.
        StartLethalTimer,

        // skill-layer-migration unit 4b — **완주하면 보상, 맞으면 파탄**(호접몽).
        // `BeginUltimateLeap` 과 같은 부류다 — 「진행형 상태 개시」이고, 개시가
        // **두 가지의 동시 부착**(잠 + 완주 감시)이라 어느 하나만 붙는 프레임이 없어야 한다.
        // 굴리는 것과 완주 판정은 시스템이 소유한다(계약 5).
        BeginDreamCocoon,

        // skill-layer-migration unit 4c — **이 대상을 잡으면 더 받는다**(살찌운 제물).
        // 다른 의도들이 「값을 준다」인 것과 달리 이것은 **가진 값을 배로 만든다** —
        // 읽고 곱해서 되쓴다. 그래서 `Amount` 는 양이 아니라 **배율**이다.
        //
        // ⚠ 대상이 **적**이다. 이 레이어에서 유일하게 「적을 이롭게 하지 않으면서
        // 적에게 거는 표식」이고, 그 값은 그 적이 죽을 때 소비된다.
        ScaleKillReward,
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

        // 궤도 위 시작 각도(라디안). 여러 개를 균등 배치하는 것이 스킬의 판단이라
        // 어댑터가 개수에서 유도하지 않는다 — 스킬이 하나씩 위상을 정해 보낸다.
        public float Phase;

        public float Amount;           // 피해·회복·실드량·배율 — kind 가 뜻을 정한다
        public float Duration;
        public int TileRange;

        // 월드 반경. `TileRange`(칸)와 **겸직시키지 않는다** — 궤도는 칸이 아니라 월드
        // 거리로 돌고, 각속도 계산이 이미 이 값을 썼다. 어댑터가 칸에서 다시 유도하면
        // 같은 수의 출처가 둘이 된다.
        public float Radius;
        public int Count;              // 스택 수·발사 수·대상 상한

        public int DataIndex;          // 탄·해저드 에셋 index. **−1 = 없음**(0 은 유효)
        // 발사 명세 슬롯 index. `DataIndex` 와 **겸직시키지 않는다** — 그쪽은 전역
        // 에셋 표를 가리키고 이쪽은 host 자기 버퍼(`PatternSlot`)의 자리다. −1 = 없음.
        public int PatternIndex;
        // 저작 탄 궤적 축(불투명). `SpawnProjectile` 전용 — 0 = 어댑터 기본(자리 폭발).
        public int ProjectileMovement;
        public int ProjectilePayload;
        // 탄 속도. `Duration`(비행 시간)과 **다른 축**이다 — 하나는 «얼마나 빨리»,
        // 다른 하나는 «몇 초 뒤에 닿나» 이고 궤적마다 쓰는 쪽이 다르다.
        public float Speed;
        // 연출 크기 배율. 0 = 저작 없음 → 어댑터가 1 로 읽는다.
        // ⚠ `HitThreshold`(맞는 반경)와 겸직시키지 않는다 — 한때 그랬고, 그 겸직이
        // 「스침 반경을 키우면 그림도 커지는」 조용한 결합을 만들었다.
        public float VisualScale;
        // killer 사양. 0 으로 새면 **무제한 통과**가 된다.
        public byte TargetTraversalLayers;
        public int StackId;            // ⚠ ApplyStatModifier 의 병합 키 일부(아래)
        public int Selector;           // stat/cc/stack kind — 어댑터가 도메인 enum 으로 번역
        public float HitThreshold;     // 탄 피격 반경 · 연출 배율 등 kind 별 보조 스칼라

        // 모디파이어 축. `Selector` 하나에 packing 하지 않는다 — 그 겸직이 이 레이어를
        // 만드는 이유였다.
        public SkillCombineOp Op;
        public SkillModifierOrigin Origin;

        // ⚠ 병합 키는 `(Source, Selector(stat), Op, StackId)` 다. **이 넷이 회수(revoke)
        // 가능성의 조건이다** — 회수가 「제거」가 아니라 같은 키로 항등을 재발행하는
        // 중립화라서, 키 구성이 바뀌면 host 가 죽어도 버프가 안 풀린다(투트랙 리뷰 지적).
        public SkillEntityId Source;

        // `Report` 전용 — 무엇이 왜 실패했나.
        public SkillReport Report;
    }

    // 스킬이 보고할 수 있는 실패. 늘리기 전에 「이게 조용하면 진단이 불가능한가」를 물어라.
    public enum SkillReport : byte
    {
        None = 0,
        NoLandingSpot,   // 밀집 셀이 없거나 링 안에 갈 수 있는 칸이 없다
    }

    public struct MetaIntent
    {
        public MetaIntentKind Kind;
        public float Amount;
    }
}
