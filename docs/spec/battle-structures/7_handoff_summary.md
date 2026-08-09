# 세션 인계 — battle-structures (착수 전)

rev 0 · 2026-08-09 · **구현 0줄. 설계만 확정.**

> 이 문서는 «종료 인계» 가 아니라 **«착수 인계»** 다. 코드는 한 줄도 없고, 커밋도 없다.
> 구현이 끝나면 이 파일을 종료 handoff 로 다시 쓴다.
> 길이가 권장(30~80줄)을 넘는 이유: *"handoff 만 보고 바로 진행"* 이 인계 조건이라, **논박 기록**(왜 이 형태가 되었나 · 무엇이 기각되었나)을 함께 담았다. 계약의 정본은 여전히 `README.md` 다.

## 0. 읽는 순서

1. **이 문서** — 왜 이 설계인가, 무엇이 기각되었나, 어디서 시작하나
2. `README.md` — 계약 10개 · 작업 단위 표 · 파이프라인 커버리지 (**계약의 정본**)
3. 착수할 unit 문서 (아직 없음 — unit 0 문서 작성이 첫 작업)

관련 스펙(읽을 필요가 생길 때만):
`goal-stability/` (원설계 — 왜 접혔나) · `goal-tower-siege/` (현행 라이브 골) · `aggro-targeting/` (도발 기계) · `placement-mask/` (비트 술어 선례) · `traversal-layers/` (통행 층, 미착수)

⛔ **`docs/blueprint/` 는 격리된 별도 트랙이다.** 이 스펙에서 참조하지 않는다(사용자 명시 지시).

## 1. 무엇을 만드나 (한 문단)

전투판의 모든 타겟을 **진영(방어·적·중립) × 종류(유닛·거점)** 교차 비트로 정의하고, 그 위에 거점 콘텐츠를 세운다. 거점은 **마음(Core)** 과 **본능(Instinct)** 두 종류. 이 축이 서면 ⑴ 도발이 «유닛을 노리는 적» 에게만 걸리고 ⑵ 3×3 을 차지하고 투사체를 쏘는 본능이 맵에 설치되고 ⑶ **적 마음의 유무만으로** 침략/공성 모드가 갈린다(enum·분기 없음).

## 2. 논박 기록 — 무엇이 기각되었고 왜

이 스펙은 초안이 세 번 뒤집혔다. **각 기각의 이유가 곧 계약의 근거**라, 모르면 같은 함정으로 되돌아간다.

**① 도발 게이트를 «런타임 targetMask» 로 판정 → 기각**
러너·스위프트는 무기가 없어 런타임 마스크가 «거점 단독» 이고, 도발이 **나중에** 유닛 비트를 OR 해주는 구조다. 런타임 마스크를 게이트로 쓰면 순환이 되어 이 둘이 영구 도발 불가가 된다.
→ **저작 의도(`EnemyTargetFilter.factionMask`, 불변)와 런타임 마스크(`AttackState.targetMask`, 가변)를 분리**하고 게이트는 저작 의도를 읽는다. (계약 2)

**② 코드 식별자 `DefenseMechanism` → 기각**
`Faction.Defender` 와 나란히 놓으면 비트 술어에서 눈으로 구분이 안 된다. **도발 게이트가 정확히 그 두 비트를 가르는 한 줄**이라 거기서 미끄러지면 조용히 틀린다. 현재는 `Core`/`Instinct` 로 대체돼 이 논박 자체는 해소됐지만 교훈은 유효하다 — **비트 이름은 술어 안에서 서로 구분되어야 한다.**

**③ "골은 `Faction.Defender` 다" 라는 초기 답 → 틀렸고, 조사 중 골이 두 벌임을 발견**
| | 라이브 | 잠자는 것 |
|---|---|---|
| 엔티티 | `GoalTowerTag` | `GoalPoint` |
| Faction | **`Defender`** | `Goal` |
| HP | `AttackDeck.goalStabilityMax`(1000) | `MapDocument.goalMaxStability[]` |
| 스폰 | `EnsureGoalTowers` | `SpawnGoalEntities` |

