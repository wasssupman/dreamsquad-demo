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

    public struct CoreEvent
    {
        public CoreEventKind Kind;
        public int Tick;

        /// <summary>주체. 「누구의 사건인가」. 판 자신이면 `SimEntityId.Match`(0).</summary>
        public SimEntityId A;

        /// <summary>대상. 없으면 `SimEntityId.None`.</summary>
        public SimEntityId B;

        public Site SiteFired;
        public Site SiteTarget;

        /// <summary>주체의 진영(발화 시점 스냅샷).</summary>
        public Faction Faction;

        /// <summary>
        /// 종류별 작은 정수. `MatchEnded` = `MatchEndReason`, 스폰/소멸 = `UnitKind`.
        /// `Amount`(실수)와 짝이고, 트레이스 한 줄의 `i`/`f` 에 그대로 실린다 —
        /// 그래서 뷰도 골든도 이벤트 하나를 다시 질의 없이 읽는다.
        /// </summary>
        public int Arg;

        /// <summary>종류별 실수(피해량·지속시간 등). 이 unit 에서는 체력.</summary>
        public float Amount;

        public static CoreEvent MatchStartedAt(int tick) => new CoreEvent
        {
            Kind = CoreEventKind.MatchStarted,
            Tick = tick,
            A = SimEntityId.Match,
            B = SimEntityId.None,
            SiteFired = Site.Nowhere,
            SiteTarget = Site.Nowhere,
            Faction = Faction.None,
            Arg = 0,
            Amount = 0f,
        };

        public static CoreEvent MatchEndedAt(int tick, MatchEndReason reason, float battleTime) => new CoreEvent
        {
            Kind = CoreEventKind.MatchEnded,
            Tick = tick,
            A = SimEntityId.Match,
            B = SimEntityId.None,
            SiteFired = Site.Nowhere,
            SiteTarget = Site.Nowhere,
            Faction = Faction.None,
            Arg = (int)reason,
            Amount = battleTime,
        };

        public static CoreEvent Spawned(int tick, Unit u) => new CoreEvent
        {
            Kind = CoreEventKind.UnitSpawned,
            Tick = tick,
            A = u.Id,
            B = SimEntityId.None,
            SiteFired = new Site(u.Position, u.HitRadius),
            SiteTarget = Site.Nowhere,
            Faction = u.Faction,
            Arg = (int)u.Kind,
            Amount = u.MaxHealth,
        };

        public static CoreEvent Destroyed(int tick, Unit u) => new CoreEvent
        {
            Kind = CoreEventKind.UnitDestroyed,
            Tick = tick,
            A = u.Id,
            B = SimEntityId.None,
            // 발화 시점 몸 반경을 **값으로** 싣는다 — 드레인 시점엔 이 개체가 없다.
            SiteFired = new Site(u.Position, u.HitRadius),
            SiteTarget = Site.Nowhere,
            Faction = u.Faction,
            Arg = (int)u.Kind,
            Amount = u.Health,
        };
    }
}
