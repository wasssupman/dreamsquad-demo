# 5 — 당김 기록 확인

## 목적

「A는 32초에 4웨이브를 당겼다」 비교(PRD §7.2)의 **원재료가 어디까지 남는지** 확정하고, 남는 지점을 테스트로 고정한다.

## ⚠ 초안의 전제가 틀렸다 (2026-08-13 확인)

초안은 「`wave_forced` 가 **제출 스냅샷**에 실리는지」를 물었다. 확인해보니 **배틀 로그는 서버로 가지 않는다**:

- `BattleLogger.SnapshotJson()` 을 부르는 **프로덕션 코드가 0곳**이다(유일한 호출자는 EditMode 테스트 2개).
- 서버로 가는 것은 `TournamentMatchReporter.ReportResult(score, deckInfoJson, …)` 뿐이고, 호출부(`BattleBridge`)가 넘기는 것은 **점수 int 하나 + 덱 정보**다.
- 배틀 로그의 종착지는 `EndSession()` 이 쓰는 **로컬 파일** `GameLogs/session-*.json` 이다.

그래서 이 unit 의 실제 사정거리는 **로컬 기록까지**다. 「나중에 백엔드가 서도 비교할 과거 데이터」는 이 unit 을 다 해도 확보되지 않는다 — 서버가 배틀 로그를 받는 계약이 선행이고, 그건 README 후속 후보의 백엔드 묶음이다.

## 변경 대상

- 테스트: `Tests/EditMode/BattleLogPullEventTests.cs`(신규)
- **프로덕션 코드 변경 0** — 아래 확인 결과가 전부 이미 구현돼 있다

## 구현

**확인 결과 3개 — 전부 이미 있다. 코드를 더하지 않는다.**

1. **당김 기록**: `BattleBridge.ForceNextWave` → `RecordWaveEvent("wave_forced", waveIndex, 시각, forced: true)` → 로컬 로그의 `wavePattern.events`. 시각은 경과 초(Battle 클럭)라 PRD §8 의 「경과 시간 축」과 맞다.
2. **누적 처치 곡선**: `AddScoreEvent("enemy_killed", 점수, 시각)` 이 시각과 함께 쌓이므로 곡선은 **파생 가능**하다. 별도 필드를 만들지 않는다 — 서버가 곡선 형태를 요구하면 그때 파생 함수를 붙인다. **웨이브 번호 축으로 쪼개지 않는다**(PRD §8 명시 — 당김으로 도달 시각이 갈린다).
3. **종료 사유**: `SetResult` 가 `victory` / `victory_siege` / `victory_timeout` / `defeat` / `defeat_timeout` 5종을 이미 구분한다. 시간 만료와 골 붕괴가 다른 문자열이다.

**그래서 이 unit 은 테스트만 남긴다.** 기록이 조용히 빠지는 사고는 몇 달 뒤 「데이터가 왜 없지」로만 드러나므로, 지금 pin 을 박는 값이 크다.

**가짜 par 는 넣지 않는다** (계약 9). unit 3 의 목표 페이스는 표시 전용이고 로그에 새면 가짜 경쟁 수치가 진짜인 척 저장된다 — 테스트가 이걸 지킨다.

## 완료 기준

- [ ] EditMode: 당김 이벤트가 로컬 로그 직렬화 결과에 **시각·웨이브 번호와 함께** 남는다
- [ ] EditMode: 종료 사유가 시간 만료(`*_timeout`)와 붕괴(`defeat`)를 구분한다
- [ ] EditMode: 직렬화 결과에 `pace`/`baseline` 필드가 **없다**(계약 9 누출 방지)
- [ ] 컴파일 통과, 콘솔 에러/경고 0

## 남는 것 (이 unit 밖)

- **배틀 로그를 서버로 보내는 계약** — 없으면 §7.2 비교 기능은 영영 데이터가 없다. README 후속 후보의 백엔드 묶음에서 다룬다.
