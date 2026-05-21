# 9. Cosmic Season Assets

## 목적

Cosmic 시즌의 backdrop PNG + SO 2종 생성. EdgeProp 은 Forest generic 6종 재사용.

## 변경 대상

신규 자산

- `Assets/_Project/Art/Season/cosmic/backdrop_cosmic.png` (Codex 생성, 4096×2048 equirectangular)
- `Assets/_Project/Data/Season/backdrop_S4_cosmic.asset`
- `Assets/_Project/Data/Season/season_S4_cosmic.asset`

수정

- `Assets/_Project/Data/Season/SeasonRegistry.asset` — `allSeasons` 에 season_S4_cosmic 추가

## 구현

### Step 1. backdrop 이미지

Codex 가 4096×2048 equirectangular PNG 생성. 핵심:

- 우주/네뷸라, 소용돌이치는 은하 팔, 흩어진 별, 보라/시안/마젠타 우주 먼지.
- 하단도 명확한 ground horizon X — 어두운 네뷸라 또는 부유 소행성 silhouette 으로 대체.
- 좌우 edge seam 일치, sRGB.

import: textureType Default, mipmap on, max 4096.

### Step 2. SeasonBackdropData

`backdrop_S4_cosmic.asset`:

```
farBackdropTexture = backdrop_cosmic.png
backdropDistance   = 25
backdropHeightWorld= 50
backdropTint       = (1, 1, 1, 1)
edgePadding        = 3
edgeProps[]        = Forest 의 generic 6 (runic_portal/0, stone_altar/1, cannon_turret/2,
                     arcane_lantern/4, coil_machine/7, crystal_node/10).
```

### Step 3. SeasonData

`season_S4_cosmic.asset`:

```
seasonId    = "S4_Cosmic"
displayName = "Astral Drift"
mapTheme    = Assets/_Project/Map/Theme/forest/forest.asset  (공유)
backdrop    = backdrop_S4_cosmic.asset
```

### Step 4. Registry

`SeasonRegistry.allSeasons` += `season_S4_cosmic`.

## 완료 기준

- 3 자산 생성, GUID 충돌 없음, `read_console` clean.
- Inspector 에서 SeasonRegistry.allSeasons.Count == 4.
- Step 10 검증: defaultSeason → Cosmic Play → 네뷸라 skybox 확인.

## 의존

- 선행: 1, 2, 3, 5
- 후행: 10
