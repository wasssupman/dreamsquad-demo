# 4 — 골/스폰 마커 뷰 재귀속 (앵커 · 균열 · 붕괴)

## 목적

`TilemapMapView` 의 구조물 프랍 경로가 소유한 세 가지 뷰 책임을 마커로 이관한다: ① 튜토리얼 포커스 앵커(`TryGetGoalVisualAnchor`/`TryGetSpawnVisualAnchor`) ② 골 균열 단계(`SetGoalCrack` — 프랍 틴트/스케일) ③ 붕괴 표시(`MarkGoalCollapsed`). 디오라마에서 골/스폰의 "몸"은 스테이지 프리팹에 저작된 프랍이므로, 연출 훅도 그 프랍이 갖는 것이 맞다. **골 HP·붕괴 판정은 심 소유 그대로** — 여기는 연출만.

## 변경 대상

- `Assets/_Project/Scripts/Core/MapStage/GoalMarker.cs` · `SpawnMarker.cs` — 뷰 훅 추가. **주의 (critic Minor 3)**: `GoalMarker` 에 안정도 필드를 넣지 않는다 — 골 HP 는 `AttackDeck.goalStabilityMax` 단독 소유(설계문서 표의 "(+ 안정도 파라미터)" 표기는 오기)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SetGoalCrack` 호출(:6058) · `MarkGoalCollapsed` 호출(:6171) 재배선 + 마커 등록부
- `Assets/_Project/Scripts/UI/Tutorial/FirstSessionTutorialController.cs` — **`VisualPlan` 의존 전체 교체 (critic M-6)**: 앵커(:169, :183)만이 아니라 `mapView.VisualPlan`(:161)·`plan.spawns`(:164-168)·`plan.goal`(:181/185) 전부 — 스폰/골 정보는 브리지 경유 마커 소스로. `BattleScene.unity:3408` `mapView` 참조 해제 포함
- 은퇴 (unit 3 에서 이관): `TilemapMapView` 의 `InstantiateStructureProps`·`_goalPropsByCell`·앵커/균열/붕괴 API + **`BoardVisualPlan` 계열 7파일** (`BoardVisualPlan`/`Builder`/`BoardVisualCell`/`BoardVisualRegion`/`BoardZoneType`/`BoardDecorAnchor` — 단 `BoardDecorAnchorType` 은 50개 PropData 에셋 직렬화 잔존으로 **파일 유지**, critic M-7) + `PropInstanceUtil` 시그니처 축소(`plan.gridSize` → `gridSize`)

## 구현

- `GoalMarker`/`SpawnMarker` 에 `visualRoot`(비면 자기 자신) + 앵커 산출(렌더러 바운즈 중심, 폴백 = 셀 중심 — 현행 의미 승계).
- 균열/붕괴는 현행과 동일한 표현(틴트/스케일 단계)을 마커의 작은 뷰 메서드로 이식 — 프랍 교체 아트가 없다는 현행 제약도 승계. 상태는 매치 수명: 스테이지 인스턴스가 매 판 새로 뜨므로 재빌드=원복(현행 `_goalPropsByCell.Clear()` 와 동형).
- `BattleBridge` 는 빌드 시 스테이지에서 마커 목록을 받아 셀→마커 사전을 만든다(기존 `_goalPropsByCell` 의 후계, 브리지 소유). 심 이벤트(안정도 단계/붕괴) 드레인은 무변경 — 소비처만 사전 교체.
- 튜토리얼은 브리지 경유로 앵커를 묻는다 — `TilemapMapView` 직접 참조 제거.

## 완료 기준

- [ ] compile + 튜토리얼 포커스가 골/스폰 프랍 위에 정확히 앉는다 (에디터 Play)
- [ ] 골 피격 → 균열 단계 진행, 안정도 0 → 붕괴 표시가 스테이지 프랍에서 재생 (Play 육안)
- [ ] 재판(리트라이) 시 균열/붕괴 상태 원복 확인
- [ ] `TilemapMapView` 에 구조물 프랍 코드 잔존 0 (grep `_goalPropsByCell`)

확인 2026-08-18 — EditMode 두 lane 2524 그린 + PlayMode 스모크 Passed. 마커 뷰 훅(MarkerVisual 헬퍼)·브리지 등록부·튜토리얼 브리지 앵커 교체(직렬화 bridge 필드 + FindAnyObjectByType 폴백 — 씬 배선 불요) 완료. mapView 필드는 효과 타일 힌트(오버레이 도메인) 전용으로 잔존. BoardVisualPlan 계열 10파일 + PropInstanceUtil/PropPlacement 은퇴 (BoardDecorAnchorType 은 50개 PropData 에셋 직렬화로 파일 유지).
