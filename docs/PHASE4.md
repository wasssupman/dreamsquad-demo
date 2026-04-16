# Phase 4 — 배치 시 효과 / 인접 시너지

> 본 문서는 `PRD.md`, `TRD.md`, `PHASE0~3.md`, `phase0~3-decisions.md`를 전제로 작성되었다. Phase 0~3에서 확립된 아키텍처 경계, 맥락 분리, 추상화 규칙, 금지 패턴은 Phase 4에서도 그대로 유지된다.

---

## 0. Phase 4의 존재 이유

### 검증 목표

> **"어디에 놓을 것인가"의 배치 판단이 H2(3분 루프 긴장감) 최종 검증을 받기 전 실질적 결정 공간을 갖는가** — PRD §0 H2의 **배치 축** 조기 검증.

Phase 3까지 배치는 "buildable 타일이면 아무 데나"였다. 배치 위치가 결정의 깊이를 가지려면 위치에 따라 **성능이 달라져야 한다**. Phase 4는 두 가지 메커니즘으로 이 결정 공간을 생성한다:

1. **인접 시너지**: 같은 타입 인접 배치에 따른 스탯 보너스.
2. **배치 시 효과(onPlace)**: 배치 순간 주변에 발동하는 즉시 효과.

추가로 Phase 2/3에서 자리만 열어둔 **투사체 onHit 효과** 중 **Splash 1종**을 이번 Phase에 구현한다. Effects 맥락 확장 관점에서 같은 Phase에 묶는다.

### Phase 4가 하는 것 / 안 하는 것

**하는 것:**
- 인접 시너지 시스템 (4방향 인접 동일 타입).
- `DefenderUnitData.onPlaceEffect` 배치 시 1회 발동.
- 기존 10종 중 3~4종에 onPlace + 시너지 기반 부여(신규 유닛 추가 없음).
- 투사체 Splash: `OnHitEffectType.Splash` 실 구현.
- **적→방어 유닛 공격 메커니즘** (Phase 4 확정 — 방어 유닛 사망 경로 실작동화). 적은 이동하면서 사거리 내 방어 유닛에 근접 즉시 데미지.
- 로깅: synergy/onPlace/splash 기록.

**안 하는 것:**
- Poison/Fire/Slow onHit (Phase 4는 Splash만).
- 대각선 시너지 / 범위 기반 시너지 (4방향만).
- 3분 타이머 / 코스트 / 봇 / H1~H3 전면 측정 (Phase 5).
- 새 맥락 폴더 추가.
- **적 공격에 투사체 적용** — 적 공격은 직격(즉시 IncomingDamage)만. 적 투사체는 Phase 5 이후 검토.

---

## 1. Phase 4의 게임 흐름 변화

```
[배치 시점]
  PlaceDefender 성공 (DefenderTile Component도 부여)
    → onPlaceEffect 있으면 EffectSpawner 경유 적용
    → RecomputeSynergyFor(cell): 자신 + 4인접 SynergyBuff 갱신/제거
    → Logger.RecordOnPlace / Logger.SetSynergyStats
    ↓
[공격 시점 — 방어→적]
  AttackSystem: emittedDamage = attack.damage * damageMul * synergyMul
    (damageMul = DamageBoost, synergyMul = SynergyBuff, 둘 다 HasComponent ? lookup.mul : 1f)
    ↓ (투사체 경로 or 즉시 데미지)
[공격 시점 — 적→방어]
  AttackSystem의 공격자 루프: AttackUnitTag 중 AttackState 보유 엔티티가
  사거리 내 DefenderUnitTag 엔티티를 찾아 IncomingDamage 즉시 append.
  (방어 유닛 측 DamageBoost는 적용 없음 — Phase 4 단순화. 투사체 없음.)
    ↓
[피격 시점]
  DamageApplicationSystem이 IncomingDamage 소비 → Health 감소 → 0 이하면 DeadTag
    ↓
[사망 시점]
  UnitLifecycleSystem이 DeadTag 보유 defender를 DestroyEntity 직전에
  DefenderDeathEventsSingleton.queue.Enqueue(DefenderDeathEvent{ tile })
    ↓
  BattleBridge.Update가 드레인 → `_defenderByTile`에서 제거 →
  RecomputeSynergyFor(deadTile): 남은 인접 유닛의 이웃 수가 감소해 SynergyBuff 재갱신/제거
```

---

## 2. Phase 4 콘텐츠 스펙

### 2.1 인접 시너지

- 판정: 4방향(상/하/좌/우) 인접 타일에 동일 `DefenderUnitData` 인스턴스가 있으면 시너지 1.
- 효과: +10% damage per same-type neighbor (최대 3 → +30%). 쿨다운 불변.
- 시너지 값은 Phase 4 한정 `const float SynergyPerNeighbor = 0.1f` 허용 (튜닝 필요해지면 SO 승격 — §4 자율 결정 영역에서 제외).

