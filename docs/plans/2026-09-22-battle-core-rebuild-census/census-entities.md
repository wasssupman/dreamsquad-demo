# 개체·수명주기 — 키워드 census

> READ-ONLY 조사. 모든 포인터는 grep 으로 존재 확인함.
> 대상 코드: `Assets/_Project/Scripts/Battle/Units/` · `Bridge/BattleBridge*.cs` · `Data/{DefenderUnitData,AttackUnitData,FootprintMath,StructureData,StructurePlacement}.cs` · `Battle/Effects/{Pickup,Resignation,BlockingHazard,EffectSpawner}.cs`
> 대상 문서: `docs/reference/battle-core-architecture.md` §1.1~1.5 · §2 · §3 · §8 / `docs/spec/README.md` Follow-up Backlog / 개별 spec README 후속 후보

| 키워드 | 한 줄 정의 (게임 언어) | 현행 구현 포인터 | 판정 | 애매하면: 결정 이력 + 미결 요지 |
|---|---|---|---|---|
| 방어유닛 | 코스트를 내고 판에 놓는 고정 개체 | `Data/DefenderUnitData.cs` · `BattleBridge.CreateDefenderEntity` | 확정 | |
| DefenderClass 5 | Ranger·Guardian·Fighter·Caster·Support. 버프 대상 축 + 적의 우선 조준 축 | `Data/DefenderClass.cs` · `Units/DefenderClassTag.cs` | 확정 | |
| DefenderRarity 4 | Common·Rare·Epic·Ego | `Data/DefenderRarity.cs` · `UI/Outgame/UnitRarityStyle.cs` · `UI/Draft/DraftCardFanView.cs` | 애매 | `docs/spec/unit-rarity-and-draft-rules/README.md`(완료 2026-05-07)의 근거는 드래프트 풀 10장 구성 규칙이었는데 편성이 스쿼드 로드아웃으로 바뀌었다. **sim 소비처 0** — 현행은 아웃게임 카드 테두리색·뱃지 텍스트·VFX 강도뿐 |
| footprint W×H | 유닛이 점유하는 칸 수(가로×세로). 배치 중 회전 없음 | `DefenderUnitData.Footprint` · `Data/FootprintMath.cs` | 확정 | 저작 실값은 2026-09-03 에 들어갔다(캐논 2×3 · 배스티온 3×2 · 버스터즈 1×2 · 나머지 2×2) |
| 앵커 + 크기 (대표 셀 은퇴) | 저장값은 점유 rect 의 min 코너와 크기 둘뿐 | `Battle/Units/DefenderFootprint.cs` | 확정 | 설계도 불변식 8 — 「이 유닛은 어느 셀에 있나」 재도입 금지. 짝수 변 footprint 는 중심 칸이 없다 |
| 몸 반경 = 가로/2 | 유닛의 몸은 원. 세로 깊이는 몸에 기여하지 않는다 | `DefenderUnitData.BodyRadiusTiles` | 확정 | rev 2026-09-04 사용자 결정(`min(W,H)/2` 내접원에서 개정). 근거 = 적은 정면에서 오고 유닛의 크기는 레인을 가로막는 폭 |
| 발밑 = sim 위치 | 유닛의 좌표는 하단 행 가로 중앙 | `FootprintMath.FootOffset` · `CreateDefenderEntity` 의 `fpBaseOff` | 확정 | 2026-09-03 베이스 통일. 사거리 원점·몸 원·자기중심 폭심·그림자·Y 소팅이 전부 이 한 점 |
| HitRadius 무조건 부착 | 모든 전투원이 몸을 들고 다닌다 | `Battle/Units/HitRadius.cs` | 확정 | 조건부 부착 금지 — 갈리면 판정이 데이터에 따라 두 갈래(불변식 7). bake 지점 4곳(방어유닛·적·순찰·거점) 전부 |
| maxOnBoard | 이 유닛이 판에 동시에 몇 기까지. 매치당 총 횟수가 아니다 | `DefenderUnitData.EffectiveMaxOnBoard` | 애매 | `defender-clock-out` 후속 후보 — 2기 이상 저작 시 **쿨타임 세탁**(런타임이 유닛 타입 키로 덮어쓰기)이 즉시 터진다. 라이브 에셋 전원 1이라 현재 미관측. 진입점은 `max(remaining, new)` 로 갈라야 함 |
| 배치·사망·퇴근 쿨타임 | 트리거는 셋인데 값은 둘. 퇴근은 사망의 **비율** | `deathCooldown` · `retireCooldownRatio` · `placementCooldown` | 확정 | 비율 파생이 계약 — 두 초를 독립 저작하면 인버전(퇴근 4초 / 사망 0초)이 생기고 화면에 안 보인다. `Clamp01` 이 진짜 방어선(시트 임포터가 `[Range]`·`OnValidate` 를 안 탄다) |
| 방어유닛 각성 보상 | 죽으면 각성치를 준다 — "죽음이 수입" | `DefenderUnitData.awakeningReward`(기본 4) | 확정 | 적과 달리 컴포넌트를 굽지 않는다. 브리지가 사망 드레인 시점에 SO 를 직독 |
| aggroCapacity | 가디언의 존재가 곧 도발 표식. 획득 범위 = 공격 사거리 | `DefenderUnitData.aggroCapacity` · `Effects/AggroCapacity` | 확정 | 구 `aggroRange` 폐기(근접 즉시 배정 산물). 획득 트리거 = 가디언의 공격 명중 |
| DeployedFacing | 방향 지정 유닛이 배치 때 1회 기록하는 고정 방향 | `Battle/Units/DeployedFacing.cs` | 확정 | 활성화 시 1회 쓰기, 이후 불변 |
| 적 | 웨이브가 스폰하고 마음을 향해 가는 개체 | `Data/AttackUnitData.cs` · `BattleBridge.CreateEnemyEntity` | 확정 | |
| EnemyClass 4 | Tanker·Runner·Bruiser·Shooter | `Data/EnemyClass.cs` | 애매 | SO 주석이 **"LABEL only, Behavior is NOT derived from this"** 를 명시. 행동은 `attackMethod`/`targetMode`/`engageMovement` 가 결정. 유일 런타임 소비처 = `WavePatternGenerator.cs:1079` 의 `slot.classFilter`. `enemy-class-system` 후속 후보 4개(Runner 도발·공격이동 정지·Shooter 2타입·웨이브 클래스 비율) 전부 미착수 |
| EnemyTier 3 | Normal·Elite·Boss | `Data/EnemyTier.cs` | 애매 | `elite-enemy-tier` unit 0. **런타임 술어는 `tier == Boss` 하나뿐**이고 `Elite` 값은 코드 소비자 0. 「저작 축에서 3등급이 보여야 한다」(2026-08-12 사용자 결정) 때문에 bool 이 아니라 enum. `stabilityDamage` 와 서로 검증하지 않는다(독립 축) |
| 크기 티어 → 몸 반경 | 소 0.25 / 중 0.5 / 대 1.0 / 보스 개별 저작 | `AttackUnitData.BodySize` · `BodyRadiusTiles` | 애매 | `distance-based-range` unit 13. `Large` 는 **예약**(오늘 소비 0) — 첫 대형 적이 올 때 코드 무변으로 켠다. `bodyRadius` float 은 Boss 티어에서만 읽힌다. 시트에 두 컬럼 다 없어 SO 저작으로 끝 |
| 보스 특권 | CC·어그로 면역 + 위협 귀속 + 등장 경보 | `BattleBridge.BakeNightmareMechanics` · `Combat/BossTag` · `CcActionLock.IsBossImmune` | 확정 | 출처가 `tier == Boss` 로 일원화됨. 그 앞까지는 「`nightmareMechanics` 가 비어있지 않으면 곧 보스」였고 그래서 엘리트를 만들 수 없었다 |
| detectionRange | 몇 칸 안의 방어유닛을 발견하면 경로를 벗어나 달려드는가 | `AttackUnitData.UsesDetection`/`HasUnlimitedDetection` · `Combat/DetectionRange`·`DetectedTarget` | 확정 | `enemy-detection-range` 완료 2026-09-08(units 0~9). 0=없음 / >0=반경 / <0=무제한. 구 `huntsDefenders` bool 을 흡수. 임계 비교(`MinDetectionRange` 0.05)를 쓰는 이유는 0.001 저작이 `!= 0` 을 통과해 헛 태그를 붙이기 때문 |
| DefenderHunterTag | 「무제한 사냥꾼」 전용 태그 — 공용 사냥판을 켜는 스위치 | `Combat/DefenderHunterTag.cs` | 확정 | unit 8 rev 에서 **다시 좁아졌다**(유한 감지는 대상 지향 추격판으로 이사). 살아있는 소비처 3곳뿐 |
| stabilityDamage | 마음을 못 때리는 돌격형이 도달했을 때 꽂는 한 방 | `AttackUnitData.stabilityDamage`(돌격형 50) | 확정 | `heart-stress-axis` unit 0 rev 2. 소비 조건 = `canSiege == false`. **마음을 조준할 수 있는 적은 이 값을 안 쓴다**(공성형에서는 inert). 마음 HP 를 1000→1500 으로 올릴 때 이 값은 일부러 같이 안 올렸다 |
| 분열 (SplitOnDeath) | 죽은 자리에 자식이 태어난다 | `BattleBridge.cs:11048` `SpawnSplitChildren` · `Data/SplitChain.cs` | 확정 | 첫 슬롯만(v1, OnDeath 폭발 선례와 같은 규약) · `MaxSplitChildren = 8` · 사슬 깊이 8/총 32 검증 |
| nightmareMechanics | 적이 갖는 트리거 × 페이로드 — 카드와 **같은 어휘** | `AttackUnitData.nightmareMechanics` · `Combat/DcTriggerSlot` | 확정 | 필드 이름이 실제 범위보다 좁다(보스 전용 아님 — 엘리트도 갖는다). 리네임 안 하는 이유 = 라이브 에셋의 YAML 키 |
| minWaveNumber / maxPerWave | 언제부터 나오나 / 한 웨이브에 몇 마리까지 | `AttackUnitData` | 확정 | seed 생성 경로(`WavePatternGenerator.Generate`)에만 적용, 저작 플랜은 디자이너 명시 배치를 존중 |
| traversalLayers | 이 개체가 **지날 수 있는** 층 | `EffectiveTraversalLayers`(방어·적 공통) | 확정 | 타입이 `PlacementLayer` 인 것은 의도(셀과 같은 비트 공간 재사용). 리네임은 참조 40곳 넘어 후속 후보. **배치 층과 다른 축** — 스폰·골 칸은 배치가 닫히고 통행은 열린다 |
| flightLift | 비행 적의 상시 화면 높이. 이동·타게팅 규칙에 관여하지 않는다 | `AttackUnitData.flightLift` | 확정 | 규칙은 `traversalLayers = Air` 가 소유 |
| 순찰 소환물 | 아군인데 **이동하는 유일한 개체**. 소환사의 담당 구역(= 소환사 사거리)을 순찰 | `Data/Abilities/SummonPatrolAbility.cs` · `BattleBridge.cs:8737` `CreatePatrolEntity` | 애매 | `summon-patrol-defender` 기능은 완료했으나 **검증 질문이 미판정으로 종료**: *"소환사를 뒤에 두고 순찰병을 앞세우는 것이, 타일에 유닛을 직접 놓는 것과 다른 배치 결정을 만드는가?"* — backlog 가 「순찰 콘텐츠를 이어서 만들기 전에 이 답부터 받는다」고 명시 |
| 소환물의 배제 | 배치 점유·각성·사직서·죽음 보상을 **갖지 않는다** | 설계도 §1.1 · `CreatePatrolEntity` 가 `CreateDefenderEntity` 를 재사용하지 않는 이유 | 확정 | `DefenderFootprint` 가 없어서 일반 사망 루프로 떨어지는 것이 배선상의 표현 |
| SummonedBy | 소환사가 죽으면 소환물도 죽는다 | `Battle/Units/SummonedBy.cs` · `PatrolLifecycleSystem.cs` | 확정 | 디버그 스폰(주인 없음)에는 미부착 = 연쇄 소멸 대상 아님 |
| 거점 | 안 움직이고 맞는 개체. 유닛 태그가 없어 배치·카드·코스트 규칙에 안 걸린다 | `Units/StructureTag.cs` · `Units/GoalTowerTag.cs` · `Data/StructureData.cs` · `BattleBridge.cs:6454` `SpawnStructureEntities` | 확정 | 마음 = 두 태그 다, 본능·적 마음 = `StructureTag` 만. 진영은 SO 가 아니라 **배치가 정한다**(`StructurePlacements.DeriveFaction`) |
| 마음 HP = 덱 소유 | 골 타워 체력은 유닛 SO 가 아니라 덱이 준다 | `Data/AttackDeck.cs` `goalStabilityMax`(라이브 1500) | 확정 | 본능 HP 는 `StructureData.health` |
| 마음이 몇 개인가 | 맵당 골 1~4 기계는 살아 있는데 판정은 1개 전제 | `_generatedMap.goals[]` · `SpawnStructureEntities` 의 `count > 1` LogWarning | 애매 | `multi-goal-map` 완료 ↔ `heart-stress-axis` 명제 10(마음 1개 전제, 종료는 **첫** 마음 파괴). 하드 에러를 안 둔 것은 의도(멀티골 기계를 건드리는 건 `map-rework` 계약 3 소관). 사용자 결정은 이미 나와 있다 — 「마음 N개가 체력을 공유」(2026-08-25) = `heart-stress-axis/12_shared_heart_pool.md`, **작성됨·착수 전** |
| CoreShielded | 본능이 살아 있는 동안 마음은 조준 후보에서 **빠진다**(막는 게 아니라 시선을 돌린다) | `Battle/Units/CoreShielded.cs` | 확정 | 소비처 **6곳**이 파일 주석에 표로 박혀 있다(조준 2 · 경로 1 · 예고 1 · 부수 피해 1 · 도달 1). 하나라도 빠지면 규칙이 샌다. writer 는 `BattleBridge.SyncGoalStability` 하나 |
| 거점 금지 계약 | 거점에 `ModifierStats`·`StatModifierSlot`·`ShieldSlot` 을 붙이지 않는다 | `Units/GoalTowerTag.cs` 주석 | 확정 | `IncomingHeal` 하나만 의도적으로 열렸다(`heart-stress-axis` unit 2 — 악몽을 잡을수록 마음이 회복). 근거: 원 금지의 명분은 `Health.max` 재계산인데 그건 `ModifierStats` 전용 |
| 거점 footprint | 마음 1×1 · 본능 3×3 고정. 몸 반경은 그 점유에서 파생 | `Data/StructurePlacement.cs` `FootprintOf`/`BodyRadiusOf` | 애매 | `battle-structures` 후속 후보 [M] — 임의 footprint 일반화 미착수. **진영이 크기를 정하므로 SO 가 알 수 없는 구조**(`PropData` 는 이미 지원, sim 쪽만 열면 됨) |
| 투사체(개체로서) | 발사 뒤 자기 수명을 사는 엔티티. `SimEntityId` 를 받는다 | `BattleBridge.DrainProjectileSpawnRequests` · `BattleBridge.cs:5612` `AttachSimEntityId` | 확정 | 궤적·페이로드는 별도 도메인 |
| 차단형 해저드 | 통행을 막는 체력 있는 방벽 | `Battle/Effects/BlockingHazard.cs` · `BlockingHazardSO` | 확정 | 아키타입이 골 타워와 동형. `decayPerSec` 노후화도 「그냥 피해」로 처리해 **죽음의 문을 하나로 유지**(계약 4) |
| 존형 장판 | 칸 기반 지속 효과(모양 × 효과 목록 × 수명) | `HazardSO` · `HazardSpawnRequestsSingleton` | 확정 | |
| 필드 캐리어 | 위치를 가진 규칙 개체 — 아군 버프장 · 당김장(토네이도) · 포탈 링크 | `Battle/Effects/EffectSpawner.cs` | 확정 | **`SimEntityId` 를 받지 않는다**(타겟 후보도 난수 씨앗도 뷰 키도 아님) |
| 픽업 | 밟으면 소비되는 일회성 개체(레드불) | `Battle/Effects/Pickup.cs` · `PickupConsumeSystem` | 확정 | 미소비 시 `remainingLife` 만료로 despawn |
| 사직서 | 배치 유닛이 죽으면 그 칸에 떨어지고, **전역 임계**에서만 소모 | `Battle/Effects/Resignation.cs` · `ResignationDropSystem`/`ResignationThresholdSystem` | 확정 | `season-gimmick-clockout` unit 8 재설계로 「강제 퇴근」이 아니라 「사망 드랍」이 됐고 환급 채널(`ClockOutRefundEvents`)은 은퇴. 유닛이 줍지 않는다 = 픽업과 다른 아키타입 |
| FactionTag | 편 × 종류 **교차 비트**(Unit/Core/Instinct + BlockingHazard) | `Scripts/Skills/Faction.cs`(namespace 는 `Wassup.Battle.Units`) · `Units/FactionTag.cs` | 애매 | 축을 쪼개지 않은 근거는 술어가 `(faction & mask) != 0` 한 줄이고 그런 자리가 20곳 이상이기 때문. **중립 3비트는 생산자·소비자 0 예약** — `battle-structures` 후속 후보 [M] 「중립 진영 콘텐츠」. `BlockingHazard` 는 거점도 유닛도 아닌 채로 남아 있다(같은 backlog [S]) |
| Health {value, max} | 체력. 쓰기는 Units 맥락만 | `Battle/Units/Health.cs` | 확정 | `ComputeRatio` 가 이벤트 페이로드와 브리지 틴트 폴링이 공유하는 단일 정의 |
| IncomingDamage + source | 피해 인박스. `source` 가 킬 귀속 축 | `Battle/Units/IncomingDamage.cs` | 확정 | DoT·배치 효과·환경 피해는 `Entity.Null` = 미귀속 |
| IncomingHeal | 힐 인박스. **적에겐 없다** | `Battle/Units/IncomingHeal.cs` | 확정 | 재생(`regenPerSec`)은 이 버퍼를 안 지난다 — `DamageApplicationSystem` 이 스탯을 직접 읽는다 |
| ShieldSlot / IncomingShield | 출처별 실드 슬롯. 같은 출처는 max 갱신, 다른 출처는 슬롯 추가(합산) | `Battle/Units/ShieldSlot.cs`(+`ShieldMath`) · `IncomingShield.cs` | 확정 | 부여자가 죽어도 잔여 실드는 유지(source 는 중첩 키일 뿐 수명 링크 아님). 적 전원에게 붙이는 이유 = 조건부 부착이 arm 의 대상 선정을 왜곡 |
| 실드 파열 | 실드 합이 양수에서 0 이 되는 순간이 하나의 사건 | `ShieldBreakEventsSingleton` · `dreamcatcher-shield-break` unit 0 | 확정 | |
| MaxHealthScale | 최대체력 배율. 1HP 바닥 · 축소 시 클램프 · 복원 시 무료 힐 없음 | `Health.ScaleMax` · `Units/MaxHealthScaleState.cs` · `MaxHealthScaleSystem.cs` | 확정 | `Health.max` 의 유일한 런타임 writer. lazy attach(배율이 1 을 벗어난 첫 프레임) |
| SimEntityId | 매치 내 스폰 순번. 재사용 없음, **프로세스 밖으로 나가는 유일한 ID** | `Battle/Units/SimEntityId.cs` · `BattleBridge.AttachSimEntityId`(:866) | 확정 | 설계도 불변식 17. 부착 범위 = 타겟 후보가 될 수 있는 것 전부 + 투사체. 미부착 = 요청 캐리어·픽업·사직서·장판 캐리어·싱글턴. 매치 경계에서 0 리셋(`BattleBridge.cs:1815`) |
| LethalTimer | 시한부 자폭 타이머 | `Battle/Units/LethalTimer.cs` · `LethalTimerSystem.cs` | 확정 | 만료 시 `DeadTag` 를 붙여 **공용 사망 경로**에 합류(전용 소멸 경로 없음) |
| HitFlashTag | 피격 순간 몸이 잠깐 커졌다 돌아온다 | `Battle/Units/HitFlashTag.cs` · `HitFlashSystem.cs` · 생산자 = `ProjectileHitSystem` | 애매 | **지속 0.15초가 코드 상수**이고 `originalScale` 을 컴포넌트가 들고 복원한다 — 제약 6(「모든 VFX 파라미터는 SO/프리팹에서」)과 충돌. 주석이 "promote to an SO field only if tuning becomes necessary" 라고 적어 뒀을 뿐 **결정 이력 미발견**. 생산자가 투사체 히트 하나뿐이라 근접 공격엔 안 뜬다 |
| DamagedCounter | 「N회 맞으면」 카드의 개체별 카운터 | `Battle/Units/DamagedCounter.cs` | 확정 | `DcTriggerSlot`(Combat)과 **일부러 다른 버퍼** — 카운터 쓰기가 Units 소유라서 |
| 배치 페이즈 | 놓였지만 아직 판에 없는 상태. 비행(InFlight) → 배치 모션(Deploying) | `Battle/Units/PendingDeployment.cs` · `DeploymentActivationSystem.cs` | 확정 | `defender-deploy-phase` unit 1·2. 존재하는 동안 쿼리 14곳이 `WithNone` 으로 배제(공격·캐스트·피격·타겟·감지·오라·픽업) |
| DeployMotionSeconds | 배치 페이즈 길이 = 저작 초가 아니라 **모션 자체**에서 파생 | `DefenderUnitData.DeployMotionSeconds` | 확정 | 2026-09-21 사용자 결정으로 저작 필드 3개 은퇴(`deploymentDuration`·`deployDelaySec`·`placementSkillDelay`). 모션이 없으면 0 |
| JustDeployed | 「방금 판에 놓였다」 1프레임 사건 태그 | `Battle/Units/JustDeployed.cs` | 확정 | 배치 확정 지점이 둘이라 브리지 후킹 대신 태그로 수렴. 소비는 `BossPeriodicTriggerSystem` |
| 배치 활성화 드레인 | 배치는 `StartBattle` **전에도** 일어난다 | `BattleBridge.DrainDefenderActivatedEvents` · `DefenderActivatedEventsSingleton` | 확정 | 브리지 드레인 중 **유일하게 `_running` 게이트 앞**. 채널 생성도 `BeginPlacement` |
| 사망 4단계 2-phase delete | 표시(DamageApplication) → 보완(HealthDeath) → 후처리(Patrol/ResignationDrop) → 일괄 삭제(UnitLifecycle) | `Battle/Units/{DamageApplication,HealthDeath,PatrolLifecycle,UnitLifecycle}System.cs` | 확정 | `docs/plans/2026-08-03-battle-sim-extraction-design.md:105` — **"즉시 삭제 단순화 금지"** 명시 |
| DeadTag | 「체력이 0이 됐다」 표시 | `Battle/Units/DeadTag.cs` | 확정 | 생산자 3(피해·`HealthDeathSystem`·`LethalTimerSystem`·`PatrolLifecycleSystem`), 소비자 1(`UnitLifecycleSystem`) |
| 퇴근(회수) | **사망이 아니다.** `DeadTag` 를 안 단다 | `BattleBridge.cs:4318` `RetireDefender` | 확정 | 설계도 불변식 11 — 「퇴근은 `DeadTag` 를 달지 않는다, 그것이 clock-out 계약 전부」. 그래서 각성·사직서·작별선물이 **배제 코드 없이** 안 일어난다. 대가 = 코스트 환급 없음 + 재배치 쿨타임 |
| 유출(골 도달) | 마음을 때릴 수 있으면 붙어서 공성, 못 때리면 산화 | `Movement/PastGoalTag.cs` · `GoalReachedEvent.canSiege` · `Units/GoalReachedMarker.cs` | 확정 | `goal-tower-siege` unit 1 + `heart-stress-axis` unit 7 정밀화. 판정 = 「마스크에 `DefenderCore` 가 있나」. 유출은 처치가 아니다(점수·각성·마음 회복 셋 다 없음) |
| 재배치(Relocation) | 놓인 유닛을 다른 칸으로 옮긴다 | `Bridge/BattleBridge.Relocation.cs` | 확정 | 배치 스킬·효과 타일은 재발화하지 않는다(`_onPlaceTriggeredEntities` 가드). 같은 칸 드롭 = 제자리 재정비 확정 |
| 킬 귀속 | 그 프레임 최대 피해를 넣은 source 가 killer | `Battle/Units/KillAttribution.cs` | 확정 | 동점은 버퍼 앞 엔트리 유지(strict `>`). `source == Null` 은 후보 제외 |
| 처치 사건 | 적이 피해로 죽었다 — 점수·각성·표식 회수·OnKill 이 여기서 갈린다 | `Battle/Units/EnemyKilledEvent.cs` | 확정 | `killScore` 는 은퇴(1킬 = 1점, 예외 없음). 적별로 다른 것은 `awakeningReward` 하나 |
| 전멸 판정 쿼리 | 「필드에 적이 남았나」 | `BattleBridge._aliveAttackersQuery`(11곳 공유) | 확정 | 불변식 16 — **여기에 필터를 걸지 않는다.** 보너스 적 제외는 전용 쿼리(`BonusWaveTag`) |
| 매치 정리 | 타입별 엔티티 파괴 + 큐 31 Dispose + 맵 teardown | `BattleBridge.cs:780` `TeardownCurrentBattle` | 확정 | `?.` 금지(Unity fake-null 로 정리가 중단돼 싱글턴 누수 실측) |
| UnitView fake-null | 뷰 베이스가 인터페이스면 파괴된 뷰가 생존 판정을 통과한다 | `Presentation/UnitView.cs` | 확정 | 추상 클래스로 둔 근거가 파일 헤더에 박혀 있다. 풀 10곳·소비처 13곳의 `!= null` 이 참조 비교로 떨어지면 컴파일은 통과하고 버그만 남는다 |

