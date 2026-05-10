# 5. Forest Season Assets — SO 채움 + EdgeProp 매핑

## 목적

Forest 시즌의 SO 인스턴스를 생성하고, Codex 미커밋 6종 + 신규 2종으로 EdgeProp 8 anchor 를 채운다. 6종 prop_concept 는 EdgeProp 전용으로 격리한다.

## 변경 대상

기존 SO 수정 (EdgeProp 전용 격리)

- `Assets/_Project/Data/Theme/forest/prop_concept_arcane_lantern_1_2.asset`
- `Assets/_Project/Data/Theme/forest/prop_concept_cannon_turret_2_1.asset`
- `Assets/_Project/Data/Theme/forest/prop_concept_coil_machine_1_1.asset`
- `Assets/_Project/Data/Theme/forest/prop_concept_crystal_node_1_1.asset`
- `Assets/_Project/Data/Theme/forest/prop_concept_runic_portal_2_2.asset`
- `Assets/_Project/Data/Theme/forest/prop_concept_stone_altar_2_2.asset`

신규 SO

- `Assets/_Project/Data/Season/season_S1_forest.asset`
- `Assets/_Project/Data/Season/backdrop_S1_forest.asset`
- `Assets/_Project/Data/Season/SeasonRegistry.asset`

신규 PropData + Prefab

- `Assets/_Project/Data/Theme/forest/prop_edge_forest_pine_cluster_2_2.asset` (+ prefab)
- `Assets/_Project/Data/Theme/forest/prop_edge_forest_mossy_boulder_2_1.asset` (+ prefab)

## 구현

### Step 1. 6종 prop_concept SO 격리 (EdgeProp 전용화)

각 SO 에 적용:

- `placementWeight = 0` — `BackgroundPropPlacer.cs:287` 에서 `<= 0` 이면 자동 분포 후보에서 제외됨.
- `billboardMode = PropBillboardMode.None` — EdgeProp 으로 사용될 때 카메라 추종 회전 OFF. (BackdropMounter 도 PropBillboard 를 disable 하지만 SO 단계에서도 명시.)

이 변경은 6종 prop_concept 가 `MapThemeData.tileProps` 에 등록되어 자동 분포에 흘러들 가능성을 차단한다.

### Step 2. 신규 PropData 2종 생성

기존 `PropDataEditor` 활용:

1. `Wassup/PropData` 메뉴 → SO 생성 (`prop_edge_forest_pine_cluster_2_2.asset`).
2. 필드:
   - `id` = 파일명
   - `displayName` = "Forest Pine Cluster"
   - `footprintX = 2`, `footprintY = 2`
   - `placementWeight = 0` (EdgeProp 전용)
   - `visualScale = 1.4`
   - `billboardMode = PropBillboardMode.None`
   - `sourceTexture` = `Generated/Props/Textures/prop_edge_forest_pine_cluster_2_2.png`
3. Inspector 의 `Generate Billboard Prefab` 버튼 → `Assets/_Project/Prefabs/Props/forest/prop_edge_forest_pine_cluster_2_2.prefab` 생성. 텍스처 import 도 자동 정규화.
4. `mossy_boulder_2_1` 도 동일 (`footprintX = 2, footprintY = 1, visualScale = 1.1`).

### Step 3. SeasonBackdropData 생성

`backdrop_S1_forest.asset`:

```
farBackdropTexture = Assets/_Project/Art/Season/forest/backdrop_forest_dawn.png
backdropDistance   = 60
backdropHeightWorld= 30
backdropTint       = (1, 1, 1, 1)
edgePadding        = 1.5

edgeProps[8]:
  [0] propData = prop_concept_runic_portal_2_2,     anchor = NorthLeft,    yaw = 0,  scale = 1.0
  [1] propData = prop_concept_stone_altar_2_2,      anchor = NorthCenter,  yaw = 0,  scale = 1.0
  [2] propData = prop_concept_cannon_turret_2_1,    anchor = NorthRight,   yaw = 0,  scale = 1.0
  [3] propData = prop_concept_arcane_lantern_1_2,   anchor = EastMiddle,   yaw = 0,  scale = 1.0
  [4] propData = prop_concept_coil_machine_1_1,     anchor = SouthCenter,  yaw = 0,  scale = 1.0
  [5] propData = prop_concept_crystal_node_1_1,     anchor = WestMiddle,   yaw = 0,  scale = 1.0
  [6] propData = prop_edge_forest_pine_cluster_2_2, anchor = SouthLeft,    yaw = 0,  scale = 1.0
  [7] propData = prop_edge_forest_mossy_boulder_2_1,anchor = SouthRight,   yaw = 0,  scale = 1.0
```

worldOffset 모두 `(0, 0)`. 6번 검증 후 디자이너 미세 조정 라운드.

### Step 4. SeasonData 생성

`season_S1_forest.asset`:

```
seasonId    = "S1_Forest"
displayName = "Verdant Bloom"
mapTheme    = Assets/_Project/Map/Theme/forest/forest.asset
backdrop    = backdrop_S1_forest.asset
```

### Step 5. SeasonRegistry 생성

`SeasonRegistry.asset`:

```
allSeasons    = [season_S1_forest]
defaultSeason = season_S1_forest
```

### Step 6. BattleScene wiring 재확인

3번 단위에서 wiring 한 `seasonRegistry` 필드가 `SeasonRegistry.asset` 으로 채워졌는지 점검.

## 완료 기준

- 4 SO + 2 PropData + 2 prefab 생성, GUID 충돌 없음.
- 6종 prop_concept SO 의 `placementWeight = 0`, `billboardMode = None` 적용.
- 신규 2종 PropData 의 텍스처 importer 가 PropDataEditor 정책으로 자동 정규화됨.
- `read_console` clean.
- BattleScene 의 BattleBridge.seasonRegistry 가 비어있지 않음.
- Inspector 에서 `season_S1_forest.asset` 의 mapTheme/backdrop 참조 모두 채워짐.

확인 일자: 2026-05-10 / 커밋: db3aa970eb5bebfe5c32dc0ca19df55e3d46becc

## 의존

- 선행: 1번, 2번, 3번, 4번
- 후행: 6번 (Play 검증)
