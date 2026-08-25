namespace Wassup.Battle.Units
{
    // skill-layer-foundation unit 2b — 진영을 **상대적으로** 부르는 순수 함수.
    //
    // 오늘 arm 들은 진영을 리터럴로 안다 — `HasComponent<AttackUnitTag>` 로 「적이냐」를
    // 묻고 `WithAll<DefenderUnitTag>` 로 후보를 모은다. 그래서 스킬이 host 를 바꾸면
    // 엉뚱한 쪽을 때린다. `DcTrigger.cs` 가 그 위험을 직접 적어 뒀다 —
    // *"누가 이 줄을 완화하면 … 보스의 파열 폭발이 자기 진영을 때린다."*
    //
    // 「누구든 이 스킬을 쓸 수 있다」의 전제가 이 파일이다: 모듈이 caster 를 보고
    // 진영을 정해 주면 concrete 는 진영을 가질 이유가 없어진다.
    //
    // 순수하게 두는 이유(CLAUDE.md 제약 10): 이 계산은 `EntityManager` 도 `Time` 도
    // 필요로 하지 않는다. 값 입력 → 값 출력이고, EditMode 로 월드 없이 고정된다.
    // 엔티티에서 진영을 **읽는** 쪽(아키텍처 종속)은 `FactionQuery` 가 따로 맡는다.
    public static class FactionRelation
    {
        // 이 시전자가 때려야 하는 유닛 진영.
        //
        // ⚠ 축이 **유닛 태그**다. `Faction` 에는 거점(Core/Instinct)도 있지만 거점은
        // CC·실드 버퍼가 없어 스킬 대상 술어의 예외다 — 거점을 노리는 스킬이 생기면
        // 그때 별도 축을 연다(지금 열면 아무도 안 지나가는 코드가 된다, 제약 8).
        public static Faction OpponentUnitsOf(Faction caster)
        {
            if ((caster & (Faction)Factions.AnyDefender) != 0) return Faction.EnemyUnit;
            if ((caster & (Faction)Factions.AnyEnemy) != 0) return Faction.DefenderUnit;
            return Faction.None;   // 중립·미지정은 아무도 안 때린다(조용한 오폭 금지)
        }

        // 이 시전자가 도와야 하는 유닛 진영. 오라·실드·버프가 쓴다.
        public static Faction AllyUnitsOf(Faction caster)
        {
            if ((caster & (Faction)Factions.AnyDefender) != 0) return Faction.DefenderUnit;
            if ((caster & (Faction)Factions.AnyEnemy) != 0) return Faction.EnemyUnit;
            return Faction.None;
        }

        // 둘이 서로 적인가. 「내가 때릴 수 있는 상대인가」의 순수 판정.
        public static bool AreOpponents(Faction a, Faction b)
            => a != Faction.None && b != Faction.None && (OpponentUnitsOf(a) & b) != 0;

        // 둘이 같은 편인가. **자기 자신도 참이다** — 자기 버프·자기 실드가 이 술어를 탄다.
        public static bool AreAllies(Faction a, Faction b)
            => a != Faction.None && b != Faction.None && (AllyUnitsOf(a) & b) != 0;
    }
}
