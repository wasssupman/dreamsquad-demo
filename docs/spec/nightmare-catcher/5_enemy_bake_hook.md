# 5 — 보스 스폰 베이크 + arm 시스템 등록

## 목적

보스 스폰 시 나이트매어캐쳐 슬롯 + 위협 테이블을 베이크하고, 두 메커닉의 신규 arm 시스템을 등록한다. defender 부착 라이프사이클을 건드리지 않는 **병렬 경로**.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` (authoring 필드 append)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (적 스폰 베이크, `:4022` 인근)
- 신규 시스템: `BossPeriodicTriggerSystem`, `BossHealthThresholdSystem` (Combat), `BlinkApplySystem` (Movement)

## 구현

### Authoring (데이터로 선언)
- `AttackUnitData` 에 `DcMechanic[] nightmareMechanics` (+ threat 사용 여부) 필드 **append**(직렬화 back-compat). 나이트매어 mechanics 있으면 그 적이 곧 보스.

### 스폰 베이크 (병렬 경로)
- 적 스폰(`BattleBridge.cs:4022` `AddComponent<AttackUnitTag>` 인근)에서 `nightmareMechanics` 있으면 → **BossTag + ThreatTable 버퍼 + DcTriggerSlot 베이크**.
- 베이크 로직은 기존 defender 경로(`BattleBridge.cs:2631` DcTriggerSlot 빌드, `:2681` 버퍼 add/get) **재사용/병렬**. defender 전용 부착 API(`:2555` `ApplyDreamcatcherCardToUnit`, defender 가드)는 **안 씀** — 적 스폰은 별도 진입점.
- **Teardown 은 신규 0**: 보스 = `AttackUnitTag` → `DestroyEntitiesByType<AttackUnitTag>()`(`:373`)로 적과 함께 정리. defender teardown(`DestroyEntitiesByType<DefenderUnitTag>`) 무관. 라이프사이클 병렬이 여기서 성립.

### 신규 arm 시스템 (선례: `TauntAttackGrantSystem`)
- **BossPeriodicTriggerSystem** (Combat) — BossTag+DcTriggerSlot 쿼리, PeriodicTimer accumulator 틱 → AreaBarrage 발사(SkyFall×TileAoe 캐리어, unit 2). ecb 구조변경 패턴은 `TauntAttackGrantSystem.cs:32/42/50` 선례.
- **BossHealthThresholdSystem** (Combat) — HealthThreshold 평가 → SelfBlink 요청 enqueue(`BlinkRequestEventsSingleton`, unit 3).
- **BlinkApplySystem** (Movement) — blink 요청 드레인 → 위치 대입(맥락 경계: 위치는 Movement 소유). `MovementSystem` 텔레포트 선례(`:90`) 참조.
- **순서**: Combat 판정 → Movement 소비(같은/다음 틱). SystemGroup 등록 순서 명시.

## 완료 기준

- [ ] 보스 스폰 시 BossTag + ThreatTable + DcTriggerSlot 부착 확인(reflection/Play).
- [ ] `nightmareMechanics` 없는 일반 적은 무변경(베이크 스킵).
- [ ] teardown 이 `AttackUnitTag` 정리로 보스 슬롯/threat 도 회수 — leak 0(반복 스폰).
- [ ] arm 시스템 순서(Combat 판정 → Movement blink) 검증.
- [ ] (렌즈 B) ecb 구조변경/NativeQueue lifecycle/시스템 순서 ecs-reviewer.
