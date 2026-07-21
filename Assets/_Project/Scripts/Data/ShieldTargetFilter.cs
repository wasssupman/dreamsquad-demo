namespace Wassup.Data
{
    // shield-guardian-defender unit 1 — 실드 캐스트 대상 필터 (SO 데이터).
    // Self = 자신만(C/범위 무시) · All = 가까운 순 C개 · MinHealth = 유효HP 비율
    // (HP+실드합)/maxHP 오름차순 C개 (spec 계약 6 — 실드 무시 정렬은 만충 대상
    // no-op 재부여 함정). append-only(직렬화 안전).
    public enum ShieldTargetFilter : byte
    {
        Self = 0,
        All = 1,
        MinHealth = 2,
    }
}
