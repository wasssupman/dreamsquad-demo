# [DRAFT — 리뷰용] skill-layer-unification — 스킬의 단일 레이어화

> 상태: **초안. 미승인·미착수.** 이 문서는 spec critic 리뷰의 대상이며 아직 `docs/spec/` 에 없다.
> 선행 spec: `docs/spec/skill-fire-dispatch/`(rev 4, 홀드) 를 **흡수**하는 것을 전제한다.

## 0. 끝점 (이 리팩터가 끝났을 때의 상태)

**이 게임의 모든 스킬 — 보스 스킬 · 방어유닛 배치 스킬 · 특수 스킬 — 이 하나의 레이어 위에 있다.**
스킬을 만든다는 것은 `ISkill` concrete 하나를 쓰는 일이고, 누가 그것을 `Execute` 하든
그 호출자가 그 스킬의 소유자가 된다. 보스가 쓰던 `BossLeap` 을 잡몹이 `Execute` 하면
코드 변경 없이 동작한다. 방어유닛이 그것을 장착해도 마찬가지다.

이 끝점에 도달하지 못하는 부분 집합이 남는다면, 그 예외는 **구조적 이유가 명문화**되어야 하며
"아직 안 옮겼다"는 예외가 아니다.

## 1. 검증 질문

> **보스 스킬 하나(`BossLeap`)를 잡몹 에셋에 슬롯 한 줄로 장착했을 때, 코드 0줄로 동작하는가?**
> 그리고 **방어유닛 배치 스킬과 보스 스킬이 같은 `ISkill` 목록에 섞여 있는가?**
> 부수 질문: 스킬 하나의 동작을 ECS 월드 없이 단위 테스트할 수 있는가?

## 2. 핵심 제약 (사용자 지시, 2026-08-24)

**`ISkill` 과 그 concrete 는 ECS 를 직접 참조하는 경로가 절대 없어야 한다.**
`Entity` · `EntityManager` · `SystemAPI` · `DynamicBuffer` · `NativeQueue` · `IComponentData` —
어느 것도 도메인 계층에 등장하지 않는다. 스킬은 **별도의 프로토콜**을 통해 각 모듈·아키텍처와
필요한 것을 주고받는다.

## 3. 구조

```
[도메인]  ISkill / concrete            ECS·Unity 참조 0. 값 타입과 SimEntityId(int) 만.
              │  질의 ↓        ↑ 의도
[포트]    ISkillContext                동사 ~20개. 인터페이스 하나.
              │
[어댑터]  EcsSkillContext              현재. SystemAPI·큐·룩업이 여기서만 산다.
          SimSkillContext              M1 이후. 같은 포트, 다른 구현.
          TestSkillContext             페이크. 스킬 단위 테스트가 ECS 월드 없이 돈다.
```

시그니처(안):
```csharp
public interface ISkill {
    SkillId Id { get; }
    void Execute(SimEntityId caster, in SkillParams p, ISkillContext ctx);
}
```

concrete 는 필요한 모듈만 조합한다:
```csharp
sealed class BossLeapSkill : ISkill {
    public void Execute(SimEntityId caster, in SkillParams p, ISkillContext ctx) {
        var dest = ctx.DensestOpponentCluster(caster, p.SearchRadius, p.RingMax);
        if (!dest.HasValue) return;
        ctx.Emit(new TeleportIntent(caster, dest.Value));
        ctx.Emit(new AreaDamageIntent(caster, dest.Value, p.SlamTiles, p.SlamDamage));
        ctx.Emit(new VisualIntent(VisualKind.LeapArc, caster, dest.Value));
    }
}
```

**호출자 = 소유자**: `caster` 가 인자다. concrete 안에 진영도 host 종류도 없다.

### 감지/실행 분리

```
감지 (Burst ISystem · 분산 유지 — 사건은 그게 나는 곳에서 난다)
   slot.trigger 매칭 → SkillFiredEvent{ caster, skillId, slotIndex } enqueue
        ↓ 같은 프레임 드레인 ([UpdateBefore] 선례: HazardCastSystem)
디스패치 (managed · 단일 지점)
   registry[skillId].Execute(caster, in params, ctx)
```

감지를 통합하지 않는 이유: 사건은 원래 그 시스템에서 난다(`AttackN`→`AttackSystem`,
`OnDeath`→`UnitLifecycleSystem`, …). 통합하면 매 프레임 전 유닛을 다시 훑게 된다.
**통합 대상은 어휘와 실행이지 감지가 아니다.**

## 4. 실측 근거 (2026-08-24, 코드 직접 검산)

