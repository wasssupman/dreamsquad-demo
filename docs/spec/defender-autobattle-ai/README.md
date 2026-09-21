# Defender Autobattle AI — 방어유닛 행동 결정을 아키텍처 무관 로직 레이어로

상태: **초안 2026-09-21 · 승인 대기** (units 미착수). 선행: `defender-deploy-phase`(3b1992a9 + `Wassup.UnitAi` asmdef 신설).

## 검증 질문

**「이 방어유닛이 지금 무엇을 하는가/할 수 있는가」가 엔진을 모르는 순수 결정 함수 하나(`DefenderAi.Decide`)에서 나오고,
ECS 는 그 입력을 만들고 결정을 실행만 하며, 뷰는 같은 상태를 읽어 그리는가 — 그리고 그 전환이 라이브 동작을 하나도 바꾸지 않는가.**

## 배경

- 사용자 원칙(2026-09-21): *"autobattle AI 의 로직은 아키텍처(모노, ECS) 상관없는 별도의 로직 레이어에서 돌아야 한다."*
  ECS 는 적용(apply) 레이어다. 선례 = `Wassup.Skills`(`noEngineReferences`, `ISkill.Execute(ISkillContext)` — 도메인이 «무엇을»을 정하고 `EcsSkillContext` 가 실행).
- 지금 방어유닛의 「무엇을 하나」는 시스템 4곳에 흩어진 파생 판정이다: `AttackSystem`(START 가능 = `!actionLocked && cooldown ≤ 0 && 타겟`),
  `MovementSystem`(`locked`), `HazardCastSystem`(쿨다운만 — CC 락 없음), 배치 페이즈(`PendingDeployment`). 적은 `EnemyAiState`(저장 상태) +
  `EnemyAiStateSystem.Evaluate`(순수 static 이지만 ECS 파일 안)로 한 단계 앞서 있다.
- `defender-deploy-phase` 가 씨앗을 심었다: `Wassup.UnitAi` 에 `UnitActionPhase`(Locked > Swinging > Free) · `DeployPhaseClock`. 이 spec 은 그 씨앗을
  **결정 함수 하나**로 키우고, 적용 레이어가 그 결정만 실행하게 뒤집는다.

## 목표 형태

```
Wassup.UnitAi (로직 · 엔진 참조 불가 · EditMode 테스트가 곧 규칙서)
   struct DefenderAiInput   { deploying, dead, actionLocked, swinging, cooldownReady, hasTarget, summonAlive, loopOverride… }  // plain 값 스냅샷
   enum   DefenderAiState   { Deploying, Locked, Swinging, Ready(대기), Engaging(공격 진행), Sustaining(소환물 유지)… }
   struct DefenderDecision  { state, startAttack, startCast, keepLoop… }
   static DefenderAi.Decide(in DefenderAiInput) → DefenderDecision

Wassup.Runtime / ECS (적용)
   DefenderAiStateSystem(Combat, 적 FSM 과 같은 밴드): 컴포넌트 → Input → Decide → DefenderAiState 컴포넌트에 씀(유일 writer)
   AttackSystem · HazardCastSystem · MovementSystem: DefenderAiState / Decision 을 **읽어** 실행. 자기 판정 삭제
   브리지: 소환사 루프 오버라이드(SetLoopOverride) 같은 «상태 → 뷰 이름» 밀어 넣기도 이 상태에서 파생

Mono (뷰)
   SpineUnitView/SpriteUnitView: 애니 우선순위·루프 선택을 DefenderAiState 하나로(지금은 원샷 게이트·오버라이드·순환이 각자)
```

## 작업 단위 (초안)

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| 0 | 로직 | `DefenderAiInput/State/Decision` + `DefenderAi.Decide` — 오늘의 판정을 **그대로** 옮긴다(동작 무변). 진리표 EditMode 테스트 = 규칙서 |
| 1 | 적용·저장 | `DefenderAiState` 컴포넌트(Combat) + `DefenderAiStateSystem`(유일 writer). 아직 아무도 안 읽음 |
| 2 | 소비 전환 | AttackSystem(START) · HazardCast · Movement 가 상태를 읽는다. 자기 판정 삭제. `AttackSystemUnifiedLoopTests`·`CcActionLockTests` 무수정 초록 |
| 3 | 소환사 | 「소환물 생존 → 능력 루프」를 브리지 폴링이 아니라 상태(`Sustaining`)로. `SetLoopOverride` 는 상태 미러가 됨 |
| 4 | 뷰 | 원샷/루프/순환 우선순위를 상태 하나로 읽기. `PatrolDefenderPlayTest` 무수정 |
| 5 | 적 편입 | `EnemyAiStateSystem.Evaluate` 를 `Wassup.UnitAi.EnemyAi.Evaluate` 로 이동(동작 무변) — 두 진영이 같은 레이어 |
| 6 | 검증 | EditMode 진리표 · 골든 코퍼스 **무변**(동작 무변의 증거) · PlayMode lane |

## Feature-wide 계약 (초안)

