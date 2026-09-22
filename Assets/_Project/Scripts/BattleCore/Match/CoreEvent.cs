using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 「무슨 일이 일어났다」. 계약 7 의 이벤트 쪽이다.
    //
    // **커맨드 ≠ 이벤트.** 커맨드는 플레이어 입력이고 동기 적용 + receipt 다.
    // 이벤트는 **값 스냅샷**이고, 받는 쪽이 코어에 상태를 되묻지 않는다(제약 4).
    // 되묻기를 허용하면 사망·소멸처럼 「발화 시점에 이미 없는」 사건이 조용히 0 을 읽는다 —
    // 제약 13 의 사망 폭발 반경이 그 함정으로 두 번 좁아졌다.
    public enum CoreEventKind : byte
    {
        None = 0,
        MatchStarted = 1,
        UnitSpawned = 2,
        UnitDestroyed = 3,
        MatchEnded = 4,
        // append-only. 번호를 재사용하면 구운 골든이 다른 사건으로 읽힌다.

        /// <summary>
        /// 종류의 개수. **종류가 아니다** — 버스의 배열 크기가 이 값에서 나온다.
        /// 새 종류는 이것 **앞**에 넣는다.
        ///
        /// 값이 밀려도 안전한 이유: `CoreEventKind` 는 **어디에도 직렬화되지 않는다.**
        /// 골든에 적히는 것은 `CoreTraceChannel` 이고 그쪽이 append-only 계약을 진다.
        /// </summary>
        _Count,
    }

    // 「어디서 일어났나」 + 「그 자리에 몸이 붙나」. 제약 13 의 «원점 항» 을 값으로 나른다.
    //
    // `OriginBody == 0` 이 **「그 자리는 칸이다」**의 표현이다(자리에 떨어지는 것 — 운석·
    // 장판·착지 슬램). 몸에서 나오는 것은 발화 시점의 `HitRadius` 를 여기 싣는다.
    // 새 필드(`isCell` 같은)를 만들지 않는 이유가 이것이다 — 0 이 이미 그 뜻이다.
    public readonly struct Site
    {
        public readonly float3 Pos;
        public readonly float OriginBody;

        public Site(float3 pos, float originBody)
        {
            Pos = pos;
            OriginBody = originBody;
        }

        /// <summary>자리에 떨어지는 것 — 몸이 없다.</summary>
        public static Site AtCell(float3 pos) => new Site(pos, 0f);

        public static readonly Site Nowhere = new Site(float3.zero, 0f);
    }

    // ⚠ **`readonly struct` 다.** 사건은 「일어난 것」이라 발행 뒤에 바뀌지 않는다.
    // 구독자가 여럿인데 값을 고칠 수 있으면 「몇 번째 구독자가 보느냐」가 규칙이 되고,
    // 그 버그는 구독 순서를 바꾸는 날에야 나타난다. 만드는 길은 아래 팩토리뿐이다.
    public readonly struct CoreEvent
    {
        public readonly CoreEventKind Kind;
        public readonly int Tick;

        /// <summary>주체. 「누구의 사건인가」. 판 자신이면 `SimEntityId.Match`(0).</summary>
        public readonly SimEntityId A;

        /// <summary>대상. 없으면 `SimEntityId.None`.</summary>
        public readonly SimEntityId B;

        public readonly Site SiteFired;
        public readonly Site SiteTarget;

        /// <summary>주체의 진영(발화 시점 스냅샷).</summary>
        public readonly Faction Faction;

        /// <summary>
        /// 종류별 작은 정수. `MatchEnded` = `MatchEndReason`, 스폰/소멸 = `UnitKind`.
        /// `Amount`(실수)와 짝이고, 트레이스 한 줄의 `i`/`f` 에 그대로 실린다 —
        /// 그래서 뷰도 골든도 이벤트 하나를 다시 질의 없이 읽는다.
        /// </summary>
        public readonly int Arg;

        /// <summary>종류별 실수(피해량·지속시간 등). 이 unit 에서는 체력.</summary>
        public readonly float Amount;

        private CoreEvent(CoreEventKind kind, int tick, SimEntityId a, SimEntityId b,
                          Site siteFired, Site siteTarget, Faction faction, int arg, float amount)
        {
            Kind = kind;
            Tick = tick;
            A = a;
            B = b;
            SiteFired = siteFired;
            SiteTarget = siteTarget;
            Faction = faction;
            Arg = arg;
            Amount = amount;
        }

        public static CoreEvent MatchStartedAt(int tick)
            => new CoreEvent(CoreEventKind.MatchStarted, tick,
                             SimEntityId.Match, SimEntityId.None,
                             Site.Nowhere, Site.Nowhere, Faction.None, 0, 0f);

        public static CoreEvent MatchEndedAt(int tick, MatchEndReason reason, float battleTime)
            => new CoreEvent(CoreEventKind.MatchEnded, tick,
                             SimEntityId.Match, SimEntityId.None,
                             Site.Nowhere, Site.Nowhere, Faction.None, (int)reason, battleTime);

        public static CoreEvent Spawned(int tick, Unit u)
            => new CoreEvent(CoreEventKind.UnitSpawned, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, (int)u.Kind, u.MaxHealth);

        // 발화 시점 몸 반경·자리를 **값으로** 싣는다 — 드레인 시점엔 이 개체가 없다.
        public static CoreEvent Destroyed(int tick, Unit u)
            => new CoreEvent(CoreEventKind.UnitDestroyed, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, (int)u.Kind, u.Health);
    }
}
