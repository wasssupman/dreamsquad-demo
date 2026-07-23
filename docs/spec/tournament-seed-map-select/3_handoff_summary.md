# 3. Handoff Summary — tournament-seed-map-select

## Commit

- unit 0 `dcf3a204` tournament.seed 파싱 · unit 1 `90bb2fd3` 로비 선발행+시드 노출 · unit 2 `d0bdb85d` 맵풀 인덱스 교체

## Implemented

- `/tournament/play` 응답의 `data.tournament.seed`(uint64)를 파싱해 맵풀 인덱스를 결정론으로 선택: `index = seed % Count`. 같은 토너먼트 참가자 = 같은 (맵, 덱) = 같은 웨이브(맵별 덱 waveSeed 고정 승계).
- **타이밍 해법**: 로비 `OnStartGame` 이 `BeginMatchFromLobby()` 로 play 를 선발행 → 씬 전환 동안 응답 도착 → `BuildMapForBattle` 이 읽는다. `GameManager.OnEnable` 의 `BeginMatch()` 는 `_lobbyIssued` 플래그로 attempt 를 승계(재발행 없음).
- 인덱스 소스 3분기: `fixedMapSeed != 0`(디버그, 기존 로컬 선택) > `HasTournamentSeed`(서버 시드) > 폴백 **0번**(게스트/응답 미도착/직접 Play/테스트 모드 — "문제는 전부 0번").
- 선택 로그: `[BattleBridge] map pool index={i}/{n} (source=tournament|debug|fallback0)`.

## Key Files

- `Scripts/Core/Api/TournamentApi.cs` — `PlayState.tournament.seed`(ulong)
- `Scripts/Core/Api/TournamentMatchReporter.cs` — `BeginMatchFromLobby`/`HasTournamentSeed`/`TournamentSeed`
- `Scripts/UI/Outgame/OutgameMenuController.cs` — `OnStartGame` 선발행(게이트 통과 직후, Go 직전)
- `Scripts/Data/MapGrid/MapPoolSelect.cs` — `SelectIndexFromTournamentSeed`(순수)
- `Scripts/Bridge/BattleBridge.cs` — `BuildMapForBattle` 풀 분기(≈884)

## Verified

- testrig 배치 EditMode **1293 중 1291 green**(0 fail, 2 skip=기존 Ignored). 신규 6 테스트: 파스 2(실측 body·구 스키마 방어) + 풀선택 4(실측 시드 %5==3·경계·ulong.MaxValue·결정론).
- 실측 dev 서버 응답으로 스키마 확인(2026-07-23 curl): seed=9128566303723636648 → 5맵 풀에서 3번.
- **사용자 Play 확인 완료(2026-07-23)**: `source=tournament` 선택·재입장 동일 인덱스·중복 발행 없음·폴백 경로 정상.

## Notes (되돌리면 안 됨)

- **`BeginMatch` 의 `_lobbyIssued` 승계는 상태 리셋도 건너뛴다** — epoch++/필드 리셋을 하면 in-flight play 응답(시드 포함)이 stale 드롭된다.
- **시드 클리어는 BeginMatch 서두, HasAccount early-return 앞** — 로그아웃 후 게스트 판에 이전 세션 시드가 새는 것 방지.
- 전환 게이트 없음(의도): 응답 미도착이면 0번. 게이트/스피너는 후속 후보.
- attempt 라이프사이클(epoch·PendingMatchStore·complete/abandon) 무변경 — 발행 시점만 이동.
- `fixedMapSeed != 0` 디버그 경로가 토너먼트 시드보다 우선(특정 맵 강제 테스트용). 라이브 씬 값 0.

## Follow-up

- 사용자 Play: 로그인 입장 `source=tournament` + 같은 토너먼트 재입장 같은 맵 / 게스트·직접 Play `fallback0` / `play ok` 콘솔 1회.
- 후속 후보(README): 전환 게이트(응답 대기), 게스트 로컬 랜덤 유지 옵션, seed 의 웨이브/기믹 확장.