**SynergyBuff Component (Effects 맥락):**
```csharp
// Battle/Effects/SynergyBuff.cs
public struct SynergyBuff : IComponentData { public float damageMul; }
```
- 시너지 이웃 0일 때: SynergyBuff **Component 자체를 제거** (기본값 필드 사용 없음, Component 존재 여부로만 판정).
- AttackSystem: `synergyMul = synergyLookup.HasComponent(defenderEntity) ? synergyLookup[...].damageMul : 1f`.

**RecomputeSynergyFor 의사코드 (BattleBridge 내부):**
```
RecomputeSynergyFor(cell):
    for c in [cell, cell+(1,0), cell+(-1,0), cell+(0,1), cell+(0,-1)]:
        if not _defenderByTile.TryGetValue(c, out (entity, data)): continue
        int n = 0
        for neighbor in [c+(1,0), c+(-1,0), c+(0,1), c+(0,-1)]:
            if _defenderByTile.TryGetValue(neighbor, out (_, neighborData)) and neighborData == data: n++
        if n == 0:
            if em.HasComponent<SynergyBuff>(entity): em.RemoveComponent<SynergyBuff>(entity)
        else:
            mul = 1f + SynergyPerNeighbor * n
            if em.HasComponent<SynergyBuff>(entity): em.SetComponentData(entity, new SynergyBuff{damageMul=mul})
            else: em.AddComponentData(entity, new SynergyBuff{damageMul=mul})
```

**`_defenderByTile` 확장 (기존 Dictionary<Vector2Int, Entity> → Dictionary<Vector2Int, (Entity entity, DefenderUnitData data)>):**
- 기존 호출처 마이그레이션 필요 — 특히 `CastSkillOnDefender`의 `TryGetValue(tile, out var entity)` 는 `.entity`를 꺼내는 코드로 수정.
- 네이밍은 코드 기준 단수형 `_defenderByTile` 유지.

### 2.2 배치 시 효과(onPlace)

**DefenderUnitData 확장:**
```csharp
public enum OnPlaceEffectType { None, SlowPulse, BoostNearbyDefenders }
public OnPlaceEffectType onPlaceEffect;
public float onPlaceRange;        // 타일 반경
public float onPlaceMagnitude;    // 효과 세기
public float onPlaceDuration;     // 지속 초
```

**구현 효과 (Phase 4 확정):**
- `SlowPulse`: 배치 반경 `onPlaceRange` 내 모든 `AttackUnitTag` 엔티티에 `EffectSpawner.ApplySlow(duration=onPlaceDuration, multiplier=onPlaceMagnitude)`.
- `BoostNearbyDefenders`: 반경 내 모든 `DefenderUnitTag` 엔티티(자신 포함/제외는 자율)에 `EffectSpawner.ApplyDamageBoost(duration=onPlaceDuration, multiplier=onPlaceMagnitude)`.

**할당 대상 자율 결정 (3~4종)**: 기존 10종 중 기본 stat이 약한 유닛에 onPlace 부여해 트레이드오프 유지. 선택은 `phase4-decisions.md` 기록.

### 2.3 투사체 Splash (onHit)

**ProjectileData SO 확장** (이미 `onHitEffect`, `splashRadius` 있으나 `splashDamageMul` 누락):
```csharp
public class ProjectileData : ScriptableObject {
    // ... 기존 필드들
    public float splashDamageMul = 0.5f;   // 신규. 중심 대 주변 비율 (1.0=동일, 0.5=절반)
}
```
CannonBall SO에 `splashDamageMul=0.5f` 설정.

**ProjectileState 필드 재추가** (Phase 3에서는 미사용으로 제거됐던 필드가 Phase 4에서 **실 사용** 되므로 데드 필드 원칙 위반 아님 — 복귀):
```csharp
public struct ProjectileState : IComponentData {
    public Entity target;
    public float speed;
    public float damage;
    public float hitThreshold;
    public OnHitEffectType onHitEffect;   // 신규
    public float splashRadius;            // 신규
    public float splashDamageMul;         // 신규
}
```

**ProjectileRef 확장** (Defender 엔티티에 부착, onHit 정보 캐시):
```csharp
public struct ProjectileRef : IComponentData {
    public int assetIndex;
    public float speed;
    public float hitThreshold;
    public float visualScale;
    public OnHitEffectType onHitEffect;   // 신규
    public float splashRadius;            // 신규
    public float splashDamageMul;         // 신규
}
```

**ProjectileSpawnRequest 확장** (전달 채널):
```csharp
public struct ProjectileSpawnRequest : IComponentData {
    ... // 기존 필드
    public OnHitEffectType onHitEffect;   // 신규
    public float splashRadius;            // 신규
    public float splashDamageMul;         // 신규
}
```

**onHit 정보 흐름 (명시, 역매핑 금지)**:
1. BattleBridge.PlaceDefender: ProjectileData에서 onHit 읽어 ProjectileRef 생성.
2. AttackSystem: ProjectileRef 읽어 ProjectileSpawnRequest에 복사.
3. BattleBridge.SpawnProjectile: req의 onHit 필드를 그대로 ProjectileState에 복사.
- **절대 금지**: BattleBridge.SpawnProjectile이 assetIndex로 ProjectileData를 역조회하여 onHit을 읽는 패턴 (req에 전달된 값만 신뢰).

