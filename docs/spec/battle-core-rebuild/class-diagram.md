# 핵심 클래스 UML — 전투 코어와 Unity 층

> 초안 2026-09-22. 이름은 제안이며 unit 0 에서 확정한다. 화살표는 **의존 방향**(코어 ← Unity 층, 코어 → Skills/UnitAi). 코어 안에서 UnityEngine 타입은 컴파일되지 않는다.
> 값 타입·순수 함수(`SkillMath` · `AttackReach` · `ModifierMath` · `FootprintMath` · `GridMath` · `FlowFieldBuilder` · `PathSmoothing` · `AgentCollision` · `Separation` · `TargetPersistence` · `KillAttribution` · `ShieldMath` · `DotTick` · `HeatMath` · `StressMath` · `MatchSeed` · `WavePatternGenerator`)은 그대로 salvage 하므로 그리지 않는다.

## 1. 경계와 조립 지점 — 매니저는 없다

```mermaid
classDiagram
    direction LR
    namespace Unity층_Wassup_Runtime {
        class MatchDefinitionBuilder { +Build(mode, squad, deck, stage, seed) MatchDefinition «SO → plain 정의표» }
        class MapStageScanner { +Scan(prefab) MapSnapshot }
        class BattleDriver { -accumulator -tickRate +Update() +SetTimeScale() «시간만» }
        class BattleInput { +OnPlace() +OnRetire() +OnAttachCard() +OnCastActive() «→ Command» }
        class ViewPools { «유닛·투사체·장판·상태FX·오버헤드 — 각자 구독, 통합 뷰 없음» }
        class HudViews { «담당자 읽기 모델 구독» }
    }
    namespace 전투코어_Wassup_BattleCore {
        class BattleMatch { +Begin(MatchDefinition) +Apply(Command) Receipt +Tick() +Events «조립 지점: 담당자 생성 + 틱 순서 나열. 규칙·상태 없음» }
        class MatchDefinition { +ModeDef +UnitDef[] +EnemyDef[] +ProjectileDef[] +HazardDef[] +CardDef[] +GimmickDef[] +StackRuleDef[] +MapSnapshot +WavePlanDef +DeckDef +seed +configHash }
        class BattleWorld { «개체 목록 · 맵 런타임» }
        class EventBus { +Publish(CoreEvent) +Subscribe(kind, order) «구독 순서가 계약» }
        class TickPipeline { «담당자가 등록한 단계의 순서» }
        class MatchClock { +tick +battleTime +timer +phase +EndMatch(reason) «종료 통로 소유» }
        class WaveScheduler { +WaveState +RNG +QueueDue() +Pull() +Bonus «처치 이벤트 구독 → 전멸 판정» }
        class CostLedger { +cost +regen +TryPay() }
        class PlacementService { +Occupancy +cooldowns +TryPlace() +Retire() «배치 판정 순서 소유» }
        class HeartMeter { +Heart Health +stress +OnGoalReached() «붕괴 → MatchClock.EndMatch(stress_full)» }
        class ScoreLedger { +kills +SubmissionScore «처치 이벤트 구독» }
        class HandDeck { +queue +awakening +attached +Recover() +RecallOthersToFront() }
        class GimmickHost { «시즌 기믹 바인딩 부착» }
        class IMatchGoal { <<interface>> +OnBegin(ctx) +OnTick(ctx) +BuildOutcome(ctx) MatchOutcome +Read GoalReadModel «goalKind 별 concrete · 쓰기 권한은 MatchClock.EndMatch 하나» }
        class MatchGoalContext { «담당자 읽기 모델 묶음 + EndMatch» }
    }
    MatchDefinitionBuilder --> MatchDefinition
    MapStageScanner --> MatchDefinition : MapSnapshot
    BattleDriver --> BattleMatch : Tick / Apply
    BattleInput --> BattleDriver : Command
    EventBus --> ViewPools : CoreEvent
    HudViews --> ScoreLedger : 읽기
    HudViews --> MatchClock : 읽기
    HudViews --> CostLedger : 읽기
    HudViews --> HeartMeter : 읽기
    BattleMatch --> MatchDefinition : 읽기만
    BattleMatch *-- BattleWorld
    BattleMatch *-- EventBus
    BattleMatch *-- TickPipeline
    BattleMatch *-- MatchClock
    BattleMatch *-- WaveScheduler
    BattleMatch *-- CostLedger
    BattleMatch *-- PlacementService
    BattleMatch *-- HeartMeter
    BattleMatch *-- ScoreLedger
    BattleMatch *-- HandDeck
    BattleMatch *-- GimmickHost
    BattleMatch *-- IMatchGoal
    IMatchGoal --> MatchGoalContext
    WaveScheduler ..> EventBus : 구독(처치)
    ScoreLedger ..> EventBus : 구독(처치)
    HeartMeter ..> EventBus : 구독(골 도달)
    HandDeck ..> EventBus : 구독(사망·퇴근·처치)
    HeartMeter --> MatchClock : EndMatch
    IMatchGoal --> MatchClock : EndMatch
```

