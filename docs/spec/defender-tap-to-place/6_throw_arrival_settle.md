# 6 · 던지기 비행과 도착 정착

## 목적

비행 종료 시 위치 점프를 없애고, 상승→하강 던지기와 착지 팝으로 배치 인과를 명확히 한다.

검증 질문: 가까운/먼 타일 모두 목표가 명확하고, 프리뷰 안착 후 위치 점프 없이 배치되는가?

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `Assets/_Project/Scripts/UI/KeyringSim.cs`
- `Assets/_Project/Scripts/Data/DragSwaySettings.cs` · 대응 에셋
- `Assets/_Project/Tests/EditMode/KeyringSimTests.cs`

## 구현

### 최종 좌표

- `endFeet = GridCellToViewCenter(targetCell)`를 불변 기준으로 두고, 여기서 `finalUnitTarget`과
  `finalRing`을 파생한다. 비행·정착 중 두 값과 `_simFocusCell=targetCell`은 고정한다.

### 던지기 비행

- `KeyringSim.CubicBezier(a,c1,c2,b,t)` 추가. endpoint 정확.
- 던지기 제어점은 `Vector2(전진, 높이 비율)` SO로 두고 상승/하강 접선을 각각 만든다.
- 좌우 변주는 두 제어점에 같은 오프셋을 적용해 중간에만 휘고 선택 타일로 복귀한다.
- 시간 진행률은 `OutCubic`으로 매핑해 빠르게 출발하고 감속 착지한다.
- 거리 비례 비행시간은 유지한다. 3차 곡선용 시작값: launch `(0.18,1)`, landing `(0.72,0.22)`,
  높이 `0.34`, 좌우 `0.12`. 다른 dirty 값은 보존한다.

### 도착 정착·확정

- 비행 완료 후 `finalRing/finalUnitTarget`을 고정하고 `_simulatedSettling=true`; 즉시 커밋하지 않는다.
- 탭 시뮬은 비행부터 정착까지 `SmoothDamp(..., tapFollowSmoothTime)` 하나로 연속 추종해
  도착 직전 따라잡기 가속을 막는다. 실제 드래그 스프링은 불변이다.
- 매 프레임 직접 판정한다.
  - `Distance(_unitPosWorld, finalUnitTarget) <= tapSettleDistance`
  - `_unitVelWorld.magnitude <= tapSettleSpeed`
- 충족/시간초과 시 최종값으로 정렬하고 착지 팝→`CommitPlacementAt`을 같은 프레임에 호출한다.
- 시작값: 거리 `0.06`, 속도 `0.4`, follow smooth time `0.06s`, 최대 `0.28s`. 네 값만 SO에 추가한다.
- 전체에 `_sessionGen`/active 가드 적용. `_simulatedSettling`은 `CleanupSession`에서 해제한다.

## 완료 기준

- 컴파일 클린. EditMode 3차 베지어 시작/끝/중간점 통과.
- Play 근/중/원거리: 빠른 출발→상승→하강→감속 착지가 읽히고, 도착 30~40% 전 추종 가속이 없다.
- 정상 정착이 주 경로이며 도착 후 `0.28s` 안에 확정된다. 타임아웃 경로도 위치 점프가 없다.
- 목표 타일은 탭 즉시 표시되고 착지 시 팝한다.
- 드래그, 비용, 포커스, 컷신, 방향 지정 회귀 없음.

사용자 Play 확인: **통과 2026-07-18** · 구현 커밋 `043aa694`
