namespace Wassup.Data
{
    // unit-overhead-ui 확장(unit 6) — 오버헤드 스택 아이콘 종류(presentation 계층 심볼).
    // Presentation 은 Battle.StackKind 를 참조하지 않으므로(overhead-ui 계약), gather(BattleBridge,
    // unit 8)가 Battle.StackKind / HeatAccrual 을 이 enum 으로 번역해 뷰에 넘긴다. append-only.
    public enum OverheadStackKind : byte
    {
        Fatigue = 0,
        Heat = 1,
    }

    // 뷰가 받는 plain DTO — 한 유닛의 활성 스택 1건(종류 + 현재 수). ECS/Battle 타입 없음.
    [System.Serializable]
    public struct OverheadStackEntry
    {
        public OverheadStackKind kind;
        public int count;
    }
}
