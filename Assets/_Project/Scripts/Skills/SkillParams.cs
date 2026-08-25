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
        public readonly int Selector;    // stat/cc/stack kind 등 저작 enum
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
            float slamDamage, int slamTileRange, int stackId, float visualScale = 0f)
        {
            Magnitude = magnitude; Duration = duration; TileRange = tileRange;
            Period = period; DataIndex = dataIndex; Selector = selector;
            Speed = speed; HitThreshold = hitThreshold;
            SlamDamage = slamDamage; SlamTileRange = slamTileRange; StackId = stackId;
            VisualScale = visualScale;
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
        public int StatSelector => _p.Selector;
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
