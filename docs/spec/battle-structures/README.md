# 거점 체계 — 마음 · 본능 (battle-structures)

상태: 초안 rev 3 — 설계 확정 · **구현 0줄** · 다른 세션으로 인계됨 (2026-08-09)
(rev 1 = 방어기제 명칭 / rev 2 = 골 두 벌 발견 / rev 3 = **원점 재설계** — 진영×종류 2축, 마음·본능, 침략/공성 모드)

> **이어받는 세션은 [`7_handoff_summary.md`](7_handoff_summary.md) 를 먼저 읽는다.**
> 왜 이 설계인지 · 무엇이 기각되었는지 · 어디서 시작하는지 · 무엇이 미검증인지가 거기 있다.
> 이 README 는 **계약의 정본**이다.

## 상위 목표

전투판의 모든 타겟을 **진영 × 종류** 두 축으로 정의하고, 그 위에 **거점(Structure)** 을 콘텐츠로 세운다.

```
진영:  방어 · 적 · 중립(정의만)
종류:  유닛 · 거점
거점:  마음(Core, 진영당 1) · 본능(Instinct, 맵당 N)
```

이 축이 서면 세 가지가 데이터로 열린다:

1. **도발은 «유닛을 노리는 적»에게만 걸린다** — 지금 무엇을 조준 중이든 도발한 가디언으로 전환(현행 sticky override 그대로). 거점만 노리는 적은 유인으로 못 막고 **죽여야만** 막힌다.
2. **본능** — 3×3 을 차지하고 투사체를 쏘는 거점. 맵마다 설치.
3. **침략 / 공성 모드** — 적 마음의 유무가 곧 모드다. **분기 코드도 enum 도 없다**(§모드 판정).
   - **침략** = 현행 게임플레이. 적 마음 없음, 스폰지점에서 웨이브가 나온다. 방어 마음 1~4(현행 멀티골 승계).
   - **공성** = 적 마음이 곧 스폰지점. **진영당 마음 정확히 1개**, 멀티골 없음. 양측 모두 본능 N개.

검증 질문: **거점이 «지형이 아니라 상대»가 되는가?** 지금 맵은 유닛만 상대한다. 거점이 체력·공격·타겟을 가지면 배치 판단이 "적을 어디서 막나" 하나에서 "무엇을 먼저 없애나"로 갈라진다.

## 명칭

| 개념 | 한글 | 코드 |
|---|---|---|
| 거점 통칭 | **거점** | `Structure` |
| 진영당 1개, 지켜야 할 것 | **마음** | `Core` |
| 맵당 N개, 공격하는 것 | **본능** | `Instinct` |

`Faction` / `FactionTag` **타입 이름은 바꾸지 않는다.** 참조가 40곳 이상이고 리네임은 이 spec 의 검증 질문과 무관하다(traversal-layers 계약 1 이 `placeMask` 에 내린 것과 같은 판단). 대신 헤더 주석에 «`Faction` 은 진영 × 종류 교차 비트다» 를 명시한다.

---

## 모드 판정 — 「마음 유무로 충분한가」

**결론: 충분하다. enum 도, 인터페이스도, 런타임 분기도 필요 없다.**

### 근거

`GeneratedMap.spawns[]` 의 소비처를 전수 확인했다 — **8곳이고 전부 «셀 좌표 목록»만 본다**:

| 소비처 | 무엇을 쓰나 |
|---|---|
| `BattleBridge:7397` `SpawnUnit` | 웨이브 적 생성 좌표 |
| `BattleBridge:7390` `EffectiveSpawnIndex` | 레인 인덱스 → `spawns.Length` 모듈로 |
| `BattleBridge:1900` 스폰 예고 라인 | 예고 경로 시작점 |
| `BattleBridge:1972` 레인 측면 분산 | `laneCount` |
| `BattleBridge:1062` `CloseCellLayers` | 스폰 칸 배치 차단 |
| `BoardVisualPlanBuilder:34` | 보드 시각 계획 |
| `MapConnectivity:17` | 스폰→골 연결성 검증 |
| `TilemapMapView` `spawnStructureProp` | 스폰 구조물 프랍 |

