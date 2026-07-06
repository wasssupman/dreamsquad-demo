# 3 — 스킬 aim/텔레그래프 범위를 격자 표시로 통일

## 목적

TilePoint 스킬(Meteor 등) 사용 흐름에서 범위 표시를 유닛 배치와 동일한 **격자 범위 표시**(`SetPlacementRange` 기계)로 통일한다:

1. **aim 중**(스킬 버튼 클릭 → 맵): 커서/포인터 아래 셀 기준 `skill.range` 격자를 실시간 추종 표시 — 유닛 드래그와 동일 UX.
2. **캐스트 후**: 기존 **빨간 쿼드 텔레그래프(`SpawnMeteorWarningVisual`) 삭제**(사용자 확인: 축도 잘못됨). 착탄 예고 기능은 격자 표시를 `warningSec` 동안 착탄 셀에 고정하는 것으로 승계.

## 변경 대상

- `Assets/_Project/Scripts/UI/SkillBar.cs` — aim 루프(`HandleAimInput`)에서 포인터 셀 → 범위 표시 갱신, `ExitAimMode` 에서 해제
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SpawnMeteorWarningVisual` 삭제 + 캐스트 성공 시 텔레그래프 고정 표시(warningSec 후/impact 시 해제) + 범위 표시 위임 API(기존 `TilemapMapView.SetPlacementRange`/`ClearPlacementRange` 재사용)
- (선택) `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `SetPlacementRange` 에 `includeCenter` 파라미터 (스킬 AOE 는 중심 셀도 피해 범위 — 배치는 중심 제외였음)

## 구현

1. **aim 추종**: `SkillBar.Update` 의 aim 분기에서 매 프레임 포인터 → `DebugWorldToCell` → 셀이 바뀌면 `SetPlacementRange(cell, GridMath.RangeToTiles(skill.range))`. TilePoint 스킬만(DefenderUnit 타겟 스킬 제외). Portal 은 range 개념이 달라 v1 제외(현행 유지).
2. **해제 경로 전수**: 캐스트 성공 · aim 취소(같은 버튼 재탭) · 배치 시작으로 인한 aim 강제 해제 · 비용 부족 ExitAimMode — 전부 `ClearPlacementRange` 경유. 잔상 금지.
3. **텔레그래프 승계**: 캐스트 성공 시 착탄 셀에 격자 고정 → `warningSec` 경과(또는 impact 이벤트) 시 해제. 빨간 쿼드 생성 코드와 `RuntimeMaterialFactory.CreateTransparent(red)` 호출부 삭제.
4. **색 채널**: v1 은 배치와 동일한 `rangeTile`/`rangeColor`(노랑) 재사용 — 사용자 요구 "유닛과 마찬가지로". 스킬 전용 색(웜/쿨 구분)은 README 후속 후보의 색 채널 분리 시점에.
5. **배치 프리뷰와의 동시성**: aim 진입이 배치를 취소(`SelectedDefender=null`)하는 기존 규칙이 있어 두 소비자가 동시에 `_rangeTilemap` 을 쓰는 경우는 없음 — 그 불변식에 의존함을 주석으로 명시.

## 완료 기준

- Play: 스킬 버튼 클릭 → 맵에서 커서 이동 시 노란 격자가 셀 단위로 추종. 캐스트 → 착탄 셀에 고정 → impact 와 함께 소멸. 빨간 쿼드 미출현.
- aim 취소/배치 전환/비용 부족 각 경로에서 격자 잔상 0.
- 유닛 드래그 배치 범위 표시 무회귀(같은 tilemap 재사용 충돌 없음).
- compile + 기존 EditMode GREEN.

확인 2026-07-06 — MCP Play 실측: aim 격자 25셀(5², 중심 포함)·스크린샷 축 정상 · 캐스트→owner=SkillTelegraph 전환·**aim clear 가 텔레그래프 못 지움(게이트 PASS)** · impact→격자 0·owner=None · 빨간 쿼드 미출현 · 콘솔 클린. hover 커서 추종의 실제 마우스 경로는 사용자 플레이에서 자연 확인(같은 코드 경로를 직접 호출로 검증).
