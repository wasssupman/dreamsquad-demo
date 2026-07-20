# 2. 퇴근 시스템 (ClockOutTimer → 사직서 스폰 + 사망)

## 목적

룰 1 완성: **전투 시작(running) 후** 배치 defender 가 `clockOutSeconds` 만료 시 배치 타일에 **사직서 스폰** + **퇴근**(기존 사망 경로 재사용). running-only 게이트(사용자 결정).

## 변경 대상

- `Assets/_Project/Scripts/Battle/BattleRunning.cs` — 신규 running 신호 싱글턴 (`BattleTimeScale` 동형)
- `Assets/_Project/Scripts/Battle/Effects/ClockOutTimer.cs` — 신규 카운트다운 컴포넌트
- `Assets/_Project/Scripts/Battle/Effects/ClockOutSystem.cs` — 신규 lazy-attach + tick + 퇴근 시스템
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `PushBattleRunningToEcs`(매 프레임 `_running`→ECS)

## 구현

1. **`BattleRunning { bool Value }`**(`Wassup.Battle`): BattleBridge 가 매 프레임 `_running` write(`PushBattleTimeScaleToEcs` 동형, 그룹-wide phase 인프라). running-only 시스템 공용 — 피로도/픽업 placement-gating 후속도 재사용 가능.
2. **`ClockOutTimer { float elapsed }`**(Effects): FatigueAccrual 전례 카운트다운.
3. **`ClockOutSystem`**(Effects, Burst, BattleSimGroup): `RequireForUpdate<ClockOutGimmickConfig>` self-gate. `TryGetSingleton<BattleRunning>` 이 false 면 early-return(배치 페이즈 미작동). Pass1 lazy-attach(활성 defender `WithNone<ClockOutTimer,PendingDeployment,DeadTag>`). Pass2 tick → 만료 시 (a) 배치 타일(`DefenderTile.cell`)에 `Resignation` 스폰 (b) 치명 `IncomingDamage`(1e9 sentinel — dmgTakenMul 곱해도 확실 사망, LastRun crash 전례) (c) `ClockOutTimer` 제거.
4. 사망은 `DamageApplicationSystem`(Units)→`DeadTag`→`UnitLifecycleSystem`→`DefenderDeathEvent` 기존 경로 그대로(맥락 경계 유지 — Health 쓰기는 Units).

## 완료 기준

- compile 0 에러(Unity 재컴파일).
- Play(ClockOut 기믹): 전투 시작 후 배치 유닛이 `clockOutSeconds`(10s)에 퇴근(소멸) + 그 타일에 사직서(흰 종이, unit 1 뷰) 등장. 배치 페이즈에선 타이머 미작동. — **기믹 asset + `gimmickPool` 등록(unit 5) 후 실측**; 이 유닛까지는 compile + 로직 배선.

확인 2026-07-16 — Unity 재컴파일 후 read_console 에러 0(authoritative). Play 실측은 unit 5 기믹 asset 배선 후.