**「적 마음이 있으면 빌드 시 `spawns[]` 를 그 셀로 채운다」 한 줄이면 8곳 전부 무변경으로 공성 모드가 성립한다.** 웨이브도, 예고 라인도, 프랍도, 연결성 검증도 자기가 무슨 모드인지 알 필요가 없다.

### enum 을 넣으면 오히려 나빠진다

저작 enum 을 두면 «mode = 공성인데 적 마음이 없다» 라는 **존재할 수 없어야 할 상태**가 생기고, 맵툴이 그걸 화해시켜야 한다. 파생이면 그 상태 자체가 표현 불가능하다. 맵툴의 «모드 선택»(요청 6)은 **드롭다운이 아니라 파생 배지 + 검증**으로 구현한다 — 적 마음을 찍으면 배지가 «공성»으로 바뀌고, 그 순간부터 `spawns[]` 저작을 에러로 잡는다.

### 파생이 깨지는 조건 (기록만, 지금 대응 없음)

파생은 **«적 마음 = 스폰지점»이 규칙으로 고정**되어야 성립한다. 나중에 «공성인데 스폰은 별도» 가 필요해지면 그때 enum 을 넣는다. 지금 넣을 이유는 없다(제약 8 — "나중을 위한" 추상 레이어 금지).

### 파생이 만드는 검증 규칙 (맵툴)

모드가 파생이므로 맵툴은 «선택» 이 아니라 «판정 + 검증» 을 한다.

| 저작 상태 | 판정 | 검증 |
|---|---|---|
| 적 마음 0 | **침략** | `spawns[]` 1개 이상 필수. 방어 마음 1~4 허용(현행 멀티골) |
| 적 마음 1 | **공성** | 방어 마음도 **정확히 1** (멀티골 금지 — 에러). `spawns[]` 저작은 에러(파생이 채운다) |
| 적 마음 2+ | **에러** | 공성 맵의 마음은 진영당 1개 |

### 걸리는 것 하나

`MapConnectivity.AllSpawnsReachGoal` 이 `spawns.Length < 2` 면 **무조건 false** 다(`:17`). 적 마음 1개 = 스폰 1개인 공성 맵은 현재 검증을 통과할 수 없다. 이 하한을 1 로 완화한다(unit 3). 침략 맵은 실측상 전부 2개 이상이라 영향 없다.

---

## 타겟 비트 — 교차 비트 1축 + 그룹 상수

진영과 종류를 **별도 마스크 2개로 쪼개지 않는다.** 교차 비트 하나에 넣고 그룹 상수를 파생한다.

```csharp
[Flags] public enum Faction : int   // 진영 × 종류 교차
{
    None             = 0,
    DefenderUnit     = 1 << 0,   // 구 Defender
    EnemyUnit        = 1 << 1,   // 구 Enemy
    BlockingHazard   = 1 << 2,   // 그대로 — 방벽은 거점이 아니다
    DefenderCore     = 1 << 3,   // 구 Goal — 방어 마음(= 현행 골 타워)
    DefenderInstinct = 1 << 4,
    EnemyCore        = 1 << 5,   // 공성 맵의 적 마음 = 스폰지점
    EnemyInstinct    = 1 << 6,
    NeutralUnit      = 1 << 7,   // 정의만 — 생산자 없음
    NeutralCore      = 1 << 8,   // 정의만
    NeutralInstinct  = 1 << 9,   // 정의만
}

public static class Factions   // 파생 그룹 — 저작·술어가 읽는 이름
{
    AnyUnit      = DefenderUnit | EnemyUnit | NeutralUnit
    AnyCore      = DefenderCore | EnemyCore | NeutralCore
    AnyInstinct  = DefenderInstinct | EnemyInstinct | NeutralInstinct
    AnyStructure = AnyCore | AnyInstinct
    AnyDefender  = DefenderUnit | DefenderCore | DefenderInstinct
    AnyEnemy     = EnemyUnit | EnemyCore | EnemyInstinct
}
```