| 항목 | 실측 |
|---|---|
| `DcPayloadKind` | 26종 (0=None … 25=RecallAttachedToFront) |
| `DcTriggerKind` | 10종 (None·AttackN·OnDamagedN·OnDeath·PeriodicTimer·HealthThreshold·OnKill·OnShieldBreak·OnRetire·OnPlace) |
| payload arm 실행 지점 | **6곳** — `BattleBridge.cs`(28) · `BattleBridge.Dreamcatcher.cs`(18) · `AttackSystem`(10) · `DamageApplicationSystem`(7) · `BossPeriodicTriggerSystem`(6) · `HealthThresholdSystem`(5) |
| 진영 리터럴 | **56개** (`AttackUnitTag` 29 + `DefenderUnitTag` 27), 위 6파일 분포 |
| `OpponentsOf`/`AlliesOf` 헬퍼 | **없음** |
| arm 보유 ECS 시스템의 Burst | **5개 전부 `[BurstCompile]`** |
| 레거시 배치 스킬 | `OnPlaceEffectType` 11종 중 **살아있는 arm 9종 / 에셋 12개**, 분기는 `BattleBridge.cs:5393~5590` if/else 체인 |
| `BattleBridge.cs` | 10,262줄 |
| 배치 스킬 PlayMode 커버리지 | **9종 중 3종** (`DotNearby`·`ApplyStackNearby`·`ForwardProjectile`) |
| 이전 대상 12행 중 동작 골든 | **5행** (궁극기·도약×2·채찍질·경계자폭 무보호) |
| 트리거 화이트리스트 | `EnemyTriggerArmed` = `PeriodicTimer\|HealthThreshold\|AttackN` / `DefenderTriggerArmed` = `OnPlace` |
| `SimEntityId` | **존재**(M0 unit 1). 부착 = 타겟 후보 + 투사체. **미부착 = 장판 캐리어·픽업·요청 캐리어·싱글턴**. 발급 카운터가 아직 `BattleBridge` 필드 |
| 골든 하네스 | `LegacyTraceV0` + `LegacyTraceRecorder` + `Tests/Golden` + `SimGoldenMenu` (M0 완료 2026-08-22) |
| `battle-sim-extraction` | M0 완료 · **M1 착수 대기**. 계약: sim = 순수 관리 C# **Burst-off** |

**이미 해소된 것 2가지 (백로그가 stale)**:
- `BakeNightmareMechanics` 는 `tier == EnemyTier.Boss` 로 갈린다 — 스킬 보유가 `BossTag` 를 강제하지 않는다. 잡몹도 능동 스킬 가능.
- `BakeUnitMechanics(hostIsEnemy:…)` 는 이미 진영 중립 — 저작→슬롯 경로는 적/방어유닛 공용.

**쓰기 프로토콜이 이미 반쯤 존재한다**: arm 의 쓰기가 전부 «의도를 남기는» 형태다
(`StatModifierApplyEvent`·`ProjectileHitEvent`·`EnemyCcEvent`·`ShieldGrantedEvent`·
`AggroAcquireEvent` enqueue, `IncomingShield`/`EmitterInstance` 인박스 append).
대상 상태를 직접 바꾸는 arm 이 없다 — 맥락 경계 규칙(CLAUDE.md 제약 2)의 부산물.

**읽기(질의) 쪽 실측 동사**: `Position(id)` · `Facing(id)` · `Opponents/Allies(caster, radius)`
(DeadTag·PendingDeployment 필터 포함) · `Stat(id, which)`(range·attackTargetCount·aggroCapacity) ·
`Has(id, condition)`. 20개 미만으로 추정 — **unit 0 이 확정한다.**

## 5. 계약 (안)

1. **도메인 계층은 ECS 를 모른다.** `ISkill`/concrete 에 `Entity`·`SystemAPI`·`DynamicBuffer`·
   `NativeQueue`·`IComponentData` 등장 금지. 위반은 컴파일 게이트로 막는다(asmdef 분리 검토).
2. **핸들은 `SimEntityId`.** `Entity` 는 포트 경계를 넘지 않는다.
3. **쓰기는 의도 방출.** concrete 는 상태를 바꾸지 않고 intent 를 `ctx.Emit` 한다.
   적용 시점·순서는 어댑터와 소유 맥락이 정한다.
4. **호출자 = 소유자.** `caster` 는 인자다. concrete 는 진영·host 종류를 갖지 않는다.
   모듈이 caster 상대적으로 답한다(`Opponents(caster, r)`).
5. **무상태.** concrete 는 필드를 갖지 않는다. 진행형 상태(도약 비행 등)는 컴포넌트+시스템 소유 —
   스킬은 개시와 수치까지다.