**키워드 행 수: 55**

---

## ECS 고유라 새 설계에서 개념 자체가 사라지는 것

- **`DeadTag`(컴포넌트)** — 나르던 규칙은 「**사망 표시 프레임과 실제 소멸 프레임을 가른다**」. 그 창에서 순찰 연쇄 소멸 · 사직서 드랍 · 자기 죽음 스킬 라우팅 · 「죽었지만 미파괴인 시체」 재조준 예외가 돈다. 순수 C# sim 에서도 "죽었지만 아직 목록에 있다" 상태가 한 틱 필요하다. 덤으로 **「퇴근은 이 표시를 안 단다」가 clock-out 계약의 전부**라(불변식 11), 표시 개념이 사라지면 그 계약을 다시 표현할 자리를 잃는다.
- **`EntityCommandBuffer` + 요청 캐리어 엔티티** — 규칙 둘. ① 「구조 변경은 순회 뒤로 미룬다」(`JustDeployed` 제거를 즉시 하면 슬롯 버퍼 이터레이션이 죽는다) ② 「요청은 **값 스냅샷**으로 한 틱을 건넌다」. 캐리어는 투사체 발사 요청 · 순찰 스폰 요청 · 해저드 스폰 요청 · 메테오 barrage 넷. 파생 규칙이 특히 안 보인다: **`StartBattle` 이 배치 페이즈에 쌓인 캐리어 3종을 버린다** — sim 은 `_running` 을 몰라서 배치 페이즈에도 돌기 때문이고, 안 버리면 판 시작 순간 낡은 좌표로 일제히 터진다.
- **`RequireForUpdate` / `RequireAnyForUpdate` 게이트** — 표면적으로는 "싱크(큐)가 없으면 fail-open". 그런데 `UnitLifecycleSystem` 에 명시 반례가 있다: 싱크가 없으면 `GoalReachedMarker` 를 **붙이지 않는다**. 붙이면 그 적은 두 번 다시 평가되지 않고 `AttackUnitTag` 는 유지돼 **웨이브 전멸 판정을 그 판 내내 막는 유령**이 된다. 새 설계는 이 비대칭(이벤트 없이 마커만 찍지 말 것)을 명시 조건으로 옮겨야 한다.
- **버퍼 사전 부착**(`IncomingDamage`/`CcEffect`/`DotEffect`/`ShieldSlot`/`IncomingShield`/`IncomingHeal`) — 표면 이유는 핫패스 구조 변경 회피지만, 실제 계약은 「**누가 무엇을 받을 수 있나는 스폰 시점에 못 박힌다**」다. 실측 사례 셋: 보스만 실드 버퍼를 주면 「악몽의 가호」 arm 의 대상 선정이 왜곡된다 · `IncomingShield` 만 붙이고 `ShieldSlot` 을 빼면 드레인이 게이팅돼 버퍼가 무한 성장한다 · 힐러 타겟을 `AnyDefender` 로 넓히면 `IncomingHeal` 없는 거점이 후보에 들어 playback 에서 던진다.
- **`WithNone<PendingDeployment>` 쿼리 14곳(+`EcsSkillContext` 2곳)** — 「배치 중 = 판에 없다(피격·타겟·감지·오라·픽업까지)」를 14개 쿼리가 각자 선언한다. 새 설계에서는 **술어 하나**여야 하고, 그 술어에 무엇이 걸리는지가 문서로 고정돼야 한다.
- **`Entity.Index`/`Version`** — 이미 `SimEntityId` 로 갈아끼웠다. 살아남는 규칙은 「ID 는 스폰 지점 **한 곳**에서만 발급 · 재사용 없음 · 매치 경계에서 0 리셋 · 기록에 할당기 번호를 싣지 않는다」(불변식 17). 타겟팅 동률 승자와 발사 패턴 난수열이 이 축에서 나오므로 결정론의 뿌리다.
- **`IEnableableComponent`**(`ModifierStatsDirty`) · **Burst lookup 잔존 규칙**(불변식 14) · **`SystemBase`/`ISystem` 구분** — 전부 소멸.
- **Unity fake-null** — `UnitView` 가 인터페이스가 아니라 추상 클래스인 이유가 사라진다. 다만 **뷰 계층이 MonoBehaviour 로 남는 한 이 함정 자체는 그대로**다(sim 쪽만 해방).
- **`GoalReachedMarker`** — `WithNone` 필터 전용 컴포넌트. 나르던 규칙은 「골 도달 이벤트는 **적 1기당 1회**」. 새 설계에서는 개체의 bool 한 칸.