`goalMaxStability` 실측(맵 문서 **9장**): 8장은 **키 자체가 없고**, `MapDocument_Test` 만 `[0]`. → `GoalPoint` 는 런타임에 한 번도 생성되지 않는다. `Faction.Goal` 은 소비자 없는 예약석. 이 발견이 스펙 전제를 뒤집었다(unit 0 이 «리팩터» 에서 «골 두 벌 정리» 로 커짐).

**④ "goal-tower-siege 가 전용 비트를 뺀 건 잘못" → 아니다. 범위 차이다**
그 스펙이 필요했던 건 «적이 타워를 때릴 수 있다» 뿐이었고 `Defender` 비트가 그걸 공짜로 줬다. **그 범위에선 옳은 판단**이다. 이 스펙은 «어떤 적은 거점**만** 때린다» 를 요구하므로 그 분리가 전제다. 남의 결정을 «과설계» 로 되받지 말 것 — 요구가 달라졌을 뿐이다.

**⑤ 진영/종류를 마스크 2개로 분리 → 기각**
현재 타겟 술어는 전부 `(faction & mask) != 0` 한 줄이고 그런 자리가 **12곳 이상**이다. 축을 쪼개면 전부 두 줄이 된다. **교차 비트 1축 + 그룹 상수**로 두면 술어 모양이 하나도 안 바뀌고 «모두/각 타게팅» 이 마스크 리터럴이 된다(`DefenderCore | DefenderInstinct`). 진영이 3개를 넘으면 이 판단을 다시 볼 것(비트 폭발).

**⑥ 모드 저작 enum → 기각, 파생 채택**
`GeneratedMap.spawns[]` 소비처를 전수 확인했다 — **8곳 전부 «셀 좌표 목록» 만 본다**(웨이브 생성 `BattleBridge:7397` · 레인 인덱스 `:7390` · 예고 라인 `:1900` · 측면 분산 `:1972` · 배치 차단 `:1062` · `BoardVisualPlanBuilder:34` · `MapConnectivity:17` · `spawnStructureProp`). 따라서 **«적 마음이 있으면 빌드 시 `spawns[]` 를 그 셀로 채운다» 한 줄이면 8곳 무변경으로 공성이 성립**한다. enum 을 두면 «공성인데 적 마음 없음» 이라는 **표현 불가능해야 할 상태**가 생기고 맵툴이 그걸 화해시켜야 한다. 맵툴의 «모드 선택» 은 드롭다운이 아니라 **파생 배지 + 검증**이다.
⚠ 파생이 깨지는 조건: «적 마음 = 스폰지점» 이 규칙으로 고정되어야 한다. «공성인데 스폰은 별도» 가 필요해지면 그때 enum 을 넣는다. 지금은 아니다(제약 8).

**⑦ "마음은 진영당 1개" 를 전역 불변식으로 → 기각**
라이브 맵 실측: 골 2개 = Serpent·Twin·Zig, 나머지 6장 1개. 전역 불변식으로 두면 출하 맵 3장을 다시 저작해야 한다. → **공성 맵의 저작 규칙으로 축소**. 침략은 현행 멀티골 승계 → **콘텐츠 이관 0**.

**⑧ "마음은 논리 1개 · 몸 N개" 절충안 → 사용자 결정으로 폐기**
2026-08-09: *"공성 모드에서는 멀티골은 없다. 무조건 방어/적 각 1개의 마음과 N개의 본능."* 절충 불필요.

