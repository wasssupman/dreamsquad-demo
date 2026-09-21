# 1 · `PendingDeployment{stage, remaining}` + `DeploymentActivationSystem` + 활성화 채널

## 목적

배치 페이즈의 시계와 종료를 sim(Units)이 소유한다. 브리지·UI 는 «시작»과 «착지»만 알린다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/PendingDeployment.cs` — 태그 → 데이터
- 신규 `Assets/_Project/Scripts/Battle/Units/DeploymentActivationSystem.cs`
- 신규 `Assets/_Project/Scripts/Battle/Units/DefenderActivatedEventsSingleton.cs` (`DefenderDeathEventsSingleton` 과 같은 형태)
- `CLAUDE.md` 채널 목록 30 → 31 · `docs/reference/battle-core-architecture.md` §4.2 밴드 B · §4.4 두 줄 계약 갱신 · §6 채널 지도
- 신규 `Tests/EditMode/DeploymentActivationSystemTests.cs`

## 구현

```csharp
public struct PendingDeployment : IComponentData
{
    public byte stage;        // 0 = InFlight(시계 없음 · 기본값) · 1 = Deploying
    public float remaining;   // Deploying 에서만 의미. 배틀 시간(초)
    public const byte InFlight = 0, Deploying = 1;
}
```
- 12곳의 `WithNone<PendingDeployment>` 는 무수정(데이터 컴포넌트도 같은 쿼리 술어). 기존 테스트의 `AddComponent<PendingDeployment>()` 는 기본값 InFlight = 「배제」 그대로.

`DeploymentActivationSystem`(ISystem, Units, **`[UpdateAfter(typeof(BossPeriodicTriggerSystem))]`** — critic M-5: 그 시스템 헤더가 「속성이 없으면
1프레임 지연이 빌드마다 달라진다」고 경고한다. After 로 못박아 「이번 틱 활성화 → 다음 틱 배치 스킬」이 항상 성립):
```
foreach (pending RW, entity) WithAll<DefenderUnitTag>:
    bool dead = HasComponent<DeadTag>(entity);
    if (!dead && pending.stage != Deploying) continue;
    if (!dead) { pending.remaining -= dt; if (pending.remaining > 0f) continue; }
    ecb.RemoveComponent<PendingDeployment>(entity);
    if (dead) continue;                                   // 시체는 배치 스킬도 활성화 이벤트도 없다(비행 중 사망 포함)
    if (HasBuffer<DcTriggerSlot>(entity)) ecb.AddComponent<JustDeployed>(entity);   // 브리지 MarkJustDeployedForRules 의 조건 그대로
    activated.Enqueue(new DefenderActivatedEvent { entity });
ecb.Playback
```
- 순수 부분: 감산·전이 판정은 자명한 두 줄이라 별도 함수로 빼지 않는다(제약 10 예외 조항).
- **모션 0 유닛의 활성화 프레임**: `Land` 는 브리지 `Update`/코루틴(둘 다 `SimulationSystemGroup` 앞 — `BattleBridge.cs:3344` 순서 주석)에서
  일어나므로 **같은 프레임의 sim 틱**에 `remaining(0) - dt ≤ 0` 으로 활성화된다. 테스트는 「Land 뒤 그룹 1회 Update 안에 활성화」로 단언(프레임
  동일성이 아니라 ≤ 1틱).
- dt = `SystemAPI.Time.DeltaTime`(그룹 RateManager 가 배틀 스케일 적용 · 정지면 그룹이 쉰다). `_running` 과 무관하게 돈다(`BattleScaledRateManager`
  는 `BattleTimeScale` 부재 시 1) — 배치 단계에도 시계가 간다.

`DefenderActivatedEventsSingleton`: `NativeQueue<DefenderActivatedEvent>` — Units 생산 · 브리지 드레인(unit 2). ⚠ 생성은 **`BeginPlacement`**(StartBattle 이
아니다 — 배치는 그 전에 일어난다, critic H-1). 해제는 기존 채널 관용구.

## 완료 기준

- EditMode: InFlight 는 시스템만으론 영원히 남는다 · Deploying 0.5s 는 dt 0.1 ×5 뒤 제거+이벤트 1건 · `DcTriggerSlot` 없는 유닛엔 `JustDeployed` 안 붙음 · `DeadTag` 유닛은 pending 만 제거되고 이벤트·`JustDeployed` 없음 · remaining 0 은 1틱 안에 활성화.
- 시스템 순서 덤프(`Wassup/Battle/Sim Order/Dump`)에서 `BossPeriodicTrigger` 뒤에 있음을 확인.
- `AttackSystemUnifiedLoopTests.PendingDeployment_Excludes_Attacker_From_Loop` 무수정 초록.
- CLAUDE.md·아키 문서 갱신 같은 커밋.
