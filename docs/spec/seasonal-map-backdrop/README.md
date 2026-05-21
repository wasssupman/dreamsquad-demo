# Seasonal Map Backdrop Spec

**상태**: 진행 중 — Forest 완료(unit 0~5+백드롭 재생성), 2026-05-11 Lava/Lunar/Cosmic 추가(스코프 확장)
**구현 주체**: Codex (이미지 생성 + 코드/SO 채움). Claude 는 spec 작성 + 코드 튜닝.

## 목표

토너먼트 시즌마다 맵 외곽을 풍성하게 채우는 백드롭 시스템을 도입한다. 시즌은 `MapTheme + Backdrop 1종` 을 묶어 활성화하고, 외곽은 ① URP Skybox/Panoramic 머티리얼로 사방 일러스트 ② 보드 둘레의 EdgeProp 12개로 구성된다. 본 spec 의 산출은 **4개 시즌 (Forest / Lava / Lunar / Cosmic)** 분.

## 검증 질문 (이 spec 이 답해야 할 것)

- 시즌 SO 1개를 swap 하면 외곽 그림이 통째로 바뀌는가?
- Forest 시즌으로 매치를 시작했을 때, 보드 주변에 8 EdgeProp + 먼 일러스트 백드롭이 자연스럽게 보이는가?
- ECS / 기존 BackgroundPropPlacer 경로를 건드리지 않고 추가되는가?

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | [0_uncommitted_split.md](0_uncommitted_split.md) | Codex 미커밋 자산 중 본 spec 자산만 분리 staging |
| 1 | [1_data_model.md](1_data_model.md) | SeasonData / SeasonBackdropData / EdgeAnchor / SeasonRegistry SO + 스크립트 |
| 2 | [2_backdrop_mounter.md](2_backdrop_mounter.md) | BackdropMounter + AnchorTable + Backdrop_Unlit shader |
| 3 | [3_bridge_integration.md](3_bridge_integration.md) | BattleBridge hook + 씬 wiring |
| 4 | [4_image_generation.md](4_image_generation.md) | Codex 이미지 생성: backdrop 1장 + EdgeProp 신규 2종 PNG |
| 5 | [5_forest_season_assets.md](5_forest_season_assets.md) | Season/Backdrop/Registry SO 채움 + EdgeProp 8 anchor 매핑 |
| 6 | [6_play_verification.md](6_play_verification.md) | Forest Play 진입 후 백드롭/EdgeProp 시각 검증 |
| 7 | [7_lava_season.md](7_lava_season.md) | Lava 시즌 백드롭 + SO + EdgeProp 매핑 |
| 8 | [8_lunar_season.md](8_lunar_season.md) | Lunar 시즌 백드롭 + SO + EdgeProp 매핑 |
| 9 | [9_cosmic_season.md](9_cosmic_season.md) | Cosmic 시즌 백드롭 + SO + EdgeProp 매핑 |
| 10 | [10_multi_season_verify.md](10_multi_season_verify.md) | 4 시즌 swap 검증 (Inspector 에서 defaultSeason 교체 후 Play) |
| 11 | 11_handoff_summary.md (구현 후 작성) | 인계 지도 |

## Feature-Wide 계약

