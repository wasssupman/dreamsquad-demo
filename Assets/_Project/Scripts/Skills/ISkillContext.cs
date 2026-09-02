using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.Skills
{
    // skill-layer-foundation unit 3 — 도메인과 아키텍처 사이의 **프로토콜**.
    //
    // 이 인터페이스가 계약 1 의 실체다. concrete 는 `Entity` 도 `EntityManager` 도
    // `SystemAPI` 도 모르고, 필요한 것을 **여기에 물어보고** 하고 싶은 것을 **여기에
    // 방출한다**. 어댑터가 그 반대편에서 ECS 든 sim lib 이든 테스트 페이크든 된다.
    //
    // ⚠ 동사는 **도출된 것**이다(unit 0). arm 전수를 읽어 「이 arm 이 실행되려면 무엇을
    // 알아야 하나」를 뽑았고, 감쌀 수 없는 읽기는 0건이었다. 임의로 늘리지 마라 —
    // 늘려야 하면 `0_protocol_surface_derivation.md` 의 표를 먼저 고친다.

    // 후보를 거르는 축. 오늘 후보 수집 구현이 **6벌**이고 조합이 전부 달랐다.
    // 이름이 같은데 후보가 다른 상태를 프로토콜 아래로 숨기지 않으려고 flag 로 명세한다.
    [System.Flags]
    public enum CandidateFilter
    {
        None = 0,
        ExcludeSelf = 1 << 0,
        ExcludeDead = 1 << 1,
        ExcludePendingDeployment = 1 << 2,
        ExcludeInUltimateLeap = 1 << 3,
        RequireDamageable = 1 << 4,   // IncomingDamage 버퍼 보유 — 없으면 총구만 낭비된다
        MatchTraversalLayers = 1 << 5, // caster 의 공격 층 ∩ 후보의 통행 층
        // 체력을 가진 후보만. `RequireDamageable`(피해 버퍼)과 다른 축이다 —
        // 이쪽은 **「얼마나 다쳤나」를 물을 수 있나**를 묻는다. 없으면 그 비율이
        // 0 으로 접혀 「가장 다친 순」 정렬의 **맨 앞**을 차지한다(재리뷰 M-6).
        RequireHealth = 1 << 6,
    }

    // 거리 자 — 값이 뜻하는 것이 다르다(둘 다 원, 둘 다 몸 걸침):
    //   AreaCircle  광역 도형. 반경 = tileRange + 칸 반폭(0.5) + 대상 몸. 「반경 1 = 여덟 이웃 전부」가
    //               성립하는 자(대각 1.414 ≤ 1.5). 자기시전·칸 조준 광역 전부.
    //   Euclidean   사거리/비행 거리. 반경 = tileRange + 대상 몸. 발사명세(EmitPattern)처럼 「탄이 실제로
    //               가는 거리」라 칸 반폭을 더하면 사거리 밖 후보를 골라 탄이 도중 소멸한다.
    // ⚠ **0 = AreaCircle** 이다 — `default(RangeMetric)`·인자 누락이 은퇴한 사각 자로 조용히 가지 않게.
    // Chebyshev(사각 SDF) 는 dreamcatcher-attach-range-preview 0a 에서 은퇴 — 술어 본체
    // `SkillMath.BodyOverlapsSquare` 는 보존(사용자 결정 2026-09-02 「기능은 남기고 비활성화」).
    // 되살릴 땐 아래 Obsolete 속성부터 떼고 `EcsSkillContext.Collect` 에 분기를 다시 단다.
    public enum RangeMetric : byte
    {
        AreaCircle = 0,
        Euclidean = 1,
        [System.Obsolete("사각 광역은 은퇴했다(attach-range-preview 0a). 광역은 AreaCircle, 사거리는 Euclidean.", true)]
        Chebyshev = 2,
    }

    // `Has(id, pred)` 의 술어. 개별 동사로 쪼개면 표면이 10개 늘어난다.
    public enum UnitPredicate : byte
    {
        Alive = 0,
        PendingDeployment,
        InUltimateLeap,
        HasShieldBuffer,
        HasAggroCapacity,   // 가디언 표식 — 도발 캐스터 자격
        IsPathFollowing,
        CanReceiveDamage,
        // 자리를 아는가. `Position()` 은 부재를 0 으로 접기 때문에 **조준하는 스킬은
        // 이걸 먼저 물어야 한다** — 위치를 모르면 조준도 못 하므로 발사 자체를 취소한다.
        // 조용히 (0,0) 방향 탄이 나가는 것이 이 축의 원래 증상이었다.
        HasPosition,
    }

    // 발사 명세(패턴)가 조준을 필요로 하는가. skill-layer-migration unit 1.
    //
    // 「방향이 비어 있으면 아직 조준되지 않은 것」이라는 판정은 **어댑터 쪽 지식**이다
    // (템플릿의 이동 바인딩과 direction 을 봐야 안다). 도메인은 결론만 받는다.
    public enum PatternAimNeed : byte
    {
        // 그런 패턴이 없다 — 슬롯 index 가 음수이거나 버퍼가 없거나 범위 밖.
        // **발사도 카운터 전진도 없다.**
        Missing = 0,
        // 저작/호출처가 이미 조준을 실어 보냈다. 템플릿을 건드리지 않고 그대로 쏜다.
        Preaimed,
        // 방향 바인딩인데 방향이 비어 있다 — **이 스킬이 방향을 정해야 한다.**
        NeedsAim,
    }

    // `Stat(id, kind)` 의 축.
    public enum UnitStat : byte
    {
        AttackRange = 0,
        AttackTargetCount,
        TargetTraversalLayers,
        AggroCapacity,
        AttackCooldownRemaining,
        // unit 2e — 이 유닛이 적을 «얼마나 높이 · 얼마나 오래» 띄우나. 평타든 배치든 같은
        // 값이라 스킬 저작이 아니라 **유닛의 성질**이다(그래서 params 가 아니라 질의다).
        KnockupVisualHeight,
        KnockupHopSeconds,
        // unit 5b — **얼마나 다쳤나**(HP+실드합 ÷ 최대HP). 원시 세 값(체력·최대체력·실드합)
        // 대신 파생값 하나를 여는 이유: 스킬이 묻는 것이 그 질문이고, 실드 합산 규칙은
        // 어댑터(`ShieldMath`)가 소유하기 때문이다.
        EffectiveHpRatio,
        // unit 18 (distance-based-range) — 판정 몸 반경(타일). 자장가의 「내가 때릴 대상
        // 제외」처럼 스킬이 사거리 술어를 미러해야 할 때 쓴다 — 술어와 같은 몸을 본다.
        BodyRadius,

    }

    public interface ISkillContext
    {
        // ── 질의: 자리 ──────────────────────────────────────────────
        float3 Position(SkillEntityId id);
        int2 CellOf(SkillEntityId id);
        int2 CellOfPosition(float3 world);
        float3 CellCenter(int2 cell);

        // skill-layer-migration unit 3f — **타일 한 칸의 월드 크기.**
        // 저작은 「반경 N칸」으로 하는데 궤도 계산은 월드 반경을 요구한다. 그 환산을
        // 어댑터에 넘기면 「각속도 = 선속도 ÷ 반경」이라는 **규칙**까지 어댑터로 새어 나간다 —
        // 그건 저작의 뜻(반경을 키워도 도는 체감이 유지된다)이라 스킬이 소유해야 한다.
        float TileSize { get; }
        // 부재 = 무조준. 배치 방향을 선언하지 않은 유닛이 대부분이다.
        bool TryFacing(SkillEntityId id, out float2 dirXZ);

        // ── 질의: 정체 ──────────────────────────────────────────────
        Faction FactionOf(SkillEntityId id);
        float Health(SkillEntityId id);
        float MaxHealth(SkillEntityId id);
        float Stat(SkillEntityId id, UnitStat stat);
        bool Has(SkillEntityId id, UnitPredicate pred);
        byte TraversalLayers(SkillEntityId id);
        // 이 대상이 이 출처로부터 이미 받은 실드량. 같은 출처의 중복 부여를 막는다.
        float ShieldValueFrom(SkillEntityId target, SkillEntityId source);

        // ── 질의: 후보 ──────────────────────────────────────────────
        // 진영은 caster 에서 파생된다 — concrete 가 「적」을 이름으로 부르지 않는 이유다.
        int Opponents(CasterRef caster, float3 center, int tileRange,
                      CandidateFilter filter, RangeMetric metric, SkillEntityId[] into);
        int Allies(CasterRef caster, float3 center, int tileRange,
                   CandidateFilter filter, RangeMetric metric, SkillEntityId[] into);

        // ── 질의: 격자 위의 판단 ────────────────────────────────────
        // 도약 계열이 쓴다. 순수 코어(`DefenderDensity`·`BlinkMath`)가 이미 있어서
        // 어댑터는 배열을 넘겨주기만 한다 — 그래서 이 둘이 포트를 넘을 수 있다.
        bool TryDensestOpponentCluster(CasterRef caster, int densityRadius, out int2 cell, out int count);
        bool TryLandingCellNear(int2 desired, int maxRing, out int2 cell);

        // ── 질의: 발사 명세 ─────────────────────────────────────────
        // 도메인은 탄 템플릿도 이동 바인딩도 모른다. 아는 것은 「내가 조준해야 하나」뿐.
        PatternAimNeed AimNeedOfPattern(SkillEntityId host, int patternIndex);

        // ── 의도 ────────────────────────────────────────────────────
        void Emit(in SimIntent intent);
        void Emit(in MetaIntent intent);
    }
}
