# 2 — 캐논: 1:1 융단폭격

## 목적

캐논의 배치 스킬을 **반경 2 안 모든 적에게 하늘에서 미사일이 1:1로 떨어지는 융단폭격**으로
바꾼다. 지금은 `MeleeBurst`(r2 · 즉발 80 · 그림 없음)라 "배치했더니 적 체력이 줄었다"만 남는다.

**피해 총량은 바뀌지 않는다.** 적당 정확히 80 으로 기존과 같고, 바뀌는 것은 **예고 0.4초와
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
| `impactTileRange` | **0** | **1:1 타격.** 1(3×3)이면 스플래시가 겹쳐 밀집 시 대상당 피해가 4배가 되고(rev2 초안의 자릿수 오류) "1:1" 이 거짓이 된다 |
| `dropHeight` / `fallPortion` | 9 / 0.35 | barrage 값 유지 |
| `projectilePrefab` | **미사일 룩** | 메테오(돌덩이)와 다른 그림 — 캐논은 포탄/미사일 |
| `hitPrefab` · `hitVfxScale` | 폭발 | barrage 값 유지 |

### `Pattern_Cannon_Strike` (발사 명세)

| 필드 | 값 | 뜻 |
|---|---|---|
| `barrel` | `Projectile_CannonStrike` | 탄 |
| `damage` | 80 | **적당** 피해. 기존 `MeleeBurst` 값 그대로 |
| `selection` | `RoundRobin` | fan-out 의 순회 축 (`fireCount % n` → 후보 0..n-1 각 1회) |
| `fanOutToAllCandidates` | **true** | unit 1 신설. 발수 = 스코프 안 후보 수 |
| `shots` | 1발 · 간격 **0.06** | **한 표적당** 1발. 간격은 발 사이 캐스케이드(융단폭격 느낌) |
| `reselectPerShot` | **true** | fan-out 필수. false 면 loud warn 후 fan-out off |
| `scopeTileRange` | **2** | unit 1 신설. 주변 2타일 |
| `telegraphSec` | 0.4 | 낙하 예고 = `flightTime` |

- ⚠ `telegraphSec` 0 이면 즉착탄이라 **하늘에서 떨어지는 그림이 한 프레임도 안 보인다.**
  브리지 bake 가 이미 SkyFall 패턴의 `telegraphSec == 0` 을 loud warn 한다.
- ⚠ `randomizeShotsPerTrigger` / `randomIntervalMinSec` / `randomIntervalMaxSec` 는 **전부
  0/false** 로 둔다. 켜지면 `PatternShotRandomizer` 가 **엔티티 index 를 시드로** 쓰므로
  "같은 배치는 항상 같은 결과"가 깨진다.
- **간격 0.06 × 최대 후보 수**가 총 낙하 시간이다. 반경 2(최대 25칸)에 적이 가득해도 1.5초.
  적이 그 사이 걸어 나가면 빗나간다 — 조준 폭격의 성질이며 간격은 Play 튜닝 대상이다.

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

## 완료 기준

- [ ] PlayMode `OnPlaceSkyStrikeTest`
  - 반경 2 안 적 5마리 → **미사일 정확히 5발**, 착탄 후 **5마리 전원** HP 감소 (1:1 핀)
  - **같은 칸에 적 2마리 → 미사일 2발**(dedupe 하지 않는다 — 한 명이 공짜로 살면 안 된다)
  - 각 적이 받는 피해가 **정확히 80**(스플래시 중복 없음 — `impactTileRange 0` 핀)
  - **반경 2 밖 적은 표적이 되지 않는다** (scope 회귀 핀 — 없으면 맵 전체 폭격이 통과한다)
  - 반경 안 적 0마리 → 발사 0, 에러/경고 0
  - **탭 배치(`PlaceDefenderAs`)와 D&D 배치 둘 다에서 발화한다** (unit 0 의 발화 지점 통일 핀 —
    기존 on-place PlayMode 는 전부 탭 경로라 이걸 안 보면 라이브에서 안 나가는 채 초록이 난다)
- [ ] 머신거너 다연발 무회귀 — `slots[0]` 을 캐논 패턴이 가져가지 않는다(unit 0 순서 핀의 소비 확인)
- [ ] 기존 on-place PlayMode 무회귀
- [ ] Play 육안: 적 무리 옆에 캐논 배치 → **적 머리마다 미사일이 하나씩, 짧은 간격으로 줄지어
      내려와 터진다.** 한 곳에 뭉쳐 떨어지지 않고, 빠진 적이 없다
- [ ] Play 육안: **전투 시작 전(적 0마리) 배치** → 조용히 아무 일도 안 일어난다(에러 0).
      ⚠ 이 경우 코스트 4 를 내고 화면에 아무 것도 없다 — README 후속 후보 「배치 페이즈 발동 정책」의
      비용을 여기서 확인만 하고 이 spec 에서 고치지 않는다
