# M0 구현 작업 요약

> 완료일: 2026-08-04
>
> 범위: battle-sim-extraction units 0~4
>
> 결과: 현행 ECS 전투의 결정론 기준선과 M1 Mono 전환용 parity 골든 확립

## 한 줄 결론

M0는 ECS를 제거한 단계가 아니다. 현행 ECS 전투를 **같은 조건이면 같은 순서·입력·시간·결과를 내는 비교 기준선**으로 고정하고, 이후 순수 C# sim으로 옮길 때 회귀를 검출할 `LegacyTraceV0` 골든을 만든 단계다.

하네스 실행 흐름은 다음과 같이 고정됐다.

`tick 입력 스케줄 → SkillRuntime → Bridge 배틀 프레임 → BattleSimGroup 1회 → presentation drain → LegacyTraceV0 기록`

라이브 PlayerLoop와 하네스 구동은 상호 배타이며, M0에서 fixed tick을 라이브 게임에 상시 적용하지 않았다.

## Unit 0 — 시스템 실행 순서 박제

### 구현

- 러닝 월드의 `BattleSimGroup` 44개 시스템 유효 총순서를 덤프하는 에디터 유틸을 추가했다.
- IncomingDamage 정산, 투사체 이동/착탄, 모디파이어 적용 등 미선언 순서 13건을 12개 파일에 `[UpdateBefore]`/`[UpdateAfter]`로 명시했다.
- 순서를 개선하거나 재배치하지 않고, 캡처된 현행 동작을 그대로 고정했다.
- 결과 정본을 [order-capture.md](order-capture.md)에 저장했다.

### 핵심 파일

- `Assets/_Project/Editor/SimOrderDumpMenu.cs`
- `Assets/_Project/Editor/SimOrderCaptureBootstrap.cs`
- `Assets/_Project/Scripts/Battle/**/**System.cs`의 순서 핀 12개 파일

### 검증

- 핀 전후 시스템 순서 diff 0
- Unity 컴파일 오류 0
- 후속 unit 2·4 자동 Play 시나리오가 전투 구동 smoke를 상위 호환으로 충족
- 커밋: `8795ac3c`

## Unit 1 — `SimEntityId` 도입

### 구현

- 매치 내에서 재사용하지 않는 spawn ordinal 기반 `SimEntityId`를 도입했다.
- Bridge가 생성하는 적, 방어 유닛, 순찰, 투사체, 존/차단 해저드, 장애물의 7개 스폰 경로에 ID를 부착했다.
- `NearestTargeting`, `FrontmostTargeting`, `LowestHealthTargeting`의 동률 축을 Entity 번호에서 `SimEntityId`로 교체했다.
- `AggroTargeting`과 `HazardCastSystem`에는 누락돼 있던 결정적 동률 규칙을 신설했다.
- `AttackSystem` 발사 패턴 RNG seed와 `ThreatTable` 동률 축도 `SimEntityId` 기반으로 바꿨다.

### 핵심 파일

