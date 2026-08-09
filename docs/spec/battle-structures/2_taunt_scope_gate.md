# unit 2 — 도발 범위 게이트

## 목적

**유닛을 노리지 않는 적은 유인으로 막을 수 없다.** 거점 전담 적(마스크 = 거점 단독)에게 가디언을 던져도 끌려오지 않고, **죽여야만** 막힌다. 이것이 거점 콘텐츠가 만드는 첫 전술 축이다.

판정은 **저작 의도**(`EnemyTargetFilter.factionMask`)로 한다 — 계약 2.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/AggroStateSystem.cs` — 부착 게이트 1줄 + RO lookup
- `Assets/_Project/Tests/EditMode/` — 게이트 통과/차단 테스트

## 구현

### 1. 부착 1지점에 술어를 더한다

`AggroStateSystem` Pass 3(히트 이벤트 드레인 → `Aggroed` 부착)의 `continue` 게이트 열에 합류한다. **보스 면역(`bossLookup.HasComponent` → `continue`)의 바로 뒤**, 같은 형태다:

```csharp
if ((filterLookup.HasComponent(ev.enemy)
     && (filterLookup[ev.enemy].factionMask & Factions.AnyUnit) == 0)) continue;
```

- `EnemyTargetFilter` 는 Combat 소유 → Effects 에서 **RO 읽기만**. `bossLookup`(`BossTag`)·`attackLookup`(`AttackState`)이 이미 같은 선례다.
- **컴포넌트 부재 = 통과**(fail-open). 합성 테스트 엔티티와 비-적 아키타입이 조용히 도발 불가가 되는 것을 막는다. `AttackSystem` 의 «필터 없으면 `filterMask = -1` = 레거시» 규약과 같은 방향이다.

**왜 소비 지점이 아니라 부착인가** (계약 3): `Aggroed` 소비 지점이 6곳이라 «붙은 것을 무시» 는 6곳을 고쳐야 하고, 하나라도 빠지면 «절반만 도발된» 상태가 된다. 보스 면역이 정확히 이 판단으로 부착을 막았고 그 선례를 따른다.

### 2. 왜 런타임 마스크가 아니라 저작 의도인가

런타임 마스크(`AttackState.targetMask`)를 읽으면 **무기 없는 적이 영구 도발 불가**가 된다:

- 러너·스위프트는 `AttackState` 가 아예 없다(`attackMethod None`) → 런타임 마스크가 존재하지 않는다.
- 도발은 `TauntAttackGrantSystem` 이 **나중에** 유닛 비트를 OR 해 주는 구조다 → 도발되어야 마스크가 생기는데, 마스크가 있어야 도발된다 = 순환.

저작 의도는 `wantsAttack` 게이트 **밖**에서 부착되므로(unit 1) 무기 없는 적도 갖는다. 그 값이 기본값(레거시 = 유닛 포함)이면 게이트를 통과한다 — 현행 도발 거동 무회귀.

### 3. `EnemyAiStateSystem` 미러 점검 — 변경 불필요

README 가 지시한 점검 결과: `EnemyAiStateSystem` 은 `Aggroed` 를 **RO 로 읽기만** 하고 부착하지 않는다(`aggroLookup.HasComponent` → `aggroed` bool → `Evaluate`). 부착을 막으면 이 미러는 «어그로 아님» 을 그대로 관측한다. 손댈 것이 없다 — 계약 3(부착 1지점)이 사는 이유가 이것이다.

## 완료 기준

- [x] 컴파일 에러 0 · 콘솔 신규 에러 0
- [x] EditMode 전량 그린 — **2013개 / 실패 0 / 의도적 스킵 3**(기준선 2010 + 신규 3)
- [x] **거점 전담 적은 도발되지 않는다** — `StructureOnlyEnemy_IsNotAggroed`
- [x] **현행 적은 그대로 도발된다** — `UnitTargetingEnemy_WithIntent_IsStillAggroed`(과잉 차단 검출선)
- [x] **무기 없지만 유닛을 노리는 적도 도발된다** — `WeaponlessEnemy_TargetingUnits_IsStillAggroed`. 픽스처가 `AttackState` 부재를 먼저 단정하므로 런타임 마스크로 판정했다면 실패한다(계약 2 순환 함정 회귀선)
- [x] `EnemyTargetFilter` 부재 적은 통과(fail-open) — 기존 `AggroStateSystemTests` 전량이 필터 없이 돌며 이 경로를 덮는다
- [x] 리뷰: 투트랙(code-reviewer + ecs-reviewer) 완료 2026-08-09. **H1 정정 반영** — 게이트가 raw `factionMask` 를 읽어 0(미저작)이 fail-closed 였다. 0 의 의미는 베이크와 소비자가 같은 함수(`EnemyTargetDefaults.Resolve`)로 읽어야 한다. 회귀 테스트 `UnauthoredFactionMask_IsStillAggroed` 추가

---

**확인 2026-08-09** — 구현 커밋: (아래 커밋 해시)

라이브 행동 변화 **0** — 유닛 비트가 전무한 적이 아직 저작되지 않았다. 게이트는 unit 3 이후 «거점 전담 적» 이 저작될 때 발효한다.
`EnemyAiStateSystem` 미러: 점검 완료, **변경 불필요**(`Aggroed` RO 읽기만 — 부착을 막으면 결과를 그대로 관측).

## 주의

- 이 게이트는 «유닛을 하나라도 노리면 통과» 다(`& AnyUnit`). 방어유닛 + 거점을 같이 노리는 적(현행 전원)은 당연히 통과한다. 차단되는 것은 **유닛 비트가 전무한** 적뿐이다.
- 아직 그런 적은 저작되지 않았다 — unit 2 시점에 라이브 행동 변화는 **0**이다. 게이트는 unit 3 이후 «거점 전담 적» 이 저작될 때 발효한다.
