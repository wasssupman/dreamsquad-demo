namespace Somnia.Battle.Data.Authoring
{
    // battle-core-rebuild unit 8c — 이 타입의 **집**만 옮겼다(옛 `Battle/Effects/CcEffect.cs` 에서 떼어냄). 네임스페이스·값·번호 무변 —
    // 새 층 저작 SO 가 이것을 부르는데 옛 폴더는 unit 9 가 통째로 지운다. 이름 정리는 unit 9.

    public enum CcKind : byte
    {
        Slow = 0,
        Impulse = 1,
        DoT = 2,
        Stun = 3,
        // combat-action-lock — Sleep: 공격+이동 정지(Stun 과 함께 action-lock). 최대 N초
        // (무한 = remainingTime +∞), 피격 시 해제(wake-on-hit). append-only.
        Sleep = 4,
    }
}
