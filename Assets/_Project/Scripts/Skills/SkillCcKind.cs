namespace Wassup.Skills
{
    // skill-layer-foundation unit 5 — 도메인이 부르는 CC 이름.
    //
    // Runtime 의 `Wassup.Battle.Effects.CcKind` 와 **값이 같아야 한다** — 어댑터가
    // 캐스트로 번역한다. 별도 enum 을 두는 이유는 그쪽이 Entities 를 참조하는
    // 어셈블리에 있고 이 어셈블리가 그것을 참조하지 않기 때문이다(계약 1).
    //
    // ⚠ **값을 눈으로 맞추지 마라.** 이 파일의 첫 판이 Stun=1·Sleep=3 으로 추측했고
    // 실제는 Stun=3·Sleep=4 였다. 그대로 갔으면 재우려던 것이 조용히 **둔화**가 됐다.
    // `SkillCcKindPinTests` 가 두 enum 의 값 일치를 고정한다.
    public enum SkillCcKind : byte
    {
        Slow = 0,
        Impulse = 1,
        DoT = 2,
        Stun = 3,
        Sleep = 4,
    }
}
