# 거점 체계 — 마음 · 본능 (battle-structures)

상태: **완료 2026-08-10** — units 0~6(거점 체계) + units 8~11(공성 승패). 검증: EditMode **2063 / 실패 0 / 의도적 스킵 3** · PlayMode **5/5**
(rev 3 설계 확정 → 리뷰 F1~F7 → units 0~6 → Play → 리뷰 C-1/H/M/L 반영 → 계약 14 → units 8~11. 인계 = [`7_handoff_summary.md`](7_handoff_summary.md)(0~6) + [`12_handoff_summary.md`](12_handoff_summary.md)(8~11))
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
3. **침략 / 공성 모드** — 적 마음의 유무가 곧 모드다. **스폰 파생에는 분기 코드도 enum 도 없다**(§모드 판정). 승패도 모드 분기가 아니라 **축 구성**으로 낸다(계약 15, unit 10).
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
| 0 | 리팩터 | `0_faction_cross_bits.md` | `Faction` 교차 비트 재정의 + 그룹 상수 + 라이브 타워 `DefenderCore` 이관 + 잠자는 경로 정리(§결정 6 표) + 거점 특별취급 제거 | **있음** — 부작용 2건 해소 **+ 거점이 거리순 일반 후보가 됨**(계약 4) |
| 1 | 데이터 | `1_authored_target_mask.md` | 적 SO 저작 타겟 마스크 → `EnemyTargetFilter.factionMask` + 순수 derive + 베이크. 기본값 = 현행 동치 | **0** |
| 2 | 시뮬 | `2_taunt_scope_gate.md` | 도발 부착 게이트 = `(저작 & AnyUnit) != 0`. `EnemyAiStateSystem` 미러 점검 | 있음 |
| 3 | 저작·툴 | `3_structure_authoring.md` | `StructureData` SO + `MapDocument.structures[]` + MapPainter 브러시·검증·모드 배지 + `AllSpawnsReachGoal` 하한 완화 | 0 (저작 없으면) |
| 4 | 스폰·뷰 | `4_structure_spawn_and_view.md` | 거점 엔티티 스폰(마음/본능) + 3×3 점유·통행 차단 + KayKit 프랍 + 체력 게이지 + 배치 배제 영역 | 있음 |
| 5 | 시뮬 | `5_instinct_attack.md` | 본능에 `AttackState` + 투사체 1발. 저작 타겟 마스크 재사용 | 있음 |
| 6 | 모드 | `6_siege_mode_derivation.md` | 적 마음 존재 → `spawns[]` = 적 마음 셀. 런타임 분기 0 + 페인터 검증 | 있음 |
| 7 | 인계 | `7_handoff_summary.md` | 0~6 종료 요약 | — |
| 8 | 데이터 | `8_defender_authored_mask.md` | 방어 SO 저작 타겟 마스크(기본 `AnyEnemy`, `targetAllies` 오버라이드 유지) + `NeutralInstinct` 배치 배제 일반화 | **있음** — 적 거점을 때릴 수 있다 |
| 9 | 시뮬 | `9_aoe_pool_symmetry.md` | TileAoe 피해자 풀 + 진영 비트 필터(`GoalTowerTag` 특례 은퇴, splash·bounce 불변) | 있음 — TileAoe도 적 거점에 먹는다 |
| 10 | 판정 | `10_siege_resolution.md` | 공성 승패 **축**(적 마음 축 신설 + 타이머 축 비교식 통합) + 공성 전용 덱 저작 | 있음 — 승패 공식 |
| 11 | 검증 | `11_siege_live_verification.md` | 공성 라이브 1개(방어유닛→본능 교전 + 적 마음 파괴 승리). 구 후속 «본능 발사 라이브 검증» 흡수 | 0 |

⚠ unit 0 의 «행동 변화» 를 «부작용 2건 해소만» 으로 적었던 rev 3 초판은 **틀렸다**(리뷰 F1). 골 타워가 타겟팅에서 다르게 다뤄지므로 공성 체감이 바뀐다 — 최종 형태는 계약 4(거리순 일반 후보).

**0~2 가 축, 3~5 가 콘텐츠, 6 이 모드다.** 0~2 만 넣고 멈춰도 판은 정상 작동한다. 3~5 는 저작이 없으면 무해하고, 6 은 적 마음이 없으면 무해하다 — 각 구간이 독립적으로 멈출 수 있다.

