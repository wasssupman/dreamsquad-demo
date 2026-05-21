# 7. Lava Season Assets

## 목적

Lava 시즌의 backdrop PNG + SO 2종 생성. EdgeProp 은 Forest 의 generic 6종 재사용 (Lava 전용 prop 은 후속 spec).

## 변경 대상

신규 자산

- `Assets/_Project/Art/Season/lava/backdrop_lava.png` (Codex 생성, 4096×2048 equirectangular)
- `Assets/_Project/Data/Season/backdrop_S2_lava.asset`
- `Assets/_Project/Data/Season/season_S2_lava.asset`

수정

- `Assets/_Project/Data/Season/SeasonRegistry.asset` — `allSeasons` 에 season_S2_lava 추가

## 구현

### Step 1. backdrop 이미지

Codex 의 image generation 스킬로 4096×2048 equirectangular PNG 생성. 핵심:

- 화산 풍경, 흐르는 용암 (하단 horizon), 검은 화산 silhouette, 부유 불꽃, 깊은 적/오렌지 팔레트, 연기 낀 검은 하늘.
- 좌우 edge seam 일치 (x=0 == x=4095 pixel 매칭).
- sRGB.

import: textureType Default, mipmap on, max 4096. (Forest backdrop 과 동일 정책.)

### Step 2. SeasonBackdropData

`backdrop_S2_lava.asset` 필드:

```
farBackdropTexture = backdrop_lava.png
backdropDistance   = 25         (Skybox 사용 시 무시되지만 인터페이스 호환)
backdropHeightWorld= 50
backdropTint       = (1, 1, 1, 1)
edgePadding        = 3
edgeProps[]        = Forest 의 generic 6 (runic_portal, stone_altar, cannon_turret,
                     arcane_lantern, coil_machine, crystal_node) 를 anchor
                     0/1/2/4/7/10 에 매핑. 스케일/위치는 Forest tuning 따라간다.
```

forest-specific 2종 (pine_cluster, mossy_boulder) 은 Lava 시즌에서 **사용하지 않는다**.

### Step 3. SeasonData

`season_S2_lava.asset`:

```
seasonId    = "S2_Lava"
displayName = "Molten Tide"
mapTheme    = Assets/_Project/Map/Theme/forest/forest.asset  (공유)
backdrop    = backdrop_S2_lava.asset
```

### Step 4. Registry

`SeasonRegistry.allSeasons` += `season_S2_lava`. `defaultSeason` 은 그대로 (Forest).

## 완료 기준

- 3 자산 생성, GUID 충돌 없음, `read_console` clean.
- Inspector 에서 SeasonRegistry.allSeasons.Count == 2 확인.
- Step 10 멀티 시즌 검증에서 defaultSeason 을 임시로 Lava 로 바꿔 Play → 적색 화산 skybox 가 보드를 둘러싸야 함.

## 의존

- 선행: 1, 2, 3, 5
- 후행: 10 (multi-season verify)

확인 일자: 2026-05-22 / 커밋: 4883741