---

## 코드에만 박혀 있고 문서에 없는 규칙 (rebuild 가 놓치기 쉬운 것)

- **분열 자식의 기준점은 부모의 «셀 중심»이지 연속 좌표가 아니다.** `MovementCellTrim` 이 유닛을 셀 중심에서 `0.5·tileSize − 1e-3` 까지 벗어나게 허용하므로, 연속 좌표에 0.25 를 더하면 자식이 **인접 셀**에 태어난다. 그 셀이 골이면 다음 틱에 `PastGoalTag` 가 찍혀 **「처치했는데 유출」**이 되고, Blocked 셀이면 flow 0 이라 한 프레임을 버린다. 배치는 `2π·c/count` 인덱스 결정론(RNG 금지 — 비동기 토너먼트 양측 동일 시뮬).
- **자기 죽음 스킬의 중복 억제 마스크가 64비트다.** `skillId ≥ 64` 면 억제가 **조용히 꺼져** 「같은 카드 두 장 = 폭발 두 번」이 부활한다. 지금은 `LogWarning` 한 줄이 유일한 방어.
- **사망 시 몸 반경을 파괴 «직전»에 스냅샷해서 이벤트에 싣는다**(`CasterBodyRadius`/`EventBodyRadius`). 드레인 시점엔 엔티티가 없어 0 으로 새고, 그러면 사망 폭발이 **조용히 좁아진다**. 진영(`CasterFaction`)도 같은 이유로 값이다 — 안 실으면 적의 작별 선물이 자기 진영을 때린다.
- **퇴근은 매칭 슬롯 «전부» 발동하고, 사망은 «첫 매칭»만 발동한다.** 사망 쪽 제약은 이벤트 struct 가 payload 를 한 벌만 실어서 생긴 것이고, 퇴근은 버퍼를 직독하므로 해당 없다 — 카드 2장이면 운석 2발.
- **죽은 채로 배치 중인 유닛은 컴포넌트만 걷고 끝난다** — 배치 스킬도 활성화 이벤트도 없다(비행 중 사망 포함). 「시체는 배치되지 않는다」.
- **`JustDeployed` 는 트리거 슬롯 버퍼가 있는 유닛에만 붙인다.** 없는 유닛에 남으면 소비 시스템의 `RequireForUpdate` 가 안 맞는 틱에 다음 배치 사건과 섞인다.
- **`visualMaterial` 이 null 이면 적 스폰이 `Entity.Null` 을 반환한다** — 경고 한 줄과 함께 조용히 실패하는 경로.
- **공격 방식을 저작했는데 출력 목록이 비면 walk-only 로 강등된다**(경고 동반). 「피해 0 공격자」를 만들지 않는 것이 규칙.
- **`_defenderByTile`(앵커 → 엔티티+SO)가 판 위 유닛의 유일한 진실원**이고 `_defenderCellOwner`(점유 셀 → 앵커)와 항상 쌍이다. **재배치는 `CreateDefenderEntity` 를 지나지 않아** 배치 수 변동 신호(`DefenderPlaced`)가 발화하지 않는다 — 기수가 안 변하니 알릴 것도 없다.
- **실드 흡수는 오래된 슬롯부터 FIFO 로 깎고 소진 슬롯을 `RemoveAt(0)`** — 삽입 순서 유지가 결정론의 근거. `ValueFromSource` 로 「기존값 ≥ 새 값」이면 재부여·VFX 를 스킵(Merge 가 max 라 no-op).
- **최대체력 배율은 복원 시 체력을 올려주지 않는다**(무료 힐 없음). 축소 시에만 현재 체력을 클램프하고, 바닥은 1 HP. 기준은 항상 `baseMax`(스폰 시점 원본) — `Health.max` 를 직접 곱하면 누적 오염.
- **순찰 생존 술어는 3중이다**(`Exists` + `DeadTag` 없음 + HP>0). 두 사망 생산자 **뒤**에 서야 같은 프레임에 주인의 죽음을 본다 — 앞에 서면 stale HP 를 읽어 순찰병이 1프레임 더 살아 공격한다.
- **골 타워가 2개 이상 스폰되면 경고만 나온다.** 하드 에러를 안 두는 것이 의도적 스코프 판단이고, 종료 판정은 「첫」 마음에 걸린다.
- **미발급 ID 는 0 이 아니라 `int.MaxValue`** — 0 을 폴백으로 쓰면 미발급끼리가 아니라 「0번 유닛」과 충돌해 동률 순위를 훔친다.
- **`retireCooldownRatio` 의 진짜 방어선은 `Clamp01` 이지 `[Range]` 가 아니다.** 시트 임포터가 리플렉션으로 필드에 직접 써서 `[Range]` 도 `OnValidate` 도 안 탄다. 읽는 자리에서 조여야 「퇴근 ≤ 사망」이 어느 저작 경로로도 안 샌다. 같은 수법이 `Footprint`(`Vector2Int.Max`)·`EffectiveMaxOnBoard`·`EffectivePlacementLayers` 에도 있다 — **이니셜라이저 + 읽는 자리 폴백 2중 구조를 함께 유지할 것.**