**8~11 은 공성을 «성립» 에서 «승패» 로 옮긴다.** 0~6 이 끝난 시점에 적 거점은 **절대 무적**이었다 — 후보 풀은 열려 있는데(`AttackSystem:44`) 방어 측 마스크가 `EnemyUnit` 리터럴이고 광역 피해풀은 `AttackUnitTag` 였다. 8~9 가 그것을 열고 10 이 승패를 얹는다. 8~11 도 같은 성질을 유지한다 — **적 거점이 저작되지 않은 맵에서는 변화가 0** 이다(해당 비트를 가진 엔티티가 아예 없다).

이 구간의 범위 제약(2026-08-10 사용자 지시): **「적 마음을 공격할 수 있다」 + 「승패 공식이 달라진다」 외에 게임플레이가 달라지지 않는다.** 이 제약으로 «본능 광역 해금»(적에게 새 공격 수단)과 «`targetAllies` bool 은퇴»(마이그레이션 없이는 힐러가 적을 때린다)를 후속으로 되돌렸다.

## Feature-wide 계약

1. **부류 판정은 오로지 비트다.** 클래스·유닛 타입·이름에 종속되는 분기를 만들지 않는다. 술어는 `(mask & bits) != 0` 한 줄(placement-mask `PlaceableAt` 선례).
2. **저작 의도와 런타임 마스크를 분리한다.**
   - **저작 의도** = `EnemyTargetFilter.factionMask` — "이 적은 무엇을 노리는 놈인가". SO 소유, 전투 중 불변.
   - **런타임 마스크** = `AttackState.targetMask` — "지금 때릴 수 있는 것". 무기 유무·도발로 변한다.
   - **도발 게이트는 저작 의도를 읽는다.** 런타임 마스크를 읽으면 무기 없는 적(러너·스위프트, 현재 마스크 = 거점 단독)이 도발 불가가 되는 함정에 빠진다 — 도발이 나중에 유닛 비트를 OR 해주는 구조라 순환이다.
3. **도발 차단은 부착 1지점.** `AggroStateSystem` 의 `Aggroed` 부착 게이트에 술어를 더한다. 보스 면역의 선례를 그대로 따른다 — 소비 지점이 6곳이라 "붙은 것을 무시" 는 비싸다.
4. **거점은 타입으로 특별 취급하지 않는다. «최후순위» 계약은 폐기했다.** (2026-08-09 사용자 확정)
   - «거점 타입이 유닛 타입에 항상 우선/후순위» 라는 **전역 규칙을 두지 않는다.** 우선순위는 **공격자 쪽 저작**이 정한다 — «이 놈은 거점을 우선하나». 그 저작은 unit 1(`EnemyTargetFilter`)에 들어간다.
   - **저작이 같으면 거리순**이다. `targetMask` 에 들어온 후보는 종류를 묻지 않고 거리로 경쟁한다.
   - **정해진 타겟이 바뀌는 규칙은 `TargetPersistence` 가 소유한다**(target-persistence spec). 죽거나 사거리를 벗어나면 놓는다. 거점도 이 술어에 균일하게 걸린다 — goal-stability 리뷰 M3 의 «거점은 잠금 대상이 아니다» 예외도 함께 제거했다.
   - ⚠ **되돌리지 말 것**: `AttackSystem` 에 «거점이니까» 로 순위를 뒤집는 분기를 다시 넣지 않는다. 필요하면 저작 축으로 표현한다.
   - 폐기 경위: goal-stability unit 2 가 넣은 최후순위는 판정 키가 잠자는 `GoalPoint` 라 라이브에서 한 번도 발효되지 않았다. unit 0 이 그것을 비트 판정으로 살리자(리뷰 F1) 규칙 자체가 재검토돼 폐기로 결론났다. 힐러가 버퍼 없는 거점을 힐 대상으로 고르던 경로는 이 규칙이 아니라 **마스크가 `DefenderUnit` 단독**이라는 사실이 막는다(계약 2).