**왜 2축이 아니라 1축인가**: 현재 타겟 술어는 전부 `(faction & mask) != 0` 한 줄이고 그런 자리가 12곳 이상이다. 축을 둘로 쪼개면 그 12곳이 전부 «진영 체크 + 종류 체크» 두 줄이 된다. 교차 비트로 두면 **술어의 모양이 하나도 안 바뀌고**, 요청 4의 «모두/각 타게팅» 이 그냥 마스크 리터럴이 된다:

| 저작 의도 | 마스크 |
|---|---|
| 방어 유닛만 | `DefenderUnit` |
| 방어 거점 전부 | `DefenderCore \| DefenderInstinct` |
| 방어 마음만 | `DefenderCore` |
| 현행 일반 적 | `AnyDefender \| BlockingHazard` |

도발 게이트도 한 번의 `&` 다 — `(저작마스크 & AnyUnit) != 0`.

**진영이 3개를 넘으면** 이 판단을 다시 본다(비트 폭발). 지금은 진영 3 × 종류 3 = 9 비트로 상한이 사용자 결정에 의해 닫혀 있다.

---

## 선행 사실 — 골이 두 벌이다 (unit 0 의 실제 작업)

골 엔티티가 두 스펙에서 각각 생성되고 **살아 있는 쪽은 `Faction.Defender`** 다.

| | 라이브 | 잠자는 것 |
|---|---|---|
| 엔티티 | `GoalTowerTag` | `GoalPoint` |
| Faction | **`Defender`** | `Goal` |
| HP 출처 | `AttackDeck.goalStabilityMax` (1000) | `MapDocument.goalMaxStability[]` |
| 스폰 | `EnsureGoalTowers` | `SpawnGoalEntities` |
| 출처 | goal-tower-siege | goal-stability |

`goalMaxStability` 는 **전 맵 0**(`MapDocument_Test` 조차 `[0]`)이라 `GoalPoint` 는 런타임에 한 번도 생성되지 않는다. `Faction.Goal` 은 소비자 없는 예약석이다.

goal-tower-siege 가 전용 비트를 «rev 1 의 과설계» 로 제거한 판단은 **그 범위에선 옳았다** — 필요한 것이 "적이 타워를 때릴 수 있다" 뿐이었고 `Defender` 비트가 그걸 공짜로 줬다. 이 spec 은 "**어떤 적은 거점만** 때린다" 를 요구하므로 그 분리가 전제다.

**타워를 `Defender` → `DefenderCore` 로 옮기면 함께 꺼지는 현행 부작용 2건** (어느 스펙 문서에도 없다 — `Defender` 비트에 딸려온 것):

1. **보스 사냥 필드에 타워가 들어간다** — `DefenderFieldSystem.cs:67` 이 `Faction.Defender` 로 필터해 타워를 방어유닛 소스로 센다.
2. **힐러가 타워를 힐 대상으로 고른다 — 타워엔 `IncomingHeal` 버퍼가 없다.** 힐러 후보 스캔(`AttackSystem:456`, mask == `Faction.Defender`)이 타워를 후보에 넣고, 체력비 최저 우선이라 깎일수록 우선순위가 오른다. 성사되면 `ecb.AppendToBuffer(tower, IncomingHeal)` 이 playback 에서 던진다. 재현 조건 = **힐러(사거리 3)를 골 3칸 이내 배치**. ⚠ 코드 경로 확인만 했고 Play 재현 미확인 — unit 0 착수 시 먼저 재현할 것.

`DefenderUnitTag` 축 시스템(실드·배치·코스트·시너지·피로도)은 원래 타워를 보지 않는다 — goal-tower-siege 가 진영과 유닛 태그를 분리해 둔 덕이다. 영향 범위는 **`Faction.Defender` 를 직접 읽는 곳뿐**이다.

---

## 작업 단위

