# 3 — 인계 요약

## Commit

| 해시 | 내용 |
|---|---|
| `80cb9eee` | unit 0 — HUD 점수를 킬점수로 통일 |
| `782a7527` | 가장자리 플래시를 순간 화력 기준으로 |
| `1e48aef8` | unit 1 — `GamePhase.Tally` + 종료 3종 라우팅 |
| `920dda1a` | unit 2 — 합산 연출 |
| `596191c5` | 인지 단계 분리 (라벨 선행·시선 유도) |
| `7a33ab7d` | 코드 리뷰 반영 7건 |
| `bdc2cff7` | 전투 여운 1초 + 딤 동시 |
| `7d74c559` | 사후 감사 — 거짓 주석·후속 후보 이관 |
| (이 커밋) | unit 4 — Tally 흐름 PlayMode 테스트 |

## Implemented

- 전투 종료 → **Tally 연출** → 결과 화면. `BeginTally`/`FinishTally` 단일 관문
- 우상단 HUD 점수가 **최종 점수의 킬축과 같은 값**이 됐다 (처치당 +10 → 유닛별 `killScore`)
- 연출이 그 숫자에서 이어 시간 → 스트레스를 순차 가산. 총 4.0초, 전 구간 탭 스킵
- 가장자리 플래시가 누계 기준 → **1초 내 300점** 순간 화력 기준으로
- 패배는 두 축이 0이라 건너뛰고 0.78초에 종료 ("0을 굴리는" 장면 없음)

## Key Files

- `Assets/_Project/Scripts/UI/ScoreTallyView.cs` — 연출 전부 (신규)
- `Assets/_Project/Scripts/UI/ScoreHudView.cs` — `AddScore` / `RollSettled` / `PulseAttention`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BeginTally` / `FinishTally`
- `Assets/_Project/Scripts/Core/GameManager.cs` — `GamePhase.Tally`

## Verified

- EditMode **1129 통과 / 0 실패**, 콘솔 클린
- PlayMode `TallyFlowTest` 2건 (unit 4) — `onDone` 을 끊어 검출력 증명 완료
- HUD == `_killScoreTotal` — 2,400 샘플 불일치 0
- 합산 실측: 킬 6,400 → +11,500 → +7,200 → **25,100**, `onDone` 도달
- 페이즈 전이 `Battle → Tally(HUD 보임) → Result(HUD 숨김)`
- 플래시 발화 조건 5케이스 (2기 안 터짐 / 3기·보스 터짐 / 만료 / 쿨다운)
- 사용자 체감 확인 (2026-07-21)

## Notes (되돌리면 안 되는 것)

1. **`GamePhase.Tally` 는 enum 맨 뒤**다. 흐름상 Battle↔Result 사이지만 `CameraDirectionConfig`
   가 이 enum 을 int 로 직렬화한다(에셋에 `phase: 1/3/4/5`). 중간 삽입 시 Result 가 5→6 으로
   밀려 카메라 설정이 어긋난다. **`git grep '*.asset'` 으로는 못 잡는다** — enum 은 이름이 안 남는다.
2. **`ScoreHudView` 의 Tally 분기에서 `_pendingKills` 를 비우지 마라.** 같은 Update 안에서
   `DrainEnemyKilledEvents → CheckVictory → SetPhase(Tally)` 가 돌아, 비우면 **판을 끝낸 그 킬만**
   연출을 통째로 잃는다. 하필 여운 1초 동안 노출된다. (리뷰가 잡은 심각 결함)
3. **서버 제출은 연출 시작 시점.** 끝까지 기다리면 그 4초 사이 앱이 죽었을 때 기록이 사라진다.
   `SetResult`/`SetScore` 는 `BeginTally` **호출 전**에 — 제출이 로그 스냅샷을 쓴다.
4. **`onDone` 은 어느 경로로 끝나도 호출된다.** 끊기면 결과 화면이 영영 안 뜬다.
   `SetActive(false)` **앞**에 두는 순서도 유지할 것(사이에 `yield` 가 끼면 조용히 죽는다).
5. **스킵 시 남은 축의 `AddScore` 가 먼저 실행된다.** 총점 보존(계약 2)이 여기 걸려 있다.
6. **`labelTopOffset` 은 HUD 배지 하단(278)보다 커야 한다.** Tally(5)가 ScoreHud(6) 아래라
   작으면 라벨이 배지에 가린다. 현재 296.
7. `PulseAttention` 은 **버스트 창을 먼저 비운다.** 안 그러면 값 변화 없이 플래시가 오발한다.

## Follow-up

- **실전 승리 경로 미확인** — 밸런스상 승리 유도가 어려워 `Play()` 직접 호출로 검증했다.
  배선은 패배 경로로 확인됐으므로 조합은 성립하지만 승리로 끝나는 판을 끝까지 본 적은 없다
- **스킵(탭) 실입력 미확인** — 코드 경로만 있다
- **보스 처치 +2,000 눈으로 미확인** — 5웨이브째 등장이라 검증 구간에 안 나왔다. 같은 채널이라
  구조상 성립한다
- **배틀로그 `score_events[]` 값 미확인** — 이걸 안 봤기 때문에 `BattleLogger.cs` 의
  "처치당 +10" 주석이 거짓이 된 걸 오래 못 잡았다(사후 감사에서 발견·수정). 로그를 실제로
  열어보는 검증이 빠지면 같은 일이 반복된다
- **딤 램프 모양 미샘플** — `EditorApplication.update` 가 비포커스 구간에서 멈춰 1초 창을 세 번 놓쳤다.
  선형 보간이라 산술은 자명하고 체감은 사람 눈 문제라 중단했다
- 후속 후보(범위 밖): 카메라 연출 동반 · 축별 사운드 · 신기록 강조

> `ScoreHudView` 는 더 이상 "표시 전용"이 아니다. `battle-score-formula` README 의 계약 12
> ("라이브 HUD 점수는 표시 전용으로 존치")와 후속 후보 "HUD ↔ 최종 점수 통합"은 이 spec 이
> 소화했다. 그 문서를 읽을 때 함께 볼 것.
