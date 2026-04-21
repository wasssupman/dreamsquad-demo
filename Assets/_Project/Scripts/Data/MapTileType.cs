namespace Wassup.Data
{
    // Phase 10: mutually exclusive 4종. 한 타일 = 한 역할.
    public enum MapTileType : byte
    {
        Walk  = 0,   // 적 이동 가능 (flow field walkable)
        Place = 1,   // defender 배치 가능
        Env   = 2,   // 환경 (Phase 10 = 시각 구분만, Phase 11 에서 효과)
        Deco  = 3,   // 배경 오브젝트 (시각 장식)
    }
}
