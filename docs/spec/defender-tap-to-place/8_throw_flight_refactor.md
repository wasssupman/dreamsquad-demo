# 8 · 던지기 비행 코드 리팩토링

## 목적

Unit 6의 Play 통과 동작과 SO 튜닝 계약을 그대로 보존하면서, 죽은 상태와 반복 좌표 계산을 제거해
`RunSimulatedDrag`의 비행→정착 흐름을 한 번에 감사할 수 있게 만든다.

검증 질문: 코드가 단순해진 뒤에도 근/원거리 던지기 속도·궤적·정착·확정 결과가 Unit 6과 같은가?

## 변경 대상

- `Assets/_Project/Scripts/UI/DefenderDragPlacementController.cs`
- `docs/spec/defender-tap-to-place/6_throw_arrival_settle.md`
- `docs/spec/defender-tap-to-place/README.md`

## 구현

### 죽은 상태 제거

- `_simulatedSettling`은 Unit 6 rev 이후 읽는 곳 없이 set/reset만 남았으므로 필드와 모든 대입을 삭제한다.
- 비행/정착 추종 분기는 계속 `_simulatedDrag` 하나가 담당한다.
- Unit 6 문서의 `_simulatedSettling` 계약도 실제 구현에 맞게 제거한다.

### 좌표·설정 흐름 명료화

- 코루틴 시작에서 `cfg = Cfg`를 잡고 해당 실행의 모든 SO 참조를 같은 로컬로 통일한다.
- `unitLift = boardN * previewHeight`, `ringLift = camUp * totalDrop`을 한 번 계산한다.
- 시작 위치, 매 프레임 목표, 최종 위치를 각각 `feet + unitLift/ringLift` 형태로 통일해
  선택 타일 발점에서 파생된다는 불변식을 코드에 직접 드러낸다.
- 황금비 숫자는 메서드 로컬 `const`로 이름을 부여하고, 제어점/거리 로컬 이름을 역할 중심으로 정리한다.
- 탭 경로도 `SmoothDamp`를 쓰므로 남은 “스프링 타깃” 주석을 “추종 목표”로 정정한다.

### 과잉 설계 금지

- 세션 세대 가드와 `yield` 순서를 한곳에서 읽을 수 있도록 코루틴은 분할하지 않는다.
- 소비처 하나뿐인 path struct/helper/interface를 만들지 않는다.
- 카메라 전용 `CameraComposeMath`에 의존하거나 범용 이징 계층을 새로 만들지 않는다.
- `KeyringSim.CubicBezier`, OutCubic 공식, SO 필드/값, 실제 드래그 경로는 변경하지 않는다.
- 공유 `DragSwaySettings.asset`의 로컬 dirty 튜닝은 수정·스테이징하지 않는다.

## 완료 기준

- 컴파일 오류 0, scoped `git diff --check` 통과.
- 기존 EditMode `KeyringSimTests` 및 전체 `Wassup.Tests.EditMode` 실패 0.
- Play 근/원거리에서 Unit 6과 같은 빠른 출발→감속 착지, 위치 점프 없는 확정.
- 공유 SO의 기존 dirty 튜닝을 보존하고 본 작업 diff·스테이징에서 제외, 다른 세션 파일 변경 0.
- 사용자 Play 확인 후 완료 표기·커밋·푸시.