**ProjectileHitSystem Splash 처리:**
- 직격 타깃에 기존대로 `IncomingDamage { amount = damage }` append.
- `onHitEffect == Splash`인 경우:
  - **AOE 후보를 스냅샷**: `SystemAPI.QueryBuilder().WithAll<AttackUnitTag, LocalTransform>().Build().ToEntityArray(Allocator.Temp)` + 동반 `ToComponentDataArray<LocalTransform>`. 이는 `ProjectileHitSystem`의 기존 projectile iteration 내부에서 중첩 쿼리 금지(Entities 1.4.x undefined behavior) — AttackSystem.cs의 기존 스냅샷 패턴과 동일.
  - 스냅샷 배열을 순회하며 직격 타깃과의 거리 확인, `splashRadius` 내면서 **직격 타깃이 아닌** 엔티티에 `IncomingDamage { amount = damage * splashDamageMul }` ECB append.
  - 루프 종료 후 반드시 `entities.Dispose(); transforms.Dispose();`.
  - HealthBar 등 다른 엔티티는 AttackUnitTag 필터로 자동 제외.
- CannonBall에 Splash 할당 (radius=1.2, damageMul=0.5 — Phase 4 기본).

### 2.4 적→방어 공격 (enemy→defender attack)

**AttackUnitData 확장:**
```csharp
public float attackDamage;     // 0 이하면 이 적은 공격 안 함(통과 적)
public float attackRange;      // 1.0 권장 (근접만)
public float attackCooldown;   // 공격 간격 초
```

- attackDamage=0이면 기존 "순수 통과" 적 동작 유지(Phase 0~3 호환). Swift 같은 빠른 적은 attackDamage=0 유지 가능.
- Tanker/Basic 등 체력 높은 적에 attackDamage>0 부여 권장(자율 결정).

**Defender 엔티티 피격 준비:**
- BattleBridge.PlaceDefender에서 defender 엔티티 생성 시 `_em.AddBuffer<IncomingDamage>(entity)` 추가.
- Health Component는 이미 Phase 0부터 부여 중.
- DamageApplicationSystem은 기존대로 IncomingDamage 소비 → Health 감소 → DeadTag 부여. 방어 엔티티도 이 경로로 진입.

**AttackUnitTag용 AttackState 생성:**
- BattleBridge.SpawnUnit에서 `entry.unitType.attackDamage > 0` 이면 `AttackState { damage, range, cooldownDuration, cooldownRemaining=0 }` 부여. 0이면 부여 안 함(기존 행동 유지).

**AttackSystem 공격자 루프 추가:**
- 기존 루프: `WithAll<DefenderUnitTag, AttackState, LocalTransform>()` → 사거리 내 AttackUnitTag 타깃.
- 신규 루프: `WithAll<AttackUnitTag, AttackState, LocalTransform>()` → 사거리 내 DefenderUnitTag 타깃.
- 두 루프의 **데미지 스케일링 규칙은 다름**:
  - 방어→적: `emittedDamage = attack.damage * damageBoost * synergy` (기존).
  - 적→방어: `emittedDamage = attack.damage` (boost/synergy 모두 미적용 — 적 측 효과 없음).
- 신규 루프는 투사체 분기 **없음** — 무조건 IncomingDamage 즉시 append.
- attacker 쪽도 DefenderUnit snapshot이 필요하므로 AttackSystem 시작부에서 `defenderQuery.ToEntityArray + ToComponentDataArray<LocalTransform>` 사전 수집. 방어→적 루프가 이미 하는 attacker snapshot 패턴을 대칭으로.

### 2.5 DefenderTile + DefenderDeathEvent

**DefenderTile Component** (Units 맥락):
```csharp
// Battle/Units/DefenderTile.cs
public struct DefenderTile : IComponentData { public int2 cell; }
```
- PlaceDefender에서 defender 엔티티 생성 시 `AddComponentData(entity, new DefenderTile { cell = new int2(tileX, tileY) })`.
- UnitLifecycleSystem이 사망 처리 시 이 Component로 타일 좌표 획득.

**DefenderDeathEvent + Singleton** (Units 맥락, GoalReachedEvent 패턴 복제):
```csharp
// Battle/Units/DefenderDeathEvent.cs
public struct DefenderDeathEvent { public int2 cell; }

// Battle/Units/DefenderDeathEventsSingleton.cs
public struct DefenderDeathEventsSingleton : IComponentData {
    public NativeQueue<DefenderDeathEvent> queue;
}
```

**라이프사이클 (BattleBridge 책임, GoalReachedEvent와 동일 패턴)**:
- StartBattle: `_defenderDeathQueue = new NativeQueue<DefenderDeathEvent>(Allocator.Persistent)` + singleton entity 생성.
- TeardownCurrentBattle: singleton entity DestroyEntity + queue Dispose.
- OnDestroy: queue Dispose.

