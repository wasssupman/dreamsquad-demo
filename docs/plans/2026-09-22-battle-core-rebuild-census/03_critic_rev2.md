# rev 2 트리거→발동 레이어 — 적대 리뷰

> 대상: `docs/plans/2026-09-22-battle-sim-rebuild-census/02_trigger_layer_rev2.md`
> 잣대: 현행 구현(코드 전수 확인) + census 6편. 기준 규율 = 「기획 내용 그대로 Mono 전환」.
> 모든 현행 주장에 파일:줄 포인터를 붙였다. 확인 못 한 것은 그렇다고 적었다.

---

## 판정 (한 줄)

rev 2 의 **6개념 골격은 옳지만 환원표가 실측과 어긋난다** — Squad 수명·기믹 시계·AreaBlast 통합·skillId 은퇴 네 행에서 플레이어가 겪는 규칙이 바뀌고, 세대 BFS 는 오늘 일어나지 않는 현상을 위한 기계이면서 켜는 순간 연쇄를 1틱으로 접는다.

---

## 깨지는 시나리오 (severity 순)

### C1. Squad 카드 수명이 「판 종료」가 아니라 **host 사망 ∪ 퇴근**이다

**현행 동작**
`ApplyDreamcatcherCardHosted`(`Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs:134-140`)가 `handle = _dcHandleCounter++`(≥1)을 발급한다. `ActiveDcEffect.handle` 주석(`:28-31`):

> handle ≥1 = hosted squad apply, revoked on host death. handle 0 = non-revocable match-long apply (드림스톤 로드아웃, ApplyPendingDreamstones — 설계상 영구).

회수는 `DreamcatcherHandController.cs:282` 가 `RecoverCardsHostedBy` 안에서 `bridge.RevokeDreamcatcherEffects(handle)` 로 부르고, 그 함수는 **사망(`OnDefenderDied`)과 퇴근(`retired: true`) 둘 다**에서 불린다. `retired` 플래그는 「앞당김(인수인계) 여부」만 가르고 revoke 여부는 안 가른다(`:270-283`).

**rev 2 에서의 동작**
환원표: `Squad 카드 | OnPlace | Any(defender, 클래스 필터) + 판 시작 1회 | — | ApplyStat | **Match** | **판 종료**`.

**기획 규칙이 바뀌나** — **예.** 스쿼드 버프를 얹은 host 를 잃어도 버프가 판 끝까지 유지된다. 「버프를 유지하려면 그 유닛을 지켜라」가 「한 번 내면 공짜」가 된다.

**부수 오류(같은 행)** — 적용 시점도 「판 시작 1회」가 아니다. `ApplyDreamcatcherCardInternal`(`:142-164`)이 **카드를 내는 그 호출에서** `_defenderByTile` 전원에 `EnqueueStatModifier` 하고, 그 뒤 배치분만 `ApplyActiveDcEffectsTo`(`:205-221`)가 상속한다. 카드는 전투 중 임의 시점에 나온다.

**수정안**
owner = host 유닛, lifetime = `소유자 소멸 ∪ 퇴근`(둘 다 rev 2 의 lifetime enum 에 이미 있다). 「판 시작 1회」 칸은 「부착 시점 전원 + 이후 배치분 상속」으로 고친다.

---

### C2. 「`AreaBlast` 통합」이 통행 층 게이트와 예고 시간을 조용히 뒤집는다

**현행 동작**
두 concrete 의 차이는 자리만이 아니다:

| 필드 | `SelfAreaBlastSkill`(Id 4) | `DeathSiteBlastSkill`(Id 20) |
|---|---|---|
| `Position` | `ctx.Position(caster.Unit)` | `p.EventPosition` |
| `OriginBodyRadius` | `caster.BodyRadius` | `p.EventBodyRadius` |
| `Duration` | **`0f` 하드코딩(즉발)** | `p.Duration`(예고 = 퇴근 운석) |
| `TargetTraversalLayers` | `p.TargetTraversalLayers`(시전자 공격 층) | **`0`(무제한)** |

`Assets/_Project/Scripts/Skills/Concrete/DeathSiteBlastSkill.cs:54-58` 이 그 `0` 에 이유를 적어 뒀다:

> 레거시는 층을 안 실었다(= 무제한). 여기서 킬러의 공격 층을 실으면 지상 전용 킬러의 시체폭발이 비행 적을 더는 못 때린다 — **그건 사양 변경이다.** 형제(잿불)는 반대로 층을 **실어야** 했다. 무회귀 쪽을 택한다: 층 게이트가 필요하면 별도 결정으로 연다.

대응 라인: `SelfAreaBlastSkill.cs:36` (`TargetTraversalLayers = p.TargetTraversalLayers`) · `SelfAreaBlastSkill.cs:32` (`Duration = 0f`).

**rev 2 에서의 동작**
§3: 「payload → concrete 1:1. 트리거별 분기는 **이벤트의 Site** 가 흡수」. §1 환원표 각주에서 `AreaBlast` 가 두 concrete 를 대체한다고 명시.

**기획 규칙이 바뀌나** — **예.** `Site{pos, originBody}` 가 나르는 것은 자리와 몸뿐이다. **층과 예고는 이벤트에 없다.** 통합하면 둘 중 하나로 통일되고, 어느 쪽이든 비행 적 상대 전투가 달라진다:
- 층을 실으면 → 지상 전용 유닛의 시체폭발이 비행 적을 못 때린다(현행 대비 약화).
- `0` 으로 통일하면 → 궁지폭발·진동갑주가 하늘의 적을 때린다(현행 대비 강화, `unit 2a` 의 그물이 잡았던 결함의 부활).