- `Assets/_Project/Scripts/Battle/Units/SimEntityId.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Battle/Combat/*Targeting.cs`
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/ThreatTable.cs`

### 행동 계약

- 동률 승자와 발사 난수열은 의도적으로 달라질 수 있다. unit 4 골든은 이 변경 이후를 기준선으로 삼는다.
- sim 로직의 `Entity.Index` 잔존은 테스트 조립 월드용 `SimEntityId.Resolve` 폴백 1곳뿐이다. 뷰/디버그 및 입력 구성 측 예외는 unit 문서에 별도로 열거했다.

### 검증

- unit 관련 EditMode 26건 통과
- 당시 전체 EditMode 1,859건 중 실패 0, 기존 Ignore 2
- 후속 고정 입력 하네스와 7개 골든의 2회 실행으로 ID/RNG 재현성 확인
- 커밋: `3e7b33f5`

## Unit 2 — 고정 스텝 하네스

### 구현

- `BattleScaledRateManager`에 하네스 게이트와 1회성 `ArmStep(fixedDt)`를 추가했다.
- `BattleBridge`에 `BeginHarness`, `StepOneTick`, `EndHarness`를 추가하고 라이브 `Update` 본문을 `AdvanceBattleFrame(battleDt)`로 추출했다.
- ECS 시계, Bridge 배틀 시계, 웨이브/스폰, 이벤트 drain, `SkillRuntime` 쿨다운이 같은 fixed dt를 소비한다.
- `HarnessInputSchedule`이 입력을 wall clock 대신 tick index에 결박한다. 같은 tick의 여러 입력은 등록 순서를 보존한다.
- `HarnessActive`와 고정 seed를 `TestModeContext`에 분리하고, 도메인 리로드는 `SessionState` carry로 건넌다.
- 하네스가 ECS 그룹을 동기 자가구동하므로 Unity Editor 비포커스 상태에서도 렌더 프레임 펌프에 의존하지 않는다.

### 핵심 파일

- `Assets/_Project/Scripts/Battle/BattleScaledRateManager.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/HarnessInputSchedule.cs`
- `Assets/_Project/Scripts/Core/TestModeContext.cs`
- `Assets/_Project/Scripts/Core/SkillRuntime.cs`
- `Assets/_Project/Editor/SimHarnessRunner.cs`

### 검증

- 집중 EditMode 12/12
- 당시 전체 EditMode 1,865건 중 실패 0, 기존 Ignore 2
- 비하네스 PlayMode smoke 1/1
- seed `20260804`, 20Hz, 306 tick 2회 다이제스트 완전 동일
- 종료 시 Persistent allocator/Native Collection leak 0
- 커밋: `cc04bc19`

## Unit 3 — canonical `MatchConfig`

### 구현

- 한 판의 입력 조건을 불변 canonical 문자열로 물질화하고 SHA-256 `configHash`를 생성했다.
- 생성된 맵/웨이브, 덱, seed, 효과 타일, 유닛·스킬·투사체·해저드·기믹 스탯, 점수/비용 규칙과 gameplay knob를 포함한다.
- 문자열/필드 순서와 문화권을 고정하고 float/double은 invariant `R` 포맷으로 직렬화한다.
- `BattleBridge`의 SerializeField 87개를 gameplay 15개와 presentation/service 72개로 분류했다.
- 하네스 중 `LoginAutoImport`와 이미 진행 중인 runtime refresher callback이 SO 값을 덮지 못하도록 import lock을 추가했다.

### 핵심 파일

- `Assets/_Project/Scripts/Core/MatchConfigSnapshot.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Core/TestModeContext.cs`
- `Assets/_Project/Scripts/UI/Outgame/LoginAutoImport.cs`
- `Assets/_Project/Scripts/Core/*RuntimeRefresher.cs`

### 검증

- 집중 EditMode 37/37
- 당시 전체 EditMode 1,883건 중 실패 0, 기존 Ignore 2
- 실제 Play 2회에서 동일 `configHash`와 7,727-byte digest 확인
- Track A와 Track B `$ecs-reviewer` 모두 APPROVE
- 커밋: `11902d32`

## Unit 4 — `LegacyTraceV0` 골든

### 구현

- `LegacyTraceV0` 스키마에 config hash, seed, tick rate, deck/map 정보, command receipt, tick read model, 이벤트, 최종 점수와 상태 hash를 담았다.
- 이벤트는 serialize → deserialize → serialize byte 동일 검사를 통과한 데이터만 저장한다.
- JSON double 왕복 표현 차이를 제거하기 위해 battle clock을 `battleClockMicros` 정수로 기록한다.
- 운영 27채널을 전부 manifest하되, Bridge가 본래 소비하는 출력 18채널만 event stream에 기록한다.
- ECS 내부 같은-tick phase 전달용 9채널은 `internalPhaseChannels`로 명시하고 외부 계약에서 제외했다.
- trace tap은 기존 Bridge drain에서 관찰만 하며 큐 소비자를 추가하거나 라이브 처리 순서를 바꾸지 않는다.
- 골든 재생성용 Editor/batch runner와 7개 추적 JSON을 추가했다.

### 골든 시나리오

- `normal`
- `boss_wave`
- `multi_goal`
- `dreamcatcher_heavy`
- `forced_wave`
- `simultaneous_death`
- `restart`

### 핵심 파일

- `Assets/_Project/Scripts/Core/LegacyTraceV0.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.LegacyTrace.cs`
- `Assets/_Project/Editor/LegacyTraceGoldenRunner.cs`
- `Assets/_Project/Tests/EditMode/LegacyTraceV0Tests.cs`
- `Assets/_Project/Tests/Golden/LegacyTraceV0/*.json`

### 검증

- Unity 스크립트 컴파일 오류 0
- 7개 시나리오를 각각 새 Play 세션에서 2회 실행해 JSON byte diff 0
- 집중 `LegacyTrace` EditMode 5/5
- 전체 EditMode 1,888건: 1,886 통과, 실패 0, 기존 Ignore 2
- CardBuff PlayMode 1/1 — 코드 문제가 아니라 과거 문서의 기대값 불일치로 종결
- Track A common APPROVE, Track B `$ecs-reviewer` APPROVE, 최종 APPROVE
- 커밋: `c0f7bd4f`

## 최종 산출물

| 산출물 | M1에서의 역할 |
|---|---|
| `order-capture.md` | 순수 C# tick phase를 재구성할 실행 순서 정본 |
| `SimEntityId` | 커맨드·이벤트·스냅샷·뷰를 잇는 stable identity |
| `StepOneTick` | 구 ECS와 신 sim을 동일 입력/tick으로 비교할 구동 seam |
| `MatchConfigSnapshot` + `configHash` | 비교 대상의 입력 조건 동일성 증명 |
| `LegacyTraceV0` + 7개 골든 | M1 A/B parity의 legacy 기준 결과 |

## 변경하지 않은 것

- ECS 전투 시스템과 Entities 패키지는 아직 제거하지 않았다.
- 라이브 전투를 fixed tick으로 전환하지 않았다.
- MonoBehaviour-per-unit 구조를 도입하지 않았다.
- pause/slow-mo gameplay 시계 정책, 순수 C# sim 이식, `IMatchSession`, A/B swap은 M1 범위다.
- 서버와 `RemoteSession` 구현은 이 spec 이후 M3 범위다.

## 잔여 위험과 다음 경계

- M0 마지막 검증은 집중 PlayMode와 골든 runner까지 수행했다. 전체 PlayMode suite와 Player build는 실행하지 않았다.
- Burst 제거 후 Android ARM64 IL2CPP 성능은 아직 측정하지 않았다. M1 swap 전에 tick p95/p99와 steady-state GC를 측정해야 한다.
- M1은 상세 unit 7+가 아직 작성되지 않았다. 세션 계약, 데이터 대응표, tick pipeline, adapter 단일 drain, 소비자 재배선, sim 이식과 A/B gate를 먼저 스펙으로 분해한다.

## 커밋 체인

`8795ac3c` → `3e7b33f5` → `cc04bc19` → `11902d32` → `c0f7bd4f`

M0 종료 문서 정리는 `d1984413`에 기록돼 있다.
