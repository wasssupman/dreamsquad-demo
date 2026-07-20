# 1 — LeaderboardList 추출

## 목적

`ResultScreen` 에 private 로 묻힌 랭킹 행 렌더링(순위 뱃지·본인 강조·WAITING 슬롯)을 공용 `LeaderboardList` 로 추출해 결과창과 히스토리 상세 팝업이 **동일 룩**을 공유한다. 순수 `BuildRows` 계약은 불변.

## 변경 대상

- `Assets/_Project/Scripts/UI/LeaderboardList.cs` (신규)
- `Assets/_Project/Scripts/UI/ResultScreen.cs` (위임으로 축소)
- `Assets/_Project/Tests/EditMode/ResultLeaderboardModelTests.cs` (`ResultScreen.BuildRows` → `LeaderboardList.BuildRows`)

## 구현

- `LeaderboardList` = plain 클래스(비 MonoBehaviour). 각 presenter 가 자기 캔버스/레이아웃을 소유하고 **행 리스트만** 위임한다.
  - `public readonly struct Row`(rank/name/score/isPlayer/isWaiting)
  - `public static List<Row> BuildRows(entries, maxEntryCount, ownUserId)` — score 내림차순, 서버 `rank>0` 우선, maxEntryCount 까지 WAITING 슬롯, 본인 매칭. (ResultScreen 에서 그대로 이관)
  - ctor 에서 행/뱃지 스프라이트 1회 베이킹, `public void Render(RectTransform content, IReadOnlyList<Row> rows)` — detach-then-destroy 후 재생성.
- `ResultScreen`: 행 팔레트/스프라이트/`Row`/`BuildRows`/`RenderRows`/`CreateRow` 제거, `LeaderboardList _leaderboard` 보유. `ShowResult`(봇) / `UpdateLeaderboard`(실데이터) 둘 다 `_leaderboard.Render(_listContent, rows)`. 헤더/푸터 팔레트(gold/navy/defeat/BadgeTextDark)와 `CreateLabel`/`StretchFull` 은 잔존.
- 행 관련 시각 상수는 `LeaderboardList` 로 이동(자기 완결). `CreateLabel`/`StretchFull` 은 각자 private 사본(자명 헬퍼, 공유 계약 아님).

## 완료 기준

- [x] compile: `dotnet build Wassup.Tests.EditMode.csproj` 오류 0.
- [ ] EditMode: `ResultLeaderboardModelTests`(6종, `LeaderboardList.BuildRows` 대상) 그린 — Unity Test Runner.
- [ ] Play: 결과창(승/패) 리더보드가 추출 전과 동일하게 보이는지 시각 무회귀 확인.