| # | 구분 | 문서 | 목적 | 행동 변화 |
|---|---|---|---|---|
| 0 | 리팩터 | `0_faction_cross_bits.md` | `Faction` 교차 비트 재정의 + 그룹 상수 + 라이브 타워 `DefenderCore` 이관 + 잠자는 `GoalPoint` 경로 정리 | 부작용 2건 해소만 |
| 1 | 데이터 | `1_authored_target_mask.md` | 적 SO 저작 타겟 마스크 → `EnemyTargetFilter.factionMask` + 순수 derive + 베이크. 기본값 = 현행 동치 | **0** |
| 2 | 시뮬 | `2_taunt_scope_gate.md` | 도발 부착 게이트 = `(저작 & AnyUnit) != 0`. `EnemyAiStateSystem` 미러 점검 | 있음 |
| 3 | 저작·툴 | `3_structure_authoring.md` | `StructureData` SO + `MapDocument.structures[]` + MapPainter 브러시·검증·모드 배지 + `AllSpawnsReachGoal` 하한 완화 | 0 (저작 없으면) |
| 4 | 스폰·뷰 | `4_structure_spawn_and_view.md` | 거점 엔티티 스폰(마음/본능) + 3×3 점유·통행 차단 + KayKit 프랍 + 체력 게이지 + 배치 배제 영역 | 있음 |
| 5 | 시뮬 | `5_instinct_attack.md` | 본능에 `AttackState` + 투사체 1발. 저작 타겟 마스크 재사용 | 있음 |
| 6 | 모드 | `6_siege_mode_derivation.md` | 적 마음 존재 → `spawns[]` = 적 마음 셀. 런타임 분기 0 + 페인터 검증 | 있음 |
| 7 | 인계 | `7_handoff_summary.md` | 종료 요약 | — |

**0~2 가 축, 3~5 가 콘텐츠, 6 이 모드다.** 0~2 만 넣고 멈춰도 판은 정상 작동한다. 3~5 는 저작이 없으면 무해하고, 6 은 적 마음이 없으면 무해하다 — 각 구간이 독립적으로 멈출 수 있다.

## Feature-wide 계약

1. **부류 판정은 오로지 비트다.** 클래스·유닛 타입·이름에 종속되는 분기를 만들지 않는다. 술어는 `(mask & bits) != 0` 한 줄(placement-mask `PlaceableAt` 선례).
2. **저작 의도와 런타임 마스크를 분리한다.**
   - **저작 의도** = `EnemyTargetFilter.factionMask` — "이 적은 무엇을 노리는 놈인가". SO 소유, 전투 중 불변.
   - **런타임 마스크** = `AttackState.targetMask` — "지금 때릴 수 있는 것". 무기 유무·도발로 변한다.
   - **도발 게이트는 저작 의도를 읽는다.** 런타임 마스크를 읽으면 무기 없는 적(러너·스위프트, 현재 마스크 = 거점 단독)이 도발 불가가 되는 함정에 빠진다 — 도발이 나중에 유닛 비트를 OR 해주는 구조라 순환이다.
3. **도발 차단은 부착 1지점.** `AggroStateSystem` 의 `Aggroed` 부착 게이트에 술어를 더한다. 보스 면역의 선례를 그대로 따른다 — 소비 지점이 6곳이라 "붙은 것을 무시" 는 비싸다.
4. **최후순위 계약 유지.** 사거리 내 유닛 후보가 있으면 그쪽이 먼저다. 판정 키를 `GoalPoint` → `(faction & AnyStructure) != 0` 으로 옮긴다. 거점 전담 적에게는 이 규칙이 공전한다(후보가 거점뿐).
5. **모드는 파생이다.** 적 마음의 유무 → `spawns[]` 채우기. 저작 enum 없음, 런타임 분기 없음.
6. **마음 개수는 전역 불변식이 아니라 «공성 맵의 저작 규칙» 이다.** (2026-08-09 사용자 결정)
   - **공성 맵**: 방어 마음 정확히 1 · 적 마음 정확히 1. **멀티골 금지** — 페인터가 에러로 잡는다.
   - **침략 맵**: 방어 마음 1~4(현행 멀티골 그대로 승계) · 적 마음 0.
   - 본능은 양쪽 모두 맵당 N개.
   - 마음은 1칸(현행 골과 동일), 본능은 3×3.
   - 라이브 맵 실측: 골 2개 = Serpent·Twin·Zig, 나머지 6장은 1개. **콘텐츠 이관 0** — 이 규칙은 공성 맵에만 걸린다.