- **Season → MapTheme + Backdrop 1종** 구조 (시즌 풀 X, 시즌당 정확히 1종).
- 시즌 SO 1개에 `MapThemeData` 와 `SeasonBackdropData` 가 함께 묶인다. `SeasonRegistry.activeSeason` 1개만 런타임에서 사용. 본 spec 의 4 시즌 모두 `forest.asset` 을 mapTheme 으로 공유한다 (테마별 타일/장애물 차별화는 별도 spec 으로 분리).
- 외곽은 두 레이어: ① **URP Skybox/Panoramic** (사방 일러스트, 4096×2048 equirectangular PNG) ② 보드 둘레 12 anchor 에 EdgeProp 인스턴스.
- EdgeProp 은 신규 데이터 모델이 아닌 **기존 PropData SO 재사용** (sprite/prefab/visualOffset/visualScale 모두 PropData 가 보유).
- **EdgeProp 전용 PropData 격리 계약**: EdgeProp 으로 사용되는 PropData 는 `placementWeight = 0` (BackgroundPropPlacer 자동 분포 제외) + `billboardMode = None` (정적 풍경, 카메라 추종 회전 OFF). 6종 prop_concept 자산은 본 spec 진입 시 이 계약으로 일괄 격리한다.
- 단일 게이트웨이 `BackdropMounter.Mount(map, camera, backdrop, tileSize)` 가 root GameObject `_Backdrop` 하나를 만들고, `Unmount(ref root)` 가 정리. Mount 는 인스턴스화 직후 PropBillboard 를 disable 하여 SO billboardMode 와 무관한 이중 안전망을 둔다.
- BattleBridge 가 유일한 호출자. `Awake` 에서 `SeasonRuntime.Bind(seasonRegistry)` 한 번 호출. `BuildMapForBattle` 은 `SeasonRuntime.Active.mapTheme` 을 local `theme` 변수로 받아 모든 mapTheme read 를 통일한다. 기존 `mapTheme` SerializedField 는 제거 (시즌이 source of truth).
- 라이프사이클 hook: `TeardownCurrentBattle`, `CleanupDraftMapBeforeRebuild`, `StopBattle`, `OnDestroy` 에서 `BackdropMounter.Unmount`. Mount 직전에도 항상 Unmount 한 번 호출 → RebuildDraftMap 안전.
- ECS 컴포넌트/시스템 신규 0개. 맥락 경계 영향 없음.
- Skybox 머티리얼은 Mount 직전 생성 → `RenderSettings.skybox` 에 주입, Unmount 시 이전 skybox/clearFlags 복원. 카메라 `clearFlags = Skybox` 도 Mount/Unmount 가 함께 토글.

## EdgeAnchor 정의

보드 perimeter 시계방향 12 슬롯: `NorthLeft / NorthCenter / NorthRight / EastTop / EastMiddle / EastBottom / SouthRight / SouthCenter / SouthLeft / WestBottom / WestMiddle / WestTop`. 각 슬롯의 월드 좌표는 `BackdropAnchorTable.Resolve(anchor, boardHalf, padding)` 로 산출. 본 Forest 시즌은 8 슬롯만 채운다: 북면 3 (Left/Center/Right) + 남면 3 (Left/Center/Right) + 동면 1 (Middle) + 서면 1 (Middle).

## 네이밍 규칙

- 시즌 SO: `season_{seasonId}.asset` (e.g. `season_S1_forest.asset`)
- 백드롭 SO: `backdrop_{seasonId}.asset`
- Registry SO: `SeasonRegistry.asset` (1개)
- 백드롭 텍스처: `Assets/_Project/Art/Season/{themeName}/backdrop_{themeName}_{variant}.png`
- EdgeProp PropData/prefab: 기존 prop 명명 규칙 유지 (`prop_{category}_{name}_{x}_{y}`)

## 비목표

- 시즌당 2~3종 랜덤 풀 (시즌당 1종 고정).
- Cubemap 6면 백드롭 (Skybox/Panoramic 1장으로 충분).
- 절벽 메쉬·입체 지형 (없음).
- 시즌 매칭 메타 (registry.defaultSeason 만 활성. 토너먼트 server hook 은 후속).
- 시즌별 차별화된 MapThemeData (타일/장애물). 본 spec 4 시즌 모두 forest 테마 공유.

## 후속 후보 (이 spec 종료 후 도큐멘트 README 의 Follow-up Backlog 으로 이관)

- 시즌별 차별화된 MapThemeData (Lava 타일/장애물, Lunar 타일/장애물 등) — 별도 spec
- EdgeProp 디자이너 manual 튜닝 라운드 (worldOffset / yawDegrees / scale)
- 백드롭 미세 시차 (camera 미세 이동에 skybox _Rotation 살짝 변화)
- Backdrop ↔ MapTheme 라이팅·포그 매칭 룩 패스
- 토너먼트 메타 hook: 서버 응답 → activeSeason swap
- 시즌 활성 시 매치 시작 UI 에 시즌 배지 노출