예고 축도 같다: `OnDamagedN × SelfTileAoe` 에 duration 이 저작돼 있으면 현행은 무시하는데(하드코딩 0), 통합하면 지연 낙하가 된다.

**수정안**
통합하려면 이벤트가 **「통행 층」과 「예고 시간」도 값으로** 날라야 하고, 그러면 「감지자별로 무엇을 싣나」 표가 다시 필요하다 — 라우팅 표를 이름만 바꾼 것이다. concrete 둘을 유지하는 쪽이 싸다.

---

### C3. skillId 은퇴가 **부착 범위 프리뷰**를 깬다

**현행 동작**
`Assets/_Project/Scripts/Core/Dreamcatcher/DcRangeCatalog.cs:120-135` `ResolveCard` 가 `DcSkillRouting.SkillIdFor(m.trigger.kind, m.payload.kind)`(`:127`)를 부른다. 그리고 `Resolve(skillId, tileRange, trigger)`(`:60-112`)는 **같은 concrete** 에 대해 트리거별로 다른 형을 돌려준다:

```
DeathSiteBlast × OnDeath   → RangeMetric.SelfArea  (몸에서 나오는 것 — 죽은 그 유닛이 터진다)   :96-101
DeathSiteBlast × OnRetire  → RangeMetric.CellArea  (자리에 떨어지는 것 — 비워진 칸에 운석)      :102-107
DeathSiteBlast × OnKill    → None                  (부착 시점에 죽일 적의 자리를 모른다)        :108-111
```

이건 제약 13 의 「효과의 형」이고, **판정 시점은 드래그 중(부착 전)** 이다. 이벤트도 Site 도 아직 존재하지 않는다.

**rev 2 에서의 동작**
§3: 「`skillId`·`DcSkillRouting` 간접층 은퇴(Burst 근거 소멸). 트리거별 분기는 **이벤트의 Site** 가 흡수」. 프리뷰 언급 없음.

**기획 규칙이 바뀌나** — **예.** 링이 안 그려지거나 틀린 반경으로 그려진다. `DcRangeCatalog` 자기 주석(`:31-33`)이 그 실패를 이미 이름 붙여 뒀다: *「화면이 판정보다 관대하면 그게 곧 «규칙을 틀리게 가르친다»다」*. `dc-attach-range-preview` 는 2026-09-03 완료된 라이브 기능이다.

**수정안**
은퇴 대상을 **「Burst 용 int 인코딩」으로 좁힌다.** 정적 `(trigger, payload) → (concrete, 형)` 표는 프리뷰가 요구하므로 반드시 남아야 한다. 이름을 바꿔도 되지만 표는 남는다.

**부수 확인(rev 2 에 유리)** — `DcApplicability.EvaluateMechanic`(`Core/Dreamcatcher/DcApplicability.cs:109-…`)은 `SkillIdFor` 를 **부르지 않는다.** host 프로파일과 trigger/payload enum 만 본다. skillId 은퇴가 이 축은 건드리지 않는다.

---

### C4. 기믹의 주기는 판이 아니라 **유닛**이 소유한다

**현행 동작**
둘 다 per-unit lazy-attach 타이머다.

*번아웃 피로* — `Assets/_Project/Scripts/Battle/Effects/FatigueAccrualSystem.cs:36-56`
```
Pass 1: Query<DefenderUnitTag>().WithNone<FatigueAccrual>() → ecb.AddComponent(entity, new FatigueAccrual{ elapsed = 0f })
Pass 2: accrual.ValueRW.elapsed += dt;  while (elapsed >= config.fatigueInterval) { elapsed -= interval; stackQ.Enqueue(StackKind.Fatigue …) }
```
`FatigueAccrual` 는 `IComponentData{ float elapsed; }`(`FatigueAccrual.cs:8-11`) — **엔티티마다 하나**.

*온천 열기* — `HeatAccrualSystem.cs:42-105`, 같은 2-pass 형태에 더해
```
HeatAccrual { float elapsed; byte stacks; }      // HeatAccrual.cs:9-13
accrual.ValueRW.stacks++ (heatMaxStack 에서 멈춤)
HeatMath.Delta(stacks, flipThreshold, …)          // HeatMath.cs:12-24 — stacks 가 회복↔손실 반전을 결정
```

**rev 2 에서의 동작**
환원표: `기믹 4 | PeriodicTimer · OnDeath(any defender) · OnPickupConsumed · OnResignationThreshold | **Any** | 시즌 게이트 | 스택/힐/픽업 스폰/메테오 | **Match** | 판 종료`.

**기획 규칙이 바뀌나** — **예, 두 축에서.**

1. **위상(phase).** 지금은 각 유닛이 **자기 부착 시점부터** 주기를 센다(배치 직후 배치된 유닛은 배치+interval 에 첫 피로). Match 호스트의 전역 시계면 경계 직전에 배치된 유닛이 1틱 만에 피로 1 을 받는다. 온천은 더 크다 — 웨이브마다 스폰되는 적이 자기 스폰부터 회복 램프를 타는 것이 현행인데, 전역 시계면 램프가 어긋난다.
2. **per-subject 상태를 담을 자리가 없다.** Match 호스트 바인딩에는 `HeatAccrual.stacks` 같은 피해자별 카운터가 살 곳이 없다. 온천의 규칙 **전체**가 그 값 위에 있다(회복 ↔ 과열 반전). rev 2 의 Binding 은 `{event, subject, conditions[], effect, owner, lifetime, instanceId, seq}` 이고 per-subject 상태 축이 없다.