**⑨ 공유 체력 스칼라 유지 → 기각, 거점 단위 체력**
현행은 타워 N개가 스칼라 1벌을 공유한다(`SyncGoalStabilityBars` 주석이 직접 그렇게 적혀 있다). **두 골에 적이 나뉘어 붙으면 한 바가 두 배 속도로 깎여** 「분리 복도 각자 골」 컨셉과 어긋난다. 거점 단위 체력·거점 단위 붕괴는 **goal-stability 의 원설계로 되돌아가는 것**이고(«엔티티 존재 = 그 셀의 골이 살아있다»), 공유 스칼라는 goal-tower-siege 의 단순화였다.

**⑩ 조사 중 발견한 현행 부작용 2건** (어느 스펙 문서에도 없다 — `Defender` 비트에 딸려온 것)
1. **보스 사냥 필드에 골 타워가 들어간다** — `DefenderFieldSystem.cs:67` 이 `Faction.Defender` 로 필터.
2. **힐러가 골 타워를 힐 대상으로 고른다 — 타워엔 `IncomingHeal` 버퍼가 없다.** 힐러 후보 스캔(`AttackSystem:456`)이 `Faction.Defender` 마스크라 타워가 후보에 들고, 체력비 최저 우선이라 깎일수록 우선순위가 오른다. 성사되면 `ecb.AppendToBuffer(tower, IncomingHeal)` 이 playback 에서 던진다.
둘 다 unit 0(타워 → `DefenderCore`)으로 **함께 꺼진다.**

**⑪ "잠자는 경로를 지우면 골 테스트 6개가 함께 폐기된다" → 기각. 실제로 죽는 건 1개다** (2026-08-09 확인)

착수 전 확인에서 나온 판단이었으나 **테스트 파일을 열어 보니 전제가 틀렸다.** `BattleBridgeGoalStabilityTests` 만 잠자는 스폰 경로를 탄다(리플렉션 `PrepareDraftMapInternal` → `BuildFlowField` → `SpawnGoalEntities`). 나머지 5개 파일은 **합성 월드에 엔티티를 직접 만들어** 시스템만 돌린다 — `MapDocument` 도 `BattleBridge` 도 거치지 않는다:

```csharp
// GoalSiegeGateTests — 스폰 경로 무관. 타입 이름만 바뀐다.
_em.AddComponentData(goal, new GoalPoint { cell = new int2(4,0), goalIndex = 0 });
_em.AddComponentData(goal, new FactionTag { value = Faction.Goal });
```

| 테스트 | 24개 중 | 판정 |
|---|---|---|
| `BattleBridgeGoalStabilityTests` | 4 | **폐기** — 스폰 경로 전용. 계약(멱등·teardown·미저작 시 무스폰)은 unit 4 `SpawnStructureEntities` 로 승계 |
| `GoalSiegeGateTests` | 4 | ⚠ **⑬ F3 에서 «삭제» 로 정정됨** (초판 판정 «재조준 필수» 는 철회). 검증 대상 게이트 자체가 unit 0 에서 삭제된다 |
| `GoalTargetingPriorityTests` | 3 | **재조준 필수** — 최후순위 = 계약 4. **unit 0 작업 4번의 유일한 안전망** |
| `GoalTauntGrantTests` | 3 | **재조준 필수** — 마스크 OR/원복 = 계약 2 의 직접 선조. unit 2 가 이 영역 |
| `GoalProjectileTests` | 3 | **재조준** — 적 투사체가 마음을 맞힌다 / 방어 AoE 는 자기 마음을 안 때린다. 둘 다 살아있는 계약 |
| `UnitLifecycleSystemTests` | 6 중 2 | **부분** — 골 케이스 2개만. 나머지 4개는 라이브 경로. **파일 전체 폐기는 과함** |

**지우면 unit 0 을 무검증으로 하게 된다.** 재조준 비용은 파일당 2~3줄 타입 치환이다.

**⑫ 그리고 공성 기계가 두 벌이다** (⑪ 확인 중 발견, 2026-08-09)

`MovementSystem:63-66` 이 매 프레임 살아있는 골 셀 집합을 만들어 그 셀에서 `PastGoalTag`(유출)를 봉인한다:

