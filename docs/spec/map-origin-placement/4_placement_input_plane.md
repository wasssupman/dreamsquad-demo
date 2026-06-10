# 4 — 배치 입력 레이캐스트 평면을 board origin 기준으로

## 목적

스크린→월드 레이캐스트가 사용하는 지면 평면을 월드 원점이 아닌 board origin 에 맞춘다. translation 만 지원하므로 평면의 **높이(origin.y)** 와, 이어지는 셀 변환의 origin 산술이 핵심이다. 셀 변환은 BattleBridge 헬퍼를 경유시켜 입력 코드가 origin 산술을 직접 하지 않게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/PlacementInput.cs` (67, 71~73)
- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs` (127, 132~135)
- `Assets/_Project/Scripts/UI/SkillBar.cs` (172)
- `Assets/_Project/Scripts/Battle/Effects/HazardDebugMenu.cs` (73)
- `Assets/_Project/Scripts/Battle/Effects/BlockingHazardDebugMenu.cs` (75)

## 구현

BattleBridge 에 board origin 노출 (읽기 전용):

```csharp
public Vector3 BoardOrigin => new Vector3(_boardOrigin.x, _boardOrigin.y, _boardOrigin.z);
```

각 입력 지점의 평면을 origin 기준으로:

```csharp
var origin = bridge != null ? bridge.BoardOrigin : Vector3.zero;
var plane  = new Plane(Vector3.up, origin);   // 기존: new Plane(Vector3.up, Vector3.zero)
if (!plane.Raycast(ray, out float enter)) return ...;
var worldPos = ray.GetPoint(enter);
```

셀 변환은 **직접 `worldPos / tileSize` 하지 말고** BattleBridge 헬퍼 경유:

- `PlacementInput.cs:71-73`: 인라인 `FloorToInt(worldPos.x / _tileSize + .5f)` → `var c = bridge.DebugWorldToCell(worldPos); int tileX = c.x; int tileY = c.y;` (DebugWorldToCell 은 작업 2 에서 origin 반영됨)
- `DefenderDragPlacementController.TryScreenToPlacement`(132-135): 인라인 cell 계산을 `bridge.DebugWorldToCell(world)` 로 교체. `world = bridge.GridToWorldCenterVector(cell, 0f)` 은 그대로(이미 origin 반영).
- `SkillBar.cs:172` / 디버그 메뉴 2곳: 평면만 origin 기준으로. 셀이 필요하면 `bridge.DebugWorldToCell` 사용(디버그 메뉴는 이미 사용 중).

## 완료 기준

> ✅ 검증 2026-06-10 (MapView 가 실제로 (0.4, 2.7, -1.2) 로 옮겨진 버그 씬, Unity MCP Play) —
> - `BattleBridge.BoardOrigin == MapView.transform.position == (0.4, 2.7, -1.2)` (라이브 reflection)
> - `FlowFieldSingleton.origin == float3(0.4, 2.7, -1.2)` (ECS 전파 확인)
> - `CreateDefenderEntity(cell(6,0))` 직접 호출 → LocalTransform `float3(6.4, 3.2, -1.2)` = origin+cell*tileSize 정확히 일치 (수정 전이라면 (6,0.5,0) 월드원점 → 화면 밖). 테스트 엔티티 정리.
> - 입력 5파일 평면을 `bridge.BoardOrigin` 기준으로, 셀 변환을 `bridge.DebugWorldToCell` 경유로 통일. 컴파일 green, EditMode 307 passed, 콘솔 에러 0.
> - 커밋: 8b8ab6f
>
> 참고: 클릭/드래그 실제 손 입력 검증은 _running 게이트(placement→battle 전환)와 무관한 좌표 경로가 이미 결정적으로 확인됨. 손 입력 최종 확인은 사용자 실기/에디터 플레이로 권장.

- [ ] compile green.
- [ ] MapView 이동 상태 Play: 화면에서 **클릭한 타일과 실제 배치 셀이 일치**(엉뚱한/빈 셀 매핑 해소). 드래그 프리뷰도 커서 아래 타일에 스냅.
- [ ] 스킬 조준(SkillBar)·해저드 디버그 메뉴의 타일 선택도 옮겨진 맵에서 정확.
- [ ] origin=0 시 기존 동작과 동일.

## 주의

- `_tileSize` 인라인 계산이 남아 있으면 origin 누락의 잠재 버그원. 헬퍼 경유로 통일하는 것이 계약(README) 준수.
- 입력 코드는 `bridge.BoardOrigin` 만 읽고 origin 산술을 자체 구현하지 않는다.
