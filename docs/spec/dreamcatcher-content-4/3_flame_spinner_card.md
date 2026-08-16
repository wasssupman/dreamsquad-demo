# 3 — 불꽃 팽이 카드 (레인 A · 카드)

## 목적

`PeriodicTimer × SelfOrbitProjectile` 발동 arm 을 붙이고 카드를 저작한다.
게임에서: 부착 유닛 주위를 **T초마다** 화염구가 나타나 **N초간** 돌고, 스친 적을 반복해서 깎는다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` (payload arm 1개)
- `Assets/_Project/Data/Projectiles/Projectile_FlameOrb.asset` **(신규)**
- `Assets/_Project/Data/Dreamcatcher/Card_FlameSpinner.asset` **(신규)**

> 트리거 자체는 이미 진영 중립으로 돈다(계약 9). unit 0 이 카드 bake 에 `periodSeconds` 를
> 배선했으므로 이 unit 은 **발동했을 때 무엇을 쏘는가**만 더한다.

## 구현

### 1) 발동 arm

`BossPeriodicTriggerSystem` 의 payload 디스패치에 `SelfOrbitProjectile` case 추가.
기존 `AreaBarrage` arm 과 **같은 형태**로 — 캐리어 엔티티에 `ProjectileSpawnRequest` 를 실어
브리지 드레인이 스폰하게 한다(dc-trigger 계약 6: 슬롯 주인의 평타가 같은 프레임에 요청을
스테이징할 수 있어 전용 캐리어가 필요하다).

요청 채우기:

| 필드 | 값 |
|---|---|
| `movement` | `OrbitAroundPoint` |
| `payload` | `PathHit` |
| `origin` / `impact` | host 셀 중심 |
| `maxDistance` | 궤도 반경 `r` = `slot.tileRange * tileSize` |
| `speed` | **각속도 = `slot.speed` ÷ `r`** (rad/s). 슬롯의 `speed` 는 탄 SO 의 월드 속도(m/s)를 구운 것이고 그 뜻 그대로 쓴다 — 덕분에 반경을 키워도 **구슬이 도는 체감 속도가 유지**된다(각속도를 직접 저작하면 큰 원에서 갑자기 빨라진다). `r > 0` 은 bake 가 보장(`tileRange <= 0` 거절) |
| `flightTime` | `slot.duration` (지속 N초) |
| `damage` | `slot.magnitude` (flat — 계약 10, attacker damageMul 미적용) |
| `pierceRemaining` | 소모하지 않으므로 큰 값 (계약 3) |
| `rehitCooldownSec` | 슬롯의 재타격 쿨타임 |
| `hitThreshold` | **피격 반경** — 슬롯의 `hitThreshold`(unit 0 이 탄 SO 에서 구웠다). 궤도 반경(`maxDistance`)과 다른 축이다 — 하나는 "얼마나 넓게 도나", 하나는 "구슬이 얼마나 굵은가" |
| `dataIndex` / `visualScale` | 슬롯이 이미 들고 있다 |
| `owner` | host (위협 귀속 — 기존 규약) |

⚠ **이 arm 은 `ISystem` 이라 SO 를 읽을 수 없다.** 위 표의 `speed`·`hitThreshold` 는 전부
**슬롯에서** 읽는다(unit 0 의 bake 가 탄 SO 에서 구워 놓는다). 재타격 쿨타임은 여기서 안 실어도
된다 — 탄 SO 소유라 브리지 드레인이 `dataIndex` 로 해석해 채운다.

`targetFaction` 은 **싣지 않는다.** PathHit 의 후보 풀은 `AttackUnitTag` 하드코딩(적 전용)이라
이 페이로드에는 진영 축이 없다 — 화염구가 아군을 때리는 경로가 구조적으로 존재하지 않는다.
통행 층 필터(`PlacementLayers.CanTarget`)는 그대로 타므로 **비행 적을 때릴지는 데이터가 정한다.**

⚠ **중복 발동 방지 여부**: T주기 < N지속 이면 화염구가 겹쳐 쌓인다. 저작으로 `T > N` 을 지키고,
bake 가 `periodSeconds <= duration` 을 **경고**한다(`AllyMoveSpeedAura` 의 반대 방향 경고와 동형).
거절이 아니라 경고 — 겹치기가 의도인 저작도 있을 수 있다.

### 2) 탄 SO — `Projectile_FlameOrb.asset`

기존 화염 계열(`Projectile_Enemy_Fireball` 등)을 참고해 신규 인스턴스. 중요한 값:
`speed`(= 도는 선속도) · `hitThreshold`(= **피격 반경**, 궤도 반경과 별개 축) ·
`rehitCooldownSec`(= **같은 적 재타격 간격**, unit 0 이 이 SO 에 추가한 필드) ·
`visualScale`(작게) · `visualHeightOffset`(타일에 안 깔리게) · `hitPrefab`(스친 순간 작은 임팩트) ·
`preserveVfxColors`. **벤더 VFX 를 쓰면 `project_vendor_projectile_vfx_integration` 의 3대 함정
(무버/RB/Collider 제거, `TrailRenderer.autodestruct=false`)을 먼저 읽는다.**

⚠ `speed / r` 이 각속도이므로 **작은 반경 + 빠른 speed 조합은 unit 1 의 현(chord) 함정**에 걸린다.
초기값은 프레임당 회전각이 작게 나오는 범위로 둔다(아래 `speed≈5`, `r = 1타일` → 약 5/1.28 ≈ 4 rad/s).

### 3) 카드 에셋 — `Card_FlameSpinner.asset`

`id=flame_spinner` · `displayName="불꽃 팽이"` · `type=Unit` · `category=Normal` · `art=null` ·
`axis`/`attachType` 는 기존 Unit 카드 관례를 따른다.
`mechanics[0]`: trigger `PeriodicTimer(periodSeconds=6)` × payload `SelfOrbitProjectile`
(`magnitude=20` · `duration=3` · `tileRange=1` · `projectile=Projectile_FlameOrb`).
**재타격 쿨타임(0.5초)·선속도(5)·피격 반경은 카드가 아니라 탄 SO 가 소유한다.**
**전부 초기값이며 튜닝 대상**이다.
`description` 은 formatter 정확 미러(unit 0 의 문안 함수 결과를 그대로).

## 완료 기준

- 컴파일 0 에러 · 콘솔 경고 0(unhandled payload 경고 포함).
- **Play 육안**: 카드를 유닛에 붙이면 6초마다 화염구가 나타나 3초간 돌고 사라진다. 도는 것이
  화면에서 실제로 원을 그린다(타일에 깔리거나 유닛 뒤로 숨지 않는다).
- **계측**: 궤도 안에 더미 적을 두고 3초 동안 받은 총 피해가 `20 × floor(3 / 0.5)` 근처인가
  (재타격 쿨타임이 실제로 도는가). 1회만 맞으면 unit 2 의 계약 3 이 깨진 것이다.
- **저프레임 확인 1회**(스펙 리뷰 잔여 리스크): 현(chord) 함정의 저작 상한은 60fps 기준이라
  30fps 에서 프레임당 회전각이 2배가 된다. `Application.targetFrameRate = 30` 으로 한 번 돌려
  궤도 위 적을 스쳐 지나가지 않는지 본다. **EditMode 로는 못 잡는다**(dt 가 프레임률에 묶임) —
  그래서 자동 테스트가 아니라 Play 체크리스트 항목이다. 놓치면 안드로이드 실기기에서만
  "가끔 안 맞는" 증상으로 나타난다.
- 카드 문안이 화면에서 읽히고 `description` 과 일치한다.

---

확인 완료 2026-08-16 (사용자 Play 확인) — 커밋 `a630d32e` (+ 차폐/즉시발동/튜닝 `a36e784e`)