**UnitLifecycleSystem 변경** (가장 중요한 순서 보장):
- 기존: `PastGoalTag + AttackUnitTag` → GoalReachedEvent 발행 + DestroyEntity.
- 추가: `DeadTag + DefenderUnitTag + DefenderTile` 쿼리 → `DefenderDeathEvent { cell = DefenderTile.cell }` enqueue **후** DestroyEntity. enqueue는 반드시 DestroyEntity 직전.
- BattleBridge.Update: `DrainDefenderDeathEvents()`가 큐에서 이벤트 dequeue → `_defenderByTile.Remove(cell)` → **`_occupiedTiles.Remove(cell)`** (타일 재배치 가능하게 해제) → `RecomputeSynergyFor(cell)`.

### 2.6 로깅 확장 (BattleLogSchema v4)

```csharp
[Serializable]
public class SynergyRecord {
    // 판 내 "PlaceDefender 결과로 새로 시너지를 얻은 엔티티" 누적 수 (같은 엔티티가 여러 번 갱신되어도 1회로 집계).
    public int activations;
    // 판 내 임의 시점에 SynergyBuff를 보유했던 defender 수의 최대값.
    public int peakCount;
}

[Serializable]
public class OnPlaceUsageLog {
    public string unit_type;
    public string effect;     // OnPlaceEffectType 문자열
    public Vector2Int tile;
    public float time;
    public int affected_count;
}

// BattleLogEntry 확장:
public SynergyRecord synergy = new();
public List<OnPlaceUsageLog> on_place_usages = new();
```

- `activations` 집계: BattleBridge 내부 `HashSet<Entity> _synergyActivatedEntities`를 판 세션 동안 유지. RecomputeSynergyFor가 "SynergyBuff 부재 → AddComponent" 전환을 수행할 때 해당 Entity를 HashSet에 add, 집합 크기 변화분만 `activations`에 더함. 동일 엔티티가 여러 번 재활성화되어도 1회만 집계됨. 타일에 새 엔티티가 오면 다른 Entity id이므로 새 activation으로 카운트(의도적 — 서로 다른 유닛이 시너지를 얻었다는 뜻).  
  StartBattle/Teardown에서 HashSet Clear.
- `peakCount` 집계: RecomputeSynergyFor **직후 한 번**만 `_em.CreateEntityQuery(ComponentType.ReadOnly<SynergyBuff>()).CalculateEntityCount()`로 현재 보유 수 측정 → `peakCount = Math.Max(peakCount, count)` 갱신. 매 틱 측정 아님.
- Splash 로깅은 Phase 4에서 별도 필드 추가하지 않음(최소 스키마 원칙).

### 2.7 기존 코드 영향 요약

| 파일 | 변경 |
|---|---|
| `DefenderUnitData.cs` | onPlaceEffect/onPlaceRange/onPlaceMagnitude/onPlaceDuration 추가 |
| `AttackUnitData.cs` | attackDamage/attackRange/attackCooldown 추가 (기본 0 = 비공격) |
| `ProjectileData.cs` | `splashDamageMul` 필드 신규 추가(기존 onHitEffect/splashRadius는 Phase 3부터 있음) |
| `Battle/Effects/EffectSpawner.cs` | `SetSynergy(em, entity, mul)` + `RemoveSynergy(em, entity)` 신규 — Effects Component 쓰기 창구 일관성 유지 |
| `Battle/Effects/SynergyBuff.cs` (신규) | Effects 맥락 |
| `Battle/Units/DefenderTile.cs` (신규) | Units 맥락 |
| `Battle/Units/DefenderDeathEvent.cs` (신규) | Units 맥락 |
| `Battle/Units/DefenderDeathEventsSingleton.cs` (신규) | Units 맥락 |
| `Battle/Combat/Projectile/ProjectileState.cs` | onHit 필드 3개 복귀 |
| `Battle/Combat/Projectile/ProjectileRef.cs` | onHit 필드 3개 추가 |
| `Battle/Combat/Projectile/ProjectileSpawnRequest.cs` | onHit 필드 3개 추가 |
| `Battle/Combat/Projectile/ProjectileHitSystem.cs` | Splash AOE 분기(`WithAll<AttackUnitTag>` 필터, 직격 제외) |
| `Battle/Combat/AttackSystem.cs` | (방어→적) SynergyBuff 읽기 + onHit 전달. (적→방어) 신규 루프: AttackUnitTag+AttackState로 DefenderUnitTag 사거리 내 타깃에 IncomingDamage 즉시 append (투사체/효과 스케일 없음) |
| `Battle/Units/UnitLifecycleSystem.cs` | DeadTag+DefenderUnitTag+DefenderTile 경로 추가, DestroyEntity 직전 enqueue. 기존 일반 DeadTag 루프는 `.WithNone<DefenderTile>()` 필터로 중복 파괴 방지 |
| `Bridge/BattleBridge.cs` | `_defenderByTile` 튜플화(기존 호출처: PlaceDefender set/CastSkillOnDefender/TeardownCurrentBattle/CheckVictory 등 5개소), PlaceDefender에 IncomingDamage buffer + DefenderTile 부여·onPlace 발동·시너지 재계산(순서: onPlace → RecomputeSynergyFor → Log), SpawnUnit에서 attackDamage>0이면 AttackState 부여, Update에 DrainDefenderDeathEvents(순서: SpawnRequest → DefenderDeath → GoalReached), Start/Teardown/OnDestroy에 singleton 라이프사이클, SpawnProjectile이 req의 onHit을 ProjectileState에 복사 |
| `Logging/BattleLogSchema.cs` | SynergyRecord/OnPlaceUsageLog + BattleLogEntry 필드 |
| `Logging/BattleLogger.cs` | RecordOnPlace, SetSynergyStats |
| `Core/DraftController.cs` 등 | 변경 없음 |