---

## 이 영역에서 rebuild 가 결정해야 할 열린 질문 (5)

1. **마음은 몇 개인가.** 맵당 골 1~4 기계(`multi-goal-map` 완료)와 「마음 1개 전제」(`heart-stress-axis` 명제 10)가 공존하고, 판 종료(`stress_full`)는 첫 마음에만 걸린다. 사용자 결정은 이미 나와 있다 — **「마음 N개가 체력을 공유」(2026-08-25)** = `docs/spec/heart-stress-axis/12_shared_heart_pool.md`, **작성됨·착수 전**. `wide-board-content` 가 이 계약을 요구한다. 새 sim 이 이 위에 설 것인지가 첫 갈림길이다.
2. **소비처 0 인 예약 값을 옮기는가.** `EnemyTier.Elite`(술어 없음) · `Faction` 중립 3비트(생산자·소비자 0) · `BodySize.Large`(예약) 셋 다 「저작 축에서 보여야 한다」 또는 「append-only 직렬화 보존」이라는 이유로만 살아 있다. ECS 직렬화 계약이 사라지면 유지 근거도 사라진다 — 되살릴지 지울지 한 번에 정해야 한다(같은 결의 항목: `ModifierOrigin` 의 생산자 0 두 자리, `PlacementRejectReason.SameCell`).
3. **희귀도를 sim 이 알아야 하는가.** 현행 `DefenderRarity` 는 아웃게임 카드색·뱃지·VFX 강도 전용이고 전투는 희귀도를 모른다. 근거였던 드래프트 풀 10장 규칙은 스쿼드 로드아웃으로 대체됐다. 유지 / sim 축으로 승격 / 은퇴 중 어느 쪽인지 기록이 없다.
4. **「배치 중」의 두 단계를 sim 이 계속 소유하는가.** `InFlight` 는 sim 이 **시계를 재지 않고** 프레젠테이션(브리지의 `LandDeployedDefender`)이 끝낸다 — 착지 없이 남으면 좀비이고, 「비행을 끝내는 모든 출구가 Land 를 부른다」가 계약이다. 엔진-프리 sim 에서 이 의존 방향을 뒤집을 것인지(비행을 순수 뷰로 밀고 sim 은 `Deploying` 한 단계만 갖는 안), 아니면 그대로 둘 것인지.
5. **다칸 footprint 와 적 통행 차단을 여는가.** 저작 실값은 2026-09-03 에 들어갔지만(캐논 2×3 등), 「방어유닛이 적 통행을 막는다」는 `defender-footprint` backlog [M] 로 남아 있고 **흐름장 1회 굽기 제약과 교착 리스크**를 동반한다. 새 sim 의 이동·충돌·경로 설계를 정하기 전에 답이 필요하다. 인접 항목: 거점 footprint 일반화(`battle-structures` [M]) — 진영이 크기를 정하는 현 구조를 유지할지.

