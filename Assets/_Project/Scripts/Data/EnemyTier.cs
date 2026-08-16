namespace Wassup.Data
{
    // elite-enemy-tier unit 0 — 적의 **등급** 축. 값이 곧 직렬화 계약이다(int) — append-only.
    //
    // 이 enum 이 소유하는 유일한 런타임 술어는 `tier == Boss` 다. 그것이
    // `BattleBridge.BakeNightmareMechanics` 에서 BossTag·ThreatEntry·등장경보의 부착을 가른다.
    // 그 앞까지는 「nightmareMechanics 가 비어있지 않으면 곧 보스」였고, 그래서 특수 메커니즘을
    // 가진 «보스가 아닌 적» 을 만들 수 없었다(CC·어그로 면역이 딸려온다).
    //
    // ⚠ `stabilityDamage` 의 값 대역과 **서로 검증하지 않는다.** 두 축은 독립이다 —
    // 이쪽은 행동을 게이팅하고 그쪽은 밸런스를 표현한다. 실측 반례: Enemy_Tanker 는 Normal 인데
    // 값은 엘리트 대역이다. OnValidate 정합성 검사를 붙이면 정상 콘텐츠에서 발화한다.
    // (`killScore` 도 같은 관계였으나 three-minute-kill-race unit 1 에서 축 자체가 은퇴했다 —
    // 등급은 이제 **점수와 아무 관계가 없다**. 1킬 = 1점, 예외 없음.)
    //
    // ⚠ `EnemyClass`(Tanker/Runner/Bruiser/Shooter)와 다른 축이다 — 그쪽은 «역할», 이쪽은 «등급».
    // 엘리트 슬라임은 tier=Elite + enemyClass=Bruiser 로 둘 다 갖는다.
    //
    // Elite 값 자체에는 아직 코드 소비자가 없다(술어는 Boss 하나). 저작 축에서 3등급이 보여야
    // 한다는 요구(2026-08-12 사용자 결정) 때문에 bool 이 아니라 enum 으로 둔다.
    public enum EnemyTier
    {
        Normal = 0,
        Elite = 1,
        Boss = 2,
    }
}