**기존 테스트 영향**: EffectIntegrationTests, ProjectileSystemTests는 ProjectileState 구조가 바뀌어 필드 초기화 구문이 달라질 수 있음. 테스트 케이스는 onHitEffect=None으로 기본 초기화하면 자동 유지 (struct 기본값).

---

## 3. 종료 조건 (Done Criteria)

### 3.1 기능 이진 체크 (작업 순서)

**[P4-01] 데이터 스키마 확장 (DefenderUnitData + AttackUnitData + ProjectileData + ProjectileState/Ref/SpawnRequest + enums)**
- [ ] `DefenderUnitData`에 onPlace 4필드 + `OnPlaceEffectType` enum
- [ ] `AttackUnitData`에 attackDamage/attackRange/attackCooldown 필드 추가(기본 0)
- [ ] `ProjectileData`에 `splashDamageMul` 필드 추가 (기본 0.5f)
- [ ] `ProjectileState`에 onHitEffect/splashRadius/splashDamageMul 필드 복귀
- [ ] `ProjectileRef`에 onHitEffect/splashRadius/splashDamageMul 필드 추가
- [ ] `ProjectileSpawnRequest`에 onHitEffect/splashRadius/splashDamageMul 필드 추가
- [ ] `Battle/Effects/SynergyBuff.cs` (IComponentData { damageMul })
- [ ] `Battle/Units/DefenderTile.cs` (IComponentData { int2 cell })
- [ ] `Battle/Units/DefenderDeathEvent.cs` + `DefenderDeathEventsSingleton.cs`
- [ ] 기존 10종 중 3~4종에 onPlace 부여 (SO Inspector 설정, 자율 기록)
- [ ] 기존 공격 유닛 3종 중 2종(Tanker/Basic 권장, Swift는 비공격 유지)에 attackDamage/Range/Cooldown 설정
- [ ] CannonBall에 onHitEffect=Splash, splashRadius=1.2, splashDamageMul=0.5 설정
- 선행: Phase 3 완료
- 완료 확인: 컴파일 정상, Inspector에서 onPlace/onHit/적 공격 필드 읽힘

**[P4-02] AttackSystem: SynergyBuff 반영 + onHit 전달**
- [ ] SynergyBuff ComponentLookup 추가
- [ ] `float synergyMul = HasComponent ? lookup[e].damageMul : 1f;`
- [ ] `emittedDamage = attack.damage * damageMul * synergyMul;` (순서: base × boost × synergy)
- [ ] ProjectileRef의 onHit 3필드를 ProjectileSpawnRequest에 복사
- 선행: P4-01
- 완료 확인: EditMode 테스트 1건 — SynergyBuff 있는 defender의 발사 damage가 배수만큼 증가. 폴백 경로(SynergyBuff 없음) 기존 그대로.

**[P4-03] BattleBridge: `_defenderByTile` 튜플화 + PlaceDefender 마이그레이션**
- [ ] `_defenderByTile` 타입을 `Dictionary<Vector2Int, (Entity entity, DefenderUnitData data)>`로 변경
- [ ] 기존 호출처 전부 마이그레이션: `CastSkillOnDefender`의 `.TryGetValue` 호출, 나머지 모든 set/get
- [ ] PlaceDefender에서 `_defenderByTile[cell] = (entity, unitData)`
- [ ] PlaceDefender에서 `DefenderTile` Component 부여
- 선행: P4-01
- 완료 확인: 컴파일 정상, 기존 스킬 캐스팅 플로우(PowerSurge/RapidFire on 방어 유닛) 여전히 동작