**추가 — 두 기믹의 subject 필터가 서로 다르다.**
| | 번아웃 | 온천 |
|---|---|---|
| 대상 | `WithAll<DefenderUnitTag>` | `WithAny<DefenderUnitTag, AttackUnitTag>` |
| 사망 제외 | **없음** | `WithNone<DeadTag>` |
| 배치중 제외 | **없음** | `WithNone<PendingDeployment>` |

「subject = Any」 하나로 접으면 반드시 한쪽이 바뀐다.

**수정안**
기믹도 **유닛 호스트 바인딩**으로(판 시작 + 스폰 시 부착, lifetime = 소유자 소멸). Match 호스트는 진짜 전역 주기에만 — 레드불 스폰 cadence(`PickupSpawnState.elapsed`, `PickupSpawnSystem.cs:57`)가 오늘 유일한 해당 사례다.

---

### H5. 세대 BFS 는 오늘 없는 현상을 위한 기계이고, 켜면 연쇄가 1틱으로 접힌다

**현행 동작**
`Assets/_Project/Scripts/Battle/Skills/SkillDispatchSystem.cs:138` 이 `int budget = queue.Count;` 로 시작 시점을 스냅샷한다. 주석(`:135-137`):

> **시작 시점 스냅샷 1회.** 드레인 중에 의도가 새 감지를 성사시키면(피해 intent → 같은 프레임 OnDamagedN) 재유입이 생긴다. 지금은 감지가 분산돼 **프레임 구조가 자연 차단기**인데, 통합 드레인이 그걸 잃는다.

그리고 concrete 는 `ctx.Emit(SimIntent)` 만 한다(계약 3) — `SkillFiredEvent` 를 직접 만들지 않는다. 생산 지점 13개소는 전부 감지자(시스템/브리지)다. **오늘 같은 드레인 안 재진입은 0건이다.** 재진입 가드(`_draining`, `:124-131`)도 브리지의 `RunImmediateSkills()` 경로만 상정한다.

인용된 라이브 연쇄(시체폭발 → OnKill → 잿불)는 드레인 재진입이 **아니라** 틱을 넘는 파이프라인이다:

```
Death seam (DamageApplication 뒤, UnitLifecycle 앞)   ← SkillDispatchSeams.cs:155-161
  → DeathSiteBlastSkill → SimIntent.SpawnProjectile (flightTime 0)
  → ProjectileSpawnRequest
  → [다음 틱] ProjectileMoveSystem  ([UpdateAfter(MovementSystem)] — ProjectileMoveSystem.cs:25)
  → ProjectileHitSystem            ([UpdateAfter(ProjectileMoveSystem)] — ProjectileHitSystem.cs:23)
  → IncomingDamage
  → DamageApplicationSystem → OnKill 감지 → Death seam
```
최소 2틱, 실제로는 이동→투사체→공격→피해 밴드 순서 때문에 링크당 1틱이다.

**rev 2 에서의 동작**
§2 규칙 3: 「발동 중 생긴 사건은 `generation+1` 큐 → **같은 seam 창 안에서** 세대 순으로 소진. 깊이 8 초과는 loud 폐기. **라이브 최장 연쇄는 3**(시체폭발→OnKill→잿불)」.

**기획 규칙이 바뀌나** — BFS 가 intent 경유 연쇄까지 덮으면 **예.**
- 연쇄 전체가 한 틱에 끝난다 → 그 사이 적이 움직이지 않고 다른 피해도 안 들어온다 → **누가 반경에 드는지가 달라진다.**
- 연출에서 리플(폭발이 차례로 번지는 그림)이 사라지고 한 프레임에 전부 터진다.

그리고 근거 자체가 어긋나 있다: **깊이 예산 8 은 라이브 측정값이 아니다.** 측정된 연쇄 3 은 이 기계를 타지 않는 종류다(틱 경계를 넘는 파이프라인). 이 기계를 타는 연쇄의 현행 최댓값은 **0** 이다.

**수정안**
BFS 를 **바인딩 → 바인딩 직접 발화**(같은 드레인 안에서 이벤트를 다시 낳는 경우)로 한정하고, intent 를 거치는 것은 다음 틱 phase 로 남긴다. 깊이 예산은 그 한정된 정의 위에서 다시 산정한다. 「같은 seam 창 안 소진」을 intent 연쇄까지 확대할 거면 **사용자 판정 대상**으로 올린다.

---

### H6. `PlacementAura` 는 효과가 둘이고 회수가 **비대칭**이다

**현행 동작**
`RegisterPlacementAura`(`BattleBridge.Dreamcatcher.cs:1337-1354`)는 한 handle 아래 **둘**을 등록한다:
```csharp
if (asPercent > 0f)  _activeDcEffects.Add(new ActiveDcEffect { stat = StatKind.AttackSpeedMul, mult = 1f + asPercent/100f, handle, origin = Dreamcatcher, axis });
if (warmupSec > 0f)  _activePlacementSleeps.Add((handle, axis, warmupSec));
```
주석(`:1334-1336`): *「`_defenderByTile` 루프 없음 → 현재 유닛/host 미적용, `ApplyActiveDcEffectsTo`(신규 배치)에서만 상속」*. Sleep 은 `ApplyPlacementSleep`(`:226-234`)이 `CcKind.Sleep` 으로 부여한다.

