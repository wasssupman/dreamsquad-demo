# 0 — 공통 어휘 + bake seam (병렬 앞의 단독 커밋)

## 목적

세 레인이 **전부** 건드릴 파일의 변경을 여기 모아 먼저 끝낸다. 이 커밋 이후 레인 A/B/C 의 파일
소유는 겹치지 않는다(README 계약 P1·P2). 어휘와 "굽는 법"만 놓고 **소비자(arm)는 놓지 않는다** —
카드 에셋이 아직 없으므로 도달 경로가 없어 무해하다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/MovementKind.cs` + `Projectile/Emission/MovementBinding.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DcApplicability.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs` + `BattleBridge.cs` 의 `SpawnProjectile`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`
- `Assets/_Project/Tests/EditMode/` — 문안 골든 3건 + bake 거절 어서션

## 구현

### 1) 정의 계층 append (`DcMechanic.cs`) — 전부 **끝에 추가**

- `DcTriggerKind` ← `OnRetire`. 주석: 발동 지점은 **브리지의 퇴근 경로**다(`RetireDefender`).
  사망(`OnDeath`)과 형제이며 **교차 발동하지 않는다** — 퇴근은 `DeadTag` 를 달지 않고
  `DefenderDied` 를 쏘지 않는다(`defender-clock-out` 계약 1). 적에겐 열리지 않는다
  (`EnemyTriggerArmed` 무변경 = fail-closed).
- `DcPayloadKind` ← `SelfOrbitProjectile = 22`. 주석: host 셀 중심을 도는 화염구 1개를
  `duration` 초 동안 띄운다. **신규 payload 필드 0** — 전부 기존 슬롯 재사용:
  `magnitude`=스친 적에게 줄 피해 · `duration`=지속 초 · `tileRange`=궤도 반경(타일) ·
  `projectile`=탄 SO(뷰 + 선속도 + 피격 반경 + 재타격 쿨타임).
  > **재타격 쿨타임은 탄 SO 가 소유한다**(ECS 리뷰 M1). 초판은 payload→슬롯→요청→상태 4단으로
  > 관통시켰는데, `pierceCount` 가 이미 **탄 SO 에 있고 드레인(`SpawnProjectile`)이 직접 읽는**
  > 선례다(`BattleBridge.cs:4644`, `dropHeight` 보충과 같은 번역자 역할). SO 경로를 쓰면
  > `DcPayloadSpec`·`DcTriggerSlot`·`ProjectileSpawnRequest` **신규 필드가 셋 다 사라진다.**
  > "같은 탄 SO 로 다른 쿨타임"이 필요해지면 탄 SO 복제로 갈라진다(README 계약 7-1 과 같은 관례).
- `DcAttackModKind` ← `DamageVsSleeping`. 주석: 상시(트리거 없음). **피해자별** 판정이며
  `DcAttackModSpec.damageMul` 재사용(2.0 = ×2) — 신규 필드 0.

### 1-1) 궤적 어휘 (`MovementKind.cs` + `MovementBinding.cs`)

`MovementKind.OrbitAroundPoint = 6` append 는 **레인 A1 이 아니라 여기 있다.** 이유는 소비자가
브리지에 있기 때문이다 — `SpawnProjectile` 드레인이 궤도 분기(중심/반경/각속도/지속/관통예산)를
채워야 하는데, 그 파일은 unit 0 소유다. 레인 A1 은 `Orbit.cs` + Move arm + 뷰만 갖는다.

⚠ **`MovementBinding` 도 같은 커밋에서 갱신한다.** `MovementKind` 를 늘리면
`PatternTargetingTests.MovementBinding_ClassifiesEveryKnownKind` 가 즉시 실패한다(C# 이 enum
switch 전수성을 강제하지 못해 EditMode 핀으로 잡는 구조 — 의도된 fail-closed).
궤도는 **`BindingClass.Cell`** 이다: 중심이 발사 시점에 고정되고 타겟 엔티티를 잡지 않는다.
`KnownKindCount` 6 → 7.
> `MovementBinding.cs` 의 기존 주석은 "오비트 같은 새 궤적은 emitter 변경 0" 을 이미 예고하고
> 있었고, 실제로 emitter 는 이 분류 한 줄 말고 손대지 않았다.

### 2) 적용성 (`DcApplicability.cs`)

- `IsTriggerWired` 에 `OnRetire` case 추가.
- `EvaluateMechanic` 의 self 계열 목록에 `SelfOrbitProjectile` 추가(host 의 공격 모델과 무관).
- `EvaluateAttackMod` 에 `DamageVsSleeping` case — `hasDamageOutput` 없으면 `NeedsDamageOutput`.