**[P4-04] RecomputeSynergyFor + EffectSpawner 쓰기 창구 경유**
- [ ] `EffectSpawner.SetSynergy(em, entity, mul)` + `EffectSpawner.RemoveSynergy(em, entity)` 메서드 신규 (Add/Update/Remove 래핑, Phase 2 EffectSpawner 패턴 일관)
- [ ] `RecomputeSynergyFor(Vector2Int cell)` 메서드 구현 (§2.1 의사코드) — **SynergyBuff Add/Set/Remove 는 반드시 `EffectSpawner` 경유**. BattleBridge가 `em.AddComponent<SynergyBuff>` 직접 호출 금지.
- [ ] 이웃 0이면 `EffectSpawner.RemoveSynergy`, 이웃 ≥1이면 `EffectSpawner.SetSynergy`
- [ ] PlaceDefender 말미에서 `RecomputeSynergyFor(cell)` 호출
- [ ] synergy.activations / peakCount 집계 (§2.6, HashSet<Entity> 기반)
- 선행: P4-03
- 완료 확인: execute_code — 같은 타입 2개 인접 배치 시 둘 다 SynergyBuff{damageMul=1.1} 보유. 3개 일자 배치 시 중간 엔티티 damageMul=1.2, 양 끝 damageMul=1.1.

**[P4-05] onPlace 효과 발동 + 로깅 (순서 확정: onPlace → 시너지 재계산 → Log)**
- [ ] PlaceDefender 말미 순서: **(1) onPlace switch → (2) RecomputeSynergyFor → (3) logger.RecordOnPlace**. 순서 이유: onPlace는 주변 스냅샷 효과이므로 자신의 SynergyBuff 상태와 무관, 재계산이 반드시 후행.
- [ ] `SlowPulse`: 반경 내 AttackUnit 쿼리 → `EffectSpawner.ApplySlow`
- [ ] `BoostNearbyDefenders`: 반경 내 DefenderUnit 쿼리 → `EffectSpawner.ApplyDamageBoost` (자신 포함 여부는 자율)
- [ ] `BattleLogger.RecordOnPlace` 호출
- 선행: P4-04
- 완료 확인: onPlace 보유 방어 유닛 배치 시 주변 엔티티에 Effect Component 부여 확인(execute_code) + 로그 파일 on_place_usages 채워짐

**[P4-06] 사망 이벤트 경로 연결**
- [ ] StartBattle: DefenderDeathEventsSingleton 생성 + NativeQueue 할당
- [ ] TeardownCurrentBattle: singleton entity DestroyEntity + queue Dispose
- [ ] OnDestroy: queue Dispose
- [ ] UnitLifecycleSystem: 신규 쿼리 `DeadTag + DefenderUnitTag + DefenderTile` → enqueue → DestroyEntity (이 순서 엄수). **기존 일반 DeadTag 루프에는 `.WithNone<DefenderTile>()` 필터 추가**하여 중복 파괴 방지.
- [ ] BattleBridge.Update에 `DrainDefenderDeathEvents()` 추가: dequeue → `_defenderByTile.Remove(cell)` → **`_occupiedTiles.Remove(cell)`** → `RecomputeSynergyFor(cell)`
- [ ] `DrainDefenderDeathEvents()`는 Update 내에서 **DrainProjectileSpawnRequests 이후, DrainGoalEvents 이전** 호출 — 같은 프레임에 배치와 사망이 겹칠 때 사망이 먼저 반영되어 재계산 기반이 일관됨
- 선행: P4-04
- 완료 확인: 인접 시너지 활성 상태에서 defender 사망 시 남은 유닛의 SynergyBuff 갱신/제거, 죽은 타일에 재배치 가능

**[P4-07] 적→방어 공격 경로 연결**
- [ ] BattleBridge.SpawnUnit에서 `entry.unitType.attackDamage > 0` 시 `AttackState { damage, range, cooldownDuration, cooldownRemaining=0 }` 부여
- [ ] BattleBridge.PlaceDefender에서 defender 엔티티에 `_em.AddBuffer<IncomingDamage>(entity)` 추가
- [ ] AttackSystem에 공격자 루프 신규: `WithAll<AttackUnitTag, AttackState, LocalTransform>()` → 사거리 내 DefenderUnitTag 스냅샷 타깃 → 즉시 IncomingDamage append + 자체 쿨다운 리셋. DamageBoost/Synergy/Projectile 분기 **없음**.
- [ ] 두 루프용 snapshot: attacker snapshot(기존, 방어→적용), defender snapshot(신규, 적→방어용) 둘 다 OnUpdate 시작부에서 수집하고 말미에 Dispose.
- 선행: P4-03
- 완료 확인: Tanker에 attackDamage=5 설정 후 Play — 배치한 defender가 Tanker 근접 통과 시 체력바가 줄어듦. Swift(attackDamage=0)는 영향 없음.

**[P4-08] ProjectileHitSystem Splash AOE**
- [ ] `onHitEffect == Splash`일 때 AOE 쿼리 (`WithAll<AttackUnitTag, LocalTransform>`)
- [ ] 직격 target은 AOE append에서 제외
- [ ] AOE 대상에 `IncomingDamage { amount = damage * splashDamageMul }` append
- [ ] SpawnProjectile이 req의 onHit 필드를 ProjectileState에 복사 (역매핑 금지)
- 선행: P4-01, P4-02
- 완료 확인: Cannon 발사 시 직격 + 주변 적에 splash 데미지. HealthBar는 영향 없음.

