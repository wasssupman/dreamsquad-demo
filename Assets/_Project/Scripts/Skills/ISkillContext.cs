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
    }

    // 거리 자. 대부분 체비셰프인데 전방 발사만 유클리드다 — 실측이라 축으로 남긴다.
    public enum RangeMetric : byte { Chebyshev = 0, Euclidean = 1 }

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
    }

    // `Stat(id, kind)` 의 축.
    public enum UnitStat : byte
    {
        AttackRange = 0,
        AttackTargetCount,
        TargetTraversalLayers,
        AggroCapacity,
        AttackCooldownRemaining,
    }

    public interface ISkillContext
    {
        // ── 질의: 자리 ──────────────────────────────────────────────
        float3 Position(SkillEntityId id);
        int2 CellOf(SkillEntityId id);
        int2 CellOfPosition(float3 world);
        float3 CellCenter(int2 cell);
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

        // ── 의도 ────────────────────────────────────────────────────
        void Emit(in SimIntent intent);
        void Emit(in MetaIntent intent);
    }
}
