# 5 — 분열 (`SplitOnDeath`) + 위치 지정 스폰 경로

## 목적

슬라임의 분열을 성립시킨다. 적이 «죽은 자리» 에서 다른 적을 스폰하는 첫 경로다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.SplitOnDeath` +
  `AttackUnitData splitUnit` (**append-only**)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BakeNightmareMechanics` 예외 1줄 +
  `SpawnUnit` 분해 + `DrainEnemyKilledEvents` 에 분열 스폰 + 드레인 순서

**ECS 쪽 변경이 0 이다** — 아래 ② 참조.

## 구현

### ① 저작

```
DcPayloadKind.SplitOnDeath = 21      // append-only (unit 4 의 AreaBreath = 20 다음)
```

`magnitude` = 자식 수 · **신규 `splitUnit`**(`AttackUnitData` 참조) = 자식 SO.
정의 계층은 SO 참조가 허용된다(`projectile`·`pattern`·`auraPrefab`·`stackModifier` 선례 —
금지 대상은 Entities/Battle 타입이고 `AttackUnitData` 는 `Wassup.Data` 다).

⚠ **`splitUnit == null` 또는 `magnitude < 1` 저작은 bake 가 loud 거절**한다. 조용히 넘어가면
«죽어도 안 갈라지는 슬라임» 이 되고 원인이 안 보인다.

### ② 슬롯도, 이벤트 필드도, sim 변경도 필요 없다 (리뷰 H2 로 재설계)

초판은 «드레인 시점엔 엔티티가 파괴돼 있어 역참조가 불가능하다» 는 이유로 인덱스 레지스트리 +
`DcTriggerSlot.splitUnitIndex` + `EnemyKilledEvent` 필드 3개 + `DamageApplicationSystem` 스탬프 +
`EnemyTriggerArmed(OnDeath)` 개방을 지시했다. **그 이유는 ECS 컴포넌트에만 맞고 managed 등록부엔
해당하지 않는다.** 분열을 스폰할 바로 그 루프가 이미 이렇게 시작한다 (`BattleBridge.cs:3971`):

```csharp
if (_enemyTypeByEntity.TryGetValue(evt.entity, out var killedType))
{ killedVisual = killedType; _enemyTypeByEntity.Remove(evt.entity); }
```

**죽은 적의 `AttackUnitData` 가 이미 손에 있다.** 이 등록부는 파괴된 Entity 값을 키로 비교하도록
설계됐고(선언부 주석: «파괴된 Entity 값도 키 비교는 유효 — 역참조 안 함, SO 참조만 보관»),
유출 경로가 이미 같은 방식으로 SO 스탯을 읽는다(`leakedType.stabilityDamage`, ≈`:5322`).

그래서 구현은 **드레인 안 한 곳**이다 — `killedType.nightmareMechanics` 를 훑어
`OnDeath × SplitOnDeath` 를 찾고 `magnitude`(자식 수)·`splitUnit`(SO)를 그 자리에서 읽어
`evt.position` 에 스폰한다. `_enemyTypeByEntity.Remove` **앞**에서 읽어야 한다.

사라지는 것: 인덱스 레지스트리 · 슬롯 필드 · 이벤트 필드 3개 · `DamageApplicationSystem` 변경 ·
**`EnemyTriggerArmed(OnDeath)` 개방**(= unit 3 이 경고하는 계열의 화이트리스트 리스크 1건).

⚠ **대신 bake 에 예외 한 줄이 필요하다.** `BakeNightmareMechanics` 는 화이트리스트에 없는 트리거를
«arm 미개방» 경고와 함께 스킵하므로, `OnDeath × SplitOnDeath` 를 **의도적 무슬롯**으로 명시
면제한다(경고 없이 슬롯 생성 생략 + «브리지가 드레인에서 SO 를 직독한다» 주석). 면제하지 않으면
슬라임을 스폰할 때마다 콘솔에 거짓 경고가 뜬다.

⚠ **`OnDeath × (SplitOnDeath 아닌 페이로드)` 는 적에게 소비자가 없다.** 방어유닛 쪽 `OnDeath`
소비자(`UnitLifecycleSystem` 의 `SelfTileAoe`)는 `WithAll<DeadTag, DefenderUnitTag>` 쿼리라 적을
보지 않는다. bake 가 이 조합은 **loud warning** 한다.

⚠ **유출(골 도달)은 분열하지 않는다 — 사양이다.** `EnemyKilledEvent` 는 goal-reach 경로에서
발화하지 않으므로(`EnemyKilledEvent.cs` 주석) 코드 추가 없이 성립한다. "체력이 전부 소진하면"
이라는 저작 의도와 일치한다.

### ③ 위치 지정 스폰 경로

`SpawnUnit(PendingSpawnEntry)` 는 맵 레인 스폰 지점에 하드와이어돼 있다(`_generatedMap.spawns
[laneIndex]` + `ComputeSpawnLateralOffset`). 본문을 갈라낸다:

```
CreateEnemyEntity(AttackUnitData unitType, Vector3 worldPos)   // 본문 전체
SpawnUnit(PendingSpawnEntry pending)  → 레인 좌표를 계산해 위 함수를 호출하는 얇은 래퍼
```