5. **모드는 파생이다.** 적 마음의 유무 → `spawns[]` 채우기. 저작 enum 없음, 런타임 분기 없음.
6. **마음 개수는 전역 불변식이 아니라 «공성 맵의 저작 규칙» 이다.** (2026-08-09 사용자 결정)
   - **공성 맵**: 방어 마음 정확히 1 · 적 마음 정확히 1. **멀티골 금지** — 페인터가 에러로 잡는다.
   - **침략 맵**: 방어 마음 1~4(현행 멀티골 그대로 승계) · 적 마음 0.
   - 본능은 양쪽 모두 맵당 N개.
   - 마음은 1칸(현행 골과 동일), 본능은 3×3.
   - 라이브 맵 실측: 골 2개 = Serpent·Twin·Zig, 나머지 6장은 1개. **콘텐츠 이관 0** — 이 규칙은 공성 맵에만 걸린다.
7. **거점은 각자 체력을 갖고, 각자 무너진다.** 현행 «타워 N개가 스칼라 1벌 공유»(`SyncGoalStabilityBars` 주석: *"값은 공유 1개라 두 바가 같은 숫자를 표시한다"*)를 엔티티별 `Health` 로 바꾼다.
   - (2026-08-10 unit 4 이행 정정) **미러 스칼라(`_goalStability`)는 유지한다** — 점수 tie-break·HUD·공개 API 가 읽는 «가장 위험한 골» 캐시로서. «걷어낸다» 는 «미러가 판정을 소유하지 않는다» 로 충족됐다(판정은 per-entity Health/부재). 붕괴 프레임의 미러는 0(방금 죽은 골), 다음 프레임부터 생존 골 최저 — 유출 전환은 미러 갱신 **뒤**에 연다(리뷰 A-M1: 제출값 순서).
   - 붕괴도 거점 단위다: 무너진 마음의 셀만 유출 지점으로 열리고 나머지는 그대로 선다. 이는 goal-stability 의 원설계(*"엔티티 존재 = 그 셀의 골이 살아있다. 붕괴 = 엔티티 파괴 — 별도 플래그 없음"*)로 **되돌아가는** 것이고, 공유 스칼라는 goal-tower-siege 의 단순화였다.
   - ⚠ **«`MovementSystem` 게이트를 되살리면 된다» 는 앞선 판단은 철회한다** (2026-08-09 리뷰 F3). 두 공성 기계는 **보완이 아니라 대안**이다. 게이트가 마음 셀에서 `PastGoalTag` 를 봉인하면 `UnitLifecycleSystem` 의 goal-reached 루프(`WithAll<PastGoalTag, AttackUnitTag>`)에 아무도 못 들어간다 → ⑴ `AttackState` 없는 Runner·Swift 가 파괴도 안 되고 안정도 피해도 못 줘 «필드에 적 0기» 판정을 영구히 막는 유령이 되고 ⑵ `GoalReachedEvent` 가 안 나가 붕괴 후 유출 처리(`evt.canSiege && _goalBreached`)도 죽는다. **라이브 공성 전체가 깨진다.**
   - ⚠⚠ **«가만히 두기» 는 선택지가 아니다 — 지뢰다.** `GoalPoint` 를 마음 태그로 흡수하면 게이트 쿼리가 **자동으로 타워를 잡아 저절로 깨어난다**(타워가 그 태그를 다니까). 그러면 위 파손이 unit 0 에서 그대로 터진다. → **unit 0 은 게이트 블록을 삭제한다**(`MovementSystem:63-66` + 소비처 + `GoalSiegeGateTests` 4개). 지운 코드는 git 이 갖고 있고, unit 4 가 그 커밋을 참조 구현으로 쓴다.
   - 거점 단위 붕괴의 구현은 **unit 4 에서 새로 짓는다.** 후보 둘: ⓐ 브리지의 전역 `_goalBreached` 를 «붕괴한 마음 셀 집합» 으로 확장(라이브 기계 유지, 최소 변경) ⓑ 게이트를 되살리되 `canSiege`/`GoalReachedMarker` 경로를 **같은 커밋에서 은퇴**(두 기계 중 하나만 남긴다). **한쪽만 켜는 중간 상태를 만들지 말 것.**
   - 현행 공유 풀은 분리 복도 컨셉과 어긋난다 — 두 골에 적이 나뉘어 붙으면 **한 바가 두 배 속도로** 깎인다. 거점 단위가 «각자 지킨다» 의도에 맞다.