7. **거점은 각자 체력을 갖고, 각자 무너진다.** 현행 «타워 N개가 스칼라 1벌 공유»(`SyncGoalStabilityBars` 주석: *"값은 공유 1개라 두 바가 같은 숫자를 표시한다"*)를 엔티티별 `Health` 로 바꾼다 — 각 타워가 이미 `Health` 를 갖고 있으므로 **미러 스칼라를 걷어내는 방향**이다.
   - 붕괴도 거점 단위다: 무너진 마음의 셀만 유출 지점으로 열리고 나머지는 그대로 선다. 이는 goal-stability 의 원설계(*"엔티티 존재 = 그 셀의 골이 살아있다. 붕괴 = 엔티티 파괴 — 별도 플래그 없음"*)로 **되돌아가는** 것이고, 공유 스칼라는 goal-tower-siege 의 단순화였다.
   - **구현체가 이미 있다** — `MovementSystem:63-66` 이 매 프레임 «살아있는 골 셀 집합» 을 만들어 그 셀에서 유출을 봉인한다. `GoalPoint` 가 안 태어나 라이브에선 항상 빈 리스트지만, **쿼리를 마음 태그로 갈아끼우면 계약 7 이 그대로 선다.** 라이브 공성(`canSiege` → 브리지 전역 bool `_goalBreached`)은 셀 단위를 표현할 수 없다. **이 게이트를 지우면 안 된다.**
   - 현행 공유 풀은 분리 복도 컨셉과 어긋난다 — 두 골에 적이 나뉘어 붙으면 **한 바가 두 배 속도로** 깎인다. 거점 단위가 «각자 지킨다» 의도에 맞다.
8. **거점은 CC·모디파이어의 대상이 아니다.** `CcEffect`/`StatModifierSlot` 버퍼 미부여(현행 골 계약 승계). `CcApplySystem` 이 버퍼 부재를 전제하므로 거점 대상 CC 를 넣으려면 이 계약부터 재검토.
9. **중립은 비트만 예약한다.** 생산자·소비자 0. 술어가 «중립을 특별 취급» 하지 않는다.
10. **본능의 공격은 유닛과 같은 파이프라인**을 탄다 — `AttackState` + `AttackOutputElement` + `ProjectileRef`. 전용 공격 시스템을 만들지 않는다.

## 배치·통행 배제 (요청 7-2)

적 본능 3×3 을 중심으로 **주변 3타일까지** 배치 불가. 즉 9×9 영역의 `placeMask` 를 0 으로.

- **배치 배제** = `placeMask` 클리어. 빌드 시 파생이며 저작본을 덮지 않는다 — `ObstaclePlacer.RederivePlaceMask` 와 같은 자리·같은 성격.
- **통행 차단** = 본능 3×3 **본체만**(주변 3타일은 통행 가능). 다중셀 점유는 `BlockingHazardCellsBuffer` 선례를 그대로 쓴다.
- **연결성 검증 필수** — 3×3 블로커가 스폰→골 경로를 끊을 수 있다. `MapConnectivity` 를 거점 적용 후 상태로 돌려 페인터가 에러로 잡는다.
- **traversal-layers 와의 관계**: 그 spec 의 rev 2 결정(«배치 가능 = 이동 가능»)이 서면 배치 배제 영역이 곧 방어유닛 이동 배제 영역이 된다. 이 spec 은 그 결정에 **의존하지 않는다** — `placeMask` 에 쓰기만 하고, 누가 읽는지는 그쪽 소관이다. 순서 의존 없음.

## 파이프라인 커버리지

가장 가까운 아키타입 = **목표지점 — 안정도 골** + **해저드 Blocking**(다중셀) (`docs/reference/object-pipeline-map.md`).

