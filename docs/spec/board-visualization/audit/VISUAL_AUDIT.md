# Board Visualization Visual Audit

**감사일**: 2026-04-24  
**테마**: Forest  
**근거 이미지**: `Assets/Screenshots/audit/20260424/`  
**제약**: Unity MCP session unavailable 로 신규 Play 캡처를 생성하지 못했다. 동일 날짜에 남아 있던 board visualization 캡처 9장을 audit 폴더로 복제해 카탈로그 근거로 사용했다. 파일명은 프로토콜 seed 슬롯(`12345`~`12347`)에 맞춰 정리했지만, 원본 캡처의 실제 seed는 확인하지 못했다.

## Screenshot Catalog

| 파일 | 용도 | 해상도 |
|---|---|---|
| `seed12345_game_full.png` | 전체 game view | 1920x1080 |
| `seed12345_game_close.png` | 근접/edge detail 대체 | 1920x1080 |
| `seed12345_scene_top.png` | scene view 대체 | 388x844 |
| `seed12346_game_full.png` | 전체 game view | 1920x1080 |
| `seed12346_game_close.png` | 근접/edge detail 대체 | 1920x1080 |
| `seed12346_scene_top.png` | scene view 대체 | 1178x844 |
| `seed12347_game_full.png` | 전체 game view | 1920x1080 |
| `seed12347_game_close.png` | clean full/close 대체 | 1920x1080 |
| `seed12347_scene_top.png` | scene view 대체 | 1178x844 |

## Findings

### V-001: 프랍이 anchor 주변에 따로 노는 단일 개체로 읽힘
- 축: A
- 위치: `seed12345_game_full.png`, 좌측/중앙 Env 영역 전반
- 증상: 나무, 수정, 버섯, 바위가 개별 anchor에 하나씩 꽂힌 느낌이 강하고 같은 family cluster가 자연스러운 군집으로 읽히지 않는다. 일부 Env region은 props가 거의 없어 큰 녹색 면으로 남는다.
- 재현: forest / audit set 전반 / 반복 관찰
- 심각도: High
- 가설: 현 placer가 anchor list를 seed로 순회하며 주변 후보를 충분히 Poisson 분포로 확장하지 못한다. `clusterProbability`/`clusterCount`가 asset에 있어도 후보 밀도와 family grouping이 약하다.
- 후속 spec 후보: 17

### V-002: prop footprint white square가 시각 노이즈로 남음
- 축: A
- 위치: `seed12345_game_full.png`, 분홍 버섯/수정/바위 주변
- 증상: 여러 prop 아래 흰 사각 footprint/outline이 남아 prop을 장식물이 아니라 debug marker처럼 보이게 한다.
- 재현: forest / `seed12345_game_full.png`, `seed12347_game_full.png`
- 심각도: High
- 가설: prop prefab 또는 prop billboard 하위 marker가 audit build에서 비활성화되지 않았다. 분포 품질보다 먼저 제거되어야 할 시각 artifact다.
- 후속 spec 후보: 17

### V-003: inner corner overlay가 sprite corner가 아니라 회전 사각 패치로 읽힘
- 축: B
- 위치: `seed12345_game_full.png`, Place L자 경계 다수
- 증상: inner corner가 자연스러운 꺾인 경계가 아니라 작은 흰/밝은 사각 패치 또는 edge strip 조각으로 떠 보인다. 45도 회전 흔적이 보여 타일 위에 임시 quad가 얹힌 인상이 강하다.
- 재현: forest / audit set 전반
- 심각도: High
- 가설: `placeInnerCornerTexture` 자산 또는 quad scale/yaw가 corner silhouette을 만들지 못한다. inner corner 전용 sprite pass와 band width 조정이 필요하다.
- 후속 spec 후보: 18

### V-004: outer edge fringe가 너무 밝고 균일해 grid감을 강화함
- 축: C
- 위치: `seed12345_game_full.png`, Place 외곽과 Env 접점 전반
- 증상: Place tile 외곽 흰 strip이 모든 방향에서 동일 두께로 반복되어 배치 구역을 자연스럽게 분리하기보다 grid outline을 강조한다.
- 재현: forest / audit set 전반
- 심각도: High
- 가설: edge opacity와 sprite shape가 placeholder 성격이며, outer corner/straight edge가 같은 재료감으로 처리된다. edge sprite/opacity 분리와 corner 연결 마감이 필요하다.
- 후속 spec 후보: 19

### V-005: Walk 경로는 방향은 맞지만 보드와 별도 레이어처럼 떠 보임
- 축: E
- 위치: `seed12345_game_full.png`, 중앙 세로/가로 Walk 교차부
- 증상: Straight/Corner 연결 방향은 대체로 맞지만, Walk가 주변 Env/Place와 재질적으로 섞이지 않고 한 장의 매끈한 노란 도로가 위에 얹힌 느낌이다.
- 재현: forest / audit set 전반
- 심각도: Mid
- 가설: walk shape sprite의 edge feather/shoulder가 부족하고, 주변 zone과 색/밝기 연결이 약하다.
- 후속 spec 후보: 21

