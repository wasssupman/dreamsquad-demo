# 6 — Handoff Summary

## Commit
- `a7e794b` `feat(view): Tilemap 광역 터레인 + 배경/원경 프랍 (tilemap-world-surround 0~5)` · **main 머지됨** (fast-forward, main tip `0f07a8c`, 2026-06-26).

## Implemented (단위 0~5, 전부 Presentation 계층 · sim/ECS 무영향)
- **내부 Deco 생성**: MapGrid 맵에 `ObstaclePlacer.DesignateDeco`(Place→Deco, Walk 불변, 시드 결정적). 노브 `MapThemeData.mapGridBuildableKeepRatio`(forest=0.6).
- **보드 내부 프랍**: `BattleBridge` gate 해제 + `TilemapMapView.InstantiateBackgroundProps`(좌표 grid 권위=BoardSpace 수식). 근경 그림자 CAST.
- **같은-category 인접 회피 룰**: `PropData.category/sameCategoryMinDistanceCells` + `BackgroundPropPlacer.ViolatesSameCategory`. 꽃 연속 배치 방지(꽃=2).
- **프랍 에셋 7종**: Test 스프라이트(flower×3/rock×3/tree) → prefab+PropData+머티리얼. 그림자용 `URP/Unlit+_ALPHATEST_ON`.
- **외곽 터레인 링**: `PaintSurroundRing` — 보드 밖에 **플레이 영역과 동일 풀 타일**(decoTile) + `Color.Lerp(흰색, surroundFarColor, t)` 그라데이션(안쪽=원색 매끄러운 연속, 바깥=어두움) + Perlin 노이즈로 banding 제거.
- **원경 프랍(침엽수림)**: `InstantiateRingProps` — 링 scatter + falloff, 그림자 OFF, 꽃 제외. `distantRingWeight`(tree=14·rock=1) + density 0.55 로 빽빽한 침엽수림 backdrop(~230).
- **모바일 예산**: `mobilePropBudgetScale`(0.5) — 모바일에서 근경/원경 프랍 솎음. 그림자: **근경 = blob 통일(데스크톱/모바일, 9rev)** / 원경 off / 캐릭터는 데스크톱 real cast 유지.
- **근경 프랍 blob(9, 9rev)**: 프랍 real cast 가 데스크톱에서도 동작 안 해, real cast 제거하고 발밑 타원 blob 으로 통일(`BlobShadow.Attach`, size×visualScale). 원경 링은 그림자 없음(사용자 지정). `9_prop_blob_shadow.md`.

## Key Files
- `Scripts/Bridge/BattleBridge.cs` (Deco designate · prop gate · ring 호출 · 모바일 예산 · 그림자 정책)
- `Scripts/Core/TilemapMapView.cs` (VisualPlan · InstantiateBackgroundProps · PaintSurroundRing · InstantiateRingProps · CellCenterToWorld)
- `Scripts/Data/BackgroundPropPlacer.cs` (theme threading · ViolatesSameCategory)
- `Scripts/Data/{PropData,TileSetData,MapThemeData,ObstaclePlacer}.cs`, `Scripts/Presentation/PropBillboard.cs`
- 에셋(정식화 후): `Prefabs/Props/forest/`(prefab+mat), `Data/Theme/forest/prop_{flower_*,rock_*,tree}.asset`, `Art/Theme/forest/`(스프라이트 7), `Map/Theme/forest/forest.asset`(tileProps 교체·튜닝), `Generated/Tiles/AutoTileTest/TileSet_AutoTileTest.asset`(ring 값), `Scenes/BattleScene.unity`(PropsTilemap 비활성)

## Verified
- compile 0 에러. Editor Play(BattleScene, MapGrid+Tilemap): Deco grass + 근경 프랍(그림자 CAST 확인) + 외곽 링(유기적 페이드) + 원경 트리/돌. 가독성 유지.
- 객관: 꽃 인접 0쌍(Chebyshev≥2). 페인트 32×22. 데스크톱 풀카운트 / 모바일 배율 로직.
- console: 기존 missing-script 경고 4건(무관), URP Forward+ 경고(무관). CS 에러 0.

## Notes (되돌리면 안 되는 의도)
- 프랍 좌표는 반드시 `CellCenterToWorld`(grid). raw `(x,y)*tileSize` 금지.
- 링 셀은 sim 무관 — `GeneratedMap.gridSize`/`BoardSpace.Configure` 는 N×M 유지.
- Legacy3D 경로 불변: ObstaclePlacer.Place 동일 keepTarget, MapView 헬퍼는 visibility만 internal 확대.
- forest 테마 tileProps 가 기존 21종 → keeper 7종으로 교체됨. 7종은 `forest` 정식 위치로 승격(264097d, R100 GUID 보존). 옛 21종 `prop_style_*` 에셋은 아직 보존(고아 — 3번에서 정리).
- 씬 저장 시 PropsTilemap 비활성 포함. 프리뷰 타일 유입 주의(edit 모드 저장).

## Follow-up

### 다음 작업 (이월 — 사용자 지정 2026-06-26)
다음 세션이 이어받을 우선 작업. 본 spec 본체는 완료, 아래는 "정식 자산화 + 잔여 토대" 정리.

