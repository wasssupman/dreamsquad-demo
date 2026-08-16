# 0 — `OnPlace` 트리거: 배치 스킬을 공용 규칙 위로

## 목적

「실행 조건 만족 → 스킬 실행」의 배관을 방어유닛 배치에도 연다. 적/보스는 이미
`DcMechanic{trigger × payload}` 데이터로 스킬을 선언하는데, 방어유닛 배치 스킬만 별개 어휘
(`OnPlaceEffectType` enum + `BattleBridge` switch)를 쓴다.

이 unit 만으로는 어떤 스킬도 새로 생기지 않는다(소비자는 units 2·4).

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcTriggerKind.OnPlace` **append**(9)
- 신규 `Assets/_Project/Scripts/Data/Abilities/UnitSkillAbility.cs`
- 신규 `Assets/_Project/Scripts/Battle/Units/JustDeployed.cs` — 1프레임 사건 태그
- `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` — 진영별 armed 술어 분해
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — `OnPlace` 소비 arm
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — bake 중립화 + 태그 부착 + 호출 순서
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — 카드 경로 `OnPlace` loud 거절
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs` — 신규 트리거 등록
- `Assets/_Project/Tests/EditMode/` — bake·발화·순서 테스트

## 구현

### `DcTriggerKind.OnPlace` (append, 값 9)

「이 유닛이 판에 놓여 활성화된 순간」. 발화 1회, 카운터 없음. 재배치는 기존 배치 스킬 규약을
따른다(재무장).

⚠ enum 은 append-only — 에셋이 int 로 직렬화한다.

### `UnitSkillAbility` (방어유닛 규칙의 집)

```
[CreateAssetMenu(menuName = "Wassup/Ability/Unit Skill")]
public class UnitSkillAbility : DefenderAbilityData { public DcMechanic[] mechanics; }
```

적의 `AttackUnitData.nightmareMechanics` 와 대칭. `DefenderUnitData` 에 flat 필드를 늘리지 않는다
(README 계약 4). `DcMechanic` 이 이미 ECS-free 라 `DefenderAbilityData` 의 「정의 계층은
아키텍처 무지」 계약을 그대로 만족한다. 상속 2단계 유지.

### ⚠ 발화 지점은 **셋**이다 — 브리지 후킹으로는 못 덮는다

| 경로 | 진입 | 호출자 |
|---|---|---|
| D&D | `ActivateDeployedDefender` → `TriggerDeploymentOnPlaceSkill` | `TryBeginDefenderDeployment` |
| **탭 배치** | `PlaceDefenderAs` → `TriggerOnPlaceAndSynergy` | `PlacementInput` (라이브) |
| **재배치** | `BattleBridge.Relocation.cs` 가 `ActivateDeployedDefender` 재호출 | 재무장이 의도 |

**기존 on-place PlayMode 테스트는 전부 탭 경로**(`OnPlaceDotNearbyTest`·`OnPlaceApplyStackNearbyTest`·
`OnPlaceForwardProjectileTest`). 한 곳만 후킹하면 unit 2 테스트가 선례대로 짜였을 때 아무것도 안
나가거나, 테스트만 D&D 로 바꿔 **라이브 탭 배치에선 스킬이 안 나가는 채 초록**이 난다.

### 발화 = `JustDeployed` 태그 + 시스템 소비 (양 리뷰 트랙 권장)

브리지는 **태그 한 줄**만 붙이고, `OnPlace` 슬롯 소비는 `BossPeriodicTriggerSystem` 에 얹는다.
그 시스템은 이미 진영 중립이고(`RequireForUpdate<DcTriggerSlot>` + `WithNone<DeadTag>` 만,
BossTag/DefenderUnitTag 게이트 없음) `EmitProjectilePattern` arm 을 갖고 있다 —
**README 계약 5(사본 금지)가 헬퍼 추출 없이 달성된다.** 브리지에 헬퍼를 두면 그게 곧 세 번째 사본이다.

세 경로가 각자 태그를 붙이므로 발화 지점 문제도 닫힌다.