회수(`RevokeDreamcatcherEffects`, `:192-203`)는 **비대칭**이다:
- 스탯: `_defenderByTile` 전원을 돌며 원래 op 를 재도출해 그 op 의 항등을 재발행 → **이미 버프받은 유닛에서 소급 중화**(`:193-195`).
- Sleep: `_activePlacementSleeps.RemoveAt(i)`(`:201-202`) — **등록부 제거만.** 이미 걸린 잠은 풀지 않는다.

**rev 2 에서의 동작**
환원표: `PlacementAura | OnPlace | Any(defender) | host 생존 | **ApplyCc(EventTarget)** | host 유닛 | 소유자 소멸`.

**기획 규칙이 바뀌나** — **예.**
1. 공속 버프(스탯) 절반이 어휘에서 통째로 사라진다. effect 가 `ApplyCc` 하나다.
2. lifetime 값 하나에 두 뜻이 겹친다 — 스탯은 「소급 회수」, Sleep 은 「미래 차단」. 「소유자 소멸」로 통일하면 어느 한쪽이 바뀐다.

**수정안**
한 바인딩이 아니라 **바인딩 둘**(같은 owner, 같은 event, 다른 effect)로 두고, lifetime 의미를 「만료 시 소급 회수 여부」축과 분리한다. rev 2 §4-1 의 「중단 정책 표」와 같은 결이다.

---

### H7. `SplitOnDeath` 를 `OnDeath` 바인딩으로 옮기면 분열 조건이 넓어진다

**현행 동작**
`Assets/_Project/Scripts/Bridge/BattleBridge.cs:11048-11050` 주석:

> 죽은 적의 SO 가 `OnDeath × SplitOnDeath` 를 선언했으면 그 자리에 자식을 스폰한다.
> **호출처 1곳(`DrainEnemyKilledEvents`) — 유출 경로는 이 이벤트를 안 타므로 «체력 소진 시에만 분열» 이 코드 추가 없이 성립한다.**

bake 는 `SplitOnDeath` 조합에서 **슬롯 엔트리를 만들지 않는다**(`BattleBridge.cs:10178-10195` — 버퍼는 붙이되 엔트리 skip, 검증만 loud). 실행은 브리지 킬 드레인이 SO 를 직독한다.

반면 `OnDeath` 의 감지자는 `UnitLifecycleSystem` 이고, `SkillDispatchSeams.cs:165-168` 가 그 성질을 못박는다:

> 자기 죽음의 정본 감지 지점은 `UnitLifecycleSystem` 이다. 거기가 **모든** 사망 경로(피해·치명 타이머·순찰 수명)가 합류하는 유일한 지점이라, ④ 로 앞당기면 피해로 죽은 경우만 작별 선물이 나오고 나머지는 조용히 빠진다.

**rev 2 에서의 동작**
환원표: `SplitOnDeath(다른 배선이던 것) | OnDeath | Self | — | SpawnUnits ★(intent 신설) | 유닛 | 소유자 소멸`.

**기획 규칙이 바뀌나** — **예.** 유출·순찰 수명 종료·비귀속 피해로 사라지는 개체가 분열하기 시작한다. 현행의 제한은 「코드 추가 없이 성립」한 것이라 **흔적이 없다** — grep 으로 못 잡는 종류다.

**두 번째 함정 — `Site{pos}` 를 그대로 쓰면 안 된다.**
`BattleBridge.cs:11105-11112` 가 자식 배치 기준점을 **부모의 셀 중심**으로 양자화한다:

> 기준점은 **부모의 셀 중심**이다. 부모의 연속 좌표에 오프셋을 더하면 안 된다 — `MovementCellTrim` 이 유닛을 셀 중심에서 `0.5·tileSize − 1e-3` 까지 벗어나게 허용하므로 거기에 0.25 를 더하면 자식이 **인접 셀**에 태어난다. 그 셀이 골이면 `MovementSystem` 이 다음 틱에 `PastGoalTag` 를 찍어 «처치했는데 유출» 이 된다. (2026-08-12 ECS 리뷰 H1)

rev 2 의 `Site{pos}` 는 연속 좌표다(`DeathSiteBlastSkill` 이 그대로 쓰는 값). 신설할 `SpawnUnits` intent 는 **양자화된 칸**을 받아야 한다.

**기타 보존해야 할 규칙** — 첫 `SplitOnDeath` 슬롯만 실행(`:11114`), `MaxSplitChildren = 8` 클램프(`:11046`), 자기순환 런타임 차단(`:11078-11084`), RNG 금지·인덱스 기반 결정 배치(`:11092`).

---

### H8. `SelfStatBuff` 는 payload 1:1 이 아니다 — **출처가 둘**이다

