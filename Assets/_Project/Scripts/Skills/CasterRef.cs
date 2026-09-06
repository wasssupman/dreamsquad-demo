using Wassup.Battle.Units;

namespace Wassup.Skills
{
    // skill-layer-foundation unit 2a — 「호출자 = 소유자」를 표현하는 타입.
    //
    // `Execute` 를 부른 주체가 곧 그 스킬의 소유자다. 그래서 concrete 는 진영도
    // host 종류도 갖지 않고 **인자로 받는다** — 보스가 쓰던 스킬을 잡몹이 부르면
    // 코드 0줄로 동작하는 것이 이 타입 하나에 달려 있다.
    //
    // ⚠ `unit` 이 무효일 수 있다. 액티브 스킬은 시전 주체 엔티티가 **아예 없고**
    // (`ThreatTable.cs` — "bridge-cast skills (player Meteor, owner == Null)")
    // 대상 타일이 앵커다. 실측 결과 액티브 6 arm 중 caster 위치를 읽는 것이
    // **0개**라 이 형태가 성립한다(unit 0 산출물).
    //
    // 그래서 진영은 caster 로부터 **파생시키지 않고** 별도 필드로 싣는다. 액티브의
    // 진영은 오늘도 시전자가 아니라 스폰 경로에서 구조적으로 갈린다
    // (`SkillData.cs` 의 "아군/적 구분은 스폰 경로에서 구조적으로 갈린다" 주석).
    public readonly struct CasterRef
    {
        public readonly SkillEntityId Unit;
        public readonly Faction Faction;

        // distance-based-range unit 23a — **시전자의 몸 반경(타일).**
        //
        // ★ **값으로 나른다**(`Faction` 과 같은 이유). 자기 죽음 seam 은 정의상 파괴 뒤에 돌아
        // 드레인 시점에 엔티티에서 못 읽는다 — `SkillFiredEvent.CasterFaction` 이 이미 그 문제를
        // 푼 선례이고, 이건 그 패턴의 n+1 번째 필드다.
        // 부수 이득: 어댑터가 후보 루프 **안**에서 `HitRadius` 를 조회할 필요가 없다(값이 이미 손에
        // 있고 루프 불변) — 조회로 되돌리면 후보마다 lookup 이 되고 fallback 0 위험이 되살아난다.
        //
        // 0 = 판 위에 몸이 없다(플레이어 시전). 「자리에 떨어지는 것」의 0 과는 **다른 축**이다 —
        // 그쪽은 `SkillParams.EventBodyRadius` 가 말한다.
        public readonly float BodyRadius;

        public CasterRef(SkillEntityId unit, Faction faction, float bodyRadius = 0f)
        {
            Unit = unit;
            Faction = faction;
            BodyRadius = bodyRadius;
        }

        // 유닛이 시전한다 — 보스·적·방어유닛 전부 이 경로다.
        public static CasterRef OfUnit(SkillEntityId unit, Faction faction, float bodyRadius = 0f)
            => new CasterRef(unit, faction, bodyRadius);

        // 플레이어가 시전한다 — 액티브 스킬. 판 위에 시전자가 없다(몸도 없다).
        public static CasterRef Player(Faction faction)
            => new CasterRef(SkillEntityId.None, faction, 0f);

        // 이 스킬이 판 위의 시전자를 갖는가. `Position`/`Facing` 질의는 이것이 참일 때만
        // 부를 수 있다 — 거짓인데 부르면 어댑터가 loud 하게 거절한다.
        public bool HasUnit => Unit.IsValid;
    }
}
