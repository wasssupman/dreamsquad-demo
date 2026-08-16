# 2 — 캐논: 1:1 융단폭격

## 목적

캐논의 배치 스킬을 **반경 2 안 모든 적에게 하늘에서 미사일이 1:1로 떨어지는 융단폭격**으로
바꾼다. 지금은 `MeleeBurst`(r2 · 즉발 80 · 그림 없음)라 "배치했더니 적 체력이 줄었다"만 남는다.

**피해 총량은 바뀌지 않는다.** 적당 정확히 80 으로 기존과 같고(그 성립 조건은 아래 «칸당 1발»),
바뀌는 것은 **예고 0.4초와
하늘에서 내려오는 그림**이다 — 사용자 의도(2026-08-16): *"융단폭격을 비주얼로 살리는 것"*.

**정체성 근거**: 캐논 평타는 `Projectile_CannonBall` 단일 대상 1발(사거리 3 · 2.4초). 배치 스킬은
**모든 적을 동시에 1:1**로 때린다. 다대일 동시 타격은 평타로 구조적으로 불가능하다.

## 변경 대상

- 신규 `Assets/_Project/Data/Projectiles/Projectile_CannonStrike.asset`
- 신규 `Assets/_Project/Data/Projectiles/Pattern_Cannon_Strike.asset`
- 신규 `Assets/_Project/Data/Abilities/Ability_SkyStrike_Cannon.asset`
- `Assets/_Project/Data/Defenders/Defender_Cannon.asset`
- 신규 `Assets/_Project/Tests/PlayMode/OnPlaceSkyStrikeTest.cs`

> 코드 변경은 units 0·1 에서 끝나 있어야 한다. 여기서 브리지/시스템에 분기를 더해야 한다면
> 그건 **앞 unit 의 계약 공백**이다 — 돌아가서 고친다.

## 구현

### `Projectile_CannonStrike` (탄)

`Projectile_NightmareBarrage` 사본(GUID 신규). 공유 `Projectile_Meteor` 를 건드리지 않는 이유는
`projectile-emission-pattern` unit 4 와 같다 — 남이 쓰는 asset 의 방치 필드에 의미를 부여하면
소유가 모호해진다.

| 필드 | 값 | 근거 |
|---|---|---|
| `flightMode` | `SkyFall`(4) | 하늘에서 낙하 |
| `impactTileRange` | **0** | 착탄 칸만. 1(3×3)이면 이웃 칸까지 겹쳐 밀집 시 대상당 피해가 배가된다. ⚠ 0 이어도 **그 칸 전원**은 맞는다 — 「적당 1발」은 emitter 의 칸 dedupe 가 함께 있어야 성립한다 |
| `dropHeight` / `fallPortion` | 9 / 0.35 | barrage 값 유지 |
| `projectilePrefab` | **미사일 룩** | 메테오(돌덩이)와 다른 그림 — 캐논은 포탄/미사일 |
| `hitPrefab` · `hitVfxScale` | 폭발 | barrage 값 유지 |

### `Pattern_Cannon_Strike` (발사 명세)

| 필드 | 값 | 뜻 |
|---|---|---|
| `barrel` | `Projectile_CannonStrike` | 탄 |
| `damage` | 80 | **적당** 피해. 기존 `MeleeBurst` 값 그대로 |
| `selection` | `RoundRobin` | **fan-out 은 selection 을 쓰지 않는다**(후보를 고르지 않고 전부 나간다). 저작값은 무해한 잔재 |
| `fanOutToAllCandidates` | **true** | unit 1 신설. 발수 = 스코프 안 후보 수 |
| `shots` | 1발 · 간격 0 | 한 번의 일제사격(갈래 사이 시차는 아래 `fanOutStaggerSec`) |
| `reselectPerShot` | true | fan-out 은 잠금 경로를 안 타므로 **의미 없다**. 저작 일관성 목적 |
| `scopeTileRange` | **2** | unit 1 신설. 주변 2타일 |
| `fanOutStaggerSec` | **0.08** | 갈래 사이 착탄 시차 — **연타**(사용자 요청 2026-08-16). 3~6칸이면 총 0.24~0.48초 |
| `telegraphSec` | 0.4 | 낙하 예고 = `flightTime` |

- ⚠ `telegraphSec` 0 이면 즉착탄이라 **하늘에서 떨어지는 그림이 한 프레임도 안 보인다.**
  브리지 bake 가 이미 SkyFall 패턴의 `telegraphSec == 0` 을 loud warn 한다.
- ⚠ `randomizeShotsPerTrigger` / `randomIntervalMinSec` / `randomIntervalMaxSec` 는 **전부
  0/false** 로 둔다. 켜지면 `PatternShotRandomizer` 가 **엔티티 index 를 시드로** 쓰므로
  "같은 배치는 항상 같은 결과"가 깨진다.