**조건 5개 — 지키지 않으면 새 결함이다:**

- **(a) 시스템 순서를 명시한다.** 같은 프레임에 패턴이 나가려면 `ProjectileEmitterSystem` 앞,
  도발이 같은 틱에 붙으려면 **`[UpdateBefore(AggroStateSystem)]`** 이 필요한데
  `BossPeriodicTriggerSystem` 에 지금 그 속성이 없다. 안 붙이면 1프레임 지연이 빌드마다 달라진다.
- **(b) 태그 제거는 반드시 ECB.** 소비 루프가 `DcTriggerSlot` 버퍼를 순회 중이라 즉시
  `RemoveComponent` 는 이터레이션을 죽인다. 그 시스템은 이미 ECB 를 갖고 있다 — 재사용.
- **(c) `PendingDeployment` 경합은 지금 없지만 우연이다.** D&D 는 발화와
  `RemoveComponent<PendingDeployment>` 가 **둘 다 즉시 EntityManager 호출**이라 sim 틱 전에 정리된다.
  둘 중 하나라도 ECB 로 바뀌면 즉시 깨지므로 계약으로 적는다.
- **(d) 1회 보장의 권위가 둘이다.** 레거시 = `_onPlaceTriggeredEntities`(managed HashSet),
  규칙 = 태그. **태그는 레거시 경로의 권위가 아니다** — 하나로 합치려다 재배치 재무장을 깨지 말 것.
- **(e) 새 결정론 표면.** 브리지 발화는 배치 호출 순서, 시스템 발화는 청크 순서다. 한 프레임에
  한 유닛만 배치되는 게임이라 실전 영향은 없으나 PlayMode 스크립트 테스트에서 재현된다.

⚠ **`AreaTaunt` 를 시스템에서 하면 브리지의 층 게이트(`CanDefenderTargetMover`)를 잃는다**
→ 비행 적이 도발 가능해진다. unit 4 에서 명시적으로 판정한다.
⚠ **`BossPeriodicTriggerSystem` 은 `[BurstCompile]` 이다** — arm 안에서 `Debug.LogWarning` 불가.
저작 실수의 loud warn 은 전부 **bake 시점**(브리지)에서 낸다. 그게 기존 관례와도 맞다.

### bake 중립화 — 진영 축을 잃지 말 것

`BakeNightmareMechanics(Entity, AttackUnitData)` 를 `DcMechanic[]` 기반으로 중립화하되,
그 함수에 **진영 종속 코드가 셋** 있다. 시그니처에서 빠뜨리면 조용히 깨진다:

1. **`BuildPatternTemplate(..., hostIsEnemy: true)` 하드코딩** → `targetFaction = hostIsEnemy ?
   Defender : Enemy`. 빠뜨리면 **캐논 미사일이 방어유닛을 때린다.**
2. `EnemyTier.Boss` → `BossTag` + `ThreatEntry` + 보스 경보 (방어유닛 경로에선 안 돈다)
3. `slot.maxHpRef = unitType.health` (HealthThreshold 트리거용)

→ `BakeUnitMechanics(Entity, DcMechanic[], bool hostIsEnemy, float maxHpRef, Func<DcTriggerKind,bool> armed, string ownerLabel)`
형태로 **진영·체력·허용 술어를 전부 파라미터화**한다. 보스 부속물은 적 호출처에 남긴다.

### `EnemyTriggerArmed` 화이트리스트를 분해한다

`BakeNightmareMechanics` 안에 **슬롯 생성 직전** 게이트가 있다:
`DcTrigger.EnemyTriggerArmed(kind)` = `{PeriodicTimer, HealthThreshold, AttackN}` **셋뿐**.

그대로 물려받으면 **`OnPlace` 슬롯이 0개 생성**되어 units 2·4 가 조용히 무동작한다.
그렇다고 화이트리스트에 `OnPlace` 를 넣으면 안 된다 — `DcTrigger.cs` 의 주석이
"이 줄을 풀면 **보스 파열 폭발이 자기 진영을 때린다**"고 명시 경고한다.

