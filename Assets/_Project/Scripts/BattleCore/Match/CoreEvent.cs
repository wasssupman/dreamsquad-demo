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

        // ── unit 2 (맵·이동) ──
        /// <summary>적이 골에 닿았다. **1회 고정.** `Arg` = 공성 가능(1) / 유출(0).</summary>
        GoalReached = 5,
        /// <summary>발견. `hunting` 0→1 전이에서만 1건 — 매 틱 쏘면 초당 60건이다.</summary>
        Detected = 6,
        /// <summary>어그로 획득. `Arg` = 도발(1) / 히트(0).</summary>
        AggroAcquired = 7,
        /// <summary>순간이동 완료. 위치를 소유한 곳(이동)이 낸다.</summary>
        Blinked = 8,

        // ── unit 3 (전투 판정) ──
        /// <summary>공격이 **성사됐다**(RESOLVE). `Arg` = 이 공격이 때린 대상 수.</summary>
        AttackResolved = 9,
        /// <summary>탄이 나갔다. `Arg` = `MovementKind`, `Amount` = 피해 스냅샷.</summary>
        ProjectileSpawned = 10,
        /// <summary>탄이 사라졌다. 모든 소멸은 소멸 사건을 낸다(계약 7).</summary>
        ProjectileDespawned = 11,
        /// <summary>탄이 닿았다. `Arg` = 이 착탄이 때린 대상 수.</summary>
        ProjectileHit = 12,
        /// <summary>
        /// 피해가 적용됐다. `Amount` = 실제로 들어간 양, `Arg` = 흡수량(정수 격자).
        /// `SiteTarget.OriginBody` 자리에 **그 틱 최종 체력 비율**을 싣는다(C7).
        /// </summary>
        DamageApplied = 13,
        /// <summary>회복 펄스. 초당 재생은 여기 오지 않는다(조용히 흐른다).</summary>
        HealApplied = 14,
        /// <summary>실드 합이 양수에서 0 이 된 **그 순간**. 시간 만료는 구조적으로 배제된다.</summary>
        ShieldBroken = 15,
        /// <summary>
        /// **피해로** 죽었다. `A` = 때린 자, `B` = 죽은 자. 분열·처치 보상의 사건이고
        /// `UnitDestroyed`(제거)와 다른 축이다 — 출처 없는 죽음은 이 사건을 안 낸다.
        /// </summary>
        UnitSlain = 16,
        /// <summary>띄우기 연출. **띄운 쪽이 대상을 직접 신호한다** — 심에서 넉업은 짧은 기절이라
        /// 뷰가 군중 제어 종류로는 일반 기절과 구분할 수 없다.</summary>
        Knockup = 17,
        /// <summary>도약 이탈. `Arg` = 궁극기(1) / 일반(0).</summary>
        LeapAscend = 18,
        /// <summary>도약 강하. 일반 도약은 sim 이 이미 착지했고 뷰만 난다.</summary>
        LeapDescend = 19,

        // ── unit 4 (매치 담당자) ──────────────────────────────────────────────
        /// <summary>웨이브가 예약됐다. `Arg` = 웨이브 번호(1부터), `Amount` = 총 마리수.</summary>
        WaveQueued = 20,
        /// <summary>
        /// 그 웨이브의 **첫 적이 실제로 나왔다.** `Arg` = 웨이브 번호, `Amount` = 보스 웨이브(1)/아님(0).
        /// 보스 경보가 읽는 **유일한** 신호다 — 판별은 생성기 한 곳이고 여기서 재판정하지 않는다(X13).
        /// </summary>
        WaveStarted = 21,
        /// <summary>보너스 당김이 제안됐다. **래치**라 한 번만 난다(문턱에서 떨리지 않는다).</summary>
        BonusOffered = 22,
        /// <summary>보너스를 당겼다. `Arg` = 이번에 나올 마리수.</summary>
        BonusPulled = 23,
        /// <summary>
        /// 코스트가 **불연속으로** 움직였다(지불·환급·획득). `Arg` = 증감(정수), `Amount` = 현재값.
        /// ⚠ 초당 재생은 여기 오지 않는다 — 연속값이라 뷰가 읽는 것이 맞고, 매 틱 쏘면 판당 만 건이다.
        /// </summary>
        CostChanged = 24,
        /// <summary>배치가 성사됐다. `Arg` = 정의표 인덱스, `Amount` = 치른 코스트.</summary>
        Placed = 25,
        /// <summary>퇴근했다. `Arg` = 정의표 인덱스, `Amount` = 그 유닛에 걸린 재배치 대기(초).</summary>
        Retired = 26,
        /// <summary>
        /// 배치가 거절됐다. **receipt 와 별개**다 — receipt 는 그 입력을 낸 쪽에게 가고,
        /// 이것은 「누가 거절당했다」를 판 전체에 알린다(트레이·연출).
        /// `Arg` = 거절 사유, `Amount` = 정의표 인덱스.
        /// </summary>
        PlacementRejected = 27,
        /// <summary>배치 페이즈가 끝나 그 유닛이 **활성화**됐다. `Arg` = 정의표 인덱스.</summary>
        DefenderActivated = 28,
        /// <summary>마음이 움직였다. `Arg` = 남은 체력(정수), `Amount` = 스트레스(0~100).</summary>
        HeartChanged = 29,
        /// <summary>마음이 무너졌다. **첫 붕괴가 곧 판의 끝**이다.</summary>
        HeartCollapsed = 30,
        /// <summary>점수가 움직였다. `Arg` = 총점.</summary>
        ScoreChanged = 31,
        /// <summary>이번 판의 기믹이 정해졌다. `Arg` = 기믹 인덱스(-1 = 없음).</summary>
        GimmickAssigned = 32,
        /// <summary>
        /// 배치 창이 열렸다/닫혔다. `Arg` = 열림(1)/닫힘(0), `Amount` = 창의 길이(초).
        /// **길이가 0 이어도 열림 신호는 난다** — 이 신호가 트레이를 만든다(census 계약 3).
        /// </summary>
        PlacementPhaseChanged = 33,
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

        /// <summary>
        /// **그 개체의 정의표 줄**(`Units`/`Enemies` 의 인덱스). 없으면 -1.
        ///
        /// 왜 값으로 싣나: 이 필드를 쓰는 사건 둘(`UnitSlain`·`GoalReached`)은 **주체가 곧
        /// 사라지는** 사건이다. 받는 쪽이 id 로 개체를 되물어 인덱스를 읽으면, 드레인 순서가
        /// 바뀌거나 소멸이 한 틱 당겨지는 날 보상·안정도 피해가 **조용히 0** 이 된다.
        /// 계약 7 이 「이벤트는 값 스냅샷」이라고 적은 것이 정확히 이 함정이고, 제약 13 의
        /// 사망 폭발 반경이 같은 이유로 두 번 좁아졌다.
        ///
        /// ⚠ 종류에 따라 **가리키는 표가 다르다**(적이면 `Enemies`, 방어유닛이면 `Units`) —
        /// `Faction` 과 짝으로 읽는다. 거점은 -1 이다(그 표는 스탯을 다르게 센다).
        /// 트레이스에는 실리지 않는다 — 채널 여섯 칸(tick·channel·a·b·i·f)이 포맷이고,
        /// 이 값은 규칙을 증언하지 않는다(`b` 로 그 개체를 찾으면 나오는 파생값이다).
        /// </summary>
        public readonly int DefIndex;

        private CoreEvent(CoreEventKind kind, int tick, SimEntityId a, SimEntityId b,
                          Site siteFired, Site siteTarget, Faction faction, int arg, float amount,
                          int defIndex = -1)
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
            DefIndex = defIndex;
        }

        public static CoreEvent MatchStartedAt(int tick)
            => new CoreEvent(CoreEventKind.MatchStarted, tick,
                             SimEntityId.Match, SimEntityId.None,
                             Site.Nowhere, Site.Nowhere, Faction.None, 0, 0f);

        public static CoreEvent MatchEndedAt(int tick, MatchEndReason reason, float battleTime)
            => new CoreEvent(CoreEventKind.MatchEnded, tick,
                             SimEntityId.Match, SimEntityId.None,
                             Site.Nowhere, Site.Nowhere, Faction.None, (int)reason, battleTime);

        // unit 5a — `DefIndex` 를 싣는다. 뷰 풀이 「어느 저작 줄인가」를 알아야 스켈레톤·시트를
        // 고르는데, 사건에서 빠져 있으면 풀이 코어에 개체를 **되물어야** 한다. 스폰은 그 되묻기가
        // 성립하는 몇 안 되는 사건이지만(개체가 막 태어났다), 그 예외를 허용하면 다음 사람이
        // 소멸 사건에서도 같은 모양을 쓴다 — 계약 7 이 막는 것이 정확히 그 습관이다.
        public static CoreEvent Spawned(int tick, Unit u)
            => new CoreEvent(CoreEventKind.UnitSpawned, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, (int)u.Kind, u.MaxHealth, u.DefIndex);

        // 발화 시점 몸 반경·자리를 **값으로** 싣는다 — 드레인 시점엔 이 개체가 없다.
        public static CoreEvent Destroyed(int tick, Unit u)
            => new CoreEvent(CoreEventKind.UnitDestroyed, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, (int)u.Kind, u.Health);

        // ── unit 2 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 골 도달. `canSiege` = 그 적이 방어 마음을 **때릴 수 있나**(공성형) — 아니면 유출이다.
        /// 소비는 unit 4(`HeartMeter`)와 unit 3(거점 공성)이 나눠 가진다.
        /// </summary>
        public static CoreEvent GoalReached(int tick, Unit u, bool canSiege)
            => new CoreEvent(CoreEventKind.GoalReached, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, canSiege ? 1 : 0, 0f, u.DefIndex);

        /// <summary>
        /// 발견. ⚠ `B`(대상)는 **트레이스 전용**이다 — 화면이 그 대상을 가리키면 안 된다.
        /// 감지는 직선 최근접 legal 을 고르는데 이동은 공용 사냥판이라 실측 5.0% 에서 둘이
        /// 갈리고, 그 구간에서 화면이 규칙을 **틀리게** 가르친다.
        /// </summary>
        public static CoreEvent Detected(int tick, Unit u, SimEntityId target)
            => new CoreEvent(CoreEventKind.Detected, tick,
                             u.Id, target,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, 0, 0f);

        /// <summary>어그로 획득. `A` = 끌려간 적, `B` = 가디언.</summary>
        public static CoreEvent AggroAcquired(int tick, Unit enemy, SimEntityId guardian, bool taunt)
            => new CoreEvent(CoreEventKind.AggroAcquired, tick,
                             enemy.Id, guardian,
                             new Site(enemy.Position, enemy.HitRadius), Site.Nowhere,
                             enemy.Faction, taunt ? 1 : 0, 0f);

        /// <summary>순간이동 완료. `SiteTarget` = 도착 자리(뷰가 아치를 그릴 근거).</summary>
        public static CoreEvent Blinked(int tick, Unit u, float3 from)
            => new CoreEvent(CoreEventKind.Blinked, tick,
                             u.Id, SimEntityId.None,
                             new Site(from, u.HitRadius), new Site(u.Position, u.HitRadius),
                             u.Faction, 0, 0f);

        // ── unit 3 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 공격 성사. `SiteFired` = 공격자(몸 붙음 — 「몸에서 나오는 것」) ·
        /// `SiteTarget` = 주 대상 자리, `Amount` = 실주기(애니가 실발사보다 빨리 끝나지 않게).
        /// </summary>
        // unit 5c — `DefIndex` 를 싣는다. 공격음은 **그 유닛의 저작**에서 오는데, 사건에
        // 줄 번호가 없으면 소리를 내는 쪽이 코어에 개체를 되물어야 한다. 공격자는 이 시점에
        // 살아 있어 되묻기가 «성립은» 하지만, 그 예외를 허용하면 다음 사람이 소멸 사건에도
        // 같은 모양을 쓴다 — 계약 7 이 막는 것이 그 습관이다(`Spawned` 가 같은 이유로 싣는다).
        // ⚠ 가리키는 표는 `Faction` 과 짝이다(적이면 적 표, 방어유닛이면 유닛 표).
        public static CoreEvent AttackResolved(int tick, Unit attacker, SimEntityId target,
                                               float3 targetPos, float targetBody,
                                               int hitCount, float period)
            => new CoreEvent(CoreEventKind.AttackResolved, tick,
                             attacker.Id, target,
                             new Site(attacker.Position, attacker.HitRadius),
                             new Site(targetPos, targetBody),
                             attacker.Faction, hitCount, period, attacker.DefIndex);

        /// <summary>
        /// 탄 발사. `SiteFired.OriginBody` 가 **제약 13 의 원점 항**을 경계 너머로 나른다 —
        /// 0 이면 「자리에 떨어지는 것」이다.
        /// </summary>
        public static CoreEvent ProjectileSpawned(int tick, Combat.Projectile.Projectile p)
            => new CoreEvent(CoreEventKind.ProjectileSpawned, tick,
                             p.Id, p.Owner,
                             new Site(p.Position, p.OriginBodyRadius),
                             new Site(p.Impact, 0f),
                             p.OwnerFaction, (int)p.Movement, p.Damage);

        public static CoreEvent ProjectileDespawned(int tick, Combat.Projectile.Projectile p)
            => new CoreEvent(CoreEventKind.ProjectileDespawned, tick,
                             p.Id, p.Owner,
                             new Site(p.Position, p.OriginBodyRadius),
                             Site.Nowhere,
                             p.OwnerFaction, (int)p.Payload, p.Elapsed);

        public static CoreEvent ProjectileHit(int tick, Combat.Projectile.Projectile p,
                                              SimEntityId victim, int hitCount)
            => new CoreEvent(CoreEventKind.ProjectileHit, tick,
                             p.Id, victim,
                             new Site(p.Position, p.OriginBodyRadius),
                             Site.Nowhere,
                             p.OwnerFaction, hitCount, p.Damage);

        /// <summary>
        /// 피해 적용. **체력 비율은 그 틱의 최종값**이다(C7) — 뷰가 계산하면 같은 틱의
        /// 숫자들이 서로 다른 비율을 나른다. 그래서 값으로 싣고, 자리는 `SiteTarget` 이다.
        /// </summary>
        public static CoreEvent DamageApplied(int tick, Unit victim, SimEntityId source,
                                              float amount, float absorbed, float hpRatioFinal)
            => new CoreEvent(CoreEventKind.DamageApplied, tick,
                             source, victim.Id,
                             Site.Nowhere,
                             new Site(victim.Position, hpRatioFinal),
                             victim.Faction, (int)(absorbed * 1000f), amount);

        public static CoreEvent HealApplied(int tick, Unit target, float amount)
            => new CoreEvent(CoreEventKind.HealApplied, tick,
                             target.Id, SimEntityId.None,
                             new Site(target.Position, target.HitRadius), Site.Nowhere,
                             target.Faction, 0, amount);

        public static CoreEvent ShieldBroken(int tick, Unit target)
            => new CoreEvent(CoreEventKind.ShieldBroken, tick,
                             target.Id, SimEntityId.None,
                             new Site(target.Position, target.HitRadius), Site.Nowhere,
                             target.Faction, 0, 0f);

        /// <summary>
        /// 피해로 죽었다. **몸 반경·진영은 발화 시점 스냅샷**이다 — 드레인 시점에 다시 읽으면
        /// 0 으로 새어 사망 폭발이 조용히 좁아진다(제약 13 이 두 번 당한 함정).
        /// </summary>
        public static CoreEvent UnitSlain(int tick, SimEntityId killer, Unit victim)
            => new CoreEvent(CoreEventKind.UnitSlain, tick,
                             killer, victim.Id,
                             Site.Nowhere,
                             new Site(victim.Position, victim.HitRadius),
                             victim.Faction, (int)victim.Kind, victim.MaxHealth, victim.DefIndex);

        public static CoreEvent Knockup(int tick, Unit target, float seconds, float height)
            => new CoreEvent(CoreEventKind.Knockup, tick,
                             target.Id, SimEntityId.None,
                             new Site(target.Position, target.HitRadius), Site.Nowhere,
                             target.Faction, (int)(height * 1000f), seconds);

        /// <summary>도약 이탈. `ultimate` = 궁극기(판 밖으로 나간다) / 일반(비행 중에도 맞는다).</summary>
        public static CoreEvent LeapAscend(int tick, Unit u, float3 landing, bool ultimate, float seconds)
            => new CoreEvent(CoreEventKind.LeapAscend, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius),
                             new Site(landing, 0f),   // 착지 자리는 **자리형**이다(0 = 칸)
                             u.Faction, ultimate ? 1 : 0, seconds);

        public static CoreEvent LeapDescend(int tick, Unit u, float3 landing, bool ultimate)
            => new CoreEvent(CoreEventKind.LeapDescend, tick,
                             u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius),
                             new Site(landing, 0f),
                             u.Faction, ultimate ? 1 : 0, 0f);

        // ── unit 4 ────────────────────────────────────────────────────────────
        //
        // 판 자신이 주체인 사건은 `A = SimEntityId.Match`(0) 다. 「누구의 사건도 아닌 사건」의
        // host 가 판이라는 것이 unit 0 항목 9 의 센티널 계약이다.

        private static CoreEvent Match(CoreEventKind kind, int tick, int arg, float amount)
            => new CoreEvent(kind, tick, SimEntityId.Match, SimEntityId.None,
                             Site.Nowhere, Site.Nowhere, Faction.None, arg, amount);

        public static CoreEvent WaveQueued(int tick, int waveNumber, int totalCount)
            => Match(CoreEventKind.WaveQueued, tick, waveNumber, totalCount);

        public static CoreEvent WaveStarted(int tick, int waveNumber, bool boss)
            => Match(CoreEventKind.WaveStarted, tick, waveNumber, boss ? 1f : 0f);

        public static CoreEvent BonusOffered(int tick)
            => Match(CoreEventKind.BonusOffered, tick, 1, 0f);

        public static CoreEvent BonusPulled(int tick, int enemyCount)
            => Match(CoreEventKind.BonusPulled, tick, enemyCount, 0f);

        public static CoreEvent CostChanged(int tick, int delta, float current)
            => Match(CoreEventKind.CostChanged, tick, delta, current);

        public static CoreEvent Placed(int tick, Unit u, int defIndex, float cost)
            => new CoreEvent(CoreEventKind.Placed, tick, u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, defIndex, cost);

        public static CoreEvent Retired(int tick, Unit u, int defIndex, float cooldown)
            => new CoreEvent(CoreEventKind.Retired, tick, u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, defIndex, cooldown);

        public static CoreEvent PlacementRejected(int tick, RejectReason reason, int defIndex)
            => Match(CoreEventKind.PlacementRejected, tick, (int)reason, defIndex);

        public static CoreEvent DefenderActivated(int tick, Unit u, int defIndex)
            => new CoreEvent(CoreEventKind.DefenderActivated, tick, u.Id, SimEntityId.None,
                             new Site(u.Position, u.HitRadius), Site.Nowhere,
                             u.Faction, defIndex, 0f);

        public static CoreEvent HeartChanged(int tick, float health, float stress)
            => Match(CoreEventKind.HeartChanged, tick, (int)health, stress);

        public static CoreEvent HeartCollapsed(int tick)
            => Match(CoreEventKind.HeartCollapsed, tick, 0, 0f);

        public static CoreEvent ScoreChanged(int tick, int score)
            => Match(CoreEventKind.ScoreChanged, tick, score, 0f);

        public static CoreEvent GimmickAssigned(int tick, int gimmickIndex)
            => Match(CoreEventKind.GimmickAssigned, tick, gimmickIndex, 0f);

        public static CoreEvent PlacementPhaseChanged(int tick, bool open, float windowSeconds)
            => Match(CoreEventKind.PlacementPhaseChanged, tick, open ? 1 : 0, windowSeconds);
    }
}