**현행 동작**
`Assets/_Project/Scripts/Skills/Concrete/SelfStatBuffSkill.cs:47-62` — 공용 base `SelfStatBuffSkillBase` 에 파생 둘:
```csharp
sealed class SelfStatBuffSkill      : Id 18, ModifierOrigin = SkillModifierOrigin.Dreamcatcher
sealed class ThresholdSelfBuffSkill : Id 22, ModifierOrigin = SkillModifierOrigin.HealthThreshold
```
차이는 `SkillModifierOrigin` **하나뿐**이고, `:55-56` 주석이 그 값의 무게를 적는다:

> 체력 경계에서 켜지는 버프(빈사폭주) — **출처가 다르다.** 그 값은 「빈사에서 켜졌다」는 뜻이고, 드림캐쳐로 바꾸면 **없던 오라가 켜진다**(`ModifierAuraClassifier` 가 그 출처만 센다).

라우팅 분기: `DcSkillRouting.cs:67-68` (`HealthThreshold × SelfStatBuff → ThresholdSelfBuffSkill`), 그 외는 `ForPayload` → `SelfStatBuffSkill`.

**rev 2 에서의 동작**
§3: 「payload → concrete 1:1. 트리거별 분기는 이벤트의 Site 가 흡수」.

**기획 규칙이 바뀌나** — **예(상태 연출).** Origin 은 Site 가 아니고 이벤트에 없다. 통합하면 광란에 빈사 오라가 켜지거나 빈사폭주의 오라가 꺼진다.

**수정안** — 이벤트가 `origin` 을 값으로 나르거나, 주장을 「**payload × origin** 1:1」로 고친다. 후자면 「파생 둘 + 공용 base」라는 현행 형태가 그대로 답이다.

---

### M9. `trigger == None` 카드에 `N회=1` 을 주면 표식 카드가 즉시 손패로 돌아온다

**현행 동작**
살찌운 제물(`BountyMark`, concrete Id 27)은 host 가 **적**이고 handle 0(무회수)이다. 카드가 손패 큐로 돌아오는 시점은 **표식된 적이 사라질 때**다:
- `BattleBridge.Dreamcatcher.cs:74-77` `NotifyEnemyGoneIfMarked(enemy)` → `EnemyGone` 이벤트(처치/유출 드레인에서 호출)
- `DreamcatcherHandController.cs:330` `private void OnEnemyGone(Entity entity) => RecoverCardsHostedBy(entity, retired: false);`
- 같은 줄 주석: *「표식은 handle 0(무회수)이라 revoke 호출도 없다 — 큐 복귀만」*

**rev 2 에서의 동작**
환원표: `trigger == None 카드 3 | OnAttach | Self | — | concrete | host | **N회=1**`.
§1: 「수명 = … 만료는 `OnDetach` 사건을 낳는다」.

**기획 규칙이 바뀌나** — **예.** 발동 직후 N 이 소진돼 만료 → `OnDetach` → 카드가 **즉시** 손패로 돌아온다. 현행은 표식한 적이 죽거나 유출될 때까지 손패에 없다. 「적 하나를 찍어 두고 그동안 카드를 못 쓴다」는 비용이 사라진다.

**수정안** — 「발동 횟수 상한」과 「부착 수명」은 **다른 축**이다. 한 `lifetime` 필드로 담을 수 없다. 두 필드로 분리하거나, 이 셋은 `lifetime = 소유자 소멸` + `fireCap = 1` 로 둔다.

**같은 행의 부수 항목** — 호접몽·마지막 불꽃은 `DcApplicability` 가 `DuplicateState` 로 거절한다(`DcApplicability.cs:158-163`, `host.hasLethalTimer` / `host.hasDreamCocoon`). 이건 preflight 가 라이브 host 상태를 읽는 것이고 rev 2 의 Condition 어휘(아래 M10)로는 표현 못 한다.

---

### M10. 레드불 소비 조건이 rev 2 의 Condition 어휘 밖이다

**현행 동작**
`PickupConsumeSystem.cs:88-89`:
```csharp
if (em.HasComponent<LastRun>(unit))
    return; // 라스트런 중 — 재소비 락(픽업 잔존)
```
주석(`:77-79`): *「라스트런 진행 중인 유닛은 소비하지 않는다 — 밟아도 픽업은 보드에 남아 만료되거나 다른 유닛이 먹는다. crash 로 비용을 치른 뒤에야 재버프 가능(재소비로 타이머 리셋 → crash 무한 회피하던 문제 차단)」*. 리뷰 #2 에서 나온 수정이고 **load-bearing** 이다.

소비 자체는 사건이 아니라 **매 프레임 공간 폴링**이다: 픽업을 `cell → entity` 해시맵으로 색인한 뒤 전 defender(`DefenderFootprint.anchor`)와 전 enemy(`LocalTransform` → 셀)를 순회해 일치를 찾는다(`:31-68`).

**rev 2 에서의 동작**
§1: 「`Condition` = 닫힌 술어 집합(**현행 `DcGateKind` × `DcGateSubject`**)의 AND 배열」.

실제 어휘는 `DcMechanic.cs:273-278`:
```csharp
public enum DcGateKind    { None, HpBelow }
public enum DcGateSubject { Self, EventTarget }
```
개방 조합은 **2개뿐**이다(`DcTrigger.GateComboSupported:88-95`) — `OnDamagedN × Self`, `AttackN × EventTarget`. 나머지 `gate != None` 은 bake 가 loud 거절.

