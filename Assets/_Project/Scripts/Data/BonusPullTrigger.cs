namespace Wassup.Data
{
    // bonus-wave-pull unit 5 → unit 9(스트레스 게이트) — 보너스 당기기 등장 규칙. **순수 함수**다.
    //
    // 한 줄짜리 비교들을 굳이 뺀 이유는 제약 10 의 (c) — 이 값이 틀리면 판이 발산하거나
    // (자기증식) 버튼이 떨린다. 회귀를 값 수준에서 고정할 가치가 있다.
    //
    // 규칙은 **두 축의 AND** 이고 성격이 다르다:
    //   · 크레딧(킬) — 누적·소비되는 **자원**. 조건이 안 맞으면 소각되지 않고 **쌓인다**.
    //   · 스트레스  — 그 자원을 **쓸 수 있는 창**. 열렸다 닫혔다 한다.
    public static class BonusPullTrigger
    {
        // 크레딧이 1회분 이상 쌓였나.
        // normalKills = **일반 적** 누적 처치(보너스 적 제외 — 세면 자기 재발화한다).
        // consumedKills = 지금까지 소비한 크레딧의 합(회당 threshold 씩 증가).
        public static bool HasCredit(int normalKills, int consumedKills, int threshold)
        {
            if (threshold <= 0) return false;
            return normalKills - consumedKills >= threshold;
        }

        // 마음이 이 보너스를 감당할 여유가 있나. 스트레스는 «차오르는» 값이라 **이하**가 통과다.
        // ⚠ 마음이 없는 맵은 StressMath 가 0 을 주므로 항상 통과한다(fail-open) — 게이트가
        // 말하려는 대상 자체가 없는 상태라 막을 이유가 없다.
        public static bool StressAllows(float stress, float maxStressToOffer)
            => stress <= maxStressToOffer;

        // 래치 전이 — 「30 이하에서 **등장**」의 정확한 구현.
        //
        //  · 크레딧이 없으면 무조건 꺼진다(소비 직후가 이 경우다 — 별도 리셋 코드가 필요 없다).
        //  · 이미 켜져 있으면 스트레스와 무관하게 유지한다(등장 조건 ≠ 유지 조건).
        //  · 꺼져 있으면 스트레스 창이 열린 순간에만 켜진다.
        //
        // 예시(사용자 시나리오): 30킬 시점에 스트레스 55 → 안 뜬다. 크레딧은 남아 있다.
        // 이후 잡아서 스트레스가 28 로 내려가면 **그때** 뜬다. 그 사이 60킬이 됐다면
        // 크레딧 2회분이 쌓여 있고, 한 회 쓰면 남은 한 회가 이어서 뜬다.
        public static bool NextLatched(
            bool latched, int normalKills, int consumedKills, int threshold,
            float stress, float maxStressToOffer)
        {
            if (!HasCredit(normalKills, consumedKills, threshold)) return false;
            return latched || StressAllows(stress, maxStressToOffer);
        }
    }
}