6. **감지는 분산 유지.** 통합 대상은 어휘·실행이며 트리거 감지는 사건이 나는 시스템에 남는다.
7. **프로토콜 표면은 «도출»한다.** unit 0 이 21행 arm 전수에서 동사를 추출한다. 상상으로 정의하지 않는다.
8. **허용된 유일한 외부 타입 = `Unity.Mathematics` 값 타입** (검토 대상 — 아래 미결정 ①).
9. **시트 무손실**: 카드 mechanics 는 시트가 덮는다(`OverlayMechanics`). 카드 authoring 무변경 + 어댑터.
10. **골든 없이 이전하지 않는다.** 무보호 4종은 이전 전에 특성화 테스트를 세운다.

## 6. 작업 단위 (안)

| unit | 하는 일 | 코드 |
|---|---|---|
| 0 | **프로토콜 표면 도출** — 21행 arm 전수에서 질의·의도 동사 추출, 광역 구조 참조 표시, `ISkill`/`ISkillContext` 시그니처 고정 | **0줄** |
| 1 | `SimEntityId` 갭(발급 싱글턴 승격 + 장판 캐리어 부착) + **진영 상대화 56곳** | 동작 무변경, 골든이 증인 |
| 2 | `ISkillContext` + `EcsSkillContext` + `TestSkillContext` + 레지스트리 + `SkillFiredEvent` seam + **첫 concrete 1개** | |
| 3 | 그물 — `TestSkillContext` 기반 스킬 단위 테스트로 무보호 4종 커버 | |
| 4 | 보스 10행 concrete 이전 (`BossLeap` 포함) | |
| 5 | 트리거 화이트리스트 2개 철거 — 트리거 × 주체 조합 개방 | |
| 6 | 배치 스킬 — 레거시 `OnPlaceEffectType` 9 arm → concrete. enum 11종·브리지 200줄·flat 필드 7개 사망 | |
| 7 | 카드 어댑터(시트 무손실) + 캐스트 계열(`HazardCastSystem`·`ShieldCastSystem`·볼리·봄런처) | |
| 8 | 인계 | |

## 7. 레이어 밖으로 두는 것 (예외 후보 — 구조적 근거 필요)

- **공격 출력 수식자** — `HeavyStrike`·`NextAttackDoubleFire`·게이트 합성. *발동한 그 공격 자신의
  출력*을 바꿔서 pre-scan 합성 불변식이 공격 계산 내부 거주를 강제한다. `Execute` 로 표현 불가.
- **기믹 bespoke** — `FatigueAccrualSystem`·`LastRunSystem`·`PickupSpawn/ConsumeSystem`.
  파킹 설계(`docs/plans/2026-07-15-effect-trigger-unification-design.md`)는 앞 둘은 rule 이관 가능,
  `PickupSpawn`(월드 오브젝트 주기 스폰)과 시즌 SO 게이팅은 **범위 밖**으로 판정했다.
- **분열(`SplitOnDeath`)** — 슬롯을 쓰지 않고 브리지 킬 드레인이 SO 를 직독한다.

⚠ 끝점(§0)이 "모든 보스/배치/특수 스킬"이므로 **이 예외 목록이 정당한지가 리뷰의 핵심 쟁점이다.**

## 8. 순서·의존

- `battle-sim-extraction` **M1 착수 전**에 이 spec 을 끝내는 것을 제안한다. 근거: (a) M1 은 82파일
  재배선 + Burst 상실 성능 게이트를 낀 대공사라 스킬 어휘 재설계까지 묶으면 실패 시 원인이 안 갈린다.
  (b) 감지/실행 분리와 포트 도입은 M1 이 어차피 해야 하는 일이라 **버려지는 코드가 0**이다.
  (c) `SimSkillContext` 는 M1 이 같은 포트에 어댑터만 갈아끼우면 된다.
- `skill-fire-dispatch` 계약 6(진영 상대화 = 후속)·12(M0 앞)는 **폐기**한다. 사용자의 끝점이 그 전제를 뒤집었다.

## 9. 미결정

① **수학 타입** — `Unity.Mathematics`(`float3`)를 도메인에 남길지. 순수 값 타입이지만 Unity 패키지다.
   남기면 M1 의 "엔진-프리"와 충돌 소지, 벗기면 자체 벡터 타입 신설 비용.
② **광역 구조 질의** — arm 이 `FlowFieldSingleton`(경로장)을 읽는다. 값으로 못 넘기므로 질의로 감싸야
   하는데(`ctx.NextStepToward(id, goal)`), 이런 «큰 것을 읽는» arm 이 몇 개인지 unit 0 이 세야 한다.
③ **디스패치 비용** — 발동당 인터페이스 가상 호출 1회 + 큐 왕복. 발동 빈도가 초당 수 회 수준이라
   무시 가능하다는 게 가설이나 측정된 적 없다.
④ **asmdef 분리로 제약 1을 컴파일 게이트화할지** — 하면 강제되지만 어셈블리가 하나 는다.