| 정거장 | 앵커 | 확인 포인트 |
|---|---|---|
| 데이터 SO | `Data/StructureData.cs` (신설) + `MapDocument.structures[]` (신설) | HP·kind(마음/본능)·footprint·프랍·공격 정의. 라이브 마음은 현재 HP 를 **덱**(`AttackDeck.goalStabilityMax`)에서 받는다 — 이관 여부는 unit 3 |
| 스폰 진입점 | `Bridge/BattleBridge.cs` `EnsureGoalTowers` 를 일반화 | ★Mono 주도. teardown = `DestroyEntitiesByType<StructureTag>` |
| ECS 컴포넌트 (Units) | `Battle/Units/StructureTag.cs` (신설) + FactionTag·Health·IncomingDamage·LocalTransform + `BlockingHazardCellsBuffer`(3×3) | `GoalTowerTag` 는 존치 — 패배 판정이 그 부재를 읽는다 |
| 시뮬 시스템 | `Combat/AttackSystem.cs`(최후순위 키·본능 공격) · `Units/DamageApplicationSystem.cs`·`HealthDeathSystem.cs`·`UnitLifecycleSystem.cs` · `Effects/ObstacleLifetimeSystem.cs`(다중셀 점유) | 마음은 공격 안 함 — `AttackState` 미부여. 이동 없음 — `PathFollowState` 미부여 |
| 이벤트 큐 | 신규 **0개** — 붕괴는 `GoalCollapsedEventsSingleton` 페이로드 일반화 검토(unit 4) | 채널 신설은 마지막 수단 |
| View/Pool | `Core/TilemapMapView.PlaceStructure` + `MapThemeData.goalStructureProp` 경로 재사용 | ★`PropData.footprintX/Y` 가 **이미 다중셀을 지원**한다(`prop-placement-layer`) — 3×3 에 새 기계 불필요. Pool N/A(맵 수명) |
| 비주얼 소스 | `Assets/KayKit/Packs/KayKit - Platformer Pack (for Unity)/Prefabs/` | 후보: `neutral/structure_A·B·C`, `red/cannon_base_red`, `pillar_2x2x*`. 진영색 세트(blue/red)가 있어 방어/적 구분에 그대로 쓸 수 있다 |
| 체력 표시 | 골 게이지(`_goalGaugeList`·`SyncGoalStabilityBars`) 재사용 | ★큐 아님 — Health read-only 폴링. 계약 7(엔티티별 체력)에 맞춰 미러 스칼라 제거 |
| 투사체 | 기존 `ProjectileRef` 경로 그대로 | 본능 전용 발사 코드 없음(계약 10) |
| 씬 wiring | BattleBridge 프랍/게이지/파괴 VFX 슬롯 | `unity-feature-wiring` 스킬. unit 4 |
| 배치 마스크 | 빌드 시 `placeMask` 배제 영역 파생 | 거점은 맵 저작물이지 플레이어 배치물이 아니다 |
| CC·모디파이어 | N/A + 이유 | 계약 8 |

## 리뷰 매칭

- unit 0·2·5 = ECS 시뮬 변경 → **ecs-reviewer**. unit 1·3(Data/Editor)·4(스폰+Mono 뷰)·6(빌드 파생) → 일반 리뷰.

## 결정 필요 (승인 시 함께)

기본값을 정해 뒀다. 다르면 말해 달라.

1. **침략 멀티골 맵의 골당 체력** — 계약 7(거점 단위 체력)로 바꾸면 골 2개 맵(Serpent·Twin·Zig)의 총 체력이 공유 1000 에서 골당 1000 = 2000 으로 는다.
   기본값: **골당 = 덱 값 그대로**(저작 없으면). 단순 2배가 아니다 — 적이 두 골로 나뉘면 각 골이 따로 깎여 실제 체감은 현행과 비슷하거나 어렵다(현행은 나뉘어도 한 바가 두 배 속도로 깎였다). **실측 후 조정** 항목이지 착수를 막는 결정이 아니다.