**[P4-09] 로깅 스키마 v4 + 연결**
- [ ] `BattleLogSchema.cs`에 SynergyRecord + OnPlaceUsageLog + `BattleLogEntry.synergy`/`on_place_usages`
- [ ] `BattleLogger.RecordOnPlace(OnPlaceUsageLog)`, `SetSynergyStats(int activations, int peakCount)`
- [ ] RecomputeSynergyFor 결과로 activations/peakCount 갱신
- 선행: P4-05
- 완료 확인: 세션 JSON에 synergy·on_place_usages 필드 적재

**[P4-10] EditMode 테스트 확장**
- [ ] SynergyBuff × AttackSystem: damage 곱셈 반영 테스트 1건 (EffectIntegrationTests에 추가 가능)
- [ ] ProjectileHitSystem × Splash: AOE append + 직격 중복 배제 + 비-AttackUnit 제외 1~2건
- [ ] 적→방어 공격: AttackUnitTag+AttackState가 DefenderUnitTag 타깃에 IncomingDamage append 1건
- [ ] 기존 테스트 회귀 없음 (SynergyBuff 없는 defender의 기본 공격, ProjectileRef의 onHit=None 경로 유지, AttackUnit에 AttackState 없을 때 공격 루프가 스킵)
- 선행: P4-02, P4-07, P4-08
- 완료 확인: run_tests 전부 pass (**기존 통과 수 + 신규 3~4건**)

**[P4-11] Phase 0~3 회귀 체크**
- [ ] 드래프트 → 전투(투사체/시너지/Splash/onPlace/적공격 비주얼) → 스킬 → 결과 → Restart/Redraft 정상
- [ ] 로그 파일에 synergy, on_place_usages 적재 + 기존 필드 무파손
- [ ] onPlace 없는 방어 유닛, ProjectileRef 없는 방어 유닛, attackDamage=0 적 유닛 정상 동작
- [ ] defender 사망 시 체력바도 사라지고 타일 재배치 가능
- 선행: P4-09, P4-10
- 완료 확인: 한 판 수동 플레이 완주, defender 사망 1회 이상 관찰

---

### 3.2 아키텍처 이진 체크

**Phase 0~3 재확인:**
- [ ] BattleBridge 유일 MonoBehaviour ↔ ECS 창구
- [ ] 맥락 4종 유지, 새 폴더 0개
- [ ] GameManager 유일 싱글톤

**Phase 4 전용:**
- [ ] SynergyBuff는 Effects 맥락, 쓰기는 BattleBridge(MonoBehaviour)만 수행
- [ ] AttackSystem은 SynergyBuff 읽기만
- [ ] onPlace 효과 구현은 EffectSpawner 경유
- [ ] Splash 로직은 ProjectileHitSystem 내부만
- [ ] DefenderDeathEvent는 GoalReachedEvent와 동일 NativeQueue singleton 패턴
- [ ] UnitLifecycleSystem이 DestroyEntity 직전에 enqueue (타이밍 엄수)
- [ ] ProjectileState/Ref/SpawnRequest 신규 필드는 전부 실 사용 (데드 필드 0)
- [ ] `_defenderByTile` 타입 변경에 따른 기존 호출처 마이그레이션 완료
- [ ] onHit 정보는 ProjectileData → ProjectileRef → SpawnRequest → ProjectileState 단방향 전달, 역매핑 없음
- [ ] Assembly Definition 2개 체제 유지

---

### 3.3 주관 평가 게이트

Phase 4 핵심 질문: **배치 결정이 의미 있게 달라지는가.**

- 3~5명에게 동일 드래프트 + 동일 맵으로 3판 이상 플레이 시킨 후:
  - "어떤 유닛을 어디에 놓을지 고민한 적이 있는가?" (Y/N)
  - "같은 유닛을 모아놓으면 더 강해진다는 것을 인지했는가?" (Y/N)
  - "onPlace 효과(배치 순간 발동 현상)가 있다는 것을 알아챘는가?" (Y/N)
- 통과 기준: 2문항 이상 Y 다수.

---

## 4. 에이전트 자율 결정 영역

- onPlace 부여 대상 방어 유닛 선택 (3~4종)
- onPlace 수치 구체값 (onPlaceRange/onPlaceMagnitude/onPlaceDuration)
- Splash 반경/비율 (기본 radius=1.2, damageMul=0.5 제공 — 튜닝 자율)
- `BoostNearbyDefenders`에서 자기 자신 포함 여부
- 적 공격 대상 공격 유닛 선택 (Tanker/Basic 권장, Swift는 0 유지)
- 적 공격 수치 구체값 (attackDamage/attackRange/attackCooldown)

