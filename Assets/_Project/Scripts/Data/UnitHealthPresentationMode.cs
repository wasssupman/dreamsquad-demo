namespace Wassup.Data
{
    // unit-overhead-ui — 레거시와 신규 체력/부착 UI는 상호 배타적이다.
    public enum UnitHealthPresentationMode : byte
    {
        Legacy = 0,
        UnifiedOverhead = 1,
    }
}