8. **거점은 CC·모디파이어의 대상이 아니다.** `CcEffect`/`StatModifierSlot` 버퍼 미부여(현행 골 계약 승계). `CcApplySystem` 이 버퍼 부재를 전제하므로 거점 대상 CC 를 넣으려면 이 계약부터 재검토.
9. **중립은 비트만 예약한다.** 생산자·소비자 0. 술어가 «중립을 특별 취급» 하지 않는다.
10. **본능의 공격은 유닛과 같은 파이프라인**을 탄다 — `AttackState` + `AttackOutputElement` + `ProjectileRef`. 전용 공격 시스템을 만들지 않는다.
11. **`ProjectileTargetFaction` 은 통합하지 않는다** (2026-08-09 리뷰 F6). 투사체 피해풀 선택은 `Defender`/`Enemy` 2값 enum 이 별도로 소유한다(`ProjectileHitSystem` · `BattleBridge:7357`·`:3950` · `BossLeap:243` · `HealthThresholdSystem:246`). 이것은 «누구를 겨누나»(교차 비트)가 아니라 «어느 스냅샷 배열을 훑나»(성능/구조)라 축이 다르다.
    ⚠ 그래서 **저작 마스크와 피해풀이 갈릴 수 있다** — unit 5(본능 공격)는 본능의 `factionMask` 와 `ProjectileTargetFaction` 이 같은 대상을 가리키는지 **명시적으로 대조**하고, 어긋나면 저작 마스크가 정본이다. 이 대조를 unit 5 완료 기준에 넣는다.
12. **마음은 통행을 막지 않는다** (2026-08-09 리뷰 F7). 방어 마음은 적이 그 셀에 서야 공성이 성립하고, 적 마음은 스폰 셀이라 그 위에서 적이 태어난다. 통행 차단은 **본능 3×3 본체만**.
13. **버퍼 보유 = 다중셀 점유 선언** (2026-08-10 리뷰 C-1 정정으로 확립). `ObstacleLifetimeSystem` 은 `BlockingHazard` **컴포넌트**가 아니라 `BlockingHazardCellsBuffer` 자체를 기준으로 `blockedCells` 를 만든다 — 컴포넌트를 요구하던 시절엔 버퍼만 든 본능이 통행을 전혀 안 막았다. 같은 판정을 `MapConnectivity`·페인터 BFS 도 공유한다(본능 footprint = 벽, 마음 = 비차단). 본능에 `BlockingHazard` 컴포넌트를 달아주는 대안은 금지 — hazard-dead 루프가 그 컴포넌트로 분기해 붕괴가 가짜 `hazardSoIndex` 를 실은 `HazardDestroyedEvent` 를 쏘고, `Obstacle` 없이는 어느 사망 루프에도 안 걸려 영구 미파괴가 된다.
14. **거점 뷰는 맵 수명, 거점 엔티티는 판 수명이다** (2026-08-10 후속 2 / 리뷰 M-5). 프랍은 `BuildMapForBattle` 말미(`SpawnStructureViews`)에 서고 `TeardownGeneratedMap` 이 걷는다 — 9×9 배치 배제가 파생되는 그 시점에 «왜 막혔나» 가 화면에 있어야 하기 때문이다. 엔티티는 `StartBattle` 그대로.
    ⚠ 그래서 `DestroyStructureEntities` 는 뷰를 건드리지 않는다 — 걷어내면 `StartBattle`(스폰 전 파괴)이 배치 중 세운 프랍을 매번 날린다. 체력 게이지는 등록부(엔티티) 기반이라 전투 시작 전엔 안 뜨는 게 맞다(체력이 아직 없다).
