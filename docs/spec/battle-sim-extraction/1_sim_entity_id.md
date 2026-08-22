# 1 — SimEntityId: 매치 내 stable ID 도입

## 목적

`Entity.Index/Version`이 ① 타겟팅 동률 tiebreak(`NearestTargeting`·`FrontmostTargeting`·`LowestHealthTargeting`)과 ② **발사 패턴 RNG seed**(`AttackSystem`: `math.hash(int2(attackerEntity.Index, fireCountBase))`)에 직접 쓰인다. Entity 번호는 할당 순서 산물이라 신 sim(M1)에서 재현 불가 — 골든(unit 4) 생성 **전에** 매치 내 비재사용 `SimEntityId`(spawnOrdinal)로 축을 통일해야 A/B parity가 성립한다. 커맨드·이벤트·스냅샷·뷰 키의 유일 축이 될 ID이기도 하다.

**의도된 행동 변경**: 동률 해소 결과와 랜덤 탄막 시퀀스가 현재와 달라질 수 있다(규칙은 동일, 동률 승자·난수열만). 골든은 이 unit 이후를 기준선으로 삼는다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/SimEntityId.cs` — `IComponentData { int value }` (매치 시작 0부터 스폰 순 발급, 재사용 없음)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 유닛/투사체/해저드/캐리어 스폰 경로에서 ordinal 발급·부착, 매치 시작 시 카운터 리셋 (ECS 내부 스폰 시스템 — `ProjectileEmitterSystem`, `AttackSystem`의 캐리어 생성, `PickupSpawnSystem` 등 — 은 ECB 생성 시 부착)
- `Combat/NearestTargeting.cs` · `Combat/FrontmostTargeting.cs` · `Combat/LowestHealthTargeting.cs` — tiebreak 축을 `entityIndex/Version` → `SimEntityId` 교체
- `Combat/AttackSystem.cs` — `PatternShotRandomizer.Apply` seed를 `SimEntityId` 기반으로
- `Effects/HazardCastSystem.cs` — 최근접 타겟 선택(라인 83-88)에 tiebreak **신설**(현재 부재 — 동률 6지점 중 유일한 무-tiebreak 타겟팅)

## 구현

발급은 스폰 단일 지점(Bridge + ECB 생성부)에서만. `Entity.Index` 사용처는 위 목록이 전부인지 `grep -rn "\.Index" Battle/`로 전수 확인 후 교체(뷰/디버그 로그 용도는 제외 가능하되 목록화). EditMode 타겟팅 테스트의 기대값을 새 tiebreak 축으로 갱신.

## unit 1 에서 실제로 한 것 (2026-08-22)

### 발급

`SimEntityId { int value }`(Units 소유, `Unassigned = int.MaxValue`) 신설. 카운터는
`BattleBridge._nextSimEntityId` 하나이고 리셋 지점도 하나다 — `EnsureQueriesAndQueues`
(매치당 1회, `_ecsInfrastructureReady` 가드). **맵 빌드보다 앞**이라 «리셋 전에 이미
발급된 엔티티» 가 생길 수 없다는 것이 그 자리를 고른 이유다.

부착은 `AttachSimEntityId` 단일 통로로 **8지점**: 적 유닛 · 방어유닛 · 소환 순찰병 ·
골 타워 · 저작 거점 · 길막 해저드(`SpawnBlockingHazardWithVisual`) · 투사체.
정확히 **`FactionTag` 를 붙이는 6곳 전부 + 투사체**다 — 타겟 후보 아키타입
(`FactionTag + Health + LocalTransform`)의 정의가 그 집합이라 목록이 우연히 맞은 게 아니다.

### 부착하지 않는 것 (의도)

요청 캐리어 · 픽업 · 사직서 · 장판/토네이도/포탈 캐리어 · 싱글턴. 전부 타겟 후보도
난수 씨앗도 아니라 **지금 읽을 곳이 없다**. 이들은 ECS 내부(ECB) 스폰이라 발급하려면
카운터가 싱글턴으로 승격돼야 하는데, 그 비용을 소비자 없이 먼저 치르지 않는다.
M1 이 ID 를 이벤트·스냅샷 키로 쓰기 시작하는 시점이 승격 시점이다.

### 축을 갈아끼운 지점

| 지점 | 이전 | 이후 |
|---|---|---|
| `NearestTargeting` · `FrontmostTargeting` · `LowestHealthTargeting` | `entityIndex` → `entityVersion` 2단 | `simId` 1단 (유일·비재사용이라 2단이 불필요) |
| `AttackSystem` 후보 조립 3곳 + `PickFallbackTarget` | `Entity.Index/Version` | 스냅샷과 나란한 `targetSimIds[]` |
| `AttackSystem` 발사 패턴 seed | `hash(attackerEntity.Index, fireCountBase)` | `hash(attackerSimId, fireCountBase)` |
| `HazardCastSystem` 최근접 | **tie-break 없음**(스냅샷 순서 = 청크 배치) | `simId` 오름차순 — 동률 6지점 중 마지막 공백 |
| `BattleBridge.TryPickNearestEnemy` | `Entity.Index` | `SimEntityId` |
| `ThreatTable.Leader` | `Entity.Index` | **표의 자기 순서**(먼저 때린 쪽) — 아래 ⚠ |

### 허용 예외 (`Entity.Index` 잔존 2건, 둘 다 sim 로직 아님)

- `BattleBridge.Relocation.cs:279` — `Debug.Log` 문자열.
- `BattleBridge.cs` `DrainHazardRuntimeEvents` — `HazardLog.target_index` 텔레메트리 필드.
  (로그 스키마 변경은 이 unit 범위 밖. 리플레이 대조에 쓰려면 별도 결정.)

### 캡처가 드러낸 사실 2건

⚠ **`ThreatTable.Leader` 는 런타임 소비자가 0 이다.** blink 목적지 계산이 이 표를 떠났고
(`HealthThresholdSystem`) 누적(`Accumulate`)만 계속 돈다. 그래서 형제들처럼
`SimEntityId` 를 parallel 배열로 받게 하면 **인자를 채울 호출자가 없는 API** 가 된다.
대신 표 자신의 순서로 갈랐다 — 표는 find-or-append 로만 자라고 항목이 빠지지 않으므로
그 순서는 할당기가 아니라 시뮬이 소유한 사실이다. (은퇴 여부는 후속 후보.)

⚠⚠ **`StartedShotgun_*` 의 «정확히 1발» 은 계약이 아니라 난수 draw 였다.** 2번 탄의
random interval(6~18ms)이 마지막 틱 11ms 를 넘느냐로 갈리는 동전이었고, seed 축이
바뀌자 뒤집혀 드러났다. 계약(«START 가 성사됐으면 target 소실 후에도 쏜다»)은 그대로 두고
상한만 구조에서 유도해 `InRange(1,2)` 로 고쳤다(11ms / 최소 6ms → 추가 최대 1발).
같은 파일의 형제 테스트가 처음부터 범위로 쓴 이유가 이것이다.

### 테스트 픽스처

`StructureFixtures.NextSimEntityId()` 가 테스트 쪽 발급기다(프로세스 단조 증가 —
값이 아니라 «먼저 만든 쪽이 작다» 만 의미가 있다). `MakeGoalTower`/`MakeInstinct` 와
`DirectionalVolleyIntegrationTests` 의 유닛 픽스처가 쓴다. `GoalTowerArchetypeTests`
(브리지 산물 == 픽스처 산물)가 이 동기화를 구조로 잡는다 — 실제로 이번에 그 테스트가
먼저 빨개져서 픽스처 누락을 잡았다. 나머지 픽스처는 **의도적으로 미발급**이다:
그 테스트들의 단언은 동률 축에 의존하지 않고, `Unassigned` 는 맨 뒤로 밀리므로
기존 배열 순서 거동이 유지된다.

## 완료 기준

- [x] compile + EditMode 타겟팅/랭킹 테스트 통과(기대값 갱신 포함) — 전체 2564건 중
      실패 1건은 사전 실패(`UnitKitCatalogTests` 말파이트 desc 길이, 이 작업과 무관).
- [x] sim 로직의 `Entity.Index/Version` 직접 사용 잔존 0건(예외 2건 위에 명시).
- [x] Play 실측: 타겟 후보 아키타입 **미발급 0 · 중복 0 · id 연속**(1차 판 0..10,
      재시작 후 다시 0..10 — 매치 경계 리셋 확인), 투사체도 미발급 0, 콘솔 에러 0.
- [~] 「같은 시나리오 2회 Play 에서 타겟 선택·탄막 로그 동일」 — **여기서는 성립하지
      않는다.** 지금 Play 는 가변 dt 라 두 판의 프레임 경계가 애초에 다르고, 그 위에서
      로그를 대조하면 ID 축이 아니라 dt 를 재는 것이 된다. 이 unit 이 실제로 보장하는
      것(= 스폰 순서 → ordinal 이 함수다)은 위 실측 두 판으로 확인했고, 프레임 단위
      대조는 고정 스텝 하네스(unit 2) 위에서 골든(unit 4)이 판정한다.

확인 2026-08-22 · Play 검증은 `PrepareDraftMap → BeginPlacement → StartBattle` 스크립트 진입.