```csharp
var aliveGoalCells = new NativeList<int2>(4, Allocator.Temp);
foreach (var goalPoint in SystemAPI.Query<RefRO<GoalPoint>>().WithNone<DeadTag>())
    aliveGoalCells.Add(goalPoint.ValueRO.cell);
```

`GoalPoint` 가 안 태어나니 **이 리스트는 라이브에서 항상 비어 있다.** 실제로 도는 공성은 goal-tower-siege 쪽 — `UnitLifecycleSystem:69` 의 `canSiege = attackStateLookup.HasComponent(entity)` → `GoalReachedMarker` → 브리지의 **전역 bool `_goalBreached`**(`BattleBridge:4931`).

**이것이 unit 0 의 판단을 바꾼다.** 계약 7 은 *"무너진 마음의 셀만 유출로 열리고 나머지는 그대로 선다"* 인데 전역 bool 로는 셀 단위를 표현할 수 없다.

> ⚠ **⑫ 의 결론 «쿼리를 마음 태그로 갈아끼우면 살아난다» 는 ⑬ 에서 철회됐다.** 두 기계는 보완이 아니라 대안이다. 아래 ⑬ F3 참조.

**⑬ 병행 세션 리뷰 (2026-08-09) — F1·F2·F3 전부 코드로 확인됨. unit 0 범위가 틀렸다**

⑪·⑫ 가 «골이 두 벌» 을 한 층 얕게 봤다. **엔티티 2벌이 아니라 «엔티티 2벌 × 소비 기계 3쌍»** 이고, 쌍마다 라이브와 잠자는 짝의 관계가 다르다.

| 소비 기계 | 라이브 | 잠자는 짝 | 관계 | 처분 |
|---|---|---|---|---|
| 타겟팅 최후순위 | 배제 없음(타워가 일반 후보) | `goalPointLookup` 배제 | **미발효** | 키 치환 = **신규 도입** |
| 공성 전환 | `canSiege` → `GoalReachedMarker` → 브리지 | `aliveGoalCells` → `PastGoalTag` 봉인 | **대안(상호 배타)** | 잠자는 쪽 **삭제**(방치 불가 — 아래) |
| 투사체 광역 풀 | `:98` `WithAny<DefenderUnitTag, GoalTowerTag>` | `:108` `GoalPoint` 풀 + 합류 | **중복** | 잠자는 쪽 **삭제** |

**F1 — 최후순위는 «유지» 가 아니라 «신규 도입» 이다.** 배제 키가 `goalPointLookup` 인데 라이브 타워는 `GoalTowerTag` 라 안 걸린다 → 지금 타워는 방어유닛과 **거리로 경쟁하는 일반 후보**다. 키를 옮기면 사거리에 방어유닛이 있을 때 적이 타워를 안 때린다 — **공성 체감이 바뀌는 게임플레이 변화**다. 그리고 `GoalTargetingPriorityTests` 는 `Faction.Goal` + `GoalPoint` 합성 엔티티라 **라이브 아키타입을 한 번도 통과시키지 않는다** — ⑪ 이 "유일한 안전망" 이라 부른 것이 실은 잠자는 경로만 검증한다. **라이브 아키타입 케이스 추가가 unit 0 완료 기준.**

**F2 — 투사체 골 풀은 치환이 아니라 삭제다.** `ProjectileHitSystem:98` 의 defender 풀이 **이미** `WithAny<DefenderUnitTag, GoalTowerTag>` 다(주석에 이력이 있다: *"보스의 AreaBarrage 가 골에 떨어져도 안정도가 한 톨도 안 줄었다"* 를 고친 자리). `:108` 골 풀은 goal-stability 가 **같은 일을 하려던 죽은 버전**이고, `:529~541` 에서 두 풀을 `inRangeEnts` 에 이어 붙이는데 **중복 제거가 없다**. `GoalPoint` → 마음 태그로 기계적 치환하면 타워가 두 풀에 다 들어 **광역 1발이 2번 때리고 `aoeTargetCap` 도 2칸 소모**한다. → `:108` 풀 + `:529~541` 합류 블록 **삭제**, `:98` 의 `WithAny` 에 마음 태그를 더하는 것으로 족하다.

