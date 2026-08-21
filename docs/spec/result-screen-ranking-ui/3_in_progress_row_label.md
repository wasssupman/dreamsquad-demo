# unit 3 — 남의 0점 행을 「악몽 처치중!」으로 바꾼다

## 목적

결과창 랭킹에서 **다른 참가자의 0점**은 성적이 아니라 **아직 성적이 없는 상태**다. 그런데
화면은 그것을 `0` 으로 그려서, 그 사람이 한 마리도 못 잡았다는 없는 성적을 지어낸다.

토너먼트 서버는 슬롯 배정과 채점이 다른 호출이다(`docs/spec/tournament-flow-guards/README.md`
락 모델):

- `POST /tournament/play` → 그 유저를 토너먼트에 **락 = 슬롯 배정**. 이 시점 점수는 없다.
- `POST /tournament/complete/{attemptId}/{score}` → 점수가 **처음** 들어간다.

그래서 "배정됐는데 0점"은 다음 중 어느 것이든 될 수 있고, 응답만으로는 **가릴 수 없다**:

1. 아직 판을 하는 중 (대부분)
2. 나가기·강제종료로 0점 몰수 (`AbandonMatch` / `ReconcilePending`)
3. 진짜로 한 마리도 못 잡음

**사용자 결정(2026-08-21)**: 셋을 구분할 신호가 없으므로 **가장 흔한 1번으로 읽히게 한다.**
0 이라는 거짓 성적보다 "아직 안 나왔다"가 화면에서 참이다.

## 변경 대상

- `Assets/_Project/Scripts/UI/ResultScreen.cs` — `CreateRow` 의 점수 컬럼 한 갈래.

**행 모델은 건드리지 않는다** (feature-wide 계약 1: 서버 데이터 경로 `Row`/`BuildRows`/
`DisplayName` 불변). 판정은 렌더 시점 한 줄이고 호출처가 하나라 별도 타입/함수로 빼지 않는다
(CLAUDE.md 제약 8·10).

**히스토리 상세 팝업(`TournamentHistoryPanel` → `LeaderboardList.Render`)은 범위 밖.** 거기는
끝난 토너먼트도 보므로 같은 문구가 영구히 거짓이 된다. 결과창은 방금 끝난 판 직후에만
떠서 "진행 중"이 참일 창이 넓다.

## 구현

```
inProgress = !IsWaiting && !IsPlayer && Score <= 0
```

| 상태 | 이름 | 점수 컬럼 |
|---|---|---|
| 빈 슬롯 (`IsWaiting`) | `대기 중...` | `-` (변화 없음) |
| 배정됐고 점수 미확정 | 유저명 | **`악몽 처치중!`** — 26pt, non-bold, `WaitingText` |
| 점수 있음 / 내 행 | 유저명 | `12,400` (변화 없음) |

- **내 행은 제외한다.** 내 점수는 방금 내가 낸 값이고, 0킬이면 진짜 0이다. 히어로 숫자가
  `0기` 인데 리스트가 "처치중!"이면 같은 화면이 두 말을 한다.
- 골드·볼드는 **실점수의 어휘**라 쓰지 않는다. 회색 non-bold 로 "이건 점수가 아니다"를 남긴다.
- 순위 뱃지는 그대로 둔다 — 현재 데이터 기준의 실제 위치라 거짓이 아니다.

## 완료 기준

- [x] compile 통과 — `dotnet build Wassup.Runtime.csproj` 오류 0 (2026-08-21).
      EditMode 는 미실행: `LeaderboardList.cs` / `ResultLeaderboardModelTests.cs` diff 0줄이라
      행 모델 경로에 영향이 없다.
- [x] 경로 검증 — 결과창의 행 렌더러는 `RenderRows`→`CreateRow` 하나뿐이고(`Show`/
      `UpdateLeaderboard` 둘 다 여기로 모인다), 새 분기는 **서버 실엔트리에서만** 켜진다.
      폴백(`BuildPendingRows`)은 모든 행이 `IsPlayer` 아니면 `IsWaiting` 이라 구조적으로
      성립 불가 → 게스트·오프라인·랭킹 도착 전 화면 무회귀.
- [x] 결과창 육안: 0점 타 참가자 행이 `악몽 처치중!` 으로, 빈 슬롯은 `대기 중... / -` 로,
      내 행은 숫자로 그려진다. 문구가 이름 컬럼을 침범하지 않는다(폭 230 안).

> **사용자 확인 2026-08-21** — 결과창 육안 통과.

## 후속 후보

- **서버가 완주 여부를 준다면 추측을 걷어낸다.** `ResultEntry` 스키마는 우리가 파싱하는
  `userId/userName/score/rank/deckInfo` 보다 크다. 완주 플래그나 완료 시각이 있으면
  `Score <= 0` 추측 대신 그것을 읽어 2번(0점 몰수)을 정직하게 갈라낼 수 있다.
  확인하려면 로그인 세션이 필요해 이번 범위에서 제외한다.