- **`BattleBridge` 는 어디에도 없다.** 369 메서드 + 직렬화 필드 91 은 unit 0 귀속표대로 위 담당자 중 하나로 가거나 삭제된다. 「어디로도 못 가는 메서드」 = 설계 결함 신호.
- 담당자는 **상태 + 규칙 + 자기 틱 단계 + 자기 이벤트**를 소유한다. `BattleMatch` 는 만들고 순서를 나열만 한다.
- 순서 의존(처치 → 전멸 판정, 골 도달 → 붕괴 → 보너스 제안)은 `EventBus` 구독 순서로 고정한다. 한 함수가 두 담당자를 차례로 부르지 않는다.
- 마음 체력은 `HeartMeter` 가 든다(마음 개체가 아니다 — 「마음 N개 공유」 이사 비용 0).
- `IMatchGoal` 은 매치 모드(별첨 `match-mode-design.md`)가 정한 목표 concrete(`KillScoreTimed` · `WaveClear` · `TimeAttack`). 담당자들은 모드를 모르고, 목표는 담당자 읽기 모델로 「끝났나 / 점수가 얼마인가」 둘만 판정한다. `ModeDef` 는 `MatchDefinition` 에 실려 담당자 파라미터(시계·웨이브 원천·기믹 풀·배치/드림캐쳐 수량)를 정한다.

## 2. 개체 모델

```mermaid
classDiagram
    class BattleWorld { +Units List~Unit~ +Projectiles List~Projectile~ +Hazards List~Hazard~ +Fields List~FieldCarrier~ +Pickups List~Pickup~ +Resignations List~Resignation~ +Map MapRuntime +Find(SimEntityId) +Spawn*() +Destroy*() «id 오름차순 · 풀 대여» }
    class Unit { +Id SimEntityId +Kind UnitKind +Faction +Def +Position float3 +HitRadius +Dead bool +Deploying bool +PastGoal bool }
    class UnitKind { <<enum>> Defender Enemy Patrol Structure BlockingHazard }
    class Health { +value +max +ScaleMax() }
    class ShieldSlots { +Absorb() +Sum() «FIFO» }
    class ModifierSet { +Slots +Effective EffectiveStats +dirty «병합 키 4축» }
    class CcState { +slots[5] +IsLocked() +BossImmune }
    class DotSet { «(origin, element) 2축» }
    class AttackState { +targetMask +targetLayers +interval +hitDelay +committedTarget +lock +shape +patternSlots }
    class MoveState { +radius +lastMoveDir +holdingGround +waypointProgress +chaseField }
    class AiStatus { «UnitAi 결정을 저장만» }
    class Footprint { +anchor +size «방어유닛만» }
    class Detection { +range +hunting +grace }
    class Aggro { +capacity +held +target }
    class BindingList { +Bindings List~Binding~ +DamagedCounter }
    class ProgressiveStates { +UltimateLeap? +Cocoon? +LethalTimer? +Charge +LastRun? «중단 정책 표 동봉» }
    class Projectile { +Id +Def +Movement +Payload +Owner +Target +pos +pierceBudget +hitRecords }
    class Hazard { +Def +originCell +radius +restDuration +targetLayers }
    class FieldCarrier { +Kind AllyBuff·Pull·Portal +cell +cell2 +range +duration }
    class Pickup
    class Resignation
    Unit *-- Health
    Unit *-- ShieldSlots
    Unit *-- ModifierSet
    Unit *-- CcState
    Unit *-- DotSet
    Unit o-- AttackState : 공격자만
    Unit o-- MoveState : 이동체만
    Unit *-- AiStatus
    Unit o-- Footprint
    Unit o-- Detection
    Unit o-- Aggro
    Unit *-- BindingList
    Unit *-- ProgressiveStates
    BattleWorld *-- Unit
    BattleWorld *-- Projectile
    BattleWorld *-- Hazard
    BattleWorld *-- FieldCarrier
    BattleWorld *-- Pickup
    BattleWorld *-- Resignation
```

- 「컴포넌트가 있나」 분기는 **정책 값**(`Def.AttackPolicy` 등) 또는 nullable 부분(`o--`)으로. 배치 중·사망·궁극기 제외는 `Unit` 의 bool 셋을 한 술어 `IsTargetable()` 로 묶는다(현행 `WithNone` 14곳의 단일화).
- 거점·길막 장판은 `Unit` 의 종류다(현행 아키타입 동형).