**기획 규칙이 바뀌나** — 표현 자체가 불가능하다. 「이미 라스트런 중인가」·「배치 중인가」·「죽었나」·「defender 인가」 전부 어휘 밖이다. rev 2 는 Condition 을 현행 게이트 어휘로 못박아 **스스로 이 확장을 막았다.**

**수정안** — Condition 어휘를 host 상태 술어까지 넓히거나, **subject 필터를 Condition 과 분리된 1급 축**으로 둔다(오늘 `WithAll/WithNone` 이 하던 일). 후자가 C4 의 두 기믹 필터 차이도 함께 푼다.

---

### M11. 「즉시 반영」이 실제로 무엇을 바꾸는지 특정되지 않았다

**현행 동작**
`Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierApplySystem.cs:10-18` — `[UpdateBefore(StatModifierTickSystem)]` + `[UpdateBefore(MovementSystem)]`, 주석:

> **이 핀이 모디파이어 클러스터 전체의 1프레임 지연을 고정한다.** 생산자 11개 중 8개가 이 시스템보다 **뒤**에 있어(공격·피해·착탄·임계 등) 그들의 모디파이어는 다음 프레임에 반영된다.

**rev 2 / 종합에서의 동작**
종합 §2-B: 「모디파이어 1프레임 지연 → **즉시 반영**(틱 안에서 적용 phase 가 이동·공격 앞) — 밸런스 미세 변동 수용」. 틱 표에서 적용은 **phase 1**.

**분석** — Attack·Death·Threshold·Lifecycle seam 은 전부 **phase 5** 다. 적용 phase 가 1 이면 그 8개는 **여전히 다음 틱**이다. 즉 「즉시 반영」 전환으로 실제로 바뀌는 것이 거의 없고, 약속된 「밸런스 미세 변동」도 일어나지 않는다.

실제로 뒤집히는 것은 하나다 — `FatigueAccrualSystem` 이 `[UpdateAfter(ModifierApplySystem)]`(`:18`)으로 그 1프레임 지연을 **명시적으로 박제**했고, 주석이 *「여기서 쌓은 피로도는 다음 프레임에 반영된다. 그 1프레임이 현행 동작이므로 그대로 박제한다」* 라고 적었다.

**실드는 이미 같은 프레임이다.** `DamageApplicationSystem.cs:179-185` 가 흡수 **직전**에 `IncomingShield` 를 merge 하고 `grants.Clear()` 한다. `SkillIntent.cs:28` 의 「⚠ 다음 프레임 드레인이 의도」는 **생산자가 `DamageApplicationSystem` 보다 뒤일 때만** 참이다. 주기 seam(`[UpdateBefore(ModifierApplySystem)]` → 이동/공격보다 앞)에서 나오는 마메모의 가호는 오늘도 같은 프레임에 흡수된다. 「다음 프레임이 의도」를 전역 사실로 적으면 다음 사람이 틀린 단서를 쫓는다.

**수정안** — 「밸런스 미세 변동 수용?」을 묻기 전에 **무엇이 실제로 바뀌는지 생산자별 목록**부터 낸다. 지금 문면으로는 바뀌는 것이 `FatigueAccrual` 하나다.

---

### L12. 사직서 임계는 edge 가 아니라 level 이고 한 틱 다중 barrage 가 사양이다

**현행 동작**
- 드랍(`ResignationDropSystem.cs:36-42`): `Query<DefenderFootprint>().WithAll<DeadTag, DefenderUnitTag>()` → 그 anchor 에 `Resignation` 스폰. **원인 불문**(주석 `:2-3`). 퇴근은 `DeadTag` 를 안 달아 자연 제외된다(불변식 11).
- 임계(`ResignationThresholdSystem.cs:36-54`): 카운터가 아니라 **엔티티 수 질의** `_resignationQuery.CalculateEntityCount()`. `barrages = count / threshold`, `toDestroy = barrages * threshold`, barrage 를 그 배수만큼 enqueue. **한 프레임 다중 임계가 사양이다**(`:40`).

**rev 2 에서의 동작** — `OnResignationThreshold` 이벤트 + Match 호스트 바인딩.

**기획 규칙이 바뀌나** — 아니오(표현은 된다). 다만 **이벤트화가 비용을 줄이지 않는다** — 생산자가 같은 폴링을 해야 한다. rev 2 §2 의 「사건당 비용 = 자기 슬롯 + 전역 소수」가 이 축을 안 본다.

**미확정** — `ResignationDropSystem`(`[UpdateBefore(UnitLifecycleSystem)]`)과 `ResignationThresholdSystem`(`[UpdateBefore(StackModifierTickSystem)]`)의 상대 순서를 정렬기가 어떻게 푸는지 확인하지 못했다. 틱 표상 `StackModifierTick` 은 `ModifierApply` 뒤(이동 앞)이고 `UnitLifecycle` 은 피해 뒤이므로 임계가 드랍보다 **앞**일 가능성이 높고, 그러면 5번째 사직서의 barrage 는 **다음 틱**이다. 세대 BFS 가 이 1틱을 없앤다면 그것도 H5 와 같은 종류의 변화다.

---

## 표현 불가 또는 더 나쁜 구조

### 1. `CardBuffKind.CostRate` — 유닛 스탯이 아니라 **판 자원**이다
`MapDcBuff`(`BattleBridge.Dreamcatcher.cs:1362-1387`)에서 `CostRate` 는 case 가 **없고** `default` 로 떨어져 `return false` 한다. 주석(`:1379-1386`):