1. **결정은 한 함수, 소유는 한 시스템.** `DefenderAi.Decide` 만이 상태를 «정하고», `DefenderAiStateSystem` 만이 컴포넌트에 «쓴다». 나머지 전부 읽기.
2. **입력은 값이다.** `DefenderAiInput` 에 Entity·컴포넌트·매니저가 들어가지 않는다. 스냅샷을 만드는 코드가 적용 레이어의 전부다.
3. **동작 무변이 기본값.** 이 spec 은 판정을 옮기지 바꾸지 않는다. 바꾸고 싶은 것(HazardCast CC 락, 사망 프레임 START 억제, 재배치 모션 길이)은 **별도 unit 으로 명시**해 골든 갈림을 그 unit 에 귀속시킨다.
4. **쿼리 랭크는 남는다.** `WithNone<PendingDeployment>`·`DeadTag` 는 «판에 없다»의 표현이라 상태 컴포넌트와 별개로 유지 — 상태는 «있는 유닛이 무엇을 하나».
5. **적과 방어유닛은 같은 레이어, 같은 모양, 다른 함수 — 2단 구조.** 공통 술어층(`UnitActionPhase.Resolve → CanStartAction`, CC 락)은 양 진영이
   **같은 함수**를 부르고, 의도층(`DefenderAi.Decide` / `EnemyAi.Evaluate`)은 진영별이다. 하나의 인터페이스로 묶지 않는다 — 입력·상태 집합이
   다르고(적 = 이동 정책 축, 방어유닛 = 배치·유지 축) 둘을 한 손으로 잡는 소비자가 없다(제약 8 · 제네릭 2개 금지). 그런 소비자가 생기면 그때 승격.
6. **트레이스.** 상태 전이는 사건이다 — `TraceChannel` append(«왜 안 쐈나»를 로그로 읽는다). 골든은 채널 추가로만 갈리고 값은 같아야 한다.
7. **아키타입 판별은 정책 필드로, 타입 체크 금지.** 소환사·폭탄맨·순찰병·캐스터의 다른 행동은 `Decide` 안의 `if (isSummoner)` 가 아니라
   SO 의 **정책 enum**(적의 `engageMovement{Halt/Advance/Pulse}` 선례)을 입력으로 받아 분기한다. 행동은 데이터, 로직은 정책을 해석한다.
   유닛이 늘어도 `Decide` 가 아키타입 switch 로 자라지 않게 하는 장치.
8. **결정 틱 = sim 틱.** 표준 오토배틀러가 쓰는 저주기 결정 틱은 이 규모(방어 ~10 · 적 ~30)에서 이득이 없고 결정론만 흐린다. 매 sim 틱 평가.

## 열린 질문 (승인 전 결정)

- 상태 집합의 이름 — 위 초안(Deploying/Locked/Swinging/Ready/Engaging/Sustaining)로 갈지, 적 FSM 어휘(Marching/Engaging/…)에 맞출지. **권장: 방어유닛 전용 어휘**(축이 다르다).
- 뷰(unit 4)까지 이번에 갈지. **권장: 포함** — 뷰의 원샷 게이트·오버라이드·순환이 각자 판정하는 마지막 사본이다.
- 적 편입(unit 5)을 여기서 할지. **권장: 포함**(동작 무변 이동 하나) — 두 진영이 같은 레이어에 나란히 있어야 다음 사람이 자리를 안 헷갈린다.

## 표준 오토배틀 AI 와의 대조 (2026-09-21 판정: 이 장르·규모에 맞는 표준형)

맞는 점: Sense(값 스냅샷) → Decide(우선순위 FSM) → Act(적용 레이어) 분리 · 결정 로직의 엔진 분리 · 결정론 · CC 인터럽트를 우선순위로.
우선순위 FSM 을 고른 이유: 킹덤러시·명일방주 류(제자리 방어유닛 + 레인 적)는 상태 4~6개 FSM 이 정석. 행동 트리·Utility AI 는 상태 십수 개·가중치 튜닝이
필요한 RTS/시뮬의 도구라 지금은 과설계.

**미리 자리를 잡아 두는 부족분 2개**:
- **타겟 선택**(오토배틀 AI 의 절반) — 지금은 `AttackSystem` 의 `NearestTargeting`·포커스 락에 있고 이 spec 은 `hasTarget` 비트만 넘긴다. 후속 spec
  `unit-ai-targeting` 에서 타겟팅 규칙(최근접/최저체력/후열/클래스 우선)을 로직 레이어로 편입 — 후보 목록을 값으로 넘기는 형태(`Span<T>` Burst 호환 확인 필요).
  이 spec 은 그 자리(`DefenderAiInput.hasTarget` → 후보 스냅샷)를 막지 않는 모양으로만 설계한다.
- **아키타입 정책 데이터** — 계약 7. 이 spec 에서 소환사(`Sustaining`)를 첫 정책 필드로 옮긴다(`SummonPatrolAbility` 존재 여부가 아니라 정책 enum).

## 이 spec 이 확장하지 않는 것

- 새 행동(재장전·후퇴·회피) — 레이어가 서면 `Decide` 에 분기 하나로 들어간다. 그게 이 레이어의 존재 이유이지, 이 spec 의 범위는 아니다.
- 타겟 선택 규칙의 이동 — 후속 `unit-ai-targeting`(위).
- 판정 변경(HazardCast CC 락 · 사망 프레임 START 억제 · 재배치 모션 길이) — 계약 3, 별도 unit/spec.
