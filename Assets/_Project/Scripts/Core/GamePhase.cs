namespace Somnia.Battle.Core
{
    // battle-core-rebuild unit 8a — `GameManager.cs` 에서 옮겨 왔다(값·순서 무변).
    // 새 전투 씬에는 `GameManager` 가 없지만 카메라·BGM 의 페이즈 먹이(`CorePhaseFeed`)가 이 값을 민다.
    // 옛 씬도 같은 열거형을 쓴다 — 집만 바뀌었다.
    //
    // ⚠ **이 enum 은 int 로 직렬화된다.** CameraDirectionConfig 의 breathPhases 가 값을
    // 에셋에 정수로 박아 둔다 (Assets/_Project/Data/Camera/CameraDirectionConfig.asset).
    // 값을 빼거나 중간에 끼우면 저장된 정수의 의미가 통째로 밀리므로, **같은 커밋에서
    // 그 에셋도 마이그레이션**해야 한다.
    // (camera-direction unit 11 — 페이즈별 포즈 엔트리는 은퇴했다. 카메라는 페이즈를 상태
    //  2종(배치/전투)으로 접어 매 프레임 해석하므로 이 enum 을 포즈 테이블에 저장하지 않는다.)
    //
    // score-tally-sequence unit 1 — Tally 는 Battle 과 Result 사이의 결과 연출 구간이고
    // Gimmick 은 Placement 앞이지만, 둘 다 **값은 맨 뒤**에 붙인다(위 직렬화 때문).
    // 그래서 값 순서 != 시간 순서다. 전 코드가 == 비교라 순서 의존은 없다.
    //
    // gift-phase-removal unit 1 — Gift 제거(값 2). 뒤 값이 한 칸씩 당겨졌고 위 에셋의
    // phase 4개 + breathPhases 3개를 같은 커밋에서 옮겼다. "직렬화 없음" 이라던 옛 주석은
    // 틀렸다 — 이 경고가 그 대체물이다.
    //
    // battle-content-finish unit 1 — Draft(드래프트 픽 은퇴) · Tally(결과 집계 연출 은퇴) 제거. 발행처 0 이었다.
    // 그 시점의 에셋 `breathPhases` 는 비어 있어 옮길 정수가 없었다(`CoreGamePhaseTests` 가 대조한다).
    public enum GamePhase { None, Placement, Battle, Result, Gimmick }
}