- ~~**머지 결정**: `feat/tilemap-world-surround` → main 머지/PR 여부.~~ — **완료** (ff 머지, main `0f07a8c`, remote 없어 PR 생략).
- ~~**프랍 에셋 정식화**: `Data/Theme/test/`·`Prefabs/Props/test/`·`Generated/Tiles/Test/` → 정식 위치 승격.~~ — **완료** (264097d. 28 에셋 R100 rename → `Data/Theme/forest/`·`Prefabs/Props/forest/`·`Art/Theme/forest/`. forest.asset 참조·PropBillboard 295 런타임 검증. 파일명 유지, 폴더만 승격).
- **고아 에셋 정리**: forest.tileProps 가 기존 21종 → test 7종으로 교체되며 옛 21종 프랍 고아 → 삭제/보존 결정.
- ~~**4b 프랍 틸트**: `PropData.tiltAngle` + `Tilted` 미사용 → 적용 or 휴면 결정.~~ — **완료** (316d45a, `7_prop_tilt_apply.md`. 7종 Tilted 전환, flower 38°/rock 45°/tree 50°. 코드 무변경, 원근감 회복).
- **Legacy 환경 프랍 노출 확인**: `tilemapHiddenEnvironment: []`(빈 배열). 스크린샷상 누출 없어 사실상 해소로 보이나 확정 확인.

#### 프랍 비주얼 가림 회피 (occlusion-aware placement) — 해소됨

틸트(7) 후 큰 프랍이 `+y`(셀, 실측 확정)로 누워 플레이 유닛을 가리는 문제. 원경/근경 각각 해소:

- **원경 = 하단 클리어런스 (8, 8rev)**: `ringPlayClearanceCells`(forest=3). 링이 플레이 `+y` 방향에
  플레이를 둘 때(=플레이 하단 링)만 비운다(`WouldOccludePlay`, dy∈[1,r]). 상/좌/우 원경은 빽빽한 숲 유지.
- **근경 = 명시 visual footprint (10)**: per-prop `visualFootprint`(width×depth, tree 1x4·rock_l 1x2·나머지
  1x1, PropData+prefab 파일명 `_WxD` 라벨). 발 셀 `+y`로 depth 셀이 플레이 침범하면 배치 거부
  (tilemap 전용 `occlusionAware`). 큰 나무는 플레이 뒤(+y)/가장자리로, 보드 내부는 낮은 꽃/돌 → 시인성+산속마을.
- **occlusion 통합 (12)**: 원경/근경이 `BackgroundPropPlacer.OccludesPlay(plan, originX, originY, width, depth)`
  공유. `+y` 방향은 이 한 곳에만 하드코딩(카메라 고정). 근경=`(footX,footY,vf.x,vf.y-1)`, 원경=`(x,y,2r+1,r)`.

#### 프랍 발 피벗 + 그림자 정책 (11, 9rev)
- **발 피벗 (11)**: `visualOffset.y=0` — sprite 하단 pivot 이 곧 발. tilemap 부모 90° 가 `(0,0,0)`을 그대로 둠 →
  좌표계 오버로드(스프라이트 +z 깊이 밀림)·부모 결합·blob 어긋남 동시 해소. **부양/blob 땜빵의 근본**.
  Legacy3D=MapGrid 라 keeper 프랍 미사용 → 무영향. (코드 리뷰 2026-06-26 식별)
- **그림자 정책**: 프랍=blob only(SpriteRenderer 가 ShadowCaster pass 없어 real cast 구조적 불가, dead 제거됨) /
  캐릭터=real(desktop)+blob(mobile) / 원경=무그림자(의도). blob=발 피벗 정렬(df3729d).

### 폐기 (사용자 지정 2026-06-26)
- ~~멀티 시즌(lava/lunar/cosmic) 테마에 동일 ring/deco 처리~~ — **드롭**. forest(S1)만 유지.

### 장기 후보 (당장 불필요)
- 실기기 프로파일링 후 프랍 캡/밀도/`mobilePropBudgetScale` 재튜닝.
- tilemap 모드 시즌 스카이박스(현재 다크 페이드 대체).
- 프랍 그림자 strength/soft 튜닝(tilemap-real-shadows 종속).
- 원경 프랍 blob 확장(데스크톱 포함 원경 접지) — 9 의 후속. 현재 근경만(사용자 지정), 원경은 그림자 없음.
- (선택) 원경 나무가 안쪽 링 가장자리 약간 침범 → 안쪽 1~2 링 나무 비우기.

#### 코드 리뷰 후속 (2026-06-26 리뷰, 11/12 에서 critical 해소 후 잔여)
- **테스트** [S] · `BackgroundPropPlacer.OccludesPlay`(public static) + visualFootprint EditMode 회귀 테스트.
  "게임플레이 가림 방지 핵심 순수 로직인데 커버리지 0" (리뷰 강권). +y depth/width 중심정렬/상하좌우 케이스.
- **`Billboard` ↔ `PropBillboard` 통합** [M] · 둘 다 `Euler(tilt,0,0)`. 틸트 로직 공유. 대규모라 보류.
- **minor** [S] · `_WxD` 파일명↔`visualFootprint` drift 위험(필드가 source). `TryPlaceNearestCandidate`↔`TryPlace`
  중복 fit/occlusion 검사(M5). 모바일 `placements.GetRange` truncation 이 공간이 아닌 순서 기반(M7).
- **`rotationYaw`** [S] · tilemap 경로에서 미적용(PropBillboard override). `PropPlacement` 공용이라 Legacy(MapView)가
  아직 사용 → 제거 불가. tilemap 전용이면 적용 or 문서화.