> `CardBuffKind.CostRate` has no entity/ECS stat (it scales `CostRuntime.RegenRateMultiplier`, a MonoBehaviour-side resource, not a StatModifier channel).

실제 소비: `GameManager.cs:422,478` → `CostRuntime.SetRegenRateMultiplier(ResolveCostRateMultiplier(squad))`, 리셋은 `DraftController.cs:123`.

rev 2 의 「Squad 카드 → `ApplyStat`」은 이 축을 표현하지 못한다. (오늘 라이브 경로는 드림스톤뿐이고 주석이 카드 경로를 *never an expected live path* 라 단언하지만, **어휘에는 자리가 필요하다** — 종합 §3.4 가 「코스트 재생 스위치를 매치 상태로 이사」한다고 했으므로 Squad 효과 축도 「유닛 대상 / 매치 대상」 둘로 갈려야 한다.)

`DamageVsCc` 는 반대로 문제없다 — `StatKind.DamageVsCcMul` 로 정상 매핑되고(`:1377-1378`), `ModifierStatsAggregateSystem.cs:48,92` 가 유닛별로 집계한다. 신규 배치분도 `ApplyActiveDcEffectsTo` 로 상속된다.

### 2. `RecallAttachedToFront`(인수인계) — 주어가 유닛이 아니라 **「그 host 에 붙은 카드 집합」**이다
`DreamcatcherHandController.cs:270-294`:
```
회수 묶음 전체를 훑어 DeclaresRetireRecall 가 하나라도 있으면 recall = true
→ 그 다음 각 카드에 대해: toFront = recall && !DeclaresRetireRecall(card)
→ 선언 카드 자신만 큐 뒤, 나머지는 _deck.RecoverToFront(...)
```
`DeclaresRetireRecall`(`:311-323`)은 `type == Unit ∧ payload == RecallAttachedToFront ∧ trigger == OnRetire` 셋을 다 본다(bake 와 동일해야 한다고 주석이 명시).

rev 2 환원표의 `인수인계 … | 동일 | Self | — | MetaIntent(큐 미경유) | host | 소유자 소멸` 은 이 **집합 연산**을 담지 못한다. subject 축(Self / Any)으로도 표현 불가다 — 주어가 엔티티가 아니라 손패 상태다. census 가 이미 「어휘 밖 — 손패 UI, 실행자가 Mono 컨트롤러」로 분류한 것을 rev 2 가 레일 안으로 끌어들이면서 형태가 사라졌다.

(참고: `magnitude` 소비자는 0 이다 — 주석은 「상한」이라 적었지만 컨트롤러도 덱도 안 읽는다.)

### 3. 어휘 밖은 **하나가 아니다**
rev 2 §1: 「**어휘 밖 = 하나.** `HeavyStrike`(자기를 부른 공격의 출력 수정)」.

census 가 같은 범주로 둔 것은 다섯이다:
- `HeavyStrike`(payload 13)
- `DcAttackModSlot` / `DcAttackModKind` 3종 — `ProjectileBounce` · `FrontmostTarget` · `DamageVsSleeping`(`Battle/Combat/DcAttackModSlot.cs`, `DcMechanic.cs:426`)
- `FrontmostAttackLock` + `damageMulSnapshot`(`Battle/Combat/FrontmostAttackLock.cs`, 부착 `BattleBridge.Dreamcatcher.cs:1080`)
- charge 의 **소비**(부여는 스킬, 소비는 `AttackSystem` 내부 — census 「판별 기준」 행의 경계 정정)

`AttackMod` 축으로 뺀 판단은 옳다. 다만 「하나」로 적으면 다음 사람이 나머지 넷을 바인딩으로 끌어들인다. `SkillPayloadPolicy` 는 어휘 밖을 **7종**으로 세고 그 이유가 일곱 다 다르다고 경고한다.

### 4. 「Any subject 리스너 색인」의 비용 계산이 실제 비용을 안 본다
rev 2 §2: 「Any subject 는 Match 리스트 하나 — 사건당 비용 = 자기 슬롯 + 전역 소수. 전 유닛 재스캔 없음」.

기믹의 비용은 리스너 조회가 아니라 **대상 열거**다:
- 온천: 주기마다 전 유닛 2-pass(`HeatAccrualSystem.cs:43-53, 65-70`)
- 레드불 소비: **매 프레임** 전 defender + 전 enemy 순회(`PickupConsumeSystem.cs:50-68`)
- 번아웃: 매 프레임 전 defender 2-pass(`FatigueAccrualSystem.cs:37-54`)

바인딩으로 접어도 이 순회는 그대로 남는다. 「전 유닛 재스캔 없음」은 **유닛 슬롯 스캔**에만 참이다.

### 5. 무할당(P4) 주장 대 현행 할당 지점
이벤트·intent 는 이미 struct 라 P4 는 이벤트 쪽에서 검증할 것이 별로 없다. 실제 할당은 **등록부와 보고** 쪽에 있다:
- `List<ActiveDcEffect> _activeDcEffects` · `List<(int,CardTargetAxis,float)> _activePlacementSleeps` · `_frontScratch` / `_recoverScratch` — 성장 시 재할당
- `Dictionary<int,int> _perSkill`(`SkillDispatchSystem.cs:68`) · `HashSet<Entity> _bountyMarked` · `HashSet<string> WarnedCards`(`DcRangeCatalog.cs:118`)
- `Report` intent 의 문자열은 **어댑터가 만든다**(census §3) — loud 경로가 곧 할당 경로다
- 드레인마다 `ToEntityArray`/`ToComponentDataArray` 4개(`SkillDispatchSystem.cs:156-159`) — `Allocator.Temp` 이지만 새 설계에선 풀이 필요하다