### 3) 카드 bake (`BattleBridge.Dreamcatcher.cs`)

- **`periodSeconds` 배선**(계약 9): 카드 슬롯 조립에 `periodSeconds = m.trigger.periodSeconds`
  를 넣고, `trigger.kind == PeriodicTimer && periodSeconds <= 0` 을 **loud 거절**한다.
  (지금까지 보스 경로만 실어 보내서 카드 주기 슬롯이 조용히 무발동이었다.)
- **`SelfOrbitProjectile` bake**: `projectile == null` / `magnitude <= 0` / `duration <= 0` /
  `tileRange <= 0` 각각 loud 거절. 통과 시 슬롯에 싣는 것 —
  `projectileDataIndex` · `magnitude` · **`duration`** · `tileRange` · `visualScale` ·
  **`speed`**(탄 SO 선속도) · **`hitThreshold`**(탄 SO 피격 반경).
  > ⚠ **`speed`·`hitThreshold`·`duration` 을 빠뜨리면 조용히 망가진다**(ECS 리뷰 M2·M3):
  > `ISystem` 은 SO 를 못 읽으므로 arm 이 볼 수 있는 것은 슬롯뿐이다. 셋이 0이면
  > **안 도는 데다(각속도 0) 아무도 못 맞히고(반경 0) 즉시 사라지는(지속 0)** 구슬이 나온다.
  > `DcTriggerSlot` 에는 이 세 필드가 **이미 있다** — 신규 필드 0, 복사 3줄이 전부다.
  > (`duration` 은 방어유닛 bake 가 payload 분기 안에서만 세팅하는 구조라 보스 bake 처럼
  > 공통으로 실리지 않는다 — 이 분기에서 명시적으로 넣어야 한다.)
  > 재타격 쿨타임은 여기서 굽지 않는다 — 탄 SO 소유이고 드레인이 읽는다(§1·§4).
- **`OnRetire` bake**: payload 가 `SelfTileAoe` 가 아니면 loud 거절(v1 배선 1쌍).
  `SelfTileAoe` 규칙(AOE view + 양수 magnitude)은 기존과 동일하고, **`duration` 을 슬롯에 실어**
  낙하 예고로 쓴다(계약 8).
- **`DamageVsSleeping` attackMod bake**: `damageMul <= 0` 거절, 슬롯에 `kind`·`damageMul` 만.

### 4) 재타격 쿨타임 = 탄 SO 소유 (`ProjectileData` + `SpawnProjectile`)

**`ProjectileSpawnRequest` 는 건드리지 않는다.** (초판은 여기 필드를 추가했다 — ECS 리뷰 M1 로 폐기.)

- `ProjectileData` 에 `rehitCooldownSec`(float, 기본 0) 추가.
- `SpawnProjectile` 드레인이 `state.rehitCooldownSec = projData != null ? projData.rehitCooldownSec : 0f`
  로 복사. **바로 옆 `pierceCount` 복사(`BattleBridge.cs:4644`)와 같은 자리·같은 형태**다 —
  "SO 해석은 드레인이 유일 seam"(projectile-emission-pattern 계약 10).
- `ProjectileState.rehitCooldownSec` 필드 **선언만** 여기서 세운다(레인 A 가 브리지를 안 만지게).
  소비(판정 분기)는 레인 A 의 unit 2 다.
- **기본 0 = 기존 전 발사 지점 무변화.**

### 5) 문안 (`DreamcatcherCardText.cs`)

- 트리거 문안: `OnRetire` → `"퇴근할 때 "`. `PeriodicTimer` 는 이미 있으면 재사용, 없으면
  `"{T}초마다 "`.
- payload 문안: `SelfOrbitProjectile` → `"주위를 도는 화염구가 {duration}초간 스치는 적에게 {magnitude} 피해"`.
- attackMod 문안: `DamageVsSleeping` → `"잠든 적에게 주는 피해 x{damageMul}"`.
- 골든 테스트 3건(`DreamcatcherCardTextTests`) — description 은 formatter 정확 미러.

## 완료 기준

- Unity 컴파일 에러 0 · 콘솔 경고 0.
- EditMode 전량 green. 신규: 문안 골든 3건 + `DcApplicability` 3 case 어서션
  (`Unclassified` 가 안 나오는 것 = 배선 누락 없음).
- **기존 카드 무회귀**: 라이브 드림캐쳐 에셋의 문안/부착 결과가 전부 동일.
- 이 커밋만으로는 **게임 동작이 하나도 바뀌지 않는다**(카드 에셋 0개 = 도달 불가).
