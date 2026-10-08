namespace Somnia.Battle.Data.Authoring
{
    // battle-core-rebuild unit 8c — 이 타입의 **집**만 옮겼다(옛 `Battle/Effects/DotEffect.cs` 에서 떼어냄). 네임스페이스·값·번호 무변 —
    // 새 층 저작 SO 가 이것을 부르는데 옛 폴더는 unit 9 가 통째로 지운다. 이름 정리는 unit 9.

    // 지속 피해의 **원소**. 오라가 읽는 축이고, 슬롯을 가르는 축이 아니다.
    // None = 원소 없음 = 오라 없음(버스터즈 배치 도트 등).
    // 새 항목은 반드시 **끝에** 추가할 것.
    public enum DotElement : byte
    {
        None = 0,
        Bleed = 1,
        Fire = 2,
        Ice = 3,
        Poison = 4,
    }
}
