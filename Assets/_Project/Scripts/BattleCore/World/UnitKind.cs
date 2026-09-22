namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 개체의 «종류». UML §2 의 닫힌 축이다.
    //
    // 진영(`Faction`)과 다른 축이다. 진영은 «누구 편인가», 종류는 «판 위에서 무엇인가».
    // 거점·길막 장판이 별도 개체 목록이 아니라 여기 종류로 들어오는 것은 옛 전투의
    // 아키타입과 동형이다(그쪽도 같은 엔티티에 태그만 달랐다).
    public enum UnitKind : byte
    {
        None = 0,
        Defender = 1,
        Enemy = 2,
        Patrol = 3,
        Structure = 4,
        BlockingHazard = 5,
    }
}