**F3 — 게이트는 «되살리기» 도 «방치» 도 안 된다. unit 0 에서 삭제한다.** goal-reached 루프는 `WithAll<PastGoalTag, AttackUnitTag>` 다. 게이트가 마음 셀에서 `PastGoalTag` 를 봉인하면 아무도 그 루프에 못 들어간다 → ⑴ `AttackState` 없는 Runner·Swift 가 파괴도 안 되고 안정도 피해도 못 줘 «필드에 적 0기» 판정을 영구히 막는 **유령**이 되고 ⑵ `GoalReachedEvent` 가 안 나가 **붕괴 후 유출 처리도 죽는다**. 
⚠⚠ **«unit 0 에선 손대지 않는다» 는 초판 대응은 틀렸다 — 방치가 곧 지뢰다.** `GoalPoint` 를 마음 태그로 흡수하는 순간 게이트 쿼리가 **자동으로 타워를 잡아 저절로 깨어난다**(타워가 그 태그를 다니까). 즉 아무것도 안 해도 위 파손이 unit 0 에서 터진다.
→ **unit 0 은 게이트 블록(`MovementSystem:63-66` + 소비처)과 `GoalSiegeGateTests` 4개를 삭제한다.** 지운 코드는 git 이 갖고 있으니 unit 4 가 그 커밋을 참조 구현으로 쓴다.
거점 단위 붕괴 구현은 unit 4 결정(ⓐ `_goalBreached` 를 셀 집합으로 확장 / ⓑ 게이트 부활 + `canSiege` 경로를 **같은 커밋에서** 은퇴). **한쪽만 켜는 중간 상태 금지.**

**F4 — 그룹 상수 기계적 치환 금지.** `Faction.Defender` → `AnyDefender` 를 일괄로 밀면 지원계(힐·실드·버프·시너지)가 버퍼 없는 거점에 append 해 **힐러 결함과 같은 계열의 새 예외**를 만든다. 자연 방어가 있는 자리도 있다 — `ZoneApplySystem:45` 은 `WithAll<PathFollowState>` 라 거점이 애초에 안 걸린다. **unit 0 문서가 치환 지점을 자리별로 분류**해야 한다(«유닛만» / «유닛+거점» / «자연 배제»). 계약 1 은 «비트로 판정» 만 말하고 어느 비트를 넣을지는 말하지 않는다.

**F5 — 미러 스칼라 이관은 unit 3 이 아니라 unit 4 다.** `EnsureGoalTowers` 는 `_goalStabilityMax <= 0` 이면 타워를 아예 안 세운다(`:4854`) — **덱 스칼라가 타워의 존재 조건**이다. `ResetGoalStability` 는 three-minute-survival 계약 9(시계와 짝)에 묶여 있고 `_goalBreached`·`_towerMissLogged`·`_leakTypeMissLogged` 리셋도 겸한다. README 파이프라인 표의 «unit 3» 표기를 unit 4 로 정정.

**F6 · F7** — 계약 11·12 로 README 에 편입(투사체 진영 축 미통합 · 마음 통행 비차단).

**리뷰가 방어한 것** (되돌리지 말 것): spawns[] 8개 소비처 감사(줄 참조 5곳 전부 유효) · 1축 교차 비트 · 저작/런타임 마스크 2분 · 모드 enum 기각 · 중립 3비트 예약(제약 8 위반 아님 — 계약 9 가 «술어 특별취급 금지» 로 닫았다).

## 3. 확정된 사용자 결정