**고정(자율 결정 아님)**:
- 시너지 % `SynergyPerNeighbor = 0.1f` const
- 4방향 인접만 (대각선 제외)
- 시너지 재계산 타이밍: PlaceDefender/DrainDefenderDeathEvents 내부에서 **즉시 동기** 호출 (§1 흐름). 같은 프레임 동시 이벤트는 사망 드레인이 배치 이후에 처리.
- PlaceDefender 순서: onPlace → RecomputeSynergyFor → Log (P4-05)
- Update Drain 순서: SpawnRequest → DefenderDeath → GoalReached (P4-06)
- peakCount 측정: RecomputeSynergyFor 직후 한 번만 (매 틱 측정 아님)
- 적 공격은 투사체/효과 스케일 없이 직격 즉시 데미지 (§2.4)

**결정 원칙**: 단순한 쪽. Phase 4는 H2 배치 축 조기 검증.

---

## 5. Phase 4 종료 시 산출물

- 동작 Unity 6 프로젝트 (시너지 + onPlace + Splash 반영)
- EditMode 테스트 (기존 + 신규 2~3건) pass
- `phase4-decisions.md` 누적 기록
- synergy/onPlace 기록이 포함된 JSON 로그 샘플 3개 이상
- Phase 5에서 재활용될 핵심 타입: SynergyBuff, OnPlaceEffectType, DefenderDeathEvent 경로, Splash 분기

---

## 6. Phase 순서 (현재)

| Phase | 내용 | 상태 |
|---|---|---|
| 0 | 실시간 디펜스 루프 | ✅ 완료 |
| 1 | 드래프트 | ✅ 완료 |
| 2 | 스킬 | ✅ 완료 |
| 3 | 전투 비주얼 | ✅ 완료 |
| **4** | **배치 시 효과 / 인접 시너지** | **현재** |
| 5 | 마무리 (3분 타이머, 봇, H1~H3) | 대기 |

Phase 4 종료 후 `PHASE5.md`를 작성한다.

---

## 7. TRD 금지 패턴의 Phase 4 재적용

- **Effects Component 쓰기는 EffectSpawner 또는 BattleBridge(MonoBehaviour 경로)만** — AttackSystem이 SynergyBuff를 직접 쓰면 안 됨(읽기만).
- **새 싱글톤 금지** — DefenderDeathEventsSingleton은 singleton-entity + NativeQueue 패턴(GoalReachedEventsSingleton 복제).
- **"나중을 위한" 인터페이스 금지** — OnPlaceEffectType / OnHitEffectType enum + switch.
- **수치 하드코딩 금지** — onPlace 값은 SO. 시너지 % 는 Phase 4 한정 const 허용.
- **새 맥락 폴더 금지** — SynergyBuff는 Effects, DefenderTile/DefenderDeathEvent는 Units.
- **Assembly Definition 2개 체제 유지**.
- **Phase 2 원본 Component 불변 원칙 유지** — AttackState.damage, PathFollowState.speed 는 여전히 쓰기 금지. SynergyBuff multiplier는 읽어서 방출값 계산에만 사용.
- **데드 필드 금지 원칙 유지** — Phase 3에서 제거했던 ProjectileState onHit 필드를 Phase 4에 복귀시키는 근거는 **Splash가 실 사용**이기 때문. 사용하지 않는 필드는 계속 도입 금지.

---

**문서 버전**: v1.2
**상태**: 확정, 3차 리뷰 반영 완료 (prior critic 14건 + Codex 5건 + lead 6건), 구현 착수 가능

**v1.1 → v1.2 변경** (lead 리뷰 + 사용자 결정 option A):
- HIGH: **적→방어 공격 메커니즘을 Phase 4 스코프에 포함** — 사망 경로 dead code 해소. AttackUnitData 확장, AttackSystem 신규 공격자 루프, defender에 IncomingDamage buffer 부여. 신규 작업 P4-07 추가, 전체 P4-01~P4-11로 재번호.
- HIGH: **SynergyBuff 쓰기 창구 EffectSpawner 경유 확정** — Phase 2 decision #9 일관성. `EffectSpawner.SetSynergy/RemoveSynergy` 신설 요구.
- MEDIUM: **PlaceDefender 순서 확정**: onPlace → RecomputeSynergyFor → Log (P4-05 명시).
- MEDIUM: **UnitLifecycleSystem 쿼리 중복 방지**: 기존 일반 DeadTag 루프에 `.WithNone<DefenderTile>()` 필터 (P4-06).
- LOW: §4 자율 결정 영역 orphan bullet 제거, 적 공격 관련 자율 항목 추가, 고정 항목에 순서/타이밍 이관.
- LOW: `_defenderByTile` 튜플화 기존 호출처 5개소 명시 (코드 영향 표).

**v1.0 → v1.1 변경** (Codex 지적):
- HIGH: 죽은 defender의 `_occupiedTiles` 해제 누락 수정.
- HIGH: `ProjectileData.splashDamageMul` 필드 누락 수정.
- HIGH: 같은 프레임 Drain 순서 확정.
- MEDIUM: Splash AOE 스냅샷 패턴 강제.
- MEDIUM: activations HashSet dedup.
- LOW: peakCount API 명시.