15. **승패는 모드가 아니라 축으로 구성된다** (2026-08-10 사용자 지시). 종료 조건마다 독립 축을 두고 「저작된 상한 > 0 이면 이 축이 산다」로 켠다 — 이미 이 코드베이스의 패턴이다(`_timerDuration<=0` → 타이머 축 없음 · `StressLimit<=0` → 유출 축 없음 · `_goalStabilityMax<=0` → 골 타워를 안 세운다). 적 마음은 그 형태의 축 하나를 더한다.
    - **타이머 축의 비교식 하나가 두 경우를 통합한다**: 만료 시 `방어 잔여 ≥ 적 잔여` → 승리. 적 축이 비활성이면 적 잔여 = 0 이라 항상 참 = 기존 `victory_timeout` 동치. 침략 맵과 공성 맵이 같은 코드를 탄다. 동률은 승리(방어 게임의 «버틴다» 계약).
    - ⚠ 축의 활성 조건은 **「적 마음 엔티티가 없다」가 아니다** — 그러면 침략 맵이 첫 프레임에 승리한다. 「저작된 상한이 있었는데 지금 잔여가 0」이다.
    - ⚠ **`MapMode` 를 런타임에서 읽지 않는다.** 페인터 배지·저작 검증 전용이다. 승패에 모드를 읽으면 적 마음이 부서지는 순간 «공성 맵이 아니게» 되어 판정이 흔들린다.
    - **두 마음의 체력은 저작으로 맞춘다**(사용자 결정: 동일 체력 + 절대값 판정). 「덱 스칼라로 통일」은 기각 — 적 마음만 `StructureData.health` 를 무시하는 예외가 필요해져 순이득이 없다. 어긋남은 문서와 덱을 둘 다 아는 유일한 자리(`MapDocumentPool.Entry`)에서 **경고**로 잡는다(에러가 아니다 — 비대칭 체력도 난이도 저작일 수 있다).
    - **유출 축은 공성 맵 덱에서 0 으로 끈다.** 유출은 이미 `stabilityDamage` 로 안정도를 깎아 방어 마음 축에 흡수되므로 「N회」는 중복 규칙이다. 맵 풀 엔트리가 자기 덱을 들고 오므로(`BattleBridge:980`) 코드 분기 0.

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
| 데이터 SO | `Data/StructureData.cs` (신설) + `MapDocument.structures[]` (신설) | HP·kind(마음/본능)·footprint·프랍·공격 정의. 라이브 마음은 현재 HP 를 **덱**(`AttackDeck.goalStabilityMax`)에서 받고, **그 스칼라가 타워의 존재 조건**이다(`EnsureGoalTowers:4854` 가 `<=0` 이면 안 세운다). `ResetGoalStability` 는 three-minute-survival 계약 9(시계와 짝) + `_goalBreached`/미스로그 리셋도 겸한다 → 이관은 **unit 4** 사안(리뷰 F5, 구 «unit 3» 표기 정정) |
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
   (2026-08-10 unit 10 정정) 이 결정은 **본능에만** 남는다. **적 마음**의 붕괴는 승리 축이 된다(계약 15) — «연출·로그만» 이었던 것은 승패가 아직 설계되지 않았기 때문이다.
