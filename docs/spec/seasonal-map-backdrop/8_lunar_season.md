# 8. Lunar Season Assets

## 목적

Lunar 시즌의 backdrop PNG + SO 2종 생성. EdgeProp 은 Forest generic 6종 재사용.

## 변경 대상

신규 자산

- `Assets/_Project/Art/Season/lunar/backdrop_lunar.png` (Codex 생성, 4096×2048 equirectangular)
- `Assets/_Project/Data/Season/backdrop_S3_lunar.asset`
- `Assets/_Project/Data/Season/season_S3_lunar.asset`

수정

- `Assets/_Project/Data/Season/SeasonRegistry.asset` — `allSeasons` 에 season_S3_lunar 추가

## 구현

### Step 1. backdrop 이미지

Codex 가 4096×2048 equirectangular PNG 생성. 핵심:

- 달빛이 내려앉은 황혼/밤, 큰 보름달 (가운데 X — wrap 안 되도록 한쪽으로 오프셋).
- 보드 아래 horizon 에는 검은 첨탑/암석 silhouette, 차가운 teal-violet 팔레트, 은빛 rim light.
- 옅은 안개 + 먼 창백한 구름.
- 좌우 edge seam 일치, sRGB.

import: textureType Default, mipmap on, max 4096.

### Step 2. SeasonBackdropData

`backdrop_S3_lunar.asset`:

```
farBackdropTexture = backdrop_lunar.png
backdropDistance   = 25
backdropHeightWorld= 50
backdropTint       = (0.9, 0.95, 1, 1)   (살짝 차갑게)
edgePadding        = 3
edgeProps[]        = Forest 의 generic 6 (runic_portal/0, stone_altar/1, cannon_turret/2,
                     arcane_lantern/4, coil_machine/7, crystal_node/10).
```

### Step 3. SeasonData

`season_S3_lunar.asset`:

```
seasonId    = "S3_Lunar"
displayName = "Pale Crescent"
mapTheme    = Assets/_Project/Map/Theme/forest/forest.asset  (공유)
backdrop    = backdrop_S3_lunar.asset
```

### Step 4. Registry

`SeasonRegistry.allSeasons` += `season_S3_lunar`.

## 완료 기준

- 3 자산 생성, GUID 충돌 없음, `read_console` clean.
- Inspector 에서 SeasonRegistry.allSeasons.Count == 3.
- Step 10 검증: defaultSeason → Lunar Play → 달빛 차가운 skybox 확인.

## 의존

- 선행: 1, 2, 3, 5
- 후행: 10

확인 일자: 2026-05-22 / 커밋: 4883741
