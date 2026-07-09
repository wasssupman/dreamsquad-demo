# 2 — 행별 플레이트 + 순위 배지 렌더

## 목적

리더보드를 단일 `mspace` TMP 블록 → 행별 라운드 플레이트 + 순위 배지 GameObject 로 교체한다. 봇 폴백과 실서버 랭킹이 같은 렌더 경로를 쓴다.

선행: unit 0 (`UiRoundedSprite`), unit 1 (리스트 영역 컨테이너).

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs` — 행 모델 + 렌더 + `BuildRows`
- `Assets/_Project/Tests/EditMode/ResultLeaderboardModelTests.cs` (신설)

## 구현

- **순수 행 모델**: `readonly struct Row { int rank; string name; int score; bool isPlayer; bool isWaiting; }`.
  - `static List<Row> BuildRows(IReadOnlyList<TournamentApi.ResultEntry> entries, int maxEntryCount, string ownUserId)`:
    - entries 를 score 내림차순 정렬(복사본), `totalSlots = max(maxEntryCount, entries.Count)`.
    - 배정 슬롯: `rank = e.rank>0 ? e.rank : i+1`, name = `DisplayName(e.userName)`, `isPlayer = ownUserId 비어있지 않고 e.userId==ownUserId`.
    - 미배정 슬롯: `isWaiting=true`, name=`"WAITING..."`, score 표시 `-`, rank=`i+1`.
  - 봇 폴백은 별도 오버로드 or 기존 `BotScoreGenerator` 결과를 Row 로 매핑(YOU 포함). 실 랭킹과 동일 렌더러 사용.
- **렌더**: `RenderRows(List<Row> rows)`:
  - 리스트 컨테이너 자식 전부 제거 후 재생성(행 수가 봇 6 ↔ 실 10 으로 바뀌므로 풀링보다 clear+rebuild 단순. 결과 팝업은 매치당 1회 렌더).
  - 각 행 = `Image`(라운드 플레이트) + 순위 배지(원, 좌측) + 이름 TMP(좌) + 점수 TMP(우, 우측정렬). `LayoutElement.preferredHeight ≈ 52`.
  - 배지 색: rank1=골드`#FFD24A` / rank2=실버`#D7DCE0` / rank3=브론즈`#C88A4B` / 그 외=네이비 칩+골드 숫자. `UiRoundedSprite.MakeCircle`.
  - 플레이트 색: 기본 `(1,1,1,0.05)`; **본인 행** = 골드 틴트 `(1,0.83,0.35,0.20)` + 골드 테두리(`UiRoundedSprite.Make` border) + 이름/점수 골드; **WAITING** = `(1,1,1,0.03)`, 텍스트 `#9AA0A6`.
- `ShowResult` → 봇 Row 렌더, `UpdateLeaderboard` → `BuildRows` 후 `RenderRows`. `UpdateLeaderboard` 의 기존 가드(`!activeSelf` 무시, null 가드) 유지.
- 기존 `BuildLeaderboard`(mspace string) 제거, `DisplayName` 재사용.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode: `ResultLeaderboardModelTests` — score 내림차순 rank, WAITING 슬롯 채움(entries<max), 본인 플래그, 10자 초과 truncation, 빈 이름 `?`
- [ ] Play: 봇 폴백이 행 플레이트로 표시 → 실 랭킹 도착 시 교체, TOP3 배지색·본인 골드 강조·WAITING 회색 육안 확인

확인: 2026-07-08 — EditMode `ResultLeaderboardModelTests` 6/6 통과. 인게임(사용자 스크린샷)에서 행 플레이트·순위 배지(금/은/동/네이비)·본인 골드 행·WAITING 회색·서버 랭킹 교체 렌더 확인.
