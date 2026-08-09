using System;

namespace Wassup.Battle.Units
{
    // battle-structures unit 0 — Faction 은 «진영 × 종류» 교차 비트다.
    //
    // 진영(방어·적·중립) × 종류(유닛·거점)를 **한 축**에 넣는다. 별도 마스크 2개로
    // 쪼개지 않는 이유: 타겟 술어가 전부 `(faction & mask) != 0` 한 줄이고 그런 자리가
    // 20곳 이상인데, 축을 쪼개면 전부 «진영 체크 + 종류 체크» 두 줄이 된다. 교차 비트면
    // 술어 모양이 안 바뀌고 «방어 거점 전부» 같은 저작 의도가 마스크 리터럴이 된다.
    //
    // 타입 이름(Faction/FactionTag)은 유지한다 — 참조가 40곳 이상이고 리네임은 이 spec 의
    // 검증 질문과 무관하다. 진영이 3개를 넘으면 이 판단을 다시 본다(비트 폭발).
    [Flags]
    public enum Faction : int
    {
        None = 0,

        DefenderUnit = 1 << 0,   // 구 Defender — 배치된 방어 유닛·순찰 아군
        EnemyUnit = 1 << 1,   // 구 Enemy
        BlockingHazard = 1 << 2,   // 방벽. 거점이 아니다 — 종류 축 밖에 남는다

        // 거점(Structure) — 마음(Core, 진영당 1) · 본능(Instinct, 맵당 N)
        DefenderCore = 1 << 3,   // 구 Goal 과 **같은 비트** — 방어 마음 = 현행 골 타워
        DefenderInstinct = 1 << 4,
        EnemyCore = 1 << 5,   // 공성 맵의 적 마음 = 스폰지점
        EnemyInstinct = 1 << 6,

        // 중립 — 비트만 예약한다. 생산자·소비자 0이고 술어가 특별 취급하지 않는다.
        NeutralUnit = 1 << 7,
        NeutralCore = 1 << 8,
        NeutralInstinct = 1 << 9,
    }

    // 파생 그룹 — 저작·술어가 읽는 이름. int 로 두어 사용처에서 캐스트가 늘지 않게 한다.
    // 지금 소비처가 있는 것은 AnyStructure(최후순위 판정)와 AnyEnemy(공격형 판별)뿐이고,
    // AnyUnit 은 도발 범위 게이트(unit 2)가 쓴다. 나머지는 분류 체계로서 선언만 둔다 —
    // 술어를 위한 추상 레이어가 아니라 비트 조합의 이름이다.
    public static class Factions
    {
        public const int AnyUnit = (int)(Faction.DefenderUnit | Faction.EnemyUnit | Faction.NeutralUnit);
        public const int AnyCore = (int)(Faction.DefenderCore | Faction.EnemyCore | Faction.NeutralCore);
        public const int AnyInstinct = (int)(Faction.DefenderInstinct | Faction.EnemyInstinct | Faction.NeutralInstinct);
        public const int AnyStructure = AnyCore | AnyInstinct;
        public const int AnyDefender = (int)(Faction.DefenderUnit | Faction.DefenderCore | Faction.DefenderInstinct);
        public const int AnyEnemy = (int)(Faction.EnemyUnit | Faction.EnemyCore | Faction.EnemyInstinct);
    }
}