---

## 핵심 파일 경로 (절대)

- `/Users/sy/dev/wassup/Assets/_Project/Scripts/Battle/Units/` — 컴포넌트 40여 개 + 수명주기 시스템 6개(`DamageApplicationSystem` · `HealthDeathSystem` · `PatrolLifecycleSystem` · `UnitLifecycleSystem` · `DeploymentActivationSystem` · `LethalTimerSystem` · `MaxHealthScaleSystem` · `HitFlashSystem`)
- `/Users/sy/dev/wassup/Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 스폰 조립 4지점: `:8437 CreateDefenderEntity` · `:10769 CreateEnemyEntity` · `:8737 CreatePatrolEntity` · `:6454 SpawnStructureEntities`. 그 외 `:866 AttachSimEntityId` · `:4318 RetireDefender` · `:780 TeardownCurrentBattle` · `:11048 SpawnSplitChildren` · `:7805 LandDeployedDefender` · `:7821 ActivateDeployedDefender`
- `/Users/sy/dev/wassup/Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs`
- `/Users/sy/dev/wassup/Assets/_Project/Scripts/Data/DefenderUnitData.cs` · `AttackUnitData.cs` · `FootprintMath.cs` · `StructureData.cs` · `StructurePlacement.cs` · `SplitChain.cs`
- `/Users/sy/dev/wassup/Assets/_Project/Scripts/Skills/Faction.cs`(엔진 참조 없는 어셈블리에 산다)
- `/Users/sy/dev/wassup/Assets/_Project/Scripts/Presentation/UnitView.cs`
- `/Users/sy/dev/wassup/docs/reference/battle-core-architecture.md` §1.1~1.5 · §2 · §3 · §8
- `/Users/sy/dev/wassup/docs/plans/2026-08-03-battle-sim-extraction-design.md:105`(사망 4단계 2-phase delete 의 유일한 문서 근거)
- `/Users/sy/dev/wassup/docs/spec/README.md` Follow-up Backlog
