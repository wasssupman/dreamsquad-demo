# 2 — 패키지 · 죽은 파일 · 이름 · 문서

## 목적

단위 0·1 뒤 `UnityEngine.Tilemaps` 를 쓰는 파일이 0 이다. 패키지와 이름에서도 Tilemap 을 지운다.

## 변경 대상

1. **manifest**: `com.unity.2d.tilemap.extras` 제거(RuleTile 류 사용 0 — 벤더·플러그인 포함 전수 확인). 그 다음 **별도 커밋**으로 `com.unity.modules.tilemap` 제거(빌트인 모듈 — URP 2D 렌더러는 이 모듈 없이 동작한다; 컴파일이 깨지면 그 커밋만 되돌린다). `packages-lock.json` 은 에디터 resolve 결과를 싣는다.
2. **삭제**: `Scripts/Data/TerrainTileRenderInfo.cs`(소비처 0 — 옛 외곽 터레인 링의 렌더 정보).
3. **개명**: `CoreSpineUnitView.ApplyTilemapShadow` → `ApplyShadowCasting`(주석 「tilemap-real-shadows — Tilemap 모드 그림자」 → 「보드 평면 그림자」) · 카메라 프리셋 `CameraPreset_TilemapRect` → `CameraPreset_Board`(에셋이 있으면 git mv, 참조는 GUID) · `DefenderUnitData:130` 「Tilemap 그리드라 바닥이 월드 XY 평면」 → 「보드 평면이 월드 XY 평면」 · `BoardSortOrder` 의 `TilemapMapView` 인용 주석은 이력이라 그대로.
4. **문서**: `object-pipeline-map.md`(오버레이 행 — Tile 에셋 → 스프라이트 · Grid 언급) · `map-stage-authoring.md:11`(「논리 격자」 표현 유지, `Grid` 컴포넌트 언급 있으면 삭제) · `lessons/03` 머리에 「Tilemap 채움·Tile 캐시 절은 옛 Tilemap 전투 기준(2026-10 untangle 로 소멸)」 · `blueprint/README.md` §4 「뷰 · 연출」 행(오버레이 그릇) · CLAUDE.md 「스택」엔 Tilemap 언급 없음(확인만).
5. 이 spec README 상태 줄 → 완료.

## 구현 결과 (2026-10-07)

- 2a(`eb734630d`): `com.unity.2d.tilemap.extras` 제거(lock 은 `8f44ffe76` — `com.unity.2d.tilemap` 도 함께 소멸) · `TerrainTileRenderInfo.cs` 삭제 · `ApplyTilemapShadow` → `ApplyShadowCasting` · 「Tilemap 그리드/보드」 주석 3곳 · `lessons/03` 머리 이력 표시. `CameraPreset_TilemapRect` 는 에셋이 없었다(문서 속 이름뿐).
- 씬: 사용자가 일회용 MenuItem 을 눌러 `BoardPlane` 의 `Grid` 컴포넌트를 떼고 저장 → 씬 커밋. 스크립트는 지웠다(미커밋).
- 2b: 빌트인 `com.unity.modules.tilemap` 을 manifest 에서 뺐다 — `UnityEngine.Tilemaps` 를 쓰는 파일 0, 벤더·플러그인 사용 0. 동치 고정 테스트(`Grid` 필요)는 함께 삭제(1e-5 동치를 확인한 뒤). 모듈 제거 뒤 에디터 컴파일로 URP 2D 렌더러 등 다른 쪽이 안 깨지는지 본다 — 깨지면 2b 커밋만 되돌린다.
- `object-pipeline-map.md` · `map-stage-authoring.md` · `blueprint` 엔 Tile 에셋·Grid 컴포넌트를 현행처럼 말하는 줄이 없었다(확인만).

## 완료 기준

- [x] `rg -i "tilemap" Assets/_Project --type cs` → 이력 주석(`TilemapMapView` 인용 · spec 이름) 외 0 · `rg "tilemap" Packages/manifest.json` → 0
- [ ] 에디터 컴파일 0(모듈 제거 뒤) · Play 한 판은 사용자 몫
- [x] 커밋(2a · lock · 씬 · 2b)
