# 5. Handoff Summary — legacy-render-removal 종료

## Commit

- unit 0 `ab13b8d` — 공용 프랍 헬퍼 MapView → PropInstanceUtil 추출
- unit 1 `11816ff` — 입력/배치 계층 dead MapView 참조 제거
- unit 2 `73a2efd` — Legacy 렌더 서브시스템 + 시즌 백드롭 통삭제
- unit 3 `82a5992` — BoardViewMode.Legacy3D 모드 제거
- unit 4 `ba3366b` — MapThemeData LEGACY 43 필드 + TerrainSurfaceVariant 제거

## Implemented

- MapView/TerrainSurfaceSelector/TerrainTileRuleResolver + 전용 테스트 + BattleScene_Legacy3D 씬/BuildSettings 항목 완전 삭제 (총 ~6,300줄 순삭)
- 백드롭 서브시스템 통삭제 (사용자 결정 — BackdropMounter/AnchorTable/SeasonBackdropData/CustomEditor/asset). 시즌 시스템(SeasonRuntime/mapTheme)은 ACTIVE 유지
- `BoardViewMode` 는 TilemapRect=1/Iso=2 만 (직렬화 안정 유지). `BoardSpace` identity 폴백 제거 — null-grid Configure = LogError+무시 (가드 테스트 추가)
- BattleBridge: UseTilemapView 상수 접기, 캐릭터 스케일/틸트 미러 = tilemap 값 일원화, dead ECS 헬스바 렌더어레이 경로 정리
- **headless sim 빌드 계약**: view-init/BoardSpace.Configure 는 view 부재 시 조용히 skip, 씬 오배선 감지는 Awake null 체크 (EditMode 헤드리스 테스트 7건이 의존)
- MapThemeData 는 ACTIVE 24 필드만 잔존, forest/desert.asset stale 키 −225줄

## Key Files

- `Assets/_Project/Scripts/Core/{BoardViewMode,BoardSpace,PropInstanceUtil,TilemapMapView}.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Data/MapThemeData.cs` + `Assets/_Project/Map/Theme/{forest,desert}/*.asset`
- `Assets/_Project/Tests/EditMode/{BoardSpaceTests,TilemapMapViewTests}.cs`

## Verified

- 각 unit: compile 0 · sweep grep 0건 · EditMode 434개 기존실패 1건(ObstaclePlacer, HEAD 계승) 외 PASS · Tilemap Play 무회귀 스크린샷(`Assets/Screenshots/legacy_removal_u{0..4}_*.png`)
- unit 3 은 전투 풀루프(배치→웨이브→교전→드림캐쳐 prep→결과창) Play 확인

## Notes

- **되돌리면 안 되는 것**: headless silent-skip (테스트 계약) · BoardViewMode 명시값 1/2 · `_boardOrigin`=zero 고정 계약
- 병행 세션 커밋 분리 이력: GA projectile 오염 → 히스토리 분리(`51955bc`), unit-health-display ⓪ 헬스바 삭제 → `74b3807` (BattleBridge hunk 분리). unit 2/3 커밋 단독 compile 유지 확인됨
- `BattleScene.unity` 에 구 MapView GameObject(missing-script) + `BattleBridge.mapView` stale serialized 참조 잔존 — 씬이 사용자 WIP dirty 라 유보. 무해(로드 경고 수준)
- season asset 2종의 stale `backdrop:` 키 잔존 — 무해, 다음 재직렬화 때 자연 소멸

## Follow-up

- → `docs/spec/README.md` Follow-up Backlog "렌더 파이프라인" 그룹: BattleScene MapView 잔재 씬 청소 [S]
- ~~"Particle Velocity curves must all be in the same mode" 콘솔 플러딩~~ — 해결 `63c7240`. 원인은 GA 가 아니라 `HazardVisual_Poison` 파티클 velocity 모드 혼용이었음