3. **적 본능은 무엇을 노리나** — 기본값: **`DefenderUnit` 만**(방어 유닛을 쏘는 포탑). 방어 거점까지 노리게 하려면 SO 마스크만 바꾸면 되므로 콘텐츠 튜닝 사안.
4. **방어 유닛이 적 거점을 때릴 수 있나** — 기본값: **아니다**(현행 `EnemyUnit` 마스크 유지). "각 모드별 컨텐츠는 이 스펙에서 다루지 않는다" 는 지시에 따라 공성 맵의 «적 마음 파괴» 는 범위 밖으로 둔다.
5. **저작 컬럼을 시트에 넣나** — 기본값: **아니다.** v1 은 SO 직접 저작. 부류는 스탯이 아니라 정체성이라 튜닝 대상이 아니다.
6. **잠자는 `GoalPoint`/goal-stability 엔티티 경로를 걷어내나** — **확정(2026-08-09 사용자 승인): 걷어내되 범위는 아래로 좁힌다.** 골이 두 벌인 채로 `StructureTag` 를 붙이면 "어느 골에 붙였나" 가 실제 버그가 된다. 단 **`goal-stability` 스펙 문서는 남긴다** — 왜 그 설계였고 왜 접혔는지가 이 spec 의 근거다.

   **자리마다 처분이 다르다.** 잠자는 경로는 «엔티티 2벌» 이 아니라 **«소비 기계 4쌍»** 이고, 쌍마다 라이브와의 관계가 다르다 — 미발효(F1) · 대안(F3) · 중복(F2) · 무생산(⑭). 기계적 일괄 치환 금지.

   ⚠ **핵심 규칙: `GoalPoint` 를 흡수하면 잠자는 소비처가 «전부 동시에» 깨어난다.** 라이브 타워가 그 태그를 다니까 쿼리들이 자동으로 타워를 잡는다 — 4쌍 4전패다. 그래서 unit 0 은 «타입 치환» 이 아니라 **«흡수 + 4자리 동시 처분»** 이고, 넷 중 하나만 빠뜨리면 라이브가 깨진다. 누락 검출은 `\bFaction\.Goal`·`GoalPoint` 그렙 공집합.

   | 자리 | 처분 | 이유 |
   |---|---|---|
   | `MapDocument.goalMaxStability[]` · `SpawnGoalEntities` | **삭제** | 라이브 스폰은 `EnsureGoalTowers` 하나 |
   | `GoalPoint` 타입 | **`StructureTag` 로 흡수** | 라이브 타워가 그 태그를 단다 |
   | 최후순위 판정 (`AttackSystem`) | **⚠ 규칙째 삭제** | 계약 4 폐기(사용자 확정) — 거점을 타입으로 특별 취급하지 않는다. 별도 `goalBest*` 트래커·폴백·M3 잠금 예외를 모두 제거하고 거리순 경쟁에 합류시킨다 |
   | `ProjectileHitSystem:108` 골 풀 + `:529~541` 합류 블록 | **⚠ 삭제 (치환 아님)** | `:98` defender 풀이 **이미** `WithAny<DefenderUnitTag, GoalTowerTag>` 다. 치환하면 타워가 두 풀에 들어 **광역 1발이 2번 때리고 `aoeTargetCap` 도 2칸 소모**. 단일 풀의 `WithAny` 에 마음 태그를 더하는 것으로 족하다 |
   | `MovementSystem:63-66` 공성 게이트 + `GoalSiegeGateTests` 4개 | **⚠ 삭제** | 태그 흡수만 하면 게이트가 **저절로 깨어나** 라이브 공성이 깨진다(F3). «가만히 두기» 불가. unit 4 가 git 에서 참조 구현으로 되살린다 |
   | `UnitLifecycleSystem:155~172` 골 사망 루프 | **⚠ 삭제** | 논박 ⑭. `GoalCollapsedEvent` 의 **유일한 생산자**이고 한 번도 발화한 적이 없다(라이브 타워는 `:180` 일반 루프에서 파괴된다 — 붕괴 이벤트 없이). 흡수하면 저절로 깨어나며, 페이로드가 `cell`·`goalIndex` 를 요구해 «unit 4 에서 일반화» 유보를 unit 0 으로 강제 소환한다 |
   | `GoalCollapsedEventsSingleton` | **채널 존치 · 생산자 삭제** | 타입과 브리지 수명주기·소비 측은 무변경. 생산자만 사라진다 = **오늘의 라이브 상태와 동일**. 거점 단위 붕괴를 짓는 unit 4 가 페이로드를 새로 정하고 이 채널이 제 자리를 찾는다 |
   | `BattleBridgeGoalStabilityTests` 4개 | **삭제** | 스폰 경로 전용. 계약은 unit 4 `SpawnStructureEntities` 승계 |
   | 골 테스트 20개 | **타입 치환 2~3줄** | 지우면 unit 0 이 무검증 |

   근거·오판 기록은 `7_handoff_summary.md` 논박 ⑪~⑭. 자리별 치환 판정 전수(21곳/8파일)는 [`0_faction_cross_bits.md`](0_faction_cross_bits.md) §2.

## 후속 후보

(리뷰 4~6 이관분 + 기존 항목. 취소선 = units 8~11 로 흡수되거나 완료된 것)

- ~~**배치 페이즈 거점 프랍 표시**~~ → **완료** (2026-08-10, `ebe4e47b`). 계약 14 로 승격 — 뷰를 맵 수명으로 옮겼다.
- ~~**NeutralInstinct 배치 배제**~~ → **unit 8 에 흡수** (같은 «진영 리터럴 → 비트 술어» 성격).
- ~~**본능 발사의 라이브 검증**~~ → **unit 11 에 흡수**. 방어유닛이 본능을 공격하는 방향은 검증됐다. 반대 방향은 배치 배제 여유와 본능 사거리가 같아 성립하지 않으며, 아래 현행유지 결정대로 의도된 저격 out-range다.
- ~~**공성 모드 콘텐츠**~~ → **unit 10 이 승패까지 담당** (사용자 규칙: 마음 HP 0 = 즉시 종료 / 3분 만료 = 절대값 우위). 점수 축은 손대지 않는다 — 현행 킬 점수 그대로.
- ~~**적 본능의 사거리 vs 배치 배제 여유**~~ → **현행유지로 결정** (2026-08-10 사용자). 「저격으로 out-range 하는 것이 정답」이 의도다.
  실측 기록: 배치 배제(체비셰프 ≤ 4)가 본능 사거리(`Structure_TestInstinct.attackRange = 4`)와 같아 최근접 합법 칸 거리 5 가 사거리 밖 → 본능은 `DefenderUnit` 단독을 노리는데 그 대상이 정의상 사거리에 못 들어온다. 반대로 사거리 6 저격수는 그 칸에서 본능을 깎는다(라이브 검증). 이것은 **모순이 아니라 배제 여유가 자기 목적을 달성한 상태**다 — 요청 7-2 의 목적이 «포탑 사거리 안에 세우는 것 방지» 였다. 조정이 필요해지면 `StructureData.attackRange`(SO, **코드 0**)를 5 초과로 올리면 그 즉시 교전이 열린다.