- 명칭: 거점 = `Structure` / 마음 = `Core` / 본능 = `Instinct`. (구 «방어기제» 폐기)
- 진영 3(방어·적·**중립은 정의만**) × 종류 2(유닛·거점), 거점은 마음/본능 2세부.
- 유닛은 현행 구현 그대로. 거점은 각자 체력.
- 본능: 3×3 점유 · 투사체 1발 고정(v1) · 비주얼은 KayKit Platformer Pack.
- 적 본능 3×3 + **주변 3타일**까지 배치 불가(= 9×9 `placeMask` 클리어).
- 모드: 침략(현행) / 공성(적 마음 = 스폰지점). **모드별 콘텐츠는 이 스펙 범위 밖.**
- 공성 맵은 진영당 마음 정확히 1개, 멀티골 없음. 침략은 현행 승계.

## 4. 미결 — 전부 기본값 확정, 착수를 막지 않음

| # | 항목 | 기본값 |
|---|---|---|
| 1 | 침략 멀티골 맵의 골당 체력 | 덱 값 그대로(총량 1000→2000). 단순 2배 아님 — 실측 후 조정 |
| 2 | 본능 파괴 시 효과 | v1 은 연출·로그만. 사격이 멎는 것 자체가 보상 |
| 3 | 적 본능의 타겟 | `DefenderUnit` 만(포탑). SO 마스크라 콘텐츠 튜닝 사안 |
| 4 | 방어 유닛이 적 거점을 때리나 | **아니다**(현행 `EnemyUnit` 유지). 모드별 콘텐츠는 범위 밖 |
| 5 | 시트 컬럼 | 넣지 않는다. 부류는 스탯이 아니라 정체성 |
| 6 | 잠자는 `GoalPoint` 경로 | **걷어내되 자리마다 처분이 다르다** — 정본은 **README §결정 6 표**(논박 ⑪~⑬ 반영본). goal-stability **스펙 문서는 남긴다**(이 스펙의 근거) |

⚠ 이 자리에 있던 «걷어낸다 / 살린다» 2열 표는 **논박 ⑬ 에서 폐기**됐다(F2 를 «살린다» 로, F3 을 «살린다» 로 잘못 분류했다). **README §결정 6 의 3열 표를 볼 것.**

## 5. ⚠ 검증되지 않은 주장 (그대로 믿지 말 것)

- **힐러 → 골 타워 `IncomingHeal` 예외**: **코드 경로만 읽었고 Play 재현은 안 했다.** 재현 조건 = 힐러(사거리 3, 3인 동시)를 골 3칸 이내 배치 후 타워를 깎는다. **unit 0 착수 시 이것부터 재현**하고, 재현되면 그 자체로 라이브 결함이므로 unit 0 커밋 메시지에 명시할 것.
- **보스 사냥 필드 오염**: 코드 확인만. 체감 영향은 미측정.
- 그 외 이 문서의 파일:줄 참조는 2026-08-09 기준. 병행 세션이 `AttackSystem` 을 활발히 고치는 중이므로 **줄 번호는 재확인**할 것.

## 6. unit 0 착수 지침

**첫 작업은 코드가 아니라 `0_faction_cross_bits.md` 작성이다** (1 파일 = 1 커밋 단위, 목적/변경 대상/구현/완료 기준 4섹션).

unit 0 이 실제로 하는 일:
1. `Faction` 을 «진영 × 종류» 교차 비트로 재정의 + `Factions` 그룹 상수(`AnyUnit`/`AnyStructure`/`AnyDefender`/…). 정확한 비트 배치는 README §타겟 비트.
2. 라이브 골 타워 `FactionTag` 를 `Defender` → `DefenderCore`.
3. 잠자는 경로 정리 — **README §결정 6 «자리마다 처분이 다르다» 표대로.** 기계적 일괄 치환 금지.
4. 최후순위 판정 키를 `GoalPoint` 보유 → `(faction & AnyStructure) != 0` 으로 이관(`AttackSystem:529`).
   **+ 라이브 아키타입(`Faction.Defender`+`GoalTowerTag`, `GoalPoint` 없음) 케이스를 `GoalTargetingPriorityTests` 에 추가** — 현재 테스트는 잠자는 아키타입만 통과시킨다(논박 ⑬ F1).
