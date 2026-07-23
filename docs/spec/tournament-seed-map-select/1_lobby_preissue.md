# 1. 로비 선발행 + 시드 정적 노출

## 목적

맵 빌드 **전에** 시드가 도착해 있도록, play 발행 시점을 로비 입장 버튼으로 앞당긴다. `GameManager.OnEnable` 의 기존 발행과 중복되지 않게 attempt 를 승계한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Api/TournamentMatchReporter.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame`

## 구현

1. `TournamentMatchReporter` 에 시드 노출 추가:
   - `public static bool HasTournamentSeed { get; }` / `public static ulong TournamentSeed { get; }`
   - play 성공 콜백에서 `state.tournament?.seed` 저장(`tournament == null` 이면 미보유). **발행 시작 시점에 클리어**(BeginMatch 서두) — 이전 매치 시드가 새 매치에 새는 것 방지.
2. 선발행 승계 플래그:
   - `public static void BeginMatchFromLobby()` — 내부적으로 기존 `BeginMatch()` 를 수행하고 `_lobbyIssued = true`.
   - `BeginMatch()` 서두에 `if (_lobbyIssued) { _lobbyIssued = false; return; }` — 로비가 이미 발행한 attempt(epoch·_attemptId·PendingMatchStore 저장 포함)를 그대로 승계. epoch 증가/필드 리셋도 건너뛴다(리셋하면 in-flight 응답이 stale 드롭됨).
   - 로비를 안 거치는 진입(에디터 직접 Play, 테스트 모드 패널)은 플래그 미설정 → 기존과 동일하게 OnEnable 발행.
3. `OutgameMenuController.OnStartGame`: 로드아웃 게이트 **통과 직후**, `SceneTransition.Go(SceneNames.Battle)` **직전**에 `TournamentMatchReporter.BeginMatchFromLobby()` 호출. 게이트 팝업으로 return 하는 경로에서는 발행하지 않는다(입장 확정 시에만).
4. attempt 라이프사이클(epoch stale 드롭·PendingMatchStore·complete/abandon)은 무변경 — 발행 "시점"만 이동.

## 완료 기준

- [x] (사용자 Play) 로비 입장 → BattleScene 도착 시점에 attempt 1개만 발행(콘솔 `play ok` 1회, 중복 발행 없음)
- [x] (사용자 Play) 시드 도착 시 `HasTournamentSeed == true`, 값 = 응답 `tournament.seed`
- [x] 새 발행 시작 시 이전 시드 클리어(BeginMatch 서두 무조건 클리어 — early return 앞)
- [x] (사용자 Play) 에디터 직접 Play(로비 미경유): 기존과 동일하게 OnEnable 발행, 회귀 없음
- [x] compile 0 error, EditMode green

확인 2026-07-23 — testrig 배치 EditMode 1293 중 1291 green. 사용자 Play 확인 완료 (커밋 90bb2fd3).
