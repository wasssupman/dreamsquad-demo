# 매치 모드 설계안 — 별첨 (2026-09-23 · 사용자 판정 4건 반영)

> 근거: `docs/plans/2026-09-22-battle-core-rebuild-census/05_match_mode_research.md`(Unreal GameMode/GameState · Lyra Experience · Unity SO 아키텍처 · BTD6 · Kingdom Rush · Arknights · Clash Royale · PvZ2 조사, 출처 URL 포함) + 이 저장소의 판례 `endless-mode`(은퇴: 모드가 하던 일이 「리포트 스킵」 하나였다).

## 원칙 6

1. **모드는 규칙, 담당자는 상태.** `WaveScheduler`·`CostLedger`·`HeartMeter`·`ScoreLedger`·`MatchClock`·`PlacementService`·`HandDeck`·`GimmickHost` 는 모드를 모른다. 담당자 안에 `if (mode == …)` 가 생기면 실패(계약 12 가 담당자 안으로 샌 것).
2. **닫힌 목표 종류 + 종류별 파라미터.** enum 으로 닫는다. 열린 액션 리스트(Lyra)는 형식만 빌리고 개방성은 빌리지 않는다.
3. **모드는 값을 덮어쓰지 않고 «어느 저작 자산을 쓸지» 고른다.** 웨이브 램프·당김 상한·보스 케이던스는 `AttackDeck` 소유 그대로 — 다르게 하려면 다른 덱을 만든다. 모드가 소유하는 것은 지금 단일 소유자가 없는 것뿐: 목표·종료 정책·점수 정책·시계 정책.
4. **저작물은 읽기 전용, 판이 쓰는 값은 구운 사본.** `MatchModeData`(SO) → `MatchDefinitionBuilder` → `ModeDef`(plain). 코어는 SO 를 모른다.
5. **재현 = modeId + seed.** 모드는 시드에서 파생하지 않는다(플레이어·서버가 고른다). 시드는 그 모드 안의 콘텐츠를 정한다.
6. **공정성 축은 모드가 안 건드린다.** 한 토너먼트 = 한 모드. 모드가 다르면 점수를 섞지 않는다.

## `MatchModeData` (SO) — 참조만, 수치 복제 없음

| 그룹 | 필드 | 비고 |
|---|---|---|
| 정체성 | `modeId`(안정 키, 리네임 금지) · `displayName` | 제출·리플레이·리더보드가 저장 |
| 목표 | `goalKind` enum · `goalParams` | 종류별 의미 아래 표 |
| 시계 | `clockKind`(FixedLimit / CountUp) · `durationSec` · `submitUnlockSec` · `allowSubmit` | 타이머 소유자 2곳(`AttackDeck`·`WavePlanAsset`)과 코드 상수 60 이 여기로 수렴 |
| 웨이브 원천 | `waveSourceKind`(GeneratedFromDeck / AuthoredPlan) · `deck`(null = 맵 풀 짝) · `plan` | 램프·당김 상한·케이던스는 덱 소유 그대로 |
| 맵 | `mapPool`(null = 기본 풀 — 드라이버 `_mapPool`) | **선택 4갈래**(옛 `BattleBridge.cs:1263~1300` 그대로 — unit 8b `MatchDefinitionBuilder.TrySelectEncounter`): dev 강제 인덱스(`DevMapOverride`, dev 슬롯 포함) > 디버그 고정 맵 시드(`seed % Count`) > 서버 토너먼트 시드(`seed % Count` — 같은 토너먼트 = 같은 맵·덱) > 0번. 맵·덱·플랜은 **같은 인덱스로 잠긴다**(엔트리 한 몸). 엔트리 플랜은 모드 플랜에 지고 강제 플랜(테스트·온보딩)에도 진다 |
| 기믹 | `gimmickEnabled` · `gimmickPool` | `BattleConfig` 2필드 이사 |
| 배치 | `costConfig` · `placementPhaseEnabled` · `autoStartCountdownSec` · `squadSlots`(현행 7) · `boardCap`(선택) · `retireEnabled` | 「배치 수량」 축 |
| 드림캐쳐 | `deckRuleConfig`(덱 10·Squad ≤2) · `awakeningConfig` · `publicActiveCount`(현행 2) | 「드림캐쳐 수량」 축 |
| 토너먼트 | `submitsReport` · `leaderboardId`(기본 = modeId) | `Endless` 판례가 요구한 플래그 형태 |

`BattleConfig` 는 4필드가 전부 이사하므로 소멸(또는 기본 모드 참조 1칸).

## 닫힌 목표 종류 (v1 후보 3)

| `goalKind` | 한 줄 | 파라미터 | 담당자에게 요구 |
|---|---|---|---|
| `KillScoreTimed` **(현행 라이브)** | 정해진 시간 안에 몇 마리 잡았나 | — | `MatchClock` 만료 → `complete` · 킬 생값 · 붕괴 = 즉시 종료(남은 시간 몰수) · 웨이브 무한 |
| `WaveClear` | 정해진 웨이브 N 을 끝까지 막았나 | `targetWaves` | `WaveScheduler` 읽기 모델 한 줄 「마지막 웨이브 dispatch + 전멸」(현행 `NoQueuedAttackersRemain` 재사용) |
| `TimeAttack` | 정해진 웨이브를 얼마나 빨리 끝냈나 | `targetWaves` | 위 신호 + `MatchClock` count-up · 점수 = 경과 시간(작을수록 좋음, 정렬 반대) |

