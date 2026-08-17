# 0 — 공통 어휘 (선행 · 단독 커밋)

## 목적

세 카드가 전부 건드릴 **정의 계층과 번역부**를 먼저 한 커밋으로 끝낸다. 이 단위가 끝나면
1~6 의 파일 소유가 서로 겹치지 않는다. **게임 동작 변화 0** 이 완료 조건이다.

## 변경 대상

| 파일 | 변경 |
|---|---|
| `Data/Dreamcatcher/DcMechanic.cs` | `DcPayloadKind.SpawnHazard = 24` append + `DcPayloadSpec.hazard`(`HazardSO`) 필드 |
| `Data/ProjectileData.cs` | `ProjectileFlightMode.Boomerang` append + 넉백 2필드 |
| `Battle/Combat/Projectile/MovementKind.cs` | `BoomerangReturn = 7` |
| `Battle/Combat/Projectile/Emission/MovementBinding.cs` | `BoomerangReturn` → `Direction` · **`KnownKindCount` +1** (타 작업의 궤적 1종이 같은 시점에 편입돼 실제 값은 9) |
| `Battle/Combat/Projectile/ProjectileState.cs` | 넉백 2필드 + 궤도가 쓴 슬롯의 왕복 의미 표 |
| `Battle/Combat/DcTriggerSlot.cs` | 추가 투사체의 **궤적 축 2필드** |
| `Bridge/BattleBridge.cs` | `ResolveProjectileAxes` +1 분기 |
| `Bridge/BattleBridge.Dreamcatcher.cs` | `SpawnHazard` bake seam(해저드 SO 등록·loud 거절) |
| `Core/Dreamcatcher/DcApplicability.cs` | `SpawnHazard` 적용성 |
| `UI/Dreamcatcher/DreamcatcherCardText.cs` | `SpawnHazard` 문안 |

## 구현

**1. 해저드 페이로드.** `SpawnHazard` 는 「어떤 불씨를 · 몇 초」만 말한다. 모양·효과·틱·뷰는
전부 `HazardSO` 저작이고 카드가 복제하지 않는다(계약 9).
- `hazard` = 깔 장판 SO. `null` = bake **loud 거절**(조용한 no-op 금지 — 기존 선례).
- **지속도 장판의 성질이라 카드가 정하지 않는다.** 수명은 sim·뷰 양쪽이 SO 값을 직접 읽고
  오버라이드 파라미터가 없다 — 뚫으려면 Effects 맥락의 스폰 시그니처까지 바꿔야 하는데
  소비자는 카드 한 장뿐이다(제약 8). **신규 스칼라 필드 0.**
- 정의 계층의 SO 참조는 `projectile`·`auraPrefab`·`pattern`·`stackModifier`·`splitUnit` 선례와
  동일하다 — 금지 대상은 Entities/Battle 타입이고 `HazardSO` 는 같은 `Wassup.Data` 다.

**2. 왕복 궤적의 자리.**
- `ProjectileFlightMode.Boomerang` → `ResolveProjectileAxes` 가 `(BoomerangReturn, PathHit)` 로 매핑.
  **이 함수가 저작→ECS 축 번역의 단일 지점**이라 다른 곳에 매핑을 복제하지 않는다.
- `MovementBinding.Of` 는 **`Direction`** — 타겟 엔티티도 착탄 셀도 잡지 않고 방향으로 나간다.
  덕분에 발사 명세(emitter)가 나중에 부메랑을 쏘려 할 때 **emitter 변경 0** 이다(그 파일의 예고).
- ⚠ **`KnownKindCount` 를 같이 올린다.** content-4 에서 이걸 빠뜨려 `MovementBinding` 테스트가
  빨개졌다 — `MovementKind` 추가마다 재발하는 함정이라 여기 못박는다.
- `ProjectileState` 는 **신규 궤적 필드 0**: `origin`=발사점 · `direction`=**발사 축(불변)** ·
  `maxDistance`=편도 거리 · `speed`·`elapsed`·`prevPos` 는 기존 의미 그대로.
  ⚠ `direction` 은 궤적 함수의 **입력**이므로 arm 이 되먹이면 안 된다(unit 1 §direction).
  「지금 어느 다리인가」는 어디에도 저장하지 않고, 필요한 곳은 스윕 벡터에서 뽑는다.

**3. 넉백은 탄 에셋이 소유한다** (계약 4).
- `ProjectileData.knockbackDistance`(월드) · `knockbackDuration`(초). 둘 다 0 = 꺼짐.
- 저작 단위가 「거리 ÷ 시간」인 것은 기존 근접 넉백(`ccData.knockbackDistance / knockbackDuration`)
  관례를 그대로 따른 것이다 — 속도를 직접 저작하게 하면 두 어휘가 갈린다.
- `ProjectileState` 에 같은 2필드를 싣고 드레인이 SO 에서 복사한다(`rehitCooldownSec` 와 같은 자리).
  **payload/슬롯/요청 struct 로 관통시키지 않는다.**

**4. 추가 투사체의 궤적 축.** `DcTriggerSlot` 에 `projectileMovement`/`projectilePayload` 를 싣고
bake 가 `ResolveProjectileAxes(projectile.flightMode)` 로 채운다. 슬롯이 이미 탄 SO 값
(`speed`·`hitThreshold`·`visualScale`·`projectileDataIndex`)을 나르고 있으므로 같은 자리다.
소비는 unit 3.

## 완료 기준

- [ ] 컴파일 통과 · `MovementBinding` 전수 분류 테스트 초록(`KnownKindCount` 갱신)
- [ ] 기존 카드 41장 **동작 무변화** — 넉백 2필드 0 · 궤적 축은 기존 탄이 전부 `Homing`
- [ ] `SpawnHazard` × `hazard=null` 저작이 콘솔에 **loud 경고**를 남기고 슬롯을 만들지 않음
- [ ] EditMode 전량 초록
