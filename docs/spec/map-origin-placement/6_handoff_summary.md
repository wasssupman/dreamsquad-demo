# 6 — Handoff Summary (map-origin-placement)

## Commit

- `8362150` 0 GridMath/MovementCellTrim origin 파라미터 + EditMode 테스트
- `a852904` 1 FlowFieldSingleton.origin + BattleBridge 캡처
- `fa69e34` 2 BattleBridge grid↔world 변환 origin 적용
- `9312dbb` 3 6개 ECS 시스템 origin 전파
- `8b8ab6f` 4 배치 입력 레이캐스트 평면 origin
- (이 spec 마무리 커밋) 5 backdrop origin + 캡처 시점 이동 + 문서/handoff

## Implemented

- board 월드 원점을 **MapView.transform.position** 단일 소스로 캡처 → 비주얼/시뮬레이션 좌표계 정렬.
- `GridMath.WorldToCell/CellToWorldCenter`, `MovementCellTrim.ClampToBoundary` 에 `float3 origin = default` 추가(기본값 zero → 기존 동작 보존).
- `FlowFieldSingleton.origin` 필드로 모든 Burst 시스템에 origin 전파(신규 싱글턴 없음).
- BattleBridge `_boardOrigin` 캡처(현재 `mapView.Initialize` 직후, Mount/BuildFlowField 보다 먼저), `GridToWorldCenter` 가산, `BoardOrigin` 공개 프로퍼티.
- Movement/Attack/MeteorResolution/ZoneApply/HazardCast/EffectSpawner 가 `field.origin` 전달.
- 입력 5파일(PlacementInput, DefenderDragPlacementController, SkillBar, Hazard/BlockingHazard DebugMenu) 평면을 `Plane(up, BoardOrigin)` + 셀 변환 `bridge.DebugWorldToCell` 경유로 통일.
- BackdropMounter.Mount `Vector3 origin` 파라미터 → 엣지 프롭 boardCenter origin 가산.

## Key Files

- `Assets/_Project/Scripts/Battle/Movement/GridMath.cs`, `MovementCellTrim.cs`
- `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`_boardOrigin`, `BoardOrigin`, `BuildMapForBattle`, `GridToWorldCenter`)
- 시스템: `Battle/Movement/MovementSystem.cs`, `Battle/Combat/AttackSystem.cs`, `MeteorResolutionSystem.cs`, `Battle/Effects/{ZoneApplySystem,HazardCastSystem,EffectSpawner}.cs`
- 입력: `Core/PlacementInput.cs`, `UI/DefenderDragPlacementController.cs`, `UI/SkillBar.cs`, `Battle/Effects/{HazardDebugMenu,BlockingHazardDebugMenu}.cs`
- `Presentation/Backdrop/BackdropMounter.cs`

## Verified

- 컴파일 green(각 단위), 콘솔 에러 0.
- EditMode **307 passed / 0 failed / 2 skipped**(skip 2개는 기존 Ignored, 무관).
- Play(MapView 실제 이동값 (0.4, 2.7, -1.2), Unity MCP):
  - `BoardOrigin == MapView.pos == (0.4,2.7,-1.2)`, `FlowFieldSingleton.origin` 동일.
  - `CreateDefenderEntity(cell(6,0))` → LocalTransform `(6.4, 3.2, -1.2)` = origin+cell*tileSize 정확. (수정 전이면 (6,0.5,0) 월드원점 → 화면 밖) 테스트 엔티티 정리함.
  - backdrop 엣지 프롭 12개 origin 반영 좌표(y=2.70, 보드 둘레)로 정렬.

## Notes

- **변환 범위 = 이동(translation)만.** 회전/스케일 비지원(비목표). origin 은 위치만.
- **origin 단일 소스 = MapView.transform.position**, 캡처는 BattleBridge 한 곳. 다른 코드가 mapView.transform 을 직접 읽어 별도 origin 만들지 말 것.
- origin=0(MapView 원점) 시 기존과 100% 동일 — 기본값 파라미터 설계 덕.
- origin 캡처는 **init 1회**. 플레이 도중 MapView 이동 추적은 범위 밖(후속 후보).

## Follow-up

- 손 입력(실제 클릭/드래그) 최종 확인은 사용자 실기/에디터 플레이 권장 — 좌표 경로는 결정적으로 검증됐으나 _running 게이트 너머의 실제 배치 플로우는 자동화로 못 밟음.
- 회전/스케일 지원이 필요해지면 별도 spec(InverseTransformPoint 전면화 + ECS LocalTransform 회전).