`Endless` 는 넣지 않는다 — `KillScoreTimed` + `CountUp` + `submitsReport=false` 로 표현된다(enum 값 자격 없음).
세 종류가 건드리는 담당자는 `MatchClock`·`ScoreLedger`·`WaveScheduler` 셋뿐. 나머지는 파라미터만 다르게 받는다.

## 모드 로직의 자리 — `IMatchGoal` (Strategy · `ISkill` 선례와 같은 형)

```
IMatchGoal
  OnBegin(ctx) · OnTick(ctx)            // 종료 판정. EndMatch 는 여기 + 담당자 통로만
  BuildOutcome(ctx) → MatchOutcome      // 점수 · 표기 · 정렬 방향
  Read → GoalReadModel                  // HUD·결과화면이 읽는 것
MatchGoalContext = 담당자 읽기 모델 묶음 + 쓰기 권한 「MatchClock.EndMatch」 하나
```
- 목표는 개체 상태를 들지 않는다. 판정 둘(끝났나 / 점수가 얼마인가)만 소유 → 계약 12 만족.
- **종료의 «사유»는 담당자가, «의미»는 목표가.** `HeartMeter` 는 모드 무관하게 붕괴 시 `stress_full`(통로 3 유지). 그 판이 「같은 잣대로 줄 세워지는 판」인지 「웨이브 7/12 에서 멈춘 판」인지는 `BuildOutcome` 이 정한다(Arknights 섬멸전의 「부분 보상 클리어」 형).
- **`IMatchGoal` 은 지금 만든다**(사용자 결정 2026-09-23, 제약 8 개정 — 목표 종류라는 닫힌 축이 근거). v1 concrete 3: `KillScoreTimedGoal`(라이브) · `WaveClearGoal` · `TimeAttackGoal`. 모드 선택 UI(로비)는 이 spec 범위 밖 — 코어·SO·테스트 하네스 진입만.

## 흐름
로비(모드 선택 / 토너먼트 서버 지정) → `MatchDefinitionBuilder.Build(mode, squad, deck, stage, seed)` → `BattleMatch.Begin` (담당자를 `ModeDef` 파라미터로 구성 + 목표 concrete 1) → HUD/결과 ← `GoalReadModel` → 제출 `{modeId, seed, score, sortDirection}`.
모드 선택 우선순위 3단: 테스트 모드 강제 > 로비 선택/서버 지정 > 기본 모드. **`MatchDefinitionBuilder` 가 모드를 읽는 유일한 지점.**

## 현행 라이브 = `MatchMode_KillScore3Min` 한 장
`KillScoreTimed · FixedLimit 180 · submitUnlock 60 · GeneratedFromDeck(null) · mapPool null · gimmick off · placementPhase off/3s · squadSlots 7 · retire on · deck 10/Squad 2 · publicActive 2 · submitsReport on`. 종료 통로 3 그대로.

## 이 설계가 걷어내는 중복
타이머 소유자 2 → 1 · `BattleConfig` 4필드 → 모드 · `SubmitUnlockSec` 상수 → 저작 · 진입 3종 각자 config → Builder 단일 지점.

## 사용자 판정 (2026-09-23 확정)
1. **`IMatchGoal` 지금 만든다.** concrete 3 을 v1 에 둔다(제약 8 개정).
2. **`WaveClear`·`TimeAttack` 에서 마음이 부서지면 «패배»다.** `MatchOutcome.kind = Defeat`. 종료 통로는 여전히 `stress_full` 하나(넷째 통로 없음) — 라벨은 목표가 붙인다. 「지지 않는다」(설계 지향 1축)는 이제 **`KillScoreTimed` 의 성질**이지 전역 규칙이 아니다 → `ingame-flow.md` 1축 문면을 unit 9 문서 교체 때 「모드별」로 고친다. 「제출」 어휘는 `allowSubmit` 모드만.
3. **한 토너먼트 = 한 모드.** 서버 계약. 제출 payload 에 `modeId` 동봉, 리더보드는 `leaderboardId` 로 분리, `sortDirection` 은 목표가 정한다.
4. **슬롯 목록은 추후 확장 가능한 구조로.** `MatchModeData` 는 필드 append-only(직렬화 보존), `goalKind` enum 도 append-only. 새 슬롯은 「지금 단일 소유자가 없는 값」일 때만 모드로 올린다(원칙 3).

## 위험
- `configHash` 가 참조 SO 필드에 무반응인 함정 → `modeId` + 모드가 고른 자산 id 를 해시에 명시로 섞는다.
- `AttackDeck.timerDurationSec` 이관은 「모드가 이기고 덱 값은 폴백」으로 한 릴리스 병행 후 제거(`bossUnit` 리네임 판례).
- 모드 × 덱/플랜 유효성(예: `targetWaves 12` 인데 덱 `maxWaveCount 10`) → 모드 유효성 EditMode 테스트 필수.
- draft 폴백 진입은 정리 대상(제거 확정 목록에 이미 있음).