- **본능별 배제 여유** [S] · `HostileInstinctPlacementPadding = 3` 은 **코드 상수**(`StructurePlacement.cs`)라 전역이다. 위 결정 때문에 지금은 무해하지만, «사거리 긴 포탑은 배제도 넓게 / 짧은 건 좁게» 로 나누려면 `StructureData` 로 옮겨야 한다(사거리만 SO 라 지금은 사거리를 올려도 여유가 따라오지 않는다 — 두 값이 한 결정을 인코딩하는데 소유자가 갈려 있다).
- **본능 광역 투사체** [M] · TileAoe 계열은 통합 루프 요청이 `targetFaction` 을 안 실어 거부 중(M-10). host 진영 도출(BossLeap/패턴 선례)을 통합 루프에 넣으면 열린다.
  ⚠ **units 8~11 의 범위 제약으로 명시적으로 되돌린 항목이다** — 「적 마음을 공격할 수 있다 + 승패 공식」 어디에도 «적 본능이 광역으로 쏜다» 는 없다. **적에게 새 공격 수단을 주는 것**이다. 현재 loud warn + 무공격으로 안전하게 막혀 있고 저작 자산도 0.
- **`targetAllies` bool 은퇴** [S] · `DefenderUnitData.targetAllies` 를 `targetFactions` 마스크로 승격(축 하나로). unit 8 이 오버라이드로 남긴 이유: 승격하면 기존 힐러 에셋의 `targetAllies: 1` 이 죽고 새 필드 기본값(`AnyEnemy`)이 이겨 **힐러가 적을 때리기 시작한다**. 에셋 마이그레이션과 함께 해야 한다.
- **`GoalCollapsedEventsSingleton` 재정의** [S] · 생산자 0 존치 중. 거점 붕괴 알림이 필요해지면 페이로드를 `goalIndex` 에서 거점 식별로 재설계.

- **중립 진영 콘텐츠** [M] · 비트만 예약돼 있다. 중립 거점(양측이 다 때릴 수 있는 것)·중립 유닛의 규칙.
- **본능의 공격 메커니즘 다양화** [M] · v1 은 투사체 1발 고정. 유닛과 같은 파이프라인이라 패턴 SO(`projectile-emission-pattern`)를 그대로 붙일 수 있다.
- **거점 수복** [M] · 방어 측이 거점을 되살리는 축. 힐러의 거점 힐은 지금 **버퍼 부재 예외로 가는 경로**라 unit 0 에서 막힌다. 되살리려면 `IncomingHeal` 버퍼 + 회복 상한 규칙 필요(goal-stability 후속 후보 "골 힐" 과 같은 항목).
- **거점 전담 적의 예고** [S] · 스폰 예고 라인이 "얘는 못 막는다" 를 알려줄 자리(spawn-point-alert 후속 후보와 합류).
- **도발 면역의 다른 사유** [S] · 보스 면역이 지금 하드코딩 술어다. 사유가 셋을 넘으면 술어 단일 소스로 묶는다(`CcActionLock.IsBossImmune` 선례).
- **유닛 부류 세분화** [S] · `BlockingHazard` 는 거점도 유닛도 아닌 채로 남아 있다. 방벽을 «중립 거점» 으로 편입할지.
- **거점 footprint 일반화** [M] · v1 은 마음 1×1 · 본능 3×3 고정. 임의 footprint 는 `PropData` 가 이미 지원하므로 sim 쪽만 열면 된다.