2. **본능이 파괴되면 무슨 일이 나나** — 기본값: **v1 은 연출·로그만**. 사격이 멎는 것 자체가 보상이다. 점수·버프·지름길은 별도 결정.
3. **적 본능은 무엇을 노리나** — 기본값: **`DefenderUnit` 만**(방어 유닛을 쏘는 포탑). 방어 거점까지 노리게 하려면 SO 마스크만 바꾸면 되므로 콘텐츠 튜닝 사안.
4. **방어 유닛이 적 거점을 때릴 수 있나** — 기본값: **아니다**(현행 `EnemyUnit` 마스크 유지). "각 모드별 컨텐츠는 이 스펙에서 다루지 않는다" 는 지시에 따라 공성 맵의 «적 마음 파괴» 는 범위 밖으로 둔다.
5. **저작 컬럼을 시트에 넣나** — 기본값: **아니다.** v1 은 SO 직접 저작. 부류는 스탯이 아니라 정체성이라 튜닝 대상이 아니다.
6. **잠자는 `GoalPoint`/goal-stability 엔티티 경로를 걷어내나** — **확정(2026-08-09 사용자 승인): 걷어내되 범위는 아래로 좁힌다.** 골이 두 벌인 채로 `StructureTag` 를 붙이면 "어느 골에 붙였나" 가 실제 버그가 된다. 단 **`goal-stability` 스펙 문서는 남긴다** — 왜 그 설계였고 왜 접혔는지가 이 spec 의 근거다.

   | 걷어낸다 | 살린다 — 재조준 |
   |---|---|
   | `MapDocument.goalMaxStability[]` 저작 축 | `MovementSystem` 셀 단위 공성 게이트 (계약 7 구현체) |
   | `BattleBridge.SpawnGoalEntities` | 최후순위 판정 (키만 `AnyStructure` 로) |
   | `GoalPoint` 타입 → `StructureTag` 흡수 | 도발 마스크 OR/원복 |
   | `BattleBridgeGoalStabilityTests` 4개 | 골 테스트 20개 (타입 치환 2~3줄) |

   `GoalCollapsedEventsSingleton` 은 **보류** — 계약 7 의 거점 단위 붕괴가 오히려 이 채널을 필요로 한다. unit 4 에서 페이로드 일반화로 재사용. 근거·오판 기록은 `7_handoff_summary.md` 논박 ⑪·⑫.

## 후속 후보

- **공성 모드 콘텐츠** [L] · 적 마음을 파괴하면 무엇이 바뀌나(스폰 정지·승리 조건·점수). 이 spec 은 «모드가 성립한다» 까지만.
- **중립 진영 콘텐츠** [M] · 비트만 예약돼 있다. 중립 거점(양측이 다 때릴 수 있는 것)·중립 유닛의 규칙.
- **본능의 공격 메커니즘 다양화** [M] · v1 은 투사체 1발 고정. 유닛과 같은 파이프라인이라 패턴 SO(`projectile-emission-pattern`)를 그대로 붙일 수 있다.
- **거점 수복** [M] · 방어 측이 거점을 되살리는 축. 힐러의 거점 힐은 지금 **버퍼 부재 예외로 가는 경로**라 unit 0 에서 막힌다. 되살리려면 `IncomingHeal` 버퍼 + 회복 상한 규칙 필요(goal-stability 후속 후보 "골 힐" 과 같은 항목).
- **거점 전담 적의 예고** [S] · 스폰 예고 라인이 "얘는 못 막는다" 를 알려줄 자리(spawn-point-alert 후속 후보와 합류).
- **도발 면역의 다른 사유** [S] · 보스 면역이 지금 하드코딩 술어다. 사유가 셋을 넘으면 술어 단일 소스로 묶는다(`CcActionLock.IsBossImmune` 선례).
- **유닛 부류 세분화** [S] · `BlockingHazard` 는 거점도 유닛도 아닌 채로 남아 있다. 방벽을 «중립 거점» 으로 편입할지.
- **거점 footprint 일반화** [M] · v1 은 마음 1×1 · 본능 3×3 고정. 임의 footprint 는 `PropData` 가 이미 지원하므로 sim 쪽만 열면 된다.
