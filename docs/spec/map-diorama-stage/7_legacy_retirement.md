# 7 — 구 파이프라인 은퇴 (MapDocument 계열 · MapPainter · 구 테스트 처분)

## 목적

계약 1(«`MapDocument`/`MapPainter` 은퇴»)의 실행 unit. units 2~5 가 스테이지 경로를 검증 완료한 **뒤에만** 착수한다 — 그 전에 지우면 되돌릴 기준선이 없다. critic M-3(은퇴 미할당)·M-4(Assets lane 테스트가 `MapDocument` 직접 참조라 컴파일 사망) 대응.

## 변경 대상

- 삭제: `Assets/_Project/Editor/MapPainterWindow.cs` · `Assets/_Project/Scripts/Data/MapGrid/` 의 `MapDocument.cs`/`MapDocumentBuilder.cs`/`MapDocumentPool.cs`/`MapGridBattleAdapter.cs`/`MapConceptRules.cs`(페인터 경고 전용 — 계약 10 정정 근거) · `Assets/_Project/Scripts/Data/ObstaclePlacer.cs`(호출부는 unit 2 가 제거)
- 삭제(에셋): `Assets/_Project/Data/Maps/MapDocument_*.asset` 14종 + `MapDocumentPool.asset` — **.meta 짝 명시 add**
- 구 Assets lane 테스트 3파일 처분 (critic M-4): `MapDocumentPoolDevEntriesTests.cs` · `MultiGoalPoolSeparationTests.cs` · `LiveMapSpawnRouteAuthoringTests.cs` — 삭제가 아니라 **의미 승계 여부를 파일별 판정**: ④ 전선 가드는 스테이지 기준 재작성(계약 3 의 BlockZone 가드), 나머지는 스테이지 저작 린트(unit 1)가 대체하면 삭제
- 잔존 확인: `MapConnectivity.cs`(스테이지 경로가 계속 사용) · `MapGenerationFailedException.cs`(**DioramaMapBuilder.Assemble 이 던진다** — MapGrid 폴더에 있지만 삭제 금지, 이동만 허용) · `BattleMapBuilder.cs`(폴백 은퇴로 소비처 0 이면 함께 삭제) · `GeneratedMap`/`MapTileType`/`PlacementLayer`(접근 C 전까지 유지)

## 구현

삭제 전 각 파일의 **참조 전수 grep**(reflection 문자열 포함 — unit 3 교훈). csproj 명시 나열 특성상 `dotnet build` 는 CS2001 오탐 가능 — Unity 컴파일 기준으로 판정. `docs/reference/map-wave-balancing.md` 의 지형 규칙·`enemy-wave-integration` 스킬의 페인터 참조는 이 unit 에서 stale 표기만 하고 재작성은 밸런스 트랙 몫.

## 완료 기준

- [ ] compile + EditMode 두 lane + PlayMode 무회귀 (구 테스트 처분 반영 후)
- [ ] 전선 가드(④)의 스테이지 기준 후계 테스트 그린
- [ ] `MapDocument` 참조 0건 grep (문자열 접근 포함)
- [ ] 스테이지 경로만으로 에디터 Play 1판 완주

확인 2026-08-19 — 68파일 은퇴(MapPainter·MapDocument 계열 6종·MapConceptRules·WaypointPath·ObstaclePlacer·문서 테스트 10종·에셋 15종+meta). 잔존 확인: MapConnectivity/MapGenerationFailedException/MapPoolSelect/StructurePlacement ✓ · BattleMapBuilder 는 테스트 픽스처 빌더로 잔존(판정). 후계 기록: 전선 가드 ④ → DioramaMapBuilderTests BlockZone 폐쇄 · dev 슬롯 해석 → StagePoolDevEntriesTests(이관) · 거점 테스트(StructureSpawnAndBreach/StructureAuthoring)는 StructureMarker 후속에서 재작성. MapDocument 타입 참조 0(잔존 4건 = 역사 설명 텍스트). EditMode 두 lane 2397 그린 + 스모크 Passed. Ralph 러너 2종은 검증 채널로 브랜치 잔존 — 병합 전 삭제 판단은 사용자 몫.