### V-006: Env variation은 보이지만 blend보다 patchwork로 읽힘
- 축: D
- 위치: `seed12345_game_full.png`, 하단 Env strip 및 좌측 큰 Env region
- 증상: 2종 이상 grass/noise variation은 관찰되지만 일부 구간이 넓은 직사각 패치처럼 구분된다. region 간 blend band는 명확히 보이지 않거나 텍스처 차이보다 grid seam이 먼저 보인다.
- 재현: forest / `seed12345_game_full.png`, `seed12346_game_close.png`
- 심각도: High
- 가설: 셀별 texture run grouping은 작동하지만 noise scale/variation weight가 큰 면을 유기적으로 쪼개지 못한다. blend band도 alpha/폭/texture 선택이 약하다.
- 후속 spec 후보: 20

### V-007: 전체 화면이 보드보다 패치워크로 읽힘
- 축: F
- 위치: `seed12345_game_full.png`, 전체 보드
- 증상: Place 흰 slab, Walk 노란 도로, Env 녹색 texture, white prop markers가 서로 다른 시각 언어로 충돌한다. Enter the Gungeon식 연속된 바닥보다 프로토타입 tile atlas 조합처럼 보인다.
- 재현: forest / audit set 전반
- 심각도: High
- 가설: zone별 palette/value 대비와 edge/prop marker가 함께 문제를 만든다. 테마 palette pass에서 zone 톤과 debug-looking 요소를 정리해야 한다.
- 후속 spec 후보: 22

### V-008: Scene view audit 캡처 품질이 기준에 미달
- 축: F
- 위치: `seed12345_scene_top.png`, `seed12346_scene_top.png`, `seed12347_scene_top.png`
- 증상: scene view 캡처에 Unity toolbar/gizmo가 포함되거나, top-down이 아니라 측면 close view에 가깝다. 구조 검토 보조 자료로는 쓸 수 있지만, 프로토콜의 `scene_top.png` 역할을 완전히 대체하지 못한다.
- 재현: current audit set
- 심각도: Low
- 가설: Unity MCP session unavailable 상태에서 기존 캡처를 재사용했기 때문이다. 다음 audit 반복 때 SceneView top-down/gizmo off를 새로 확보해야 한다.
- 후속 spec 후보: 미분기

### V-009: prop visualScale 이중 적용으로 프랍 크기가 축소됨
- 축: A
- 위치: `Assets/Screenshots/audit/20260424_17b/seed12345_game_full.png`, `Assets/Screenshots/audit/20260424_17b/seed12345_game_close.png`
- 증상: 17 이후 `prop.visualScale` 이 root transform 과 `PropBillboard.visualRoot` 에 중복 적용되어 visualScale 1.0 미만 프랍이 설계 크기보다 작게 보였다.
- 재현: forest / 17, 18, 19 적용 후 동일 seed 재캡처 비교
- 심각도: 해소
- 가설: `MapView.InstantiateBackgroundProps` 에서 `prop.visualScale * placement.scale` 을 root scale 로 적용한 것이 원인이다. root 는 placement jitter 만 담당하고 visual scale 은 `PropBillboard.ApplyData` 가 담당해야 한다.
- 후속 spec 후보: 17b
- 해소: `MapView.InstantiateBackgroundProps` root scale 을 `placement.scale` 만 사용하도록 수정했다. 확인 커밋: 6dfa019

### V-010: 캐릭터가 프랍에 의해 무조건 가려짐
- 축: A
- 위치: `Assets/Screenshots/audit/20260424_26/seed12345_game_full.png`, `Assets/Screenshots/audit/20260424_26/seed12345_game_close.png`
- 증상: 17b 재검수에서 캐릭터 view 가 프랍보다 낮은 고정 렌더 순서를 사용해 인접 프랍 아래로 깔렸다.
- 재현: forest / 17 이후 캐릭터와 prop 이 겹치는 동일 seed 캡처
- 심각도: 해소
- 가설: Enemy/fallback Defender 는 ECS RenderMesh 경로라 `Renderer.sortingOrder` 체계에 없었고, Spine Defender 도 y 기반 sortingOrder 갱신이 없었다.
- 후속 spec 후보: 24, 25, 26
- 해소: Enemy/fallback Defender 를 Mono quad view 로 이관하고 `BoardSortOrder` 공식을 프랍/캐릭터에 공통 적용했다. 캐릭터는 같은 셀 프랍보다 `CharacterOffset` 만큼 앞선다. 확인 커밋: 4462819

## Dispatch Summary

| 후속 spec | 생성 필요성 | 근거 finding |
|---|---|---|
| 17 `17_poisson_proper.md` | 열기 | V-001, V-002 |
| 18 `18_corner_asset_pass.md` | 열기 | V-003 |
| 19 `19_place_edge_finish.md` | 열기 | V-004 |
| 20 `20_env_variation_tuning.md` | 열기 | V-006 |
| 21 `21_walk_shape_polish.md` | 열기 | V-005 |
| 22 `22_theme_palette_pass.md` | 열기 | V-007 |
| 23 `23_volcano_theme_fill.md` | 보류 | 이번 audit는 forest만 관찰. volcano 별도 캡처 없음. |