## 3. 맵 런타임

```mermaid
classDiagram
    class MapSnapshot { +tiles +placeMask +spawns +goals +waypoints +spawnRoutes +structures +bonusSpawns +size «plain, 스캐너 산출» }
    class MapRuntime { +Flow FlowFieldSet +Nav NavGridSet +Obstacles ObstacleSet +Hunt DefenderHuntField +Occupancy PlacementOccupancy +EffectTiles }
    class FlowFieldSet { +Slot(dest, mask) FlowSlot «(목적지 × 통행 마스크)» +Rebuild(signature) }
    class FlowSlot { +dist[] +dir[] «직접 인덱싱 금지» }
    class NavGridSet { +For(layer) NavGrid }
    class ObstacleSet { +signature +Add/Remove }
    class PlacementOccupancy { +occupied +cellOwner +Occupy(footprint) +Release() «항상 쌍» }
    MapRuntime --> MapSnapshot : 읽기만
    MapRuntime *-- FlowFieldSet
    FlowFieldSet *-- FlowSlot
    MapRuntime *-- NavGridSet
    MapRuntime *-- ObstacleSet
    MapRuntime *-- PlacementOccupancy
```

## 4. 틱 파이프라인 · 커맨드 · 이벤트

```mermaid
classDiagram
    class TickPipeline { +Run(ctx) «phase 순서가 계약» }
    class ITickPhase { <<interface>> +Run(TickContext) }
    class CommandPhase { «phase 0 · 동기 · Immediate seam» }
    class FieldPrepPhase { «장애물→흐름장 · 어그로 · 모디파이어 적용 · CC · 존 · 주기 → [Periodic]» }
    class DeathConvergePhase { «사망 표시 수렴 · 배치 활성화» }
    class AiMovePhase { «도발 부여 · UnitAi · 거점 목적지 · 이동 · 분리» }
    class TickProjectilePhase { «효과 틱 · 투사체 · 스탯 만료/집계 · 스택 · 열기/피로 · 픽업 · 사직서» }
    class CombatPhase { «공격 → [Attack] → 피해 → [Death] → 후처리 → 소멸 → [Lifecycle] → 경계 → [Threshold] → 궁극기 · 순간이동» }
    class OwnerSteps { «담당자가 각자 등록: WaveScheduler.Step · CostLedger.Step · PlacementService.Step · HeartMeter.Step · GimmickHost.Step · IMatchGoal.Evaluate · MatchClock.Step — 순서는 TickPipeline 이 나열» }
    class FlushPhase { «CoreEvent 플러시 · 세대 초기화» }
    class TickContext { +World +State +Def +dt +Dispatcher +Outbox }
    class Command { +Kind +unitDef +cell +facing +cardId +host +skill +targetCell «struct» }
    class Receipt { +accepted +reason RejectReason }
    class CoreEvent { +Kind +tick +a SimEntityId +b SimEntityId +SiteFired +SiteTarget +faction +amount «값 스냅샷 · 뷰 전용 20종» }
    class Site { +pos float3 +originBody «0 = 칸» }
    TickPipeline o-- ITickPhase
    ITickPhase <|.. CommandPhase
    ITickPhase <|.. FieldPrepPhase
    ITickPhase <|.. DeathConvergePhase
    ITickPhase <|.. AiMovePhase
    ITickPhase <|.. TickProjectilePhase
    ITickPhase <|.. CombatPhase
    ITickPhase <|.. OwnerSteps
    ITickPhase <|.. FlushPhase
    CommandPhase --> Command
    CommandPhase --> Receipt
    FlushPhase --> CoreEvent
    CoreEvent *-- Site
```

- `[…]` 는 트리거 seam(§5). 캐스트 seam 은 캐스터 제거로 없다.
- 매치 규칙은 한 phase 가 아니라 **담당자별 단계**다. `TickPipeline` 은 순서만 든다 — 옛 `TickBattleFrame` 처럼 한 함수가 드레인 20개를 차례로 부르는 형태를 만들지 않는다.
- 슬로모·정지는 `BattleDriver` 가 `Tick()` 호출 횟수로 만든다. `dt` 는 항상 1/60.

## 5. 트리거→발동 (rev 3)