5. `ProjectileHitSystem:108` 골 풀 + `:529~541` 합류 블록 **삭제**. `:98` 의 `WithAny` 에 마음 태그 추가(논박 ⑬ F2). **치환하면 광역 2중 피해.**
6. `MovementSystem:63-66` 게이트 + 소비처 + `GoalSiegeGateTests` 4개 **삭제**(논박 ⑬ F3). **방치하면 태그 흡수로 저절로 깨어나 라이브 공성이 깨진다.** unit 4 가 git 에서 참조 구현으로 되살린다.
7. `Faction.Defender` → 그룹 상수 치환 지점을 **자리별로 분류한 표를 unit 0 문서에 넣는다**(«유닛만» / «유닛+거점» / «자연 배제»). 일괄 치환 시 버퍼 없는 거점에 append(논박 ⑬ F4).
8. **행동 변화를 정직하게 적는다** — «부작용 2건 해소만» 이 아니다. 최후순위 신규 도입(F1)이 공성 체감을 바꾸는 게임플레이 변화다.

리뷰: ECS 시뮬 변경이므로 **`ecs-reviewer`**.
테스트: 기존 EditMode 전량 그린이 최소 조건. `Faction` 리터럴을 쓰는 테스트가 깨지면 그것이 곧 영향 범위 목록이다.
**골 테스트 20개는 지우지 말고 타입만 치환한다**(논박 ⑪). 다만 그것만으로는 unit 0 이 검증되지 않는다 — 위 4번의 라이브 케이스 추가가 실질 안전망이다.

## 7. 워크트리 · 병행 세션 (중요)

- **같은 워크트리를 여러 세션이 공유한다.** 스테이징은 **경로 명시**로만. `git status -sb` 로 위치 먼저 확인.
- 현재 상태(2026-08-09 인계 시점): `main` ahead 2, `docs/spec/battle-structures/` **untracked**(이 스펙 전체가 아직 미커밋).
- **병행 세션이 `AttackSystem.cs` 를 활발히 고치고 있다** — 방금 `target-persistence` unit 0(`6ef701bf`)이 «공격 1회 타겟 커밋» 블록을 **어그로 sticky 바로 뒤 + `!aggroed` 게이트**로 넣었다. battle-structures unit 2 는 `AggroStateSystem`(부착 지점)을 건드리므로 직접 충돌은 없지만, **unit 0 의 `Faction` 리터럴 일괄 변경은 `AttackSystem` 전역을 스치므로 충돌 핫스팟**이다. 착수 전 `git pull` 필수.
- `git push` 는 **매번 사용자 승인 후**. GitLab 미러는 `git push gitlab main:refs/heads/master`(SSH만).

## 8. 되돌리면 안 되는 것

- **저작 의도 / 런타임 마스크 2분** — 되돌리면 무기 없는 적이 도발 불가가 된다(논박 ①).
- **도발 차단은 `AggroStateSystem` 부착 1지점** — 소비 지점이 6곳이라 «붙은 것을 무시» 는 비싸다. 보스 면역이 같은 자리에 같은 방식으로 있다.
- **모드는 파생** — enum 을 넣는 순간 표현 불가능해야 할 상태가 생긴다(논박 ⑥).
- **`Faction`/`FactionTag` 타입 이름은 유지** — 참조 40곳 이상, 리네임은 검증 질문과 무관(traversal-layers 가 `placeMask` 에 내린 것과 같은 판단). 헤더 주석으로 «진영 × 종류 교차 비트» 만 명시한다.
- **`goal-stability` 스펙 문서 삭제 금지** — 코드 경로는 걷어내되 문서는 이 스펙의 근거다.