「풀링 + 사전 할당 리스트」로 충분한지는 **상한이 정의된 뒤에**만 말할 수 있다. 오늘 정의된 상한은 후보 64(`migration README`, 「가까운 64」가 아니라 **풀 순서 선착 64**)뿐이고 census 가 그것도 미결로 뒀다.

---

## rev 2 가 옳고 현행이 우연이었던 것

1. **`SimEntityId` 정렬 순회.** `ApplyDreamcatcherCardInternal`(`:152`)·`RevokeDreamcatcherEffects`(`:183`)가 `_defenderByTile`(`Dictionary`)을 순회한다 — 순서가 비결정적이다. 오늘은 독립 enqueue 라 무해하지만 우연이고, 전순서 키를 계약에 올리는 쪽이 맞다.

2. **Lifetime 1급화.** 오늘 수명은 세 곳에 흩어져 있다 — `handle` 규약(`<0` 실패 / `0` 무회수 / `>0` 회수), `DreamcatcherHandController._attachedTo` 레지스트리, 그리고 엔티티 파괴에 딸려 가는 암묵 수명. 회수가 「삭제」가 아니라 「항등 재발행 중화」(`:189-195`, op 까지 재도출)인 것도 병합 키의 성질에서 나온 우연이지 설계가 아니다.

3. **「생존당 1회」의 경계 음수 hack.** `DcTrigger.HealthThresholdEval`(`:54-64`)은 단조 래치 `k` 를 쓰고, 「1회」는 `fraction ≥ 0.5` 일 때 둘째 경계가 음수가 되어 **우연히** 성립한다. 밸런스로 값 한 칸이 0.4 가 되면 조용히 2회가 된다. `N회=1` 을 명시하는 쪽이 옳다.
   ⚠ **단 궁극기에만.** 같은 트리거를 빈사폭주(`ThresholdSelfBuffSkill`)·진동갑주·가호가 쓰고 그쪽은 **다회 발동이 사양**이다(경계마다 누적 하향 돌파). 「HealthThreshold = N회 1」로 일반화하면 그 셋이 죽는다.

4. **커맨드 ≠ 이벤트 + receipt.** `CommitAttach`(`DreamcatcherHandController.cs:~380-400`)가 이미 「apply 먼저, 실패(`handle < 0`)면 무차감·무순환」(contract 9)을 손으로 지키고, 지불 실패 시 `RevokeDreamcatcherEffects` 로 되돌리는 롤백 경로까지 갖고 있다. 거절 사유는 `DcRejectReason` 8종으로 이미 enum 이다. receipt 로 굳히는 것이 맞다.

5. **`DcApplicability` 는 라우팅에 의존하지 않는다.** `EvaluateMechanic`(`DcApplicability.cs:109-…`)은 `SkillIdFor` 를 부르지 않고 host 프로파일 + trigger/payload enum 만 본다. skillId 은퇴가 이 축은 건드리지 않는다 — rev 2 의 암묵 전제는 참이다. (깨지는 것은 `DcRangeCatalog` 쪽 하나다 → C3.)

6. **진행형 상태의 중단 정책 표(§4-1).** 옳고 범위를 더 넓혀야 한다 — `Begin*` 5종 외에 `LastRun`(레드불 crash 타이머, `LastRunSystem.cs:35-51`)도 같은 성질이다. 「엔티티와 함께 소멸」이 현행 정책인데 그건 ECS 수명의 부산물이지 선언이 아니다.

---

## 남는 열린 질문 (5)

1. **기믹의 subject 필터를 통일할 것인가?** 유닛 호스트 바인딩으로 내려도 번아웃(defender 전용·사망 미제외)과 온천(전 유닛·사망·배치중 제외)의 필터가 다르다. 통일하면 규칙이 바뀐다 — 어느 쪽으로?

2. **`AreaBlast` 통합을 포기할 것인가, 이벤트를 넓힐 것인가?** 포기하면 concrete 2 유지, 넓히면 이벤트가 「통행 층」·「예고 시간」까지 날라야 하고 감지자별 표가 다시 생긴다.

3. **세대 BFS 의 적용 범위.** 바인딩 직접 발화로 한정할 것인가, intent 경유 연쇄(시체폭발→OnKill→잿불)까지 한 틱에 접을 것인가? 후자면 연쇄 전투가 바뀐다 — **플레이어가 겪는 규칙이므로 사용자 판정**이다. 깊이 8 의 근거도 함께 다시 대야 한다.

4. **Condition 어휘를 host 상태 술어까지 넓힐 것인가?** 넓히지 않으면 기믹·픽업(`LastRun` 재소비 락)·`DuplicateState` 를 바인딩으로 못 옮긴다. 대안은 「subject 필터」를 Condition 과 별개 축으로 두는 것.

5. **「발동 횟수 상한」과 「부착 수명」을 분리할 것인가?** 표식 카드가 `N회=1 로 발동` + `호스트 소멸까지 부착 유지` 를 동시에 요구해 lifetime 단일 필드로는 담기지 않는다.