```mermaid
classDiagram
    class BindingDef { +EventKind +Subject Self·Any +Conditions[] +SubjectFilter +Effect ISkill +Params SkillParams +fireCap +Lifetime +revokeOnExpire +Origin «정의표 · 정적 (트리거,페이로드)→(concrete,형)» }
    class Binding { +Def +Owner SimEntityId +InstanceId +Seq +fireCount +counters +remaining «풀 대여» }
    class BindingRegistry { +Attach(owner, def) Binding +Detach(instanceId) +ForHost(id) +MatchBindings +Expire(lifetime 사유) «OnDetach 발행» }
    class TriggerEvent { +Seam +EventKind +Host +Target +SiteFired +SiteTarget +CasterFaction +TargetLayers +Direction +Params +Generation «값 스냅샷» }
    class TriggerDispatcher { +Enqueue(TriggerEvent) +Drain(seam) «세대 BFS · 깊이 4 · 전순서 (seam, gen, seq, owner, instance)» }
    class CoreSkillContext { «ISkillContext 구현 · 질의는 World · Emit → IntentApplier» }
    class IntentApplier { +Apply(SimIntent) +Apply(MetaIntent) «즉시 적용 · 원자 개시는 한 함수» }
    class AttackMod { <<static>> +Compose(AttackContext) «HeavyStrike · Bounce · Frontmost · DmgVsSleeping · charge 소비 — 바인딩 밖» }
    class ISkill { <<interface · Wassup.Skills 기존>> +Execute(caster, target, params, ctx) }
    class ISkillContext { <<interface · 기존>> }
    Binding --> BindingDef
    BindingRegistry *-- Binding
    TriggerDispatcher --> BindingRegistry : 리스너 조회
    TriggerDispatcher --> TriggerEvent
    TriggerDispatcher --> ISkill : Execute
    ISkill --> ISkillContext
    ISkillContext <|.. CoreSkillContext
    CoreSkillContext --> IntentApplier
    IntentApplier --> BattleWorld
    IntentApplier --> CostLedger : MetaIntent(GainCost)
    IntentApplier --> HandDeck : MetaIntent(쿨다운·인수인계)
    CombatPhase ..> AttackMod : 공격 조립
```

- 감지자(공격·피해·소멸·경계·주기·커맨드)는 `TriggerEvent` 를 **값으로 채워** 넣는다. 드레인 시점 재질의 없음.
- 정적 표(`DcSkillRouting` 후계)는 `BindingDef` 를 만들 때 한 번 읽히고 프리뷰(`DcRangeCatalog`)가 같은 표를 읽는다.

## 6. Unity 층 상세 — 통합 뷰 없음

```mermaid
classDiagram
    class BattleDriver { -BattleMatch match -float acc -float rate +Update() «acc += dt*rate; while acc ≥ 1/60: match.Tick(); 이벤트 방출» +Pause() +SetTimeScale() }
    class EventBus { «코어 소유 · Unity 층은 구독만» }
    class UnitViewPool { -Dictionary~SimEntityId, UnitView~ +OnSpawn/OnDeath/OnMove «자기 등록부만» }
    class UnitView { <<abstract · 기존>> +Id SimEntityId }
    class SpineUnitView
    class SpriteUnitView
    class QuadUnitView
    class ProjectileViewPool { «OnProjectileSpawn/Hit 구독» }
    class HazardViewPool { «OnHazardSpawn/Destroy 구독» }
    class StatusFxSpawner { «OnCc/OnDot/OnShield 구독» }
    class DcAuraVisualPool { «OnAttach/OnDetach 구독» }
    class UnitOverheadUiLayer { «OnHealth/OnAttach 구독» }
    class LeapPresenter { «OnLeap 구독 — 유닛 동기 앞 순서 고정» }
    class BattleInput { +DragPlacement +CardDrag +ActiveAim «→ Command → Receipt 로 손패 복귀 등 결정» }
    class HudViews { «ScoreHud · Timer · Tray · CostWell · Hand — 담당자 읽기 모델 + 이벤트 구독» }
    BattleDriver --> EventBus : 틱 뒤 방출
    EventBus --> UnitViewPool
    EventBus --> ProjectileViewPool
    EventBus --> HazardViewPool
    EventBus --> StatusFxSpawner
    EventBus --> DcAuraVisualPool
    EventBus --> UnitOverheadUiLayer
    EventBus --> LeapPresenter
    UnitViewPool --> UnitView
    UnitView <|-- SpineUnitView
    UnitView <|-- SpriteUnitView
    UnitView <|-- QuadUnitView
    BattleInput --> BattleDriver
    HudViews --> BattleDriver : 읽기 모델
```

- 뷰 등록부(`_defenderByTile` 등 11종)는 코어 담당자(`PlacementService`·`BindingRegistry`·`HandDeck`)로 이사하고, 각 뷰 풀은 `SimEntityId → 자기 뷰` 사전만 갖는다. Entities 누수 24파일은 키 타입 치환으로 끝난다.
- 뷰 간 순서가 필요한 곳(도약 연출이 유닛 위치 동기 **앞**)은 구독 순서로 고정한다 — 옛 `LateUpdate` 순서 계약의 후계.
