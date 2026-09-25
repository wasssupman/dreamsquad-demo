namespace Wassup.Core
{
    // battle-core-rebuild unit 8d — **옛 씬 전용 필드.** 옛 첫 판 안내(`UI/Tutorial/FirstRunTutorialController`)와 옛
    // `GameManager` 만 읽고 쓴다. 새 씬·로비는 이 값을 모른다(사용자 결정 ④ 2026-09-25 — 안내 전량 제거). 옛 씬이
    // unit 9 까지 컴파일돼야 해서 필드를 이 부분 파일로 떼었고, unit 9 가 `retire-set.md` 대로 파일째 지운다.
    // 지운 뒤 기존 세이브의 키는 JsonUtility 가 버린다(`PlayerProfileCompatTests`).
    public partial class PlayerProfile
    {
        public bool firstRunTutorialDone;
    }
}
