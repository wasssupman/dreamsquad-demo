namespace Wassup.UI
{
    // lobby-keyring-drag — LobbyKeyringDrag ↔ 캐릭터 본체 접점.
    // 드래그 컴포넌트는 캐릭터 내부 상태를 직접 만지지 않고 이 두 훅만 호출한다.
    public interface ILobbyKeyringTarget
    {
        // 드래그 픽업 순간: 진행 중 리액션 강제 종료(+전역 잠금 해제), 로밍/틱 정지.
        void SuspendForKeyring();

        // 착지 완료: 정지 해제, 새 위치에서 기존 행동 재개.
        void ResumeFromKeyring();
    }
}
