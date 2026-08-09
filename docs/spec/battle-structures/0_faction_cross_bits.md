# unit 0 — Faction 교차 비트 + 잠자는 골 경로 정리

## 목적

`Faction` 을 «진영 × 종류» 교차 비트로 재정의하고, 라이브 골 타워를 `DefenderCore` 로 옮긴다. 동시에 goal-stability 가 남긴 **잠자는 소비 기계 3쌍**을 처분한다 — 쌍마다 라이브와의 관계가 달라 일괄 치환하면 라이브가 깨진다(README §결정 6, handoff 논박 ⑬).

**행동 변화: 있다.** 부작용 2건 해소 + **최후순위 신규 도입**. 「무변경 리팩터」가 아니다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/Faction.cs` — 교차 비트 + `Factions` 그룹 상수
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 타워 진영, 마스크 베이크, `SpawnGoalEntities` 삭제
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 최후순위 키, 힐러 게이트
- `Assets/_Project/Scripts/Battle/Combat/{TauntAttackGrantSystem,HealthThresholdSystem}.cs`
- `Assets/_Project/Scripts/Battle/Effects/{DefenderFieldSystem,ZoneApplySystem}.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — 골 풀 **삭제**
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 공성 게이트 **삭제**
- `Assets/_Project/Scripts/Battle/Units/UnitLifecycleSystem.cs` — 골 사망 루프 **삭제** + 일반 루프 `WithNone` 정리
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument*.cs` — `goalMaxStability` 축 삭제
- 테스트: `GoalTargetingPriorityTests`(라이브 케이스 신규) · 골 테스트 20개(타입 치환) · `GoalSiegeGateTests`·`BattleBridgeGoalStabilityTests` 삭제

## 구현

### 1. 비트 재정의

README §타겟 비트의 10비트 배치와 `Factions` 그룹 상수를 그대로 따른다. `Faction`/`FactionTag` **타입 이름은 유지**하고 헤더 주석에 «진영 × 종류 교차 비트» 를 명시한다.

`Faction.Goal`(`1 << 3`)이 `DefenderCore`(`1 << 3`)로 **같은 비트를 물려받는다** — 직렬화된 마스크가 있어도 의미가 보존된다.

그룹 상수 중 unit 0 에서 실제 소비처가 생기는 것은 `AnyStructure`(최후순위 키)뿐이고 `AnyUnit` 은 unit 2(도발 게이트)가 쓴다. 나머지는 분류 체계로서 선언만 — 술어를 위한 추상 레이어가 아니다.

### 2. 치환 지점 — 자리별 판정 (전수 21곳 / 8파일)

`\bFaction\.` 실측. **기계적 일괄 치환 금지** — 지원계가 넓은 비트를 받으면 버퍼 없는 거점에 append 해 힐러 결함과 같은 예외가 난다.

| 자리 | 현행 | → | 근거 |
|---|---|---|---|
| `BattleBridge:4870` 타워 태그 | `Defender` | **`DefenderCore`** | 이 unit 의 핵심 |
| `BattleBridge:872` 잠자는 골 태그 | `Goal` | **삭제** | `SpawnGoalEntities` 전체 제거 |
| `BattleBridge:5901` 방어유닛 태그 | `Defender` | `DefenderUnit` | |
| `BattleBridge:6143` 순찰 아군 태그 | `Defender` | `DefenderUnit` | |
| `BattleBridge:7408` 적 태그 | `Enemy` | `EnemyUnit` | |
| `BattleBridge:5908` 힐러 마스크 | `Defender` | **`DefenderUnit`** | ★힐러 결함의 근원. 넓히면 타워를 힐 대상으로 고른다 |
| `BattleBridge:5908` 일반 방어유닛 마스크 | `Enemy` | `EnemyUnit` | 결정 4 — 방어유닛은 적 거점을 안 때린다 |
| `BattleBridge:5950` 해저드 캐스터 | `Enemy` | `EnemyUnit` | |
| `BattleBridge:6150` 순찰 아군 마스크 | `Enemy` | `EnemyUnit` | |
| `BattleBridge:7454` 적 base 마스크 | `Defender\|BlockingHazard\|Goal` | **`DefenderUnit\|BlockingHazard\|DefenderCore`** | ★유일한 «유닛+거점». 타워 피격 가능성이 여기서 나온다 |
| `BattleBridge:7506` walk-only 적 마스크 | `Goal` | **`DefenderCore`** | ★«거점 전담 적» 의 현존 사례. 계약 4 가 공전하는 대상이고 unit 1 기본값의 근거 |
| `AttackSystem:456` 힐러 rank 게이트 | `== Defender` | **`== DefenderUnit`** | ★등가 비교. 이 한 줄이 힐러 결함을 끈다 |
| `AttackSystem:391` 순찰 소환 게이트 | `& Enemy` | `EnemyUnit` | 순찰은 거점을 못 때린다 |
| `AttackSystem:1780` 니들 후보 | `& Enemy` | `EnemyUnit` | 동상 |
| `HealthThresholdSystem:246` host 진영 | `== Enemy` | **`== EnemyUnit`** | ★등가 비교. 적 거점 host 는 unit 5 소관 |
| `TauntAttackGrantSystem:48·75·76` | `Defender` | `DefenderUnit` | 도발된 적은 가디언(유닛)만 본다 |
| `ZoneApplySystem:48` | `& Enemy` | `EnemyUnit` | `WithAll<PathFollowState>` 로 거점이 이미 자연 배제 |
| `DefenderFieldSystem:67` | `& Defender` | **`DefenderUnit`** | ★부작용 1(보스 사냥 필드에 타워) 해소 |
| `EffectSpawner:170` | `BlockingHazard` | **무변경** | 방벽은 거점이 아니다 |

주석 3곳(`BattleBridge:4841`·`:4927`, `DefenderUnitData:96`)은 새 이름으로 갱신.

### 3. 최후순위 키 이관 + 라이브 안전망

`AttackSystem:529` 의 `goalPointLookup.HasComponent(...)` → `(faction & Factions.AnyStructure) != 0`. `:113` 의 lookup 과 `:680` 의 잠금 배제도 같은 술어로.

⚠ **이 계약은 라이브에서 한 번도 발효된 적이 없다**(README 계약 4). 기존 `GoalTargetingPriorityTests` 는 `Faction.Goal` + `GoalPoint` 합성 엔티티를 써서 라이브 아키타입을 통과시키지 않는다.

**안전망은 «케이스 추가» 가 아니라 «아키타입 단일 소스» 로 세운다** — 이번 결함의 원인이 테스트 아키타입과 `EnsureGoalTowers` 아키타입의 drift 였기 때문이다. 케이스를 늘리면 다음에 또 갈린다.

→ 최후순위 테스트가 **`EnsureGoalTowers` 를 리플렉션으로 호출해 브리지가 실제로 만든 타워**를 대상으로 돌게 한다. 기법은 폐기 예정인 `BattleBridgeGoalStabilityTests`(`PrepareDraftMapInternal` → 실 ECS World 주입)에서 승계한다.

### 4. 삭제 — 잠자는 소비 기계 (치환 아님)

세 자리 모두 **현재 죽은 코드**라 삭제는 라이브 동작을 바꾸지 않는다.

- **`ProjectileHitSystem:108` 골 풀 + `:529~541` 합류 블록** — `:98` defender 풀이 이미 `WithAny<DefenderUnitTag, GoalTowerTag>` 다. 치환하면 타워가 두 풀에 들어 `inRangeEnts` 에 중복 등재되고(중복 제거 없음) **광역 1발이 2번 때리며 `aoeTargetCap` 도 2칸 소모**한다. `:98` 은 unit 0 에서 손대지 않는다(`GoalTowerTag` 존치 → 라이브 타워는 이미 잡힌다). 마음 태그 추가는 unit 4 몫.
- **`MovementSystem:63-66` 게이트 + 소비처(`:165~169`) + `GoalSiegeGateTests` 4개** — 방치는 선택지가 아니다. `GoalPoint` 를 흡수하면 쿼리가 타워를 잡아 **저절로 깨어나** goal-reached 루프(`WithAll<PastGoalTag, AttackUnitTag>`)를 봉인하고, `AttackState` 없는 Runner·Swift 가 유령이 된다. 거점 단위 붕괴는 unit 4 에서 새로 짓는다(README 계약 7 의 ⓐ/ⓑ).
- **`UnitLifecycleSystem:155~172` 골 사망 루프** — 논박 ⑬ 이 못 잡은 **4번째 쌍**이다(아래).
- **`SpawnGoalEntities` · `MapDocument.goalMaxStability[]` 축 · `BattleBridgeGoalStabilityTests` 4개** — 라이브 스폰은 `EnsureGoalTowers` 하나다.

### 5. 4번째 기계 쌍 — 골 사망 루프 (신규 발견)

`UnitLifecycleSystem` 에 사망 처리가 두 벌이다:

- `:155~172` **골 사망 루프** — `Query<GoalPoint, LocalTransform>.WithAll<DeadTag>` → `GoalCollapsedEvent` enqueue → destroy. `GoalPoint` 가 없으니 **한 번도 발화하지 않는다**. 즉 `GoalCollapsedEventsSingleton` 은 **생산자가 없는 채널**이다.
- `:180~188` **일반 사망 루프** — `WithNone<DefenderTile, BlockingHazard, GoalPoint>`. 라이브 타워는 이 세 조건을 다 피하므로 **여기서 파괴된다 — 붕괴 이벤트 없이.** 패배 판정은 브리지의 `_goalTowerCount` 비교가 따로 한다.

⚠ `GoalPoint` → `StructureTag` 흡수는 이 쌍도 **저절로 깨운다**(F3 와 같은 기제): `:156` 쿼리가 라이브 타워를 잡아 붕괴 이벤트가 처음으로 발화하고, `:184` 의 `WithNone` 이 타워를 일반 루프에서 빼낸다. 게다가 페이로드가 `cell`·`goalIndex` 를 요구하므로 «`GoalCollapsedEventsSingleton` 은 unit 4 에서 일반화» 라는 유보가 **unit 0 에서 강제로 앞당겨진다**(안 하면 컴파일이 막힌다).

→ **골 사망 루프도 삭제한다.** 다른 세 자리와 같은 근거다 — 오늘 죽은 코드라 삭제가 행동 중립이고, 거점 단위 붕괴는 unit 4 가 새로 짓는다(그때 이 채널이 제 자리를 찾는다). `GoalCollapsedEventsSingleton` **타입과 브리지 수명주기는 존치**(소비 측 무변경) — 생산자만 사라진다. 이는 오늘의 라이브 상태와 같다.

`GoalPoint` 타입은 `StructureTag` 로 흡수한다. `cell`·`goalIndex` 필드는 위 네 소비처가 모두 사라지므로 **unit 0 에서는 옮기지 않는다** — unit 4 가 거점 식별에 필요한 형태로 새로 정한다.

## 완료 기준

- [ ] 컴파일 에러 0 · 콘솔 신규 에러 0
- [ ] EditMode 전량 그린. 기준선 = 이 unit 직전 **2014개 / 실패 0 / 의도적 스킵 3**. 최종 수는 `2014 − 삭제분 + 신규분` 이고 **실패 0 · 신규 스킵 0**
- [ ] 최후순위 테스트가 `EnsureGoalTowers` 가 만든 타워를 대상으로 돌고, 사거리 내 방어유닛이 있으면 그쪽을 먼저 고른다
- [ ] 힐러 결함 회귀 테스트: 힐러(`targetAllies`)의 후보에 타워가 **들지 않는다**. 현행 코드로 돌리면 `IncomingHeal` 버퍼 부재 예외로 실패해야 한다(결함의 존재 증명)
- [ ] 보스 광역 1발이 타워에 **1회만** 데미지를 넣는다(F2 이중 피해 회귀)
- [ ] `AttackState` 없는 적(Runner·Swift)이 골 셀 도달 시 여전히 파괴되고 안정도 피해가 들어간다(F3 유령 회귀)
- [ ] `Faction.Goal`/`GoalPoint` 잔존 참조 0 (`\bFaction\.Goal`·`GoalPoint` 그렙 공집합)
- [ ] 리뷰: **`ecs-reviewer`** (ECS 시뮬 변경)
- [ ] 행동 변화를 커밋 메시지에 명시 — 부작용 2건 해소 + 최후순위 신규 도입(공성 체감 변화)
