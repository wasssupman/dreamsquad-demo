# 2 — 고정 스텝 하네스 드라이버 (StepOneTick)

## 목적

현행 sim은 가변 프레임 dt 구동이라 같은 seed 2회 실행이 같은 결과를 내지 않는다 — 골든의 전제가 없다. **주의: dt 상수 주입만으로는 안 된다** — `BattleScaledRateManager`는 렌더 프레임당 1회 갱신이라 고정 dt를 꽂으면 게임 속도가 프레임레이트에 비례한다. 하네스 모드 한정으로 **명시적 `StepOneTick()` 드라이버**를 만들어, ECS 시계·Mono `_battleClock`(웨이브/스폰)·`SkillRuntime` 쿨다운(현재 별도 `Time.deltaTime`)을 **한 스텝 안에서** 전진시키고, 입력을 벽시계가 아닌 sim tick 스케줄로 반입한다. 라이브 게임 경로는 무변(fixed tick 상시화는 M1 신 sim의 몫).

## 변경 대상

- `Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs` — 하네스 모드: 외부 주입 고정 dt를 push (프레임 결합 해제)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StepOneTick(fixedDt)` 진입점: `_battleClock` 가산·웨이브/스폰 체크·큐 drain을 스텝 구동으로 호출 가능하게 (Update 경로와 상호 배타)
- `Assets/_Project/Scripts/Core/SkillRuntime.cs` — 하네스 시계 주입(스텝 dt 소비)
- 스크립트 배틀 확장 — 기존 `TestModeContext.Set`+StartBattle 경로에 **입력 스케줄**(tick N에 배치/카드/스킬 커맨드) 주입기 추가. 기존 e2e는 웨이브 캐리만 하므로 신규 작성분
- 하네스 실행 중 `Time.captureDeltaTime` 고정(뷰 코루틴 잔여 결합 방어)

## 구현

하네스 모드 플래그는 `TestModeContext`에 둔다. 스텝 루프: `입력 반입(스케줄된 tick) → BattleSimGroup 1회 갱신(고정 dt) → Bridge 스텝(시계·웨이브·drain)`. 에디터 포커스 함정(비포커스 시 frame 정지 — lessons 참조)은 스텝 구동이라 회피됨을 확인. pause/slow-mo는 하네스에서 미사용(라이브 전용 — gameplay 시계 정책화는 M1 후속).

> ⚠ **위 두 문장(플래그 위치·스텝 순서)은 착수 시 스케치이고 구현에서 정정됐다.** 플래그는
> `TestModeContext` 가 아니라 전용 `SimHarnessClock` 에, 스텝 순서는 `Bridge → ECS` 다.
> 이유는 아래 「발견 3건」. 변경 대상 목록의 `SkillRuntime` 도 셋 중 하나였을 뿐이다
> (`CostRuntime`·`PlacementCooldownRuntime` 이 같은 부류).


## unit 2 에서 실제로 한 것 (2026-08-22)

### 구동 모드

`Wassup.Core.TimeControl.SimHarnessClock`(신규) 이 「얼마나」(`StepDt`)와 「언제 한 번」
(스텝 요청)을 **둘 다** 소유한다. 둘 다 필요한 이유가 이 unit 의 핵심이다 — dt 만 상수로
꽂으면 `BattleScaledRateManager` 는 여전히 렌더 프레임당 1회 갱신이라 게임 속도가
프레임레이트에 비례한다. 요청 소비를 통과 조건으로 두면 플레이어 루프의 그룹 갱신이
그 자리에서 죽고, 스텝만 그룹을 전진시킨다.

⚠ **플래그를 `TestModeContext` 에 두지 않았다**(스펙 스케치와 다른 점). 그쪽 `Active` 는
웨이브 플랜 캐리를 가리키고 `GameManager` 가 **1회 소비 후 `Clear()`** 한다 — 같이 두면
첫 소비에서 하네스가 조용히 꺼진다.

시간 원천은 한 곳에서 갈아끼웠다: `TimeManager.DeltaTime` 이 하네스 중에는 벽시계 대신
`StepDt` 를 곱한다. 소비처를 하나씩 고치지 않은 이유 — 하나 빠뜨리면 「대부분 결정론」이
되고 그건 결정론이 아니다.

### 스텝 1회 = `BattleBridge.StepOneTick()`

① 자기 `Update` 로 돌던 배틀 런타임 3종 → ② `TickBattleFrame()`(시계·웨이브·스폰·drain)
→ ③ `BattleSimGroup` 1회. 라이브 `Update` 는 하네스 중 early-return 한다(이중 전진 금지).

### 발견 3건

⚠ **스펙 스케치의 스텝 순서가 뒤집혀 있었다.** 스케치는 「입력 → ECS → Bridge」인데
라이브 플레이어 루프는 `MonoBehaviour.Update` → `SimulationSystemGroup` → `LateUpdate`
순이다(그 사실은 `BattleBridge.LateUpdate` 주석이 이미 근거로 쓰고 있었다). 스케치대로
두면 ECS 가 만든 캐리어를 **같은 스텝에서** 드레인하게 되어 한 틱 빠른 세상이 되고,
그 위에서 뜬 골든은 라이브가 한 번도 낸 적 없는 궤적을 정본이라 우긴다. 그래서
**Bridge 먼저, ECS 나중**으로 구현했다. (unit 0 의 교훈과 같은 형태 — 스케치가 아니라
실제 실행 순서가 정본이다.)

⚠⚠ **스텝에 넣어야 할 런타임이 `SkillRuntime` 하나가 아니었다.** `CostRuntime` 과
`PlacementCooldownRuntime` 도 자기 `Update` 에서 `TimeManager` 배틀 델타로 self-tick 하고,
셋 다 **입력이 통과하느냐를 게이트한다**. 스텝 밖에 남기면 같은 틱의 같은 입력이 두 판에서
다른 판정을 받는다. 이건 추론이 아니라 실측으로 드러났다 — 넣기 전에는 하네스의 배치
입력이 매번 `InsufficientCost` 로 거부돼 **입력 스케줄이 통째로 공전**했다.

⚠⚠⚠ **코스트 재생의 스위치를 UI 가 갖고 있다.** `PlacementPhaseView` 가 배치 진입에
`ResetToStart`, 전투 시작에 `BeginRegen` 을 부른다. 스크립트 진입(`PrepareDraftMap →
BeginPlacement → StartBattle`)은 그 뷰를 지나지 않아 코스트가 0 에 멎는다. 지금은
하네스 드라이버가 그 UI 역할을 대신하고 있고, **라이브 진입 경로 자체의 재현은 unit 3
(MatchConfig 물질화)의 몫**이다 — 이 자리에 남은 유일한 「하네스 ≠ 라이브」다.

### 드라이버

`Assets/_Project/Editor/Battle/SimHarnessRunMenu.cs` —
`Wassup/Battle/Sim Harness/Run Determinism Check (2 runs)`. 같은 seed 로 판을 두 번 세워
900틱씩 굴리고 틱별 다이제스트를 대조해 `harness-determinism.md` 를 생성한다.

다이제스트는 **카운트가 아니라 상태 지문**이다: 살아 있는 모든 sim 엔티티의
(`SimEntityId`, 위치, 체력)을 ID 순으로 접은 FNV-1a. 수만 맞고 위치가 갈리는 사고를
통과시키지 않기 위해서다 — 그게 정확히 골든이 잡아야 할 종류의 사고다.

입력 스케줄(틱 → 커맨드)은 **런타임이 아니라 드라이버가** 갖는다. 지금 소비자가 하나뿐이고,
커맨드 어휘의 정본은 unit 4(골든)와 M1(세션 파사드)이 정할 것이라 그 앞에 자리를 잡아두면
두 번 만들게 된다.

## 완료 기준

- [x] 하네스에서 같은 seed + 같은 입력 스케줄 2회 → **900틱 전량 일치**(`_battleClock`
      궤적·엔티티 수·상태 지문). 시계는 정확히 `15.0000s = 900 × 1/60`.
      입력 4건(틱 150·330·510·690, 유닛 4종)이 **전부 실제로 배치됐다** — 같은 유닛 4번을
      걸었을 땐 `LimitReached` 로 3건이 공전해 증거가 반쯤 비어 있었다.
- [x] 라이브(비하네스) 경로 행동 무변 — Play smoke 1판 정상(357 프레임에 시계 7.24s,
      적 5기 생존, 코스트 재생 정상), 콘솔 에러 0, `Time.captureDeltaTime` 원복 확인.
      EditMode 2564건 중 실패 1건은 사전 실패(말파이트 desc 길이, 무관).
- [x] 에디터 비포커스에서 정지 없이 완주 — 실측 `editorFocused=False`,
      2×900 스텝이 **`unityFramesConsumed=0`** 으로 0.8초에 완주. 「프레임이 멎어도 돈다」가
      아니라 **애초에 프레임을 쓰지 않는다**.

확인 2026-08-22 · 증거 `harness-determinism.md`(자동 생성).