- **연타로 떨어진다.** 예고 0.4초 뒤부터 0.08초 간격으로 **row-major 순서**(y 작은 칸 먼저)로
  꽂힌다. 적이 그 사이 걸어 나가면 빗나간다 — 조준 폭격의 성질이며 예고·시차 둘 다 Play 튜닝
  대상이다(시차가 클수록 뒤쪽 칸이 더 잘 빗나간다).
- ⚠ **칸당 1발이다(적당 1발이 아니다).** 셀을 겨누는 낙하탄은 `impactTileRange 0` 이어도
  그 칸 **전원**을 때리므로, 같은 칸에 두 발을 떨어뜨리면 두 적이 각자 두 발씩 맞아 피해가
  2배가 된다(리뷰 지적 → 실측 160). emitter 가 셀 바인딩 fan-out 에서 칸을 접는 이유다.

### `Ability_SkyStrike_Cannon` (`UnitSkillAbility`)

```
mechanics[0]:
    trigger.kind    = OnPlace
    payload.kind    = EmitProjectilePattern
    payload.pattern = Pattern_Cannon_Strike
```

`magnitude`/`tileRange`/`duration`/`projectile` 은 패턴·탄으로 이사했으니 0/null 로 비운다
(보스 융단폭격 이관 때와 같은 정리).

### `Defender_Cannon.asset`

| 필드 | 현재 | 변경 |
|---|---|---|
| `abilities` | `[]` | `[Ability_SkyStrike_Cannon]` |
| `onPlaceEffect` | 4 (`MeleeBurst`) | **0 (`None`)** — 레거시 경로 해제 |
| `onPlaceRange` / `onPlaceMagnitude` | 2 / 80 | 0 / 0 (패턴이 소유) |

`projectile`(포탄, 평타)은 **그대로** 둔다 — 배치 스킬 탄과 다른 것이 정상이다(unit 0 이
다연발 bake 의 barrel 일치 검사를 이 경로에 적용하지 않는 이유).

## ⚠ 전투 시작 전 배치는 **미사일이 한 발도 안 뜬다** (실측 2026-08-16)

브리지의 `DrainProjectileSpawnRequests` 는 `Update` 의 `if (!_running) return;` **아래**에 있다.
배치 페이즈에 캐논을 놓으면 트리거·스코프·fan-out 은 전부 정상 동작해 **캐리어까지 만들어지는데**
(실측 `maxCarrier=3`) 아무도 드레인하지 않아 투사체가 0이다.

README 후속 후보 「배치 페이즈 발동 정책」이 가리키던 비용의 실물이다. 「낭비된다」는 기존 사양
그대로 두되, **뒤늦게 터지는 것**은 오작동이라 리뷰 반영으로 막았다 — `StartBattle()` 이
잔류 캐리어를 버린다(그전에는 `_running` 이 켜지는 순간 낡은 좌표로 일제 드레인됐다).
테스트는 `StartBattle()` 뒤에 배치한다.

## 완료 기준

- [x] PlayMode `OnPlaceSkyStrikeTest` 4/4 (2026-08-16)
  - 반경 2 안 적 5마리 → **미사일 정확히 5발**, 착탄 후 **5마리 전원** HP 감소 (1:1 핀)
  - **같은 칸에 적 2마리 → 각자 정확히 80**(칸당 1발 핀. 리뷰 전에는 각 160 이었고 이 단언이
    `> 0` 이라 못 잡았다 — 지금은 저작값 exact 로 잰다)
  - 서로 다른 칸의 인접 적도 **각자 정확히 80**(`impactTileRange 0` 핀)
  - **반경 2 밖 적은 표적이 되지 않는다** (scope 회귀 핀 — 없으면 맵 전체 폭격이 통과한다)
  - 반경 안 적 0마리 → 발사 0, 에러/경고 0
  - **탭 배치(`PlaceDefenderAs`)와 D&D 배치 둘 다에서 발화한다** (unit 0 의 발화 지점 통일 핀 —
    기존 on-place PlayMode 는 전부 탭 경로라 이걸 안 보면 라이브에서 안 나가는 채 초록이 난다)
- [x] 머신거너 다연발 무회귀 — `slots[0]` 을 캐논 패턴이 가져가지 않는다(unit 0 순서 핀의 소비 확인)
- [x] 기존 on-place PlayMode 무회귀
- [ ] Play 육안: 적 무리 옆에 캐논 배치 → **적이 있는 칸마다 미사일이 하나씩 하늘에서
      내려와 짧은 간격으로 줄지어 꽂힌다.** 빠진 칸이 없고, 한 칸에 겹쳐 떨어지지 않는다
- [ ] Play 육안: **전투 시작 전(적 0마리) 배치** → 조용히 아무 일도 안 일어난다(에러 0).
      ⚠ 이 경우 코스트 4 를 내고 화면에 아무 것도 없다 — README 후속 후보 「배치 페이즈 발동 정책」의
      비용을 여기서 확인만 하고 이 spec 에서 고치지 않는다
