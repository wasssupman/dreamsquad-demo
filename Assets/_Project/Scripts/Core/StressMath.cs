namespace Wassup.Core
{
    /// <summary>
    /// heart-stress-axis unit 0 — 마음 체력을 «차오르는 스트레스»로 읽는 **유일한** 산식.
    ///
    /// 정본은 마음의 <c>Health</c> 하나다. 스트레스는 별도 리소스가 아니라 그 값의 **표시 반전**
    /// 이고, 이 함수가 그 반전을 소유한다. 소비처 셋이 같은 답을 봐야 하므로 한 곳에 둔다:
    ///   ① 종료 판정 (스트레스 100 == 마음 HP 0)
    ///   ② 마음 머리 위 바 (unit 1)
    ///   ③ 화면 붉은 림 (unit 3)
    ///
    /// **「100」은 표시 정규화이지 HP 최대치가 아니다.** <c>Health.max</c> 를 진짜 100 으로 두면
    /// 공격력 20 짜리 적이 5대에 판을 끝낸다. 정본 HP 는 덱의 <c>goalStabilityMax</c> 스케일을 쓴다.
    ///
    /// 아키텍처 무참조 순수 값 — UnityEngine/Entities 를 참조하지 않는다(<c>MatchTally</c> 선례).
    /// </summary>
    public static class StressMath
    {
        /// <summary>스트레스 만점. 「100 이 되면 판이 끝난다」의 100 이 이 상수다.</summary>
        public const float Max = 100f;

        /// <summary>
        /// 마음 체력 → 스트레스(0~<see cref="Max"/>). 만피 = 0, 체력 0 = 100.
        ///
        /// <paramref name="max"/> 가 0 이하면 **0 을 준다**(100 이 아니다). 그 상태는
        /// 「마음이 미저작·미스폰」이라 판을 끝낼 대상 자체가 없다는 뜻인데, 이걸 100 으로
        /// 읽으면 판이 시작하자마자 종료된다.
        /// </summary>
        public static float FromHealth(float value, float max)
        {
            if (max <= 0f) return 0f;
            float ratio = value / max;
            if (ratio <= 0f) return Max;
            if (ratio >= 1f) return 0f;
            return (1f - ratio) * Max;
        }

        /// <summary>스트레스가 만점인가 = 마음이 무너졌는가. 종료 판정의 어휘를 한 곳에 둔다.
        /// 판정 자체는 여전히 <c>Health.value &lt;= 0</c> 이 정본이고 이 함수는 그것을
        /// 스트레스 어휘로 읽어줄 뿐이다(부동소수 비교를 각자 하지 않게).</summary>
        public static bool IsFull(float stress) => stress >= Max;
    }
}