`laneIndex` 를 **넘기지 않는다**(리뷰 M9). 실측으로 lane 의 유일한 용도는 `spawnWorldPos` 계산
(`_generatedMap.spawns[spawnIndex]` + `ComputeSpawnLateralOffset`)이고 본문 나머지에서 다시
등장하지 않는다. `spawns.Length == 0` 가드와 `laneIndex` 범위 폴백도 **래퍼에만** 남긴다 —
본문으로 내리면 «스폰 지점이 없는 맵에서 분열이 조용히 막히는» 결합이 생긴다. (같은 이유로
«위치 파라미터를 옵셔널로 하나 추가» 안은 기각한다 — 그러면 두 경로의 가드가 한 몸이 된다.)

**`CreatePatrolEntity` 처럼 병렬 복제하지 않는다.** 순찰병은 방어유닛 세트의 일부만 필요했지만
분열 자식은 **적의 표준 세트 전부**(Health·FactionTag·AwakeningReward·KillScore·버퍼 6종·
`PathFollowState`·`AttackState`·behavior·뷰 등록)가 필요하다. 복제하면 다음에 적 스폰에 무언가
추가될 때 한쪽만 갱신된다.

자식 배치: 부모 위치를 중심으로 서로 다른 미세 오프셋을 준다. 겹침은 `AgentSeparationSystem` 이
자연히 밀어낸다 — 셀 배분 로직을 새로 만들지 않는다.

### ④ 웨이브 회전과의 경계 — **이 단위의 가장 조용한 버그** (리뷰로 확정)

`BattleBridge.Update` 순서가 `QueueDueWaves(t)`(≈2594) → … → `DrainEnemyKilledEvents()`(≈2626)
→ … → `CheckVictory()`(≈2639) 다. 브리지 `Update` 가 도는 시점엔 ECS 가 이미
`DamageApplicationSystem`(DeadTag + 이벤트 enqueue)과 `UnitLifecycleSystem`(엔티티 파괴)을 끝냈다.
따라서 부모 슬라임이 **마지막 생존 적**일 때:

- `QueueDueWaves` 시점 — 부모는 `_aliveAttackersQuery` 에서 이미 사라졌고, 자식은 아직 없고,
  `_pending` 에도 분열 항목이 없다 → `NoQueuedAttackersRemain()`(≈5646) 이 참 → **다음 웨이브 큐잉**
- `CheckVictory` 도 같은 술어를 쓰므로 **자식이 생기기 전에 승리를 선언할 수 있다**

**해법 = `DrainEnemyKilledEvents()` 를 `QueueDueWaves(t)` 앞으로 옮긴다.** 리뷰가 이 드레인이 다른
드레인·스폰 루프에 의존하지 않음을 확인했다 — 킬 버스트는 `DrainProjectileSpawnRequests` 를 거치지
않고 `SpawnProjectile` 직접 호출이며, 점수·각성 중계는 순수 가산, `_enemyTypeByEntity` 정리와 표식
회수는 순서 무관이다. 자식은 ECB 가 아니라 `_em.AddComponent<AttackUnitTag>` 직접 부착이라 **같은
프레임에 즉시** `_aliveAttackersQuery` 에 들어온다.

⚠ **`NoQueuedAttackersRemain()` 에 «미처리 분열» 항을 더하는 대안은 채택하지 않는다** — 웨이브 진행
술어가 특정 킬 이벤트 페이로드를 알게 되고, NativeQueue 를 훔쳐보거나 별도 카운터를 유지해야 한다.

### 하지 않는 것

- **세대 카운터·깊이 상한을 넣지 않는다**(계약 2). 자식 SO 에 메커니즘이 없어 재귀가 데이터
  구조상 불가능하다.
- **`maxPerWave` 를 분열에 적용하지 않는다.** 그 필드는 웨이브 생성 시점 축이다.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode: `EnemyTriggerArmed` 화이트리스트가 **3종**(`PeriodicTimer`·`HealthThreshold` +
      unit 3 의 `AttackN`)이고 **`OnDeath` 는 열리지 않았다**
- [ ] EditMode: `splitUnit == null` / `magnitude < 1` / `OnDeath × 다른 페이로드` 저작이
      loud 거절·경고된다
- [ ] Play: 슬라임 스폰 시 콘솔에 «arm 미개방» **거짓 경고가 뜨지 않는다**(bake 면제)
- [ ] PlayMode 신규 e2e: 슬라임 1기를 죽이면 **정확히 2기**가 그 자리에 생기고, 체력이 부모
      최대치의 50% 이며, **그 자식을 죽여도 더 생기지 않는다**
- [ ] PlayMode 신규: 자식이 골 방향으로 **실제로 이동한다**(스폰만 되고 굳지 않는다 —
      `summon-patrol-defender` 가 겪은 «뷰가 제자리에 선다» 계열 회귀 방지)
- [ ] PlayMode 신규: **부모가 마지막 적일 때** 죽여도 다음 웨이브가 큐잉되지 않는다 —
      **킬 프레임 전후로 `_nextWaveIndex` 가 변하지 않음**을 단언한다(④). 같은 프레임에 승리
      선언도 없어야 한다. ★이 테스트가 없으면 나중에 누가 드레인 순서를 다시 옮길 때 조용히
      회귀한다
- [ ] `CreateEnemyEntity` 가 **레인이 아닌 임의 위치**에서도 `PathFollowState` 를 올바로 굽는다
      (기존 bake 경로는 레인 원점 시작을 전제한다 — 위 이동 단언이 이것을 덮는다)
- [ ] PlayMode 무회귀 — baseline 대비 실패 집합 동일. 특히 기존 킬 경로(각성·점수·데미지넘버)
- [ ] **신규 이벤트 채널 0 · `EnemyKilledEvent` 필드 추가 0 · ECS 시스템 변경 0** (리뷰 H2)
