# 2 — 고정 스텝 하네스 드라이버 (StepOneTick)

## 목적

현행 sim은 가변 프레임 dt 구동이라 같은 seed 2회 실행이 같은 결과를 내지 않는다 — 골든의 전제가 없다. **주의: dt 상수 주입만으로는 안 된다** — `BattleScaledRateManager`는 렌더 프레임당 1회 갱신이라 고정 dt를 꽂으면 게임 속도가 프레임레이트에 비례한다. 하네스 모드 한정으로 **명시적 `StepOneTick()` 드라이버**를 만들어, ECS 시계·Mono `_battleClock`(웨이브/스폰)·`SkillRuntime` 쿨다운(현재 별도 `Time.deltaTime`)을 **한 스텝 안에서** 전진시키고, 입력을 벽시계가 아닌 sim tick 스케줄로 반입한다. 라이브 게임 경로는 무변(fixed tick 상시화는 M1 신 sim의 몫).

## 변경 대상

- `Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs` — 하네스 게이트 + `ArmStep(fixedDt)`: 게이트 중 플레이어 루프 구동은 전부 skip, arm 된 스텝만 고정 dt push. **동기 자가구동 계약** — 호출측이 arm 직후 `group.Update()` 를 직접 호출(플레이어 루프에 맡기면 에디터 비포커스 정지가 재현된다). 기본 off — 라이브/기존 `BattleScaledRateManagerTests` 무변
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — Update 배틀 프레임 본문을 `AdvanceBattleFrame(battleDt)` 로 추출(시계·펜딩 히트 VFX·빔이 **한 dt** 공유 — 기존 3회 독립 read 는 프레임 상수라 라이브 값 불변) + `BeginHarness/EndHarness/StepOneTick`. 스텝 순서 계약: ①스케줄 입력 ②스킬 Tick ③브리지 Update 상당(시계·웨이브/스폰·이전 sim 이벤트 drain) ④심그룹 1회 ⑤도약 뷰 드레인(LateUpdate 소유분의 tick 정밀 회수). 라이브 `Update → ECS Simulation → LateUpdate` 순서를 보존하고, 라이브 Update 는 `HarnessActive` 게이트로 상호 배타
- `Assets/_Project/Scripts/Core/SkillRuntime.cs` — `Tick(float dt)` seam 추출. 라이브 raw `Time.deltaTime` 유지(기존 동작 — 배틀 시계와 갈리는 것도 기존 그대로, M0 라이브 무변 계약)
- `Assets/_Project/Scripts/Core/HarnessInputSchedule.cs` — 신규: tick 인덱스 결박 입력 주입기(같은 tick 다건 = 등록 순서 계약). 데이터 커맨드화는 M1 IMatchSession 몫
- `Assets/_Project/Scripts/Core/TestModeContext.cs` — `HarnessActive`(장수명 — **`Active` 에 얹으면 안 된다**: GameManager.Start 가 1회 소비 후 Clear) + `HarnessFixedSeed`(SessionState 캐리로 도메인 리로드 횡단, `ApplyEditorTestCarry` 동형). 시드는 `EnsureMatchSeed` 에서 1회 소비하고 `EndHarness` 가 잔여 상태를 정리
- `Assets/_Project/Scripts/Core/GameManager.cs` — `EnsureMatchSeed` 에 하네스 시드 최우선 분기(완료 기준 "같은 seed 2회"의 실현 수단 — 초안 변경 대상에 없던 추가) + 실제 하네스/디버그 고정 여부를 `MatchSeedFixed` 에 반영
- `Assets/_Project/Editor/SimHarnessRunner.cs` — 검증 러너: 같은 seed + 같은 스케줄(tick 100 `ForceNextWave`)로 2회 Play, tick별 다이제스트(클럭·적/방어/투사체·웨이브 인덱스·대기 스폰·골·킬 점수) 전문 대조. 배틀 진입은 PlayMode 테스트 관례(`bridge.StartBattle()` 직행). 종료는 World가 살아 있을 때 `StopBattle` 로 Persistent 필드를 정리한 뒤 PlayMode를 빠져나오고, 배치 프로세스 종료는 `EnteredEditMode` 이후 수행
- `Assets/_Project/Tests/EditMode/BattleScaledRateManagerTests.cs`, `HarnessInputScheduleTests.cs`, `TestModeContextHarnessTests.cs` — arm 1회 소비·라이브 복귀·잘못된 dt 차단·입력 순서·시드 1회 소비 회귀 핀
- 하네스 실행 중 `Time.captureDeltaTime = fixedDt` 고정(뷰 코루틴 잔여 결합 방어), 종료 시 진입 전 값 복원

## 구현

하네스 모드 플래그는 `TestModeContext`에 둔다. 스텝 루프: `입력 반입(스케줄된 tick) → SkillRuntime Tick → Bridge 스텝(시계·웨이브·이전 sim 이벤트 drain) → BattleSimGroup 1회 갱신(고정 dt) → LateUpdate 소유 도약 이벤트 drain`. Bridge가 ECS보다 먼저 도는 라이브 PlayerLoop 순서를 그대로 박제한다. 에디터 포커스 함정(비포커스 시 frame 정지 — lessons 참조)은 동기 자가구동으로 회피한다. pause/slow-mo는 하네스에서 미사용(라이브 전용 — gameplay 시계 정책화는 M1 후속).

## 완료 기준

- 하네스에서 같은 seed + 같은 입력 스케줄 2회 → `_battleClock` 궤적·웨이브 스폰 tick·이벤트 카운트 완전 동일.
- 라이브(비하네스) 경로 행동 무변 — Play smoke 1판 정상, 콘솔 에러 0.
- 에디터 비포커스 상태에서도 하네스 실행이 정지 없이 완주.

## 검증 결과 (2026-08-04)

- 집중 EditMode: **12/12 통과** — RateManager 기존 6 + 하네스 게이트 3 + 스케줄 1 + 시드 상태 2.
- 전체 EditMode: **1,865건 / 실패 0** — 통과 1,863, 기존 Ignore 2.
- 비하네스 PlayMode smoke: `ActiveTileCastTest` **1/1 통과**.
- 배치·비포커스 결정론: seed `20260804`, 20Hz, tick 100 `ForceNextWave`, 각 run **306 tick** 실행. 두 다이제스트 **7,651자 완전 동일**.
- 종료 로그: `NullReferenceException`, 컴파일 오류, `Persistent allocates`, Native Collection leak **0건**. 종료 직전 `StopBattle` 로 Persistent 필드를 dispose하고 `EnteredEditMode` 뒤 프로세스를 종료함.