→ `DefenderTriggerArmed(kind)` = `{OnPlace}` 를 신설하고 호출자가 자기 술어를 넘긴다
(`TriggerArmedFor(faction, kind)` 분해도 동형). 적 경로의 fail-closed 는 **한 글자도 안 바뀐다.**

### bake 호출 순서 — `slots[0]` 하드코딩

`AttackSystem` 의 다연발 경로는 `PatternSlot` 버퍼의 **`slots[0]` 하나만** 읽는다.
`EntityManager.AddBuffer<T>` 는 add-or-get 이라 두 번째 부착이 내용을 보존하므로 두 슬롯이
공존하고, **index 0 을 누가 갖느냐가 bake 호출 순서로만 정해진다.**

→ `BakeUnitMechanics` 는 **`BakeDefenderDirectionalPattern` 뒤**에 호출한다. 순서를 EditMode 로
고정한다 — 안 하면 **머신거너 다연발이 캐논 배치 스킬 패턴을 쏜다.**

`EmitterInstance` 사전 부착은 별도 작업이 아니다 — 중립화된 bake 가 기존 `wantsPattern` 스캔을
그대로 물려받으므로 패턴 payload 를 가진 유닛에 자동으로 붙는다. 다만 그 자리의
**AddBuffer 순서 함정 주석**(먼저 잡은 핸들이 죽는다)은 그대로 유효하다.

### 카드 경로에서 `OnPlace` 를 거절한다

`DcApplicability` 에 `OnPlace` 를 등록하지 않으면 `Unclassified` fail-closed 로 전수 테스트가
실패한다(설계된 안전망). 등록은 하되 — 드림캐쳐 **카드**가 `OnPlace` 를 선언하면 카드는 배치
**후**에 붙으므로 1회 가드에 막혀 **붙는데 영영 안 터진다**. 바로 옆에 정반대 판단의 선례가 있다:
카드 경로가 `EmitProjectilePattern` 을 loud 거절한다.

→ 카드 bake 경로에서 `OnPlace` **loud 거절**. 유닛 자기 규칙(`UnitSkillAbility`)만 허용.

### 레거시와의 공존

한 유닛이 `onPlaceEffect != None` 과 `UnitSkillAbility` 를 **둘 다** 선언하면 bake 시점에
loud warn 한다(조용한 통과 금지). 발화 시점이 아니라 bake 인 이유는 (b) 의 Burst 제약과,
기존 authoring 거절이 전부 bake-time 이라는 관례 때문이다.

## 완료 기준

- [x] compile 0 error (신규 `.cs` — `refresh_unity scope=all`)
- [x] **이 unit 만으로는 어떤 배치 스킬도 바뀌지 않는다.** 기존 10종 무회귀
- [x] EditMode
  - `UnitSkillAbility` 유닛 bake → `DcTriggerSlot` 에 `OnPlace` 슬롯 부착 (`DefenderTriggerArmed` 통과)
  - **적 경로 fail-closed 무회귀** — `EnemyTriggerArmed` 로 막히던 kind 가 여전히 막힌다
  - **`targetFaction` 진영 핀** — 방어유닛 host 패턴의 `targetFaction == Enemy`
  - **`slots[0]` 순서 핀** — 다연발 + 배치 스킬 둘 다 가진 유닛에서 다연발이 index 0 을 갖는다
  - 드림캐쳐 카드가 붙은 유닛도 배치 스킬 슬롯이 남는다(버퍼 공존)
  - 카드가 `OnPlace` 를 선언 → loud 거절
  - 레거시 enum + 능력 동시 선언 → 경고
  - `DcApplicability` 전수 테스트 green
- [x] PlayMode: **세 배치 경로(D&D · 탭 · 재배치) 전부에서 `OnPlace` 슬롯이 1회 발화**
- [x] `grep` 로 `EmitProjectilePattern` 실행 사본이 **2곳**(늘지 않았다)
- [x] 기존 EditMode/PlayMode 무회귀 — 보스 메커닉·드림캐쳐 카드·머신거너 다연발
