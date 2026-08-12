# 5 — 적 `OnDeath` 개방 + `SplitOnDeath` + 위치 지정 스폰 경로

## 목적

슬라임의 분열을 성립시킨다. 적이 «죽은 자리» 에서 다른 적을 스폰하는 첫 경로다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — `EnemyTriggerArmed` 에 `OnDeath` 추가
- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadKind.SplitOnDeath` +
  `AttackUnitData splitUnit` (**append-only**)
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs` — `splitUnitIndex`
- `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs` — `hasSplit` / `splitCount` /
  `splitUnitIndex` (**append-only**)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — 킬 시점 스탬프
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnUnit` 분해 + `DrainEnemyKilledEvents`

## 구현

### ① 저작 → 슬롯

```
DcPayloadKind.SplitOnDeath = 21      // append-only (unit 4 의 AreaBreath = 20 다음)
```

`magnitude` = 자식 수 · **신규 `splitUnit`**(`AttackUnitData` 참조) = 자식 SO.
정의 계층은 SO 참조가 허용된다(`projectile`·`pattern`·`auraPrefab` 선례 — 금지 대상은
Entities/Battle 타입이다).

슬롯은 unmanaged 이므로 SO 를 담을 수 없다 → **인덱스 레지스트리**를 쓴다. `projectileDataIndex`
와 완전히 같은 관용구(`Dictionary<T,int> + GetOrCreateXxxIndex`)로 `splitUnitIndex` 를 만든다.

⚠ **`splitUnit == null` 저작은 bake 가 loud 거절**한다(슬롯을 만들지 않는다). 조용히 넘어가면
«죽어도 안 갈라지는 슬라임» 이 되고 원인이 안 보인다.

### ② 킬 시점 스탬프 — `hasKillBurst` 선례 그대로

`DamageApplicationSystem` 의 킬 처리에서 **죽는 적 자신의** `DcTriggerSlot` 을 읽어
`trigger == OnDeath && payload == SplitOnDeath` 첫 슬롯을 이벤트에 굽는다. 드레인 시점엔 엔티티가
이미 파괴돼 있어 역참조가 불가능하기 때문이다(`hasKillBurst`·`awakeningReward`·`killScore` 와 같은
이유). 위치는 `EnemyKilledEvent.position` 이 이미 싣고 있다.

⚠ **`OnDeath × (SplitOnDeath 아닌 페이로드)` 는 적에게 소비자가 없다.** 방어유닛 쪽 `OnDeath`
소비자(`UnitLifecycleSystem` 의 `SelfTileAoe`)는 `WithAll<DeadTag, DefenderUnitTag>` 쿼리라 적을
보지 않는다. bake 가 이 조합을 **loud warning** 한다.

⚠ **유출(골 도달)은 분열하지 않는다 — 사양이다.** `EnemyKilledEvent` 는 goal-reach 경로에서
발화하지 않으므로(`EnemyKilledEvent.cs` 주석) 코드 추가 없이 성립한다. "체력이 전부 소진하면"
이라는 저작 의도와 일치한다.

### ③ 위치 지정 스폰 경로

`SpawnUnit(PendingSpawnEntry)` 는 맵 레인 스폰 지점에 하드와이어돼 있다(`_generatedMap.spawns
[laneIndex]` + `ComputeSpawnLateralOffset`). 본문을 갈라낸다:

```
CreateEnemyEntity(AttackUnitData unitType, Vector3 worldPos, int laneIndex)   // 본문 전체
SpawnUnit(PendingSpawnEntry pending)  → 레인 좌표를 계산해 위 함수를 호출하는 얇은 래퍼
```

**`CreatePatrolEntity` 처럼 병렬 복제하지 않는다.** 순찰병은 방어유닛 세트의 일부만 필요했지만
분열 자식은 **적의 표준 세트 전부**(Health·FactionTag·AwakeningReward·KillScore·버퍼 6종·
`PathFollowState`·`AttackState`·behavior·뷰 등록)가 필요하다. 복제하면 다음에 적 스폰에 무언가
추가될 때 한쪽만 갱신된다.

자식 배치: 부모 위치를 중심으로 서로 다른 미세 오프셋을 준다. 겹침은 `AgentSeparationSystem` 이
자연히 밀어낸다 — 셀 배분 로직을 새로 만들지 않는다.

### ④ 웨이브 회전과의 경계 — **이 단위의 가장 조용한 버그**

`Update` 순서가 `QueueDueWaves(t)` → … → `DrainEnemyKilledEvents()` 다. 부모 슬라임이 **마지막
생존 적**일 때 죽으면, 자식이 스폰되기 전에 `NoQueuedAttackersRemain()` 이 참이 되어 **다음 웨이브가
먼저 큐잉된다.** 그러면 «엘리트를 죽이면 오히려 판이 빨라지는» 뒤집힌 인센티브가 된다.

**계약: 분열 스폰은 웨이브 진행 판정보다 앞에서 관측돼야 한다.** 구현 선택지는 두 가지 —
드레인을 `QueueDueWaves` 앞으로 옮기거나, `NoQueuedAttackersRemain()` 에 «미처리 분열 있음» 항을
더한다. 어느 쪽이든 아래 완료 기준의 단언으로 증명한다.

### 하지 않는 것

- **세대 카운터·깊이 상한을 넣지 않는다**(계약 2). 자식 SO 에 메커니즘이 없어 재귀가 데이터
  구조상 불가능하다.
- **`maxPerWave` 를 분열에 적용하지 않는다.** 그 필드는 웨이브 생성 시점 축이다.

## 완료 기준

- [ ] compile 통과
- [ ] EditMode: `EnemyTriggerArmed(OnDeath) == true` · `OnShieldBreak` 는 **여전히 false**
- [ ] EditMode: `splitUnit == null` / `OnDeath × 다른 페이로드` 저작이 loud 거절·경고된다
- [ ] PlayMode 신규 e2e: 슬라임 1기를 죽이면 **정확히 2기**가 그 자리에 생기고, 체력이 부모
      최대치의 50% 이며, **그 자식을 죽여도 더 생기지 않는다**
- [ ] PlayMode 신규: 자식이 골 방향으로 **실제로 이동한다**(스폰만 되고 굳지 않는다 —
      `summon-patrol-defender` 가 겪은 «뷰가 제자리에 선다» 계열 회귀 방지)
- [ ] PlayMode 신규: **부모가 마지막 적일 때** 죽여도 자식이 스폰될 때까지 다음 웨이브가 큐잉되지
      않는다 (④ 계약)
- [ ] PlayMode 무회귀 — baseline 대비 실패 집합 동일. 특히 기존 킬 경로(각성·점수·데미지넘버)
- [ ] 신규 이벤트 채널 0 (`EnemyKilledEvent` 필드 append 만)
