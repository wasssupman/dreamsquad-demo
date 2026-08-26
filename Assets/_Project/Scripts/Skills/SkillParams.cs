namespace Wassup.Skills
{
    // skill-layer-foundation unit 3 — 저작 수치가 concrete 에 도달하는 형태.
    //
    // ⚠ **오늘의 슬롯 스칼라를 그대로 물려받지 않는다.** `DcTriggerSlot` 은
    // `tileRange` 하나가 **13가지 의미**를 겸직하고(AoE 반경·궤도 반경·maxStack·
    // 피해감소%·폴백 반경·착지 링 상한·최대중첩·조준 사거리 …), 같은 kind 안에서도
    // 탄 궤적에 따라 「반경」↔「비행 거리」로 뜻이 바뀐다. 그 겸직이 이 코드를 못 읽게
    // 만든 원인이고, 그대로 옮기면 레이어를 만드는 이유가 사라진다(unit 0 실측).
    //
    // 그래서 **읽는 쪽이 이름으로 읽는다**. 이 struct 는 슬롯 위에 씌우는 **뷰**이고,
    // 슬롯→뷰 번역은 디스패처가 skillId 별로 한 번 한다. 슬롯 자체는 그대로 굽는다
    // (계약: bake 무변경 — 골든·시트·저작이 전부 그 형식에 매여 있다).
    //
    // `skill-fire-dispatch` rev 4 의 「params 뷰 struct」를 계승한다. 새 발명이 아니다.
    public readonly struct SkillParams
    {
        // 슬롯의 원시 스칼라. concrete 는 **이걸 직접 읽지 않는다** — 아래 뷰 타입이
        // 이름을 붙여 읽는다. public 인 이유는 번역층이 skillId 별 뷰를 만들 때 쓰기 때문.
        public readonly float Magnitude;
        public readonly float Duration;
        public readonly int TileRange;
        public readonly int Period;
        public readonly int DataIndex;   // ⚠ **−1 = 없음.** 0 은 유효한 index 다
        // 발사 명세 슬롯 index. **`DataIndex` 와 겸직시키지 않는다** — 그쪽은 전역
        // 에셋 표(탄·해저드 프리팹)를 가리키고 이쪽은 **host 자기 버퍼**(PatternSlot)의
        // 자리를 가리킨다. 한 칸에 접으면 「0번 탄」과 「0번 패턴」이 같은 값이 되고,
        // 그 혼동은 조용하다. 역시 **−1 = 없음**.
        public readonly int PatternIndex;
        // 저작 스탯 축(`SkillStatKind`). `Selector`(cc/stack)와 다른 축이다.
        public readonly int StatSelector;
        // 저작 스택 축(`SkillStackKind`).
        public readonly int StackSelector;
        // 저작 탄 궤적 축. **도메인은 해석하지 않는다**(`DataIndex` 와 같은 불투명 토큰) —
        // 「어떤 탄을 쏘나」는 저작의 사실이고, 스킬의 판단은 「쏘나 · 누구에게」까지다.
        public readonly int ProjectileMovement;
        public readonly int ProjectilePayload;
        // killer 사양(발화 시점 스냅샷). 0 으로 새면 **무제한 통과**가 된다 —
        // 죽음 계열은 드레인 시점에 host 가 이미 없어 재질의가 불가능하다.
        public readonly byte TargetTraversalLayers;
        // **사건이 일어난 자리.** 시전자 자리(`ctx.Position(caster)`)와 다르다 —
        // 죽음 계열은 피해자가 쓰러진 곳이 그 자리이고, 드레인 시점엔 그 엔티티가
        // 이미 없어 **재질의가 불가능**하다. 그래서 발화 시점 좌표를 싣는다.
        public readonly Unity.Mathematics.float3 EventPosition;
        // 해저드 저작 index. **`DataIndex`(탄·연출)와 다른 표**를 가리킨다 —
        // 겸직시키면 「0번 탄」과 「0번 장판」이 같은 값이 된다. −1 = 없음.
        public readonly int HazardDataIndex;
        public bool HasHazard => HazardDataIndex >= 0;
        public readonly int Selector;    // stat/cc/stack kind 등 저작 enum
        // unit 5b — 대상 수 상한(0 = 없음) · 자기 포함 여부.
        public readonly int Selector2;   // 두 번째 선택자(실드 필터 등)
        public readonly int Count;
        public readonly bool IncludesSelf;
        public readonly float Speed;
        public readonly float HitThreshold;
        public readonly float SlamDamage;
        public readonly int SlamTileRange;
        public readonly int StackId;     // ⚠ ApplyStatModifier 병합 키의 일부 — 아래
        // 연출 크기 배율. `Speed` 로 대신 쓰지 않는다 — 그 겸직이 이 레이어가
        // 없애려는 것 자체다(0 = 저작 없음 → 어댑터가 1 로 읽는다).
        public readonly float VisualScale;

        public SkillParams(
            float magnitude, float duration, int tileRange, int period, int dataIndex,
            int selector, float speed, float hitThreshold,
            float slamDamage, int slamTileRange, int stackId, float visualScale = 0f,
            int patternIndex = NoDataIndex, int statSelector = 0, int stackSelector = 0,
            int projectileMovement = 0, int projectilePayload = 0,
            byte targetTraversalLayers = 0, Unity.Mathematics.float3 eventPosition = default,
            int hazardDataIndex = NoDataIndex,
            int count = 0, bool includesSelf = false, int selector2 = 0)
        {
            Magnitude = magnitude; Duration = duration; TileRange = tileRange;
            Period = period; DataIndex = dataIndex; Selector = selector;
            Speed = speed; HitThreshold = hitThreshold;
            SlamDamage = slamDamage; SlamTileRange = slamTileRange; StackId = stackId;
            VisualScale = visualScale; PatternIndex = patternIndex;
            StatSelector = statSelector; StackSelector = stackSelector;
            ProjectileMovement = projectileMovement; ProjectilePayload = projectilePayload;
            TargetTraversalLayers = targetTraversalLayers; EventPosition = eventPosition;
            HazardDataIndex = hazardDataIndex;
            Count = count; IncludesSelf = includesSelf; Selector2 = selector2;
        }

        // 영구를 뜻하는 인코딩. 저작이 「안 끝난다」를 표현하는 방법이 이 값이다.
        public const float PermanentDuration = 1e9f;
        public bool IsPermanent => Duration >= PermanentDuration;

        // 「없음」 sentinel. 0 을 폴백으로 쓰면 **0번 에셋**을 가리키게 된다.
        public const int NoDataIndex = -1;
        public bool HasData => DataIndex != NoDataIndex;
    }

    // ── params 뷰 ────────────────────────────────────────────────────
    // 스킬마다 하나. 겸직이 여기서 끝난다 — `p.SleepCount` 는 인원이고
    // `p.Radius` 는 반경이며, 둘이 같은 슬롯 칸에서 왔다는 사실을 concrete 는 모른다.
    //
    // 뷰를 struct 로 두는 이유: 할당이 없고, `in` 으로 넘어가며, 이름이 곧 문서다.

    public readonly struct AreaSleepParams
    {
        private readonly SkillParams _p;
        public AreaSleepParams(in SkillParams p) => _p = p;

        public int SleepCount => (int)_p.Magnitude;   // 재울 인원 M
        public int Radius => _p.TileRange;            // 체비셰프 반경 N
        public float Duration => _p.Duration;         // 수면 초 L
    }

    public readonly struct AreaDamageParams
    {
        private readonly SkillParams _p;
        public AreaDamageParams(in SkillParams p) => _p = p;

        public float Damage => _p.Magnitude;
        public int Radius => _p.TileRange;
        public int VfxDataIndex => _p.DataIndex;      // −1 = 무연출
        public float VisualScale => _p.VisualScale;   // 0 = 저작 없음
    }

    // skill-layer-migration unit 1 — 발사 명세 트리거.
    public readonly struct EmitPatternParams
    {
        private readonly SkillParams _p;
        public EmitPatternParams(in SkillParams p) => _p = p;

        public int PatternIndex => _p.PatternIndex;   // host PatternSlot 버퍼의 자리
        // 조준 후보를 보는 반경 **이자** 탄의 최대 비행 거리다. 두 자가 같은 저작값에서
        // 나오는 것이 계약이다 — 갈리면 「조준은 성립하는데 탄은 도중에 소멸」해
        // 발사 연출만 나가고 아무도 안 맞는다(on-place-shuttle-shotgun 리뷰 M1).
        public int Range => _p.TileRange;
    }

    // skill-layer-migration unit 1 — 범위 도발.
    public readonly struct TauntParams
    {
        private readonly SkillParams _p;
        public TauntParams(in SkillParams p) => _p = p;

        public int Radius => _p.TileRange;      // 체비셰프 반경
        public float Duration => _p.Duration;   // 도발 지속 초
    }

    // skill-layer-migration unit 2d — 광역 스택 도포.
    public readonly struct AreaStackParams
    {
        private readonly SkillParams _p;
        public AreaStackParams(in SkillParams p) => _p = p;

        public int Count => (int)_p.Magnitude;          // 몇 겹
        public int Radius => _p.TileRange;              // 체비셰프 반경
        public float PerStackDuration => _p.Duration;   // 겹당 지속(초)
        // ⚠ **상한은 여기 없다.** 그건 스택 종류의 성질이라 어댑터가 푼다.
        public SkillStackKind Stack => (SkillStackKind)_p.StackSelector;
    }

    // skill-layer-migration unit 2e — 광역 CC(+ 부수 피해).
    public readonly struct AreaCcParams
    {
        private readonly SkillParams _p;
        public AreaCcParams(in SkillParams p) => _p = p;

        public int Radius => _p.TileRange;
        public float Duration => _p.Duration;     // 잡는 시간
        public float Damage => _p.Magnitude;      // 0 = CC 만
        public SkillCcKind Cc => (SkillCcKind)_p.Selector;
    }

    // unit 2e — 광역 지속 피해.
    public readonly struct AreaDotParams
    {
        private readonly SkillParams _p;
        public AreaDotParams(in SkillParams p) => _p = p;

        public int Radius => _p.TileRange;
        public float Duration => _p.Duration;
        // ⚠ **틱당 피해지 DPS 가 아니다.** 총 피해 = 이 값 × (지속 / 틱 간격).
        public float PerTickDamage => _p.Magnitude;
        public float TickInterval => _p.Speed;
    }

    // skill-layer-migration unit 3a — 대상 하나에게 거는 CC.
    public readonly struct TargetCcParams
    {
        private readonly SkillParams _p;
        public TargetCcParams(in SkillParams p) => _p = p;

        public SkillCcKind Cc => (SkillCcKind)_p.Selector;
        public float Duration => _p.Duration;
        // 밀쳐냄일 때만 뜻이 있다 — 넉백 «속도».
        public float Magnitude => _p.Magnitude;
    }

    // skill-layer-migration unit 3b — 자기 스탯 버프(누적형).
    public readonly struct SelfBuffParams
    {
        private readonly SkillParams _p;
        public SelfBuffParams(in SkillParams p) => _p = p;

        public SkillStatKind Stat => (SkillStatKind)_p.StatSelector;
        // ⚠ **저작은 배율이다**(1.08 = +8%). 오라의 퍼센트 축과 다르다 — 그쪽은 사람이
        // 읽는 문안까지 퍼센트인데 이쪽은 누적 곱이라 배율이 그대로 뜻이 된다.
        public float Multiplier => _p.Magnitude;
        // <=0 = 영구. 저작이 「안 끝난다」를 표현하는 방법이다.
        public float Duration => _p.Duration;
        // 누적 상한 배율. 0 = 중첩 없음(덮어쓰기).
        public float MagnitudeCap => _p.TileRange;
        // 병합 키의 일부 — 같은 스택 id 끼리만 누적된다.
        public int StackId => _p.StackId;
    }

    public readonly struct AuraParams
    {
        private readonly SkillParams _p;
        public AuraParams(in SkillParams p) => _p = p;

        // ⚠ 저작은 **퍼센트**다(20 = +20%). 배율로 쓰려면 1 + m/100.
        // 변환은 **concrete 가 한 번** 한다(legacy arm 과 같은 자리). 어댑터는 받은 값을
        // 그대로 싣는다 — 양쪽에서 하면 두 번 곱해진다.
        // (리뷰 M3: 이 주석이 원래 정반대로 적혀 있었다. 그대로 믿고 어댑터를 「고치면」
        //  이속 버프가 조용히 제곱된다.)
        public float PercentDelta => _p.Magnitude;
        public int Radius => _p.TileRange;
        public float Ttl => _p.Duration;
        // ⚠ `Selector`(cc/stack)가 아니라 **전용 축**을 읽는다. 예전엔 겸직이었고,
        // 스탯 오라가 cc 를 안 쓴다는 우연에만 기대고 있었다.
        public SkillStatKind Stat => (SkillStatKind)_p.StatSelector;
    }

    public readonly struct LeapParams
    {
        private readonly SkillParams _p;
        public LeapParams(in SkillParams p) => _p = p;

        // ⚠ **뒤집기 쉬운 자리다.** 저작이 `magnitude` 를 밀집 탐색 반경으로,
        // `tileRange` 를 착지 링 상한으로 쓴다 — 이름과 직관이 어긋난다.
        // 이 뷰가 존재하는 이유가 정확히 그것이다: 읽는 쪽이 이름으로 읽으면
        // 뒤집을 수 없다. (이전 중 실제로 한 번 뒤집었다가 원본 호출을 보고 잡았다.)
        public int DensitySearchRadius => (int)_p.Magnitude;
        public int MaxLandingRing => _p.TileRange;
        public float SlamDamage => _p.SlamDamage;
        public int SlamTileRange => _p.SlamTileRange;
        public float TelegraphSeconds => _p.Duration;     // 궁극기만 쓴다
    }
}
